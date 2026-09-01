using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        RunningTrial,
        Completed,
        Aborted
    }

    /// <summary>
    /// Führt den dynamischen Random-Dot-Einstelltest durch. Die Person
    /// schwenkt den Kopf, verändert k und bestätigt den Wert, bei dem das
    /// weltfeste Punktfeld subjektiv stabil erscheint.
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
        [Tooltip("Pseudonymisierte ID; keine Klarnamen verwenden.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "random_dot_k_pilot";

        [SerializeField]
        private int randomSeed = 20260828;

        [SerializeField]
        private int dotSeedBase = 24680;

        [SerializeField]
        [Tooltip("Leer = Application.persistentDataPath/Measurements.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Vollfaktorieller Trialplan")]
        [SerializeField]
        private List<float> angularDiametersDegrees = new() { 70f };

        [SerializeField]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [SerializeField]
        [Tooltip("Zwei Richtungen prüfen Anker- und Hystereseeffekte der Einstellung.")]
        private List<float> startingKValues = new() { 0.3f, 0.9f };

        [SerializeField]
        private List<float> magnifications = new() { 10f };

        [SerializeField]
        [Tooltip("HeadTracked ist die Versuchsbedingung; SimulatedYaw dient der Technikprüfung.")]
        private List<RandomDotMotionMode> motionModes = new()
        {
            RandomDotMotionMode.HeadTracked
        };

        [SerializeField, Min(1)]
        private int repetitionsPerCondition = 1;

        [Header("Trial-Ablauf")]
        [SerializeField]
        private bool recenterAtTrialStart = true;

        [SerializeField, Min(0f)]
        private float interTrialSeconds = 0.5f;

        [SerializeField]
        [Tooltip("Enter wird erst nach der geforderten Zahl echter Seitenwechsel angenommen.")]
        private bool requireHeadSweepsBeforeConfirmation = true;

        [SerializeField]
        [Tooltip("Optional: Enter wird nur bei erfüllter Fixationsdauer angenommen.")]
        private bool requireFixationBeforeConfirmation;

        [Header("Tasten")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        private Key confirmTrialKey = Key.Enter;

        [SerializeField]
        private Key abortSessionKey = Key.F6;

        [Header("Laufzeitstatus")]
        [SerializeField]
        private RandomDotSessionState sessionState = RandomDotSessionState.Idle;

        [SerializeField]
        private int currentTrialNumber;

        [SerializeField]
        private int totalTrials;

        [SerializeField]
        private string activeSessionFolder = string.Empty;

        private IReadOnlyList<RandomDotTrial> trialPlan;
        private RandomDotTrial currentTrial;
        private RandomDotExperimentFiles experimentFiles;
        private int currentPlanIndex = -1;
        private int kAdjustmentCount;
        private int recenterCount;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private Coroutine interTrialCoroutine;
        private bool eventsSubscribed;

        public event Action<RandomDotTrial> TrialStarted;
        public event Action<RandomDotTrialResult> TrialEnded;
        public event Action<RandomDotSessionState> SessionFinished;

        public RandomDotSessionState SessionState => sessionState;
        public RandomDotTrial CurrentTrial => currentTrial;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public string ActiveSessionFolder => activeSessionFolder;
        public bool RequireHeadSweepsBeforeConfirmation =>
            requireHeadSweepsBeforeConfirmation;
        public bool RequireFixationBeforeConfirmation =>
            requireFixationBeforeConfirmation;
        public bool IsSessionActive =>
            sessionState == RandomDotSessionState.InterTrial ||
            sessionState == RandomDotSessionState.RunningTrial;

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
            // Start, Abbruch und Antwort gehören zur Sitzung. Änderungen von k und
            // Recenter kommen als Ereignisse aus der separaten Tastatursteuerung.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

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

            if (sessionState == RandomDotSessionState.RunningTrial &&
                keyboard[confirmTrialKey].wasPressedThisFrame)
            {
                ConfirmCurrentTrial();
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

            if (requireFixationBeforeConfirmation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Fixationsfreigabe ist aktiv, aber kein Random-Dot Fixation Monitor zugewiesen.",
                    this);
                return false;
            }

            try
            {
                // Der komplette Plan und die CSV-Köpfe entstehen vor dem ersten
                // sichtbaren Trial. Ein Fehler hinterlässt damit keine laufende Messung.
                trialPlan = RandomDotTrialPlanner.CreateRandomizedPlan(
                    angularDiametersDegrees,
                    eyePresentations,
                    startingKValues,
                    magnifications,
                    motionModes,
                    repetitionsPerCondition,
                    randomSeed,
                    dotSeedBase);

                DateTime sessionStartUtc = DateTime.UtcNow;
                string resolvedRoot = string.IsNullOrWhiteSpace(outputRoot)
                    ? Path.Combine(Application.persistentDataPath, "Measurements")
                    : outputRoot;
                experimentFiles = RandomDotExperimentFiles.Create(
                    resolvedRoot,
                    participantId,
                    sessionLabel,
                    sessionStartUtc,
                    randomSeed);
                experimentFiles.WritePlan(trialPlan);
                activeSessionFolder = experimentFiles.SessionFolder;

                if (eyeTrackingToolbox != null)
                {
                    // Eye Tracking schreibt in denselben Sitzungsordner. Marker
                    // ordnen k-Schritte und Kopfseitenwechsel den Gaze-Samples zu.
                    if (eyeTrackingToolbox.IsRecording)
                    {
                        eyeTrackingToolbox.StopRecording();
                    }

                    eyeTrackingToolbox.SetOutputFolder(activeSessionFolder);
                    eyeTrackingToolbox.StartRecording(experimentFiles.BaseFileName);
                    WriteMarker(string.Format(
                        CultureInfo.InvariantCulture,
                        "SessionStart;task=random_dot_k;participant={0};session={1};seed={2};trials={3}",
                        CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                        CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "random_dot"),
                        randomSeed,
                        trialPlan.Count));
                }
                else
                {
                    Debug.LogWarning("Sitzung läuft ohne Eye-Tracking-Aufzeichnung.", this);
                }
            }
            catch (Exception exception)
            {
                sessionState = RandomDotSessionState.Aborted;
                Debug.LogError(
                    "Random-Dot-Sitzung konnte nicht gestartet werden: " + exception.Message,
                    this);
                return false;
            }

            StopPendingInterTrial();
            currentPlanIndex = -1;
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            sessionState = RandomDotSessionState.InterTrial;
            Debug.Log(
                $"Random-Dot-k-Sitzung gestartet: {totalTrials} Trials.\n{activeSessionFolder}",
                this);
            BeginNextTrial();
            return true;
        }

        public bool ConfirmCurrentTrial()
        {
            if (sessionState != RandomDotSessionState.RunningTrial || currentTrial == null)
            {
                return false;
            }

            if (requireHeadSweepsBeforeConfirmation &&
                (sweepMonitor == null || !sweepMonitor.RequirementMet))
            {
                // Die abgewiesene Antwort wird mit erreichtem und gefordertem
                // Sweep-Stand gespeichert, statt nur eine Warnung anzuzeigen.
                WriteMarker(string.Format(
                    CultureInfo.InvariantCulture,
                    "TrialConfirmationRejected;sequence={0};reason=head_sweeps;completed={1};required={2}",
                    currentTrial.SequenceIndex,
                    sweepMonitor?.CompletedHalfSweeps ?? 0,
                    sweepMonitor?.RequiredHalfSweeps ?? 0));
                Debug.LogWarning(
                    "Antwort noch nicht angenommen: Kopf-Schwenkkriterium ist nicht erfüllt.",
                    this);
                return false;
            }

            if (requireFixationBeforeConfirmation &&
                (fixationMonitor == null || !fixationMonitor.RequirementMet))
            {
                WriteMarker(
                    $"TrialConfirmationRejected;sequence={currentTrial.SequenceIndex};reason=fixation");
                Debug.LogWarning(
                    "Antwort noch nicht angenommen: Fixationskriterium ist nicht erfüllt.",
                    this);
                return false;
            }

            RandomDotTrialResult result = CaptureCurrentResult("confirmed");
            try
            {
                experimentFiles.AppendResult(result, totalTrials);
            }
            catch (Exception exception)
            {
                FailAfterWriteError(exception);
                return false;
            }

            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialConfirmed;task=random_dot_k;sequence={0};final_k={1:F4};response_s={2:F4};half_sweeps={3}",
                currentTrial.SequenceIndex,
                result.FinalK,
                result.ResponseTimeSeconds,
                result.CompletedHalfSweeps));
            TrialEnded?.Invoke(result);
            stimulus.Hide();
            sessionState = RandomDotSessionState.InterTrial;

            if (interTrialSeconds <= 0f)
            {
                BeginNextTrial();
            }
            else
            {
                interTrialCoroutine = StartCoroutine(BeginNextTrialAfterDelay());
            }

            return true;
        }

        public void AbortSession(string reason = "ManualAbort")
        {
            if (!IsSessionActive)
            {
                return;
            }

            StopPendingInterTrial();
            if (sessionState == RandomDotSessionState.RunningTrial &&
                currentTrial != null && experimentFiles != null)
            {
                try
                {
                    RandomDotTrialResult result = CaptureCurrentResult(
                        "aborted:" + (reason ?? string.Empty));
                    experimentFiles.AppendResult(result, totalTrials);
                    TrialEnded?.Invoke(result);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "Abgebrochener Trial konnte nicht gespeichert werden: " +
                        exception.Message,
                        this);
                }
            }

            WriteMarker("SessionAborted;task=random_dot_k;reason=" +
                CheckerboardExperimentFiles.SanitizeIdentifier(reason, "unspecified"));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = RandomDotSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void BeginNextTrial()
        {
            interTrialCoroutine = null;
            currentPlanIndex++;
            if (trialPlan == null || currentPlanIndex >= trialPlan.Count)
            {
                CompleteSession();
                return;
            }

            currentTrial = trialPlan[currentPlanIndex];
            currentTrialNumber = currentTrial.SequenceIndex;
            // Alle Trialwerte werden im unsichtbaren Zustand gesetzt. Das verhindert,
            // dass ein Frame mit Parametern des vorherigen Trials gezeigt wird.
            stimulus.Hide();
            stimulus.SetAngularDiameter(currentTrial.AngularDiameterDegrees);
            stimulus.SetMagnification(currentTrial.Magnification);
            stimulus.SetEyePresentation(currentTrial.EyePresentation);
            stimulus.SetMotionMode(currentTrial.MotionMode);
            stimulus.SetMerlitzK(currentTrial.StartingK);
            stimulus.ConfigurePointField(
                stimulus.DotCount,
                currentTrial.DotSeed,
                stimulus.WorldCoverageDiameterDegrees);

            if (recenterAtTrialStart)
            {
                stimulus.PlaceAroundObserver();
            }

            sweepMonitor.ResetForTrial();
            fixationMonitor?.ResetFixationWindow();
            kAdjustmentCount = 0;
            recenterCount = 0;
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = RandomDotSessionState.RunningTrial;
            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show();
            TrialStarted?.Invoke(currentTrial);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Random-Dot-Trial {0}/{1}: {2}, m={3:F2}, Start-k={4:F2}. " +
                "Kopf links/rechts schwenken, k mit Pfeiltasten einstellen, Enter bestätigt.",
                currentTrial.SequenceIndex,
                totalTrials,
                currentTrial.MotionMode,
                currentTrial.Magnification,
                currentTrial.StartingK),
                this);
        }

        private IEnumerator BeginNextTrialAfterDelay()
        {
            yield return new WaitForSecondsRealtime(interTrialSeconds);
            BeginNextTrial();
        }

        private RandomDotTrialResult CaptureCurrentResult(string status)
        {
            // Hier werden Planwerte, aktuelle Einstellung, beobachtete Kopfbewegung
            // und Fixationszustand zu genau einer Trialzeile zusammengeführt.
            double endTime = Time.realtimeSinceStartupAsDouble;
            return new RandomDotTrialResult(
                currentTrial,
                trialStartUtc,
                trialStartUnitySeconds,
                endTime,
                stimulus.MerlitzK,
                kAdjustmentCount,
                recenterCount,
                sweepMonitor?.CompletedHalfSweeps ?? 0,
                sweepMonitor?.YawThresholdDegrees ?? 0f,
                sweepMonitor?.MinimumYawDegrees ?? 0f,
                sweepMonitor?.MaximumYawDegrees ?? 0f,
                fixationMonitor != null && fixationMonitor.CurrentSampleValid,
                fixationMonitor != null && fixationMonitor.IsInsideTolerance,
                fixationMonitor != null && fixationMonitor.RequirementMet,
                fixationMonitor != null ? fixationMonitor.CurrentAngleDegrees : float.NaN,
                fixationMonitor != null ? fixationMonitor.ContinuousFixationSeconds : 0f,
                stimulus.DotCount,
                stimulus.WorldCoverageDiameterDegrees,
                stimulus.FieldRadiusMeters,
                status);
        }

        private void CompleteSession()
        {
            currentTrial = null;
            currentTrialNumber = totalTrials;
            WriteMarker("SessionCompleted;task=random_dot_k;trials=" +
                totalTrials.ToString(CultureInfo.InvariantCulture));
            stimulus.Hide();
            StopEyeTrackingRecording();
            sessionState = RandomDotSessionState.Completed;
            SessionFinished?.Invoke(sessionState);
            Debug.Log("Random-Dot-k-Sitzung abgeschlossen.\n" + activeSessionFolder, this);
        }

        private void HandleKChanged(float previous, float current)
        {
            if (sessionState != RandomDotSessionState.RunningTrial || currentTrial == null)
            {
                return;
            }

            kAdjustmentCount++;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "KAdjusted;task=random_dot_k;sequence={0};from={1:F4};to={2:F4};count={3};yaw={4:F3};half_sweeps={5}",
                currentTrial.SequenceIndex,
                previous,
                current,
                kAdjustmentCount,
                sweepMonitor?.CurrentYawDegrees ?? 0f,
                sweepMonitor?.CompletedHalfSweeps ?? 0));
        }

        private void HandleRecentered()
        {
            if (sessionState != RandomDotSessionState.RunningTrial || currentTrial == null)
            {
                return;
            }

            // Nach einem Recenter beziehen sich alte Gierwinkel und alte
            // Fixationsdauer nicht mehr auf denselben Mittelpunkt und werden verworfen.
            recenterCount++;
            sweepMonitor?.ResetForTrial();
            fixationMonitor?.ResetFixationWindow();
            WriteMarker(
                $"Recentered;task=random_dot_k;sequence={currentTrial.SequenceIndex};count={recenterCount}");
        }

        private void HandleHalfSweepCompleted(int count, float yawDegrees)
        {
            if (sessionState != RandomDotSessionState.RunningTrial || currentTrial == null)
            {
                return;
            }

            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "HeadHalfSweep;task=random_dot_k;sequence={0};count={1};yaw={2:F3};k={3:F4}",
                currentTrial.SequenceIndex,
                count,
                yawDegrees,
                stimulus.MerlitzK));
        }

        private static string BuildTrialStartMarker(RandomDotTrial trial)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;task=random_dot_k;sequence={0};condition={1};repetition={2};" +
                "eye={3};fov_deg={4:F3};magnification={5:F4};starting_k={6:F4};" +
                "motion={7};dot_seed={8}",
                trial.SequenceIndex,
                trial.ConditionIndex,
                trial.Repetition,
                trial.EyePresentation,
                trial.AngularDiameterDegrees,
                trial.Magnification,
                trial.StartingK,
                trial.MotionMode,
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

            keyboardController.KChanged += HandleKChanged;
            keyboardController.Recentered += HandleRecentered;
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
                keyboardController.KChanged -= HandleKChanged;
                keyboardController.Recentered -= HandleRecentered;
            }

            if (sweepMonitor != null)
            {
                sweepMonitor.HalfSweepCompleted -= HandleHalfSweepCompleted;
            }

            eventsSubscribed = false;
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

        private void StopPendingInterTrial()
        {
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
                "Random-Dot-Trial konnte nicht gespeichert werden; Sitzung wird beendet: " +
                exception.Message,
                this);
            WriteMarker("SessionAborted;task=random_dot_k;reason=result_write_error");
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = RandomDotSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void OnValidate()
        {
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
