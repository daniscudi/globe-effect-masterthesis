using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
// Kurzname, damit nicht überall RandomDotSessionState ausgeschrieben werden muss.
using State = GlobeEffect.VRCheckerboard.Experiment.RandomDotSessionState;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    public enum RandomDotSessionState
    {
        Idle,
        InterTrial,
        WaitingForFixation,
        WaitingForHeadMotion, // Active: Kreuz sichtbar, wartet, bis der Kopf dreht
        PresentingMotion,
        WaitingForResponse,
        PausedBetweenMiniBlocks, // Pause zwischen Viertelblöcken; der Versuchsleiter setzt mit F5 fort.
        Completed,
        Aborted,
        ResponseInstructions,
        ActiveMotionInstructions,
        SimulatedTrainingInstructions,
        SimulatedTrainingMotion,
        SimulatedTrainingResponse,
        ActivePracticeInstructions, // Active Practice: Anleitung, Start mit F5
        ActivePracticeMotion, // Active Practice: warten, Punkte oder Rückmeldung
        ActivePracticeResponse, // Active Practice: Antwort nach den Punkten
        FreeMovementPractice // Active Practice 1: freies Schwenken, Ende mit der Continue Key
    }

    /// <summary>
    /// Steuert den ganzen Random-Dot-Versuch.
    ///
    /// So läuft ein Durchgang ab: Die Person schaut auf das Kreuz. Liegt der Blick
    /// ruhig genug, kommen die Punkte. Im SimulatedYaw-Block schwenken sie kurz in
    /// eine Richtung. Im HeadTracked-Block (Active) dreht die Person den Kopf selbst;
    /// die Punkte kommen, sobald der Kopf schneller als der Still-Schwellwert dreht,
    /// und verschwinden früh, wenn er stehen bleibt oder zurückdreht. Danach ist nur
    /// die graue Fläche zu sehen, und die Person antwortet konkav oder konvex, mit den
    /// Pfeiltasten oder mit Trigger und Trackpad-Klick am VR-Controller. Nach der
    /// Antwort blinkt die graue Fläche kurz, dann kommt das Kreuz wieder.
    ///
    /// Welche Instrumentenwerte k und m gezeigt werden, steht vorher fest. Wurde ein
    /// Durchgang abgebrochen (weggeschaut, Kopf gestoppt), antwortet die Person
    /// trotzdem; der Durchgang zählt dann nicht und kommt später noch einmal dran.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class RandomDotExperimentManager : ExperimentManagerBase, IPreviewTakeoverManager
    {
        // Das läuft fast genauso wie beim Checkerboard:
        //
        // StartSession          baut den gemischten Plan und speichert ihn.
        // BeginNextAttempt      zeigt erst mal nur das Kreuz.
        // PresentCurrentTrial   startet die Punkte und die Bewegung.
        //
        // Danach wird auf konkav oder konvex gewartet. Hat die Person
        // danebengeschaut, kommt der Durchgang ganz hinten noch einmal dran.
        [Header("References")]
        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [SerializeField]
        private RandomDotKeyboardController keyboardController;

        [SerializeField]
        private RandomDotHeadSweepMonitor sweepMonitor;

        [SerializeField]
        private RandomDotFixationMonitor fixationMonitor;

        [Header("Session")]
        [SerializeField]
        private string sessionLabel = "random_dot_instrument_pilot";

        [SerializeField]
        private int dotSeedBase = 24680;

        [Header("Trial Plan")]
        [SerializeField]
        [FormerlySerializedAs("stimulusKValues")]
        [FormerlySerializedAs("visualSpaceLValues")]
        [Tooltip("All distortions (k) that are tested. k = 1 is straight, k = 0.5 is the Helmholtz point. " +
            "The k that looks flat to the participant estimates their l.")]
        private List<float> instrumentDistortionKValues = new() { 1.2f, 1f, 0.8f, 0.6f, 0.5f, 0.4f, 0.2f };

        [SerializeField]
        [Tooltip("Magnification of the simulated binoculars (m). 10 = normal 10x binoculars. With several " +
            "values, each is tested.")]
        private List<float> instrumentMagnificationMValues = new() { 10f };

        [SerializeField]
        [FormerlySerializedAs("magnifications")]
        [Tooltip("Extra zoom on top of the magnification. Leave at 1 for the binocular test.")]
        private List<float> contentZoomValues = new() { 1f };

        [SerializeField]
        [Tooltip("Movement type of this session. F5 starts this type, F7 starts the other type as a new " +
            "session.")]
        private RandomDotMotionMode sessionMotionMode = RandomDotMotionMode.SimulatedYaw;

        [SerializeField]
        [Tooltip("How often each k value is shown, in the same order as the k list. Missing entries use " +
            "Repeats Per Condition.")]
        private List<int> repeatsPerKValue = new();

        [Header("Simulated Movement")]
        [SerializeField]
        [Tooltip("Direction of the simulated movement: Horizontal = left/right, Vertical = up/down.")]
        private RandomDotSweepAxis simulatedSweepAxis = RandomDotSweepAxis.Horizontal;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("How long the dots are shown in a trial (s), simulated and head-tracked. In simulated " +
            "trials the dots move once in one direction; left and right come equally often in random order.")]
        private float simulatedSweepSeconds = 0.8f;

        [SerializeField, Range(0.5f, 120f)]
        [Tooltip("Speed of the dots in the middle of the image, in degrees per second. The same at every " +
            "magnification.")]
        private float simulatedImageCenterSpeed = 12f;

        [Header("Head-Tracked Movement")]
        [FormerlySerializedAs("headTrackedCoverageYawDegrees")]
        [SerializeField, Range(3f, 60f)]
        [Tooltip("How far the head may turn from the start direction without reaching the end of the dot " +
            "field, in degrees. Larger values need many more dots.")]
        private float headTurnSafetyDegrees = 30f;

        [SerializeField]
        [Tooltip("Which head turn counts. Horizontal = left or right, like the simulated movement.")]
        private HeadVelocityAxis velocityGateAxis = HeadVelocityAxis.Horizontal;

        [SerializeField]
        [Tooltip("One sweep per trial, alternating: the dots only appear while the head turns the other way " +
            "than in the last trial (right, answer, left, answer, ...). Off = any direction.")]
        private bool alternateSweepDirection = true;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Slower than this the head counts as still (degrees per second). Faster: the dots appear, " +
            "at any speed. If the head gets slower than this while the dots are shown, or turns back, the " +
            "trial stops: the participant still answers and the trial is repeated later.")]
        private float minimumMovingHeadSpeed = 1f;

        [SerializeField, Range(0.02f, 0.5f)]
        [Tooltip("Smooths the measured head speed so small shakes do not count (s). Used for the still rule " +
            "and for the saved head speeds.")]
        private float velocitySmoothingSeconds = 0.1f;

        [Header("Active Practice")]
        [SerializeField]
        [Tooltip("Before head-tracked trials, practise the head movement with the real dots. Nothing is " +
            "saved.")]
        private bool trainActivePanning = true;

        [SerializeField]
        [Tooltip("Before the practice, the computer moves the dots once at the simulated speed while the head " +
            "stays still, so the participant sees an example of a calm speed.")]
        private bool showActiveSpeedExample = true;

        [SerializeField]
        [Tooltip("Practice 1: the participant turns the head freely with k = 1. The dots are shown while the " +
            "head moves and fade out when it stops. No answers. The experimenter ends it with the Continue Key.")]
        private bool freeMovementPractice = true;

        [SerializeField]
        [Tooltip("k values used in practice 2 (trials like in the test). Clear values make it easier to focus " +
            "on the head movement.")]
        private List<float> activePracticeKValues = new() { 0.2f, 1.8f };

        [SerializeField, Range(1, 20)]
        [Tooltip("Practice 2 ends after this many good trials. Good = the dots did not stop early.")]
        private int activePracticeGoodTrials = 6;

        [SerializeField, Range(1, 60)]
        [Tooltip("Practice 2 ends after this many tries, even if not enough were good.")]
        private int activePracticeMaxTrials = 20;

        [Header("Practice")]
        [SerializeField]
        [Tooltip("Practice texts (examples, notes after each practice trial) stay until the participant " +
            "presses the trackpad, or the experimenter presses the key below. Off = they go on by themselves " +
            "after a few seconds.")]
        private bool practiceTextsWaitForPress = true;

        [SerializeField]
        [Tooltip("Key for the experimenter to go on from a practice text.")]
        private Key practiceContinueKey = Key.Space;

        [SerializeField]
        [Tooltip("Before simulated trials, give practice trials without head movement.")]
        private bool trainSimulatedMotion = true;

        [SerializeField]
        private List<float> simulatedTrainingKValues = new() { 0.2f, 1.2f };

        [SerializeField, Min(1)]
        private int simulatedTrainingRepeatsPerValue = 2;

        [SerializeField]
        [Tooltip("Before the practice, show one labelled example for CONVEX and CONCAVE: first long, then " +
            "as short as in the real test.")]
        private bool showSimulatedExamples = true;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("k value of the convex example.")]
        private float exampleConvexK = 1.2f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("k value of the concave example.")]
        private float exampleConcaveK = 0.2f;

        [SerializeField, Range(0.5f, 10f)]
        [Tooltip("How long the first, long showing of each example lasts (s).")]
        private float exampleLongSeconds = 2.5f;

        [SerializeField, Range(0.2f, 10f)]
        [Tooltip("How long the text before each example is shown (s).")]
        private float exampleTextSeconds = 3f;

        [SerializeField]
        [Tooltip("After each practice answer, show if it was right. Only for clear k values (see below), " +
            "never in the real test.")]
        private bool simulatedTrainingFeedback;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Practice k values up to this count as clearly concave.")]
        private float feedbackConcaveMaxK = 0.3f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Practice k values from this count as clearly convex.")]
        private float feedbackConvexMinK = 1.1f;

        [SerializeField, Range(0.2f, 5f)]
        [Tooltip("How long right/wrong is shown (s).")]
        private float feedbackSeconds = 1.5f;

        [Header("Timing")]
        [FormerlySerializedAs("interTrialSeconds")]
        [SerializeField, Min(0f)]
        private float pauseSeconds = 0.25f;

        [SerializeField, Range(0f, 0.3f)]
        [Tooltip("The dots fade in over this time instead of popping up (s). 0 = no fade. Used in all trials " +
            "and practice, simulated and head-tracked. In active practice 1 the dots also fade out this fast.")]
        private float dotsFadeInSeconds = 0.2f;

        [Header("Flash After The Answer")]
        [SerializeField]
        [Tooltip("Colour that briefly tints the whole grey circle after each answer, also after a stopped " +
            "trial. Then the cross comes back. No sounds.")]
        private Color answerFlashColor = Color.cyan;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("How strong the flash is. Keep it low so it does not dazzle. 0 = no flash.")]
        private float answerFlashOpacity = 0.1f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("How long the flash lasts (s). The cross comes back after the flash or after the Grey Pause " +
            "Between Trials, whichever is longer.")]
        private float answerFlashSeconds = 0.2f;

        [Header("Keys")]
        [SerializeField]
        [Tooltip("Starts a new session with the other movement type.")]
        private Key startOtherMotionSessionKey = Key.F7;

        [Header("Current Status")]
        [SerializeField]
        private RandomDotSessionState sessionState = RandomDotSessionState.Idle;

        [SerializeField]
        private int currentMotionBlock;

        [SerializeField]
        private int currentMiniBlock;

        private readonly FrameTimingTracker frameTiming = new();
        private IReadOnlyList<RandomDotTrial> trialPlan;
        private RandomDotTrialQueue trialQueue;
        private RandomDotTrial currentTrial;
        private RandomDotExperimentFiles experimentFiles;
        private int frameTimingStartFrame;
        private int trialsWithSlowFrames;
        private int repeatedAttempts;
        private int headStoppedAttempts;
        // Grund, warum die laufende Darbietung abgebrochen wurde; null = nicht abgebrochen.
        private string stoppedReason;
        private bool eventsSubscribed;

        // Übungen und Anleitungen: in einer Sitzung jeweils nur einmal.
        private bool simulatedPracticeDone;
        private bool activePracticeDone;
        private bool activeInstructionsShown;
        private bool standaloneTraining;
        private bool trainingResponseReceived;
        private CheckerboardCurvatureResponse trainingResponse;
        // Für die Antwortzeit im Experiment Monitor: Beginn der Antwortphase und Tastendruck.
        private double practiceAnswerStartUnitySeconds;
        private double trainingResponseUnitySeconds;
        private bool practiceContinueRequested;

        private Coroutine interTrialCoroutine;
        private Coroutine motionCoroutine;
        private Coroutine simulatedTrainingCoroutine;
        private Coroutine activePracticeCoroutine;
        private RandomDotHeadsetText headsetText;

        // ---- Laufzeit: Kopftempo bei Active ----
        private readonly RandomDotActiveSweepRule activeSweep = new();
        private readonly RandomDotLiveHeadSpeed liveHeadSpeed = new();
        // Active Practice 1: eigene Messung, Richtung egal.
        private readonly RandomDotHeadVelocityGate freeMovementSpeed = new();

        // Flash nach der Antwort: bis wann er noch zu sehen ist.
        private float answerFlashUntil;

        private const string HeadStoppedReason = "head_stopped";

        public RandomDotSessionState SessionState => sessionState;
        public RandomDotMotionMode SessionMotionMode => sessionMotionMode;
        public int CurrentMotionBlock => currentMotionBlock;
        public int CurrentMiniBlock => currentMiniBlock;
        public RandomDotFieldStimulus Stimulus => stimulus;
        public RandomDotLiveHeadSpeed LiveHeadSpeed => liveHeadSpeed;
        public string ControllerSummary => keyboardController != null ? keyboardController.ControllerSummary : "–";
        public string KeyboardSummary => keyboardController != null ? keyboardController.KeyboardSummary : "–";

        /// <summary>Ungültige Durchgänge dieser Sitzung (werden hinten wiederholt).</summary>
        public int RepeatedAttempts => repeatedAttempts;

        /// <summary>Davon abgebrochen, weil der Kopf während der Punkte stehen blieb oder zurückdrehte.</summary>
        public int HeadStoppedAttempts => headStoppedAttempts;

        /// <summary>Wie viele beschriftete Beispiele im laufenden Training schon gezeigt wurden.</summary>
        public int TrainingExamplesShown { get; private set; }

        /// <summary>Ergebnis der letzten Übungsrückmeldung; null = keine Rückmeldung (k nicht eindeutig).</summary>
        public bool? LastTrainingAnswerCorrect { get; private set; }

        /// <summary>Gute Übungstrials in der laufenden oder letzten Active Practice 2.</summary>
        public int ActivePracticeGoodTrials { get; private set; }

        /// <summary>Alle Übungstrials in der laufenden oder letzten Active Practice 2.</summary>
        public int ActivePracticeAttempts { get; private set; }

        /// <summary>Ergebnis des letzten Übungstrials: good oder stopped.</summary>
        public string LastActivePracticeResult { get; private set; } = string.Empty;

        /// <summary>Wartet gerade ein Übungstext auf einen Tastendruck?</summary>
        public bool IsWaitingForPracticeContinue { get; private set; }

        UnityEngine.Object IPreviewTakeoverManager.PreviewStimulus => stimulus;

        public bool IsTrainingActive => sessionState is State.SimulatedTrainingInstructions
            or State.SimulatedTrainingMotion or State.SimulatedTrainingResponse
            or State.ActivePracticeInstructions or State.ActivePracticeMotion or State.ActivePracticeResponse
            or State.FreeMovementPractice;

        // Aktiv ist alles außer: noch nicht gestartet, fertig oder abgebrochen.
        public bool IsSessionActive => !standaloneTraining
            && sessionState is not (State.Idle or State.Completed or State.Aborted);

        /// <summary>So weit darf der Kopf im HeadTracked-Block drehen, bevor die Punktwelt endet.</summary>
        public float HeadTrackedReachDegrees => headTurnSafetyDegrees;

        /// <summary>Kopftempo, das dem simulierten Schwenk entspricht, für das erste m und den ersten Zoom.</summary>
        public float SimulatedHeadSpeedPreview => RandomDotSimulatedSweep.ObjectSpeed(simulatedImageCenterSpeed,
            instrumentMagnificationMValues.Count > 0 ? instrumentMagnificationMValues[0] : 1f,
            contentZoomValues.Count > 0 ? contentZoomValues[0] : 1f);

        /// <summary>Richtung des nächsten Schwenks (Right/Left bzw. Up/Down) oder "any".</summary>
        public string NextSweepDirection => activeSweep.NextDirection;

        // =====================================================================
        // Unity-Ablauf und Tasten
        // =====================================================================

        private void Awake()
        {
            // Möglichst früh suchen, damit später beim Start alles da ist.
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
            else
            {
                ShowWelcomeScreen();
            }
        }

        private void Update()
        {
            // Hier werden die Tasten vom Versuchsleiter abgefragt: starten und
            // abbrechen. Läuft gerade die Bewegung, wird nebenher in jedem Frame
            // geprüft, ob der Blick noch auf dem Kreuz liegt.
            if (previewActive) KeepPreviewPointWorldLikeSession();
            UpdateLiveHeadSpeed();
            if (answerFlashUntil > 0f && Time.unscaledTime >= answerFlashUntil) ClearAnswerFlash();

            // Bildzeiten nur während der Darbietung zählen, ab dem Frame nach dem Start.
            if (sessionState == State.PresentingMotion && Time.frameCount > frameTimingStartFrame)
                frameTiming.Add(Time.unscaledDeltaTime);

            if (HandleKeys(Keyboard.current)) return;

            if (currentTrial != null && IsActiveTrial(currentTrial)
                && sessionState is State.WaitingForHeadMotion or State.PresentingMotion)
            {
                UpdateVelocityGate();
            }

            if (sessionState == State.WaitingForFixation
                && fixationMonitor != null && fixationMonitor.IsReadyForPresentation(maxSampleAgeSeconds))
            {
                PresentOrWaitForHeadMotion();
                return;
            }

            if (sessionState == State.PresentingMotion && requireFixation)
            {
                MonitorFixationDuringMotion();
            }
        }

        // Tasten des Versuchsleiters. True, wenn in diesem Frame eine Taste etwas ausgelöst hat.
        private bool HandleKeys(Keyboard keyboard)
        {
            if (keyboard == null) return false;
            bool idle = !IsSessionActive && !IsTrainingActive;

            if (idle && keyboard[previewKey].wasPressedThisFrame)
            {
                TogglePreview();
                return true;
            }

            if (keyboard[abortSessionKey].wasPressedThisFrame)
            {
                if (previewActive) StopPreview();
                else if (standaloneTraining) StopTrainingAndReturnToWelcome();
                else if (IsSessionActive) AbortSession("ManualAbort");
                return true;
            }

            if (idle && keyboard[trainingKey].wasPressedThisFrame)
            {
                StartTraining();
                return true;
            }

            if (idle && keyboard[startOtherMotionSessionKey].wasPressedThisFrame)
            {
                StartOtherMotionSession();
                return true;
            }

            if (!keyboard[startSessionKey].wasPressedThisFrame) return false;

            // F5 bestätigt die gerade gezeigte Anleitung oder startet eine Sitzung.
            if (sessionState == State.ResponseInstructions) ConfirmResponseInstructions();
            else if (sessionState == State.PausedBetweenMiniBlocks) ContinueAfterBlockPause();
            else if (sessionState is State.SimulatedTrainingInstructions or State.ActivePracticeInstructions)
                ConfirmTrainingInstructions();
            else if (sessionState == State.ActiveMotionInstructions) ConfirmActiveMotionInstructions();
            else if (idle) StartSession();
            return true;
        }

        private void OnDisable()
        {
            RestorePreviewSettings();
            if (standaloneTraining) StopTrainingAndReturnToWelcome();
            UnsubscribeEvents();
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }

            headsetText?.Dispose();
            headsetText = null;
        }

        // =====================================================================
        // Startbildschirm und Vorschau
        // =====================================================================

        public void ShowWelcomeScreen()
        {
            RestorePreviewSettings();
            standaloneTraining = false;
            sessionState = State.Idle;
            ResolveReferences();
            if (stimulus == null || stimulus.Observer == null) return;
            stimulus.SessionRunning = false;
            stimulus.ShowFixationOnly();
            ClearAnswerFlash();
            ShowWelcomeText(completed: false);
        }

        private void ShowWelcomeText(bool completed)
        {
            if (!EnsureHeadsetText()) return;
            headsetText.ShowWelcome(
                ResponseInputController.GetReadableKeyName(startSessionKey),
                ResponseInputController.GetReadableKeyName(startOtherMotionSessionKey),
                ResponseInputController.GetReadableKeyName(trainingKey),
                ResponseInputController.GetReadableKeyName(previewKey), sessionMotionMode, completed);
        }

        // Der Text im Headset braucht die Kopfkamera; ohne sie gibt es keinen Text.
        private bool EnsureHeadsetText()
        {
            if (stimulus == null || stimulus.Observer == null) return false;
            headsetText ??= new RandomDotHeadsetText(stimulus.Observer);
            return true;
        }

        /// <summary>Nur Punkte anschauen und live einstellen. Keine Dateien oder Blickaufnahme.</summary>
        public void TogglePreview()
        {
            if (IsSessionActive || IsTrainingActive) return;
            if (previewActive)
            {
                StopPreview();
                return;
            }

            ResolveReferences();
            if (stimulus == null) return;
            settingsBeforePreview = JsonUtility.ToJson(stimulus);
            headsetText?.Hide();
            previewActive = true;
            stimulus.SessionRunning = false;
            stimulus.PlaceAroundObserver();
            stimulus.Show();
        }

        public void StopPreview()
        {
            if (previewActive) ShowWelcomeScreen();
        }

        private void RestorePreviewSettings()
        {
            if (!previewActive) return;
            previewActive = false;
            if (stimulus != null && !string.IsNullOrEmpty(settingsBeforePreview))
                stimulus.RestorePreviewSettings(settingsBeforePreview);
            // Ein übernommenes Punktbild bleibt nach der Vorschau erhalten.
            if (stimulus != null && lookToKeepAfterPreview != null)
                stimulus.RestorePreviewSettings(lookToKeepAfterPreview);
            settingsBeforePreview = null;
            lookToKeepAfterPreview = null;
        }

        private void KeepPreviewPointWorldLikeSession()
        {
            // In der Vorschau mit aktiver Kopfbewegung dieselbe Reichweite wie in der
            // Sitzung (Head Turn Safety Degrees), nicht World Coverage. Wird bei jeder
            // Änderung von FOV, m, k oder Zoom nachgeführt. Simulated behält World Coverage.
            if (stimulus == null || stimulus.MotionMode != RandomDotMotionMode.HeadTracked) return;
            float coverage = Mathf.Min(RandomDotFieldStimulus.MaximumCoverageDegrees,
                RandomDotFieldStimulus.CoverageNeeded(stimulus.FieldOfViewDegrees, stimulus.ContentZoom,
                    stimulus.InstrumentMagnificationM, stimulus.InstrumentDistortionK, HeadTrackedReachDegrees,
                    stimulus.ApertureEdgeSoftnessDegrees));
            if (Mathf.Abs(coverage - stimulus.WorldCoverageDiameterDegrees) > 0.05f)
                stimulus.SetWorldCoverage(coverage);
        }

        /// <summary>
        /// Übernimmt FOV, m, Zoom, Auge, Bewegungsart, Achse und Geschwindigkeit der
        /// Vorschau in den Versuchsplan und behält das Punktbild (Dichte, Punktgröße,
        /// Edge Softness, Farben). Die k-Liste bleibt unverändert. Beim ersten Mal
        /// wird der bisherige Stand als Original gesichert.
        /// </summary>
        public bool TakeOverPreviewValues()
        {
            if (IsSessionActive || IsTrainingActive) return false;
            ResolveReferences();
            if (stimulus == null) return false;
            if (!HasValuesBeforePreviewTakeover)
                valuesBeforePreviewTakeover = CaptureExperimentValues();
            RandomDotPreviewTakeover.CopyPreviewIntoPlan(stimulus, this);
            if (previewActive)
                lookToKeepAfterPreview = RandomDotPreviewTakeover.CaptureLook(JsonUtility.ToJson(stimulus));
            return true;
        }

        /// <summary>Stand vor dem ersten Übernehmen zurückholen. Beliebig oft möglich.</summary>
        public bool RestoreValuesBeforePreviewTakeover()
        {
            if (IsSessionActive || IsTrainingActive || !HasValuesBeforePreviewTakeover) return false;
            ResolveReferences();
            ApplyExperimentValues(valuesBeforePreviewTakeover);
            valuesBeforePreviewTakeover = string.Empty;
            return true;
        }

        /// <summary>Aktueller Versuchsplan plus Punktbild, so wie sie nach der Vorschau gelten.</summary>
        public string CaptureExperimentValues()
        {
            ResolveReferences();
            string look = lookToKeepAfterPreview
                ?? (previewActive ? settingsBeforePreview : null)
                ?? (stimulus != null ? JsonUtility.ToJson(stimulus) : "{}");
            return RandomDotPreviewTakeover.Capture(this, look);
        }

        public void ApplyExperimentValues(string valuesJson)
        {
            RandomDotPreviewTakeover.ApplyPlan(valuesJson, this);
            ResolveReferences();
            if (stimulus == null) return;
            string look = RandomDotPreviewTakeover.LookOf(valuesJson);
            if (Application.isPlaying)
            {
                stimulus.RestorePreviewSettings(look);
                if (previewActive) lookToKeepAfterPreview = look;
            }
            else
            {
                JsonUtility.FromJsonOverwrite(look, stimulus);
            }
        }

        // =====================================================================
        // Sitzung starten, Anleitungen, abbrechen
        // =====================================================================

        /// <summary>Andere Variante bewusst als neue Sitzung starten, niemals eine laufende Sitzung ändern.</summary>
        public bool StartOtherMotionSession()
        {
            if (IsSessionActive || IsTrainingActive) return false;
            sessionMotionMode = sessionMotionMode == RandomDotMotionMode.SimulatedYaw
                ? RandomDotMotionMode.HeadTracked : RandomDotMotionMode.SimulatedYaw;
            return StartSession();
        }

        public bool StartSession()
        {
            // Das ist der große Startknopf. Der Reihe nach passiert hier:
            // Einstellungen prüfen -> alle Durchgänge bauen und mischen ->
            // die Messdateien anlegen -> Eye Tracking starten ->
            // den ersten Durchgang zeigen.
            if (IsSessionActive || IsTrainingActive)
            {
                Debug.LogWarning("Eine Random-Dot-Sitzung läuft bereits.", this);
                return false;
            }

            StopPreview();

            ResolveReferences();
            SubscribeEvents();
            if (stimulus == null || keyboardController == null || sweepMonitor == null)
            {
                Debug.LogError(
                    "Random-Dot-Stimulus, Tastatursteuerung und Sweep-Monitor müssen zugewiesen sein.", this);
                return false;
            }

            // Ab hier sind stimulus, keyboardController und sweepMonitor sicher da.
            // Der Rest vom Skript muss das deshalb nicht jedes Mal neu prüfen.

            if (requireFixation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Fixationskontrolle ist aktiv, aber der Random-Dot Fixation Monitor fehlt.", this);
                return false;
            }

            // Hat die Auswahl "Auto" keinen echten Eye Tracker gefunden, darf eine
            // Messung nicht unbemerkt mit dem Dummy (Maus-Blick) laufen.
            if (EyeTrackerProviderResolver.TryGetSessionBlockReason(
                eyeTrackingToolbox, requireFixation, out string eyeTrackerBlockReason))
            {
                Debug.LogError(eyeTrackerBlockReason, this);
                return false;
            }

            try
            {
                trialPlan = RandomDotTrialPlanner.CreateSingleMotionPlan(
                    fieldOfViewValues, eyePresentations, instrumentDistortionKValues,
                    instrumentMagnificationMValues, contentZoomValues, sessionMotionMode,
                    repeatsPerCondition, randomSeed, dotSeedBase, simulatedSweepAxis, repeatsPerKValue);
                CheckPointWorlds(trialPlan);
                trialQueue = new RandomDotTrialQueue(trialPlan);

                DateTime sessionStartUtc = DateTime.UtcNow;
                experimentFiles = new RandomDotExperimentFiles(
                    ExperimentOutputPath.Resolve(outputRoot), participantId, sessionLabel,
                    sessionStartUtc, randomSeed);
                experimentFiles.WritePlan(trialPlan);
                activeSessionFolder = experimentFiles.SessionFolder;
                StartEyeTracking(sessionStartUtc);
                experimentFiles.WriteSessionSettings(ExperimentSessionSettings.Capture(
                    this, stimulus, fixationMonitor, keyboardController, eyeTrackingToolbox));
            }
            catch (Exception exception)
            {
                EndSession(State.Aborted);
                Debug.LogError(
                    "Random-Dot-Sitzung konnte nicht gestartet werden: " + exception.Message, this);
                return false;
            }

            StopPendingCoroutines();
            headsetText?.Hide();
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            validTrialsCompleted = 0;
            presentationCount = 0;
            repeatedAttempts = 0;
            headStoppedAttempts = 0;
            trialsWithSlowFrames = 0;
            pausesTaken = 0;
            currentMotionBlock = 0;
            currentMiniBlock = 0;
            simulatedPracticeDone = false;
            activePracticeDone = false;
            activeInstructionsShown = false;
            activeSweep.ResetDirection();
            sessionState = State.InterTrial;
            // Ab jetzt keine Vorschau-Dauerschleife mehr: Jeder Schwenk läuft einmal.
            stimulus.SessionRunning = true;

            Debug.Log($"Random-Dot-Sitzung gestartet: {totalTrials} gültige Trials geplant.\n" +
                activeSessionFolder, this);
            ShowResponseInstructions();
            return true;
        }

        private void ShowResponseInstructions()
        {
            // Vor dem ersten Durchgang liest die Person im Headset, welche Taste
            // welche Antwort gibt. Der Text richtet sich nach der Zuordnung, die
            // am Random Dot Keyboard Controller eingestellt ist. Mit Auto Start
            // (nur zum schnellen Ausprobieren) wird die Anzeige übersprungen.
            Debug.Log("Random-Dot-Antworten: Controller " + ControllerSummary +
                "; Tastatur " + KeyboardSummary + ".", this);
            if (autoStartOnPlay || !EnsureHeadsetText())
            {
                BeginTrainingOrNextAttempt();
                return;
            }

            stimulus.PlaceAroundObserver();
            stimulus.ShowFixationOnly();
            sessionState = State.ResponseInstructions;
            headsetText.ShowResponseInstructions(
                ResponseLinesWithFlashNote(), ResponseInputController.GetReadableKeyName(startSessionKey));
            WriteMarker("ResponseInstructionsShown;task=random_dot_instrument");
        }

        /// <summary>
        /// Beendet die Anzeige der Tastenbelegung. Danach beginnt die Übung oder
        /// der erste Durchgang.
        /// </summary>
        public void ConfirmResponseInstructions()
        {
            if (sessionState != State.ResponseInstructions)
            {
                return;
            }

            headsetText.Hide();
            WriteMarker("ResponseInstructionsConfirmed;task=random_dot_instrument");
            sessionState = State.InterTrial;
            BeginTrainingOrNextAttempt();
        }

        // Die beiden Zeilen mit der Tastenbelegung für die Anzeigen im Headset.
        private string BuildResponseLines()
        {
            return keyboardController.BuildResponseLines(
                "CONVEX (curves outward)", "CONCAVE (curves inward)");
        }

        // Für die Anleitungen: Tastenbelegung plus ein Satz zum Flash nach der Antwort.
        private string ResponseLinesWithFlashNote()
        {
            return BuildResponseLines() + (answerFlashOpacity > 0f
                ? "\n\nAfter each answer the grey field flashes briefly."
                : string.Empty);
        }

        private void BeginTrainingOrNextAttempt()
        {
            // Vor den ersten Trials einer Sitzung: Übung (falls eingeschaltet) und bei
            // Active die Anleitung. Jedes davon kommt pro Sitzung nur einmal.
            bool simulated = sessionMotionMode == RandomDotMotionMode.SimulatedYaw;
            bool canShowText = !autoStartOnPlay && stimulus.Observer != null;
            if (trialQueue != null && trialQueue.TryPeekNext(out _) && canShowText)
            {
                if (simulated && trainSimulatedMotion && !simulatedPracticeDone)
                {
                    ShowSimulatedTrainingInstructions();
                    return;
                }

                if (!simulated && trainActivePanning && !activePracticeDone)
                {
                    ShowActivePracticeInstructions();
                    return;
                }

                if (!simulated && !activeInstructionsShown)
                {
                    EnsureHeadsetText();
                    stimulus.ShowFixationOnly();
                    sessionState = State.ActiveMotionInstructions;
                    headsetText.ShowActiveMotionInstructions(BuildResponseLines(),
                        ResponseInputController.GetReadableKeyName(startSessionKey), velocityGateAxis,
                        alternateSweepDirection);
                    return;
                }
            }

            BeginNextAttempt();
        }

        public void ConfirmActiveMotionInstructions()
        {
            if (sessionState != State.ActiveMotionInstructions) return;
            activeInstructionsShown = true;
            headsetText.Hide();
            sessionState = State.InterTrial;
            BeginNextAttempt();
        }

        public void AbortSession(string reason = "ManualAbort")
        {
            if (standaloneTraining)
            {
                StopTrainingAndReturnToWelcome();
                return;
            }
            // Stoppt Bewegung und Aufnahme sauber. Beim Abbrechen geht nichts
            // verloren, alles bisher Gemessene bleibt im Sitzungsordner liegen.
            if (!IsSessionActive)
            {
                return;
            }

            StopPendingCoroutines();
            headsetText?.Hide();
            sweepMonitor.StopTracking();
            if ((sessionState is State.PresentingMotion or State.WaitingForResponse)
                && currentTrial != null && experimentFiles != null)
            {
                if (stimulusEndUnitySeconds <= trialStartUnitySeconds)
                {
                    stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
                }

                TryAppendResult(CaptureCurrentResult(
                    CheckerboardCurvatureResponse.None, validForAnalysis: false, "aborted:" + reason));
            }

            WriteMarker("SessionAborted;task=random_dot_instrument;reason=" +
                ExperimentFilesBase.SanitizeIdentifier(reason, "unspecified"));
            EndSession(State.Aborted);
        }

        // =====================================================================
        // Übungen (ohne Messdateien)
        // =====================================================================

        /// <summary>Eigenständiges Training mit T, noch ohne Messdateien.</summary>
        public bool StartTraining()
        {
            if (IsSessionActive || IsTrainingActive) return false;
            StopPreview();
            ResolveReferences();
            if (stimulus == null || stimulus.Observer == null
                || keyboardController == null || sweepMonitor == null) return false;
            standaloneTraining = true;
            stimulus.SessionRunning = true;
            if (sessionMotionMode == RandomDotMotionMode.HeadTracked)
                ShowActivePracticeInstructions();
            else
                ShowSimulatedTrainingInstructions();
            return IsTrainingActive;
        }

        public void StopTrainingAndReturnToWelcome()
        {
            if (!standaloneTraining) return;
            StopPendingCoroutines();
            headsetText?.Hide();
            ShowWelcomeScreen();
        }

        public void ConfirmTrainingInstructions()
        {
            if (sessionState == State.SimulatedTrainingInstructions)
                simulatedTrainingCoroutine = StartCoroutine(RunSimulatedTraining());
            else if (sessionState == State.ActivePracticeInstructions)
                activePracticeCoroutine = StartCoroutine(RunActivePractice());
        }

        // Übungs- und Beispieltrials: immer erstes FOV, Auge, m und Zoom des Versuchsplans.
        // seedOffset trennt die Zufallszahlen der Übungen von denen der Messung.
        private IReadOnlyList<RandomDotTrial> BuildPracticePlan(IReadOnlyList<float> kValues,
            RandomDotMotionMode mode, int repeats, int seedOffset)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { fieldOfViewValues[0] }, new[] { eyePresentations[0] }, kValues,
                new[] { instrumentMagnificationMValues[0] }, new[] { contentZoomValues[0] },
                new[] { mode }, repeats, repeats,
                unchecked(randomSeed + seedOffset), dotSeedBase + seedOffset, simulatedSweepAxis);
        }

        // Prüft vor einer Übung, ob deren Punktwelten passen. Sonst zurück zum Start
        // bzw. Sitzung abbrechen.
        private bool PracticePlanFits(IReadOnlyList<RandomDotTrial> plan, string practiceName)
        {
            try
            {
                CheckPointWorlds(plan);
                if (stimulus.Observer == null) throw new InvalidOperationException("Training braucht einen Observer.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(practiceName + ": " + exception.Message, this);
                if (standaloneTraining) ShowWelcomeScreen();
                else AbortSession("InvalidTrainingSettings");
                return false;
            }
        }

        private void FinishPractice()
        {
            headsetText.Hide();
            if (standaloneTraining)
            {
                ShowWelcomeScreen();
                return;
            }
            // In der Sitzung geht es mit der Anleitung bzw. dem ersten Trial weiter.
            sessionState = State.InterTrial;
            BeginTrainingOrNextAttempt();
        }

        // ---- Simulated-Übung ----

        private IReadOnlyList<RandomDotTrial> BuildSimulatedTrainingPlan() => BuildPracticePlan(
            simulatedTrainingKValues, RandomDotMotionMode.SimulatedYaw, simulatedTrainingRepeatsPerValue, 0);

        private void ShowSimulatedTrainingInstructions()
        {
            if (!PracticePlanFits(BuildSimulatedTrainingPlan(), "Simulated-Panning-Training")) return;
            EnsureHeadsetText();
            stimulus.ShowFixationOnly();
            sessionState = State.SimulatedTrainingInstructions;
            headsetText.ShowSimulatedInstructions(ResponseLinesWithFlashNote(),
                ResponseInputController.GetReadableKeyName(startSessionKey), simulatedTrainingFeedback,
                showSimulatedExamples);
        }

        private IEnumerator RunSimulatedTraining()
        {
            sessionState = State.SimulatedTrainingMotion;
            practiceLog.Clear();
            if (showSimulatedExamples)
            {
                yield return ShowSimulatedExamples();
            }

            IReadOnlyList<RandomDotTrial> practice = BuildSimulatedTrainingPlan();
            for (int index = 0; index < practice.Count; index++)
            {
                headsetText.Hide();
                ConfigureStimulus(practice[index]);
                stimulus.ShowFixationOnly();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, pauseSeconds));
                stimulus.RestartMotionPhase();
                sessionState = State.SimulatedTrainingMotion;
                stimulus.Show(dotsFadeInSeconds);
                yield return new WaitForSecondsRealtime(simulatedSweepSeconds);
                stimulus.ShowFixationOnly();
                headsetText.ShowSimulatedResponse(index + 1, practice.Count, BuildResponseLines());
                yield return WaitForPracticeAnswer(State.SimulatedTrainingResponse);
                LogPracticeAnswer("Simulated", practice[index].InstrumentDistortionK, string.Empty);

                // Subjektives Formurteil: eine "richtige" Antwort gibt es nur bei
                // eindeutigen Extremwerten. Dort sagt die Rückmeldung, was konvex
                // und konkav heißt, ohne eine Grenze in der Mitte vorzugeben.
                CheckerboardCurvatureResponse expected = simulatedTrainingFeedback
                    ? ExpectedTrainingResponse(practice[index].InstrumentDistortionK,
                        feedbackConcaveMaxK, feedbackConvexMinK)
                    : CheckerboardCurvatureResponse.None;
                LastTrainingAnswerCorrect = expected == CheckerboardCurvatureResponse.None
                    ? null : trainingResponse == expected;
                if (LastTrainingAnswerCorrect.HasValue)
                {
                    headsetText.ShowSimulatedFeedback(LastTrainingAnswerCorrect.Value, CategoryLabel(expected));
                    yield return PracticeTextPause(feedbackSeconds);
                }
            }

            simulatedPracticeDone = true;
            simulatedTrainingCoroutine = null;
            FinishPractice();
        }

        private IEnumerator ShowSimulatedExamples()
        {
            // Wie beim Checkerboard: Zuerst je ein deutliches, beschriftetes Beispiel,
            // damit klar ist, was "konvex" und "konkav" heißt. Danach Übung ohne
            // Hinweise; eine Grenze in der Mitte wird so nicht vorgegeben.
            TrainingExamplesShown = 0;
            var examples = new[]
            {
                (category: CheckerboardCurvatureResponse.Convex, k: exampleConvexK),
                (category: CheckerboardCurvatureResponse.Concave, k: exampleConcaveK)
            };
            for (int index = 0; index < examples.Length; index++)
            {
                RandomDotTrial example = BuildPracticePlan(new[] { examples[index].k },
                    RandomDotMotionMode.SimulatedYaw, 1, 50000 + index)[0];
                string label = CategoryLabel(examples[index].category);

                headsetText.ShowSimulatedExample(label, "Watch the dots. This example is shown longer.");
                yield return PracticeTextPause(exampleTextSeconds);
                yield return PresentSimulatedExample(example, exampleLongSeconds);

                headsetText.ShowSimulatedExample(label, string.Format(CultureInfo.InvariantCulture,
                    "The same example again,\nas briefly as in the experiment ({0:0.0#} s).",
                    simulatedSweepSeconds));
                yield return PracticeTextPause(exampleTextSeconds);
                yield return PresentSimulatedExample(example, simulatedSweepSeconds);
                TrainingExamplesShown++;
            }

            headsetText.ShowSimulatedPracticeStart(simulatedTrainingFeedback);
            yield return PracticeTextPause(exampleTextSeconds);
        }

        private IEnumerator PresentSimulatedExample(RandomDotTrial example, float seconds)
        {
            headsetText.Hide();
            ConfigureStimulus(example, seconds);
            stimulus.ShowFixationOnly();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, pauseSeconds));
            stimulus.RestartMotionPhase();
            stimulus.Show(dotsFadeInSeconds);
            yield return new WaitForSecondsRealtime(seconds);
            stimulus.ShowFixationOnly();
        }

        private static string CategoryLabel(CheckerboardCurvatureResponse category) =>
            category == CheckerboardCurvatureResponse.Convex ? "CONVEX (curves outward)" : "CONCAVE (curves inward)";

        /// <summary>
        /// Welche Antwort im Training als eindeutig gilt: k bis concaveMaxK = konkav,
        /// k ab convexMinK = konvex, dazwischen None (keine Rückmeldung).
        /// </summary>
        public static CheckerboardCurvatureResponse ExpectedTrainingResponse(
            float k, float concaveMaxK, float convexMinK)
        {
            if (k <= concaveMaxK) return CheckerboardCurvatureResponse.Concave;
            if (k >= convexMinK) return CheckerboardCurvatureResponse.Convex;
            return CheckerboardCurvatureResponse.None;
        }

        // ---- Active Practice ----

        private IReadOnlyList<RandomDotTrial> BuildActivePracticePlan()
        {
            // Genug Übungstrials für die Höchstzahl, k abwechselnd aus der Liste.
            int repeats = Mathf.Max(1, Mathf.CeilToInt(activePracticeMaxTrials /
                (float)Mathf.Max(1, activePracticeKValues.Count)));
            return BuildPracticePlan(activePracticeKValues, RandomDotMotionMode.HeadTracked, repeats, 70000);
        }

        // Active Practice 1: ein Trial mit k = 1 (gerade), damit es nur um die Bewegung geht.
        private RandomDotTrial BuildFreeMovementTrial() =>
            BuildPracticePlan(new[] { 1f }, RandomDotMotionMode.HeadTracked, 1, 72000)[0];

        private void ShowActivePracticeInstructions()
        {
            var plan = new List<RandomDotTrial>(BuildActivePracticePlan()) { BuildFreeMovementTrial() };
            if (!PracticePlanFits(plan, "Active Practice")) return;
            EnsureHeadsetText();
            stimulus.ShowFixationOnly();
            sessionState = State.ActivePracticeInstructions;
            headsetText.ShowActivePracticeInstructions(ResponseLinesWithFlashNote(),
                ResponseInputController.GetReadableKeyName(startSessionKey), showActiveSpeedExample,
                freeMovementPractice);
            WriteMarker("ActivePracticeInstructions;task=random_dot_instrument");
        }

        private IEnumerator RunActivePractice()
        {
            // Erst ein Beispiel für ein ruhiges Tempo, dann Practice 1 (frei schwenken,
            // ohne Antworten) und Practice 2: Trials genau wie im Test, ohne Texte
            // dazwischen und ohne Daten zu speichern.
            sessionState = State.ActivePracticeMotion;
            practiceLog.Clear();
            ActivePracticeGoodTrials = 0;
            ActivePracticeAttempts = 0;
            LastActivePracticeResult = string.Empty;
            activeSweep.ResetDirection();

            if (showActiveSpeedExample)
            {
                // Simulierter Schwenk mit dem Tempo des Simulated-Blocks; k = 1 (gerade),
                // damit das Beispiel nur das Tempo zeigt und keine Form vorgibt.
                headsetText.ShowActiveSpeedExample();
                yield return PracticeTextPause(exampleTextSeconds);
                RandomDotTrial speedExample = BuildPracticePlan(new[] { 1f }, RandomDotMotionMode.SimulatedYaw,
                    1, 71000)[0];
                yield return PresentSimulatedExample(speedExample, exampleLongSeconds);
            }

            if (freeMovementPractice)
            {
                headsetText.ShowFreeMovementPracticeStart(velocityGateAxis);
                yield return PracticeTextPause(exampleTextSeconds);
                yield return RunFreeMovementPractice();
                sessionState = State.ActivePracticeMotion;
            }

            headsetText.ShowTrialPracticeStart(BuildResponseLines(), velocityGateAxis, alternateSweepDirection);
            yield return PracticeTextPause(exampleTextSeconds);
            headsetText.Hide();

            IReadOnlyList<RandomDotTrial> practice = BuildActivePracticePlan();
            while (ActivePracticeGoodTrials < activePracticeGoodTrials && ActivePracticeAttempts < activePracticeMaxTrials)
            {
                RandomDotTrial trial = practice[ActivePracticeAttempts % practice.Count];
                ActivePracticeAttempts++;
                ConfigureStimulus(trial);
                stimulus.ShowFixationOnly();
                sessionState = State.ActivePracticeMotion;
                ResetFixationChecks();

                // Wie im echten Trial: erst ruhiger Blick (falls verlangt), dann warten,
                // bis der Kopf in der erlaubten Richtung dreht.
                while (!GazeReadyForDots()) yield return null;
                BeginActiveSweepWait();
                while (true)
                {
                    SampleActiveSweep(presenting: false);
                    if (activeSweep.ReadyForDots) break;
                    yield return null;
                }

                activeSweep.BeginPresentation(stimulus.Observer.forward, stimulus.Observer.up);
                ResetFixationChecks();
                stimulus.PlaceAroundObserver();
                stimulus.RestartMotionPhase();
                stimulus.Show(dotsFadeInSeconds);
                string stopped = null;
                double shownAt = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble - shownAt < simulatedSweepSeconds)
                {
                    yield return null;
                    SampleActiveSweep(presenting: true);
                    stopped = activeSweep.HeadStopped ? HeadStoppedReason
                        : requireFixation ? CheckGazeDuringDots() : null;
                    if (stopped != null) break;
                }

                // Wie im Test: Punkte und Kreuz weg, antworten (auch nach einem Abbruch), Flash.
                yield return WaitForPracticeAnswer(State.ActivePracticeResponse);

                LastActivePracticeResult = stopped == null ? "good" : "stopped";
                if (stopped == null) ActivePracticeGoodTrials++;
                LogPracticeAnswer("Practice 2", trial.InstrumentDistortionK,
                    stopped == null ? "good" : "stopped (" + stopped + ")");
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "Active Practice 2, Trial {0}: {1}{2} ({3}/{4} gut).",
                    ActivePracticeAttempts, LastActivePracticeResult, stopped != null ? " (" + stopped + ")" : "",
                    ActivePracticeGoodTrials, activePracticeGoodTrials), this);
                WriteMarker("ActivePracticeTrial;task=random_dot_instrument;attempt={0};result={1};reason={2};" +
                    "good={3}", ActivePracticeAttempts, LastActivePracticeResult, stopped ?? "none",
                    ActivePracticeGoodTrials);
            }

            WriteMarker("ActivePracticeCompleted;task=random_dot_instrument;attempts={0};good={1}",
                ActivePracticeAttempts, ActivePracticeGoodTrials);
            activePracticeDone = true;
            activePracticeCoroutine = null;
            FinishPractice();
        }

        // Active Practice 1: freies Schwenken mit k = 1, ohne Antworten. Die Punkte sind
        // da, solange der Kopf schneller als der Still-Schwellwert dreht (Richtung egal),
        // und werden ausgeblendet, wenn er langsamer wird. Mit Require Fixation auch,
        // wenn die Person zu lange vom Kreuz wegschaut. Der Versuchsleiter beendet die
        // Übung mit der Continue Key.
        private IEnumerator RunFreeMovementPractice()
        {
            ConfigureStimulus(BuildFreeMovementTrial());
            stimulus.ShowFixationOnly();
            headsetText.Hide();
            sessionState = State.FreeMovementPractice;
            freeMovementSpeed.Reset();
            ResetFixationChecks();
            WriteMarker("FreeMovementPracticeStarted;task=random_dot_instrument");

            bool dotsOn = false;
            Vector3 placedForward = stimulus.Observer.forward;
            // Ein Druck aus dem Frame davor (der Text davor) zählt nicht.
            yield return null;
            practiceContinueRequested = false;
            while (!ExperimenterContinuePressed())
            {
                Transform head = stimulus.Observer;
                freeMovementSpeed.AddSample(head.forward, Time.unscaledDeltaTime, velocityGateAxis,
                    velocitySmoothingSeconds);
                float speed = freeMovementSpeed.SpeedDegreesPerSecond;
                bool moving = speed >= minimumMovingHeadSpeed;
                if (dotsOn)
                {
                    // Kurz vor dem Rand der Punktwelt ausblenden. Beim nächsten Einblenden
                    // wird sie neu um den Kopf gelegt.
                    bool nearEdge = Vector3.Angle(placedForward, head.forward) + speed * dotsFadeInSeconds
                        >= headTurnSafetyDegrees;
                    if (!moving || nearEdge || (requireFixation && CheckGazeDuringDots() != null))
                    {
                        stimulus.FadeOutDots(dotsFadeInSeconds);
                        dotsOn = false;
                    }
                }
                else if (moving && !stimulus.PointsVisible && GazeReadyForDots())
                {
                    // Erst wieder einblenden, wenn das Ausblenden fertig ist.
                    stimulus.PlaceAroundObserver();
                    stimulus.RestartMotionPhase();
                    stimulus.Show(dotsFadeInSeconds);
                    placedForward = head.forward;
                    ResetFixationChecks();
                    dotsOn = true;
                }
                yield return null;
            }

            practiceContinueRequested = false;
            stimulus.ShowFixationOnly();
            WriteMarker("FreeMovementPracticeEnded;task=random_dot_instrument");
        }

        // Nur die Taste des Versuchsleiters (oder ContinuePractice), nicht das Trackpad.
        private bool ExperimenterContinuePressed()
        {
            Keyboard keyboard = Keyboard.current;
            return practiceContinueRequested
                || (keyboard != null && keyboard[practiceContinueKey].wasPressedThisFrame);
        }

        // ---- Gemeinsam für beide Übungen ----

        // Wartet auf die Antwort im Übungstrial, wie in der Messung: nur die graue
        // Fläche, nach der Antwort der Flash, danach kommt das Kreuz wieder.
        private IEnumerator WaitForPracticeAnswer(State responseState)
        {
            stimulus.ShowEmptyField();
            trainingResponseReceived = false;
            practiceAnswerStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = responseState;
            while (!trainingResponseReceived) yield return null;
            ShowAnswerFlash();
            float wait = SecondsAfterAnswer;
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
        }

        // Eine Zeile im Experiment Monitor, nur für den Versuchsleiter, nicht gespeichert.
        // Der Hinweis "näher am Beispiel" gilt für alle Übungstrials (die Übungslisten
        // enthalten nur deutliche k); bei gleichem Abstand bleibt er leer.
        private void LogPracticeAnswer(string part, float k, string note)
        {
            practiceLog.Add(part, k, trainingResponse,
                trainingResponseUnitySeconds - practiceAnswerStartUnitySeconds,
                PracticeAnswerLog.CloserToExample(k, exampleConvexK, exampleConcaveK), note);
        }

        /// <summary>
        /// Weiter vom aktuellen Übungstext (wie Leertaste oder Trackpad). Für Tests und
        /// falls der Versuchsleiter es über den Inspector auslösen möchte.
        /// </summary>
        public void ContinuePractice() => practiceContinueRequested = true;

        // Übungstexte: bleiben stehen, bis Trackpad (Person) oder Leertaste (Versuchsleiter)
        // gedrückt wird. Mit "Practice Texts Wait For Press" aus nach der alten festen Zeit.
        private IEnumerator PracticeTextPause(float seconds)
        {
            if (!practiceTextsWaitForPress)
            {
                yield return new WaitForSecondsRealtime(seconds);
                yield break;
            }

            headsetText?.AppendFooter("PRESS TRACKPAD TO CONTINUE");
            IsWaitingForPracticeContinue = true;
            // Ein Druck aus dem Frame davor (zum Beispiel die Antwort) zählt nicht.
            yield return null;
            practiceContinueRequested = false;
            while (true)
            {
                Keyboard keyboard = Keyboard.current;
                if (practiceContinueRequested
                    || (keyboard != null && keyboard[practiceContinueKey].wasPressedThisFrame)
                    || (VrControllerResponses.TryReadPress(out VrControllerButton button)
                        && button == VrControllerButton.TrackpadClick))
                    break;
                yield return null;
            }
            IsWaitingForPracticeContinue = false;
            practiceContinueRequested = false;
        }

        // =====================================================================
        // Ein Durchgang: Kreuz, (Kopfbewegung,) Punkte, Antwort
        // =====================================================================

        private void BeginNextAttempt()
        {
            // Holt den nächsten Durchgang aus der Warteschlange. Erst ist nur das
            // Kreuz zu sehen. Die Punkte kommen erst, wenn der Blick ruhig liegt.
            interTrialCoroutine = null;
            if (trialQueue == null || !trialQueue.TryTakeNext(out currentTrial))
            {
                CompleteSession();
                return;
            }

            presentationCount++;
            currentTrialNumber = validTrialsCompleted + 1;
            stoppedReason = null;
            currentMotionBlock = currentTrial.MotionBlockIndex;
            currentMiniBlock = currentTrial.MiniBlockIndex;
            ConfigureStimulus(currentTrial);
            sweepMonitor.ResetForTrial();
            ResetFixationChecks();
            stimulusEndUnitySeconds = 0d;

            if (requireFixation)
            {
                sessionState = State.WaitingForFixation;
                stimulus.ShowFixationOnly();
                WriteMarker("FixationAcquisitionStart;task=random_dot_instrument;sequence={0};attempt={1}",
                    currentTrial.SequenceIndex, currentTrial.AttemptNumber);
            }
            else
            {
                PresentOrWaitForHeadMotion();
            }
        }

        // simulatedSecondsOverride: nur für die längeren Trainingsbeispiele.
        private void ConfigureStimulus(RandomDotTrial trial, float? simulatedSecondsOverride = null)
        {
            stimulus.Hide();
            stimulus.SetAngularDiameter(trial.AngularDiameterDegrees);
            stimulus.SetInstrumentDistortionK(trial.InstrumentDistortionK);
            stimulus.SetInstrumentMagnification(trial.InstrumentMagnificationM);
            stimulus.SetContentZoom(trial.ContentZoom);
            stimulus.SetEyePresentation(trial.EyePresentation);
            stimulus.SetMotionMode(trial.MotionMode);
            stimulus.SetSweepAxis(trial.SweepAxis);
            stimulus.SetSimulatedSweep(SweepAmplitude(trial, simulatedSecondsOverride), SweepSpeed(trial));
            stimulus.SetSweepDirection(trial.SweepDirection);
            // Jeder Trial bekommt seine eigene Punktwelt: so groß wie nötig und so
            // dicht, dass es in der Bildmitte bei jedem m gleich aussieht.
            stimulus.ConfigurePointField(trial.DotSeed, WorldCoverageFor(trial, simulatedSecondsOverride));
            stimulus.PlaceAroundObserver();
        }

        // Aktive Trials warten zuerst auf eine gleichmäßige Kopfbewegung.
        // Simulierte Trials starten sofort.
        private void PresentOrWaitForHeadMotion()
        {
            if (IsActiveTrial(currentTrial))
            {
                BeginWaitingForHeadMotion();
            }
            else
            {
                PresentCurrentTrial();
            }
        }

        private void PresentCurrentTrial()
        {
            // Gibt k, m, Zusatzzoom, FOV, Augenmodus und Bewegungsart an das
            // Punktfeld weiter und lässt die Bewegung von vorne losgehen.
            if (currentTrial == null)
            {
                return;
            }

            ResetFixationChecks();
            ClearAnswerFlash();
            stoppedReason = null;
            // Die aktive Punktwelt wird erst jetzt am aktuellen Kopf ausgerichtet.
            // Zwischen Fixationsbeginn und Trialstart kann sich der Kopf bewegen.
            stimulus.PlaceAroundObserver();
            sweepMonitor.ResetForTrial();
            stimulus.RestartMotionPhase();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulusEndUnitySeconds = 0d;
            frameTiming.Reset(FrameTimingTracker.CurrentExpectedFrameMilliseconds());
            frameTimingStartFrame = Time.frameCount;
            sessionState = State.PresentingMotion;

            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show(dotsFadeInSeconds);
            motionCoroutine = StartCoroutine(EndMotionAfterDuration());

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Random-Dot-Trial {0}/{1}, Präsentation {2}: k={3:F3}, m={4:F2}x, ContentZoom={5:F2}, " +
                "{6}, Achse {7}, Richtung {8}, {9} Punkte, Versuch {10}. " +
                "Fixationskreuz anschauen; Antwort folgt nach der Bewegung.",
                currentTrialNumber, totalTrials, presentationCount,
                currentTrial.InstrumentDistortionK, currentTrial.InstrumentMagnificationM,
                currentTrial.ContentZoom, currentTrial.MotionMode, currentTrial.SweepAxis,
                currentTrial.DirectionLabel, stimulus.DotCount, currentTrial.AttemptNumber), this);
        }

        private IEnumerator EndMotionAfterDuration()
        {
            // Eine Coroutine wartet, ohne dass dabei alles andere stehen bleibt.
            // Unity zeichnet weiter und das Eye Tracking misst weiter.
            yield return new WaitForSecondsRealtime(simulatedSweepSeconds);
            motionCoroutine = null;
            EndMotionPresentation();
        }

        private void EndMotionPresentation()
        {
            // Die Bewegung ist vorbei. Ab jetzt wird nur noch auf die Antwort gewartet.
            if (sessionState != State.PresentingMotion || currentTrial == null)
            {
                return;
            }

            sweepMonitor.StopTracking();
            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            // Punkte und Kreuz aus, nur der graue Kreis bleibt. So gibt es beim
            // Wechsel zur Antwortphase keinen globalen Helligkeitssprung im Headset.
            stimulus.ShowEmptyField();
            sessionState = State.WaitingForResponse;
            WriteMarker("StimulusEnded;task=random_dot_instrument;sequence={0};attempt={1};duration_s={2:F4};" +
                "stopped={3}", currentTrial.SequenceIndex, currentTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds, stoppedReason ?? "none");
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            if (response == CheckerboardCurvatureResponse.None) return;

            // Übungen: Antwort merken und die Antwortphase sofort schließen, damit ein
            // zweiter Tastendruck nicht für den nächsten Übungstrial zählt.
            if (sessionState is State.SimulatedTrainingResponse or State.ActivePracticeResponse)
            {
                trainingResponseReceived = true;
                trainingResponse = response;
                trainingResponseUnitySeconds = Time.realtimeSinceStartupAsDouble;
                sessionState = sessionState == State.SimulatedTrainingResponse
                    ? State.SimulatedTrainingMotion : State.ActivePracticeMotion;
                return;
            }

            // Drückt die Person schon während der Bewegung, zählt das nicht. Sie
            // soll sich erst die ganze Bewegung ansehen und dann entscheiden.
            // Die erste Antwort beendet die Antwortphase sofort. Ein zweiter
            // Tastendruck landet deshalb in keinem Durchgang, auch nicht im nächsten.
            if (sessionState != State.WaitingForResponse || currentTrial == null)
            {
                return;
            }

            // Abgebrochene Darbietung: Antwort speichern, aber als ungültig.
            if (stoppedReason != null)
            {
                RecordStoppedAttempt(response);
                return;
            }

            RandomDotTrialResult result = CaptureCurrentResult(response, validForAnalysis: true, "valid");
            if (!TryAppendResult(result))
            {
                return;
            }

            ShowAnswerFlash();
            validTrialsCompleted++;
            WriteMarker(
                "TrialResponse;task=random_dot_instrument;sequence={0};attempt={1};" +
                "response={2};response_s={3:F4};valid=1",
                currentTrial.SequenceIndex, currentTrial.AttemptNumber, response, result.ResponseTimeSeconds);
            FinishAttemptAndScheduleNext();
        }

        private void MonitorFixationDuringMotion()
        {
            // Zwei Dinge werden getrennt mitgezählt: wie lange die Person am Kreuz
            // vorbeigeschaut hat, und wie lange gar keine Daten da waren.
            //
            // Getrennt deshalb, weil ein Blinzeln etwas anderes ist als wegschauen.
            // Wird eine der beiden Zeiten am Stück überschritten, zählt nur dieser
            // eine Durchgang nicht. Die Sitzung läuft normal weiter.
            string problem = CheckGazeDuringDots();
            if (problem != null)
            {
                StopCurrentPresentation(problem);
            }
        }

        // Ein Frame Blickkontrolle während der Punkte: Grund für einen Abbruch oder null.
        private string CheckGazeDuringDots()
        {
            if (fixationMonitor == null) return "missing_fixation_monitor";
            bool sampleUsable =
                fixationMonitor.HasRecentSample(maxSampleAgeSeconds) && fixationMonitor.CurrentSampleValid;
            lookAway.Add(Time.unscaledDeltaTime, sampleUsable, fixationMonitor.IsInsideTolerance);
            return lookAway.FindProblem(maxLookAwaySeconds, maxNoDataSeconds);
        }

        // Darf es mit den Punkten losgehen? Ohne Require Fixation immer.
        private bool GazeReadyForDots() => !requireFixation
            || (fixationMonitor != null && fixationMonitor.IsReadyForPresentation(maxSampleAgeSeconds));

        private void StopCurrentPresentation(string reason)
        {
            // Punkte und Kreuz sofort weg, dann die normale Antwortphase. Gespeichert
            // wird erst mit der Antwort (als ungültig), danach kommt der Durchgang
            // ganz hinten noch einmal dran.
            if (sessionState != State.PresentingMotion || currentTrial == null)
            {
                return;
            }

            StopAndClear(ref motionCoroutine);
            stoppedReason = reason;
            repeatedAttempts++;
            if (reason == HeadStoppedReason) headStoppedAttempts++;
            WriteMarker("TrialStopped;task=random_dot_instrument;sequence={0};attempt={1};reason={2}",
                currentTrial.SequenceIndex, currentTrial.AttemptNumber, reason);
            EndMotionPresentation();
        }

        private void RecordStoppedAttempt(CheckerboardCurvatureResponse response)
        {
            // Der abgebrochene Versuch wird mit Antwort gespeichert, zählt aber nicht.
            // Derselbe Durchgang kommt mit höherer Versuchsnummer ganz hinten wieder
            // in die Warteschlange.
            string reason = stoppedReason;
            RandomDotTrial invalidTrial = currentTrial;
            RandomDotTrialResult result = CaptureCurrentResult(
                response, validForAnalysis: false, "invalid:" + reason);
            if (!TryAppendResult(result))
            {
                return;
            }

            ShowAnswerFlash();
            WriteMarker(
                "TrialInvalid;task=random_dot_instrument;sequence={0};attempt={1};reason={2};" +
                "response={3};response_s={4:F4};off_target_s={5:F4};invalid_gaze_s={6:F4};" +
                "mean_yaw_speed_deg_s={7:F4};peak_yaw_speed_deg_s={8:F4};max_abs_yaw_deg={9:F4}",
                invalidTrial.SequenceIndex, invalidTrial.AttemptNumber, reason, response,
                result.ResponseTimeSeconds, lookAway.LongestLookAwaySeconds, lookAway.LongestNoDataSeconds,
                sweepMonitor.MeanAbsoluteYawSpeedDegreesPerSecond,
                sweepMonitor.PeakAbsoluteYawSpeedDegreesPerSecond,
                sweepMonitor.MaximumAbsoluteYawDegrees);

            if (maxRepeatsPerTrial > 0 && invalidTrial.AttemptNumber >= maxRepeatsPerTrial)
            {
                WriteMarker(
                    "SessionAborted;task=random_dot_instrument;reason=maximum_repeat_attempts_reached");
                EndSession(State.Aborted);
                Debug.LogError(
                    "Die maximale Zahl an Wiederholungen wurde erreicht. Die Sitzung wurde beendet.", this);
                return;
            }

            RandomDotTrial repeat = trialQueue.AppendRepeatedAttempt(invalidTrial);
            WriteMarker(
                "TrialRepeatQueued;task=random_dot_instrument;sequence={0};" +
                "next_attempt={1};queue_position={2}",
                repeat.SequenceIndex, repeat.AttemptNumber, trialQueue.Count);
            FinishAttemptAndScheduleNext();
        }

        private RandomDotTrialResult CaptureCurrentResult(
            CheckerboardCurvatureResponse response, bool validForAnalysis, string status)
        {
            // Hier wird alles zu diesem Durchgang in ein Objekt gepackt: die
            // Bedingung, die Antwort, die Bewegung, die Zeiten und die Blickwerte.
            // Dieses Objekt geht danach an die Dateiklasse, die daraus eine
            // CSV-Zeile macht.
            double responseTime = Time.realtimeSinceStartupAsDouble;
            bool active = IsActiveTrial(currentTrial);
            var result = new RandomDotTrialResult
            {
                Trial = currentTrial,
                PresentationIndex = presentationCount,
                TrialStartUtc = trialStartUtc,
                TrialStartUnitySeconds = trialStartUnitySeconds,
                StimulusEndUnitySeconds = stimulusEndUnitySeconds > trialStartUnitySeconds
                    ? stimulusEndUnitySeconds : responseTime,
                ResponseUnitySeconds = responseTime,
                Response = response,
                ValidForAnalysis = validForAnalysis,
                Status = status,
                MinimumYawDegrees = sweepMonitor.MinimumYawDegrees,
                MaximumYawDegrees = sweepMonitor.MaximumYawDegrees,
                MaximumAbsoluteYawDegrees = sweepMonitor.MaximumAbsoluteYawDegrees,
                MeanAbsoluteYawSpeedDegreesPerSecond = sweepMonitor.MeanAbsoluteYawSpeedDegreesPerSecond,
                PeakAbsoluteYawSpeedDegreesPerSecond = sweepMonitor.PeakAbsoluteYawSpeedDegreesPerSecond,
                ApertureEdgeSoftnessDegrees = stimulus.ApertureEdgeSoftnessDegrees,
                DotCount = stimulus.DotCount,
                WorldCoverageDiameterDegrees = stimulus.WorldCoverageDiameterDegrees,
                CarrierRadiusMeters = stimulus.FieldRadiusMeters,
                Gaze = fixationMonitor != null ? fixationMonitor.TakeSnapshot() : FixationSnapshot.None,
                LongestOffTargetSeconds = lookAway.LongestLookAwaySeconds,
                LongestInvalidGazeSeconds = lookAway.LongestNoDataSeconds
            };
            // Kopie: der Zähler wird für den nächsten Trial zurückgesetzt.
            result.FrameTiming.CopyFrom(frameTiming);

            if (!active)
            {
                // Simulated: programmierter Schwenk.
                result.SweepAmplitudeDegrees = stimulus.SweepAmplitudeDegrees;
                result.SweepSpeedDegreesPerSecond = stimulus.SweepSpeedDegreesPerSecond;
                return result;
            }

            // Active: Kopftempo, Richtung und Bewegung neben der Schwenkachse.
            result.HeadVelocity = activeSweep.Summarize(SimulatedHeadSpeed(currentTrial));
            result.HeadDirection = activeSweep.Direction;
            result.HeadOffAxisSpeed = activeSweep.OffAxis.MeanOffAxisSpeed;
            result.HeadOffAxisDeviation = activeSweep.OffAxis.MaxOffAxisDeviationDegrees;
            result.HeadRollDeviation = activeSweep.OffAxis.MaxRollDeviationDegrees;
            return result;
        }

        private bool TryAppendResult(RandomDotTrialResult result)
        {
            // Wenn das Schreiben schiefgeht, zum Beispiel weil die Datei noch in
            // Excel offen ist, wird abgebrochen. Sonst würde die Messung munter
            // weiterlaufen und am Ende wäre nichts gespeichert.
            try
            {
                experimentFiles.AppendResult(result, totalTrials);
                if (result.FrameTiming.SlowFrames > 0) trialsWithSlowFrames++;
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
            // Nach der Antwort: Flash auf der grauen Fläche, kurz warten, dann den
            // nächsten Durchgang holen (erst dann ist das Kreuz wieder da). Der
            // neutrale graue Kreis bleibt durchgehend sichtbar. So wechseln weder
            // mittlere Helligkeit noch Größe der kreisförmigen Öffnung.
            StopAndClear(ref motionCoroutine);
            stimulus.ShowEmptyField();
            currentTrial = null;
            stoppedReason = null;

            if (trialQueue == null || !trialQueue.TryPeekNext(out _))
            {
                CompleteSession();
                return;
            }

            if (TryStartBlockPause())
            {
                return;
            }

            sessionState = State.InterTrial;
            if (SecondsAfterAnswer <= 0f)
            {
                BeginNextAttempt();
            }
            else
            {
                interTrialCoroutine = StartCoroutine(BeginNextAttemptAfterDelay());
            }
        }

        // Nach der Antwort bis zum Kreuz: Grey Pause oder Flash, was länger ist.
        private float SecondsAfterAnswer =>
            Mathf.Max(pauseSeconds, answerFlashOpacity > 0f ? answerFlashSeconds : 0f);

        private IEnumerator BeginNextAttemptAfterDelay()
        {
            yield return new WaitForSecondsRealtime(SecondsAfterAnswer);
            BeginNextAttempt();
        }

        private bool TryStartBlockPause()
        {
            // Viertelblöcke: Nach 25, 50 und 75 % der geplanten gültigen Trials eine
            // Pause. Wiederholte ungültige Trials zählen nicht mit. Das rote Kreuz und
            // die graue Fläche bleiben stehen; Text steht darüber und darunter.
            int nextPause = pausesTaken + 1;
            if (blocksPerSession <= 1 || nextPause >= blocksPerSession) return false;
            int boundary = Mathf.RoundToInt(totalTrials * nextPause / (float)blocksPerSession);
            if (validTrialsCompleted < boundary) return false;

            pausesTaken = nextPause;
            sessionState = State.PausedBetweenMiniBlocks;
            stimulus.ShowFixationOnly();
            if (EnsureHeadsetText()) headsetText.ShowBlockPause(pausesTaken, blocksPerSession);
            WriteMarker("BlockPauseStarted;task=random_dot_instrument;block={0};of={1};valid_trials={2}",
                pausesTaken, blocksPerSession, validTrialsCompleted);
            Debug.Log($"Pause nach Block {pausesTaken} von {blocksPerSession} ({validTrialsCompleted} gültige " +
                $"Trials). Weiter mit {ResponseInputController.GetReadableKeyName(startSessionKey)}.", this);
            return true;
        }

        /// <summary>Versuchsleiter beendet die Viertelblock-Pause (F5).</summary>
        public void ContinueAfterBlockPause()
        {
            if (sessionState != State.PausedBetweenMiniBlocks) return;
            headsetText?.Hide();
            WriteMarker("BlockPauseEnded;task=random_dot_instrument;block={0}", pausesTaken);
            sessionState = State.InterTrial;
            BeginNextAttempt();
        }

        private void CompleteSession()
        {
            // Zum Schluss: Marker in die Aufnahme schreiben und die
            // Eye-Tracking-Aufzeichnung beenden.
            currentTrialNumber = totalTrials;
            WriteMarker("SessionCompleted;task=random_dot_instrument;valid_trials={0};presentations={1};" +
                "presentations_with_slow_frames={2}", validTrialsCompleted, presentationCount, trialsWithSlowFrames);
            if (trialsWithSlowFrames > 0)
                Debug.LogWarning($"{trialsWithSlowFrames} von {presentationCount} Darbietungen hatten ausgefallene " +
                    "Frames (Spalte slow_frames in der CSV). Die Bewegung kann dort geruckelt haben.", this);
            EndSession(State.Completed);
            ShowWelcomeText(completed: true);
            Debug.Log($"Random-Dot-Sitzung vollständig gespeichert: {validTrialsCompleted} gültige Trials " +
                $"aus {presentationCount} Präsentationen.\n" + activeSessionFolder, this);
        }

        private void FailAfterWriteError(Exception exception)
        {
            Debug.LogError("Random-Dot-Trial konnte nicht gespeichert werden; die Sitzung wird beendet: " +
                exception.Message, this);
            WriteMarker("SessionAborted;task=random_dot_instrument;reason=result_write_error");
            StopPendingCoroutines();
            headsetText?.Hide();
            EndSession(State.Aborted);
        }

        // Beendet die Sitzung: Punkte aus, Aufnahme stoppen, Zustand setzen.
        // Das brauchen Abbrechen, Fertigwerden und Schreibfehler gleichermaßen.
        private void EndSession(State endState)
        {
            ClearAnswerFlash();
            stoppedReason = null;
            if (stimulus != null)
            {
                stimulus.Hide();
                stimulus.SessionRunning = false;
            }

            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = endState;
        }

        // =====================================================================
        // Active: Kopftempo-Regel (Details in RandomDotActiveSweepRule)
        // =====================================================================

        private bool IsActiveTrial(RandomDotTrial trial) => trial.MotionMode == RandomDotMotionMode.HeadTracked;

        private void BeginWaitingForHeadMotion()
        {
            // Graue Fläche und Kreuz, noch keine Punkte. Die Messung beginnt neu.
            BeginActiveSweepWait();
            stimulus.ShowFixationOnly();
            sessionState = State.WaitingForHeadMotion;
            WriteMarker("WaitingForHeadMotion;task=random_dot_instrument;sequence={0};attempt={1};" +
                "simulated_speed_deg_s={2:F3};still_below_deg_s={3:F3};next_direction={4}",
                currentTrial.SequenceIndex, currentTrial.AttemptNumber, SimulatedHeadSpeed(currentTrial),
                minimumMovingHeadSpeed, activeSweep.NextDirection);
        }

        private void UpdateVelocityGate()
        {
            bool presenting = sessionState == State.PresentingMotion;
            SampleActiveSweep(presenting);
            if (sessionState == State.WaitingForHeadMotion && activeSweep.ReadyForDots)
            {
                activeSweep.BeginPresentation(stimulus.Observer.forward, stimulus.Observer.up);
                PresentCurrentTrial();
            }
            else if (presenting && activeSweep.HeadStopped)
            {
                // Kopf bleibt stehen oder dreht zurück: abbrechen, antworten, später wiederholen.
                StopCurrentPresentation(HeadStoppedReason);
            }
        }

        // Einstellungen aus dem Inspector übernehmen und die Messung neu beginnen.
        private void BeginActiveSweepWait()
        {
            activeSweep.Axis = velocityGateAxis;
            activeSweep.StillBelow = minimumMovingHeadSpeed;
            activeSweep.SmoothingSeconds = velocitySmoothingSeconds;
            activeSweep.AlternateDirection = alternateSweepDirection;
            activeSweep.BeginWait();
        }

        // Einmal pro Frame aufrufen.
        private void SampleActiveSweep(bool presenting)
        {
            activeSweep.Sample(stimulus.Observer.forward, stimulus.Observer.up, Time.unscaledDeltaTime, presenting);
        }

        // Live-Anzeige für den Experiment Monitor: derselbe Still-Schwellwert wie im
        // Trial, für das m und den Zoom, die gerade am Punktfeld stehen (in der
        // Vorschau also die Vorschauwerte, in der Sitzung die des laufenden Trials).
        // Das Kopftempo des simulierten Schwenks steht nur zum Vergleich daneben.
        private void UpdateLiveHeadSpeed()
        {
            Transform observer = stimulus != null ? stimulus.Observer : null;
            if (observer == null)
            {
                liveHeadSpeed.Reset();
                return;
            }
            float m = stimulus.InstrumentMagnificationM;
            float zoom = stimulus.ContentZoom;
            liveHeadSpeed.AddSample(observer.forward, Time.unscaledDeltaTime, velocityGateAxis,
                velocitySmoothingSeconds, RandomDotSimulatedSweep.ObjectSpeed(simulatedImageCenterSpeed, m, zoom),
                minimumMovingHeadSpeed, m * zoom);
        }

        // =====================================================================
        // Flash nach der Antwort
        // =====================================================================

        // Ohne Töne, nur kurz und schwach über die ganze graue Fläche. Nach jeder
        // Antwort, auch nach einem Abbruch. Gleich in Simulated, Active und in den Übungen.
        private void ShowAnswerFlash()
        {
            if (answerFlashOpacity <= 0f || stimulus == null) return;
            stimulus.SetFieldFlash(answerFlashColor, answerFlashOpacity);
            answerFlashUntil = Time.unscaledTime + answerFlashSeconds;
        }

        private void ClearAnswerFlash()
        {
            answerFlashUntil = 0f;
            if (stimulus != null) stimulus.ClearFieldFlash();
        }

        // =====================================================================
        // Geschwindigkeit, Schwenkweite und Punktwelt eines Trials
        // =====================================================================

        // Kopftempo, das dem simulierten Schwenk entspricht: So schnell dreht das
        // Instrument im Simulated-Block über die Außenwelt. Bei Active nur zum Vergleich.
        private float SimulatedHeadSpeed(RandomDotTrial trial) =>
            RandomDotSimulatedSweep.ObjectSpeed(simulatedImageCenterSpeed, trial.InstrumentMagnificationM,
                trial.ContentZoom);

        // Wie schnell im simulierten Schwenk über die Außenwelt geschwenkt wird, in
        // Grad Objektwinkel pro Sekunde: so, dass die Punkte in der Bildmitte bei jedem
        // m gleich schnell laufen, also bei großem m langsamer.
        private float SweepSpeed(RandomDotTrial trial) => SimulatedHeadSpeed(trial);

        // Wie weit das Feld im Trial zu jeder Seite der Mitte schwenkt, in Grad. Der
        // simulierte Schwenk läuft in der eingestellten Zeit mit fester Geschwindigkeit
        // einmal von der einen Seite zur anderen, also die halbe Strecke zu jeder Seite.
        private float SweepAmplitude(RandomDotTrial trial, float? simulatedSecondsOverride = null) =>
            0.5f * SweepSpeed(trial) * (simulatedSecondsOverride ?? simulatedSweepSeconds);

        private float WorldCoverageFor(RandomDotTrial trial, float? simulatedSecondsOverride = null)
        {
            // Wie groß die Punktwelt für diesen Trial sein muss. Im HeadTracked-Block
            // kann der Kopf weit drehen. Dafür gibt es dort den Sicherheitsbereich.
            float reach = IsActiveTrial(trial)
                ? headTurnSafetyDegrees
                : SweepAmplitude(trial, simulatedSecondsOverride);
            return RandomDotFieldStimulus.CoverageNeeded(trial.AngularDiameterDegrees, trial.ContentZoom,
                trial.InstrumentMagnificationM, trial.InstrumentDistortionK, reach,
                stimulus.ApertureEdgeSoftnessDegrees);
        }

        private void CheckPointWorlds(IReadOnlyList<RandomDotTrial> plan)
        {
            // Vor dem Start wird für alle Trials einmal nachgerechnet, ob die
            // Punktwelt überall unter die Grenzen passt. Lieber hier abbrechen als
            // mitten in der Messung.
            int fewestDots = int.MaxValue;
            int mostDots = 0;
            foreach (RandomDotTrial trial in plan)
            {
                // Der Stimulus würde zu kleine oder zu große Werte stillschweigend
                // abschneiden. Dann liefe der Schwenk anders als eingestellt.
                float speed = SweepSpeed(trial);
                float amplitude = SweepAmplitude(trial);
                if (speed < RandomDotFieldStimulus.MinimumSweepSpeed
                    || speed > RandomDotFieldStimulus.MaximumSweepSpeed
                    || amplitude < RandomDotFieldStimulus.MinimumSweepAmplitudeDegrees
                    || amplitude > RandomDotFieldStimulus.MaximumSweepAmplitudeDegrees)
                {
                    throw new ArgumentOutOfRangeException(nameof(simulatedImageCenterSpeed), string.Format(
                        CultureInfo.InvariantCulture,
                        "Bei m = {0} ergibt sich im {1}-Block ein Schwenk mit {2:F4} Grad pro Sekunde und " +
                        "{3:F4} Grad je Seite. Erlaubt sind {4} bis {5} Grad pro Sekunde und {6} bis {7} Grad.",
                        trial.InstrumentMagnificationM, trial.MotionMode, speed, amplitude,
                        RandomDotFieldStimulus.MinimumSweepSpeed, RandomDotFieldStimulus.MaximumSweepSpeed,
                        RandomDotFieldStimulus.MinimumSweepAmplitudeDegrees,
                        RandomDotFieldStimulus.MaximumSweepAmplitudeDegrees));
                }

                float coverage = WorldCoverageFor(trial);
                if (coverage > RandomDotFieldStimulus.MaximumCoverageDegrees)
                {
                    throw new ArgumentOutOfRangeException(nameof(instrumentMagnificationMValues),
                        "Die Kombination aus FOV, k, m, Content Zoom und Schwenkweite braucht mehr als " +
                        "170 Grad Punktwelt oder erreicht den Umkehrpunkt der Instrumentenabbildung.");
                }

                float m = trial.InstrumentMagnificationM;
                int dots = RandomDotFieldStimulus.DotCountFor(
                    stimulus.DotDensity, m, trial.ContentZoom, coverage);
                if (dots > RandomDotFieldStimulus.MaximumDotCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(instrumentMagnificationMValues),
                        $"Bei m = {m} braucht der {trial.MotionMode}-Block {dots} Punkte, erlaubt sind " +
                        $"{RandomDotFieldStimulus.MaximumDotCount}. Die Punktdichte am Random Dot Field " +
                        "senken oder Head Turn Safety verkleinern.");
                }

                fewestDots = Mathf.Min(fewestDots, dots);
                mostDots = Mathf.Max(mostDots, dots);
            }

            Debug.Log($"Punktwelt pro Trial: {fewestDots} bis {mostDots} Punkte, damit es in der " +
                $"Bildmitte immer {stimulus.DotDensity:F3} Punkte pro Quadratgrad sind.", this);
        }

        // =====================================================================
        // Eye Tracking, Marker und Hilfsfunktionen
        // =====================================================================

        private void StartEyeTracking(DateTime sessionStartUtc)
        {
            // Die Blickdaten selbst schreibt die Lab-Toolbox. Von hier wird die
            // Aufnahme nur an- und ausgeschaltet, und es werden Marker gesetzt.
            if (eyeTrackingToolbox == null)
            {
                Debug.LogWarning("Sitzung läuft ohne Eye-Tracking-Aufzeichnung.", this);
                return;
            }

            StopEyeTrackingRecording();
            eyeTrackingToolbox.SetOutputFolder(activeSessionFolder);
            eyeTrackingToolbox.StartRecording(experimentFiles.BaseFileName);
            WriteMarker(
                "SessionStart;task=random_dot_instrument;participant={0};session={1};seed={2};" +
                "planned_trials={3};utc={4};mapping={5};dot_density_per_deg2={6:F4};" +
                "simulated_image_center_speed_deg_s={7:F3};controller_mapping={8};convex_control={9};" +
                "concave_control={10};response_keys_swapped={11}",
                ExperimentFilesBase.SanitizeIdentifier(participantId, "pilot"),
                ExperimentFilesBase.SanitizeIdentifier(sessionLabel, "random_dot"),
                randomSeed, trialPlan.Count, sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                RandomDotExperimentFiles.MappingVersion, stimulus.DotDensity, simulatedImageCenterSpeed,
                keyboardController.VrControllerMapping,
                keyboardController.GetResponseControlName(CheckerboardCurvatureResponse.Convex),
                keyboardController.GetResponseControlName(CheckerboardCurvatureResponse.Concave),
                keyboardController.SwapResponseKeys ? 1 : 0);
        }

        private string BuildTrialStartMarker(RandomDotTrial trial)
        {
            // Ein Marker ist eine kurze Notiz mitten in der Eye-Tracking-Aufnahme.
            // Hier stehen alle wichtigen Werte des Durchgangs mit drin. Dadurch
            // weiß man beim Auswerten, welcher Blickwert zu welcher Bedingung gehört.
            bool active = IsActiveTrial(trial);
            float speed = active ? float.NaN : SweepSpeed(trial);
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;task=random_dot_instrument;presentation={0};sequence={1};" +
                "condition={2};repetition={3};attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "instrument_distortion_k={8:F4};instrument_magnification_m={9:F4};" +
                "content_zoom={10:F4};motion={11};direction={12};duration_s={13:F3};" +
                "amplitude_deg={14:F3};speed_deg_s={15:F3};dot_seed={16};axis={17};dot_count={18};" +
                "image_center_speed_deg_s={19:F3};head_motion_constraint={20}",
                presentationCount, trial.SequenceIndex, trial.ConditionIndex, trial.Repetition,
                trial.AttemptNumber, trial.EyePresentation, trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees, trial.InstrumentDistortionK,
                trial.InstrumentMagnificationM, trial.ContentZoom, trial.MotionMode,
                active ? activeSweep.Direction : trial.DirectionLabel, simulatedSweepSeconds,
                active ? float.NaN : SweepAmplitude(trial), speed,
                trial.DotSeed, trial.SweepAxis, stimulus.DotCount,
                RandomDotSimulatedSweep.ImageCenterSpeed(speed, trial.InstrumentMagnificationM, trial.ContentZoom),
                active ? "free" : "simulated");
        }

        private void ResolveReferences()
        {
            // Felder, die im Inspector leer geblieben sind, werden hier in der Szene
            // gesucht. Was schon eingetragen ist, wird nicht angefasst.
            stimulus = UnityTools.FindIfMissing(stimulus);
            if (stimulus != null && keyboardController == null)
            {
                keyboardController = stimulus.GetComponent<RandomDotKeyboardController>();
            }

            if (stimulus != null && sweepMonitor == null)
            {
                sweepMonitor = stimulus.GetComponent<RandomDotHeadSweepMonitor>();
            }

            if (eyeTrackingToolbox == null)
            {
                eyeTrackingToolbox = EyeTrackingToolbox.Instance;
            }

            eyeTrackingToolbox = UnityTools.FindIfMissing(eyeTrackingToolbox);
            fixationMonitor = UnityTools.FindIfMissing(fixationMonitor);
        }

        private void SubscribeEvents()
        {
            if (eventsSubscribed || keyboardController == null)
            {
                return;
            }

            keyboardController.ResponseSubmitted += HandleResponseSubmitted;
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

            eventsSubscribed = false;
        }

        // Blickkontrolle und Wegschau-Zähler fangen für den Durchgang bei null an.
        private void ResetFixationChecks()
        {
            if (fixationMonitor != null)
            {
                fixationMonitor.ResetFixationWindow();
            }

            lookAway.Reset();
        }

        // Übungen schreiben keine Marker: Es gibt keine Messdateien dazu.
        protected override bool MarkersEnabled => !standaloneTraining;

        private void StopPendingCoroutines()
        {
            StopAndClear(ref simulatedTrainingCoroutine);
            StopAndClear(ref activePracticeCoroutine);
            StopAndClear(ref motionCoroutine);
            StopAndClear(ref interTrialCoroutine);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            simulatedTrainingRepeatsPerValue = Mathf.Max(1, simulatedTrainingRepeatsPerValue);
            // Die Konkav-Grenze muss unter der Konvex-Grenze liegen.
            feedbackConcaveMaxK = Mathf.Min(feedbackConcaveMaxK, feedbackConvexMinK - 0.01f);
            simulatedSweepSeconds = Mathf.Clamp(simulatedSweepSeconds, 0.1f, 5f);
            simulatedImageCenterSpeed = Mathf.Clamp(simulatedImageCenterSpeed, 0.5f, 120f);
            headTurnSafetyDegrees = Mathf.Clamp(headTurnSafetyDegrees, 3f, 60f);
            pauseSeconds = Mathf.Max(0f, pauseSeconds);
        }
    }
}
