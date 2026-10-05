using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
// Kurzname, damit nicht überall CheckerboardSessionState ausgeschrieben werden muss.
using State = GlobeEffect.VRCheckerboard.Experiment.CheckerboardSessionState;

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
        Aborted,
        PreStimulus
    }

    /// <summary>
    /// Die beiden Abläufe eines Durchgangs. Die Phase direkt nach dem Schachbrett
    /// ist immer die Antwortphase. Nach der Antwort folgt die jeweils andere
    /// Darstellung als kurze Phase vor dem nächsten Schachbrett.
    /// </summary>
    public enum CheckerboardTrialSequence
    {
        [InspectorName("A: Noise-Maske (Antwort), dann graue Fläche")]
        NoiseResponseThenGray = 0,

        [InspectorName("B: Graue Fläche (Antwort), dann Noise-Maske")]
        GrayResponseThenNoise = 1
    }

    /// <summary>
    /// Sagt für einen Ablauf, in welcher Phase die Noise-Maske zu sehen ist. In
    /// der jeweils anderen Phase steht die graue Fläche mit dem Kreuz.
    /// </summary>
    public static class CheckerboardTrialSequenceExtensions
    {
        public static bool ShowsNoiseDuringResponse(this CheckerboardTrialSequence sequence)
        {
            return sequence == CheckerboardTrialSequence.NoiseResponseThenGray;
        }

        public static bool ShowsNoiseBeforeStimulus(this CheckerboardTrialSequence sequence)
        {
            return !sequence.ShowsNoiseDuringResponse();
        }
    }

    /// <summary>
    /// Steuert den ganzen Checkerboard-Versuch.
    ///
    /// So läuft ein Durchgang ab: Die Person schaut auf das Kreuz. Liegt der Blick
    /// ruhig genug, kommt kurz das Schachbrett. Direkt danach beginnt die
    /// Antwortphase, in der die Person Category A oder B drückt. Je nach Ablauf
    /// ist dabei die Noise-Maske (A) oder die graue Fläche (B) zu sehen; die
    /// andere Darstellung folgt nach der Antwort vor dem nächsten Schachbrett.
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
        // BeginNextAttempt       holt den nächsten Durchgang aus der Warteschlange
        //                        und zeigt die Phase vor dem Schachbrett.
        // PresentCurrentTrial    gibt die Werte an den Stimulus weiter und zeigt ihn.
        // MonitorFixationDuringTrial  schaut nebenher, ob der Blick liegen bleibt.
        //
        // Danach wird die Antwort gespeichert. Hat die Person danebengeschaut,
        // kommt derselbe Durchgang ganz hinten noch einmal in die Warteschlange.
        [Header("References")]
        [SerializeField]
        private VrCheckerboardStimulus stimulus;

        [SerializeField]
        private CheckerboardKeyboardController keyboardController;

        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private CheckerboardFixationMonitor fixationMonitor;

        [Header("Session")]
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
        [Tooltip("Alle l-Werte, die gezeigt werden sollen. l = 1 ist gerade, l = 0,5 ist der Helmholtz-Punkt. Das hier sind nur Pilotwerte, die Liste darf komplett geändert werden.")]
        private List<float> visualSpaceLValues = new() { 1.2f, 1f, 0.8f, 0.6f, 0.5f, 0.4f, 0.2f };

        [FormerlySerializedAs("repetitionsPerCondition")]
        [SerializeField, Min(1)]
        [Tooltip("Wie oft jede Kombination gezeigt wird. Mehr Wiederholungen heißt sicherere Ergebnisse, aber auch eine längere Sitzung.")]
        private int repeatsPerCondition = 3;

        [Header("Fixation And Repeats")]
        [SerializeField]
        [Tooltip("Das Muster kommt erst, wenn der Blick ruhig auf dem Kreuz liegt, und wird dabei auch weiter überwacht. Für eine echte Messung muss das an sein.")]
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
        [SerializeField]
        [Tooltip("A: Schachbrett, Noise-Maske mit Antwort, graue Fläche, nächstes Schachbrett. B: Schachbrett, graue Fläche mit Antwort, Noise-Maske, nächstes Schachbrett.")]
        private CheckerboardTrialSequence trialSequence = CheckerboardTrialSequence.NoiseResponseThenGray;

        [FormerlySerializedAs("stimulusDurationSeconds")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Wie lange das Schachbrett zu sehen ist. 0,6 sind 600 Millisekunden.")]
        private float patternSeconds = 0.6f;

        [FormerlySerializedAs("responseTimeoutSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("Wie lange die Person ab Beginn der Antwortphase Zeit zum Antworten hat. 0 heißt: unbegrenzt.")]
        private float answerTimeoutSeconds = 5f;

        [SerializeField, Min(0f)]
        [Tooltip("Wie lange die Phase vor dem nächsten Schachbrett mindestens dauert, in Sekunden: bei A die graue Fläche, bei B die Noise-Maske. Mit Fixationskontrolle geht es erst weiter, wenn zusätzlich der Blick ruhig auf dem Kreuz liegt.")]
        private float preStimulusSeconds = 0.5f;

        [Header("Response Keys")]
        [SerializeField]
        private Key convexResponseKey = Key.UpArrow;

        [SerializeField]
        private Key concaveResponseKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Nimmt im Headset Trigger und Trackpad-Klick an. Die Pfeiltasten funktionieren zusätzlich weiter zum Testen am Laptop.")]
        private bool useVrControllerButtons = true;

        // Früher war das ein Häkchen "Swap Response Buttons". Aus = erster
        // Eintrag, An = zweiter Eintrag; gespeicherte Szenen behalten ihren Wert.
        [FormerlySerializedAs("swapResponseButtons")]
        [SerializeField]
        [Tooltip("Welche Controller-Taste welche Antwort gibt. Konvex ist Category A (nach außen), konkav Category B (nach innen). Vor einer Sitzung einstellen und währenddessen nicht ändern.")]
        private VrControllerMapping controllerMapping = VrControllerMapping.TrackpadConcaveTriggerConvex;

        [FormerlySerializedAs("categoryAResponseText")]
        [SerializeField]
        [Tooltip("Wie diese Antwort der Person genannt wird. Gespeichert wird sie im Code und in der CSV weiter als Convex.")]
        private string categoryALabel = "CATEGORY A";

        [FormerlySerializedAs("categoryBResponseText")]
        [SerializeField]
        [Tooltip("Wie diese Antwort der Person genannt wird. Gespeichert wird sie im Code und in der CSV weiter als Concave.")]
        private string categoryBLabel = "CATEGORY B";

        [Header("Welcome And Training")]
        [SerializeField]
        [Tooltip("Taste, mit der vom Startbildschirm aus das Training losgeht.")]
        private Key trainingKey = Key.T;

        [SerializeField]
        [Tooltip("Taste zum Weiterblättern im Training. Damit startet später auch der erste Durchgang.")]
        private Key continueTrainingKey = Key.Space;

        [SerializeField]
        [Tooltip("Ist das an, geht der Versuch erst los, wenn das Training einmal komplett durchlaufen wurde.")]
        private bool requireTrainingBeforeSession = true;

        [FormerlySerializedAs("trainingCategoryAVisualSpaceL")]
        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Das deutliche Beispiel für Category A im Training. Der Wert sollte klar zu erkennen sein und trotzdem im Bereich liegen, der später im Versuch vorkommt.")]
        private float trainingExampleA = 1.2f;

        [FormerlySerializedAs("trainingCategoryBVisualSpaceL")]
        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Das deutliche Beispiel für Category B im Training. Der Wert sollte klar zu erkennen sein und trotzdem im Bereich liegen, der später im Versuch vorkommt.")]
        private float trainingExampleB = 0.2f;

        [FormerlySerializedAs("trainingVisualSpaceLValues")]
        [SerializeField]
        [Tooltip("Die l-Werte für die Übungsdurchgänge. Jeder kommt gleich oft dran, und es gibt keine Rückmeldung, ob die Antwort stimmte.")]
        private List<float> trainingLValues = new() { 0.2f, 0.4f, 0.8f, 1.2f };

        [FormerlySerializedAs("trainingRepetitionsPerValue")]
        [SerializeField, Min(1)]
        [Tooltip("Wie oft jeder Übungswert im Training drankommt.")]
        private int trainingRepeatsPerValue = 3;

        [FormerlySerializedAs("trainingExampleStimulusSeconds")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Wie lange die beiden deutlichen Beispiele für Category A und B gezeigt werden. Die normalen Übungsdurchgänge bleiben so kurz wie im richtigen Experiment.")]
        private float trainingExampleSeconds = 2f;

        [FormerlySerializedAs("trainingExampleNoiseSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("Wie lange nach den beiden Beispielen die Darstellung der Antwortphase zu sehen ist: bei A die Noise-Maske, bei B die graue Fläche.")]
        private float trainingNoiseSeconds = 0.5f;

        [FormerlySerializedAs("trainingFixationSecondsWithoutEyeTracking")]
        [SerializeField, Min(0f)]
        [Tooltip("Wie lange im Training die Phase vor dem Muster mindestens dauert, wenn die Blickkontrolle aus ist. Dann kann ja nicht gemessen werden, ob der Blick ruhig liegt. Gilt nur, wenn der Wert größer ist als Pre Stimulus Seconds.")]
        private float trainingFixationSeconds = 0.5f;

        [Header("Keys")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        private Key abortSessionKey = Key.F6;

        [Header("Runtime Status (Read Only)")]
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

        private readonly LookAwayTimer lookAway = new();
        private IReadOnlyList<CheckerboardTrial> trialPlan;
        private CheckerboardTrialQueue trialQueue;
        private CheckerboardTrial currentTrial;
        private CheckerboardExperimentFiles experimentFiles;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private double stimulusEndUnitySeconds;
        private double responseWindowStartUnitySeconds;
        private double preStimulusEndUnitySeconds;
        private Coroutine presentationCoroutine;
        private Coroutine trainingCoroutine;
        private bool keyboardEventsSubscribed;
        private bool trainingCompleted;
        private bool trainingAdvanceRequested;
        private bool trainingResponseReceived;

        // Die Blickwerte vom Ende des Durchgangs. Leer (null), solange der
        // Durchgang noch läuft.
        //
        // Das Kreuz bleibt auch während der Noise-Maske stehen, die Person soll
        // ja weiter dorthin schauen. Deshalb werden die Blickwerte erst
        // festgehalten, wenn die Antwort kommt oder abgebrochen wird.
        private FixationSnapshot? gazeAtTrialEnd;

        public CheckerboardSessionState SessionState => sessionState;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public int ValidTrialsCompleted => validTrialsCompleted;
        public int PresentationCount => presentationCount;
        public bool RequireFixation => requireFixation;
        public bool TrainingCompleted => trainingCompleted;
        public CheckerboardTrialSequence TrialSequence => trialSequence;
        public bool ResponseKeysSwapped => keyboardController != null && keyboardController.SwapResponseKeys;
        public string ConvexResponseKeyName => ResponseKeyName(CheckerboardCurvatureResponse.Convex);
        public string ConcaveResponseKeyName => ResponseKeyName(CheckerboardCurvatureResponse.Concave);
        public string ControllerSummary => keyboardController != null ? keyboardController.ControllerSummary : "–";
        public string KeyboardSummary => keyboardController != null ? keyboardController.KeyboardSummary : "–";

        public bool IsSessionActive =>
            sessionState is State.WaitingForExperimentReady
                or State.InterTrial
                or State.PreStimulus
                or State.WaitingForFixation
                or State.RunningTrial
                or State.WaitingForResponse;

        public bool IsTrainingActive =>
            sessionState is State.TrainingInstructions
                or State.TrainingFixation
                or State.TrainingExample
                or State.TrainingNoise
                or State.TrainingWaitingForResponse
                or State.TrainingComplete;

        // Ein Durchgang läuft: Das Muster ist zu sehen oder die Antwortphase läuft.
        private bool IsTrialRunning => sessionState is State.RunningTrial or State.WaitingForResponse;

        private string ContinueKeyName =>
            CheckerboardKeyboardController.GetReadableKeyName(continueTrainingKey);

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
                else if (!IsSessionActive && keyboard[trainingKey].wasPressedThisFrame)
                {
                    StartTraining();
                    return;
                }
                else if (!IsSessionActive && keyboard[startSessionKey].wasPressedThisFrame)
                {
                    StartSession();
                    return;
                }

                if (IsSessionActive && keyboard[abortSessionKey].wasPressedThisFrame)
                {
                    AbortSession("ManualAbort");
                    return;
                }

                if (sessionState == State.WaitingForExperimentReady
                    && keyboard[continueTrainingKey].wasPressedThisFrame)
                {
                    ConfirmExperimentReady();
                    return;
                }
            }

            if (sessionState == State.PreStimulus
                && Time.realtimeSinceStartupAsDouble >= preStimulusEndUnitySeconds)
            {
                // Die Mindestdauer der Phase vor dem Schachbrett ist um. Mit
                // Fixationskontrolle muss jetzt nur noch der Blick ruhig liegen.
                sessionState = State.WaitingForFixation;
            }

            if (sessionState == State.WaitingForFixation
                && (!requireFixation || (fixationMonitor != null && fixationMonitor.RequirementMet)))
            {
                PresentCurrentTrial();
                return;
            }

            if (IsTrialRunning && requireFixation)
            {
                MonitorFixationDuringTrial();
            }
        }

        private void OnDisable()
        {
            UnsubscribeKeyboardEvents();
            StopAndClear(ref trainingCoroutine);
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }
        }

        public void ShowWelcomeScreen(string notice = "")
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
            sessionState = State.Welcome;
            stimulus.ShowResponsePrompt(BuildWelcomePrompt(notice));
        }

        public bool StartTraining()
        {
            if (IsSessionActive || IsTrainingActive || !CheckSceneSetup())
            {
                return false;
            }

            ApplyResponseKeySettings();
            StopAndClear(ref trainingCoroutine);
            StopAndClear(ref presentationCoroutine);
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

            StopAndClear(ref trainingCoroutine);
            ShowWelcomeScreen("PRACTICE STOPPED");
        }

        private IEnumerator RunTrainingSequence()
        {
            sessionState = State.TrainingInstructions;
            stimulus.ShowResponsePrompt(
                "PRACTICE\n\n" +
                "Keep looking at the cross.\n" +
                "First you will see both categories.\n" +
                "Then you can practise.\n\n" +
                BuildResponsePrompt() + "\n\n" +
                ContinueKeyName + " = CONTINUE");
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
                sessionState = State.TrainingInstructions;
                stimulus.ShowResponsePrompt(BuildTrainingCategoryIntroduction(example));
                yield return WaitForTrainingAdvance();

                presentationIndex++;
                float exampleL =
                    example == CheckerboardCurvatureResponse.Convex ? trainingExampleA : trainingExampleB;
                yield return ShowTrainingPattern(exampleL, unchecked(randomSeed + 50000 + presentationIndex),
                    waitForResponse: false, patternDurationSeconds: trainingExampleSeconds);
                if (!IsTrainingActive)
                {
                    yield break;
                }

                sessionState = State.TrainingInstructions;
                stimulus.ShowResponsePrompt(BuildCategoryText(example) + "\n\n" +
                    ContinueKeyName + " = CONTINUE");
                yield return WaitForTrainingAdvance();
            }

            sessionState = State.TrainingInstructions;
            stimulus.ShowResponsePrompt(string.Format(
                CultureInfo.InvariantCulture,
                "PRACTICE TRIALS\n\n" +
                "Each pattern lasts {0:0} ms.\n" +
                "Answer while the {1} is visible.\n" +
                "Keep looking at the cross.\n\n" +
                "{2}\n\n" +
                "{3} = CONTINUE",
                patternSeconds * 1000f,
                trialSequence.ShowsNoiseDuringResponse() ? "noise" : "grey screen",
                BuildResponsePrompt(),
                ContinueKeyName));
            yield return WaitForTrainingAdvance();

            foreach (float practiceL in BuildTrainingLOrder(unchecked(randomSeed ^ 0x51F15EED)))
            {
                presentationIndex++;
                yield return ShowTrainingPattern(practiceL, unchecked(randomSeed + 60000 + presentationIndex),
                    waitForResponse: true, patternDurationSeconds: patternSeconds);
                if (!IsTrainingActive)
                {
                    yield break;
                }

                // Warten, bis geantwortet wurde oder die Antwortzeit um ist.
                trainingResponseReceived = false;
                sessionState = State.TrainingWaitingForResponse;
                double responseDeadline = answerTimeoutSeconds > 0f
                    ? Time.realtimeSinceStartupAsDouble + answerTimeoutSeconds
                    : double.PositiveInfinity;
                while (IsTrainingActive && !trainingResponseReceived
                    && Time.realtimeSinceStartupAsDouble < responseDeadline)
                {
                    yield return null;
                }
            }

            trainingCompleted = true;
            sessionState = State.TrainingComplete;
            stimulus.ShowResponsePrompt("PRACTICE COMPLETE\n\n" + ContinueKeyName + " = RETURN TO WELCOME");
            yield return WaitForTrainingAdvance();

            trainingCoroutine = null;
            ShowWelcomeScreen();
        }

        private IEnumerator ShowTrainingPattern(
            float visualSpaceL, int noiseSeed, bool waitForResponse, float patternDurationSeconds)
        {
            // Ein Übungsdurchgang läuft genauso ab wie im Versuch: Phase vor dem
            // Muster -> Muster -> Antwortphase. Welche der beiden Phasen die
            // Noise-Maske zeigt und welche die graue Fläche, hängt vom Ablauf ab.
            PrepareTrainingCondition(visualSpaceL);
            if (fixationMonitor != null)
            {
                fixationMonitor.ResetFixationWindow();
            }

            sessionState = State.TrainingFixation;
            ShowNoiseOrGray(trialSequence.ShowsNoiseBeforeStimulus(), unchecked(noiseSeed + 0x4A31));
            yield return WaitForTrainingSeconds(
                Mathf.Max(preStimulusSeconds, requireFixation ? 0f : trainingFixationSeconds));
            while (requireFixation && IsTrainingActive
                && fixationMonitor != null && !fixationMonitor.RequirementMet)
            {
                yield return null;
            }

            if (!IsTrainingActive)
            {
                yield break;
            }

            sessionState = State.TrainingExample;
            stimulus.Show();
            yield return WaitForTrainingSeconds(patternDurationSeconds);
            if (!IsTrainingActive)
            {
                yield break;
            }

            // Der Zustand heißt weiter TrainingNoise, auch wenn bei Ablauf B hier
            // die graue Fläche steht. Gemeint ist die Antwortphase.
            sessionState = State.TrainingNoise;
            ShowNoiseOrGray(trialSequence.ShowsNoiseDuringResponse(), noiseSeed);
            if (!waitForResponse && trainingNoiseSeconds > 0f)
            {
                yield return WaitForTrainingSeconds(trainingNoiseSeconds);
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
            double endTime = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, seconds);
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
            if (fieldOfViewValues != null && fieldOfViewValues.Count > 0)
            {
                stimulus.SetAngularDiameter(fieldOfViewValues[0]);
            }

            if (eyePresentations != null && eyePresentations.Count > 0)
            {
                stimulus.SetEyePresentation(eyePresentations[0]);
            }

            stimulus.SetVisualSpaceL(visualSpaceL);
        }

        private List<float> BuildTrainingLOrder(int seed)
        {
            // Jeder Übungswert kommt gleich oft vor. Danach wird die Liste gemischt,
            // genauso wie der Plan für den richtigen Versuch.
            var order = new List<float>();
            if (trainingLValues != null)
            {
                for (int repetition = 0; repetition < Mathf.Max(1, trainingRepeatsPerValue); repetition++)
                {
                    foreach (float value in trainingLValues)
                    {
                        order.Add(Mathf.Clamp(value, 0f, 1.4f));
                    }
                }
            }

            PlannerTools.Shuffle(order, seed);
            return order;
        }

        private string BuildTrainingCategoryIntroduction(CheckerboardCurvatureResponse response)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}\n\n" +
                "Example: {1:0.#} seconds\n\n" +
                "{2} = SHOW",
                BuildCategoryText(response),
                trainingExampleSeconds,
                ContinueKeyName);
        }

        // Name der Kategorie, ihre Beschreibung und die Taste, mit der die Person
        // sie später wählt. Die Taste richtet sich nach der eingestellten Zuordnung.
        private string BuildCategoryText(CheckerboardCurvatureResponse response)
        {
            bool convex = response == CheckerboardCurvatureResponse.Convex;
            return (convex ? categoryALabel : categoryBLabel) + "\n" +
                (convex ? "CURVES OUTWARD" : "CURVES INWARD") + "\n" +
                "RESPONSE: " + ResponseKeyName(response);
        }

        public bool StartSession()
        {
            // Das ist der große Startknopf. Der Reihe nach passiert hier:
            // prüfen, ob in der Szene alles da ist -> alle Durchgänge bauen und
            // mischen -> den Messordner anlegen -> Eye Tracking starten ->
            // den ersten Durchgang zeigen.
            if (IsSessionActive || IsTrainingActive)
            {
                Debug.LogWarning("Eine Checkerboard-Sitzung oder ein Training läuft bereits.", this);
                return false;
            }

            if (requireTrainingBeforeSession && !trainingCompleted)
            {
                ShowWelcomeScreen("PLEASE COMPLETE THE PRACTICE FIRST");
                return false;
            }

            if (!CheckSceneSetup())
            {
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

            // Die Tastenbelegung wird mit Absicht hier gesetzt und nicht im
            // Keyboard Controller. So gilt sie auch in älteren Szenen, in denen
            // am Controller noch die alten Tasten eingetragen sind.
            ApplyResponseKeySettings();

            try
            {
                trialPlan = CheckerboardTrialPlanner.CreateRandomizedPlan(
                    fieldOfViewValues, eyePresentations, visualSpaceLValues, repeatsPerCondition, randomSeed);
                trialQueue = new CheckerboardTrialQueue(trialPlan);

                DateTime sessionStartUtc = DateTime.UtcNow;
                experimentFiles = new CheckerboardExperimentFiles(
                    ExperimentOutputPath.Resolve(outputRoot), participantId, sessionLabel,
                    sessionStartUtc, randomSeed);
                experimentFiles.WritePlan(
                    trialPlan, stimulus.GridLineSpacingDegrees, patternSeconds, trialSequence,
                    preStimulusSeconds, answerTimeoutSeconds, ConvexResponseKeyName,
                    ConcaveResponseKeyName, ResponseKeysSwapped, controllerMapping);
                activeSessionFolder = experimentFiles.SessionFolder;
                StartEyeTracking(sessionStartUtc);
            }
            catch (Exception exception)
            {
                sessionState = State.Aborted;
                Debug.LogError(
                    "Checkerboard-Sitzung konnte nicht gestartet werden: " + exception.Message, this);
                return false;
            }

            StopAndClear(ref presentationCoroutine);
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            validTrialsCompleted = 0;
            presentationCount = 0;
            sessionState = State.WaitingForExperimentReady;

            Debug.Log($"Checkerboard-Sitzung vorbereitet: {totalTrials} gültige Trials geplant, " +
                $"Seed {randomSeed}.\n" + activeSessionFolder, this);
            stimulus.ShowResponsePrompt(
                "MAIN EXPERIMENT\n\n" +
                "Keep looking at the cross.\n\n" +
                "RESPONSES\n" + BuildResponsePrompt() + "\n\n" +
                ContinueKeyName + " = START");
            WriteMarker("ExperimentReadyScreenShown");
            return true;
        }

        /// <summary>
        /// Die Person ist bereit: Der Hinweisbildschirm verschwindet und der erste
        /// Durchgang beginnt. Im Versuch löst das die Weiter-Taste aus.
        /// </summary>
        public void ConfirmExperimentReady()
        {
            if (sessionState != State.WaitingForExperimentReady)
            {
                return;
            }

            WriteMarker("ExperimentReadyConfirmed");
            sessionState = State.InterTrial;
            BeginNextAttempt();
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

            StopAndClear(ref presentationCoroutine);
            if (IsTrialRunning && currentTrial != null && experimentFiles != null)
            {
                TryAppendResult(CaptureCurrentResult(
                    CheckerboardCurvatureResponse.None, validForAnalysis: false, "aborted:" + reason));
            }

            WriteMarker("SessionAborted;reason=" +
                ExperimentFilesBase.SanitizeIdentifier(reason, "unspecified"));
            EndSession(State.Aborted);
            Debug.LogWarning("Checkerboard-Sitzung abgebrochen: " + reason, this);
        }

        private void BeginNextAttempt()
        {
            // Aus der Warteschlange kommt entweder ein neuer Durchgang oder eine
            // Wiederholung, die vorhin hinten angehängt wurde. Beides sieht hier
            // gleich aus. Ist die Warteschlange leer, ist die Sitzung fertig.
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

            ResetFixationChecks();
            trialStartUnitySeconds = 0d;
            stimulusEndUnitySeconds = 0d;
            responseWindowStartUnitySeconds = 0d;
            gazeAtTrialEnd = null;

            if (preStimulusSeconds <= 0f && !requireFixation)
            {
                // Ohne Mindestdauer und ohne Blickkontrolle gibt es keine Phase
                // vor dem Schachbrett. Es erscheint sofort.
                PresentCurrentTrial();
                return;
            }

            // Die Phase vor dem Schachbrett: bei Ablauf A die graue Fläche, bei
            // Ablauf B die Noise-Maske, jeweils mit Kreuz. Update zeigt das
            // Schachbrett, sobald die Mindestdauer um ist und der Blick ruhig liegt.
            // Ein eigener Seed sorgt dafür, dass hier nie dasselbe Rauschen steht
            // wie in einer Antwortphase.
            sessionState = State.PreStimulus;
            preStimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble + preStimulusSeconds;
            ShowPhase("pre_stimulus", trialSequence.ShowsNoiseBeforeStimulus(),
                unchecked(randomSeed + presentationCount * 1879 + 0x4A31), preStimulusSeconds);
            if (requireFixation)
            {
                WriteMarker("FixationAcquisitionStart;sequence={0};attempt={1}",
                    currentTrial.SequenceIndex, currentTrial.AttemptNumber);
            }
        }

        private void ShowPhase(string phase, bool noise, int noiseSeed, float plannedSeconds)
        {
            // Zeigt die Darstellung einer Phase und vermerkt sie in der Aufnahme.
            // plannedSeconds ist bei der Antwortphase das Antworttimeout (0 =
            // unbegrenzt) und sonst die Mindestdauer.
            ShowNoiseOrGray(noise, noiseSeed);
            WriteMarker("{0};phase={1};sequence={2};attempt={3};seed={4};planned_s={5:F4}",
                noise ? "NoiseMaskStarted" : "GrayScreenStarted", phase,
                currentTrial.SequenceIndex, currentTrial.AttemptNumber, noise ? noiseSeed : 0,
                plannedSeconds);
        }

        // Noise-Maske oder graue Fläche, beide mit Fixationskreuz und in derselben
        // runden Öffnung. Versuch und Training benutzen dieselbe Stelle.
        private void ShowNoiseOrGray(bool noise, int noiseSeed)
        {
            if (noise)
            {
                stimulus.ShowNoise(noiseSeed);
            }
            else
            {
                stimulus.ShowFixationOnly();
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

            ResetFixationChecks();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulusEndUnitySeconds = 0d;
            responseWindowStartUnitySeconds = 0d;
            sessionState = State.RunningTrial;

            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show();

            StopAndClear(ref presentationCoroutine);
            presentationCoroutine = StartCoroutine(RunPresentationSequence(currentTrial));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Trial {0}/{1}, Präsentation {2}: {3}, FOV={4:F1}°, l={5:F3}, " +
                "Versuch {6}. Stimulus={7:F3}s, danach Antwortphase mit {8}.",
                currentTrialNumber, totalTrials, presentationCount, currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees, currentTrial.VisualSpaceL,
                currentTrial.AttemptNumber, patternSeconds,
                trialSequence.ShowsNoiseDuringResponse() ? "Noise-Maske" : "grauer Fläche"), this);
        }

        private IEnumerator RunPresentationSequence(CheckerboardTrial presentedTrial)
        {
            // Das Muster ist bei allen Personen exakt gleich lange zu sehen.
            // Sonst könnte man die Antworten hinterher nicht vergleichen.
            yield return new WaitForSecondsRealtime(patternSeconds);
            if (sessionState != State.RunningTrial || currentTrial != presentedTrial)
            {
                presentationCoroutine = null;
                yield break;
            }

            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            WriteMarker("StimulusEnded;sequence={0};attempt={1};duration_s={2:F4}",
                presentedTrial.SequenceIndex, presentedTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds);

            // Ab jetzt läuft die Antwortphase: bei Ablauf A mit der Noise-Maske,
            // bei Ablauf B mit der grauen Fläche. Jeder Durchgang bekommt sein
            // eigenes Noise-Muster.
            int noiseSeed = unchecked(
                randomSeed + presentedTrial.SequenceIndex * 1009 + presentedTrial.AttemptNumber * 9176);
            responseWindowStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = State.WaitingForResponse;
            ShowPhase("response", trialSequence.ShowsNoiseDuringResponse(), noiseSeed, answerTimeoutSeconds);

            if (answerTimeoutSeconds <= 0f)
            {
                presentationCoroutine = null;
                yield break;
            }

            yield return new WaitForSecondsRealtime(answerTimeoutSeconds);

            // Erst die Referenz leeren, dann abbrechen. Die Coroutine ist an
            // dieser Stelle nämlich schon von selbst fertig, und man würde
            // sonst versuchen, etwas zu stoppen, das gar nicht mehr läuft.
            presentationCoroutine = null;
            if (sessionState == State.WaitingForResponse && currentTrial == presentedTrial)
            {
                InvalidateCurrentTrial("response_timeout");
            }
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            // Im Training zählt der Tastendruck nur als "weiter". Er wird nicht
            // bewertet und landet auch in keiner Messdatei.
            if (sessionState == State.TrainingWaitingForResponse
                && response != CheckerboardCurvatureResponse.None)
            {
                trainingResponseReceived = true;
                return;
            }

            // Im Versuch wird die Antwort nur in der Antwortphase angenommen. Die
            // erste Antwort beendet diese Phase sofort. Ein zweiter Tastendruck
            // landet deshalb in keinem Durchgang, auch nicht im nächsten: Dessen
            // Antwortphase beginnt erst nach seinem eigenen Schachbrett. Pro
            // Durchgang wird genau eine Zeile geschrieben.
            if (sessionState != State.WaitingForResponse || currentTrial == null
                || response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            StopAndClear(ref presentationCoroutine);
            gazeAtTrialEnd = TakeFixationSnapshot();

            CheckerboardTrialResult result = CaptureCurrentResult(response, validForAnalysis: true, "valid");
            if (!TryAppendResult(result))
            {
                return;
            }

            validTrialsCompleted++;
            WriteMarker("TrialResponse;sequence={0};attempt={1};response={2};response_s={3:F4};valid=1",
                currentTrial.SequenceIndex, currentTrial.AttemptNumber, response, result.ResponseTimeSeconds);
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
            // Der misslungene Versuch wird trotzdem gespeichert, zählt aber nicht als
            // Antwort. Man sieht später in der CSV, dass es ihn gab und warum er
            // nicht gezählt hat.
            //
            // Dieselbe Bedingung kommt danach ganz hinten wieder in die
            // Warteschlange, mit einer höheren Versuchsnummer.
            if (!IsTrialRunning || currentTrial == null)
            {
                return;
            }

            StopAndClear(ref presentationCoroutine);
            gazeAtTrialEnd ??= TakeFixationSnapshot();

            CheckerboardTrial invalidTrial = currentTrial;
            string status = reason == "response_timeout"
                ? "invalid_response:response_timeout"
                : "invalid_fixation:" + reason;
            CheckerboardTrialResult result =
                CaptureCurrentResult(CheckerboardCurvatureResponse.None, validForAnalysis: false, status);
            if (!TryAppendResult(result))
            {
                return;
            }

            WriteMarker(
                "TrialInvalid;sequence={0};attempt={1};reason={2};off_target_s={3:F4};invalid_gaze_s={4:F4}",
                invalidTrial.SequenceIndex, invalidTrial.AttemptNumber, reason,
                lookAway.LongestLookAwaySeconds, lookAway.LongestNoDataSeconds);

            if (maxRepeatsPerTrial > 0 && invalidTrial.AttemptNumber >= maxRepeatsPerTrial)
            {
                WriteMarker("SessionAborted;reason=maximum_repeat_attempts_reached");
                EndSession(State.Aborted);
                Debug.LogError(
                    "Die maximale Zahl an Wiederholungen wurde erreicht. Die Sitzung wurde beendet.", this);
                return;
            }

            CheckerboardTrial repeat = trialQueue.AppendRepeatedAttempt(invalidTrial);
            WriteMarker("TrialRepeatQueued;sequence={0};next_attempt={1};queue_position={2}",
                repeat.SequenceIndex, repeat.AttemptNumber, trialQueue.Count);
            FinishAttemptAndScheduleNext();
        }

        private CheckerboardTrialResult CaptureCurrentResult(
            CheckerboardCurvatureResponse response, bool validForAnalysis, string status)
        {
            // Hier wird alles zu diesem Durchgang in ein Objekt gepackt: die
            // Bedingung, die Zeiten und die Blickwerte. Dieses Objekt geht danach
            // an die Dateiklasse, die daraus eine CSV-Zeile macht.
            //
            // Wurden die Blickwerte am Ende schon festgehalten, gelten die. Sonst
            // (zum Beispiel beim Abbrechen) werden die aktuellen genommen.
            FixationSnapshot gaze = gazeAtTrialEnd ?? TakeFixationSnapshot();

            double resultEndTime = Time.realtimeSinceStartupAsDouble;
            double stimulusEnd = stimulusEndUnitySeconds > trialStartUnitySeconds
                ? stimulusEndUnitySeconds
                : resultEndTime;
            double responseWindowStart = responseWindowStartUnitySeconds >= stimulusEnd
                ? responseWindowStartUnitySeconds
                : stimulusEnd;

            return new CheckerboardTrialResult(
                currentTrial, presentationCount, trialStartUtc,
                trialStartUnitySeconds, stimulusEnd, responseWindowStart, resultEndTime,
                stimulus.ApertureEdgeSoftnessDegrees, stimulus.UseCircularAperture,
                stimulus.GridLineSpacingDegrees, stimulus.GridLineSpacingUv,
                response, validForAnalysis,
                gaze.SampleValid, gaze.OnTarget, gaze.AngleDegrees,
                gaze.SteadySeconds, gaze.ValidSampleFraction,
                lookAway.LongestLookAwaySeconds, lookAway.LongestNoDataSeconds,
                status, trialSequence);
        }

        private FixationSnapshot TakeFixationSnapshot()
        {
            return fixationMonitor != null ? fixationMonitor.TakeSnapshot() : FixationSnapshot.None;
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
            // Der nächste Durchgang beginnt sofort mit seiner Phase vor dem
            // Schachbrett. Dort steht die jeweils andere Darstellung als in der
            // Antwortphase.
            StopAndClear(ref presentationCoroutine);
            currentTrial = null;
            sessionState = State.InterTrial;
            BeginNextAttempt();
        }

        private void CompleteSession()
        {
            // Zum Schluss: Marker in die Aufnahme schreiben, Bild ausblenden und
            // die Eye-Tracking-Aufzeichnung beenden.
            StopAndClear(ref presentationCoroutine);
            currentTrialNumber = totalTrials;
            WriteMarker("SessionCompleted;valid_trials={0};presentations={1}",
                validTrialsCompleted, presentationCount);
            EndSession(State.Completed);

            Debug.Log($"Checkerboard-Sitzung vollständig gespeichert: {validTrialsCompleted} " +
                $"gültige Trials aus {presentationCount} Präsentationen.\n" + activeSessionFolder, this);
        }

        private void FailSessionAfterWriteError(Exception exception)
        {
            StopAndClear(ref presentationCoroutine);
            Debug.LogError("Trialdaten konnten nicht gespeichert werden; die Sitzung wird beendet: " +
                exception.Message, this);
            WriteMarker("SessionAborted;reason=result_write_error");
            EndSession(State.Aborted);
        }

        // Beendet die Sitzung: Bild aus, Aufnahme stoppen, Zustand setzen.
        // Das brauchen Abbrechen, Fertigwerden und Schreibfehler gleichermaßen.
        private void EndSession(State endState)
        {
            if (stimulus != null)
            {
                stimulus.Hide();
            }

            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = endState;
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
                "SessionStart;participant={0};session={1};seed={2};planned_trials={3};utc={4};mapping={5};" +
                "noise_dot_deg={6:F4};noise_refresh_hz={7:F2};{8}",
                ExperimentFilesBase.SanitizeIdentifier(participantId, "pilot"),
                ExperimentFilesBase.SanitizeIdentifier(sessionLabel, "session"),
                randomSeed, trialPlan.Count, sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                VisualSpaceRadialMapping.MappingVersion,
                stimulus.NoiseDotSizeDegrees, stimulus.NoiseRefreshRateHz, BuildTimingAndResponseFields());
        }

        // Ablauf, Zeiten und Tastenbelegung für die Marker SessionStart und
        // TrialStart. So stehen dieselben Angaben nur an einer Stelle.
        private string BuildTimingAndResponseFields()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stimulus_duration_s={0:F4};trial_sequence={1};pre_stimulus_s={2:F4};" +
                "response_timeout_s={3:F4};category_a_key={4};category_b_key={5};" +
                "response_keys_swapped={6};controller_mapping={7}",
                patternSeconds, trialSequence, preStimulusSeconds, answerTimeoutSeconds,
                ConvexResponseKeyName, ConcaveResponseKeyName, ResponseKeysSwapped ? 1 : 0,
                controllerMapping);
        }

        // Sucht fehlende Verweise und prüft, ob alles da ist, was Training und
        // Versuch brauchen. Fehlt etwas, steht in der Konsole, was.
        private bool CheckSceneSetup()
        {
            ResolveReferences();
            SubscribeKeyboardEvents();
            if (stimulus == null || keyboardController == null)
            {
                Debug.LogError("Stimulus und Checkerboard Keyboard Controller müssen zugewiesen sein.", this);
                return false;
            }

            if (requireFixation && fixationMonitor == null)
            {
                Debug.LogError("Fixationskontrolle ist aktiv, aber der Fixation Monitor fehlt.", this);
                return false;
            }

            return true;
        }

        private void ResolveReferences()
        {
            // Felder, die im Inspector leer geblieben sind, werden hier in der Szene
            // gesucht. Was schon eingetragen ist, wird nicht angefasst.
            stimulus = UnityTools.FindIfMissing(stimulus);
            if (keyboardController == null && stimulus != null)
            {
                keyboardController = stimulus.GetComponent<CheckerboardKeyboardController>();
            }

            if (eyeTrackingToolbox == null)
            {
                eyeTrackingToolbox = EyeTrackingToolbox.Instance;
            }

            eyeTrackingToolbox = UnityTools.FindIfMissing(eyeTrackingToolbox);
            fixationMonitor = UnityTools.FindIfMissing(fixationMonitor);
        }

        private void ApplyResponseKeySettings()
        {
            if (keyboardController == null)
            {
                return;
            }

            keyboardController.SetResponseKeys(concaveResponseKey, convexResponseKey);
            keyboardController.SetVrControllerButtonsEnabled(useVrControllerButtons);
            keyboardController.SetVrControllerMapping(controllerMapping);
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
            if (keyboardEventsSubscribed && keyboardController != null)
            {
                keyboardController.ResponseSubmitted -= HandleResponseSubmitted;
            }

            keyboardEventsSubscribed = false;
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

        // Hält eine laufende Coroutine an und merkt sich, dass keine mehr läuft.
        private void StopAndClear(ref Coroutine coroutine)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
                coroutine = null;
            }
        }

        private void StopEyeTrackingRecording()
        {
            if (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }
        }

        private void WriteMarker(string message)
        {
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

        private string BuildTrialStartMarker(CheckerboardTrial trial)
        {
            // Ein Marker ist eine kurze Notiz mitten in der Eye-Tracking-Aufnahme.
            // Damit weiß man beim Auswerten, welcher Blickwert zu welchem Teil des
            // Versuchs gehört, also zum Beispiel zum Muster oder zur Noise-Maske.
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "circular_aperture={8};grid_spacing_deg={9:F3};" +
                "grid_spacing_uv={10:F6};visual_space_l={11:F4};{12}",
                presentationCount, trial.SequenceIndex, trial.ConditionIndex, trial.Repetition,
                trial.AttemptNumber, trial.EyePresentation, trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees, stimulus.UseCircularAperture,
                stimulus.GridLineSpacingDegrees, stimulus.GridLineSpacingUv, trial.VisualSpaceL,
                BuildTimingAndResponseFields());
        }

        private string ResponseKeyName(CheckerboardCurvatureResponse response)
        {
            return keyboardController != null ? keyboardController.GetResponseControlName(response) : "–";
        }

        // Die beiden Zeilen mit der Tastenbelegung, wie sie auf dem Startbildschirm,
        // im Training und vor dem Versuch stehen. Sie richten sich immer nach der
        // eingestellten Zuordnung.
        private string BuildResponsePrompt()
        {
            return keyboardController != null
                ? keyboardController.BuildResponseLines("A / OUTWARD", "B / INWARD")
                : string.Empty;
        }

        private string BuildWelcomePrompt(string notice)
        {
            string practiceState = trainingCompleted
                ? "PRACTICE: COMPLETE"
                : requireTrainingBeforeSession
                    ? "PRACTICE: REQUIRED"
                    : "PRACTICE: OPTIONAL";
            string noticeLine = string.IsNullOrWhiteSpace(notice) ? string.Empty : notice.Trim() + "\n\n";

            // So sieht der Startbildschirm Zeile für Zeile aus.
            return "WELCOME\n\n" +
                noticeLine +
                CheckerboardKeyboardController.GetReadableKeyName(trainingKey) + " = PRACTICE\n" +
                CheckerboardKeyboardController.GetReadableKeyName(startSessionKey) + " = START\n\n" +
                practiceState + "\n\n" +
                "Always look at the cross.\n\n" +
                "RESPONSES\n" + BuildResponsePrompt();
        }

        private void OnValidate()
        {
            repeatsPerCondition = Mathf.Max(1, repeatsPerCondition);
            patternSeconds = Mathf.Max(0.01f, patternSeconds);
            answerTimeoutSeconds = Mathf.Max(0f, answerTimeoutSeconds);
            trainingExampleA = Mathf.Clamp(trainingExampleA, 0f, 1.4f);
            trainingExampleB = Mathf.Clamp(trainingExampleB, 0f, 1.4f);
            trainingRepeatsPerValue = Mathf.Max(1, trainingRepeatsPerValue);
            trainingNoiseSeconds = Mathf.Max(0f, trainingNoiseSeconds);
            if (trainingLValues != null)
            {
                for (int index = 0; index < trainingLValues.Count; index++)
                {
                    trainingLValues[index] = Mathf.Clamp(trainingLValues[index], 0f, 1.4f);
                }
            }

            trainingFixationSeconds = Mathf.Max(0f, trainingFixationSeconds);
            maxLookAwaySeconds = Mathf.Max(0f, maxLookAwaySeconds);
            maxNoDataSeconds = Mathf.Max(0f, maxNoDataSeconds);
            maxSampleAgeSeconds = Mathf.Max(0.01f, maxSampleAgeSeconds);
            maxRepeatsPerTrial = Mathf.Max(0, maxRepeatsPerTrial);
            trainingExampleSeconds = Mathf.Max(0.1f, trainingExampleSeconds);
            preStimulusSeconds = Mathf.Max(0f, preStimulusSeconds);
        }
    }
}
