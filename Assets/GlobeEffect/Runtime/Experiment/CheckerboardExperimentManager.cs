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
        PreStimulus,
        BlockPause // Pause zwischen Viertelblöcken; der Versuchsleiter setzt mit F5 fort.
    }

    /// <summary>
    /// Steuert den ganzen Checkerboard-Versuch.
    ///
    /// So läuft ein Durchgang ab: Die Person schaut auf das Kreuz. Liegt der Blick
    /// ruhig genug, kommt kurz das Schachbrett. Direkt danach beginnt die
    /// Antwortphase, in der die Person konvex oder konkav drückt. Dabei ist die
    /// Noise-Maske zu sehen; nach der Antwort folgt die graue Fläche mit Kreuz
    /// als Phase vor dem nächsten Schachbrett. (Der frühere Ablauf B mit grauer
    /// Fläche beim Antworten wurde am 22.10.2026 entfernt.)
    ///
    /// Welches k gezeigt wird, steht vorher fest. Die Person kann daran nichts
    /// verändern.
    ///
    /// Vor dem richtigen Versuch kann man dieselben Schritte erst einmal üben.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class CheckerboardExperimentManager : ExperimentManagerBase, IPreviewTakeoverManager
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
        private CheckerboardFixationMonitor fixationMonitor;

        [Header("Session")]
        [SerializeField]
        private string sessionLabel = "checkerboard_pilot";

        [Header("Trial Plan")]
        [SerializeField]
        [Tooltip("Merlitz instrument k values. 1 = straight, 0.5 = circle condition, 0 = angle condition. " +
            "Same parameter definition as the random dots; not the old visual-space l mapping.")]
        [FormerlySerializedAs("visualSpaceLValues")]
        private List<float> instrumentDistortionKValues = new() { 1.2f, 1f, 0.8f, 0.6f, 0.5f, 0.4f, 0.2f };

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Magnification m used for every checkerboard in trials, practice and examples. Default 10. " +
            "Try other values to see how m changes the distortion. m = 1 makes every k look straight.")]
        private float instrumentMagnificationM = CheckerboardInstrumentMapping.DefaultMagnificationM;

        // Die Abbildung der Trials: festes Quellgitter (Standard). Die randnormierte
        // Variante ist noch im Code, aber im Inspector ausgeblendet (22.10.2026).
        [SerializeField]
        [Tooltip("Merlitz instrument mapping with a fixed source grid. No extra scaling at the window edge.")]
        private bool testFixedSourceGrid = true;

        [SerializeField]
        [Tooltip("Hidden option: same instrument mapping, source grid normalized at the window edge.")]
        private bool testEdgeNormalizedGrid;

        [SerializeField]
        [Tooltip("How often each k value is shown, in the same order as the k list. Missing entries use " +
            "Repeats Per Condition.")]
        [FormerlySerializedAs("repeatsPerLValue")]
        private List<int> repeatsPerKValue = new();

        // Vorschlag des Betreuers: Abwechselnde Polarität soll Nachbildeffekte
        // verringern (jede Stelle ist über viele Trials im Mittel grau). Ob das
        // die Teilnehmer irritiert, ist offen; deshalb nur als Option, Standard aus.
        [SerializeField]
        [Tooltip("Black and white squares swap from one trial to the next. The first trial is normal, " +
            "repeated trials count too. In the practice they swap every two trials; the two examples " +
            "stay normal. The noise does not change.")]
        private bool invertCheckerboardEachTrial;

        [Header("Timing")]
        [FormerlySerializedAs("stimulusDurationSeconds")]
        [SerializeField, Min(0.01f)]
        [Tooltip("How long the checkerboard is shown (s). 0.6 = 600 ms.")]
        private float patternSeconds = 0.6f;

        [FormerlySerializedAs("responseTimeoutSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("How long the participant has to answer (s). 0 = no limit.")]
        private float answerTimeoutSeconds = 5f;

        [SerializeField, Min(0f)]
        [Tooltip("Minimum time before the next checkerboard (s): grey in order A, noise in order B. With " +
            "gaze check on it also waits until the participant looks at the cross.")]
        private float preStimulusSeconds = 0.5f;

        [Header("Response Keys")]
        [SerializeField]
        private Key convexResponseKey = Key.UpArrow;

        [SerializeField]
        private Key concaveResponseKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Answers can also be given with the VR controller (trigger and trackpad click). The arrow " +
            "keys keep working.")]
        private bool useVrControllerButtons = true;

        // Früher war das ein Häkchen "Swap Response Buttons". Aus = erster
        // Eintrag, An = zweiter Eintrag; gespeicherte Szenen behalten ihren Wert.
        [FormerlySerializedAs("swapResponseButtons")]
        [SerializeField]
        [Tooltip("Which controller button means convex and which concave. Do not change it during a " +
            "session.")]
        private VrControllerMapping controllerMapping = VrControllerMapping.TrackpadConcaveTriggerConvex;

        [FormerlySerializedAs("categoryAResponseText")]
        [SerializeField]
        [Tooltip("Word the participant sees for convex (bulging out). The data file always says Convex.")]
        private string categoryALabel = "CATEGORY A";

        [FormerlySerializedAs("categoryBResponseText")]
        [SerializeField]
        [Tooltip("Word the participant sees for concave (bulging in). The data file always says Concave.")]
        private string categoryBLabel = "CATEGORY B";

        [Header("Practice")]
        [SerializeField]
        [Tooltip("Off = preview shows the checkerboard. On = preview shows the noise. Can be switched " +
            "while the preview runs.")]
        private bool previewShowsNoise;

        [SerializeField]
        [Tooltip("During the preview: plays checkerboard, then noise, then grey. On the grey screen you " +
            "can check if an afterimage of the checkerboard stays.")]
        private Key maskDemoKey = Key.M;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Afterimage test: how long the checkerboard is shown (s).")]
        private float maskDemoPatternSeconds = 2f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Afterimage test: how long the noise is shown after it (s).")]
        private float maskDemoMaskSeconds = 1f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Afterimage test: how long the grey screen is shown at the end (s).")]
        private float maskDemoGraySeconds = 3f;

        [SerializeField]
        [Tooltip("Key to go to the next step in the practice. It also starts the first trial.")]
        private Key continueTrainingKey = Key.Space;

        [SerializeField]
        [Tooltip("If on, the session can only start after the practice was done once.")]
        private bool requireTrainingBeforeSession = true;

        [FormerlySerializedAs("trainingCategoryAVisualSpaceL")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("k value of the convex example in the practice. Should be easy to see but inside " +
            "the tested range.")]
        private float trainingExampleA = 1.2f;

        [FormerlySerializedAs("trainingCategoryBVisualSpaceL")]
        [SerializeField, Range(0f, 2f)]
        [Tooltip("k value of the concave example in the practice. Should be easy to see but inside " +
            "the tested range.")]
        private float trainingExampleB = 0.2f;

        // Zwei feste Übungsteile, nicht gemischt: erst deutlich verschiedene k,
        // dann k aus dem mittleren Bereich. Niedrig und hoch wechseln sich ab.
        // Die alten Felder trainingKValues/trainingRepeatsPerValue (gemischt) sind
        // weggefallen; ihre Werte in alten Szenen werden nicht übernommen.
        [SerializeField]
        [Tooltip("Practice part 1: k values shown in exactly this order, not shuffled. Use clearly " +
            "different values, low and high taking turns. No right/wrong feedback.")]
        private List<float> trainingEasyKValues = new()
            { 0f, 1.4f, 0.2f, 1.2f, 0f, 1.4f, 0.2f, 1.2f, 0f, 1.4f, 0.2f, 1.2f };

        [SerializeField]
        [Tooltip("Practice part 2: k values shown in exactly this order, not shuffled. Values from the " +
            "middle, low and high taking turns. No right/wrong feedback.")]
        private List<float> trainingHardKValues = new()
            { 0.4f, 1.0f, 0.5f, 1.2f, 0.6f, 0.9f, 0.4f, 1.1f, 0.5f, 1.0f, 0.6f, 1.2f };

        [FormerlySerializedAs("trainingExampleStimulusSeconds")]
        [SerializeField, Min(0.1f)]
        [Tooltip("How long the two clear examples are shown (s). The practice trials after them are as " +
            "short as in the real test.")]
        private float trainingExampleSeconds = 2f;

        [FormerlySerializedAs("trainingExampleNoiseSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("What comes after each example and for how long (s): noise in order A, grey in order B.")]
        private float trainingNoiseSeconds = 0.5f;

        [FormerlySerializedAs("trainingFixationSecondsWithoutEyeTracking")]
        [SerializeField, Min(0f)]
        [Tooltip("Minimum wait before each practice image when the gaze check is off (s). Only used if " +
            "larger than Time Before Checkerboard.")]
        private float trainingFixationSeconds = 0.5f;

        [Header("Current Status")]
        [SerializeField]
        private CheckerboardSessionState sessionState = CheckerboardSessionState.Idle;

        private IReadOnlyList<CheckerboardTrial> trialPlan;
        private CheckerboardTrialQueue trialQueue;
        private CheckerboardGridMapping trainingGridMapping;

        private List<CheckerboardGridMapping> SelectedGridMappings()
        {
            var mappings = new List<CheckerboardGridMapping>();
            if (testFixedSourceGrid) mappings.Add(CheckerboardGridMapping.FixedSourceGrid);
            if (testEdgeNormalizedGrid) mappings.Add(CheckerboardGridMapping.EdgeNormalized);
            if (mappings.Count == 0)
                throw new ArgumentException("Mindestens eine Checkerboard-Variante auswählen.");
            return mappings;
        }
        private CheckerboardTrial currentTrial;
        private CheckerboardExperimentFiles experimentFiles;
        private double responseWindowStartUnitySeconds;
        private double preStimulusEndUnitySeconds;
        private Coroutine presentationCoroutine;
        private Coroutine trainingCoroutine;
        private bool keyboardEventsSubscribed;
        private bool trainingCompleted;
        private bool trainingAdvanceRequested;
        private bool trainingResponseReceived;
        private CheckerboardCurvatureResponse trainingResponse;
        private double trainingResponseUnitySeconds;
        private bool previewShowingNoise;
        private Coroutine maskDemoCoroutine;

        // Die Blickwerte vom Ende des Durchgangs. Leer (null), solange der
        // Durchgang noch läuft.
        //
        // Das Kreuz bleibt auch während der Noise-Maske stehen, die Person soll
        // ja weiter dorthin schauen. Deshalb werden die Blickwerte erst
        // festgehalten, wenn die Antwort kommt oder abgebrochen wird.
        private FixationSnapshot? gazeAtTrialEnd;
        private int lastFixationCheckFrame = -1;

        public CheckerboardSessionState SessionState => sessionState;
        public bool TrainingCompleted => trainingCompleted;
        public VrCheckerboardStimulus Stimulus => stimulus;
        UnityEngine.Object IPreviewTakeoverManager.PreviewStimulus => stimulus;

        public bool InvertCheckerboardEachTrial => invertCheckerboardEachTrial;
        public float InstrumentMagnificationM => instrumentMagnificationM;
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
                or State.WaitingForResponse
                or State.BlockPause;

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
            if (!CheckRequiredRecording()) return;
            // Schachbrett/Noise-Maske in der Vorschau live umschalten.
            if (previewActive && maskDemoCoroutine == null && previewShowsNoise != previewShowingNoise)
                ShowPreviewDisplay();
            // Hier werden nur die Tasten vom Versuchsleiter abgefragt: starten,
            // trainieren, abbrechen, weiter. Die Antworttasten A und B laufen
            // weiter über den Keyboard Controller.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!IsSessionActive && !IsTrainingActive && keyboard[previewKey].wasPressedThisFrame)
                {
                    TogglePreview();
                    return;
                }

                if (previewActive && keyboard[abortSessionKey].wasPressedThisFrame)
                {
                    StopPreview();
                    return;
                }

                if (previewActive && keyboard[maskDemoKey].wasPressedThisFrame)
                {
                    PlayMaskDemo();
                    return;
                }

                if (sessionState == State.BlockPause && keyboard[startSessionKey].wasPressedThisFrame)
                {
                    ContinueAfterBlockPause();
                    return;
                }

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
                && (!requireFixation || (fixationMonitor != null
                    && fixationMonitor.IsReadyForPresentation(maxSampleAgeSeconds))))
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
            RestorePreviewSettings();
            UnsubscribeKeyboardEvents();
            StopAndClear(ref trainingCoroutine);
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }
        }

        public void ShowWelcomeScreen(string notice = "")
        {
            RestorePreviewSettings();
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
            ResetCheckerboardPolarity();
            sessionState = State.Welcome;
            stimulus.ShowResponsePrompt(BuildWelcomePrompt(notice));
        }

        /// <summary>Reine Live-Vorschau: Werte direkt am Stimulus ändern, keine Messung.</summary>
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
            previewActive = true;
            ShowPreviewDisplay();
        }

        public bool IsMaskDemoRunning => maskDemoCoroutine != null;

        /// <summary>
        /// Nachbild-Test in der Vorschau: Schachbrett, dann die eingestellte Maske, dann
        /// graue Fläche. So lässt sich vergleichen, welche Maske das Nachbild löscht.
        /// </summary>
        public void PlayMaskDemo()
        {
            if (!previewActive) return;
            StopAndClear(ref maskDemoCoroutine);
            // Mit "Invert Checkerboard Each Trial" wechselt jeder Druck die
            // Polarität, wie von Trial zu Trial. Nach der Vorschau wird sie
            // zusammen mit allen anderen Werten zurückgesetzt.
            if (invertCheckerboardEachTrial)
                stimulus.SetCheckerboardInverted(!stimulus.CheckerboardInverted);
            maskDemoCoroutine = StartCoroutine(RunMaskDemo());
        }

        private IEnumerator RunMaskDemo()
        {
            stimulus.Show();
            yield return new WaitForSecondsRealtime(maskDemoPatternSeconds);
            stimulus.ShowNoise(unchecked(randomSeed + Time.frameCount));
            yield return new WaitForSecondsRealtime(maskDemoMaskSeconds);
            stimulus.ShowFixationOnly();
            yield return new WaitForSecondsRealtime(maskDemoGraySeconds);
            maskDemoCoroutine = null;
            ShowPreviewDisplay();
        }

        private void ShowPreviewDisplay()
        {
            previewShowingNoise = previewShowsNoise;
            if (previewShowsNoise) stimulus.ShowNoise(randomSeed);
            else stimulus.Show();
        }

        public void StopPreview()
        {
            if (!previewActive) return;
            ShowWelcomeScreen();
        }

        private void RestorePreviewSettings()
        {
            if (!previewActive) return;
            StopAndClear(ref maskDemoCoroutine);
            previewActive = false;
            if (stimulus != null && !string.IsNullOrEmpty(settingsBeforePreview))
                stimulus.RestorePreviewSettings(settingsBeforePreview);
            // Eine übernommene Darstellung bleibt nach der Vorschau erhalten.
            if (stimulus != null && lookToKeepAfterPreview != null)
                stimulus.RestorePreviewSettings(lookToKeepAfterPreview);
            settingsBeforePreview = null;
            lookToKeepAfterPreview = null;
        }

        /// <summary>
        /// Übernimmt FOV, Augenmodus und festes m der Vorschau in den Versuchsplan (Listen werden
        /// ersetzt) und behält die Darstellung (Edge Softness, Kreisblende, Gitterabstand,
        /// Farben, Fixationskreuz, Noise). Die k-Liste bleibt unverändert. Beim ersten Mal
        /// wird der bisherige Stand als Original gesichert.
        /// </summary>
        public bool TakeOverPreviewValues()
        {
            if (IsSessionActive || IsTrainingActive) return false;
            ResolveReferences();
            if (stimulus == null) return false;
            if (!HasValuesBeforePreviewTakeover)
                valuesBeforePreviewTakeover = CaptureExperimentValues();
            CheckerboardPreviewTakeover.CopyPreviewIntoPlan(stimulus, this);
            if (previewActive)
                lookToKeepAfterPreview = CheckerboardPreviewTakeover.CaptureLook(JsonUtility.ToJson(stimulus));
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

        /// <summary>Aktueller Versuchsplan plus Darstellung, so wie sie nach der Vorschau gelten.</summary>
        public string CaptureExperimentValues()
        {
            ResolveReferences();
            string look = lookToKeepAfterPreview
                ?? (previewActive ? settingsBeforePreview : null)
                ?? (stimulus != null ? JsonUtility.ToJson(stimulus) : "{}");
            return CheckerboardPreviewTakeover.Capture(this, look);
        }

        public void ApplyExperimentValues(string valuesJson)
        {
            CheckerboardPreviewTakeover.ApplyPlan(valuesJson, this);
            ResolveReferences();
            if (stimulus == null) return;
            string look = CheckerboardPreviewTakeover.LookOf(valuesJson);
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

        public bool StartTraining()
        {
            if (IsSessionActive || IsTrainingActive)
            {
                return false;
            }

            StopPreview();
            if (!CheckSceneSetup()) return false;

            try
            {
                SelectedGridMappings();
                float fov = fieldOfViewValues != null && fieldOfViewValues.Count > 0
                    ? fieldOfViewValues[0] : stimulus.AngularDiameterDegrees;
                var values = new List<float>();
                if (trainingEasyKValues != null) values.AddRange(trainingEasyKValues);
                if (trainingHardKValues != null) values.AddRange(trainingHardKValues);
                values.Add(trainingExampleA);
                values.Add(trainingExampleB);
                foreach (float k in values)
                    CheckerboardInstrumentMapping.ValidateParameters(fov,
                        instrumentMagnificationM, k,
                        stimulus.ApertureEdgeSoftnessDegrees, stimulus.UseCircularAperture);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError("Checkerboard-Training: " + exception.Message, this);
                return false;
            }

            ApplyResponseKeySettings();
            StopAndClear(ref trainingCoroutine);
            StopAndClear(ref presentationCoroutine);
            trainingCompleted = false;
            trainingAdvanceRequested = false;
            trainingResponseReceived = false;
            currentTrial = null;
            practiceLog.Clear();
            trainingCoroutine = StartCoroutine(RunTrainingSequence());
            return true;
        }

        /// <summary>
        /// Weiter im Training, wie die Continue-Taste. Für Tests und falls der
        /// Versuchsleiter es von außen auslösen möchte.
        /// </summary>
        public void ContinueTraining()
        {
            if (IsTrainingActive) trainingAdvanceRequested = true;
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
            // Danach folgen zwei Übungsteile in fester Reihenfolge, ohne Hinweis
            // und ohne Rückmeldung, ob eine Antwort richtig war. Das ist Absicht,
            // sonst würde man das Ergebnis verfälschen. Die Antworten sieht nur
            // der Versuchsleiter im Experiment Monitor.
            //
            // Beide Beispiele haben dieselbe (normale) Polarität. Sonst würde die
            // Polarität die Kategorie verraten.
            var examples = new[]
            {
                CheckerboardCurvatureResponse.Convex,
                CheckerboardCurvatureResponse.Concave
            };
            int presentationIndex = 0;
            foreach (CheckerboardCurvatureResponse example in examples)
            {
                foreach (CheckerboardGridMapping mapping in SelectedGridMappings())
                {
                    trainingGridMapping = mapping;
                    sessionState = State.TrainingInstructions;
                    stimulus.ShowResponsePrompt(BuildTrainingCategoryIntroduction(example));
                    yield return WaitForTrainingAdvance();

                    presentationIndex++;
                    float exampleK =
                        example == CheckerboardCurvatureResponse.Convex ? trainingExampleA : trainingExampleB;
                    yield return ShowTrainingPattern(exampleK, unchecked(randomSeed + 50000 + presentationIndex),
                        waitForResponse: false, patternDurationSeconds: trainingExampleSeconds, inverted: false);
                    if (!IsTrainingActive)
                    {
                        yield break;
                    }

                    sessionState = State.TrainingInstructions;
                    stimulus.ShowResponsePrompt(BuildCategoryText(example) + "\n\n" +
                        ContinueKeyName + " = CONTINUE");
                    yield return WaitForTrainingAdvance();
                }
            }

            // Teil 1 leicht, Teil 2 schwerer. Jeder Teil in der eingetragenen
            // Reihenfolge, nicht gemischt. Vor jedem Teil steht ein kurzer Text;
            // dort kann der Versuchsleiter nach einem Blick auf den Monitor noch
            // einmal erklären. Der Hinweis "näher am konvexen/konkaven Beispiel"
            // steht nur bei Teil 1.
            var parts = new[]
            {
                (number: 1, values: CopyKValues(trainingEasyKValues), closerHint: true),
                (number: 2, values: CopyKValues(trainingHardKValues), closerHint: false)
            };
            int practiceIndex = 0;
            foreach (var part in parts)
            {
                if (part.values.Count == 0)
                {
                    continue;
                }

                sessionState = State.TrainingInstructions;
                stimulus.ShowResponsePrompt(BuildPracticePartPrompt(part.number));
                yield return WaitForTrainingAdvance();

                foreach (float practiceK in part.values)
                {
                    practiceIndex++;
                    bool inverted = CheckerboardPolarity.InvertedForPractice(
                        invertCheckerboardEachTrial, practiceIndex);
                    foreach (CheckerboardGridMapping mapping in SelectedGridMappings())
                    {
                        trainingGridMapping = mapping;
                        presentationIndex++;
                        yield return ShowTrainingPattern(practiceK,
                            unchecked(randomSeed + 60000 + presentationIndex),
                            waitForResponse: true, patternDurationSeconds: patternSeconds, inverted: inverted);
                        if (!IsTrainingActive)
                        {
                            yield break;
                        }

                        // Warten, bis geantwortet wurde oder die Antwortzeit um ist.
                        trainingResponseReceived = false;
                        trainingResponse = CheckerboardCurvatureResponse.None;
                        sessionState = State.TrainingWaitingForResponse;
                        double responseStart = Time.realtimeSinceStartupAsDouble;
                        double responseDeadline = answerTimeoutSeconds > 0f
                            ? responseStart + answerTimeoutSeconds
                            : double.PositiveInfinity;
                        while (IsTrainingActive && !trainingResponseReceived
                            && Time.realtimeSinceStartupAsDouble < responseDeadline)
                        {
                            yield return null;
                        }

                        if (!IsTrainingActive)
                        {
                            yield break;
                        }

                        LogPracticeAnswer(part.number, practiceK, part.closerHint, inverted, responseStart);
                    }
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
            float instrumentDistortionK, int noiseSeed, bool waitForResponse, float patternDurationSeconds,
            bool inverted)
        {
            // Ein Übungsdurchgang läuft genauso ab wie im Versuch: graue Fläche vor
            // dem Muster -> Muster -> Antwortphase mit Noise-Maske.
            PrepareTrainingCondition(instrumentDistortionK, inverted);
            if (fixationMonitor != null)
            {
                fixationMonitor.ResetFixationWindow();
            }

            sessionState = State.TrainingFixation;
            ShowNoiseOrGray(noise: false, unchecked(noiseSeed + 0x4A31));
            yield return WaitForTrainingSeconds(
                Mathf.Max(preStimulusSeconds, requireFixation ? 0f : trainingFixationSeconds));
            while (requireFixation && IsTrainingActive
                && fixationMonitor != null && !fixationMonitor.IsReadyForPresentation(maxSampleAgeSeconds))
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

            // Antwortphase mit Noise-Maske.
            sessionState = State.TrainingNoise;
            ShowNoiseOrGray(noise: true, noiseSeed);
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

        private void PrepareTrainingCondition(float instrumentDistortionK, bool inverted)
        {
            // Im Training soll alles genauso aussehen wie später im Versuch.
            // Deshalb werden FOV, Augenmodus und Zoom aus der ersten echten
            // Bedingung übernommen. Nur k wird durch den Übungswert ersetzt.
            if (fieldOfViewValues != null && fieldOfViewValues.Count > 0)
            {
                stimulus.SetAngularDiameter(fieldOfViewValues[0]);
            }

            if (eyePresentations != null && eyePresentations.Count > 0)
            {
                stimulus.SetEyePresentation(eyePresentations[0]);
            }

            CheckerboardInstrumentMapping.ValidateParameters(stimulus.AngularDiameterDegrees,
                instrumentMagnificationM, instrumentDistortionK,
                stimulus.ApertureEdgeSoftnessDegrees, stimulus.UseCircularAperture);
            // Die tanh-Erweiterung gibt es nur in der Live-Vorschau, nie in Übung oder Trials.
            stimulus.SetPincushionExtensionEnabled(false);
            stimulus.SetInstrumentMagnificationM(instrumentMagnificationM);
            stimulus.SetInstrumentDistortionK(instrumentDistortionK);
            stimulus.SetGridMapping(trainingGridMapping);
            stimulus.SetCheckerboardInverted(inverted);
        }

        // Kopie der Übungsliste in der eingetragenen Reihenfolge, nicht gemischt.
        private static List<float> CopyKValues(List<float> values)
        {
            var copy = new List<float>();
            if (values != null)
            {
                foreach (float value in values)
                {
                    copy.Add(Mathf.Clamp(value, 0f, 2f));
                }
            }

            return copy;
        }

        private string BuildPracticePartPrompt(int part)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "PRACTICE PART {0}\n\n" +
                "Each pattern lasts {1:0} ms.\n" +
                "Answer while the noise is visible.\n" +
                "Keep looking at the cross.\n\n" +
                "{2}\n\n" +
                "{3} = CONTINUE",
                part,
                patternSeconds * 1000f,
                BuildResponsePrompt(),
                ContinueKeyName);
        }

        // Eine Zeile im Experiment Monitor. Nur der Versuchsleiter sieht sie;
        // gespeichert wird nichts.
        private void LogPracticeAnswer(int part, float k, bool closerHint, bool inverted, double responseStart)
        {
            CheckerboardCurvatureResponse closer = closerHint
                ? PracticeAnswerLog.CloserToExample(k, trainingExampleA, trainingExampleB)
                : CheckerboardCurvatureResponse.None;
            var notes = new List<string>();
            // Die Variante nur nennen, wenn die ausgeblendete randnormierte Variante aktiv ist.
            if (trainingGridMapping != CheckerboardGridMapping.FixedSourceGrid)
                notes.Add(trainingGridMapping.ToString());
            if (!trainingResponseReceived) notes.Add("no answer");
            if (inverted) notes.Add("inverted");
            practiceLog.Add("Part " + part, k,
                trainingResponseReceived ? trainingResponse : CheckerboardCurvatureResponse.None,
                trainingResponseUnitySeconds - responseStart, closer, string.Join(", ", notes));
        }

        // Nach Training und Sitzung wieder normale Polarität, damit eine spätere
        // Vorschau nicht zufällig das invertierte Muster des letzten Trials zeigt.
        private void ResetCheckerboardPolarity()
        {
            if (stimulus != null && stimulus.CheckerboardInverted)
            {
                stimulus.SetCheckerboardInverted(false);
            }
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

            StopPreview();

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
                    fieldOfViewValues, eyePresentations, instrumentDistortionKValues, repeatsPerCondition, randomSeed,
                    repeatsPerKValue, instrumentMagnificationM, SelectedGridMappings());
                foreach (CheckerboardTrial planned in trialPlan)
                    CheckerboardInstrumentMapping.ValidateParameters(planned.AngularDiameterDegrees,
                        planned.InstrumentMagnificationM, planned.InstrumentDistortionK,
                        stimulus.ApertureEdgeSoftnessDegrees, stimulus.UseCircularAperture);
                trialQueue = new CheckerboardTrialQueue(trialPlan);

                DateTime sessionStartUtc = DateTime.UtcNow;
                experimentFiles = new CheckerboardExperimentFiles(
                    ExperimentOutputPath.Resolve(outputRoot), participantId, sessionLabel,
                    sessionStartUtc, randomSeed);
                experimentFiles.WritePlan(
                    trialPlan, stimulus.GridLineSpacingDegrees, patternSeconds,
                    preStimulusSeconds, answerTimeoutSeconds, ConvexResponseKeyName,
                    ConcaveResponseKeyName, ResponseKeysSwapped, controllerMapping,
                    invertCheckerboardEachTrial);
                activeSessionFolder = experimentFiles.SessionFolder;
                StartEyeTracking(sessionStartUtc);
                experimentFiles.WriteSessionSettings(ExperimentSessionSettings.Capture(
                    this, stimulus, fixationMonitor, keyboardController, eyeTrackingToolbox));
            }
            catch (Exception exception)
            {
                EndSession(State.Aborted);
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
            pausesTaken = 0;
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
            stimulus.SetPincushionExtensionEnabled(false);
            stimulus.SetInstrumentMagnificationM(currentTrial.InstrumentMagnificationM);
            stimulus.SetInstrumentDistortionK(currentTrial.InstrumentDistortionK);
            stimulus.SetGridMapping(currentTrial.GridMapping);
            // Abwechselnd pro gezeigtem Trial; Wiederholungen zählen mit.
            stimulus.SetCheckerboardInverted(CheckerboardPolarity.InvertedForPresentation(
                invertCheckerboardEachTrial, presentationCount));

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

            // Die Phase vor dem Schachbrett: graue Fläche mit Kreuz. Update zeigt das
            // Schachbrett, sobald die Mindestdauer um ist und der Blick ruhig liegt.
            sessionState = State.PreStimulus;
            preStimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble + preStimulusSeconds;
            ShowPhase("pre_stimulus", noise: false,
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
                "Trial {0}/{1}, Präsentation {2}: {3}, FOV={4:F1}°, k={5:F3}, " +
                "Versuch {6}. Stimulus={7:F3}s, danach Antwortphase mit {8}.",
                currentTrialNumber, totalTrials, presentationCount, currentTrial.EyePresentation,
                currentTrial.AngularDiameterDegrees, currentTrial.InstrumentDistortionK,
                currentTrial.AttemptNumber, patternSeconds,
                "Noise-Maske"), this);
        }

        private IEnumerator RunPresentationSequence(CheckerboardTrial presentedTrial)
        {
            // Die Soll-Dauer ist gleich. Unity schaltet aber nur an Frame-Grenzen
            // um; die tatsächlich verstrichene Softwarezeit wird mitgespeichert.
            // Das ist noch keine Messung des Displays mit einer Photodiode.
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

            // Ab jetzt läuft die Antwortphase mit der Noise-Maske. Jeder Durchgang
            // bekommt sein eigenes Noise-Muster.
            int noiseSeed = unchecked(
                randomSeed + presentedTrial.SequenceIndex * 1009 + presentedTrial.AttemptNumber * 9176);
            responseWindowStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            sessionState = State.WaitingForResponse;
            ShowPhase("response", noise: true, noiseSeed, answerTimeoutSeconds);

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
            // Im Training beendet der Tastendruck die Antwortphase. Er wird nicht
            // bewertet und landet in keiner Messdatei, nur im Experiment Monitor.
            // Ein zweiter Druck im selben Frame überschreibt die erste Antwort nicht.
            if (sessionState == State.TrainingWaitingForResponse
                && response != CheckerboardCurvatureResponse.None)
            {
                if (!trainingResponseReceived)
                {
                    trainingResponse = response;
                    trainingResponseUnitySeconds = Time.realtimeSinceStartupAsDouble;
                    trainingResponseReceived = true;
                }

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
            CheckerboardTrial answeredTrial = currentTrial;
            if (!CheckRequiredRecording()) return;
            if (requireFixation)
            {
                // Die Eingabe kann vor Manager.Update kommen. Auch dann muss der
                // Blick im Antwort-Frame noch geprüft werden, bevor die Antwort zählt.
                MonitorFixationDuringTrial();
                if (sessionState != State.WaitingForResponse || currentTrial != answeredTrial) return;
            }
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
            // Eingabe und Update können diese Prüfung im selben Frame aufrufen.
            // Die verstrichene Zeit darf dabei nicht zweimal gezählt werden.
            if (lastFixationCheckFrame == Time.frameCount) return;
            lastFixationCheckFrame = Time.frameCount;
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

            return new CheckerboardTrialResult
            {
                Trial = currentTrial,
                PresentationIndex = presentationCount,
                TrialStartUtc = trialStartUtc,
                TrialStartUnitySeconds = trialStartUnitySeconds,
                StimulusEndUnitySeconds = stimulusEnd,
                ResponseWindowStartUnitySeconds = responseWindowStart,
                TrialEndUnitySeconds = resultEndTime,
                ApertureEdgeSoftnessDegrees = stimulus.ApertureEdgeSoftnessDegrees,
                CircularApertureEnabled = stimulus.UseCircularAperture,
                GridLineSpacingDegrees = stimulus.GridLineSpacingDegrees,
                GridLineSpacingUv = stimulus.GridLineSpacingUv,
                Response = response,
                ValidForAnalysis = validForAnalysis,
                Gaze = gaze,
                LongestOffTargetSeconds = lookAway.LongestLookAwaySeconds,
                LongestInvalidGazeSeconds = lookAway.LongestNoDataSeconds,
                Status = status,
                CheckerboardInverted = stimulus.CheckerboardInverted
            };
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
            if (TryStartBlockPause())
            {
                return;
            }

            sessionState = State.InterTrial;
            BeginNextAttempt();
        }

        private bool TryStartBlockPause()
        {
            // Viertelblöcke: Nach 25, 50 und 75 % der geplanten gültigen Trials eine
            // Pause. Wiederholte ungültige Trials zählen nicht mit. Das rote Kreuz
            // bleibt stehen. Am Ende der Sitzung kommt keine Pause mehr.
            int nextPause = pausesTaken + 1;
            if (blocksPerSession <= 1 || nextPause >= blocksPerSession
                || trialQueue == null || trialQueue.Count == 0) return false;
            int boundary = Mathf.RoundToInt(totalTrials * nextPause / (float)blocksPerSession);
            if (validTrialsCompleted < boundary) return false;

            pausesTaken = nextPause;
            sessionState = State.BlockPause;
            stimulus.ShowPausePrompt(BuildBlockPausePrompt());
            WriteMarker("BlockPauseStarted;block={0};of={1};valid_trials={2}",
                pausesTaken, blocksPerSession, validTrialsCompleted);
            Debug.Log($"Pause nach Block {pausesTaken} von {blocksPerSession} ({validTrialsCompleted} gültige " +
                $"Trials). Weiter mit {ResponseInputController.GetReadableKeyName(startSessionKey)}.", this);
            return true;
        }

        /// <summary>Versuchsleiter beendet die Viertelblock-Pause (F5).</summary>
        public void ContinueAfterBlockPause()
        {
            if (sessionState != State.BlockPause) return;
            WriteMarker("BlockPauseEnded;block={0}", pausesTaken);
            sessionState = State.InterTrial;
            BeginNextAttempt();
        }


        private string BuildBlockPausePrompt()
        {
            // Der Text steht mittig. Die leeren Zeilen in der Mitte lassen das
            // rote Kreuz frei: Überschrift darüber, Hinweis darunter.
            return "SHORT BREAK\nBlock " + pausesTaken + " of " + blocksPerSession + " done.\n\n\n\n\n" +
                "Rest a moment if you like.\nThe experimenter will continue.";
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
                ResetCheckerboardPolarity();
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
                "noise_dot_deg={6:F4};noise_dot_px={7:F2};noise_refresh_hz={8:F2};" +
                "background_rgb={9:F3}/{10:F3}/{11:F3};noise_broadband_deg={6:F2}-{13:F2};" +
                "noise_octaves={14};{12}",
                ExperimentFilesBase.SanitizeIdentifier(participantId, "pilot"),
                ExperimentFilesBase.SanitizeIdentifier(sessionLabel, "session"),
                randomSeed, trialPlan.Count, sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                testFixedSourceGrid && testEdgeNormalizedGrid ? "mixed-instrument-grid-variants"
                    : CheckerboardInstrumentMapping.VersionFor(false,
                        testEdgeNormalizedGrid ? CheckerboardGridMapping.EdgeNormalized
                            : CheckerboardGridMapping.FixedSourceGrid),
                stimulus.NoiseDotSizeDegrees, stimulus.NoiseDotSizePixels, stimulus.NoiseRefreshRateHz,
                stimulus.BackgroundColor.r, stimulus.BackgroundColor.g, stimulus.BackgroundColor.b,
                BuildTimingAndResponseFields(), stimulus.NoiseBroadbandCoarsestDegrees,
                stimulus.NoiseOctaves);
        }

        // Ablauf, Zeiten und Tastenbelegung für die Marker SessionStart und
        // TrialStart. So stehen dieselben Angaben nur an einer Stelle.
        private string BuildTimingAndResponseFields()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "stimulus_duration_s={0:F4};pre_stimulus_s={1:F4};" +
                "response_timeout_s={2:F4};category_a_key={3};category_b_key={4};" +
                "response_keys_swapped={5};controller_mapping={6};invert_each_trial={7}",
                patternSeconds, preStimulusSeconds, answerTimeoutSeconds,
                ConvexResponseKeyName, ConcaveResponseKeyName, ResponseKeysSwapped ? 1 : 0,
                controllerMapping, invertCheckerboardEachTrial ? 1 : 0);
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

            if (requireFixation && (eyeTrackingToolbox == null
                || !fixationMonitor.Tracks(stimulus, eyeTrackingToolbox) || !stimulus.FixationTargetEnabled))
            {
                Debug.LogError("Blickkontrolle braucht die passende Toolbox, denselben Stimulus im " +
                    "Fixation Monitor und ein sichtbares Fixationskreuz.", this);
                return false;
            }

            if (concaveResponseKey == Key.None || convexResponseKey == Key.None
                || concaveResponseKey == convexResponseKey)
            {
                Debug.LogError("Für konkav und konvex zwei unterschiedliche Antworttasten einstellen.", this);
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
            lastFixationCheckFrame = -1;
        }

        private bool CheckRequiredRecording()
        {
            if (!IsSessionActive || !requireFixation
                || (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)) return true;
            AbortSession("eye_tracking_recording_stopped");
            Debug.LogError("Blickaufzeichnung wurde während der Messung beendet. Sitzung abgebrochen.", this);
            return false;
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
                "grid_spacing_uv={10:F6};instrument_distortion_k={11:F4};{12};" +
                "instrument_magnification_m={13:F3};mapping={14};checkerboard_inverted={15};" +
                "grid_mapping={16};grid_source_scale={17:F6}",
                presentationCount, trial.SequenceIndex, trial.ConditionIndex, trial.Repetition,
                trial.AttemptNumber, trial.EyePresentation, trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees, stimulus.UseCircularAperture,
                stimulus.GridLineSpacingDegrees, stimulus.GridLineSpacingUv, trial.InstrumentDistortionK,
                BuildTimingAndResponseFields(), trial.InstrumentMagnificationM,
                CheckerboardInstrumentMapping.VersionFor(false, trial.GridMapping),
                stimulus.CheckerboardInverted ? 1 : 0,
                trial.GridMapping, stimulus.GridSourceScale);
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
                ? keyboardController.BuildResponseLines("OUTWARD / GLOBE", "INWARD / DENT")
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
                CheckerboardKeyboardController.GetReadableKeyName(previewKey) + " = PREVIEW (NO DATA)\n\n" +
                practiceState + "\n\n" +
                "Always look at the cross.\n\n" +
                "RESPONSES\n" + BuildResponsePrompt();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            patternSeconds = Mathf.Max(0.01f, patternSeconds);
            answerTimeoutSeconds = Mathf.Max(0f, answerTimeoutSeconds);
            instrumentMagnificationM = Mathf.Clamp(instrumentMagnificationM, 1f, 20f);
            trainingExampleA = Mathf.Clamp(trainingExampleA, 0f, 2f);
            trainingExampleB = Mathf.Clamp(trainingExampleB, 0f, 2f);
            trainingNoiseSeconds = Mathf.Max(0f, trainingNoiseSeconds);
            foreach (List<float> values in new[] { trainingEasyKValues, trainingHardKValues })
            {
                if (values == null) continue;
                for (int index = 0; index < values.Count; index++)
                {
                    values[index] = Mathf.Clamp(values[index], 0f, 2f);
                }
            }

            trainingFixationSeconds = Mathf.Max(0f, trainingFixationSeconds);
            trainingExampleSeconds = Mathf.Max(0.1f, trainingExampleSeconds);
            preStimulusSeconds = Mathf.Max(0f, preStimulusSeconds);
        }
    }
}
