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
        PausedBetweenMiniBlocks, // Alter serialisierter Status; im aktuellen Ablauf nicht mehr verwendet.
        PausedBetweenMotionBlocks, // Alter Status; neue Sitzungen enthalten nur eine Bewegungsart.
        Completed,
        Aborted,
        ResponseInstructions,
        SimulatedTrainingInstructions,
        SimulatedTrainingMotion,
        SimulatedTrainingResponse,
        ActiveMotionInstructions
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
    public sealed class RandomDotExperimentManager : MonoBehaviour
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
        [Tooltip("Kennung der Versuchsperson. Hier gehören keine echten Namen rein, sondern zum Beispiel pilot_001.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "random_dot_instrument_pilot";

        [SerializeField]
        [Tooltip("Mit derselben Zahl und denselben Einstellungen kommt wieder genau dieselbe Reihenfolge heraus.")]
        private int randomSeed = 20260901;

        [SerializeField]
        private int dotSeedBase = 24680;

        [SerializeField]
        [Tooltip("Wohin die Messdaten kommen. Leer lassen ist normal, dann landen sie im Ordner measurements im Projekt.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Trial Plan")]
        [FormerlySerializedAs("angularDiametersDegrees")]
        [SerializeField]
        [Tooltip("Wie groß der runde Ausschnitt sein soll, in Grad. Es können auch mehrere Werte drinstehen, dann wird jeder davon gezeigt.")]
        private List<float> fieldOfViewValues = new() { 90f };

        [SerializeField]
        [Tooltip("Auf welchen Augen gezeigt wird: beide, nur links oder nur rechts.")]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [SerializeField]
        [FormerlySerializedAs("stimulusKValues")]
        [FormerlySerializedAs("visualSpaceLValues")]
        [Tooltip("Alle Instrumentenverzeichnungen k. k = 1 ist geometrisch gerade, k = 0,5 der Helmholtz-/Kreispunkt. Der subjektiv neutrale k-Wert schätzt das l der Person.")]
        private List<float> instrumentDistortionKValues = new() { 1.2f, 1f, 0.8f, 0.6f, 0.5f, 0.4f, 0.2f };

        [SerializeField]
        [Tooltip("Fernglasvergrößerungen m. Ein einzelner Wert hält m fest; mehrere Werte machen m zu einem Versuchsparameter. 10 entspricht einem typischen 10x-Fernglas.")]
        private List<float> instrumentMagnificationMValues = new() { 10f };

        [SerializeField]
        [FormerlySerializedAs("magnifications")]
        [Tooltip("Optionaler Nach-Zoom für das Punktfeld. Für die Fernglassimulation auf 1 lassen; die echte Instrumentenvergrößerung steht in m.")]
        private List<float> contentZoomValues = new() { 1f };

        [SerializeField]
        [Tooltip("Eine Bewegungsart pro Sitzung. F5 startet diese Auswahl; F7 startet die andere als neue Sitzung.")]
        private RandomDotMotionMode sessionMotionMode = RandomDotMotionMode.SimulatedYaw;

        [FormerlySerializedAs("repetitionsPerCondition")]
        [SerializeField, Min(1)]
        [Tooltip("Wie oft jede Kombination gezeigt wird. Mehr Wiederholungen heißt sicherere Ergebnisse, aber auch eine längere Sitzung.")]
        private int repeatsPerCondition = 3;

        [Header("Simulated Sweep")]
        [SerializeField]
        [Tooltip("Achse des simulierten Schwenks: Horizontal = links/rechts, Vertical = oben/unten. " +
            "Freie aktive Kopfbewegung ist nicht an diese Achse gebunden.")]
        private RandomDotSweepAxis simulatedSweepAxis = RandomDotSweepAxis.Horizontal;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Wie lange die Punkte im SimulatedYaw-Block zu sehen sind, in Sekunden. In dieser Zeit schwenkt das Feld einmal in eine Richtung, ohne umzukehren. Ob nach links oder rechts, wechselt von Trial zu Trial in zufälliger Reihenfolge, und beide Richtungen kommen gleich oft dran.")]
        private float simulatedSweepSeconds = 0.8f;

        [SerializeField]
        [Tooltip("Worauf sich die Geschwindigkeit des simulierten Schwenks bezieht. Bildmitte: Die Punkte laufen in der Bildmitte bei jedem m gleich schnell durch das Bild (Simulated Image Center Speed). Objektwinkel: bisherige Definition, das Instrument schwenkt bei jedem m mit Sweep Speed, und das Bild läuft in der Mitte m-mal so schnell. Die durch k erzeugten Unterschiede zwischen Mitte und Rand bleiben in beiden Fällen erhalten. Der HeadTracked-Block ist davon nicht betroffen.")]
        private RandomDotSweepSpeedReference simulatedSpeedReference = RandomDotSweepSpeedReference.ImageCenter;

        [SerializeField, Range(0.5f, 120f)]
        [Tooltip("Sichtbare Winkelgeschwindigkeit der Punkte in der Bildmitte, in Grad pro Sekunde. Gilt nur bei der Einstellung Bildmitte. 12 entspricht dem bisherigen Stand mit m = 10 und 1,2 Grad pro Sekunde.")]
        private float simulatedImageCenterSpeed = 12f;

        [FormerlySerializedAs("sweepSpeedDegreesPerSecond")]
        [SerializeField, Range(0.1f, 60f)]
        [Tooltip("Wie schnell über die Außenwelt geschwenkt wird, in Grad Objektwinkel pro Sekunde. Im HeadTracked-Block ist es die mittlere Geschwindigkeit der Kopfbewegung, die an den Umkehrpunkten langsamer wird. Im SimulatedYaw-Block gilt der Wert nur bei der Einstellung Objektwinkel; dann bleibt die Geschwindigkeit die ganze Zeit gleich. Überschreibt den Vorschauwert am Random Dot Field.")]
        private float sweepSpeed = 1.2f;

        [Header("Active Head Tracked Sweep")]
        [FormerlySerializedAs("motionSeconds")]
        [FormerlySerializedAs("motionDurationSeconds")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Wie lange die Punkte im HeadTracked-Block zu sehen sind, in Sekunden. " +
            "Die Bewegung kann frei oder optional geführt sein.")]
        private float headTrackedSeconds = 5f;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Wie weit die Person den Kopf im HeadTracked-Block zu jeder Seite drehen soll, in Grad.")]
        private float sweepAmplitudeDegrees = 2f;

        [SerializeField]
        [Tooltip("Vor dem aktiven Bewegungsblock die Kopfbewegung mit Soll- und Ist-Marker üben.")]
        private bool trainHeadMovement = true;

        [SerializeField, Range(2, 10)]
        [Tooltip("So viele passende Übungsschwenks hintereinander, abwechselnd rechts und links zuerst, sind für den aktiven Block nötig.")]
        private int requiredGoodTrainingSweeps = 4;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Maximaler zeitgewichteter mittlerer Winkelfehler zwischen Kopfbewegung und Sinusbahn. Gilt im Training und bei aktiven Haupttrials.")]
        private float maximumProfileErrorDegrees = 0.9f;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Maximaler Fehler an den beiden Soll-Umkehrpositionen im Training.")]
        private float maximumTrainingEndpointErrorDegrees = 0.9f;

        [FormerlySerializedAs("headTrackedCoverageYawDegrees")]
        [SerializeField, Range(3f, 60f)]
        [Tooltip("Bis zu dieser Kopfrotation je Seite enthält die Punktwelt einen Sicherheitsbereich. Der gültige Zielschwenk bleibt deutlich kleiner.")]
        private float headTurnSafetyDegrees = 15f;

        [FormerlySerializedAs("validateHeadTrackedMotion")]
        [SerializeField]
        [Tooltip("Ungenügende, zu schnelle oder zu große aktive Kopfbewegungen werden gespeichert und später wiederholt.")]
        private bool checkHeadMotion = true;

        [SerializeField]
        [Tooltip("Aktive Haupttrials sind frei: links/rechts, oben/unten und ohne Solltempo. " +
            "Keine Wiederholung wegen Sinusbahn, Wendepunkten oder Geschwindigkeit. " +
            "Blickkontrolle bleibt unabhängig aktiv. Das geführte Kopftraining bleibt erhalten.")]
        private bool freeHeadMovement = true;

        [SerializeField]
        [Tooltip("Nur mit Free Head Movement: Die Punkte bleiben sichtbar, bis die Person antwortet. " +
            "Kein Zeitlimit, keine Richtung und kein Tempo vorgegeben; Head Tracked Seconds gilt dann nicht. " +
            "Nach der Antwort kommt das graue Feld (Pause Seconds), dann der nächste Trial.")]
        private bool openEndedActiveTrials = true;

        [Header("Simulated Panning Practice")]
        [SerializeField]
        [Tooltip("Vor einem simulierten Bewegungsblock eigene Übungstrials ohne Kopfbewegungs-Sinus.")]
        private bool trainSimulatedMotion = true;

        [SerializeField]
        private List<float> simulatedTrainingKValues = new() { 0.2f, 1.2f };

        [SerializeField, Min(1)]
        private int simulatedTrainingRepeatsPerValue = 2;

        [SerializeField]
        [Tooltip("Vor den Übungstrials je ein beschriftetes Beispiel für KONVEX und KONKAV, wie beim " +
            "Checkerboard: erst länger (Example Long Seconds), dann noch einmal so kurz wie in der " +
            "Messung (Simulated Sweep Seconds). Danach Übung ohne Hinweise.")]
        private bool showSimulatedExamples = true;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("k des Beispiels für KONVEX.")]
        private float exampleConvexK = 1.2f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("k des Beispiels für KONKAV.")]
        private float exampleConcaveK = 0.2f;

        [SerializeField, Range(0.5f, 10f)]
        [Tooltip("Erste, längere Darbietung jedes Beispiels in Sekunden. Gleiche Geschwindigkeit wie " +
            "in der Messung, nur länger, also ein weiterer Schwenk.")]
        private float exampleLongSeconds = 2.5f;

        [SerializeField, Range(0.2f, 10f)]
        [Tooltip("Wie lange der Hinweistext vor jeder Beispiel-Darbietung im Headset steht, in Sekunden.")]
        private float exampleTextSeconds = 3f;

        [SerializeField]
        [Tooltip("Nach jeder Übungsantwort kurz anzeigen, ob sie zum Beispiel passte. Nur bei eindeutigen " +
            "Extremwerten (Grenzen unten); für k dazwischen gibt es bewusst keine Rückmeldung, " +
            "damit keine persönliche Grenze antrainiert wird. Gilt nicht in der Messung. " +
            "Standardmäßig aus: die beschrifteten Beispiele ersetzen sie.")]
        private bool simulatedTrainingFeedback;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Übungs-k bis zu diesem Wert gilt eindeutig als KONKAV.")]
        private float feedbackConcaveMaxK = 0.3f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Übungs-k ab diesem Wert gilt eindeutig als KONVEX.")]
        private float feedbackConvexMinK = 1.1f;

        [SerializeField, Range(0.2f, 5f)]
        [Tooltip("Wie lange die Rückmeldung im Headset steht, in Sekunden.")]
        private float feedbackSeconds = 1.5f;

        [FormerlySerializedAs("headTrackedTurnaroundThresholdDegrees")]
        [SerializeField, Range(0.5f, 30f)]
        [Tooltip("Diese Auslenkung muss auf beiden Seiten erreicht werden. Bei einer Zielamplitude von 2 Grad sind 1,5 Grad ein robuster Umkehrpunkt.")]
        private float turnaroundDegrees = 1.5f;

        [FormerlySerializedAs("requiredHeadSweepAlternations")]
        [SerializeField, Range(1, 10)]
        [Tooltip("Wie viele vollständige Wechsel zwischen den beiden Umkehrpunkten mindestens vorkommen müssen.")]
        private int requiredSweeps = 1;

        [FormerlySerializedAs("maximumValidHeadYawDegrees")]
        [SerializeField, Range(0f, 30f)]
        [Tooltip("So weit darf der Kopf höchstens zur Seite gedreht werden, in Grad. Wer weiter dreht, dessen Durchgang zählt nicht.")]
        private float maxHeadTurnDegrees = 4f;

        [FormerlySerializedAs("minimumMeanHeadSpeedDegreesPerSecond")]
        [SerializeField, Range(0f, 20f)]
        [Tooltip("So schnell muss der Kopf im Schnitt mindestens bewegt werden, in Grad pro Sekunde. Darunter war die Bewegung zu langsam.")]
        private float minMeanHeadSpeed = 0.5f;

        [FormerlySerializedAs("maximumPeakHeadSpeedDegreesPerSecond")]
        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("So schnell darf der Kopf in der Spitze höchstens werden, in Grad pro Sekunde. Darüber war die Bewegung ein Ruck.")]
        private float maxPeakHeadSpeed = 2.5f;

        [Header("Fixation And Repeats")]
        [SerializeField]
        [Tooltip("Die Punkte kommen erst, wenn der Blick ruhig auf dem Kreuz liegt, und werden dabei auch weiter überwacht. Für eine echte Messung muss das an sein.")]
        private bool requireFixation = true;

        [FormerlySerializedAs("maximumOffTargetSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("So lange darf die Person am Stück vom Kreuz wegschauen. Danach wird der Durchgang ungültig.")]
        private float maxLookAwaySeconds = 0.15f;

        [FormerlySerializedAs("maximumInvalidGazeSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("So lange darf am Stück gar kein brauchbarer Blickwert kommen, zum Beispiel beim Blinzeln.")]
        private float maxNoDataSeconds = 0.2f;

        [FormerlySerializedAs("maximumGazeSampleAgeSeconds")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Ist der letzte Blickwert älter als das hier, gilt er als nicht mehr aktuell und zählt wie gar kein Wert.")]
        private float maxSampleAgeSeconds = 0.1f;

        [FormerlySerializedAs("maximumAttemptsPerTrial")]
        [SerializeField, Min(0)]
        [Tooltip("Wie oft derselbe Durchgang höchstens wiederholt werden darf. 0 heißt: so lange, bis es klappt.")]
        private int maxRepeatsPerTrial;

        [Header("Timing")]
        [FormerlySerializedAs("interTrialSeconds")]
        [SerializeField, Min(0f)]
        private float pauseSeconds = 0.25f;

        [Header("Keys")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        [Tooltip("Startet im Ruhezustand die jeweils andere Bewegungsart als separate Sitzung.")]
        private Key startOtherMotionSessionKey = Key.F7;

        [SerializeField]
        private Key abortSessionKey = Key.F6;

        [SerializeField]
        private Key trainingKey = Key.T;

        [SerializeField]
        [Tooltip("Reine Live-Vorschau ohne Sitzung, Training oder Datenaufzeichnung ein/aus.")]
        private Key previewKey = Key.P;

        // Stand vor dem ersten "Vorschau-Werte übernehmen". Leer heißt: es wurde
        // nichts übernommen. "Originalwerte wiederherstellen" holt ihn zurück.
        [SerializeField, HideInInspector]
        private string valuesBeforePreviewTakeover = string.Empty;

        [Header("Runtime Status (Read Only)")]
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
        private int trainedSimulatedMotionBlock;
        private int instructedActiveMotionBlock;
        private Coroutine simulatedTrainingCoroutine;

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
            or State.SimulatedTrainingResponse;
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
            Keyboard keyboard = Keyboard.current;
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
                else if (sessionState is State.HeadTrainingInstructions or State.SimulatedTrainingInstructions)
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

            if (sessionState == State.WaitingForFixation
                && fixationMonitor != null && fixationMonitor.IsReadyForPresentation(maxSampleAgeSeconds))
            {
                PresentCurrentTrial();
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
            if (sessionMotionMode == RandomDotMotionMode.HeadTracked)
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
                    repeatsPerCondition, randomSeed, dotSeedBase, simulatedSweepAxis);
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
            currentMotionBlock = 0;
            currentMiniBlock = 0;
            trainedHeadMotionBlock = 0;
            trainedSimulatedMotionBlock = 0;
            instructedActiveMotionBlock = 0;
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
                BuildResponseLines(), ResponseInputController.GetReadableKeyName(startSessionKey),
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

                if (!autoStartOnPlay && next.MotionMode == RandomDotMotionMode.HeadTracked
                    && instructedActiveMotionBlock != next.MotionBlockIndex && stimulus.Observer != null)
                {
                    headTrainingView ??= new RandomDotHeadSweepTrainingView(stimulus.Observer);
                    stimulus.ShowFixationOnly();
                    sessionState = State.ActiveMotionInstructions;
                    headTrainingView.ShowActiveMotionInstructions(freeHeadMovement,
                        IsOpenEndedTrial(next), BuildResponseLines(), ResponseInputController.GetReadableKeyName(startSessionKey));
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
                yield return new WaitForSecondsRealtime(exampleTextSeconds);
                yield return PresentSimulatedExample(example, exampleLongSeconds);

                headTrainingView.ShowSimulatedExample(label, string.Format(CultureInfo.InvariantCulture,
                    "The same example again,\nas briefly as in the experiment ({0:0.0#} s).",
                    simulatedSweepSeconds));
                yield return new WaitForSecondsRealtime(exampleTextSeconds);
                yield return PresentSimulatedExample(example, simulatedSweepSeconds);
                TrainingExamplesShown++;
            }

            headTrainingView.ShowSimulatedPracticeStart(simulatedTrainingFeedback);
            yield return new WaitForSecondsRealtime(exampleTextSeconds);
        }

        private IEnumerator PresentSimulatedExample(RandomDotTrial example, float seconds)
        {
            headTrainingView.Hide();
            ConfigureStimulus(example, seconds);
            stimulus.ShowFixationOnly();
            yield return new WaitForSecondsRealtime(Mathf.Max(0.25f, pauseSeconds));
            stimulus.RestartMotionPhase();
            stimulus.Show();
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
            headTrainingView.ShowSimulatedInstructions(BuildResponseLines(),
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
                stimulus.Show();
                yield return new WaitForSecondsRealtime(simulatedSweepSeconds);
                stimulus.ShowFixationOnly();
                sessionState = State.SimulatedTrainingResponse;
                headTrainingView.ShowSimulatedResponse(index + 1, practice.Count, BuildResponseLines());
                while (!trainingResponseReceived) yield return null;

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
                    yield return new WaitForSecondsRealtime(feedbackSeconds);
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
                PresentCurrentTrial();
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

        private void PresentCurrentTrial()
        {
            // Gibt k, m, Zusatzzoom, FOV, Augenmodus und Bewegungsart an das
            // Punktfeld weiter und lässt die Bewegung von vorne losgehen.
            if (currentTrial == null)
            {
                return;
            }

            ResetFixationChecks();
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
            sessionState = State.PresentingMotion;

            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show();
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

            if (answeredWhileVisible)
            {
                stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
                WriteMarker(
                    "StimulusEnded;task=random_dot_instrument;sequence={0};attempt={1};duration_s={2:F4};" +
                    "ended_by=response",
                    currentTrial.SequenceIndex, currentTrial.AttemptNumber,
                    stimulusEndUnitySeconds - trialStartUnitySeconds);
            }

            RandomDotTrialResult result = CaptureCurrentResult(response, validForAnalysis: true, "valid");
            if (!TryAppendResult(result))
            {
                return;
            }

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
                status, IsFreeHeadTrial(currentTrial));
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
            WriteMarker("SessionCompleted;task=random_dot_instrument;valid_trials={0};presentations={1}",
                validTrialsCompleted, presentationCount);
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

        private bool IsOpenEndedTrial(RandomDotTrial trial) =>
            IsFreeHeadTrial(trial) && openEndedActiveTrials;

        private bool OpenEndedSession =>
            sessionMotionMode == RandomDotMotionMode.HeadTracked && freeHeadMovement && openEndedActiveTrials;

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
            trial.MotionMode == RandomDotMotionMode.HeadTracked ? headTrackedSeconds : simulatedSweepSeconds;

        private float WorldCoverageFor(RandomDotTrial trial, float? simulatedSecondsOverride = null)
        {
            // Wie groß die Punktwelt für diesen Trial sein muss. Im HeadTracked-Block
            // kann der Kopf weiter drehen als der Sollschwenk. Dafür gibt es dort den
            // größeren Sicherheitsbereich.
            float reach = trial.MotionMode == RandomDotMotionMode.HeadTracked
                ? Mathf.Max(sweepAmplitudeDegrees, headTurnSafetyDegrees)
                : SweepAmplitude(trial, simulatedSecondsOverride);
            return RandomDotFieldStimulus.CoverageNeeded(trial.AngularDiameterDegrees, trial.ContentZoom,
                trial.InstrumentMagnificationM, trial.InstrumentDistortionK, reach);
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
            StopAndClear(ref motionCoroutine);
            StopAndClear(ref headTrainingCoroutine);
            StopAndClear(ref interTrialCoroutine);
        }

        private void DisposeTrainingView()
        {
            headTrainingView?.Dispose();
            headTrainingView = null;
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
