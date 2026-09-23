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
        Welcome,
        TrainingInstructions,
        TrainingFixation,
        TrainingExample,
        TrainingNoise,
        TrainingWaitingForResponse,
        TrainingComplete,
        WaitingForExperimentReady,
        InterTrial,
        WaitingForFixation,
        RunningTrial,
        WaitingForResponse,
        Completed,
        Aborted
    }

    /// <summary>
    /// Steuert den ganzen Checkerboard-Versuch.
    ///
    /// So läuft ein Durchgang ab: Die Person schaut auf das Kreuz. Liegt der Blick
    /// ruhig genug, kommt kurz das Schachbrett. Direkt danach die Noise-Maske, und
    /// während die zu sehen ist, drückt die Person Category A oder B.
    ///
    /// Welches l gezeigt wird, steht vorher fest. Die Person kann daran nichts
    /// verändern.
    ///
    /// Vor dem richtigen Versuch kann man dieselben Schritte erst einmal üben.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class CheckerboardExperimentManager : MonoBehaviour
    {
        // Hier läuft alles zusammen. Der Weg durch die Datei ist ungefähr dieser:
        //
        // StartSession           baut den gemischten Plan und speichert ihn.
        // BeginNextAttempt       holt den nächsten Durchgang aus der Warteschlange.
        // PresentCurrentTrial    gibt die Werte an den Stimulus weiter und zeigt ihn.
        // MonitorFixationDuringTrial  schaut nebenher, ob der Blick liegen bleibt.
        //
        // Danach wird die Antwort gespeichert. Hat die Person danebengeschaut,
        // kommt derselbe Durchgang ganz hinten noch einmal in die Warteschlange.
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
        [Tooltip("Kennung der Versuchsperson. Hier gehören keine echten Namen rein, sondern zum Beispiel pilot_001.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "checkerboard_pilot";

        [SerializeField]
        [Tooltip("Mit derselben Zahl und denselben Einstellungen kommt wieder genau dieselbe Reihenfolge heraus.")]
        private int randomSeed = 20260901;

        [SerializeField]
        [Tooltip("Wohin die Messdaten kommen. Leer lassen ist normal, dann landen sie im Ordner measurements im Projekt.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Trialplan")]
        [SerializeField]
        [Tooltip("Wie groß der runde Ausschnitt sein soll, in Grad. Es können auch mehrere Werte drinstehen, dann wird jeder davon gezeigt.")]
        private List<float> angularDiametersDegrees = new() { 90f };

        [SerializeField]
        [Tooltip("Auf welchen Augen gezeigt wird: beide, nur links oder nur rechts.")]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [SerializeField]
        [Tooltip("Alle l-Werte, die gezeigt werden sollen. l = 1 ist gerade, l = 0,5 ist der Helmholtz-Punkt. Das hier sind nur Pilotwerte, die Liste darf komplett geändert werden.")]
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
        [Tooltip("Wie oft jede Kombination gezeigt wird. Mehr Wiederholungen heißt sicherere Ergebnisse, aber auch eine längere Sitzung.")]
        private int repetitionsPerCondition = 3;

        [Header("Fixation und Wiederholung")]
        [SerializeField]
        [Tooltip("Das Muster kommt erst, wenn der Blick ruhig auf dem Kreuz liegt, und wird dabei auch weiter überwacht. Für eine echte Messung muss das an sein.")]
        private bool requireFixation = true;

        [SerializeField, Min(0f)]
        [Tooltip("So lange darf die Person am Stück vom Kreuz wegschauen. Danach wird der Durchgang ungültig.")]
        private float maximumOffTargetSeconds = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("So lange darf am Stück gar kein brauchbarer Blickwert kommen, zum Beispiel beim Blinzeln.")]
        private float maximumInvalidGazeSeconds = 0.2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Ist der letzte Blickwert älter als das hier, gilt er als nicht mehr aktuell und zählt wie gar kein Wert.")]
        private float maximumGazeSampleAgeSeconds = 0.1f;

        [SerializeField, Min(0)]
        [Tooltip("Wie oft derselbe Durchgang höchstens wiederholt werden darf. 0 heißt: so lange, bis es klappt.")]
        private int maximumAttemptsPerTrial;

        [Header("Ablauf")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Wie lange das Schachbrett zu sehen ist. 0,6 sind 600 Millisekunden.")]
        private float stimulusDurationSeconds = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Wie lange die Person ab der Noise-Maske Zeit zum Antworten hat. 0 heißt: unbegrenzt.")]
        private float responseTimeoutSeconds = 5f;

        [SerializeField, Min(0f)]
        [Tooltip("Zusätzliche Noise-Maske nach der Antwort. Normalerweise 0 lassen, dann beginnt direkt die Fixation für den nächsten Durchgang.")]
        private float postResponseNoiseSeconds;

        [Header("Antwortkategorien und Tasten")]
        [SerializeField]
        private Key convexResponseKey = Key.UpArrow;

        [SerializeField]
        private Key concaveResponseKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Nimmt im Headset Trigger und Trackpad an. Die Pfeiltasten funktionieren zusätzlich weiter zum Testen am Laptop.")]
        private bool useVrControllerButtons = true;

        [SerializeField]
        [Tooltip("Aus: Trigger = nach außen. An: Trigger = nach innen. Vor einer Sitzung einstellen und währenddessen nicht ändern.")]
        private bool swapResponseButtons;

        [SerializeField]
        [Tooltip("Wie diese Antwort der Person genannt wird. Gespeichert wird sie im Code und in der CSV weiter als Convex.")]
        private string categoryAResponseText = "CATEGORY A";

        [SerializeField]
        [Tooltip("Wie diese Antwort der Person genannt wird. Gespeichert wird sie im Code und in der CSV weiter als Concave.")]
        private string categoryBResponseText = "CATEGORY B";

        [Header("Welcome und Training")]
        [SerializeField]
        [Tooltip("Taste, mit der vom Startbildschirm aus das Training losgeht.")]
        private Key trainingKey = Key.T;

        [SerializeField]
        [Tooltip("Taste zum Weiterblättern im Training. Damit startet später auch der erste Durchgang.")]
        private Key continueTrainingKey = Key.Space;

        [SerializeField]
        [Tooltip("Ist das an, geht der Versuch erst los, wenn das Training einmal komplett durchlaufen wurde.")]
        private bool requireTrainingBeforeSession = true;

        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Das deutliche Beispiel für Category A im Training. Der Wert sollte klar zu erkennen sein und trotzdem im Bereich liegen, der später im Versuch vorkommt.")]
        private float trainingCategoryAVisualSpaceL = 1.2f;

        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Das deutliche Beispiel für Category B im Training. Der Wert sollte klar zu erkennen sein und trotzdem im Bereich liegen, der später im Versuch vorkommt.")]
        private float trainingCategoryBVisualSpaceL = 0.2f;

        [SerializeField]
        [Tooltip("Die l-Werte für die Übungsdurchgänge. Jeder kommt gleich oft dran, und es gibt keine Rückmeldung, ob die Antwort stimmte.")]
        private List<float> trainingVisualSpaceLValues = new()
        {
            0.2f,
            0.4f,
            0.8f,
            1.2f
        };

        [SerializeField, Min(1)]
        [Tooltip("Wie oft jeder Übungswert im Training drankommt.")]
        private int trainingRepetitionsPerValue = 3;

        [SerializeField, Min(0.1f)]
        [Tooltip("Wie lange die beiden deutlichen Beispiele für Category A und B gezeigt werden. Die normalen Übungsdurchgänge bleiben so kurz wie im richtigen Experiment.")]
        private float trainingExampleStimulusSeconds = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("Wie lange die Noise-Maske nach den beiden Beispielen zu sehen ist.")]
        private float trainingExampleNoiseSeconds = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Wie lange im Training nur das Kreuz gezeigt wird, wenn die Blickkontrolle aus ist. Dann kann ja nicht gemessen werden, ob der Blick ruhig liegt.")]
        private float trainingFixationSecondsWithoutEyeTracking = 0.5f;

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
        private double responseWindowStartUnitySeconds;
        private float currentOffTargetSeconds;
        private float currentInvalidGazeSeconds;
        private float longestOffTargetSeconds;
        private float longestInvalidGazeSeconds;
        private Coroutine interTrialCoroutine;
        private Coroutine presentationCoroutine;
        private Coroutine trainingCoroutine;
        private bool keyboardEventsSubscribed;
        private bool trainingCompleted;
        private bool trainingAdvanceRequested;
        private bool trainingResponseReceived;
        private bool fixationSnapshotAvailable;
        private bool fixationSampleValidAtTrialEnd;
        private bool fixationInsideAtTrialEnd;
        private float fixationAngleAtTrialEnd = float.NaN;
        private float continuousFixationAtTrialEnd;
        private float fixationValidFractionAtTrialEnd = float.NaN;

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
        public bool TrainingCompleted => trainingCompleted;
        public bool ResponseKeysSwapped =>
            keyboardController != null && keyboardController.SwapResponseKeys;
        public string ConvexResponseKeyName => keyboardController != null
            ? keyboardController.GetResponseControlName(
                CheckerboardCurvatureResponse.Convex)
            : "–";
        public string ConcaveResponseKeyName => keyboardController != null
            ? keyboardController.GetResponseControlName(
                CheckerboardCurvatureResponse.Concave)
            : "–";
        public bool IsSessionActive =>
            sessionState == CheckerboardSessionState.WaitingForExperimentReady ||
            sessionState == CheckerboardSessionState.InterTrial ||
            sessionState == CheckerboardSessionState.WaitingForFixation ||
            sessionState == CheckerboardSessionState.RunningTrial ||
            sessionState == CheckerboardSessionState.WaitingForResponse;
        public bool IsTrainingActive =>
            sessionState == CheckerboardSessionState.TrainingInstructions ||
            sessionState == CheckerboardSessionState.TrainingFixation ||
            sessionState == CheckerboardSessionState.TrainingExample ||
            sessionState == CheckerboardSessionState.TrainingNoise ||
            sessionState == CheckerboardSessionState.TrainingWaitingForResponse ||
            sessionState == CheckerboardSessionState.TrainingComplete;

        private void Awake()
        {
            // Möglichst früh suchen, damit später beim Start alles da ist.
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
                // Auto Start ist nur zum schnellen Ausprobieren da. Begrüßung und
                // Training werden dann übersprungen. Für eine echte Messung darf
                // das nicht an sein.
                trainingCompleted = true;
                StartSession();
                return;
            }

            ShowWelcomeScreen();
        }

        private void Update()
        {
            // Hier werden nur die Tasten vom Versuchsleiter abgefragt: starten,
            // trainieren, abbrechen, weiter. Die Antworttasten A und B laufen
            // weiter über den Keyboard Controller.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (IsTrainingActive)
                {
                    if (keyboard[abortSessionKey].wasPressedThisFrame)
                    {
                        StopTrainingAndReturnToWelcome();
                        return;
                    }

                    if (keyboard[continueTrainingKey].wasPressedThisFrame)
                    {
                        trainingAdvanceRequested = true;
                    }
                }
                else if (!IsSessionActive &&
                         keyboard[trainingKey].wasPressedThisFrame)
                {
                    StartTraining();
                    return;
                }
                else if (!IsSessionActive &&
                         keyboard[startSessionKey].wasPressedThisFrame)
                {
                    StartSession();
                    return;
                }

                if (IsSessionActive && keyboard[abortSessionKey].wasPressedThisFrame)
                {
                    AbortSession("ManualAbort");
                    return;
                }

                if (sessionState == CheckerboardSessionState.WaitingForExperimentReady &&
                    keyboard[continueTrainingKey].wasPressedThisFrame)
                {
                    WriteEyeTrackingMarker("ExperimentReadyConfirmed");
                    sessionState = CheckerboardSessionState.InterTrial;
                    BeginNextAttempt();
                    return;
                }
            }

            if (sessionState == CheckerboardSessionState.WaitingForFixation &&
                fixationMonitor != null && fixationMonitor.RequirementMet)
            {
                PresentCurrentTrial();
                return;
            }

            if ((sessionState == CheckerboardSessionState.RunningTrial ||
                 sessionState == CheckerboardSessionState.WaitingForResponse) &&
                requireFixation)
            {
                MonitorFixationDuringTrial();
            }
        }

        private void OnDisable()
        {
            UnsubscribeKeyboardEvents();
            StopTrainingCoroutine();
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

        public void ShowWelcomeScreen()
        {
            ShowWelcomeScreen(string.Empty);
        }

        private void ShowWelcomeScreen(string notice)
        {
            // Der Startbildschirm ist der ruhige Punkt, an dem nichts läuft. Von
            // hier geht es ins Training oder in den Versuch.
            //
            // Hier wird noch keine Datei angelegt und noch nichts aufgezeichnet.
            ResolveReferences();
            if (stimulus == null || keyboardController == null)
            {
                return;
            }

            ApplyResponseKeySettings();
            sessionState = CheckerboardSessionState.Welcome;
            stimulus.ShowResponsePrompt(BuildWelcomePrompt(notice));
        }

        public bool StartTraining()
        {
            if (IsSessionActive || IsTrainingActive)
            {
                return false;
            }

            ResolveReferences();
            SubscribeKeyboardEvents();
            if (stimulus == null || keyboardController == null)
            {
                Debug.LogError(
                    "Stimulus und Checkerboard Keyboard Controller werden für das Training benötigt.",
                    this);
                return false;
            }

            if (requireFixation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Das Training verlangt Fixation, aber der Fixation Monitor fehlt.",
                    this);
                return false;
            }

            ApplyResponseKeySettings();
            StopTrainingCoroutine();
            StopPendingInterTrial();
            StopPresentationCoroutine();
            trainingCompleted = false;
            trainingAdvanceRequested = false;
            trainingResponseReceived = false;
            currentTrial = null;
            trainingCoroutine = StartCoroutine(RunTrainingSequence());
            return true;
        }

        public void StopTrainingAndReturnToWelcome()
        {
            if (!IsTrainingActive)
            {
                return;
            }

            StopTrainingCoroutine();
            ShowWelcomeScreen("PRACTICE STOPPED");
        }

        private IEnumerator RunTrainingSequence()
        {
            sessionState = CheckerboardSessionState.TrainingInstructions;
            stimulus.ShowResponsePrompt(
                "PRACTICE\n\n" +
                "Keep looking at the cross.\n" +
                "First you will see both categories.\n" +
                "Then you can practise.\n\n" +
                BuildResponsePrompt() + "\n\n" +
                CheckerboardKeyboardController.GetReadableKeyName(continueTrainingKey) +
                " = CONTINUE");
            yield return WaitForTrainingAdvance();

            // Zuerst kommen zwei ganz deutliche Beispiele. Die zeigen der Person,
            // was mit Category A und was mit Category B gemeint ist.
            //
            // Danach folgen die gemischten Übungsdurchgänge, und da steht dann kein
            // Hinweis mehr dabei. Es gibt auch keine Rückmeldung, ob eine Antwort
            // richtig war. Das ist Absicht, sonst würde man das Ergebnis verfälschen.
            var examples = new[]
            {
                CheckerboardCurvatureResponse.Convex,
                CheckerboardCurvatureResponse.Concave
            };
            int presentationIndex = 0;
            foreach (CheckerboardCurvatureResponse example in examples)
            {
                sessionState = CheckerboardSessionState.TrainingInstructions;
                stimulus.ShowResponsePrompt(
                    BuildTrainingCategoryIntroduction(example));
                yield return WaitForTrainingAdvance();

                presentationIndex++;
                float exampleL = example == CheckerboardCurvatureResponse.Convex
                    ? trainingCategoryAVisualSpaceL
                    : trainingCategoryBVisualSpaceL;
                yield return ShowTrainingPattern(
                    exampleL,
                    unchecked(randomSeed + 50000 + presentationIndex),
                    leaveNoiseVisible: false,
                    patternDurationSeconds: trainingExampleStimulusSeconds);
                if (!IsTrainingActive)
                {
                    yield break;
                }

                sessionState = CheckerboardSessionState.TrainingInstructions;
                stimulus.ShowResponsePrompt(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}\n{1}\n\n{2} = CONTINUE",
                    GetCategoryLabel(example),
                    GetCategoryDescription(example),
                    CheckerboardKeyboardController.GetReadableKeyName(
                        continueTrainingKey)));
                yield return WaitForTrainingAdvance();
            }

            sessionState = CheckerboardSessionState.TrainingInstructions;
            stimulus.ShowResponsePrompt(string.Format(
                CultureInfo.InvariantCulture,
                "PRACTICE TRIALS\n\n" +
                "Each pattern lasts {0:0} ms.\n" +
                "Answer while the noise is visible.\n" +
                "Keep looking at the cross.\n\n" +
                "{1} = CONTINUE",
                stimulusDurationSeconds * 1000f,
                CheckerboardKeyboardController.GetReadableKeyName(
                    continueTrainingKey)));
            yield return WaitForTrainingAdvance();

            List<float> practiceTrials = BuildTrainingLOrder(
                trainingVisualSpaceLValues,
                trainingRepetitionsPerValue,
                unchecked(randomSeed ^ 0x51F15EED));
            foreach (float practiceL in practiceTrials)
            {
                presentationIndex++;
                yield return ShowTrainingPattern(
                    practiceL,
                    unchecked(randomSeed + 60000 + presentationIndex),
                    leaveNoiseVisible: true,
                    patternDurationSeconds: stimulusDurationSeconds);
                if (!IsTrainingActive)
                {
                    yield break;
                }

                trainingResponseReceived = false;
                sessionState = CheckerboardSessionState.TrainingWaitingForResponse;

                double responseDeadline = responseTimeoutSeconds > 0f
                    ? Time.realtimeSinceStartupAsDouble + responseTimeoutSeconds
                    : double.PositiveInfinity;
                while (IsTrainingActive &&
                       !trainingResponseReceived &&
                       Time.realtimeSinceStartupAsDouble < responseDeadline)
                {
                    yield return null;
                }

                if (postResponseNoiseSeconds > 0f)
                {
                    int postResponseNoiseSeed = unchecked(
                        randomSeed + 70000 + presentationIndex * 1879);
                    stimulus.ShowNoise(postResponseNoiseSeed);
                    yield return WaitForTrainingSeconds(postResponseNoiseSeconds);
                }
            }

            trainingCompleted = true;
            sessionState = CheckerboardSessionState.TrainingComplete;
            stimulus.ShowResponsePrompt(
                "PRACTICE COMPLETE\n\n" +
                CheckerboardKeyboardController.GetReadableKeyName(continueTrainingKey) +
                " = RETURN TO WELCOME");
            yield return WaitForTrainingAdvance();

            trainingCoroutine = null;
            ShowWelcomeScreen();
        }

        private IEnumerator ShowTrainingPattern(
            float visualSpaceL,
            int noiseSeed,
            bool leaveNoiseVisible,
            float patternDurationSeconds)
        {
            PrepareTrainingCondition(visualSpaceL);
            fixationMonitor?.ResetFixationWindow();
            sessionState = CheckerboardSessionState.TrainingFixation;
            stimulus.ShowFixationOnly();

            if (requireFixation)
            {
                while (IsTrainingActive &&
                       fixationMonitor != null &&
                       !fixationMonitor.RequirementMet)
                {
                    yield return null;
                }
            }
            else if (trainingFixationSecondsWithoutEyeTracking > 0f)
            {
                yield return WaitForTrainingSeconds(
                    trainingFixationSecondsWithoutEyeTracking);
            }

            if (!IsTrainingActive)
            {
                yield break;
            }

            sessionState = CheckerboardSessionState.TrainingExample;
            stimulus.Show();
            yield return WaitForTrainingSeconds(patternDurationSeconds);
            if (!IsTrainingActive)
            {
                yield break;
            }

            sessionState = CheckerboardSessionState.TrainingNoise;
            stimulus.ShowNoise(noiseSeed);
            if (!leaveNoiseVisible && trainingExampleNoiseSeconds > 0f)
            {
                yield return WaitForTrainingSeconds(trainingExampleNoiseSeconds);
            }
        }

        private IEnumerator WaitForTrainingAdvance()
        {
            trainingAdvanceRequested = false;
            while (IsTrainingActive && !trainingAdvanceRequested)
            {
                yield return null;
            }

            trainingAdvanceRequested = false;
        }

        private IEnumerator WaitForTrainingSeconds(float seconds)
        {
            double endTime = Time.realtimeSinceStartupAsDouble +
                Mathf.Max(0f, seconds);
            while (IsTrainingActive && Time.realtimeSinceStartupAsDouble < endTime)
            {
                yield return null;
            }
        }

        private void PrepareTrainingCondition(float visualSpaceL)
        {
            // Im Training soll alles genauso aussehen wie später im Versuch.
            // Deshalb werden FOV, Augenmodus und Zoom aus der ersten echten
            // Bedingung übernommen. Nur l wird durch den Übungswert ersetzt.
            if (angularDiametersDegrees != null && angularDiametersDegrees.Count > 0)
            {
                stimulus.SetAngularDiameter(angularDiametersDegrees[0]);
            }

            if (eyePresentations != null && eyePresentations.Count > 0)
            {
                stimulus.SetEyePresentation(eyePresentations[0]);
            }

            stimulus.SetVisualSpaceL(visualSpaceL);
        }

        private List<float> BuildTrainingLOrder(
            IReadOnlyList<float> values,
            int repetitionsPerValue,
            int seed)
        {
            var order = new List<float>();
            for (int repetition = 0;
                 repetition < Mathf.Max(1, repetitionsPerValue);
                 repetition++)
            {
                if (values == null)
                {
                    continue;
                }

                for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
                {
                    order.Add(Mathf.Clamp(values[valueIndex], 0f, 1.4f));
                }
            }

            var random = new System.Random(seed);
            for (int index = order.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                float temporary = order[index];
                order[index] = order[swapIndex];
                order[swapIndex] = temporary;
            }

            return order;
        }

        private string BuildTrainingCategoryIntroduction(
            CheckerboardCurvatureResponse response)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}\n{1}\n\n" +
                "Example: {2:0.#} seconds\n\n" +
                "{3} = SHOW",
                GetCategoryLabel(response),
                GetCategoryDescription(response),
                trainingExampleStimulusSeconds,
                CheckerboardKeyboardController.GetReadableKeyName(
                    continueTrainingKey));
        }

        private string GetCategoryDescription(CheckerboardCurvatureResponse response)
        {
            return response == CheckerboardCurvatureResponse.Convex
                ? "CURVES OUTWARD"
                : "CURVES INWARD";
        }

        private string GetCategoryLabel(CheckerboardCurvatureResponse response)
        {
            return response == CheckerboardCurvatureResponse.Convex
                ? categoryAResponseText
                : categoryBResponseText;
        }

        public bool StartSession()
        {
            // Das ist der große Startknopf. Der Reihe nach passiert hier:
            // prüfen, ob in der Szene alles da ist -> alle Durchgänge bauen und
            // mischen -> den Messordner anlegen -> Eye Tracking starten ->
            // den ersten Durchgang zeigen.
            if (IsSessionActive || IsTrainingActive)
            {
                Debug.LogWarning(
                    "Eine Checkerboard-Sitzung oder ein Training läuft bereits.",
                    this);
                return false;
            }

            if (requireTrainingBeforeSession && !trainingCompleted)
            {
                ShowWelcomeScreen("PLEASE COMPLETE THE PRACTICE FIRST");
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

            // Die Tastenbelegung wird mit Absicht hier gesetzt und nicht im
            // Keyboard Controller. So gilt sie auch in älteren Szenen, in denen
            // am Controller noch die alten Tasten eingetragen sind.
            ApplyResponseKeySettings();

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
                experimentFiles.WritePlan(
                    trialPlan,
                    stimulus.GridLineSpacingDegrees,
                    stimulusDurationSeconds,
                    postResponseNoiseSeconds,
                    responseTimeoutSeconds,
                    ConvexResponseKeyName,
                    ConcaveResponseKeyName,
                    ResponseKeysSwapped);
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
            sessionState = CheckerboardSessionState.WaitingForExperimentReady;

            Debug.Log(
                $"Checkerboard-Sitzung vorbereitet: {totalTrials} gültige Trials geplant, Seed {randomSeed}.\n" +
                activeSessionFolder,
                this);
            stimulus.ShowResponsePrompt(
                "MAIN EXPERIMENT\n\n" +
                "Keep looking at the cross.\n\n" +
                CheckerboardKeyboardController.GetReadableKeyName(
                    continueTrainingKey) + " = START");
            WriteEyeTrackingMarker("ExperimentReadyScreenShown");
            return true;
        }

        public void AbortSession(string reason = "ManualAbort")
        {
            // Beim Abbrechen geht nichts verloren. Alles, was schon in der Datei
            // steht, bleibt stehen. Läuft gerade noch ein Durchgang, wird der extra
            // als abgebrochen vermerkt.
            if (!IsSessionActive)
            {
                return;
            }

            StopPendingInterTrial();
            StopPresentationCoroutine();
            if ((sessionState == CheckerboardSessionState.RunningTrial ||
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
            // Aus der Warteschlange kommt entweder ein neuer Durchgang oder eine
            // Wiederholung, die vorhin hinten angehängt wurde. Beides sieht hier
            // gleich aus. Ist die Warteschlange leer, ist die Sitzung fertig.
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
            // Erst ab hier ist das Muster zu sehen. Vorher lief nur das Warten auf
            // eine ruhige Fixation. Deshalb fangen auch die Zeitmessung und die
            // Blickkontrolle erst jetzt an und nicht schon vorher.
            if (currentTrial == null)
            {
                return;
            }

            fixationMonitor?.ResetFixationWindow();
            ResetTrialFixationCounters();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulusEndUnitySeconds = 0d;
            responseWindowStartUnitySeconds = 0d;
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
                "Versuch {6}. Stimulus={7:F3}s, danach Noise bis zur Antwort.",
                currentTrialNumber,
                totalTrials,
                presentationCount,
                currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees,
                currentTrial.VisualSpaceL,
                currentTrial.AttemptNumber,
                stimulusDurationSeconds),
                this);
        }

        private IEnumerator RunPresentationSequence(CheckerboardTrial presentedTrial)
        {
            // Das Muster ist bei allen Personen exakt gleich lange zu sehen.
            // Sonst könnte man die Antworten hinterher nicht vergleichen.
            yield return new WaitForSecondsRealtime(stimulusDurationSeconds);
            if (sessionState != CheckerboardSessionState.RunningTrial ||
                currentTrial != presentedTrial)
            {
                presentationCoroutine = null;
                yield break;
            }

            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "StimulusEnded;sequence={0};attempt={1};duration_s={2:F4}",
                presentedTrial.SequenceIndex,
                presentedTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds));

            int noiseSeed = unchecked(
                randomSeed +
                presentedTrial.SequenceIndex * 1009 +
                presentedTrial.AttemptNumber * 9176);
            responseWindowStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = CheckerboardSessionState.WaitingForResponse;
            stimulus.ShowNoise(noiseSeed);
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "NoiseMaskStarted;sequence={0};attempt={1};until_response=1;seed={2};timeout_s={3:F4}",
                presentedTrial.SequenceIndex,
                presentedTrial.AttemptNumber,
                noiseSeed,
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
                // Erst die Referenz leeren, dann abbrechen. Die Coroutine ist an
                // dieser Stelle nämlich schon von selbst fertig, und man würde
                // sonst versuchen, etwas zu stoppen, das gar nicht mehr läuft.
                presentationCoroutine = null;
                InvalidateCurrentTrial("response_timeout");
                yield break;
            }

            presentationCoroutine = null;
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            // Im Training zählt der Tastendruck nur als "weiter". Er wird nicht
            // bewertet und landet auch in keiner Messdatei.
            if (sessionState == CheckerboardSessionState.TrainingWaitingForResponse &&
                response != CheckerboardCurvatureResponse.None)
            {
                trainingResponseReceived = true;
                return;
            }

            // Im Versuch wird die Antwort angenommen, solange die Noise-Maske zu
            // sehen ist. Pro Durchgang wird danach genau eine Zeile geschrieben.
            if (sessionState != CheckerboardSessionState.WaitingForResponse ||
                currentTrial == null ||
                response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            StopPresentationCoroutine();
            CaptureFixationAtTrialEnd();

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
            // Zwei Dinge werden getrennt mitgezählt: wie lange die Person am Kreuz
            // vorbeigeschaut hat, und wie lange gar keine Daten da waren.
            //
            // Getrennt deshalb, weil ein Blinzeln etwas anderes ist als wegschauen.
            // Der Durchgang wird nur dann ungültig, wenn eines davon am Stück zu
            // lange dauert. Kurze Aussetzer sind also in Ordnung.
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
            // Der misslungene Versuch wird trotzdem gespeichert, zählt aber nicht als
            // Antwort. Man sieht später in der CSV, dass es ihn gab und warum er
            // nicht gezählt hat.
            //
            // Dieselbe Bedingung kommt danach ganz hinten wieder in die
            // Warteschlange, mit einer höheren Versuchsnummer.
            if ((sessionState != CheckerboardSessionState.RunningTrial &&
                 sessionState != CheckerboardSessionState.WaitingForResponse) ||
                currentTrial == null)
            {
                return;
            }

            StopPresentationCoroutine();
            if (!fixationSnapshotAvailable)
            {
                CaptureFixationAtTrialEnd();
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
            // Hier wird alles zu diesem Durchgang in ein Objekt gepackt: die
            // Bedingung, die Zeiten und die Blickwerte. Dieses Objekt geht danach
            // an die Dateiklasse, die daraus eine CSV-Zeile macht.
            bool sampleValid = fixationSnapshotAvailable
                ? fixationSampleValidAtTrialEnd
                : fixationMonitor != null && fixationMonitor.CurrentSampleValid;
            bool inside = fixationSnapshotAvailable
                ? fixationInsideAtTrialEnd
                : fixationMonitor != null && fixationMonitor.IsInsideTolerance;
            float angle = fixationSnapshotAvailable
                ? fixationAngleAtTrialEnd
                : fixationMonitor != null
                    ? fixationMonitor.CurrentAngleDegrees
                    : float.NaN;
            float continuousSeconds = fixationSnapshotAvailable
                ? continuousFixationAtTrialEnd
                : fixationMonitor != null
                    ? fixationMonitor.ContinuousFixationSeconds
                    : 0f;
            float validSampleFraction = fixationSnapshotAvailable
                ? fixationValidFractionAtTrialEnd
                : fixationMonitor != null
                    ? fixationMonitor.ValidSampleFraction
                    : float.NaN;

            double resultEndTime = Time.realtimeSinceStartupAsDouble;
            double resolvedStimulusEnd = stimulusEndUnitySeconds > trialStartUnitySeconds
                ? stimulusEndUnitySeconds
                : resultEndTime;
            double resolvedResponseWindowStart = responseWindowStartUnitySeconds >= resolvedStimulusEnd
                ? responseWindowStartUnitySeconds
                : resolvedStimulusEnd;

            return new CheckerboardTrialResult(
                currentTrial,
                presentationCount,
                trialStartUtc,
                trialStartUnitySeconds,
                resolvedStimulusEnd,
                resolvedResponseWindowStart,
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
            // Wenn das Schreiben schiefgeht, zum Beispiel weil die Datei noch in
            // Excel offen ist, wird das hier abgefangen und gemeldet. Sonst würde
            // die Messung munter weiterlaufen und am Ende wäre nichts gespeichert.
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
            StopPresentationCoroutine();
            currentTrial = null;
            sessionState = CheckerboardSessionState.InterTrial;

            if (postResponseNoiseSeconds <= 0f)
            {
                BeginNextAttempt();
            }
            else
            {
                // Optional kann nach der Antwort noch ein zweites Noise-Bild
                // stehen bleiben. Für den normalen Ablauf bleibt der Wert bei 0.
                ShowPostResponseNoise();
                interTrialCoroutine = StartCoroutine(BeginNextAttemptAfterDelay());
            }
        }

        private IEnumerator BeginNextAttemptAfterDelay()
        {
            yield return new WaitForSecondsRealtime(postResponseNoiseSeconds);
            BeginNextAttempt();
        }

        private void ShowPostResponseNoise()
        {
            if (stimulus == null)
            {
                return;
            }

            // Ein anderer Seed verhindert, dass nach dem Tastendruck genau das
            // gleiche Noise-Muster bis zum nächsten Durchgang stehen bleibt.
            int postResponseNoiseSeed = unchecked(
                randomSeed + presentationCount * 1879 + 0x4A31);
            stimulus.ShowNoise(postResponseNoiseSeed);
            WriteEyeTrackingMarker(string.Format(
                CultureInfo.InvariantCulture,
                "PostTrialNoiseStarted;presentation={0};duration_s={1:F4};seed={2}",
                presentationCount,
                postResponseNoiseSeconds,
                postResponseNoiseSeed));
        }

        private void CompleteSession()
        {
            // Zum Schluss: Marker in die Aufnahme schreiben, Bild ausblenden und
            // die Eye-Tracking-Aufzeichnung beenden.
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
            // Die Blickdaten selbst schreibt die Lab-Toolbox. Von hier wird die
            // Aufnahme nur an- und ausgeschaltet, und es werden Marker gesetzt.
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
                "stimulus_duration_s={6:F4};noise_until_response=1;post_response_noise_s={7:F4};" +
                "response_timeout_s={8:F4};category_a_key={9};category_b_key={10};response_keys_swapped={11}",
                CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "session"),
                randomSeed,
                trialPlan.Count,
                sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                VisualSpaceRadialMapping.MappingVersion,
                stimulusDurationSeconds,
                postResponseNoiseSeconds,
                responseTimeoutSeconds,
                ConvexResponseKeyName,
                ConcaveResponseKeyName,
                ResponseKeysSwapped ? 1 : 0));
        }

        private void ResolveReferences()
        {
            // Felder, die im Inspector leer geblieben sind, werden hier in der Szene
            // gesucht. Was schon eingetragen ist, wird nicht angefasst.
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

        private void ApplyResponseKeySettings()
        {
            if (keyboardController == null)
            {
                return;
            }

            keyboardController.SetResponseKeys(
                concaveResponseKey,
                convexResponseKey);
            keyboardController.SetVrControllerButtonsEnabled(
                useVrControllerButtons);
            keyboardController.SetSwapResponseKeys(swapResponseButtons);
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

        private void StopTrainingCoroutine()
        {
            if (trainingCoroutine == null)
            {
                return;
            }

            StopCoroutine(trainingCoroutine);
            trainingCoroutine = null;
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
            // Ein Marker ist eine kurze Notiz mitten in der Eye-Tracking-Aufnahme.
            // Damit weiß man beim Auswerten, welcher Blickwert zu welchem Teil des
            // Versuchs gehört, also zum Beispiel zum Muster oder zur Noise-Maske.
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "circular_aperture={8};grid_spacing_deg={9:F3};" +
                "grid_spacing_uv={10:F6};visual_space_l={11:F4};" +
                "stimulus_duration_s={12:F4};noise_until_response=1;post_response_noise_s={13:F4};" +
                "response_timeout_s={14:F4};category_a_key={15};category_b_key={16};response_keys_swapped={17}",
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
                stimulusDurationSeconds,
                postResponseNoiseSeconds,
                responseTimeoutSeconds,
                ConvexResponseKeyName,
                ConcaveResponseKeyName,
                ResponseKeysSwapped ? 1 : 0);
        }

        private string BuildResponsePrompt()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} = A / OUTWARD\n" +
                "{1} = B / INWARD",
                ConvexResponseKeyName,
                ConcaveResponseKeyName);
        }

        private string BuildWelcomePrompt(string notice)
        {
            string practiceState = trainingCompleted
                ? "PRACTICE: COMPLETE"
                : requireTrainingBeforeSession
                    ? "PRACTICE: REQUIRED"
                    : "PRACTICE: OPTIONAL";
            string noticeLine = string.IsNullOrWhiteSpace(notice)
                ? string.Empty
                : notice.Trim() + "\n\n";

            return string.Format(
                CultureInfo.InvariantCulture,
                "WELCOME\n\n" +
                "{0}" +
                "{1} = PRACTICE\n" +
                "{2} = START\n\n" +
                "{3}\n\n" +
                "Always look at the cross.\n\n" +
                "RESPONSES\n{4}",
                noticeLine,
                CheckerboardKeyboardController.GetReadableKeyName(trainingKey),
                CheckerboardKeyboardController.GetReadableKeyName(startSessionKey),
                practiceState,
                BuildResponsePrompt());
        }

        private void CaptureFixationAtTrialEnd()
        {
            // Das Kreuz bleibt auch während der Noise-Maske stehen, die Person soll
            // ja weiter dorthin schauen. Deshalb werden die Blickwerte erst
            // festgehalten, wenn die Antwort kommt oder abgebrochen wird.
            fixationSnapshotAvailable = true;
            fixationSampleValidAtTrialEnd = fixationMonitor != null &&
                fixationMonitor.CurrentSampleValid;
            fixationInsideAtTrialEnd = fixationMonitor != null &&
                fixationMonitor.IsInsideTolerance;
            fixationAngleAtTrialEnd = fixationMonitor != null
                ? fixationMonitor.CurrentAngleDegrees
                : float.NaN;
            continuousFixationAtTrialEnd = fixationMonitor != null
                ? fixationMonitor.ContinuousFixationSeconds
                : 0f;
            fixationValidFractionAtTrialEnd = fixationMonitor != null
                ? fixationMonitor.ValidSampleFraction
                : float.NaN;
        }

        private void ResetPresentationTimes()
        {
            trialStartUnitySeconds = 0d;
            stimulusEndUnitySeconds = 0d;
            responseWindowStartUnitySeconds = 0d;
            fixationSnapshotAvailable = false;
            fixationSampleValidAtTrialEnd = false;
            fixationInsideAtTrialEnd = false;
            fixationAngleAtTrialEnd = float.NaN;
            continuousFixationAtTrialEnd = 0f;
            fixationValidFractionAtTrialEnd = float.NaN;
        }

        private void ResetTrialFixationCounters()
        {
            // Jeder Versuch fängt mit frischen Zählern bei null an. Sonst würde man
            // die Aussetzer vom vorigen Durchgang mitschleppen.
            currentOffTargetSeconds = 0f;
            currentInvalidGazeSeconds = 0f;
            longestOffTargetSeconds = 0f;
            longestInvalidGazeSeconds = 0f;
        }

        private void OnValidate()
        {
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            stimulusDurationSeconds = Mathf.Max(0.01f, stimulusDurationSeconds);
            responseTimeoutSeconds = Mathf.Max(0f, responseTimeoutSeconds);
            trainingCategoryAVisualSpaceL = Mathf.Clamp(
                trainingCategoryAVisualSpaceL,
                0f,
                1.4f);
            trainingCategoryBVisualSpaceL = Mathf.Clamp(
                trainingCategoryBVisualSpaceL,
                0f,
                1.4f);
            trainingRepetitionsPerValue = Mathf.Max(1, trainingRepetitionsPerValue);
            trainingExampleNoiseSeconds = Mathf.Max(0f, trainingExampleNoiseSeconds);
            if (trainingVisualSpaceLValues != null)
            {
                for (int index = 0; index < trainingVisualSpaceLValues.Count; index++)
                {
                    trainingVisualSpaceLValues[index] = Mathf.Clamp(
                        trainingVisualSpaceLValues[index],
                        0f,
                        1.4f);
                }
            }
            trainingFixationSecondsWithoutEyeTracking = Mathf.Max(
                0f,
                trainingFixationSecondsWithoutEyeTracking);
            maximumOffTargetSeconds = Mathf.Max(0f, maximumOffTargetSeconds);
            maximumInvalidGazeSeconds = Mathf.Max(0f, maximumInvalidGazeSeconds);
            maximumGazeSampleAgeSeconds = Mathf.Max(0.01f, maximumGazeSampleAgeSeconds);
            maximumAttemptsPerTrial = Mathf.Max(0, maximumAttemptsPerTrial);
            trainingExampleStimulusSeconds = Mathf.Max(
                0.1f,
                trainingExampleStimulusSeconds);
            postResponseNoiseSeconds = Mathf.Max(0f, postResponseNoiseSeconds);
        }
    }
}
