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
    /// Führt den statischen Checkerboard-Test aus. l wird vorgegeben und nicht
    /// von der Versuchsperson verändert. Nach stabiler Fixation erscheint das
    /// Muster und danach die Entscheidung zwischen Category A und Category B.
    /// Während der Antwort bleibt die Noise-Maske mit Fixationskreuz sichtbar.
    /// Vor der eigentlichen Sitzung können dieselben Schritte geübt werden.
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
        [Tooltip("Maximale Antwortzeit ab Beginn der Noise-Maske. 0 bedeutet ohne Zeitlimit.")]
        private float responseTimeoutSeconds = 5f;

        [SerializeField, Min(0f)]
        private float interTrialSeconds = 0.5f;

        [Header("Antwortkategorien und Tasten")]
        [SerializeField]
        private Key convexResponseKey = Key.UpArrow;

        [SerializeField]
        private Key concaveResponseKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Bezeichnung der intern als Convex gespeicherten Antwort.")]
        private string categoryAResponseText = "CATEGORY A";

        [SerializeField]
        [Tooltip("Bezeichnung der intern als Concave gespeicherten Antwort.")]
        private string categoryBResponseText = "CATEGORY B";

        [Header("Welcome und Training")]
        [SerializeField]
        [Tooltip("Startet das Training vom Welcome Screen.")]
        private Key trainingKey = Key.T;

        [SerializeField]
        [Tooltip("Geht im Training zur nächsten Erklärung weiter.")]
        private Key continueTrainingKey = Key.Space;

        [SerializeField]
        [Tooltip("Wenn aktiv, muss das Training vor F5 einmal vollständig beendet werden.")]
        private bool requireTrainingBeforeSession = true;

        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Deutliches Beispiel für Category A. Der Wert sollte pilotiert werden und im geplanten Versuchsbereich liegen.")]
        private float trainingCategoryAVisualSpaceL = 1.2f;

        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Deutliches Beispiel für Category B. Der Wert sollte pilotiert werden und im geplanten Versuchsbereich liegen.")]
        private float trainingCategoryBVisualSpaceL = 0.2f;

        [SerializeField]
        [Tooltip("l-Werte für die unbewerteten Übungstrials. Jeder Wert wird gleich oft gezeigt.")]
        private List<float> trainingVisualSpaceLValues = new()
        {
            0.2f,
            0.4f,
            0.8f,
            1.2f
        };

        [SerializeField, Min(1)]
        [Tooltip("Wie oft jeder eingetragene l-Wert im Training vorkommt.")]
        private int trainingRepetitionsPerValue = 3;

        [SerializeField, Min(0f)]
        [Tooltip("Kurze Noise-Dauer nach den beiden erklärten Beispielmustern.")]
        private float trainingExampleNoiseSeconds = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Fixationszeit im Training, falls die Eye-Tracking-Kontrolle ausgeschaltet ist.")]
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
                // Auto Start ist nur für technische Tests gedacht und überspringt
                // deshalb den vorgeschalteten Welcome-/Trainingsschritt.
                trainingCompleted = true;
                StartSession();
                return;
            }

            ShowWelcomeScreen();
        }

        private void Update()
        {
            // Start, Training und Abbruch werden hier abgefragt. Die Antworten A/B
            // meldet weiterhin der Keyboard Controller.
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
            // Der Welcome Screen ist der ruhige Ausgangspunkt vor Training und
            // Hauptversuch. Hier werden noch keine Dateien oder Blickdaten angelegt.
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
                "Keep looking at the fixation cross.\n" +
                "First, both categories will be explained.\n" +
                "Afterwards you can practise the complete sequence.\n\n" +
                "RESPONSE KEYS\n" + BuildResponsePrompt() + "\n\n" +
                CheckerboardKeyboardController.GetReadableKeyName(continueTrainingKey) +
                " = CONTINUE");
            yield return WaitForTrainingAdvance();

            // Die beiden deutlichen Beispiele erklären nur, was mit A und B gemeint
            // ist. Erst danach kommen die gemischten Übungstrials ohne Hinweistext.
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
                    leaveNoiseVisible: false);
                if (!IsTrainingActive)
                {
                    yield break;
                }

                sessionState = CheckerboardSessionState.TrainingInstructions;
                stimulus.ShowResponsePrompt(string.Format(
                    CultureInfo.InvariantCulture,
                    "THIS WAS {0}\n\n{1}\n\n{2} = CONTINUE",
                    GetCategoryLabel(example),
                    GetCategoryDescription(example),
                    CheckerboardKeyboardController.GetReadableKeyName(
                        continueTrainingKey)));
                yield return WaitForTrainingAdvance();
            }

            sessionState = CheckerboardSessionState.TrainingInstructions;
            stimulus.ShowResponsePrompt(
                "PRACTICE TRIALS\n\n" +
                "The category labels will no longer be shown.\n" +
                "After each pattern, respond while the noise is visible.\n" +
                "Keep looking at the fixation cross.\n" +
                "There is no correct/incorrect feedback.\n\n" +
                CheckerboardKeyboardController.GetReadableKeyName(continueTrainingKey) +
                " = CONTINUE");
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
                    leaveNoiseVisible: true);
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

                stimulus.Hide();
                if (interTrialSeconds > 0f)
                {
                    yield return WaitForTrainingSeconds(interTrialSeconds);
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
            bool leaveNoiseVisible)
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
            yield return WaitForTrainingSeconds(stimulusDurationSeconds);
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
            // Training und Hauptversuch verwenden dieselbe erste FOV-, Augen- und
            // Zoom-Bedingung. Nur l wird durch den jeweiligen Übungswert ersetzt.
            if (angularDiametersDegrees != null && angularDiametersDegrees.Count > 0)
            {
                stimulus.SetAngularDiameter(angularDiametersDegrees[0]);
            }

            if (eyePresentations != null && eyePresentations.Count > 0)
            {
                stimulus.SetEyePresentation(eyePresentations[0]);
            }

            if (contentZoomValues != null && contentZoomValues.Count > 0)
            {
                stimulus.SetContentZoom(contentZoomValues[0]);
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
                "THIS IS {0}\n\n{1}\n\n{2} = SHOW EXAMPLE",
                GetCategoryLabel(response),
                GetCategoryDescription(response),
                CheckerboardKeyboardController.GetReadableKeyName(
                    continueTrainingKey));
        }

        private string GetCategoryDescription(CheckerboardCurvatureResponse response)
        {
            return response == CheckerboardCurvatureResponse.Convex
                ? "The pattern appears to bulge outward, like the surface of a ball."
                : "The pattern appears to curve inward, like the inside of a bowl.";
        }

        private string GetCategoryLabel(CheckerboardCurvatureResponse response)
        {
            return response == CheckerboardCurvatureResponse.Convex
                ? categoryAResponseText
                : categoryBResponseText;
        }

        public bool StartSession()
        {
            // Diese Methode prüft die Szene, erzeugt alle Trialkombinationen,
            // legt den Messordner an und startet danach die erste Präsentation.
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

            // Die Tasten stehen bewusst beim Experiment Manager. Dadurch gelten
            // die neuen Hoch-/Runter-Standardwerte auch in bereits vorhandenen Szenen.
            ApplyResponseKeySettings();

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
                CheckerboardKeyboardController.GetReadableKeyName(
                    continueTrainingKey) +
                " = START WHEN READY");
            WriteEyeTrackingMarker("ExperimentReadyScreenShown");
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
                "Zoom={6:F2}, Versuch {7}. Stimulus={8:F3}s, danach Noise bis zur Antwort.",
                currentTrialNumber,
                totalTrials,
                presentationCount,
                currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees,
                currentTrial.VisualSpaceL,
                currentTrial.ContentZoom,
                currentTrial.AttemptNumber,
                stimulusDurationSeconds),
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
            // Im Training wird die Antwort nur als Tastendruck verwendet. Sie wird
            // nicht bewertet und nicht in die Ergebnisdateien übernommen.
            if (sessionState == CheckerboardSessionState.TrainingWaitingForResponse &&
                response != CheckerboardCurvatureResponse.None)
            {
                trainingResponseReceived = true;
                return;
            }

            // Im Hauptversuch wird die Antwort während der Noise-Maske angenommen.
            // Danach wird genau ein Ergebnis geschrieben.
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
            // Hier werden Trialbedingung, Zeitpunkte und Blickstatus in einem Objekt
            // gesammelt. Die Dateiklasse schreibt dieses Objekt anschließend als CSV.
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
                "stimulus_duration_s={6:F4};noise_until_response=1;response_timeout_s={7:F4};" +
                "category_a_key={8};category_b_key={9};response_keys_swapped={10}",
                CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "session"),
                randomSeed,
                trialPlan.Count,
                sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                VisualSpaceRadialMapping.MappingVersion,
                stimulusDurationSeconds,
                responseTimeoutSeconds,
                ConvexResponseKeyName,
                ConcaveResponseKeyName,
                ResponseKeysSwapped ? 1 : 0));
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

        private void ApplyResponseKeySettings()
        {
            keyboardController?.SetResponseKeys(
                concaveResponseKey,
                convexResponseKey);
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
            // Der Marker steht zusätzlich in der Eye-Tracking-Datei. Dadurch kann
            // man Blicksamples später der gerade gezeigten Bedingung zuordnen.
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "circular_aperture={8};grid_spacing_deg={9:F3};" +
                "grid_spacing_uv={10:F6};visual_space_l={11:F4};content_zoom={12:F4};" +
                "stimulus_duration_s={13:F4};noise_until_response=1;response_timeout_s={14:F4};" +
                "category_a_key={15};category_b_key={16};response_keys_swapped={17}",
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
                responseTimeoutSeconds,
                ConvexResponseKeyName,
                ConcaveResponseKeyName,
                ResponseKeysSwapped ? 1 : 0);
        }

        private string BuildResponsePrompt()
        {
            Key convexKey = keyboardController.GetKeyForResponse(
                CheckerboardCurvatureResponse.Convex);
            Key concaveKey = keyboardController.GetKeyForResponse(
                CheckerboardCurvatureResponse.Concave);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} = {1}\n\n{2} = {3}",
                CheckerboardKeyboardController.GetReadableKeyName(convexKey),
                categoryAResponseText,
                CheckerboardKeyboardController.GetReadableKeyName(concaveKey),
                categoryBResponseText);
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
                "WELCOME TO THE VISUAL PERCEPTION EXPERIMENT\n\n" +
                "{0}" +
                "{1} = PRACTICE\n" +
                "{2} = START EXPERIMENT\n" +
                "{3} = ABORT CURRENT RUN\n\n" +
                "{4}\n\n" +
                "RESPONSE KEYS\n{5}",
                noticeLine,
                CheckerboardKeyboardController.GetReadableKeyName(trainingKey),
                CheckerboardKeyboardController.GetReadableKeyName(startSessionKey),
                CheckerboardKeyboardController.GetReadableKeyName(abortSessionKey),
                practiceState,
                BuildResponsePrompt());
        }

        private void CaptureFixationAtTrialEnd()
        {
            // Das Fixationskreuz bleibt auch in der Noise-Phase sichtbar. Deshalb
            // werden die letzten Blickwerte erst bei Antwort oder Abbruch eingefroren.
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
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
