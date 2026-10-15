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
        HeadTrainingInstructions,
        HeadTrainingMotion,
        HeadTrainingFeedback,
        InterTrial,
        WaitingForFixation,
        PresentingMotion,
        WaitingForResponse,
        PausedBetweenMiniBlocks, // Pause zwischen Viertelblöcken; der Versuchsleiter setzt mit F5 fort.
        PausedBetweenMotionBlocks, // Alter Status; neue Sitzungen enthalten nur eine Bewegungsart.
        Completed,
        Aborted,
        ResponseInstructions,
        SimulatedTrainingInstructions,
        SimulatedTrainingMotion,
        SimulatedTrainingResponse,
        ActiveMotionInstructions,
        WaitingForHeadMotion, // activer trial: Kreuz sichtbar, wartet auf das richtige Kopftempo
        ActivePracticeInstructions, // Active Practice: Anleitung, Start mit F5
        ActivePracticeMotion, // Active Practice: warten, Punkte oder Rückmeldung
        ActivePracticeResponse // Active Practice: Antwort nach den Punkten
    }

    /// <summary>
    /// Steuert den ganzen Random-Dot-Versuch.
    ///
    /// So läuft ein Durchgang ab: Die Person schaut auf das Kreuz. Liegt der Blick
    /// ruhig genug, kommen die Punkte. Im SimulatedYaw-Block schwenken sie kurz in
    /// eine Richtung, im HeadTracked-Block dreht die Person den Kopf selbst.
    /// Freie Bewegung ist der Standard; die geführte Sinusbahn bleibt optional.
    /// Erst wenn die Bewegung vorbei ist, antwortet die Person konkav oder konvex,
    /// mit den Pfeiltasten oder mit Trigger und Trackpad-Klick am VR-Controller.
    /// Welche Taste was bedeutet, liest sie vor dem ersten Durchgang im Headset.
    ///
    /// Welche Instrumentenwerte k und m gezeigt werden, steht vorher fest. Schaut
    /// die Person zwischendurch zu lange weg, zählt der Durchgang nicht und kommt
    /// später noch einmal dran.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class RandomDotExperimentManager : MonoBehaviour, IPreviewTakeoverManager
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
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private RandomDotFixationMonitor fixationMonitor;

        [Header("Session")]
        [SerializeField]
        [Tooltip("Code for the participant, e.g. pilot_001. Do not enter real names.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "random_dot_instrument_pilot";

        [SerializeField]
        [Tooltip("Same number and same settings = exactly the same trial order again.")]
        private int randomSeed = 20260901;

        [SerializeField]
        private int dotSeedBase = 24680;

        [SerializeField]
        [Tooltip("Folder for the data files. Leave empty to save into the measurements folder of the " +
            "project.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Trial Plan")]
        [FormerlySerializedAs("angularDiametersDegrees")]
        [SerializeField]
        [Tooltip("Size of the round window in degrees. With several values, each size is tested.")]
        private List<float> fieldOfViewValues = new() { 90f };

        [SerializeField]
        [Tooltip("Which eyes see the image: both, only left or only right.")]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

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

        [FormerlySerializedAs("repetitionsPerCondition")]
        [SerializeField, Min(1)]
        [Tooltip("How often each combination is shown, if a k value has no own number below.")]
        private int repeatsPerCondition = 3;

        [SerializeField]
        [Tooltip("How often each k value is shown, in the same order as the k list. Missing entries use " +
            "Repeats Per Condition.")]
        private List<int> repeatsPerKValue = new();

        [SerializeField, Range(1, 10)]
        [Tooltip("Splits the session into parts with a break in between. 4 = breaks after 25, 50 and 75 % " +
            "of the trials (red cross, continue with F5). 1 = no breaks.")]
        private int blocksPerSession = 4;

        [Header("Simulated Movement")]
        [SerializeField]
        [Tooltip("Direction of the simulated movement: Horizontal = left/right, Vertical = up/down.")]
        private RandomDotSweepAxis simulatedSweepAxis = RandomDotSweepAxis.Horizontal;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("How long the dots are shown in a simulated trial (s). The dots move once in one " +
            "direction. Left and right come equally often in random order.")]
        private float simulatedSweepSeconds = 0.8f;

        [SerializeField]
        [Tooltip("Image Center: the dots in the middle move equally fast at every magnification. Object " +
            "Angle: the binoculars turn equally fast, so the dots move faster at higher " +
            "magnification.")]
        private RandomDotSweepSpeedReference simulatedSpeedReference = RandomDotSweepSpeedReference.ImageCenter;

        [SerializeField, Range(0.5f, 120f)]
        [Tooltip("Speed of the dots in the middle of the image, in degrees per second. Only used with " +
            "Image Center.")]
        private float simulatedImageCenterSpeed = 12f;

        [FormerlySerializedAs("sweepSpeedDegreesPerSecond")]
        [SerializeField, Range(0.1f, 60f)]
        [Tooltip("How fast the binoculars turn, in degrees per second. Simulated: only used with Object " +
            "Angle. Guided head movement: the average speed of the head.")]
        private float sweepSpeed = 1.2f;

        [Header("Head-Tracked Movement")]
        [FormerlySerializedAs("motionSeconds")]
        [FormerlySerializedAs("motionDurationSeconds")]
        [SerializeField, Min(0.1f)]
        [Tooltip("How long the dots are shown in a head-tracked trial (s).")]
        private float headTrackedSeconds = 5f;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Guided head movement: how far to turn the head to each side, in degrees.")]
        private float sweepAmplitudeDegrees = 2f;

        [SerializeField]
        [Tooltip("Before head-tracked trials, practice the head movement with a target marker.")]
        private bool trainHeadMovement = true;

        [SerializeField, Range(2, 10)]
        [Tooltip("How many good practice turns in a row are needed before the head-tracked trials start.")]
        private int requiredGoodTrainingSweeps = 4;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("How far the head may be off the guided path on average, in degrees.")]
        private float maximumProfileErrorDegrees = 0.9f;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Practice: how far the head may miss the two turning points, in degrees.")]
        private float maximumTrainingEndpointErrorDegrees = 0.9f;

        [FormerlySerializedAs("headTrackedCoverageYawDegrees")]
        [SerializeField, Range(3f, 60f)]
        [Tooltip("How far the head may turn from the start direction without reaching the end of the dot " +
            "field, in degrees. Larger values need many more dots.")]
        private float headTurnSafetyDegrees = 30f;

        [FormerlySerializedAs("validateHeadTrackedMotion")]
        [SerializeField]
        [Tooltip("Head movements that are too small, too fast or too big are saved and repeated later.")]
        private bool checkHeadMotion = true;

        [SerializeField]
        [Tooltip("On = the head may move in any direction at any speed. Off = the participant follows a " +
            "guided left-right path. The gaze check stays on.")]
        private bool freeHeadMovement = true;

        [SerializeField]
        [Tooltip("Dots stay until the participant answers. No time limit, direction or speed. Only with " +
            "Free Head Movement.")]
        private bool openEndedActiveTrials = true;

        [Header("Dots Only While The Head Turns Steadily")]
        [SerializeField]
        [Tooltip("Head-tracked trials: dots appear once the head turns steadily, at any speed, and stay " +
            "for the same time as in simulated trials. Replaces No Time Limit.")]
        private bool useVelocityGate = true;

        [SerializeField]
        [Tooltip("Which head turn counts. Horizontal = left or right, like the simulated movement.")]
        private HeadVelocityAxis velocityGateAxis = HeadVelocityAxis.Horizontal;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("How much the head speed may change and still count as steady. Before the dots: to start " +
            "them. While the dots are shown: compared with the speed just before they appeared. " +
            "0.25 = ±25 %.")]
        private float velocityChangeTolerance = 0.25f;

        [SerializeField]
        [Tooltip("One sweep per trial, alternating: the dots only appear while the head turns the other way " +
            "than in the last trial (right, answer, left, answer, ...). Gives a calm rhythm with a rest at each " +
            "answer and balances left and right.")]
        private bool alternateSweepDirection = true;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Slower than this the head counts as still, and no dots appear (degrees per second).")]
        private float minimumMovingHeadSpeed = 0.5f;

        [SerializeField, Range(0f, 20f)]
        [Tooltip("The dots appear only after the head has already turned this far in the current sweep " +
            "(degrees), in addition to all other conditions. Stopping or turning around starts a new sweep. " +
            "0 = off.")]
        private float minimumTurnBeforeDotsDegrees = 3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How long the head must turn steadily before the dots appear (s).")]
        private float velocityHoldSeconds = 0.3f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("After each trial the dots can appear again only after this time, even if the head " +
            "already turns steadily (s). Also after a stopped trial.")]
        private float minimumWaitBeforeDotsSeconds = 1f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("How long the head speed may differ from the speed before the dots while they are shown. " +
            "Longer, and the dots disappear and the trial is repeated later (s).")]
        private float velocityGraceSeconds = 0.1f;

        [SerializeField, Range(0.02f, 0.5f)]
        [Tooltip("Smooths the measured head speed so small shakes do not count (s).")]
        private float velocitySmoothingSeconds = 0.1f;

        [Header("Active Practice")]
        [SerializeField]
        [Tooltip("Before head-tracked trials (Dots Only While Head Turns Steadily), practise the head movement " +
            "with the real dots. Nothing is saved.")]
        private bool trainActivePanning = true;

        [SerializeField]
        [Tooltip("Before the practice, the computer moves the dots once at the simulated speed while the head " +
            "stays still, so the participant sees what a calm speed looks like.")]
        private bool showActiveSpeedExample = true;

        [SerializeField]
        [Tooltip("k values used in the practice. Clear values make it easier to focus on the head movement.")]
        private List<float> activePracticeKValues = new() { 0.2f, 1.8f };

        [SerializeField, Range(1, 20)]
        [Tooltip("The practice ends after this many good practice trials. Good = the dots did not stop early " +
            "and, with the speed hint on, the speed was in the natural range.")]
        private int activePracticeGoodTrials = 6;

        [SerializeField, Range(1, 60)]
        [Tooltip("The practice ends after this many tries, even if not enough were good.")]
        private int activePracticeMaxTrials = 20;

        [SerializeField]
        [Tooltip("After each practice trial, tell the participant if the head turn was quite fast or quite slow " +
            "(range below). Never in the real test.")]
        private bool activePracticeSpeedHint = true;

        [SerializeField, Range(0.5f, 10f)]
        [Tooltip("Practice hint: a head turn slower than this counts as quite slow (degrees per second).")]
        private float naturalHeadSpeedMin = 1.5f;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Practice hint: a head turn faster than this counts as quite fast (degrees per second).")]
        private float naturalHeadSpeedMax = 4f;

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

        [FormerlySerializedAs("headTrackedTurnaroundThresholdDegrees")]
        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("How far the head must turn to each side before it counts as a turn, in degrees.")]
        private float turnaroundDegrees = 1.5f;

        [FormerlySerializedAs("requiredHeadSweepAlternations")]
        [SerializeField, Range(1, 10)]
        [Tooltip("Minimum number of full left-right turns.")]
        private int requiredSweeps = 1;

        [FormerlySerializedAs("maximumValidHeadYawDegrees")]
        [SerializeField, Range(0f, 30f)]
        [Tooltip("Turning the head further than this makes the trial invalid, in degrees.")]
        private float maxHeadTurnDegrees = 4f;

        [FormerlySerializedAs("minimumMeanHeadSpeedDegreesPerSecond")]
        [SerializeField, Range(0f, 20f)]
        [Tooltip("Head moving slower than this on average makes the trial invalid (degrees per second).")]
        private float minMeanHeadSpeed = 0.5f;

        [FormerlySerializedAs("maximumPeakHeadSpeedDegreesPerSecond")]
        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Head moving faster than this at any moment makes the trial invalid (degrees per second).")]
        private float maxPeakHeadSpeed = 2.5f;

        [Header("Gaze Check")]
        [SerializeField]
        [Tooltip("The dots only appear when the participant looks steadily at the cross. Must be on for " +
            "real measurements.")]
        private bool requireFixation = true;

        [FormerlySerializedAs("maximumOffTargetSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("How long the participant may look away from the cross before the trial is repeated later " +
            "(s).")]
        private float maxLookAwaySeconds = 0.15f;

        [FormerlySerializedAs("maximumInvalidGazeSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("How long the eye tracker may give no gaze data, e.g. when blinking, before the trial is " +
            "repeated later (s).")]
        private float maxNoDataSeconds = 0.2f;

        [FormerlySerializedAs("maximumGazeSampleAgeSeconds")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Gaze data older than this counts as missing (s).")]
        private float maxSampleAgeSeconds = 0.1f;

        [FormerlySerializedAs("maximumAttemptsPerTrial")]
        [SerializeField, Min(0)]
        [Tooltip("How often one trial may be tried in total. 0 = until it works.")]
        private int maxRepeatsPerTrial;

        [Header("Timing")]
        [FormerlySerializedAs("interTrialSeconds")]
        [SerializeField, Min(0f)]
        private float pauseSeconds = 0.25f;

        [SerializeField, Range(0f, 0.3f)]
        [Tooltip("The dots fade in over this time instead of popping up (s). 0 = no fade. 0.05 s is about " +
            "4-5 frames at 90 Hz. Used in all trials and practice, simulated and head-tracked.")]
        private float dotsFadeInSeconds = 0.05f;

        [Header("Signals At The Cross")]
        [SerializeField]
        [Tooltip("The cross briefly flashes a colour: one when the participant can answer, another when a " +
            "trial stopped early. No sounds.")]
        private bool showCrossSignals = true;

        [SerializeField]
        [Tooltip("Colour that flashes briefly at the cross when the dots are over and the participant can " +
            "answer.")]
        private Color answerSignalColor = Color.cyan;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("How long the answer colour flashes (s).")]
        private float answerSignalSeconds = 0.3f;

        [SerializeField]
        [Tooltip("Colour that flashes briefly at the cross when the answer was taken. A different colour " +
            "than Answer Now, so a quick answer does not blend into the first flash.")]
        private Color answeredSignalColor = Color.green;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("How long the answer-taken colour flashes (s).")]
        private float answeredSignalSeconds = 0.3f;

        [SerializeField]
        [Tooltip("Colour of the cross for a moment when a trial stopped early (head speed changed or looked " +
            "away). No answer is needed then.")]
        private Color stoppedSignalColor = Color.magenta;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("How strong the signal colour is. Lower = more see-through over the grey background.")]
        private float crossSignalOpacity = 0.6f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("How long the cross shows the stopped colour (s).")]
        private float stoppedSignalSeconds = 0.5f;

        [Header("Keys")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        [Tooltip("Starts a new session with the other movement type.")]
        private Key startOtherMotionSessionKey = Key.F7;

        [SerializeField]
        private Key abortSessionKey = Key.F6;

        [SerializeField]
        private Key trainingKey = Key.T;

        [SerializeField]
        [Tooltip("Turns the preview on or off. Nothing is recorded.")]
        private Key previewKey = Key.P;

        [SerializeField]
        [Tooltip("Preview: press while turning your head at a speed that feels natural. Saves the head speed " +
            "of the last second. A controller button (trigger or trackpad click) works too.")]
        private Key saveHeadSpeedKey = Key.Space;

        [SerializeField]
        [Tooltip("Preview: shows or hides the head speed in the headset.")]
        private Key headSpeedReadoutKey = Key.H;

        [SerializeField]
        [Tooltip("Preview: shows the head speed and dot speed under the cross in the headset. Leave off to " +
            "find a natural speed without looking at numbers.")]
        private bool showHeadSpeedInHeadset;

        // Stand vor dem ersten "Vorschau-Werte übernehmen". Leer heißt: es wurde
        // nichts übernommen. "Originalwerte wiederherstellen" holt ihn zurück.
        [SerializeField, HideInInspector]
        private string valuesBeforePreviewTakeover = string.Empty;

        [Header("Current Status")]
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
        private int currentMotionBlock;

        [SerializeField]
        private int currentMiniBlock;

        [SerializeField]
        private string activeSessionFolder = string.Empty;

        private readonly LookAwayTimer lookAway = new();
        private IReadOnlyList<RandomDotTrial> trialPlan;
        private RandomDotTrialQueue trialQueue;
        private RandomDotTrial currentTrial;
        private RandomDotExperimentFiles experimentFiles;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private double stimulusEndUnitySeconds;
        private Coroutine interTrialCoroutine;
        private Coroutine motionCoroutine;
        private Coroutine headTrainingCoroutine;
        private RandomDotHeadSweepTrainingView headTrainingView;
        private int trainedHeadMotionBlock;
        private Vector3 trainingCenterForward;
        private bool eventsSubscribed;
        private bool previewActive;
        private string settingsBeforePreview;
        private string lookToKeepAfterPreview;
        private bool standaloneTraining;
        private bool trainingResponseReceived;
        private CheckerboardCurvatureResponse trainingResponse;
        private readonly FrameTimingTracker frameTiming = new();
        private readonly RandomDotHeadVelocityGate velocityGate = new();
        private readonly RandomDotSteadyHeadDetector steadyHead = new();
        private readonly RandomDotHeadOffAxisMeter offAxis = new();
        // Tatsächliche Schwenkrichtung beim Einblenden (Right/Left bzw. Up/Down).
        private string presentationDirection = string.Empty;
        // Vorzeichen des letzten Schwenks mit Punkten (+1 rechts/oben, -1 links/unten, 0 = noch keiner).
        private int lastSweepSign;
        // Laufender Schwenk vor den Punkten: Richtung und schon gedrehte Strecke (°).
        private int currentSweepSign;
        private float currentSweepTravelDegrees;
        private float presentationTurnBeforeDots = float.NaN;
        // Abbruchquote für den Experiment Monitor (laufende Sitzung).
        private int repeatedAttempts;
        private int speedChangedAttempts;
        private double waitingForHeadMotionSince;
        // Tempo des gleichmäßigen Abschnitts direkt vor den Punkten: Maßstab während der Punkte.
        private float presentationReferenceSpeed = float.NaN;
        private readonly RandomDotLiveHeadSpeed liveHeadSpeed = new();
        private RandomDotPilotSpeedView pilotSpeedView;
        private float pilotSavedMessageUntil;
        private float crossSignalUntil;
        private bool practiceContinueRequested;
        private int frameTimingStartFrame;
        private int trialsWithSlowFrames;
        private int pausesTaken;
        private int trainedSimulatedMotionBlock;
        private int trainedActivePracticeBlock;
        private int instructedActiveMotionBlock;
        private Coroutine simulatedTrainingCoroutine;
        private Coroutine activePracticeCoroutine;

        public RandomDotSessionState SessionState => sessionState;
        public RandomDotMotionMode SessionMotionMode => sessionMotionMode;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public int ValidTrialsCompleted => validTrialsCompleted;
        public int CurrentMotionBlock => currentMotionBlock;
        public int CurrentMiniBlock => currentMiniBlock;
        public bool RequireFixation => requireFixation;
        public bool FreeHeadMovement => freeHeadMovement;
        public bool IsPreviewActive => previewActive;
        public RandomDotFieldStimulus Stimulus => stimulus;
        public RandomDotLiveHeadSpeed LiveHeadSpeed => liveHeadSpeed;

        /// <summary>Wie oft in dieser Sitzung Punkte gezeigt wurden, Wiederholungen eingeschlossen.</summary>
        public int PresentationCount => presentationCount;

        /// <summary>Ungültige Durchgänge dieser Sitzung (werden hinten wiederholt).</summary>
        public int RepeatedAttempts => repeatedAttempts;

        /// <summary>Davon abgebrochen, weil sich das Kopftempo während der Punkte geändert hat.</summary>
        public int SpeedChangedAttempts => speedChangedAttempts;
        UnityEngine.Object IPreviewTakeoverManager.PreviewStimulus => stimulus;

        // Für die Editor-Übergabe aus dem Play Mode in die Szene.
        public string ValuesBeforePreviewTakeover
        {
            get => valuesBeforePreviewTakeover;
            set => valuesBeforePreviewTakeover = value ?? string.Empty;
        }

        public bool HasValuesBeforePreviewTakeover => !string.IsNullOrEmpty(valuesBeforePreviewTakeover);
        public bool IsTrainingActive => sessionState is State.HeadTrainingInstructions
            or State.HeadTrainingMotion or State.HeadTrainingFeedback
            or State.SimulatedTrainingInstructions or State.SimulatedTrainingMotion
            or State.SimulatedTrainingResponse
            or State.ActivePracticeInstructions or State.ActivePracticeMotion or State.ActivePracticeResponse;
        public string ControllerSummary => keyboardController != null ? keyboardController.ControllerSummary : "–";
        public string KeyboardSummary => keyboardController != null ? keyboardController.KeyboardSummary : "–";

        // Aktiv ist alles außer: noch nicht gestartet, fertig oder abgebrochen.
        public bool IsSessionActive => !standaloneTraining
            && sessionState is not (State.Idle or State.Completed or State.Aborted);

        // Der Haupttrial kann frei/kurz sein. Das unveränderte Sinustraining
        // braucht unabhängig davon genug Zeit für beide Wendepunkte.
        private float HeadTrainingSeconds =>
            Mathf.Max(headTrackedSeconds, 3f * sweepAmplitudeDegrees / sweepSpeed);

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
            if (crossSignalUntil > 0f && Time.unscaledTime >= crossSignalUntil) ClearCrossSignal();

            // Bildzeiten nur während der Darbietung zählen, ab dem Frame nach dem Start.
            if (sessionState == State.PresentingMotion && Time.frameCount > frameTimingStartFrame)
                frameTiming.Add(Time.unscaledDeltaTime);

            Keyboard keyboard = Keyboard.current;
            if (previewActive) UpdateHeadSpeedPilot(keyboard);
            else pilotSpeedView?.Hide();

            if (keyboard != null && !IsSessionActive && !IsTrainingActive
                && keyboard[previewKey].wasPressedThisFrame)
            {
                TogglePreview();
                return;
            }

            if (keyboard != null && keyboard[abortSessionKey].wasPressedThisFrame)
            {
                if (previewActive) StopPreview();
                else if (standaloneTraining) StopTrainingAndReturnToWelcome();
                else if (IsSessionActive) AbortSession("ManualAbort");
                return;
            }

            if (keyboard != null && !IsSessionActive && !IsTrainingActive
                && keyboard[trainingKey].wasPressedThisFrame)
            {
                StartTraining();
                return;
            }

            if (keyboard != null && !IsSessionActive && !IsTrainingActive
                && keyboard[startOtherMotionSessionKey].wasPressedThisFrame)
            {
                StartOtherMotionSession();
                return;
            }

            if (keyboard != null && keyboard[startSessionKey].wasPressedThisFrame)
            {
                if (sessionState == State.ResponseInstructions)
                {
                    ConfirmResponseInstructions();
                }
                else if (sessionState == State.PausedBetweenMiniBlocks)
                {
                    ContinueAfterBlockPause();
                }
                else if (sessionState is State.HeadTrainingInstructions or State.SimulatedTrainingInstructions
                    or State.ActivePracticeInstructions)
                {
                    ConfirmTrainingInstructions();
                }
                else if (sessionState == State.ActiveMotionInstructions)
                {
                    ConfirmActiveMotionInstructions();
                }
                else if (!IsSessionActive && !IsTrainingActive)
                {
                    StartSession();
                }

                return;
            }

            if (currentTrial != null && IsVelocityGatedTrial(currentTrial)
                && (sessionState is State.WaitingForHeadMotion or State.PresentingMotion))
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

        private void OnDisable()
        {
            RestorePreviewSettings();
            if (standaloneTraining) StopTrainingAndReturnToWelcome();
            UnsubscribeEvents();
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }

            DisposeTrainingView();
        }

        public void ShowWelcomeScreen()
        {
            RestorePreviewSettings();
            standaloneTraining = false;
            sessionState = State.Idle;
            ResolveReferences();
            if (stimulus == null || stimulus.Observer == null) return;
            headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
            stimulus.SessionRunning = false;
            stimulus.ShowFixationOnly();
            ClearCrossSignal();
            headTrainingView.ShowWelcome(
                ResponseInputController.GetReadableKeyName(startSessionKey),
                ResponseInputController.GetReadableKeyName(startOtherMotionSessionKey),
                ResponseInputController.GetReadableKeyName(trainingKey),
                ResponseInputController.GetReadableKeyName(previewKey), sessionMotionMode);
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
            headTrainingView?.Hide();
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
            if (sessionMotionMode == RandomDotMotionMode.HeadTracked && UsesActivePractice)
                ShowActivePracticeInstructions();
            else if (sessionMotionMode == RandomDotMotionMode.HeadTracked)
                ShowHeadTrainingInstructions(null);
            else
                ShowSimulatedTrainingInstructions();
            return IsTrainingActive;
        }

        public void StopTrainingAndReturnToWelcome()
        {
            if (!standaloneTraining) return;
            StopPendingCoroutines();
            sweepMonitor.StopProfileTracking();
            headTrainingView?.Hide();
            ShowWelcomeScreen();
        }

        public void ConfirmTrainingInstructions()
        {
            if (sessionState == State.HeadTrainingInstructions)
                headTrainingCoroutine = StartCoroutine(RunHeadMovementTraining());
            else if (sessionState == State.SimulatedTrainingInstructions)
                simulatedTrainingCoroutine = StartCoroutine(RunSimulatedTraining());
            else if (sessionState == State.ActivePracticeInstructions)
                activePracticeCoroutine = StartCoroutine(RunActivePractice());
        }

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

            // Im HeadTracked-Block kommt der zweite Umkehrpunkt erst nach
            // 3 * Amplitude / Geschwindigkeit.
            if (sessionMotionMode == RandomDotMotionMode.HeadTracked
                && !freeHeadMovement && checkHeadMotion
                && headTrackedSeconds + 0.001f < 3f * sweepAmplitudeDegrees / sweepSpeed)
            {
                Debug.LogError(
                    "Die HeadTracked-Dauer muss lang genug sein, damit der Sinus " +
                    "beide Umkehrpunkte erreicht " +
                    "(mindestens 3 * Amplitude / mittlere Geschwindigkeit).", this);
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
            headTrainingView?.Hide();
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            validTrialsCompleted = 0;
            presentationCount = 0;
            repeatedAttempts = 0;
            speedChangedAttempts = 0;
            trialsWithSlowFrames = 0;
            pausesTaken = 0;
            currentMotionBlock = 0;
            currentMiniBlock = 0;
            trainedHeadMotionBlock = 0;
            trainedSimulatedMotionBlock = 0;
            trainedActivePracticeBlock = 0;
            instructedActiveMotionBlock = 0;
            lastSweepSign = 0;
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
            Transform observer = stimulus.Observer;
            if (autoStartOnPlay || observer == null)
            {
                BeginTrainingOrNextAttempt();
                return;
            }

            headTrainingView ??= new RandomDotHeadSweepTrainingView(observer);
            stimulus.PlaceAroundObserver();
            stimulus.ShowFixationOnly();
            sessionState = State.ResponseInstructions;
            headTrainingView.ShowResponseInstructions(
                ResponseLinesWithSignalNote(), ResponseInputController.GetReadableKeyName(startSessionKey),
                OpenEndedSession);
            WriteMarker("ResponseInstructionsShown;task=random_dot_instrument");
        }

        /// <summary>
        /// Beendet die Anzeige der Tastenbelegung. Danach beginnt der erste
        /// Durchgang oder, im HeadTracked-Block, das Kopfbewegungstraining.
        /// </summary>
        public void ConfirmResponseInstructions()
        {
            if (sessionState != State.ResponseInstructions)
            {
                return;
            }

            headTrainingView.Hide();
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

        // Für die Anleitungen: Tastenbelegung plus ein Satz zu den Farbsignalen am Kreuz.
        private string ResponseLinesWithSignalNote()
        {
            return BuildResponseLines() + (showCrossSignals
                ? "\n\nThe cross flashes one colour when you can answer,\n" +
                    "and another colour if the trial stopped early."
                : string.Empty);
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
            headTrainingView?.Hide();
            sweepMonitor.StopProfileTracking();
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

        private void BeginTrainingOrNextAttempt()
        {
            // Jeder Bewegungsblock bekommt sein eigenes Training. Die kurze
            // simulierte Darbietung hat keine Kopfbewegungs-Sollbahn.
            if (trialQueue != null && trialQueue.TryPeekNext(out RandomDotTrial next))
            {
                if (trainHeadMovement && next.MotionMode == RandomDotMotionMode.HeadTracked
                    && trainedHeadMotionBlock != next.MotionBlockIndex)
                {
                    ShowHeadTrainingInstructions(next);
                    return;
                }

                if (trainSimulatedMotion && !autoStartOnPlay
                    && next.MotionMode == RandomDotMotionMode.SimulatedYaw
                    && trainedSimulatedMotionBlock != next.MotionBlockIndex)
                {
                    ShowSimulatedTrainingInstructions();
                    return;
                }

                if (UsesActivePractice && !autoStartOnPlay && IsVelocityGatedTrial(next)
                    && trainedActivePracticeBlock != next.MotionBlockIndex && stimulus.Observer != null)
                {
                    ShowActivePracticeInstructions();
                    return;
                }

                if (!autoStartOnPlay && next.MotionMode == RandomDotMotionMode.HeadTracked
                    && instructedActiveMotionBlock != next.MotionBlockIndex && stimulus.Observer != null)
                {
                    headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
                    stimulus.ShowFixationOnly();
                    sessionState = State.ActiveMotionInstructions;
                    headTrainingView.ShowActiveMotionInstructions(freeHeadMovement,
                        IsOpenEndedTrial(next), BuildResponseLines(), ResponseInputController.GetReadableKeyName(startSessionKey),
                        IsVelocityGatedTrial(next), velocityGateAxis, alternateSweepDirection);
                    return;
                }
            }

            BeginNextAttempt();
        }

        public void ConfirmActiveMotionInstructions()
        {
            if (sessionState != State.ActiveMotionInstructions) return;
            if (trialQueue.TryPeekNext(out RandomDotTrial next))
                instructedActiveMotionBlock = next.MotionBlockIndex;
            headTrainingView.Hide();
            sessionState = State.InterTrial;
            BeginNextAttempt();
        }

        /// <summary>Wie viele beschriftete Beispiele im laufenden Training schon gezeigt wurden.</summary>
        public int TrainingExamplesShown { get; private set; }

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
                RandomDotTrial example = RandomDotTrialPlanner.CreateRandomizedPlan(
                    new[] { fieldOfViewValues[0] }, new[] { eyePresentations[0] }, new[] { examples[index].k },
                    new[] { instrumentMagnificationMValues[0] }, new[] { contentZoomValues[0] },
                    new[] { RandomDotMotionMode.SimulatedYaw }, 1, 1,
                    unchecked(randomSeed + 50000 + index), dotSeedBase + 50000 + index, simulatedSweepAxis)[0];
                string label = examples[index].category == CheckerboardCurvatureResponse.Convex
                    ? "CONVEX (curves outward)" : "CONCAVE (curves inward)";

                headTrainingView.ShowSimulatedExample(label,
                    "Watch the dots. This example is shown longer.");
                yield return PracticeTextPause(exampleTextSeconds);
                yield return PresentSimulatedExample(example, exampleLongSeconds);

                headTrainingView.ShowSimulatedExample(label, string.Format(CultureInfo.InvariantCulture,
                    "The same example again,\nas briefly as in the experiment ({0:0.0#} s).",
                    simulatedSweepSeconds));
                yield return PracticeTextPause(exampleTextSeconds);
                yield return PresentSimulatedExample(example, simulatedSweepSeconds);
                TrainingExamplesShown++;
            }

            headTrainingView.ShowSimulatedPracticeStart(simulatedTrainingFeedback);
            yield return PracticeTextPause(exampleTextSeconds);
        }

        private IEnumerator PresentSimulatedExample(RandomDotTrial example, float seconds)
        {
            headTrainingView.Hide();
            ConfigureStimulus(example, seconds);
            stimulus.ShowFixationOnly();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, pauseSeconds));
            stimulus.RestartMotionPhase();
            stimulus.Show(dotsFadeInSeconds);
            yield return new WaitForSecondsRealtime(seconds);
            stimulus.ShowFixationOnly();
        }

        private IReadOnlyList<RandomDotTrial> BuildSimulatedTrainingPlan()
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { fieldOfViewValues[0] }, new[] { eyePresentations[0] }, simulatedTrainingKValues,
                new[] { instrumentMagnificationMValues[0] }, new[] { contentZoomValues[0] },
                new[] { RandomDotMotionMode.SimulatedYaw }, simulatedTrainingRepeatsPerValue,
                simulatedTrainingRepeatsPerValue, randomSeed, dotSeedBase, simulatedSweepAxis);
        }

        private void ShowSimulatedTrainingInstructions()
        {
            try
            {
                CheckPointWorlds(BuildSimulatedTrainingPlan());
                if (stimulus.Observer == null) throw new InvalidOperationException("Training braucht einen Observer.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Simulated-Panning-Training: " + exception.Message, this);
                if (standaloneTraining) ShowWelcomeScreen();
                else AbortSession("InvalidTrainingSettings");
                return;
            }

            headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
            stimulus.ShowFixationOnly();
            sessionState = State.SimulatedTrainingInstructions;
            headTrainingView.ShowSimulatedInstructions(ResponseLinesWithSignalNote(),
                ResponseInputController.GetReadableKeyName(startSessionKey), simulatedTrainingFeedback,
                showSimulatedExamples);
        }

        private IEnumerator RunSimulatedTraining()
        {
            sessionState = State.SimulatedTrainingMotion;
            if (showSimulatedExamples)
            {
                yield return ShowSimulatedExamples();
            }

            IReadOnlyList<RandomDotTrial> practice = BuildSimulatedTrainingPlan();
            for (int index = 0; index < practice.Count; index++)
            {
                headTrainingView.Hide();
                ConfigureStimulus(practice[index]);
                stimulus.ShowFixationOnly();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, pauseSeconds));
                stimulus.RestartMotionPhase();
                trainingResponseReceived = false;
                sessionState = State.SimulatedTrainingMotion;
                stimulus.Show(dotsFadeInSeconds);
                yield return new WaitForSecondsRealtime(simulatedSweepSeconds);
                stimulus.ShowFixationOnly();
                sessionState = State.SimulatedTrainingResponse;
                headTrainingView.ShowSimulatedResponse(index + 1, practice.Count, BuildResponseLines());
                ShowAnswerSignal();
                while (!trainingResponseReceived) yield return null;
                ShowAnsweredSignal();

                // Subjektives Formurteil: eine "richtige" Antwort gibt es nur bei
                // eindeutigen Extremwerten. Dort sagt die Rückmeldung, was konvex
                // und konkav heißt, ohne eine Grenze in der Mitte vorzugeben.
                CheckerboardCurvatureResponse expected = simulatedTrainingFeedback
                    ? ExpectedTrainingResponse(practice[index].InstrumentDistortionK,
                        feedbackConcaveMaxK, feedbackConvexMinK)
                    : CheckerboardCurvatureResponse.None;
                if (expected != CheckerboardCurvatureResponse.None)
                {
                    LastTrainingAnswerCorrect = trainingResponse == expected;
                    headTrainingView.ShowSimulatedFeedback(LastTrainingAnswerCorrect.Value,
                        expected == CheckerboardCurvatureResponse.Convex
                            ? "CONVEX (curves outward)" : "CONCAVE (curves inward)");
                    yield return PracticeTextPause(feedbackSeconds);
                }
                else
                {
                    LastTrainingAnswerCorrect = null;
                }
            }

            if (!standaloneTraining && trialQueue.TryPeekNext(out RandomDotTrial next))
                trainedSimulatedMotionBlock = next.MotionBlockIndex;
            headTrainingView.Hide();
            simulatedTrainingCoroutine = null;
            if (standaloneTraining) ShowWelcomeScreen();
            else
            {
                sessionState = State.InterTrial;
                BeginNextAttempt();
            }
        }

        // Farbsignale am Kreuz, ohne Töne, jeweils nur kurz: "jetzt antworten" (Answer
        // Signal Seconds) und "abgebrochen" (Stopped Signal Seconds). Gleich in Simulated,
        // Active und in den Übungen.
        private void ShowAnswerSignal()
        {
            FlashCrossSignal(answerSignalColor, answerSignalSeconds);
        }

        private void ShowAnsweredSignal()
        {
            FlashCrossSignal(answeredSignalColor, answeredSignalSeconds);
        }

        private void ShowStoppedSignal()
        {
            FlashCrossSignal(stoppedSignalColor, stoppedSignalSeconds);
        }

        private void FlashCrossSignal(Color color, float seconds)
        {
            if (!showCrossSignals || stimulus == null) return;
            stimulus.SetFixationSignal(color, crossSignalOpacity);
            crossSignalUntil = Time.unscaledTime + seconds;
        }

        private void ClearCrossSignal()
        {
            crossSignalUntil = 0f;
            if (stimulus != null) stimulus.ClearFixationSignal();
        }

        /// <summary>
        /// Weiter vom aktuellen Übungstext (wie Leertaste oder Trackpad). Für Tests und
        /// falls der Versuchsleiter es über den Inspector auslösen möchte.
        /// </summary>
        public void ContinuePractice() => practiceContinueRequested = true;

        /// <summary>Wartet gerade ein Übungstext auf einen Tastendruck?</summary>
        public bool IsWaitingForPracticeContinue { get; private set; }

        // Übungstexte: bleiben stehen, bis Trackpad (Person) oder Leertaste (Versuchsleiter)
        // gedrückt wird. Mit "Practice Texts Wait For Press" aus nach der alten festen Zeit.
        private IEnumerator PracticeTextPause(float seconds)
        {
            if (!practiceTextsWaitForPress)
            {
                yield return new WaitForSecondsRealtime(seconds);
                yield break;
            }

            headTrainingView?.AppendFooter("PRESS TRACKPAD TO CONTINUE");
            practiceContinueRequested = false;
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

        // Active Practice gibt es nur für aktive Trials mit Geschwindigkeitserkennung.
        private bool UsesActivePractice => trainActivePanning && freeHeadMovement && useVelocityGate;

        /// <summary>Gute Übungstrials im laufenden oder letzten Active Practice.</summary>
        public int ActivePracticeGoodTrials { get; private set; }

        /// <summary>Alle Übungstrials im laufenden oder letzten Active Practice.</summary>
        public int ActivePracticeAttempts { get; private set; }

        /// <summary>Ergebnis des letzten Übungstrials: good, fast, slow oder changed.</summary>
        public string LastActivePracticeResult { get; private set; } = string.Empty;

        private IReadOnlyList<RandomDotTrial> BuildActivePracticePlan()
        {
            // Genug Übungstrials für die Höchstzahl, k abwechselnd aus der Liste.
            int repeats = Mathf.Max(1, Mathf.CeilToInt(activePracticeMaxTrials /
                (float)Mathf.Max(1, activePracticeKValues.Count)));
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { fieldOfViewValues[0] }, new[] { eyePresentations[0] }, activePracticeKValues,
                new[] { instrumentMagnificationMValues[0] }, new[] { contentZoomValues[0] },
                new[] { RandomDotMotionMode.HeadTracked }, repeats, repeats,
                unchecked(randomSeed + 70000), dotSeedBase + 70000, simulatedSweepAxis);
        }

        private RandomDotTrial BuildActiveSpeedExample()
        {
            // Simulierter Schwenk mit dem Tempo des Simulated-Blocks; k = 1 (gerade),
            // damit das Beispiel nur das Tempo zeigt und keine Form vorgibt.
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { fieldOfViewValues[0] }, new[] { eyePresentations[0] }, new[] { 1f },
                new[] { instrumentMagnificationMValues[0] }, new[] { contentZoomValues[0] },
                new[] { RandomDotMotionMode.SimulatedYaw }, 1, 1,
                unchecked(randomSeed + 71000), dotSeedBase + 71000, simulatedSweepAxis)[0];
        }

        private void ShowActivePracticeInstructions()
        {
            try
            {
                CheckPointWorlds(BuildActivePracticePlan());
                if (stimulus.Observer == null) throw new InvalidOperationException("Training braucht einen Observer.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Active Practice: " + exception.Message, this);
                if (standaloneTraining) ShowWelcomeScreen();
                else AbortSession("InvalidTrainingSettings");
                return;
            }

            headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
            stimulus.ShowFixationOnly();
            sessionState = State.ActivePracticeInstructions;
            headTrainingView.ShowActivePracticeInstructions(ResponseLinesWithSignalNote(),
                ResponseInputController.GetReadableKeyName(startSessionKey), velocityGateAxis, showActiveSpeedExample,
                alternateSweepDirection);
            WriteMarker("ActivePracticeInstructions;task=random_dot_instrument");
        }

        private IEnumerator RunActivePractice()
        {
            // Übt genau den Ablauf der aktiven Trials (gleichmäßig schwenken, Punkte,
            // Antwort), ohne Daten zu speichern. Danach eine kurze Rückmeldung zur
            // Bewegung, damit die Person ein natürliches Tempo findet.
            sessionState = State.ActivePracticeMotion;
            ActivePracticeGoodTrials = 0;
            lastSweepSign = 0;
            ActivePracticeAttempts = 0;
            LastActivePracticeResult = string.Empty;

            if (showActiveSpeedExample)
            {
                headTrainingView.ShowActiveSpeedExample();
                yield return PracticeTextPause(exampleTextSeconds);
                yield return PresentSimulatedExample(BuildActiveSpeedExample(), exampleLongSeconds);
            }

            IReadOnlyList<RandomDotTrial> practice = BuildActivePracticePlan();
            while (ActivePracticeGoodTrials < activePracticeGoodTrials && ActivePracticeAttempts < activePracticeMaxTrials)
            {
                RandomDotTrial trial = practice[ActivePracticeAttempts % practice.Count];
                ActivePracticeAttempts++;
                ConfigureStimulus(trial);
                stimulus.ShowFixationOnly();
                sessionState = State.ActivePracticeMotion;
                headTrainingView.ShowActivePracticeTurn(ActivePracticeAttempts, ActivePracticeGoodTrials,
                    activePracticeGoodTrials, velocityGateAxis, RequiredSweepSign);

                // Warten, bis der Kopf gleichmäßig dreht (wie im echten Trial).
                BeginHeadSpeedWait();
                while (true)
                {
                    SampleHeadSpeed(presenting: false);
                    if (HeadReadyForDots) break;
                    yield return null;
                }

                BeginHeadSpeedPresentation();
                headTrainingView.Hide();
                stimulus.PlaceAroundObserver();
                stimulus.RestartMotionPhase();
                stimulus.Show(dotsFadeInSeconds);
                bool stopped = false;
                double shownAt = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble - shownAt < simulatedSweepSeconds)
                {
                    yield return null;
                    SampleHeadSpeed(presenting: true);
                    if (HeadSpeedChangedTooLong)
                    {
                        ShowStoppedSignal();
                        stopped = true;
                        break;
                    }
                }
                stimulus.ShowFixationOnly();

                if (!stopped)
                {
                    trainingResponseReceived = false;
                    sessionState = State.ActivePracticeResponse;
                    headTrainingView.ShowActivePracticeResponse(BuildResponseLines());
                    ShowAnswerSignal();
                    while (!trainingResponseReceived) yield return null;
                    ShowAnsweredSignal();
                }

                float speed = presentationReferenceSpeed;
                LastActivePracticeResult = stopped ? "changed"
                    : !activePracticeSpeedHint ? "good"
                    : speed > naturalHeadSpeedMax ? "fast"
                    : speed < naturalHeadSpeedMin ? "slow"
                    : "good";
                if (LastActivePracticeResult == "good") ActivePracticeGoodTrials++;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "Active Practice {0}: {1}, Kopftempo vor den Punkten {2:F2} °/s ({3}/{4} gut).",
                    ActivePracticeAttempts, LastActivePracticeResult, speed, ActivePracticeGoodTrials,
                    activePracticeGoodTrials), this);
                WriteMarker("ActivePracticeTrial;task=random_dot_instrument;attempt={0};result={1};" +
                    "reference_speed_deg_s={2:F3};good={3}", ActivePracticeAttempts, LastActivePracticeResult,
                    speed, ActivePracticeGoodTrials);

                sessionState = State.ActivePracticeMotion;
                headTrainingView.ShowActivePracticeFeedback(ActivePracticeFeedbackText(LastActivePracticeResult),
                    ActivePracticeGoodTrials, activePracticeGoodTrials);
                yield return PracticeTextPause(Mathf.Max(1.5f, feedbackSeconds));
            }

            if (!standaloneTraining && trialQueue.TryPeekNext(out RandomDotTrial next))
                trainedActivePracticeBlock = next.MotionBlockIndex;
            WriteMarker("ActivePracticeCompleted;task=random_dot_instrument;attempts={0};good={1}",
                ActivePracticeAttempts, ActivePracticeGoodTrials);
            headTrainingView.Hide();
            activePracticeCoroutine = null;
            if (standaloneTraining) ShowWelcomeScreen();
            else
            {
                // Danach kommt die normale Anleitung für die aktiven Trials (F5 startet).
                sessionState = State.InterTrial;
                BeginTrainingOrNextAttempt();
            }
        }

        private static string ActivePracticeFeedbackText(string result) => result switch
        {
            "changed" => "THE DOTS STOPPED EARLY\n\nYou changed your speed while they were visible.\n" +
                "Keep the same speed until they disappear.",
            "fast" => "QUITE FAST\n\nThe dots moved very fast.\nTry a calmer head turn.",
            "slow" => "QUITE SLOW\n\nTry a slightly quicker, natural head turn.",
            _ => "GOOD\n\nKeep turning like this."
        };

        private void ShowHeadTrainingInstructions(RandomDotTrial next)
        {
            Transform observer = stimulus.Observer;
            if (observer == null)
            {
                Debug.LogError("Das Kopfbewegungstraining braucht die XR-Kamera als Observer.", this);
                AbortSession("MissingTrainingObserver");
                return;
            }

            headTrainingView ??= new RandomDotHeadSweepTrainingView(observer);
            stimulus.SetMotionMode(RandomDotMotionMode.HeadTracked);
            stimulus.PlaceAroundObserver();
            stimulus.ShowFixationOnly();
            trainingCenterForward = RandomDotHeadSweepMonitor.RemoveUpDownTilt(observer.forward);
            sessionState = State.HeadTrainingInstructions;
            headTrainingView.ShowInstructions(sweepAmplitudeDegrees, BuildResponseLines(),
                ResponseInputController.GetReadableKeyName(startSessionKey),
                ResponseInputController.GetReadableKeyName(abortSessionKey));
            WriteMarker(
                "HeadTrainingInstructions;task=random_dot_instrument;motion_block={0};" +
                "amplitude_deg={1:F3};mean_speed_deg_s={2:F3}",
                next?.MotionBlockIndex ?? 0, sweepAmplitudeDegrees, sweepSpeed);
        }

        private IEnumerator RunHeadMovementTraining()
        {
            int goodSweeps = 0;
            int attempt = 0;
            RandomDotSweepDirection firstDirection = trialQueue != null
                && trialQueue.TryPeekNext(out RandomDotTrial next)
                ? next.SweepDirection
                : RandomDotSweepDirection.RightFirst;
            Transform observer = stimulus.Observer;
            float firstTurnSeconds = sweepAmplitudeDegrees / sweepSpeed;
            float oppositeTurnSeconds = 3f * firstTurnSeconds;

            while (goodSweeps < requiredGoodTrainingSweeps)
            {
                // Zwischen den Übungsdurchgängen zur ursprünglichen Mitte
                // zurückkehren. Sonst verschiebt sich der Nullpunkt schrittweise.
                sessionState = State.HeadTrainingFeedback;
                yield return WaitUntilHeadCentered(observer);
                yield return new WaitForSecondsRealtime(0.35f);
                if (IsHeadOffCenter(observer))
                {
                    continue;
                }

                // Abwechselnd geht es zuerst in die eine und dann in die andere Richtung.
                RandomDotSweepDirection direction =
                    attempt % 2 == 0 ? firstDirection : Opposite(firstDirection);
                attempt++;
                sweepMonitor.ConfigureCriterion(turnaroundDegrees, requiredSweeps);
                sweepMonitor.StartProfileTracking(sweepAmplitudeDegrees, sweepSpeed, direction);
                sessionState = State.HeadTrainingMotion;
                headTrainingView.ShowPractice(goodSweeps, requiredGoodTrainingSweeps, direction);

                // An jeder Wende kommt ein Ton. firstSide ist die Seite der ersten Wende
                // (+ rechts, - links), die zweite Wende liegt auf der anderen Seite.
                float firstSide = direction == RandomDotSweepDirection.RightFirst
                    ? sweepAmplitudeDegrees
                    : -sweepAmplitudeDegrees;
                double startSeconds = Time.realtimeSinceStartupAsDouble;
                bool firstCuePlayed = false;
                bool oppositeCuePlayed = false;
                while (Time.realtimeSinceStartupAsDouble - startSeconds < HeadTrainingSeconds)
                {
                    double elapsed = Time.realtimeSinceStartupAsDouble - startSeconds;
                    float targetYaw = RandomDotSimulatedSweep.EvaluateBackAndForthDegrees(
                        elapsed, sweepAmplitudeDegrees, sweepSpeed, direction);
                    headTrainingView.UpdateMarkers(
                        targetYaw, sweepMonitor.CurrentYawDegrees, sweepAmplitudeDegrees);

                    if (!firstCuePlayed && elapsed >= firstTurnSeconds)
                    {
                        headTrainingView.PlayTurnCue(firstSide);
                        firstCuePlayed = true;
                    }

                    if (!oppositeCuePlayed && elapsed >= oppositeTurnSeconds)
                    {
                        headTrainingView.PlayTurnCue(-firstSide);
                        oppositeCuePlayed = true;
                    }

                    yield return null;
                }

                sweepMonitor.StopProfileTracking();
                if (!oppositeCuePlayed)
                {
                    headTrainingView.PlayTurnCue(-firstSide);
                }

                string problem = FindHeadMotionProblem(checkTurnPoints: true);
                string hint = problem == null ? null : TrainingHint(problem);
                goodSweeps = problem == null ? goodSweeps + 1 : 0;
                WriteMarker(
                    "HeadTrainingAttempt;task=random_dot_instrument;attempt={0};direction={1};accepted={2};" +
                    "consecutive_good={3};reason={4};profile_rmse_deg={5:F3};first_turn_error_deg={6:F3};" +
                    "second_turn_error_deg={7:F3};mean_speed_deg_s={8:F3};peak_speed_deg_s={9:F3}",
                    attempt, direction, problem == null ? 1 : 0, goodSweeps, hint ?? "none",
                    sweepMonitor.ProfileErrorDegrees, sweepMonitor.FirstExtremeErrorDegrees,
                    sweepMonitor.SecondExtremeErrorDegrees, sweepMonitor.MeanAbsoluteYawSpeedDegreesPerSecond,
                    sweepMonitor.PeakAbsoluteYawSpeedDegreesPerSecond);
                sessionState = State.HeadTrainingFeedback;
                headTrainingView.ShowFeedback(
                    hint ?? "GOOD - FOLLOW THAT RHYTHM", goodSweeps, requiredGoodTrainingSweeps);
                yield return new WaitForSecondsRealtime(1f);
            }

            yield return WaitUntilHeadCentered(observer);

            trainedHeadMotionBlock = !standaloneTraining && trialQueue.TryPeekNext(out RandomDotTrial completedNext)
                ? completedNext.MotionBlockIndex : 0;
            headTrainingView.ShowCompleted();
            WriteMarker("HeadTrainingCompleted;task=random_dot_instrument;motion_block={0};attempts={1}",
                trainedHeadMotionBlock, attempt);
            yield return new WaitForSecondsRealtime(1f);
            headTrainingView.Hide();
            headTrainingCoroutine = null;
            if (standaloneTraining)
            {
                ShowWelcomeScreen();
                yield break;
            }
            sessionState = State.InterTrial;
            BeginTrainingOrNextAttempt();
        }

        // Zeigt "zurück zur Mitte", bis der Kopf wieder geradeaus schaut.
        private IEnumerator WaitUntilHeadCentered(Transform observer)
        {
            while (IsHeadOffCenter(observer))
            {
                headTrainingView.ShowCentering(HeadYawFromTrainingCenter(observer), sweepAmplitudeDegrees);
                yield return null;
            }
        }

        // Mehr als ein halbes Grad neben der Mitte zählt als "nicht in der Mitte".
        private bool IsHeadOffCenter(Transform observer)
        {
            return Mathf.Abs(HeadYawFromTrainingCenter(observer)) > 0.5f;
        }

        private float HeadYawFromTrainingCenter(Transform observer)
        {
            return RandomDotHeadSweepMonitor.YawFromStart(trainingCenterForward, observer);
        }

        private static RandomDotSweepDirection Opposite(RandomDotSweepDirection direction)
        {
            return direction == RandomDotSweepDirection.RightFirst
                ? RandomDotSweepDirection.LeftFirst
                : RandomDotSweepDirection.RightFirst;
        }

        private string FindHeadMotionProblem(bool checkTurnPoints)
        {
            // Die Regeln für eine geführte Kopfbewegung gelten im Training und
            // in geführten Haupttrials. Freie Haupttrials rufen diese Prüfung nicht auf.
            // Nur im Training wird zusätzlich geprüft,
            // ob die beiden Umkehrpunkte gut getroffen wurden.
            // Gibt den ersten Fehler als kurzen Text zurück, oder null, wenn alles passt.
            if (sweepMonitor.MaximumAbsoluteYawDegrees > maxHeadTurnDegrees)
            {
                return "head_yaw_too_large";
            }

            if (sweepMonitor.CompletedHalfSweeps < requiredSweeps)
            {
                return "head_sweep_incomplete";
            }

            if (sweepMonitor.MeanAbsoluteYawSpeedDegreesPerSecond < minMeanHeadSpeed)
            {
                return "head_sweep_too_slow";
            }

            if (sweepMonitor.PeakAbsoluteYawSpeedDegreesPerSecond > maxPeakHeadSpeed)
            {
                return "head_sweep_too_fast";
            }

            bool turnPointsMissed = checkTurnPoints
                && (sweepMonitor.FirstExtremeErrorDegrees > maximumTrainingEndpointErrorDegrees
                    || sweepMonitor.SecondExtremeErrorDegrees > maximumTrainingEndpointErrorDegrees);
            if (sweepMonitor.ProfileErrorDegrees > maximumProfileErrorDegrees || turnPointsMissed)
            {
                return "head_sweep_profile_mismatch";
            }

            return null;
        }

        // Der Hinweis, den die Person im Training zu einem Fehler im Headset liest.
        private static string TrainingHint(string problem)
        {
            return problem switch
            {
                "head_yaw_too_large" => "TOO FAR - TURN LESS",
                "head_sweep_incomplete" => "REACH BOTH SIDES",
                "head_sweep_too_slow" => "TOO SLOW - FOLLOW BLUE",
                "head_sweep_too_fast" => "TOO FAST - MOVE SMOOTHLY",
                _ => "FOLLOW BLUE MORE CLOSELY"
            };
        }

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
            currentMotionBlock = currentTrial.MotionBlockIndex;
            currentMiniBlock = currentTrial.MiniBlockIndex;
            ConfigureStimulus(currentTrial);

            // ConfigureCriterion setzt den Sweep Monitor auch gleich auf null zurück.
            sweepMonitor.ConfigureCriterion(turnaroundDegrees, requiredSweeps);
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

        // Aktive Trials mit Geschwindigkeitserkennung warten zuerst auf die richtige
        // Kopfbewegung. Alle anderen Trials starten wie bisher sofort.
        private void PresentOrWaitForHeadMotion()
        {
            if (IsVelocityGatedTrial(currentTrial))
            {
                BeginWaitingForHeadMotion();
            }
            else
            {
                PresentCurrentTrial();
            }
        }

        private void BeginWaitingForHeadMotion()
        {
            // Graue Fläche und Kreuz, noch keine Punkte. Die Messung beginnt neu, und
            // ab jetzt läuft die Pflicht-Wartezeit (auch nach einem Abbruch).
            BeginHeadSpeedWait();
            stimulus.ShowFixationOnly();
            sessionState = State.WaitingForHeadMotion;
            WriteMarker("WaitingForHeadMotion;task=random_dot_instrument;sequence={0};attempt={1};" +
                "simulated_speed_deg_s={2:F3};min_wait_s={3:F3}", currentTrial.SequenceIndex,
                currentTrial.AttemptNumber, VelocityGateTargetSpeed(currentTrial), minimumWaitBeforeDotsSeconds);
        }

        private void UpdateVelocityGate()
        {
            bool presenting = sessionState == State.PresentingMotion;
            SampleHeadSpeed(presenting);
            if (sessionState == State.WaitingForHeadMotion && HeadReadyForDots)
            {
                BeginHeadSpeedPresentation();
                PresentCurrentTrial();
            }
            else if (presenting && HeadSpeedChangedTooLong)
            {
                // Tempo hat sich während der Punkte zu stark geändert: abbrechen, später wiederholen.
                InvalidateCurrentTrial("head_speed_changed");
            }
        }

        // Gemeinsame Kopftempo-Regel für Trials und Active Practice. Kein Zieltempo:
        // Die Person schwenkt so schnell, wie es sich natürlich anfühlt. Vor den Punkten
        // zählt nur, ob der Kopf gleichmäßig dreht; während der Punkte ist das Tempo
        // direkt davor der Maßstab (± Velocity Change Tolerance).

        // Messung neu beginnen; ab jetzt läuft auch die Pflicht-Wartezeit.
        private void BeginHeadSpeedWait()
        {
            velocityGate.Reset();
            steadyHead.Reset();
            offAxis.Reset();
            presentationReferenceSpeed = float.NaN;
            presentationDirection = string.Empty;
            currentSweepSign = 0;
            currentSweepTravelDegrees = 0f;
            presentationTurnBeforeDots = float.NaN;
            waitingForHeadMotionSince = Time.realtimeSinceStartupAsDouble;
        }

        // Einmal pro Frame aufrufen.
        private void SampleHeadSpeed(bool presenting)
        {
            float minimum = presenting ? presentationReferenceSpeed * (1f - velocityChangeTolerance) : 0f;
            float maximum = presenting ? presentationReferenceSpeed * (1f + velocityChangeTolerance) : float.MaxValue;
            velocityGate.AddSample(stimulus.Observer.forward, Time.unscaledDeltaTime, velocityGateAxis,
                velocitySmoothingSeconds, minimum, maximum);
            if (!presenting)
            {
                // Abwechselnde Richtung: Ein Schwenk in die Richtung des letzten Trials
                // zählt wie Stillhalten, die Punkte kommen erst beim Zurückschwenken.
                bool rightWay = RequiredSweepSign == 0
                    || Math.Sign(velocityGate.SignedSpeedDegreesPerSecond) == RequiredSweepSign;
                steadyHead.Add(rightWay ? velocityGate.SpeedDegreesPerSecond : 0f, Time.unscaledDeltaTime,
                    velocityChangeTolerance, minimumMovingHeadSpeed);

                // Strecke des laufenden Schwenks: zählt nur, solange der Kopf in eine (erlaubte)
                // Richtung dreht. Stillhalten oder Umkehren beginnt einen neuen Schwenk.
                float signed = velocityGate.SignedSpeedDegreesPerSecond;
                bool moving = Mathf.Abs(signed) >= minimumMovingHeadSpeed;
                if (moving && rightWay)
                {
                    int sign = signed >= 0f ? 1 : -1;
                    if (sign != currentSweepSign)
                    {
                        currentSweepSign = sign;
                        currentSweepTravelDegrees = 0f;
                    }
                    currentSweepTravelDegrees += Mathf.Abs(signed) * Time.unscaledDeltaTime;
                }
                else
                {
                    currentSweepSign = 0;
                    currentSweepTravelDegrees = 0f;
                }
            }
            else
                offAxis.Add(stimulus.Observer.forward, stimulus.Observer.up, Time.unscaledDeltaTime);
        }

        // Wartezeit vorbei und lange genug gleichmäßig: Die Punkte dürfen kommen.
        // Alle Bedingungen zusammen; die Mindeststrecke allein löst nie etwas aus.
        private bool HeadReadyForDots =>
            Time.realtimeSinceStartupAsDouble - waitingForHeadMotionSince >= minimumWaitBeforeDotsSeconds
            && steadyHead.SteadySeconds >= velocityHoldSeconds
            && currentSweepTravelDegrees >= minimumTurnBeforeDotsDegrees;

        // Das (geglättete) Tempo direkt vor den Punkten wird zum Maßstab. Nicht der
        // Mittelwert des ganzen gleichmäßigen Abschnitts: Der enthält noch einen Teil
        // des Anfahrens und läge dadurch etwas unter dem echten Tempo.
        private void BeginHeadSpeedPresentation()
        {
            presentationReferenceSpeed = velocityGate.SpeedDegreesPerSecond;
            presentationTurnBeforeDots = currentSweepTravelDegrees;
            presentationDirection = RandomDotHeadOffAxisMeter.Direction(
                velocityGate.SignedSpeedDegreesPerSecond, velocityGateAxis);
            // Auch ein abgebrochener Schwenk zählt: Der nächste geht in die andere Richtung.
            lastSweepSign = velocityGate.SignedSpeedDegreesPerSecond >= 0f ? 1 : -1;
            velocityGate.BeginPresentation();
            // Ab jetzt auch Nicken/Drehen neben der Schwenkachse und Kippen messen.
            offAxis.Begin(stimulus.Observer.forward, stimulus.Observer.up, velocityGateAxis);
        }

        private bool HeadSpeedChangedTooLong => velocityGate.SecondsOutOfRange > velocityGraceSeconds;

        // +1/-1: in diese Richtung muss der nächste Schwenk gehen, 0 = egal.
        private int RequiredSweepSign => alternateSweepDirection ? -lastSweepSign : 0;

        /// <summary>Richtung des nächsten Schwenks (Right/Left bzw. Up/Down) oder "any".</summary>
        public string NextSweepDirection => RequiredSweepSign == 0
            ? "any"
            : RandomDotHeadOffAxisMeter.Direction(RequiredSweepSign, velocityGateAxis);

        // Live-Anzeige für den Experiment Monitor: dieselbe Erkennung "gleichmäßig"
        // wie im Trial, für das m und den Zoom, die gerade am Punktfeld stehen (in der
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
            float simulatedHeadSpeed = RandomDotSimulatedSweep.ObjectSpeed(simulatedSpeedReference, sweepSpeed,
                simulatedImageCenterSpeed, m, zoom);
            liveHeadSpeed.AddSample(observer.forward, Time.unscaledDeltaTime, velocityGateAxis,
                velocitySmoothingSeconds, simulatedHeadSpeed, velocityChangeTolerance, minimumMovingHeadSpeed,
                velocityHoldSeconds, m * zoom);
        }

        // Pilot in der Vorschau, mit Brille auf: Tempo speichern, das sich natürlich
        // anfühlt (Taste oder Controller), und auf Wunsch das Tempo im Headset sehen.
        private void UpdateHeadSpeedPilot(Keyboard keyboard)
        {
            if (stimulus == null || stimulus.Observer == null) return;
            pilotSpeedView ??= new RandomDotPilotSpeedView(stimulus.Observer);

            if (keyboard != null && keyboard[headSpeedReadoutKey].wasPressedThisFrame)
                showHeadSpeedInHeadset = !showHeadSpeedInHeadset;

            bool savePressed = (keyboard != null && keyboard[saveHeadSpeedKey].wasPressedThisFrame)
                || VrControllerResponses.TryReadPress(out _);
            if (savePressed && liveHeadSpeed.TrySaveSpeed(out RandomDotSavedSpeed saved))
            {
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "Pilot: natürliches Kopftempo gespeichert: {0:F2} °/s Kopf, {1:F1} °/s Punkte in der " +
                    "Bildmitte (m × Zoom = {2:F2}). Bisher {3} Werte.", saved.HeadSpeed, saved.ImageSpeed,
                    saved.ImageGain, liveHeadSpeed.SavedSpeeds.Count), this);
                pilotSpeedView.PlaySavedTone();
                pilotSavedMessageUntil = Time.unscaledTime + 1f;
            }

            // Nach dem Speichern kurz "SAVED", ohne Zahl, wenn die Anzeige aus ist.
            string speed = string.Format(CultureInfo.InvariantCulture, "HEAD {0:F1} deg/s   DOTS {1:F0} deg/s",
                liveHeadSpeed.AverageHeadSpeed, liveHeadSpeed.AverageImageCenterSpeed);
            if (Time.unscaledTime < pilotSavedMessageUntil)
                pilotSpeedView.Show(showHeadSpeedInHeadset ? "SAVED   " + speed : "SAVED");
            else if (showHeadSpeedInHeadset)
                pilotSpeedView.Show(speed);
            else
                pilotSpeedView.Hide();
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
            ClearCrossSignal();
            // Die aktive Punktwelt wird erst jetzt am aktuellen Kopf ausgerichtet.
            // Zwischen Fixationsbeginn und Trialstart kann sich der Kopf bewegen.
            stimulus.PlaceAroundObserver();
            sweepMonitor.ResetForTrial();
            stimulus.RestartMotionPhase();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            if (currentTrial.MotionMode == RandomDotMotionMode.HeadTracked && !freeHeadMovement)
            {
                sweepMonitor.StartProfileTracking(
                    sweepAmplitudeDegrees, sweepSpeed, currentTrial.SweepDirection);
            }

            stimulusEndUnitySeconds = 0d;
            frameTiming.Reset(FrameTimingTracker.CurrentExpectedFrameMilliseconds());
            frameTimingStartFrame = Time.frameCount;
            sessionState = State.PresentingMotion;

            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show(dotsFadeInSeconds);
            // Offene aktive Trials enden erst mit der Antwort, nicht nach einer Zeit.
            bool openEnded = IsOpenEndedTrial(currentTrial);
            if (!openEnded)
                motionCoroutine = StartCoroutine(EndMotionAfterDuration());

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Random-Dot-Trial {0}/{1}, Präsentation {2}: k={3:F3}, m={4:F2}x, ContentZoom={5:F2}, " +
                "{6}, Achse {7}, Richtung {8}, {9} Punkte, Versuch {10}. {11}",
                currentTrialNumber, totalTrials, presentationCount,
                currentTrial.InstrumentDistortionK, currentTrial.InstrumentMagnificationM,
                currentTrial.ContentZoom, currentTrial.MotionMode, currentTrial.SweepAxis,
                currentTrial.DirectionLabel, stimulus.DotCount, currentTrial.AttemptNumber,
                openEnded
                    ? "Frei umschauen, ohne Zeitlimit; die Antwort beendet den Trial."
                    : "Fixationskreuz anschauen; Antwort folgt nach der Bewegung."), this);
        }

        private IEnumerator EndMotionAfterDuration()
        {
            // Eine Coroutine wartet, ohne dass dabei alles andere stehen bleibt.
            // Unity zeichnet weiter und das Eye Tracking misst weiter.
            yield return new WaitForSecondsRealtime(MotionSeconds(currentTrial));
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

            // Die Kopfbewegung wird nur im HeadTracked-Block geprüft.
            sweepMonitor.StopProfileTracking();
            bool headTracked = currentTrial.MotionMode == RandomDotMotionMode.HeadTracked;
            string motionProblem =
                checkHeadMotion && headTracked && !freeHeadMovement
                    ? FindHeadMotionProblem(checkTurnPoints: false) : null;
            if (motionProblem != null)
            {
                InvalidateCurrentTrial(motionProblem);
                return;
            }

            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            // Nur die Punkte ausblenden. Der graue Kreis und das Fixationskreuz
            // bleiben stehen, damit der Wechsel zur Antwortphase keinen globalen
            // Helligkeitssprung im Headset erzeugt.
            stimulus.ShowFixationOnly();
            sessionState = State.WaitingForResponse;
            ShowAnswerSignal();
            WriteMarker(
                "StimulusEnded;task=random_dot_instrument;sequence={0};attempt={1};duration_s={2:F4};" +
                "profile_rmse_deg={3:F4};first_turn_error_deg={4:F4};second_turn_error_deg={5:F4}",
                currentTrial.SequenceIndex, currentTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds,
                HeadTrackedOnly(sweepMonitor.ProfileErrorDegrees),
                HeadTrackedOnly(sweepMonitor.FirstExtremeErrorDegrees),
                HeadTrackedOnly(sweepMonitor.SecondExtremeErrorDegrees));
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            if (sessionState == State.ActivePracticeResponse && response != CheckerboardCurvatureResponse.None)
            {
                trainingResponseReceived = true;
                trainingResponse = response;
                // Sofort schließen: ein zweiter Tastendruck zählt nicht für den nächsten Übungstrial.
                sessionState = State.ActivePracticeMotion;
                return;
            }
            if (sessionState == State.SimulatedTrainingResponse && response != CheckerboardCurvatureResponse.None)
            {
                trainingResponseReceived = true;
                trainingResponse = response;
                // Sofort schließen: ein zweiter Tastendruck darf nicht das nächste Beispiel beantworten.
                sessionState = State.SimulatedTrainingMotion;
                return;
            }
            // Drückt die Person schon während der Bewegung, zählt das nicht. Sie
            // soll sich erst die ganze Bewegung ansehen und dann entscheiden.
            // Ausnahme: offene aktive Trials. Dort schaut die Person frei, so lange
            // sie will, und die Antwort beendet die Darbietung.
            // Die erste Antwort beendet die Antwortphase sofort. Ein zweiter
            // Tastendruck landet deshalb in keinem Durchgang, auch nicht im nächsten.
            bool answeredWhileVisible = sessionState == State.PresentingMotion
                && currentTrial != null && IsOpenEndedTrial(currentTrial);
            if ((sessionState != State.WaitingForResponse && !answeredWhileVisible) || currentTrial == null
                || response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            // Bei einer Antwort während der Punkte ist noch kein Darbietungsende gesetzt.
            // CaptureCurrentResult nimmt dann den Antwortzeitpunkt als Ende, also genau
            // denselben Moment (Antwortzeit 0). Der Marker kommt erst danach, damit das
            // Schreiben in die Aufnahme die Zeiten nicht auseinanderzieht.
            RandomDotTrialResult result = CaptureCurrentResult(response, validForAnalysis: true, "valid");
            if (answeredWhileVisible)
            {
                stimulusEndUnitySeconds = result.StimulusEndUnitySeconds;
                WriteMarker(
                    "StimulusEnded;task=random_dot_instrument;sequence={0};attempt={1};duration_s={2:F4};" +
                    "ended_by=response",
                    currentTrial.SequenceIndex, currentTrial.AttemptNumber,
                    stimulusEndUnitySeconds - trialStartUnitySeconds);
            }

            if (!TryAppendResult(result))
            {
                return;
            }

            ShowAnsweredSignal();
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
            if (fixationMonitor == null)
            {
                InvalidateCurrentTrial("missing_fixation_monitor");
                return;
            }

            bool sampleUsable =
                fixationMonitor.HasRecentSample(maxSampleAgeSeconds) && fixationMonitor.CurrentSampleValid;
            lookAway.Add(Time.unscaledDeltaTime, sampleUsable, fixationMonitor.IsInsideTolerance);
            string problem = lookAway.FindProblem(maxLookAwaySeconds, maxNoDataSeconds);
            if (problem != null)
            {
                InvalidateCurrentTrial(problem);
            }
        }

        private void InvalidateCurrentTrial(string reason)
        {
            // Bewegung stoppen, den misslungenen Versuch trotzdem speichern und
            // denselben Durchgang mit höherer Versuchsnummer ganz hinten wieder
            // in die Warteschlange hängen.
            if (sessionState != State.PresentingMotion || currentTrial == null)
            {
                return;
            }

            StopAndClear(ref motionCoroutine);
            ShowStoppedSignal();
            repeatedAttempts++;
            if (reason == "head_speed_changed") speedChangedAttempts++;
            sweepMonitor.StopProfileTracking();
            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            RandomDotTrial invalidTrial = currentTrial;
            RandomDotTrialResult result = CaptureCurrentResult(
                CheckerboardCurvatureResponse.None, validForAnalysis: false, "invalid:" + reason);
            if (!TryAppendResult(result))
            {
                return;
            }

            WriteMarker(
                "TrialInvalid;task=random_dot_instrument;sequence={0};attempt={1};reason={2};" +
                "off_target_s={3:F4};invalid_gaze_s={4:F4};mean_yaw_speed_deg_s={5:F4};" +
                "peak_yaw_speed_deg_s={6:F4};max_abs_yaw_deg={7:F4}",
                invalidTrial.SequenceIndex, invalidTrial.AttemptNumber, reason,
                lookAway.LongestLookAwaySeconds, lookAway.LongestNoDataSeconds,
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
            double stimulusEnd = stimulusEndUnitySeconds > trialStartUnitySeconds
                ? stimulusEndUnitySeconds
                : responseTime;
            FixationSnapshot gaze =
                fixationMonitor != null ? fixationMonitor.TakeSnapshot() : FixationSnapshot.None;

            return new RandomDotTrialResult(
                currentTrial, presentationCount, trialStartUtc,
                trialStartUnitySeconds, stimulusEnd, responseTime,
                response, validForAnalysis,
                sweepMonitor.CompletedHalfSweeps,
                sweepMonitor.MinimumYawDegrees,
                sweepMonitor.MaximumYawDegrees,
                sweepMonitor.MaximumAbsoluteYawDegrees,
                sweepMonitor.MeanAbsoluteYawSpeedDegreesPerSecond,
                sweepMonitor.PeakAbsoluteYawSpeedDegreesPerSecond,
                HeadTrackedOnly(sweepMonitor.ProfileErrorDegrees),
                HeadTrackedOnly(sweepMonitor.FirstExtremeErrorDegrees),
                HeadTrackedOnly(sweepMonitor.SecondExtremeErrorDegrees),
                IsFreeHeadTrial(currentTrial) ? float.NaN : stimulus.SweepAmplitudeDegrees,
                IsFreeHeadTrial(currentTrial) ? float.NaN : stimulus.SweepSpeedDegreesPerSecond,
                stimulus.ApertureEdgeSoftnessDegrees,
                gaze.SampleValid, gaze.OnTarget, gaze.AngleDegrees,
                gaze.SteadySeconds, gaze.ValidSampleFraction,
                lookAway.LongestLookAwaySeconds, lookAway.LongestNoDataSeconds,
                stimulus.DotCount, stimulus.WorldCoverageDiameterDegrees, stimulus.FieldRadiusMeters,
                status, IsFreeHeadTrial(currentTrial), frameTiming,
                headVelocity: IsVelocityGatedTrial(currentTrial)
                    ? velocityGate.Summarize(VelocityGateTargetSpeed(currentTrial))
                    : HeadVelocitySummary.None,
                headSpeedReference: IsVelocityGatedTrial(currentTrial) ? presentationReferenceSpeed : float.NaN,
                headDirection: IsVelocityGatedTrial(currentTrial) ? presentationDirection : string.Empty,
                offAxisSpeed: IsVelocityGatedTrial(currentTrial) ? offAxis.MeanOffAxisSpeed : float.NaN,
                offAxisDeviation: IsVelocityGatedTrial(currentTrial) ? offAxis.MaxOffAxisDeviationDegrees : float.NaN,
                rollDeviation: IsVelocityGatedTrial(currentTrial) ? offAxis.MaxRollDeviationDegrees : float.NaN,
                turnBeforeDots: IsVelocityGatedTrial(currentTrial) ? presentationTurnBeforeDots : float.NaN);
        }

            // Die Fehlerwerte zur Sollbahn gibt es nur bei geführten HeadTracked-Trials.
        // In allen anderen Durchgängen steht NaN in der Datei.
        private float HeadTrackedOnly(float value)
        {
            return currentTrial.MotionMode == RandomDotMotionMode.HeadTracked && !freeHeadMovement
                ? value : float.NaN;
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
            // Punkte ausblenden, kurz warten und dann den nächsten Durchgang holen.
            // Der neutrale graue Kreis bleibt durchgehend sichtbar. So wechseln
            // weder mittlere Helligkeit noch Größe der kreisförmigen Öffnung.
            StopAndClear(ref motionCoroutine);
            stimulus.ShowFixationOnly();
            currentTrial = null;

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
            if (pauseSeconds <= 0f)
            {
                BeginNextAttempt();
            }
            else
            {
                interTrialCoroutine = StartCoroutine(BeginNextAttemptAfterDelay());
            }
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
            if (stimulus.Observer != null)
            {
                headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
                headTrainingView.ShowBlockPause(pausesTaken, blocksPerSession);
            }
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
            headTrainingView?.Hide();
            WriteMarker("BlockPauseEnded;task=random_dot_instrument;block={0}", pausesTaken);
            sessionState = State.InterTrial;
            BeginNextAttempt();
        }

        public int PausesTaken => pausesTaken;

        private IEnumerator BeginNextAttemptAfterDelay()
        {
            yield return new WaitForSecondsRealtime(pauseSeconds);
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
            if (stimulus.Observer != null)
            {
                headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
                headTrainingView.ShowWelcome(
                    ResponseInputController.GetReadableKeyName(startSessionKey),
                    ResponseInputController.GetReadableKeyName(startOtherMotionSessionKey),
                    ResponseInputController.GetReadableKeyName(trainingKey),
                    ResponseInputController.GetReadableKeyName(previewKey), sessionMotionMode, completed: true);
            }
            Debug.Log($"Random-Dot-Sitzung vollständig gespeichert: {validTrialsCompleted} gültige Trials " +
                $"aus {presentationCount} Präsentationen.\n" + activeSessionFolder, this);
        }

        private void FailAfterWriteError(Exception exception)
        {
            Debug.LogError("Random-Dot-Trial konnte nicht gespeichert werden; die Sitzung wird beendet: " +
                exception.Message, this);
            WriteMarker("SessionAborted;task=random_dot_instrument;reason=result_write_error");
            StopPendingCoroutines();
            headTrainingView?.Hide();
            EndSession(State.Aborted);
        }

        // Beendet die Sitzung: Punkte aus, Aufnahme stoppen, Zustand setzen.
        // Das brauchen Abbrechen, Fertigwerden und Schreibfehler gleichermaßen.
        private void EndSession(State endState)
        {
            ClearCrossSignal();
            if (stimulus != null)
            {
                stimulus.Hide();
                stimulus.SessionRunning = false;
            }

            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = endState;
        }

        private void HandleHalfSweepCompleted(int count, float sweepDegrees)
        {
            // Der Sweep Monitor sagt Bescheid, sobald die Bewegung wieder an einem
            // Rand angekommen ist. So kann man hinterher nachzählen, wie oft die
            // Punkte in einem Durchgang hin und her gelaufen sind.
            if (sessionState != State.PresentingMotion || currentTrial == null)
            {
                return;
            }

            WriteMarker(
                "MotionHalfSweep;task=random_dot_instrument;sequence={0};count={1};sweep_deg={2:F3};" +
                "instrument_k={3:F4};instrument_m={4:F4};axis={5}",
                currentTrial.SequenceIndex, count, sweepDegrees, currentTrial.InstrumentDistortionK,
                currentTrial.InstrumentMagnificationM, currentTrial.SweepAxis);
        }

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
                "simulated_speed_reference={7};simulated_image_center_speed_deg_s={8:F3};" +
                "object_speed_deg_s={9:F3};controller_mapping={10};convex_control={11};" +
                "concave_control={12};response_keys_swapped={13}",
                ExperimentFilesBase.SanitizeIdentifier(participantId, "pilot"),
                ExperimentFilesBase.SanitizeIdentifier(sessionLabel, "random_dot"),
                randomSeed, trialPlan.Count, sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                RandomDotExperimentFiles.MappingVersion, stimulus.DotDensity,
                simulatedSpeedReference, simulatedImageCenterSpeed, sweepSpeed,
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
                IsFreeHeadTrial(trial) ? "Free" : trial.DirectionLabel,
                IsOpenEndedTrial(trial) ? float.NaN : MotionSeconds(trial),
                IsFreeHeadTrial(trial) ? float.NaN : SweepAmplitude(trial),
                IsFreeHeadTrial(trial) ? float.NaN : SweepSpeed(trial),
                trial.DotSeed, trial.SweepAxis, stimulus.DotCount,
                RandomDotSimulatedSweep.ImageCenterSpeed(
                    IsFreeHeadTrial(trial) ? float.NaN : SweepSpeed(trial),
                    trial.InstrumentMagnificationM, trial.ContentZoom),
                trial.MotionMode == RandomDotMotionMode.SimulatedYaw
                    ? "simulated" : IsFreeHeadTrial(trial) ? "free" : "guided");
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

        // Wie schnell im Trial über die Außenwelt geschwenkt wird, in Grad
        // Objektwinkel pro Sekunde. Im HeadTracked-Block ist das die mittlere
        // Kopfgeschwindigkeit. Im simulierten Block hängt es von der gewählten
        // Definition ab: Bei "Bildmitte" wird so geschwenkt, dass die Punkte in der
        // Bildmitte bei jedem m gleich schnell laufen, also bei großem m langsamer.
        private float SweepSpeed(RandomDotTrial trial) =>
            trial.MotionMode == RandomDotMotionMode.HeadTracked
                ? sweepSpeed
                : RandomDotSimulatedSweep.ObjectSpeed(simulatedSpeedReference, sweepSpeed,
                    simulatedImageCenterSpeed, trial.InstrumentMagnificationM, trial.ContentZoom);

        private bool IsFreeHeadTrial(RandomDotTrial trial) =>
            trial.MotionMode == RandomDotMotionMode.HeadTracked && freeHeadMovement;

        /// <summary>Ergebnis der letzten Übungsrückmeldung; null = keine Rückmeldung (k nicht eindeutig).</summary>
        public bool? LastTrainingAnswerCorrect { get; private set; }

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

        /// <summary>So weit darf der Kopf im HeadTracked-Block drehen, bevor die Punktwelt endet.</summary>
        public float HeadTrackedReachDegrees => Mathf.Max(sweepAmplitudeDegrees, headTurnSafetyDegrees);

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

        //Aktiver Trial mit Geschwindigkeitserkennung (Vorrang vor "offen")
        private bool IsVelocityGatedTrial(RandomDotTrial trial) =>
            IsFreeHeadTrial(trial) && useVelocityGate;

        // Zieltempo der Kopfdrehung in Grad pro Sekunde: genau so schnell, wie das
        // Intrument im simulierten Schwenk über die Außenwelt dreht. Dann laufen
        // die Punkte im Bild bei Simulated und Active gleich schnell.
        private float VelocityGateTargetSpeed(RandomDotTrial trial) =>
            RandomDotSimulatedSweep.ObjectSpeed(simulatedSpeedReference, sweepSpeed,
                simulatedImageCenterSpeed, trial.InstrumentMagnificationM, trial.ContentZoom);
        
        private bool IsOpenEndedTrial(RandomDotTrial trial) =>
            IsFreeHeadTrial(trial) && openEndedActiveTrials && !useVelocityGate;

        private bool OpenEndedSession =>
            sessionMotionMode == RandomDotMotionMode.HeadTracked && freeHeadMovement && openEndedActiveTrials
            && !useVelocityGate;

        /// <summary>Zieltempo der Kopfdrehung für das erste m und den ersten Zoom (Anzeige im Inspector).</summary>
        public float VelocityGateTargetSpeedPreview => RandomDotSimulatedSweep.ObjectSpeed(
            simulatedSpeedReference, sweepSpeed, simulatedImageCenterSpeed,
            instrumentMagnificationMValues.Count > 0 ? instrumentMagnificationMValues[0] : 1f,
            contentZoomValues.Count > 0 ? contentZoomValues[0] : 1f);

        // Wie weit das Feld im Trial zu jeder Seite der Mitte schwenkt, in Grad. Der
        // simulierte Schwenk läuft in der eingestellten Zeit mit fester Geschwindigkeit
        // einmal von der einen Seite zur anderen, also die halbe Strecke zu jeder Seite.
        // Die Dauer bleibt bei jedem m gleich; bei "Bildmitte" wird deshalb die
        // Schwenkweite in der Außenwelt mit wachsendem m kleiner.
        private float SweepAmplitude(RandomDotTrial trial, float? simulatedSecondsOverride = null) =>
            trial.MotionMode == RandomDotMotionMode.HeadTracked
                ? sweepAmplitudeDegrees
                : 0.5f * SweepSpeed(trial) * (simulatedSecondsOverride ?? simulatedSweepSeconds);

        // Wie lange die Punkte im Trial zu sehen sind, in Sekunden.
        private float MotionSeconds(RandomDotTrial trial) =>
            IsVelocityGatedTrial(trial) ? simulatedSweepSeconds
            : trial.MotionMode == RandomDotMotionMode.HeadTracked ? headTrackedSeconds : simulatedSweepSeconds;

        private float WorldCoverageFor(RandomDotTrial trial, float? simulatedSecondsOverride = null)
        {
            // Wie groß die Punktwelt für diesen Trial sein muss. Im HeadTracked-Block
            // kann der Kopf weiter drehen als der Sollschwenk. Dafür gibt es dort den
            // größeren Sicherheitsbereich.
            float reach = trial.MotionMode == RandomDotMotionMode.HeadTracked
                ? Mathf.Max(sweepAmplitudeDegrees, headTurnSafetyDegrees)
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

        // Blickkontrolle und Wegschau-Zähler fangen für den Durchgang bei null an.
        private void ResetFixationChecks()
        {
            if (fixationMonitor != null)
            {
                fixationMonitor.ResetFixationWindow();
            }

            lookAway.Reset();
        }

        private void WriteMarker(string message)
        {
            if (standaloneTraining) return;
            if (eyeTrackingToolbox != null)
            {
                eyeTrackingToolbox.WriteMessage(message);
            }
        }

        // Wie string.Format, aber immer mit Punkt als Dezimaltrennzeichen, egal
        // welche Sprache Windows eingestellt hat. Sonst stünde mal 0,5 und mal 0.5
        // in der Aufnahme.
        private void WriteMarker(string format, params object[] values)
        {
            WriteMarker(string.Format(CultureInfo.InvariantCulture, format, values));
        }

        private void StopEyeTrackingRecording()
        {
            if (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }
        }

        // Hält eine laufende Coroutine an und merkt sich, dass keine mehr läuft.
        private void StopAndClear(ref Coroutine coroutine)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
                coroutine = null;
            }
        }

        private void StopPendingCoroutines()
        {
            StopAndClear(ref simulatedTrainingCoroutine);
            StopAndClear(ref activePracticeCoroutine);
            StopAndClear(ref motionCoroutine);
            StopAndClear(ref headTrainingCoroutine);
            StopAndClear(ref interTrialCoroutine);
        }

        private void DisposeTrainingView()
        {
            headTrainingView?.Dispose();
            headTrainingView = null;
            pilotSpeedView?.Dispose();
            pilotSpeedView = null;
        }

        private void OnValidate()
        {
            repeatsPerCondition = Mathf.Max(1, repeatsPerCondition);
            simulatedTrainingRepeatsPerValue = Mathf.Max(1, simulatedTrainingRepeatsPerValue);
            // Die Konkav-Grenze muss unter der Konvex-Grenze liegen.
            feedbackConcaveMaxK = Mathf.Min(feedbackConcaveMaxK, feedbackConvexMinK - 0.01f);
            simulatedSweepSeconds = Mathf.Clamp(simulatedSweepSeconds, 0.1f, 5f);
            headTrackedSeconds = Mathf.Max(0.1f, headTrackedSeconds);
            sweepAmplitudeDegrees = Mathf.Clamp(sweepAmplitudeDegrees, 0.1f, 30f);
            sweepSpeed = Mathf.Clamp(sweepSpeed, 0.1f, 60f);
            simulatedImageCenterSpeed = Mathf.Clamp(simulatedImageCenterSpeed, 0.5f, 120f);
            requiredGoodTrainingSweeps = Mathf.Clamp(requiredGoodTrainingSweeps, 2, 10);
            maximumProfileErrorDegrees = Mathf.Clamp(maximumProfileErrorDegrees, 0.1f, 3f);
            maximumTrainingEndpointErrorDegrees = Mathf.Clamp(maximumTrainingEndpointErrorDegrees, 0.1f, 3f);
            headTurnSafetyDegrees = Mathf.Clamp(headTurnSafetyDegrees, 3f, 60f);
            turnaroundDegrees = Mathf.Clamp(turnaroundDegrees, 0.5f, 30f);
            requiredSweeps = Mathf.Clamp(requiredSweeps, 1, 10);
            maxHeadTurnDegrees = Mathf.Clamp(maxHeadTurnDegrees, turnaroundDegrees, headTurnSafetyDegrees);
            minMeanHeadSpeed = Mathf.Clamp(minMeanHeadSpeed, 0f, 20f);
            maxPeakHeadSpeed = Mathf.Clamp(maxPeakHeadSpeed, Mathf.Max(0.1f, minMeanHeadSpeed), 30f);
            maxLookAwaySeconds = Mathf.Max(0f, maxLookAwaySeconds);
            maxNoDataSeconds = Mathf.Max(0f, maxNoDataSeconds);
            maxSampleAgeSeconds = Mathf.Max(0.01f, maxSampleAgeSeconds);
            maxRepeatsPerTrial = Mathf.Max(0, maxRepeatsPerTrial);
            pauseSeconds = Mathf.Max(0f, pauseSeconds);
        }
    }
}
