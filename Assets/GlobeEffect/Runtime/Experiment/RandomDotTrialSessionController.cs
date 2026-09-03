using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    public enum RandomDotSessionState
    {
        Idle,
        InterTrial,
        WaitingForFixation,
        PresentingMotion,
        WaitingForResponse,
        Completed,
        Aborted
    }

    /// <summary>
    /// Führt den dynamischen Random-Dot-Test mit festen k-Werten aus. Nach
    /// stabiler Fixation bewegt Unity das kopffeste Punktfeld für eine feste
    /// Dauer. Erst danach antwortet die Versuchsperson "konkav" oder "konvex".
    /// Ein Fixationsbruch macht die Präsentation ungültig und hängt dieselbe
    /// Bedingung hinten an die Warteschlange.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class RandomDotTrialSessionController : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [SerializeField]
        private RandomDotKeyboardController keyboardController;

        [SerializeField]
        private RandomDotHeadSweepMonitor sweepMonitor;

        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private RandomDotFixationMonitor fixationMonitor;

        [Header("Sitzung")]
        [SerializeField]
        [Tooltip("Pseudonymisierte Versuchsperson-ID; keine Klarnamen verwenden.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "random_dot_k_pilot";

        [SerializeField]
        [Tooltip("Gleicher Seed und gleiche Inspector-Werte ergeben dieselbe Reihenfolge.")]
        private int randomSeed = 20260901;

        [SerializeField]
        private int dotSeedBase = 24680;

        [SerializeField]
        [Tooltip("Leer = measurements-Ordner direkt im Unity-Projekt.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Trialplan")]
        [SerializeField]
        [Tooltip("Ein oder mehrere Winkeldurchmesser der runden Öffnung.")]
        private List<float> angularDiametersDegrees = new() { 70f };

        [SerializeField]
        [Tooltip("Both Eyes, Left Eye Only oder Right Eye Only.")]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [SerializeField]
        [Tooltip("Vorläufige feste k-Pilotwerte. Die Person verändert k nicht selbst.")]
        private List<float> stimulusKValues = new()
        {
            0f,
            0.3f,
            0.5f,
            0.6f,
            0.7f,
            0.85f,
            1f
        };

        [SerializeField]
        [Tooltip("m bleibt ein unabhängiger Instrumentparameter. Für einen Versuch normalerweise nur einen festen Wert eintragen.")]
        private List<float> magnifications = new() { 10f };

        [SerializeField]
        [Tooltip("SimulatedYaw ist die kontrollierte Hauptbedingung. HeadTracked bleibt optional.")]
        private List<RandomDotMotionMode> motionModes = new()
        {
            RandomDotMotionMode.SimulatedYaw
        };

        [SerializeField, Min(1)]
        [Tooltip("Wie oft jede Kombination aus FOV, Auge, k, m und Bewegungsmodus vorkommt.")]
        private int repetitionsPerCondition = 3;

        [Header("Simulierter Schwenk")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Dauer, für die das bewegte Punktfeld sichtbar ist.")]
        private float motionDurationSeconds = 4f;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Maximaler Winkel zu jeder Seite.")]
        private float sweepAmplitudeDegrees = 5f;

        [SerializeField, Range(0.1f, 60f)]
        [Tooltip("Gleichbleibende Winkelgeschwindigkeit zwischen den Umkehrpunkten.")]
        private float sweepSpeedDegreesPerSecond = 5f;

        [Header("Fixation und Wiederholung")]
        [SerializeField]
        [Tooltip("Vor dem Punktfeld wird stabile Fixation verlangt und während der Bewegung überwacht.")]
        private bool requireFixation = true;

        [SerializeField, Min(0f)]
        [Tooltip("Maximal erlaubte zusammenhängende Zeit außerhalb des Fixationsbereichs.")]
        private float maximumOffTargetSeconds = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximal erlaubte zusammenhängende Zeit ohne gültige Blickdaten.")]
        private float maximumInvalidGazeSeconds = 0.2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Ältere Eye-Tracking-Samples gelten als fehlende Daten.")]
        private float maximumGazeSampleAgeSeconds = 0.1f;

        [SerializeField, Min(0)]
        [Tooltip("0 = unbegrenzt wiederholen. Ein positiver Wert setzt eine Obergrenze pro geplantem Trial.")]
        private int maximumAttemptsPerTrial;

        [Header("Ablauf")]
        [SerializeField, Min(0f)]
        private float interTrialSeconds = 0.5f;

        [Header("Tasten")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        private Key abortSessionKey = Key.F6;

        [Header("Laufzeitstatus (nur Anzeige)")]
        [SerializeField]
        private RandomDotSessionState sessionState = RandomDotSessionState.Idle;

        [SerializeField]
        private int currentTrialNumber;

        [SerializeField]
        private int totalTrials;

        [SerializeField]
        private int validTrialsCompleted;

        [SerializeField]
        private int presentationCount;

        [SerializeField]
        private string activeSessionFolder = string.Empty;

        private IReadOnlyList<RandomDotTrial> trialPlan;
        private RandomDotTrialQueue trialQueue;
        private RandomDotTrial currentTrial;
        private RandomDotExperimentFiles experimentFiles;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private double stimulusEndUnitySeconds;
        private float currentOffTargetSeconds;
        private float currentInvalidGazeSeconds;
        private float longestOffTargetSeconds;
        private float longestInvalidGazeSeconds;
        private Coroutine interTrialCoroutine;
        private Coroutine motionCoroutine;
        private bool eventsSubscribed;

        public event Action<RandomDotTrial> TrialStarted;
        public event Action<RandomDotTrialResult> TrialEnded;
        public event Action<RandomDotSessionState> SessionFinished;

        public RandomDotSessionState SessionState => sessionState;
        public RandomDotTrial CurrentTrial => currentTrial;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public int ValidTrialsCompleted => validTrialsCompleted;
        public int PresentationCount => presentationCount;
        public int PendingTrialCount => trialQueue?.Count ?? 0;
        public float CurrentOffTargetSeconds => currentOffTargetSeconds;
        public float CurrentInvalidGazeSeconds => currentInvalidGazeSeconds;
        public string ActiveSessionFolder => activeSessionFolder;
        public bool RequireFixation => requireFixation;
        public bool ResponseKeysSwapped =>
            keyboardController != null && keyboardController.SwapResponseKeys;
        public bool IsSessionActive =>
            sessionState == RandomDotSessionState.InterTrial ||
            sessionState == RandomDotSessionState.WaitingForFixation ||
            sessionState == RandomDotSessionState.PresentingMotion ||
            sessionState == RandomDotSessionState.WaitingForResponse;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void Start()
        {
            if (autoStartOnPlay)
            {
                StartSession();
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!IsSessionActive && keyboard[startSessionKey].wasPressedThisFrame)
                {
                    StartSession();
                    return;
                }

                if (IsSessionActive && keyboard[abortSessionKey].wasPressedThisFrame)
                {
                    AbortSession("ManualAbort");
                    return;
                }
            }

            if (sessionState == RandomDotSessionState.WaitingForFixation &&
                fixationMonitor != null && fixationMonitor.RequirementMet)
            {
                PresentCurrentTrial();
                return;
            }

            if (sessionState == RandomDotSessionState.PresentingMotion &&
                requireFixation)
            {
                MonitorFixationDuringMotion();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }
        }

        public void Configure(
            RandomDotFieldStimulus randomDotStimulus,
            RandomDotKeyboardController randomDotKeyboard,
            RandomDotHeadSweepMonitor headSweepMonitor,
            EyeTrackingToolbox toolbox,
            RandomDotFixationMonitor randomDotFixationMonitor)
        {
            UnsubscribeEvents();
            stimulus = randomDotStimulus;
            keyboardController = randomDotKeyboard;
            sweepMonitor = headSweepMonitor;
            eyeTrackingToolbox = toolbox;
            fixationMonitor = randomDotFixationMonitor;
            if (isActiveAndEnabled)
            {
                SubscribeEvents();
            }
        }

        public bool StartSession()
        {
            if (IsSessionActive)
            {
                Debug.LogWarning("Eine Random-Dot-Sitzung läuft bereits.", this);
                return false;
            }

            ResolveReferences();
            SubscribeEvents();
            if (stimulus == null || keyboardController == null || sweepMonitor == null)
            {
                Debug.LogError(
                    "Random-Dot-Stimulus, Tastatursteuerung und Sweep-Monitor müssen zugewiesen sein.",
                    this);
                return false;
            }

            if (requireFixation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Fixationskontrolle ist aktiv, aber der Random-Dot Fixation Monitor fehlt.",
                    this);
                return false;
            }

            try
            {
                trialPlan = RandomDotTrialPlanner.CreateRandomizedPlan(
                    angularDiametersDegrees,
                    eyePresentations,
                    stimulusKValues,
                    magnifications,
                    motionModes,
                    repetitionsPerCondition,
                    randomSeed,
                    dotSeedBase);
                trialQueue = new RandomDotTrialQueue(trialPlan);

                DateTime sessionStartUtc = DateTime.UtcNow;
                string resolvedRoot = ExperimentOutputPath.Resolve(outputRoot);
                experimentFiles = RandomDotExperimentFiles.Create(
                    resolvedRoot,
                    participantId,
                    sessionLabel,
                    sessionStartUtc,
                    randomSeed);
                experimentFiles.WritePlan(trialPlan);
                activeSessionFolder = experimentFiles.SessionFolder;
                StartEyeTracking(sessionStartUtc);
            }
            catch (Exception exception)
            {
                sessionState = RandomDotSessionState.Aborted;
                Debug.LogError(
                    "Random-Dot-Sitzung konnte nicht gestartet werden: " +
                    exception.Message,
                    this);
                return false;
            }

            StopPendingCoroutines();
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            validTrialsCompleted = 0;
            presentationCount = 0;
            sessionState = RandomDotSessionState.InterTrial;

            Debug.Log(
                $"Random-Dot-k-Sitzung gestartet: {totalTrials} gültige Trials geplant.\n" +
                activeSessionFolder,
                this);
            BeginNextAttempt();
            return true;
        }

        public void AbortSession(string reason = "ManualAbort")
        {
            if (!IsSessionActive)
            {
                return;
            }

            StopPendingCoroutines();
            if ((sessionState == RandomDotSessionState.PresentingMotion ||
                 sessionState == RandomDotSessionState.WaitingForResponse) &&
                currentTrial != null && experimentFiles != null)
            {
                if (stimulusEndUnitySeconds <= trialStartUnitySeconds)
                {
                    stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
                }

                TryAppendResult(CaptureCurrentResult(
                    CheckerboardCurvatureResponse.None,
                    validForAnalysis: false,
                    "aborted:" + (reason ?? string.Empty)));
            }

            WriteMarker("SessionAborted;task=random_dot_k;reason=" +
                CheckerboardExperimentFiles.SanitizeIdentifier(reason, "unspecified"));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = RandomDotSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void BeginNextAttempt()
        {
            interTrialCoroutine = null;
            if (trialQueue == null || !trialQueue.TryTakeNext(out currentTrial))
            {
                CompleteSession();
                return;
            }

            presentationCount++;
            currentTrialNumber = validTrialsCompleted + 1;
            stimulus.Hide();
            stimulus.SetAngularDiameter(currentTrial.AngularDiameterDegrees);
            stimulus.SetMagnification(currentTrial.Magnification);
            stimulus.SetEyePresentation(currentTrial.EyePresentation);
            stimulus.SetMotionMode(currentTrial.MotionMode);
            stimulus.SetMerlitzK(currentTrial.StimulusK);
            stimulus.SetSimulatedSweep(
                sweepAmplitudeDegrees,
                sweepSpeedDegreesPerSecond);
            stimulus.SetSweepDirection(currentTrial.SweepDirection);
            stimulus.ConfigurePointField(
                stimulus.DotCount,
                currentTrial.DotSeed,
                stimulus.WorldCoverageDiameterDegrees);
            stimulus.PlaceAroundObserver();

            sweepMonitor.ResetForTrial();
            fixationMonitor?.ResetFixationWindow();
            ResetFixationCounters();
            stimulusEndUnitySeconds = 0d;

            if (requireFixation)
            {
                sessionState = RandomDotSessionState.WaitingForFixation;
                stimulus.ShowFixationOnly();
                WriteMarker(string.Format(
                    CultureInfo.InvariantCulture,
                    "FixationAcquisitionStart;task=random_dot_k;sequence={0};attempt={1}",
                    currentTrial.SequenceIndex,
                    currentTrial.AttemptNumber));
            }
            else
            {
                PresentCurrentTrial();
            }
        }

        private void PresentCurrentTrial()
        {
            if (currentTrial == null)
            {
                return;
            }

            fixationMonitor?.ResetFixationWindow();
            ResetFixationCounters();
            sweepMonitor?.ResetForTrial();
            stimulus.RestartMotionPhase();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulusEndUnitySeconds = 0d;
            sessionState = RandomDotSessionState.PresentingMotion;

            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show();
            TrialStarted?.Invoke(currentTrial);
            motionCoroutine = StartCoroutine(EndMotionAfterDuration());

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Random-Dot-Trial {0}/{1}, Präsentation {2}: k={3:F3}, m={4:F2}, " +
                "{5}, zuerst {6}, Versuch {7}. Fixationskreuz anschauen; Antwort folgt nach der Bewegung.",
                currentTrialNumber,
                totalTrials,
                presentationCount,
                currentTrial.StimulusK,
                currentTrial.Magnification,
                currentTrial.MotionMode,
                currentTrial.SweepDirection,
                currentTrial.AttemptNumber),
                this);
        }

        private IEnumerator EndMotionAfterDuration()
        {
            yield return new WaitForSecondsRealtime(motionDurationSeconds);
            motionCoroutine = null;
            EndMotionPresentation();
        }

        private void EndMotionPresentation()
        {
            if (sessionState != RandomDotSessionState.PresentingMotion ||
                currentTrial == null)
            {
                return;
            }

            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulus.Hide();
            sessionState = RandomDotSessionState.WaitingForResponse;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "StimulusEnded;task=random_dot_k;sequence={0};attempt={1};duration_s={2:F4}",
                currentTrial.SequenceIndex,
                currentTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds));

            string responseHint = ResponseKeysSwapped
                ? "Links = konvex, rechts = konkav."
                : "Links = konkav, rechts = konvex.";
            Debug.Log("Random-Dot-Antwort: " + responseHint, this);
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            if (sessionState != RandomDotSessionState.WaitingForResponse ||
                currentTrial == null ||
                response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            RandomDotTrialResult result = CaptureCurrentResult(
                response,
                validForAnalysis: true,
                "valid");
            if (!TryAppendResult(result))
            {
                return;
            }

            validTrialsCompleted++;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialResponse;task=random_dot_k;sequence={0};attempt={1};response={2};response_s={3:F4};valid=1",
                currentTrial.SequenceIndex,
                currentTrial.AttemptNumber,
                response,
                result.ResponseTimeSeconds));
            TrialEnded?.Invoke(result);
            FinishAttemptAndScheduleNext();
        }

        private void MonitorFixationDuringMotion()
        {
            if (fixationMonitor == null)
            {
                InvalidateCurrentTrial("missing_fixation_monitor");
                return;
            }

            float delta = Time.unscaledDeltaTime;
            bool sampleRecent = fixationMonitor.HasRecentSample(
                maximumGazeSampleAgeSeconds);

            if (!sampleRecent || !fixationMonitor.CurrentSampleValid)
            {
                currentInvalidGazeSeconds += delta;
                currentOffTargetSeconds = 0f;
            }
            else if (!fixationMonitor.IsInsideTolerance)
            {
                currentOffTargetSeconds += delta;
                currentInvalidGazeSeconds = 0f;
            }
            else
            {
                currentOffTargetSeconds = 0f;
                currentInvalidGazeSeconds = 0f;
            }

            longestOffTargetSeconds = Mathf.Max(
                longestOffTargetSeconds,
                currentOffTargetSeconds);
            longestInvalidGazeSeconds = Mathf.Max(
                longestInvalidGazeSeconds,
                currentInvalidGazeSeconds);

            if (currentOffTargetSeconds > maximumOffTargetSeconds)
            {
                InvalidateCurrentTrial("off_target");
            }
            else if (currentInvalidGazeSeconds > maximumInvalidGazeSeconds)
            {
                InvalidateCurrentTrial("invalid_gaze_data");
            }
        }

        private void InvalidateCurrentTrial(string reason)
        {
            if (sessionState != RandomDotSessionState.PresentingMotion ||
                currentTrial == null)
            {
                return;
            }

            StopMotionCoroutine();
            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            RandomDotTrial invalidTrial = currentTrial;
            RandomDotTrialResult result = CaptureCurrentResult(
                CheckerboardCurvatureResponse.None,
                validForAnalysis: false,
                "invalid_fixation:" + reason);
            if (!TryAppendResult(result))
            {
                return;
            }

            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialInvalid;task=random_dot_k;sequence={0};attempt={1};reason={2};off_target_s={3:F4};invalid_gaze_s={4:F4}",
                invalidTrial.SequenceIndex,
                invalidTrial.AttemptNumber,
                reason,
                longestOffTargetSeconds,
                longestInvalidGazeSeconds));
            TrialEnded?.Invoke(result);

            bool limitReached = maximumAttemptsPerTrial > 0 &&
                invalidTrial.AttemptNumber >= maximumAttemptsPerTrial;
            if (limitReached)
            {
                stimulus.Hide();
                WriteMarker("SessionAborted;task=random_dot_k;reason=maximum_repeat_attempts_reached");
                StopEyeTrackingRecording();
                currentTrial = null;
                sessionState = RandomDotSessionState.Aborted;
                SessionFinished?.Invoke(sessionState);
                Debug.LogError(
                    "Die maximale Zahl an Wiederholungen wurde erreicht. Die Sitzung wurde beendet.",
                    this);
                return;
            }

            RandomDotTrial repeat = trialQueue.AppendRepeatedAttempt(invalidTrial);
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialRepeatQueued;task=random_dot_k;sequence={0};next_attempt={1};queue_position={2}",
                repeat.SequenceIndex,
                repeat.AttemptNumber,
                trialQueue.Count));
            FinishAttemptAndScheduleNext();
        }

        private RandomDotTrialResult CaptureCurrentResult(
            CheckerboardCurvatureResponse response,
            bool validForAnalysis,
            string status)
        {
            double responseTime = Time.realtimeSinceStartupAsDouble;
            double resolvedStimulusEnd = stimulusEndUnitySeconds > trialStartUnitySeconds
                ? stimulusEndUnitySeconds
                : responseTime;

            return new RandomDotTrialResult(
                currentTrial,
                presentationCount,
                trialStartUtc,
                trialStartUnitySeconds,
                resolvedStimulusEnd,
                responseTime,
                response,
                validForAnalysis,
                sweepMonitor?.CompletedHalfSweeps ?? 0,
                sweepMonitor?.MinimumYawDegrees ?? 0f,
                sweepMonitor?.MaximumYawDegrees ?? 0f,
                stimulus.SweepAmplitudeDegrees,
                stimulus.SweepSpeedDegreesPerSecond,
                stimulus.ApertureEdgeSoftnessDegrees,
                fixationMonitor != null && fixationMonitor.CurrentSampleValid,
                fixationMonitor != null && fixationMonitor.IsInsideTolerance,
                fixationMonitor != null ? fixationMonitor.CurrentAngleDegrees : float.NaN,
                fixationMonitor != null ? fixationMonitor.ContinuousFixationSeconds : 0f,
                fixationMonitor != null ? fixationMonitor.ValidSampleFraction : float.NaN,
                longestOffTargetSeconds,
                longestInvalidGazeSeconds,
                stimulus.DotCount,
                stimulus.WorldCoverageDiameterDegrees,
                stimulus.FieldRadiusMeters,
                status);
        }

        private bool TryAppendResult(RandomDotTrialResult result)
        {
            try
            {
                experimentFiles.AppendResult(result, totalTrials);
                return true;
            }
            catch (Exception exception)
            {
                FailAfterWriteError(exception);
                return false;
            }
        }

        private void FinishAttemptAndScheduleNext()
        {
            StopMotionCoroutine();
            stimulus.Hide();
            currentTrial = null;
            sessionState = RandomDotSessionState.InterTrial;

            if (interTrialSeconds <= 0f)
            {
                BeginNextAttempt();
            }
            else
            {
                interTrialCoroutine = StartCoroutine(BeginNextAttemptAfterDelay());
            }
        }

        private IEnumerator BeginNextAttemptAfterDelay()
        {
            yield return new WaitForSecondsRealtime(interTrialSeconds);
            BeginNextAttempt();
        }

        private void CompleteSession()
        {
            currentTrial = null;
            currentTrialNumber = totalTrials;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "SessionCompleted;task=random_dot_k;valid_trials={0};presentations={1}",
                validTrialsCompleted,
                presentationCount));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = RandomDotSessionState.Completed;
            SessionFinished?.Invoke(sessionState);
            Debug.Log(
                $"Random-Dot-k-Sitzung vollständig gespeichert: {validTrialsCompleted} gültige Trials aus {presentationCount} Präsentationen.\n" +
                activeSessionFolder,
                this);
        }

        private void HandleHalfSweepCompleted(int count, float yawDegrees)
        {
            if (sessionState != RandomDotSessionState.PresentingMotion ||
                currentTrial == null)
            {
                return;
            }

            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "MotionHalfSweep;task=random_dot_k;sequence={0};count={1};yaw={2:F3};k={3:F4}",
                currentTrial.SequenceIndex,
                count,
                yawDegrees,
                currentTrial.StimulusK));
        }

        private void StartEyeTracking(DateTime sessionStartUtc)
        {
            if (eyeTrackingToolbox == null)
            {
                Debug.LogWarning("Sitzung läuft ohne Eye-Tracking-Aufzeichnung.", this);
                return;
            }

            if (eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }

            eyeTrackingToolbox.SetOutputFolder(activeSessionFolder);
            eyeTrackingToolbox.StartRecording(experimentFiles.BaseFileName);
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "SessionStart;task=random_dot_k;participant={0};session={1};seed={2};planned_trials={3};utc={4};mapping={5}",
                CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "random_dot"),
                randomSeed,
                trialPlan.Count,
                sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                RandomDotExperimentFiles.MappingVersion));
        }

        private string BuildTrialStartMarker(RandomDotTrial trial)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;task=random_dot_k;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "magnification={8:F4};stimulus_k={9:F4};motion={10};direction={11};" +
                "duration_s={12:F3};amplitude_deg={13:F3};speed_deg_s={14:F3};dot_seed={15}",
                presentationCount,
                trial.SequenceIndex,
                trial.ConditionIndex,
                trial.Repetition,
                trial.AttemptNumber,
                trial.EyePresentation,
                trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees,
                trial.Magnification,
                trial.StimulusK,
                trial.MotionMode,
                trial.SweepDirection,
                motionDurationSeconds,
                sweepAmplitudeDegrees,
                sweepSpeedDegreesPerSecond,
                trial.DotSeed);
        }

        private void ResolveReferences()
        {
            stimulus ??= FindAnyObjectByType<RandomDotFieldStimulus>();
            keyboardController ??= stimulus != null
                ? stimulus.GetComponent<RandomDotKeyboardController>()
                : null;
            sweepMonitor ??= stimulus != null
                ? stimulus.GetComponent<RandomDotHeadSweepMonitor>()
                : null;
            eyeTrackingToolbox ??= EyeTrackingToolbox.Instance;
            eyeTrackingToolbox ??= FindAnyObjectByType<EyeTrackingToolbox>();
            fixationMonitor ??= FindAnyObjectByType<RandomDotFixationMonitor>();
        }

        private void SubscribeEvents()
        {
            if (eventsSubscribed || keyboardController == null || sweepMonitor == null)
            {
                return;
            }

            keyboardController.ResponseSubmitted += HandleResponseSubmitted;
            sweepMonitor.HalfSweepCompleted += HandleHalfSweepCompleted;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed)
            {
                return;
            }

            if (keyboardController != null)
            {
                keyboardController.ResponseSubmitted -= HandleResponseSubmitted;
            }

            if (sweepMonitor != null)
            {
                sweepMonitor.HalfSweepCompleted -= HandleHalfSweepCompleted;
            }

            eventsSubscribed = false;
        }

        private void ResetFixationCounters()
        {
            currentOffTargetSeconds = 0f;
            currentInvalidGazeSeconds = 0f;
            longestOffTargetSeconds = 0f;
            longestInvalidGazeSeconds = 0f;
        }

        private void WriteMarker(string message)
        {
            eyeTrackingToolbox?.WriteMessage(message);
        }

        private void StopEyeTrackingRecording()
        {
            if (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }
        }

        private void StopMotionCoroutine()
        {
            if (motionCoroutine == null)
            {
                return;
            }

            StopCoroutine(motionCoroutine);
            motionCoroutine = null;
        }

        private void StopPendingCoroutines()
        {
            StopMotionCoroutine();
            if (interTrialCoroutine == null)
            {
                return;
            }

            StopCoroutine(interTrialCoroutine);
            interTrialCoroutine = null;
        }

        private void FailAfterWriteError(Exception exception)
        {
            Debug.LogError(
                "Random-Dot-Trial konnte nicht gespeichert werden; die Sitzung wird beendet: " +
                exception.Message,
                this);
            WriteMarker("SessionAborted;task=random_dot_k;reason=result_write_error");
            StopPendingCoroutines();
            stimulus?.Hide();
            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = RandomDotSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void OnValidate()
        {
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            motionDurationSeconds = Mathf.Max(0.1f, motionDurationSeconds);
            sweepAmplitudeDegrees = Mathf.Clamp(sweepAmplitudeDegrees, 0.1f, 30f);
            sweepSpeedDegreesPerSecond = Mathf.Clamp(
                sweepSpeedDegreesPerSecond,
                0.1f,
                60f);
            maximumOffTargetSeconds = Mathf.Max(0f, maximumOffTargetSeconds);
            maximumInvalidGazeSeconds = Mathf.Max(0f, maximumInvalidGazeSeconds);
            maximumGazeSampleAgeSeconds = Mathf.Max(0.01f, maximumGazeSampleAgeSeconds);
            maximumAttemptsPerTrial = Mathf.Max(0, maximumAttemptsPerTrial);
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
