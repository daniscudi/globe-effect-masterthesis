using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    public enum CheckerboardSessionState
    {
        Idle,
        InterTrial,
        RunningTrial,
        Completed,
        Aborted
    }

    /// <summary>
    /// Steuert eine vollstaendige Pilot-Sitzung: reproduzierbare
    /// Randomisierung, Stimulusparameter, k-Antwort, Recenter, Fixationsstatus,
    /// Eye-Tracking-Marker und fortlaufend gesicherte CSV-Dateien.
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
        [Tooltip("Gleicher Seed erzeugt bei identischen Bedingungen dieselbe Reihenfolge.")]
        private int randomSeed = 20260827;

        [SerializeField]
        [Tooltip("Leer = Application.persistentDataPath/Measurements.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Vollfaktorieller Trialplan")]
        [SerializeField]
        private List<float> angularDiametersDegrees = new() { 70f };

        [SerializeField]
        private List<float> viewingDistancesMeters = new() { 1f };

        [SerializeField]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes,
            CheckerboardEyePresentation.LeftEyeOnly,
            CheckerboardEyePresentation.RightEyeOnly
        };

        [SerializeField]
        [Tooltip("Niedriger und hoher Startwert helfen, Richtungseffekte der Einstellung zu erkennen.")]
        private List<float> startingKValues = new() { 0.3f, 0.9f };

        [SerializeField, Min(0.01f)]
        private float magnification = 10f;

        [SerializeField, Min(1)]
        private int repetitionsPerCondition = 1;

        [Header("Trial-Ablauf")]
        [SerializeField]
        private bool recenterAtTrialStart = true;

        [SerializeField, Min(0f)]
        private float interTrialSeconds = 0.5f;

        [SerializeField]
        [Tooltip("Wenn aktiv, nimmt Enter die Antwort erst nach erfuelltem Fixationskriterium an.")]
        private bool requireFixationBeforeConfirmation;

        [Header("Tasten")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        private Key confirmTrialKey = Key.Enter;

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
        private string activeSessionFolder = string.Empty;

        private IReadOnlyList<CheckerboardTrial> trialPlan;
        private CheckerboardTrial currentTrial;
        private CheckerboardExperimentFiles experimentFiles;
        private int currentPlanIndex = -1;
        private int kAdjustmentCount;
        private int recenterCount;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private Coroutine interTrialCoroutine;
        private bool keyboardEventsSubscribed;

        public event Action<CheckerboardTrial> TrialStarted;
        public event Action<CheckerboardTrialResult> TrialEnded;
        public event Action<CheckerboardSessionState> SessionFinished;

        public CheckerboardSessionState SessionState => sessionState;
        public CheckerboardTrial CurrentTrial => currentTrial;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public string ActiveSessionFolder => activeSessionFolder;
        public bool RequireFixationBeforeConfirmation =>
            requireFixationBeforeConfirmation;
        public bool IsSessionActive =>
            sessionState == CheckerboardSessionState.InterTrial ||
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

            if (sessionState == CheckerboardSessionState.RunningTrial &&
                keyboard[confirmTrialKey].wasPressedThisFrame)
            {
                ConfirmCurrentTrial();
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

        /// <summary>
        /// Erzeugt den Plan, legt die Sitzungsdateien an und startet Trial 1.
        /// </summary>
        public bool StartSession()
        {
            if (IsSessionActive)
            {
                Debug.LogWarning("Eine Checkerboard-Sitzung laeuft bereits.", this);
                return false;
            }

            ResolveReferences();
            SubscribeKeyboardEvents();
            if (stimulus == null || keyboardController == null)
            {
                Debug.LogError(
                    "Stimulus und Checkerboard Keyboard Controller muessen zugewiesen sein.",
                    this);
                return false;
            }

            if (requireFixationBeforeConfirmation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Die Fixationsfreigabe ist aktiv, aber es ist kein Fixation Monitor zugewiesen.",
                    this);
                return false;
            }

            try
            {
                trialPlan = CheckerboardTrialPlanner.CreateRandomizedPlan(
                    angularDiametersDegrees,
                    viewingDistancesMeters,
                    eyePresentations,
                    startingKValues,
                    magnification,
                    repetitionsPerCondition,
                    randomSeed);

                DateTime sessionStartUtc = DateTime.UtcNow;
                string resolvedOutputRoot = string.IsNullOrWhiteSpace(outputRoot)
                    ? Path.Combine(Application.persistentDataPath, "Measurements")
                    : outputRoot;
                experimentFiles = CheckerboardExperimentFiles.Create(
                    resolvedOutputRoot,
                    participantId,
                    sessionLabel,
                    sessionStartUtc,
                    randomSeed);
                experimentFiles.WritePlan(trialPlan);
                activeSessionFolder = experimentFiles.SessionFolder;

                if (eyeTrackingToolbox != null)
                {
                    if (eyeTrackingToolbox.IsRecording)
                    {
                        eyeTrackingToolbox.StopRecording();
                    }

                    eyeTrackingToolbox.SetOutputFolder(activeSessionFolder);
                    eyeTrackingToolbox.StartRecording(experimentFiles.BaseFileName);
                    WriteEyeTrackingMarker(string.Format(
                        CultureInfo.InvariantCulture,
                        "SessionStart;participant={0};session={1};seed={2};trials={3}",
                        CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                        CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "session"),
                        randomSeed,
                        trialPlan.Count));
                }
                else
                {
                    Debug.LogWarning(
                        "Sitzung laeuft ohne Eye-Tracking-Aufzeichnung.",
                        this);
                }
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
            currentPlanIndex = -1;
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            sessionState = CheckerboardSessionState.InterTrial;

            Debug.Log(
                $"Checkerboard-Sitzung gestartet: {totalTrials} Trials, Seed {randomSeed}.\n" +
                activeSessionFolder,
                this);
            BeginNextTrial();
            return true;
        }

        /// <summary>Nimmt den aktuellen k-Wert als Antwort an.</summary>
        public bool ConfirmCurrentTrial()
        {
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial == null)
            {
                return false;
            }

            if (requireFixationBeforeConfirmation &&
                (fixationMonitor == null || !fixationMonitor.RequirementMet))
            {
                WriteEyeTrackingMarker(
                    $"TrialConfirmationRejected;sequence={currentTrial.SequenceIndex};reason=fixation");
                Debug.LogWarning(
                    "Antwort noch nicht angenommen: Fixationskriterium ist nicht erfuellt.",
                    this);
                return false;
            }

            CheckerboardTrialResult result = CaptureCurrentResult("confirmed");
            try
            {
                experimentFiles.AppendResult(result, totalTrials);
            }
            catch (Exception exception)
            {
                FailSessionAfterWriteError(exception);
                return false;
            }

            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialConfirmed;sequence={0};final_k={1:F4};response_s={2:F4}",
                currentTrial.SequenceIndex,
                result.FinalK,
                result.ResponseTimeSeconds));
            TrialEnded?.Invoke(result);
            stimulus.Hide();
            sessionState = CheckerboardSessionState.InterTrial;

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
            if (sessionState == CheckerboardSessionState.RunningTrial &&
                currentTrial != null && experimentFiles != null)
            {
                try
                {
                    CheckerboardTrialResult result = CaptureCurrentResult(
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

            WriteEyeTrackingMarker("SessionAborted;reason=" +
                CheckerboardExperimentFiles.SanitizeIdentifier(reason, "unspecified"));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = CheckerboardSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
            Debug.LogWarning("Checkerboard-Sitzung abgebrochen: " + reason, this);
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
            stimulus.Hide();
            stimulus.SetGeometry(
                currentTrial.AngularDiameterDegrees,
                currentTrial.ViewingDistanceMeters);
            stimulus.SetMagnification(currentTrial.Magnification);
            stimulus.SetEyePresentation(currentTrial.EyePresentation);
            stimulus.SetMerlitzK(currentTrial.StartingK);

            if (recenterAtTrialStart)
            {
                stimulus.PlaceInFrontOfObserver();
            }

            fixationMonitor?.ResetFixationWindow();
            kAdjustmentCount = 0;
            recenterCount = 0;
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = CheckerboardSessionState.RunningTrial;

            WriteEyeTrackingMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show();
            TrialStarted?.Invoke(currentTrial);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Trial {0}/{1}: {2}, FOV={3:F1} deg, d={4:F2} m, Start-k={5:F2}. " +
                "Mit Pfeiltasten einstellen, mit Enter bestaetigen.",
                currentTrial.SequenceIndex,
                totalTrials,
                currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees,
                currentTrial.ViewingDistanceMeters,
                currentTrial.StartingK),
                this);
        }

        private IEnumerator BeginNextTrialAfterDelay()
        {
            yield return new WaitForSecondsRealtime(interTrialSeconds);
            BeginNextTrial();
        }

        private CheckerboardTrialResult CaptureCurrentResult(string status)
        {
            CheckerboardStimulusSnapshot snapshot = stimulus.CaptureSnapshot();
            bool fixationSampleValid = fixationMonitor != null &&
                fixationMonitor.CurrentSampleValid;
            bool fixationInside = fixationMonitor != null &&
                fixationMonitor.IsInsideTolerance;
            bool fixationMet = fixationMonitor != null &&
                fixationMonitor.RequirementMet;
            float fixationAngle = fixationMonitor != null
                ? fixationMonitor.CurrentAngleDegrees
                : float.NaN;
            float fixationSeconds = fixationMonitor != null
                ? fixationMonitor.ContinuousFixationSeconds
                : 0f;

            return new CheckerboardTrialResult(
                currentTrial,
                trialStartUtc,
                trialStartUnitySeconds,
                Time.realtimeSinceStartupAsDouble,
                snapshot.physicalDiameterMeters,
                snapshot.merlitzK,
                kAdjustmentCount,
                recenterCount,
                fixationSampleValid,
                fixationInside,
                fixationMet,
                fixationAngle,
                fixationSeconds,
                status);
        }

        private void CompleteSession()
        {
            currentTrial = null;
            currentTrialNumber = totalTrials;
            WriteEyeTrackingMarker("SessionCompleted;trials=" +
                totalTrials.ToString(CultureInfo.InvariantCulture));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = CheckerboardSessionState.Completed;
            SessionFinished?.Invoke(sessionState);

            Debug.Log(
                "Checkerboard-Sitzung vollstaendig gespeichert:\n" +
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
            sessionState = CheckerboardSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void HandleKChanged(float previousK, float currentK)
        {
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial == null)
            {
                return;
            }

            kAdjustmentCount++;
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "KAdjusted;sequence={0};previous={1:F4};current={2:F4};count={3}",
                currentTrial.SequenceIndex,
                previousK,
                currentK,
                kAdjustmentCount));
        }

        private void HandleRecentered()
        {
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial == null)
            {
                return;
            }

            recenterCount++;
            WriteEyeTrackingMarker(
                $"Recentered;sequence={currentTrial.SequenceIndex};count={recenterCount}");
        }

        private void ResolveReferences()
        {
            if (stimulus == null)
            {
                stimulus = FindAnyObjectByType<VrCheckerboardStimulus>();
            }

            if (keyboardController == null && stimulus != null)
            {
                keyboardController = stimulus.GetComponent<CheckerboardKeyboardController>();
            }

            if (eyeTrackingToolbox == null)
            {
                eyeTrackingToolbox = EyeTrackingToolbox.Instance;
                eyeTrackingToolbox ??= FindAnyObjectByType<EyeTrackingToolbox>();
            }

            if (fixationMonitor == null)
            {
                fixationMonitor = FindAnyObjectByType<CheckerboardFixationMonitor>();
            }
        }

        private void SubscribeKeyboardEvents()
        {
            if (keyboardEventsSubscribed || keyboardController == null)
            {
                return;
            }

            keyboardController.KChanged += HandleKChanged;
            keyboardController.Recentered += HandleRecentered;
            keyboardEventsSubscribed = true;
        }

        private void UnsubscribeKeyboardEvents()
        {
            if (!keyboardEventsSubscribed || keyboardController == null)
            {
                keyboardEventsSubscribed = false;
                return;
            }

            keyboardController.KChanged -= HandleKChanged;
            keyboardController.Recentered -= HandleRecentered;
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

        private static string BuildTrialStartMarker(CheckerboardTrial trial)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;sequence={0};condition={1};repetition={2};eye={3};" +
                "fov={4:F2};distance={5:F3};magnification={6:F3};start_k={7:F4}",
                trial.SequenceIndex,
                trial.ConditionIndex,
                trial.Repetition,
                trial.EyePresentation,
                trial.AngularDiameterDegrees,
                trial.ViewingDistanceMeters,
                trial.Magnification,
                trial.StartingK);
        }

        private void OnValidate()
        {
            magnification = Mathf.Max(0.01f, magnification);
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
