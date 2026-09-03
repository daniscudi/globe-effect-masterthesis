using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    public enum CheckerboardSessionState
    {
        Idle,
        InterTrial,
        WaitingForFixation,
        RunningTrial,
        Completed,
        Aborted
    }

    /// <summary>
    /// Führt den statischen Checkerboard-Test aus. l wird vorgegeben und nicht
    /// von der Versuchsperson verändert. Nach stabiler Fixation erscheint das
    /// Muster und wird mit "konkav" oder "konvex" beurteilt. Verlässt der Blick
    /// das Ziel zu lange, wird diese Präsentation als ungültig gespeichert und
    /// dieselbe Bedingung am Ende der Warteschlange erneut gezeigt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class CheckerboardTrialSessionController : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField]
        private VrCheckerboardStimulus stimulus;

        [SerializeField]
        private CheckerboardKeyboardController keyboardController;

        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private CheckerboardFixationMonitor fixationMonitor;

        [Header("Sitzung")]
        [SerializeField]
        [Tooltip("Pseudonymisierte Versuchsperson-ID; keine Klarnamen verwenden.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "checkerboard_pilot";

        [SerializeField]
        [Tooltip("Gleicher Seed und gleiche Inspector-Werte ergeben dieselbe Reihenfolge.")]
        private int randomSeed = 20260901;

        [SerializeField]
        [Tooltip("Leer = measurements-Ordner direkt im Unity-Projekt.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Trialplan")]
        [SerializeField]
        [Tooltip("Ein oder mehrere Winkeldurchmesser der kreisrunden Blende.")]
        private List<float> angularDiametersDegrees = new() { 90f };

        [SerializeField]
        [Tooltip("Hier kann Both Eyes, Left Eye Only oder Right Eye Only gewählt werden.")]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [SerializeField]
        [Tooltip("Vorläufige Pilotwerte. l = 1 ist gerade, l = 0,5 ist der Helmholtz-Endpunkt. Die Liste kann vollständig geändert werden.")]
        private List<float> visualSpaceLValues = new()
        {
            1.2f,
            1f,
            0.8f,
            0.6f,
            0.5f,
            0.4f,
            0.2f
        };

        [SerializeField, Min(1)]
        [Tooltip("Wie oft jede Kombination aus FOV, Augenmodus und l vorkommt.")]
        private int repetitionsPerCondition = 3;

        [Header("Fixation und Wiederholung")]
        [SerializeField]
        [Tooltip("Vor dem Muster wird stabile Fixation verlangt und während des Trials überwacht.")]
        private bool requireFixation = true;

        [SerializeField, Min(0f)]
        [Tooltip("So lange darf der Blick am Stück außerhalb der Toleranz liegen, bevor der Trial ungültig wird.")]
        private float maximumOffTargetSeconds = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("So lange dürfen am Stück ungültige oder fehlende Blickdaten vorliegen.")]
        private float maximumInvalidGazeSeconds = 0.2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Ältere Eye-Tracking-Samples gelten als fehlende Daten.")]
        private float maximumGazeSampleAgeSeconds = 0.1f;

        [SerializeField, Min(0)]
        [Tooltip("0 = unbegrenzt wiederholen. Ein positiver Wert bricht die Sitzung nach so vielen erfolglosen Versuchen derselben Bedingung ab.")]
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
        private CheckerboardSessionState sessionState = CheckerboardSessionState.Idle;

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

        private IReadOnlyList<CheckerboardTrial> trialPlan;
        private CheckerboardTrialQueue trialQueue;
        private CheckerboardTrial currentTrial;
        private CheckerboardExperimentFiles experimentFiles;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private float currentOffTargetSeconds;
        private float currentInvalidGazeSeconds;
        private float longestOffTargetSeconds;
        private float longestInvalidGazeSeconds;
        private Coroutine interTrialCoroutine;
        private bool keyboardEventsSubscribed;

        public event Action<CheckerboardTrial> TrialStarted;
        public event Action<CheckerboardTrialResult> TrialEnded;
        public event Action<CheckerboardSessionState> SessionFinished;

        public CheckerboardSessionState SessionState => sessionState;
        public CheckerboardTrial CurrentTrial => currentTrial;
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
            sessionState == CheckerboardSessionState.InterTrial ||
            sessionState == CheckerboardSessionState.WaitingForFixation ||
            sessionState == CheckerboardSessionState.RunningTrial;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeKeyboardEvents();
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

            if (sessionState == CheckerboardSessionState.WaitingForFixation &&
                fixationMonitor != null && fixationMonitor.RequirementMet)
            {
                PresentCurrentTrial();
                return;
            }

            if (sessionState == CheckerboardSessionState.RunningTrial &&
                requireFixation)
            {
                MonitorFixationDuringTrial();
            }
        }

        private void OnDisable()
        {
            UnsubscribeKeyboardEvents();
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }
        }

        public void Configure(
            VrCheckerboardStimulus checkerboardStimulus,
            CheckerboardKeyboardController checkerboardKeyboardController,
            EyeTrackingToolbox toolbox,
            CheckerboardFixationMonitor monitor)
        {
            UnsubscribeKeyboardEvents();
            stimulus = checkerboardStimulus;
            keyboardController = checkerboardKeyboardController;
            eyeTrackingToolbox = toolbox;
            fixationMonitor = monitor;
            if (isActiveAndEnabled)
            {
                SubscribeKeyboardEvents();
            }
        }

        public bool StartSession()
        {
            if (IsSessionActive)
            {
                Debug.LogWarning("Eine Checkerboard-Sitzung läuft bereits.", this);
                return false;
            }

            ResolveReferences();
            SubscribeKeyboardEvents();
            if (stimulus == null || keyboardController == null)
            {
                Debug.LogError(
                    "Stimulus und Checkerboard Keyboard Controller müssen zugewiesen sein.",
                    this);
                return false;
            }

            if (requireFixation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Fixationskontrolle ist aktiv, aber der Fixation Monitor fehlt.",
                    this);
                return false;
            }

            try
            {
                trialPlan = CheckerboardTrialPlanner.CreateRandomizedPlan(
                    angularDiametersDegrees,
                    eyePresentations,
                    visualSpaceLValues,
                    repetitionsPerCondition,
                    randomSeed);
                trialQueue = new CheckerboardTrialQueue(trialPlan);

                DateTime sessionStartUtc = DateTime.UtcNow;
                string resolvedOutputRoot = ExperimentOutputPath.Resolve(outputRoot);
                experimentFiles = CheckerboardExperimentFiles.Create(
                    resolvedOutputRoot,
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
                sessionState = CheckerboardSessionState.Aborted;
                Debug.LogError(
                    "Checkerboard-Sitzung konnte nicht gestartet werden: " +
                    exception.Message,
                    this);
                return false;
            }

            StopPendingInterTrial();
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            validTrialsCompleted = 0;
            presentationCount = 0;
            sessionState = CheckerboardSessionState.InterTrial;

            Debug.Log(
                $"Checkerboard-Sitzung gestartet: {totalTrials} gültige Trials geplant, Seed {randomSeed}.\n" +
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

            StopPendingInterTrial();
            if (sessionState == CheckerboardSessionState.RunningTrial &&
                currentTrial != null && experimentFiles != null)
            {
                TryAppendResult(CaptureCurrentResult(
                    CheckerboardCurvatureResponse.None,
                    validForAnalysis: false,
                    "aborted:" + (reason ?? string.Empty)));
            }

            WriteEyeTrackingMarker("SessionAborted;reason=" +
                CheckerboardExperimentFiles.SanitizeIdentifier(reason, "unspecified"));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = CheckerboardSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
            Debug.LogWarning("Checkerboard-Sitzung abgebrochen: " + reason, this);
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
            stimulus.SetEyePresentation(currentTrial.EyePresentation);
            stimulus.SetVisualSpaceL(currentTrial.VisualSpaceL);

            fixationMonitor?.ResetFixationWindow();
            ResetTrialFixationCounters();

            if (requireFixation)
            {
                sessionState = CheckerboardSessionState.WaitingForFixation;
                stimulus.ShowFixationOnly();
                WriteEyeTrackingMarker(string.Format(
                    CultureInfo.InvariantCulture,
                    "FixationAcquisitionStart;sequence={0};attempt={1}",
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
            ResetTrialFixationCounters();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = CheckerboardSessionState.RunningTrial;

            WriteEyeTrackingMarker(BuildTrialStartMarker(currentTrial, presentationCount));
            stimulus.Show();
            TrialStarted?.Invoke(currentTrial);

            string responseHint = ResponseKeysSwapped
                ? "Links = konvex, rechts = konkav."
                : "Links = konkav, rechts = konvex.";

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Trial {0}/{1}, Präsentation {2}: {3}, FOV={4:F1}°, l={5:F3}, Versuch {6}. " +
                "{7}",
                currentTrialNumber,
                totalTrials,
                presentationCount,
                currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees,
                currentTrial.VisualSpaceL,
                currentTrial.AttemptNumber,
                responseHint),
                this);
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial == null ||
                response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            if (requireFixation &&
                (fixationMonitor == null ||
                 !fixationMonitor.HasRecentSample(maximumGazeSampleAgeSeconds) ||
                 !fixationMonitor.CurrentSampleValid ||
                 !fixationMonitor.IsInsideTolerance))
            {
                InvalidateCurrentTrial("response_off_target");
                return;
            }

            CheckerboardTrialResult result = CaptureCurrentResult(
                response,
                validForAnalysis: true,
                "valid");
            if (!TryAppendResult(result))
            {
                return;
            }

            validTrialsCompleted++;
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialResponse;sequence={0};attempt={1};response={2};response_s={3:F4};valid=1",
                currentTrial.SequenceIndex,
                currentTrial.AttemptNumber,
                response,
                result.ResponseTimeSeconds));
            TrialEnded?.Invoke(result);
            FinishAttemptAndScheduleNext();
        }

        private void MonitorFixationDuringTrial()
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
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial == null)
            {
                return;
            }

            CheckerboardTrial invalidTrial = currentTrial;
            CheckerboardTrialResult result = CaptureCurrentResult(
                CheckerboardCurvatureResponse.None,
                validForAnalysis: false,
                "invalid_fixation:" + reason);
            if (!TryAppendResult(result))
            {
                return;
            }

            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialInvalid;sequence={0};attempt={1};reason={2};off_target_s={3:F4};invalid_gaze_s={4:F4}",
                invalidTrial.SequenceIndex,
                invalidTrial.AttemptNumber,
                reason,
                longestOffTargetSeconds,
                longestInvalidGazeSeconds));
            TrialEnded?.Invoke(result);

            bool attemptLimitReached = maximumAttemptsPerTrial > 0 &&
                invalidTrial.AttemptNumber >= maximumAttemptsPerTrial;
            if (attemptLimitReached)
            {
                stimulus.Hide();
                WriteEyeTrackingMarker(
                    "SessionAborted;reason=maximum_repeat_attempts_reached");
                StopEyeTrackingRecording();
                currentTrial = null;
                sessionState = CheckerboardSessionState.Aborted;
                SessionFinished?.Invoke(sessionState);
                Debug.LogError(
                    "Die maximale Zahl an Wiederholungen wurde erreicht. Die Sitzung wurde beendet.",
                    this);
                return;
            }

            CheckerboardTrial repeat = trialQueue.AppendRepeatedAttempt(invalidTrial);
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialRepeatQueued;sequence={0};next_attempt={1};queue_position={2}",
                repeat.SequenceIndex,
                repeat.AttemptNumber,
                trialQueue.Count));
            FinishAttemptAndScheduleNext();
        }

        private CheckerboardTrialResult CaptureCurrentResult(
            CheckerboardCurvatureResponse response,
            bool validForAnalysis,
            string status)
        {
            bool sampleValid = fixationMonitor != null &&
                fixationMonitor.CurrentSampleValid;
            bool inside = fixationMonitor != null &&
                fixationMonitor.IsInsideTolerance;
            float angle = fixationMonitor != null
                ? fixationMonitor.CurrentAngleDegrees
                : float.NaN;
            float continuousSeconds = fixationMonitor != null
                ? fixationMonitor.ContinuousFixationSeconds
                : 0f;
            float validSampleFraction = fixationMonitor != null
                ? fixationMonitor.ValidSampleFraction
                : float.NaN;

            return new CheckerboardTrialResult(
                currentTrial,
                presentationCount,
                trialStartUtc,
                trialStartUnitySeconds,
                Time.realtimeSinceStartupAsDouble,
                stimulus.ApertureEdgeSoftnessDegrees,
                stimulus.UseCircularAperture,
                response,
                validForAnalysis,
                sampleValid,
                inside,
                angle,
                continuousSeconds,
                validSampleFraction,
                longestOffTargetSeconds,
                longestInvalidGazeSeconds,
                status);
        }

        private bool TryAppendResult(CheckerboardTrialResult result)
        {
            try
            {
                experimentFiles.AppendResult(result, totalTrials);
                return true;
            }
            catch (Exception exception)
            {
                FailSessionAfterWriteError(exception);
                return false;
            }
        }

        private void FinishAttemptAndScheduleNext()
        {
            stimulus.Hide();
            currentTrial = null;
            sessionState = CheckerboardSessionState.InterTrial;

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
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "SessionCompleted;valid_trials={0};presentations={1}",
                validTrialsCompleted,
                presentationCount));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = CheckerboardSessionState.Completed;
            SessionFinished?.Invoke(sessionState);

            Debug.Log(
                $"Checkerboard-Sitzung vollständig gespeichert: {validTrialsCompleted} gültige Trials aus {presentationCount} Präsentationen.\n" +
                activeSessionFolder,
                this);
        }

        private void FailSessionAfterWriteError(Exception exception)
        {
            Debug.LogError(
                "Trialdaten konnten nicht gespeichert werden; die Sitzung wird beendet: " +
                exception.Message,
                this);
            WriteEyeTrackingMarker("SessionAborted;reason=result_write_error");
            stimulus?.Hide();
            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = CheckerboardSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
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
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "SessionStart;participant={0};session={1};seed={2};planned_trials={3};utc={4};mapping={5}",
                CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "session"),
                randomSeed,
                trialPlan.Count,
                sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                VisualSpaceRadialMapping.MappingVersion));
        }

        private void ResolveReferences()
        {
            stimulus ??= FindAnyObjectByType<VrCheckerboardStimulus>();
            if (keyboardController == null && stimulus != null)
            {
                keyboardController = stimulus.GetComponent<CheckerboardKeyboardController>();
            }

            if (eyeTrackingToolbox == null)
            {
                eyeTrackingToolbox = EyeTrackingToolbox.Instance;
                eyeTrackingToolbox ??= FindAnyObjectByType<EyeTrackingToolbox>();
            }

            fixationMonitor ??= FindAnyObjectByType<CheckerboardFixationMonitor>();
        }

        private void SubscribeKeyboardEvents()
        {
            if (keyboardEventsSubscribed || keyboardController == null)
            {
                return;
            }

            keyboardController.ResponseSubmitted += HandleResponseSubmitted;
            keyboardEventsSubscribed = true;
        }

        private void UnsubscribeKeyboardEvents()
        {
            if (!keyboardEventsSubscribed || keyboardController == null)
            {
                keyboardEventsSubscribed = false;
                return;
            }

            keyboardController.ResponseSubmitted -= HandleResponseSubmitted;
            keyboardEventsSubscribed = false;
        }

        private void StopPendingInterTrial()
        {
            if (interTrialCoroutine == null)
            {
                return;
            }

            StopCoroutine(interTrialCoroutine);
            interTrialCoroutine = null;
        }

        private void StopEyeTrackingRecording()
        {
            if (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }
        }

        private void WriteEyeTrackingMarker(string message)
        {
            eyeTrackingToolbox?.WriteMessage(message);
        }

        private string BuildTrialStartMarker(
            CheckerboardTrial trial,
            int presentationIndex)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "circular_aperture={8};visual_space_l={9:F4}",
                presentationIndex,
                trial.SequenceIndex,
                trial.ConditionIndex,
                trial.Repetition,
                trial.AttemptNumber,
                trial.EyePresentation,
                trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees,
                stimulus.UseCircularAperture,
                trial.VisualSpaceL);
        }

        private void ResetTrialFixationCounters()
        {
            currentOffTargetSeconds = 0f;
            currentInvalidGazeSeconds = 0f;
            longestOffTargetSeconds = 0f;
            longestInvalidGazeSeconds = 0f;
        }

        private void OnValidate()
        {
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            maximumOffTargetSeconds = Mathf.Max(0f, maximumOffTargetSeconds);
            maximumInvalidGazeSeconds = Mathf.Max(0f, maximumInvalidGazeSeconds);
            maximumGazeSampleAgeSeconds = Mathf.Max(0.01f, maximumGazeSampleAgeSeconds);
            maximumAttemptsPerTrial = Mathf.Max(0, maximumAttemptsPerTrial);
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
