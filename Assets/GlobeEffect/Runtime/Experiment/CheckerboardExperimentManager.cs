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
        ShowingNoise,
        WaitingForResponse,
        Completed,
        Aborted
    }

    /// <summary>
    /// Führt den statischen Checkerboard-Test aus. l wird vorgegeben und nicht
    /// von der Versuchsperson verändert. Nach stabiler Fixation erscheint das
    /// Muster, eine kurze Noise-Maske und danach eine einfache Ball-/Schüssel-
    /// Entscheidung. Verlässt der Blick während des Musters das Ziel zu lange,
    /// wird die Präsentation als ungültig gespeichert und später wiederholt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class CheckerboardExperimentManager : MonoBehaviour
    {
        // Hier läuft der komplette Versuch zusammen:
        // StartSession erstellt und speichert den zufälligen Trialplan.
        // BeginNextAttempt holt die nächste Bedingung aus der Warteschlange.
        // PresentCurrentTrial überträgt die Werte an den Stimulus.
        // MonitorFixationDuringTrial prüft den Blick während der Präsentation.
        // Die Antwort wird gespeichert; ein ungültiger Trial kommt hinten dran.
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

        [SerializeField]
        [Tooltip("Unabhängiger Zoom des Gitterinhalts. 1 bedeutet Originalgröße. Dieser Wert ist nicht die Merlitz-Vergrößerung m.")]
        private List<float> contentZoomValues = new() { 1f };

        [SerializeField, Min(1)]
        [Tooltip("Wie oft jede Kombination aus FOV, Augenmodus, l und Content Zoom vorkommt.")]
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
        [SerializeField, Min(0.01f)]
        [Tooltip("Wie lange das Checkerboard sichtbar ist. 0,6 entspricht 600 ms.")]
        private float stimulusDurationSeconds = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Dauer der Schwarz-Weiß-Noise-Maske direkt nach dem Muster.")]
        private float noiseMaskDurationSeconds = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximale Antwortzeit ab Einblendung der Antwort. 0 bedeutet ohne Zeitlimit.")]
        private float responseTimeoutSeconds = 5f;

        [SerializeField, Min(0f)]
        private float interTrialSeconds = 0.5f;

        [Header("Antworttext")]
        [SerializeField]
        private Key convexResponseKey = Key.UpArrow;

        [SerializeField]
        private Key concaveResponseKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Einfache Beschreibung für die konvexe Wahrnehmung.")]
        private string convexResponseText = "Wölbt sich zu mir (wie ein Ball)";

        [SerializeField]
        [Tooltip("Einfache Beschreibung für die konkave Wahrnehmung.")]
        private string concaveResponseText = "Wölbt sich von mir weg (wie eine Schüssel)";

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
        private double stimulusEndUnitySeconds;
        private double responsePromptUnitySeconds;
        private float currentOffTargetSeconds;
        private float currentInvalidGazeSeconds;
        private float longestOffTargetSeconds;
        private float longestInvalidGazeSeconds;
        private Coroutine interTrialCoroutine;
        private Coroutine presentationCoroutine;
        private bool keyboardEventsSubscribed;
        private bool fixationSnapshotAvailable;
        private bool fixationSampleValidAtStimulusEnd;
        private bool fixationInsideAtStimulusEnd;
        private float fixationAngleAtStimulusEnd = float.NaN;
        private float continuousFixationAtStimulusEnd;
        private float fixationValidFractionAtStimulusEnd = float.NaN;

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
        public string ConvexResponseKeyName => keyboardController != null
            ? CheckerboardKeyboardController.GetReadableKeyName(
                keyboardController.GetKeyForResponse(
                    CheckerboardCurvatureResponse.Convex))
            : "–";
        public string ConcaveResponseKeyName => keyboardController != null
            ? CheckerboardKeyboardController.GetReadableKeyName(
                keyboardController.GetKeyForResponse(
                    CheckerboardCurvatureResponse.Concave))
            : "–";
        public bool IsSessionActive =>
            sessionState == CheckerboardSessionState.InterTrial ||
            sessionState == CheckerboardSessionState.WaitingForFixation ||
            sessionState == CheckerboardSessionState.RunningTrial ||
            sessionState == CheckerboardSessionState.ShowingNoise ||
            sessionState == CheckerboardSessionState.WaitingForResponse;

        private void Awake()
        {
            // Referenzen werden früh gesucht, damit StartSession sie sicher findet.
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
            // Hier werden nur Start/Abbruch und die laufende Fixationskontrolle
            // abgefragt. Die Konkav-/Konvex-Tasten meldet der Keyboard Controller.
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
            // Diese Methode prüft die Szene, erzeugt alle Trialkombinationen,
            // legt den Messordner an und startet danach die erste Präsentation.
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

            // Die Tasten stehen bewusst beim Experiment Manager. Dadurch gelten
            // die neuen Hoch-/Runter-Standardwerte auch in bereits vorhandenen Szenen.
            keyboardController.SetResponseKeys(
                concaveResponseKey,
                convexResponseKey);

            try
            {
                trialPlan = CheckerboardTrialPlanner.CreateRandomizedPlan(
                    angularDiametersDegrees,
                    eyePresentations,
                    visualSpaceLValues,
                    contentZoomValues,
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
                experimentFiles.WritePlan(
                    trialPlan,
                    stimulus.GridLineSpacingDegrees,
                    stimulusDurationSeconds,
                    noiseMaskDurationSeconds,
                    responseTimeoutSeconds);
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
            StopPresentationCoroutine();
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
            // Beim Abbruch bleiben bereits geschriebene Zeilen erhalten. Ein gerade
            // laufender Trial wird zusätzlich als abgebrochen protokolliert.
            if (!IsSessionActive)
            {
                return;
            }

            StopPendingInterTrial();
            StopPresentationCoroutine();
            if ((sessionState == CheckerboardSessionState.RunningTrial ||
                 sessionState == CheckerboardSessionState.ShowingNoise ||
                 sessionState == CheckerboardSessionState.WaitingForResponse) &&
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
            // Die Queue liefert entweder einen neuen Trial oder eine zuvor hinten
            // angehängte Wiederholung. Ist sie leer, ist die Sitzung fertig.
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
            stimulus.SetContentZoom(currentTrial.ContentZoom);

            fixationMonitor?.ResetFixationWindow();
            ResetTrialFixationCounters();
            ResetPresentationTimes();

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
            // Erst hier wird die aktuelle Bedingung sichtbar. Damit zählen
            // Trialzeit und Fixationsprüfung nicht schon während der Wartephase.
            if (currentTrial == null)
            {
                return;
            }

            fixationMonitor?.ResetFixationWindow();
            ResetTrialFixationCounters();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulusEndUnitySeconds = 0d;
            responsePromptUnitySeconds = 0d;
            sessionState = CheckerboardSessionState.RunningTrial;

            WriteEyeTrackingMarker(BuildTrialStartMarker(currentTrial, presentationCount));
            stimulus.Show();
            TrialStarted?.Invoke(currentTrial);

            StopPresentationCoroutine();
            presentationCoroutine = StartCoroutine(
                RunPresentationSequence(currentTrial));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Trial {0}/{1}, Präsentation {2}: {3}, FOV={4:F1}°, l={5:F3}, " +
                "Zoom={6:F2}, Versuch {7}. Stimulus={8:F3}s, Noise={9:F3}s.",
                currentTrialNumber,
                totalTrials,
                presentationCount,
                currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees,
                currentTrial.VisualSpaceL,
                currentTrial.ContentZoom,
                currentTrial.AttemptNumber,
                stimulusDurationSeconds,
                noiseMaskDurationSeconds),
                this);
        }

        private IEnumerator RunPresentationSequence(CheckerboardTrial presentedTrial)
        {
            // Das Muster bleibt für alle Personen exakt gleich lange sichtbar.
            yield return new WaitForSecondsRealtime(stimulusDurationSeconds);
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial != presentedTrial)
            {
                presentationCoroutine = null;
                yield break;
            }

            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            CaptureFixationAtStimulusEnd();
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "StimulusEnded;sequence={0};attempt={1};duration_s={2:F4}",
                presentedTrial.SequenceIndex,
                presentedTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds));

            if (noiseMaskDurationSeconds > 0f)
            {
                sessionState = CheckerboardSessionState.ShowingNoise;
                int noiseSeed = unchecked(
                    randomSeed +
                    presentedTrial.SequenceIndex * 1009 +
                    presentedTrial.AttemptNumber * 9176);
                stimulus.ShowNoise(noiseSeed);
                WriteEyeTrackingMarker(string.Format(
                    CultureInfo.InvariantCulture,
                    "NoiseMaskStarted;sequence={0};attempt={1};planned_duration_s={2:F4};seed={3}",
                    presentedTrial.SequenceIndex,
                    presentedTrial.AttemptNumber,
                    noiseMaskDurationSeconds,
                    noiseSeed));
                yield return new WaitForSecondsRealtime(noiseMaskDurationSeconds);

                if (sessionState != CheckerboardSessionState.ShowingNoise ||
                    currentTrial != presentedTrial)
                {
                    presentationCoroutine = null;
                    yield break;
                }
            }

            responsePromptUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = CheckerboardSessionState.WaitingForResponse;
            stimulus.ShowResponsePrompt(BuildResponsePrompt());
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "ResponsePromptShown;sequence={0};attempt={1};timeout_s={2:F4}",
                presentedTrial.SequenceIndex,
                presentedTrial.AttemptNumber,
                responseTimeoutSeconds));

            if (responseTimeoutSeconds <= 0f)
            {
                presentationCoroutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(responseTimeoutSeconds);
            if (sessionState == CheckerboardSessionState.WaitingForResponse &&
                currentTrial == presentedTrial)
            {
                // Vor Invalidate wird die Referenz geleert, weil die Coroutine
                // sich an dieser Stelle bereits selbst beendet.
                presentationCoroutine = null;
                InvalidateCurrentTrial("response_timeout");
                yield break;
            }

            presentationCoroutine = null;
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            // Eine Antwort wird nur angenommen, solange wirklich ein Trial läuft.
            // Danach wird genau ein Ergebnis geschrieben und weitergeschaltet.
            if (sessionState != CheckerboardSessionState.WaitingForResponse ||
                currentTrial == null ||
                response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            // Nach Ende des Musters darf die Person zum Lesen der Antwortanzeige
            // den Blick bewegen. Deshalb wird die Fixation hier nicht erneut geprüft.
            StopPresentationCoroutine();

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
            // Off target und ungültige Blickdaten werden getrennt gezählt. Nur eine
            // ununterbrochene Überschreitung der erlaubten Zeit macht den Trial ungültig.
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
            // Der ungültige Versuch wird gespeichert, aber nicht als gültige Antwort
            // gezählt. Dieselbe Bedingung erhält eine höhere Attempt Number und wird
            // am Ende der Queue erneut eingeordnet.
            if ((sessionState != CheckerboardSessionState.RunningTrial &&
                 sessionState != CheckerboardSessionState.ShowingNoise &&
                 sessionState != CheckerboardSessionState.WaitingForResponse) ||
                currentTrial == null)
            {
                return;
            }

            StopPresentationCoroutine();
            if (!fixationSnapshotAvailable)
            {
                CaptureFixationAtStimulusEnd();
            }

            CheckerboardTrial invalidTrial = currentTrial;
            CheckerboardTrialResult result = CaptureCurrentResult(
                CheckerboardCurvatureResponse.None,
                validForAnalysis: false,
                reason == "response_timeout"
                    ? "invalid_response:response_timeout"
                    : "invalid_fixation:" + reason);
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
            // Hier werden Trialbedingung, Zeitpunkte und Blickstatus in einem Objekt
            // gesammelt. Die Dateiklasse schreibt dieses Objekt anschließend als CSV.
            bool sampleValid = fixationSnapshotAvailable
                ? fixationSampleValidAtStimulusEnd
                : fixationMonitor != null && fixationMonitor.CurrentSampleValid;
            bool inside = fixationSnapshotAvailable
                ? fixationInsideAtStimulusEnd
                : fixationMonitor != null && fixationMonitor.IsInsideTolerance;
            float angle = fixationSnapshotAvailable
                ? fixationAngleAtStimulusEnd
                : fixationMonitor != null
                    ? fixationMonitor.CurrentAngleDegrees
                    : float.NaN;
            float continuousSeconds = fixationSnapshotAvailable
                ? continuousFixationAtStimulusEnd
                : fixationMonitor != null
                    ? fixationMonitor.ContinuousFixationSeconds
                    : 0f;
            float validSampleFraction = fixationSnapshotAvailable
                ? fixationValidFractionAtStimulusEnd
                : fixationMonitor != null
                    ? fixationMonitor.ValidSampleFraction
                    : float.NaN;

            double resultEndTime = Time.realtimeSinceStartupAsDouble;
            double resolvedStimulusEnd = stimulusEndUnitySeconds > trialStartUnitySeconds
                ? stimulusEndUnitySeconds
                : resultEndTime;
            double resolvedPromptTime = responsePromptUnitySeconds >= resolvedStimulusEnd
                ? responsePromptUnitySeconds
                : resolvedStimulusEnd;

            return new CheckerboardTrialResult(
                currentTrial,
                presentationCount,
                trialStartUtc,
                trialStartUnitySeconds,
                resolvedStimulusEnd,
                resolvedPromptTime,
                resultEndTime,
                stimulus.ApertureEdgeSoftnessDegrees,
                stimulus.UseCircularAperture,
                stimulus.GridLineSpacingDegrees,
                stimulus.GridLineSpacingUv,
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
            // Schreibfehler werden abgefangen, damit nicht unbemerkt ein Versuch
            // weiterläuft, obwohl keine Ergebnisse gespeichert werden können.
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
            // Zwischen zwei Präsentationen wird der Stimulus ausgeblendet und die
            // eingestellte Inter-Trial-Zeit abgewartet.
            StopPresentationCoroutine();
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
            // Abschlussmarker schreiben, Stimulus ausblenden und Aufzeichnung stoppen.
            StopPresentationCoroutine();
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
            StopPresentationCoroutine();
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
            // Die vorhandene Lab-Toolbox übernimmt weiterhin die eigentlichen
            // Blickdaten. Der Manager startet nur die Aufnahme und schreibt Marker.
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
                "SessionStart;participant={0};session={1};seed={2};planned_trials={3};utc={4};mapping={5};" +
                "stimulus_duration_s={6:F4};noise_duration_s={7:F4};response_timeout_s={8:F4}",
                CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "session"),
                randomSeed,
                trialPlan.Count,
                sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                VisualSpaceRadialMapping.MappingVersion,
                stimulusDurationSeconds,
                noiseMaskDurationSeconds,
                responseTimeoutSeconds));
        }

        private void ResolveReferences()
        {
            // Leere Inspector-Felder werden, soweit eindeutig möglich, aus der
            // offenen Szene ergänzt. Fest eingetragene Referenzen bleiben erhalten.
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

        private void StopPresentationCoroutine()
        {
            if (presentationCoroutine == null)
            {
                return;
            }

            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
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
            // Der Marker steht zusätzlich in der Eye-Tracking-Datei. Dadurch kann
            // man Blicksamples später der gerade gezeigten Bedingung zuordnen.
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "circular_aperture={8};grid_spacing_deg={9:F3};" +
                "grid_spacing_uv={10:F6};visual_space_l={11:F4};content_zoom={12:F4};" +
                "stimulus_duration_s={13:F4};noise_duration_s={14:F4};response_timeout_s={15:F4}",
                presentationIndex,
                trial.SequenceIndex,
                trial.ConditionIndex,
                trial.Repetition,
                trial.AttemptNumber,
                trial.EyePresentation,
                trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees,
                stimulus.UseCircularAperture,
                stimulus.GridLineSpacingDegrees,
                stimulus.GridLineSpacingUv,
                trial.VisualSpaceL,
                trial.ContentZoom,
                stimulusDurationSeconds,
                noiseMaskDurationSeconds,
                responseTimeoutSeconds);
        }

        private string BuildResponsePrompt()
        {
            Key convexKey = keyboardController.GetKeyForResponse(
                CheckerboardCurvatureResponse.Convex);
            Key concaveKey = keyboardController.GetKeyForResponse(
                CheckerboardCurvatureResponse.Concave);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}\n{1}\n\n{2}\n{3}",
                CheckerboardKeyboardController.GetReadableKeyName(convexKey),
                convexResponseText,
                CheckerboardKeyboardController.GetReadableKeyName(concaveKey),
                concaveResponseText);
        }

        private void CaptureFixationAtStimulusEnd()
        {
            // Die Werte werden genau beim Ausblenden des Musters eingefroren.
            // Augenbewegungen beim späteren Lesen verändern diese Daten nicht mehr.
            fixationSnapshotAvailable = true;
            fixationSampleValidAtStimulusEnd = fixationMonitor != null &&
                fixationMonitor.CurrentSampleValid;
            fixationInsideAtStimulusEnd = fixationMonitor != null &&
                fixationMonitor.IsInsideTolerance;
            fixationAngleAtStimulusEnd = fixationMonitor != null
                ? fixationMonitor.CurrentAngleDegrees
                : float.NaN;
            continuousFixationAtStimulusEnd = fixationMonitor != null
                ? fixationMonitor.ContinuousFixationSeconds
                : 0f;
            fixationValidFractionAtStimulusEnd = fixationMonitor != null
                ? fixationMonitor.ValidSampleFraction
                : float.NaN;
        }

        private void ResetPresentationTimes()
        {
            trialStartUnitySeconds = 0d;
            stimulusEndUnitySeconds = 0d;
            responsePromptUnitySeconds = 0d;
            fixationSnapshotAvailable = false;
            fixationSampleValidAtStimulusEnd = false;
            fixationInsideAtStimulusEnd = false;
            fixationAngleAtStimulusEnd = float.NaN;
            continuousFixationAtStimulusEnd = 0f;
            fixationValidFractionAtStimulusEnd = float.NaN;
        }

        private void ResetTrialFixationCounters()
        {
            // Jeder Präsentationsversuch beginnt mit eigenen leeren Zeitzählern.
            currentOffTargetSeconds = 0f;
            currentInvalidGazeSeconds = 0f;
            longestOffTargetSeconds = 0f;
            longestInvalidGazeSeconds = 0f;
        }

        private void OnValidate()
        {
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            stimulusDurationSeconds = Mathf.Max(0.01f, stimulusDurationSeconds);
            noiseMaskDurationSeconds = Mathf.Max(0f, noiseMaskDurationSeconds);
            responseTimeoutSeconds = Mathf.Max(0f, responseTimeoutSeconds);
            maximumOffTargetSeconds = Mathf.Max(0f, maximumOffTargetSeconds);
            maximumInvalidGazeSeconds = Mathf.Max(0f, maximumInvalidGazeSeconds);
            maximumGazeSampleAgeSeconds = Mathf.Max(0.01f, maximumGazeSampleAgeSeconds);
            maximumAttemptsPerTrial = Mathf.Max(0, maximumAttemptsPerTrial);
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
