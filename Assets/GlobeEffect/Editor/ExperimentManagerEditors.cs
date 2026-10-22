using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEditor;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Editor
{
    /// <summary>
    /// Einstellungszentrale für beide Versuche. Darstellung und Blickkriterium
    /// werden direkt an den verknüpften Komponenten bearbeitet, nicht kopiert.
    /// Bestehende Szenen, Feldnamen und Zuständigkeiten bleiben erhalten.
    ///
    /// Alle sichtbaren Texte sind bewusst einfaches Englisch, damit auch Leute
    /// ohne Vorwissen den Versuch einstellen können.
    /// </summary>
    public abstract class ExperimentManagerEditor : UnityEditor.Editor
    {
        // Einfachere Anzeigenamen nur für Felder, deren Name sonst unverständlich
        // wäre. Die serialisierten Feldnamen bleiben unverändert (Szenen, Tests).
        private static readonly Dictionary<string, string> SimpleLabels = new()
        {
            // Noise-Maske und Nachbild-Test
            ["noiseBroadbandFinestDegrees"] = "Smallest Spots (degrees)",
            ["noiseBroadbandCoarsestDegrees"] = "Largest Spots (degrees)",
            ["noiseDotSizeDegrees"] = "Spot Size (degrees)",
            ["noiseDotSizePixels"] = "Spot Size (pixels)",
            ["noiseRefreshRateHz"] = "Noise Changes Per Second",
            ["maskDemoKey"] = "Afterimage Test Key",
            ["maskDemoPatternSeconds"] = "Afterimage Test: Checkerboard (s)",
            ["maskDemoMaskSeconds"] = "Afterimage Test: Noise (s)",
            ["maskDemoGraySeconds"] = "Afterimage Test: Grey Screen (s)",
            // Checkerboard-Ablauf und Training
            ["trialSequence"] = "Trial Order",
            ["testFixedSourceGrid"] = "Variant 2: Fixed Source Grid",
            ["testEdgeNormalizedGrid"] = "Variant 3: Edge-Normalized Grid",
            ["patternSeconds"] = "Checkerboard Time (s)",
            ["answerTimeoutSeconds"] = "Time To Answer (s)",
            ["preStimulusSeconds"] = "Time Before Checkerboard (s)",
            ["categoryALabel"] = "Word For Convex Answer",
            ["categoryBLabel"] = "Word For Concave Answer",
            ["trainingExampleA"] = "Convex Example (k)",
            ["trainingExampleB"] = "Concave Example (k)",
            ["trainingEasyKValues"] = "Practice Part 1: k Order (Easy)",
            ["trainingHardKValues"] = "Practice Part 2: k Order (Harder)",
            ["invertCheckerboardEachTrial"] = "Invert Checkerboard Each Trial",
            ["invertCheckerboard"] = "Swap Black And White",
            ["trainingNoiseSeconds"] = "Screen After Example (s)",
            ["trainingFixationSeconds"] = "Min Wait Before Practice Image (s)",
            ["gridSpacingDegrees"] = "Square Size (degrees)",
            ["useCircularAperture"] = "Round Window",
            ["textDistanceMeters"] = "Text Size As If At (m)",
            // Blendenrand und Fixation
            ["edgeSoftnessDegrees"] = "Soft Edge Width (degrees)",
            ["maxSampleAgeSeconds"] = "Ignore Gaze Data Older Than (s)",
            ["toleranceDegrees"] = "Allowed Distance From Cross (degrees)",
            ["requiredSteadySeconds"] = "Steady Look Needed (s)",
            // Random Dots: Plan und Bewegung
            ["dotSeedBase"] = "Dot Layout Seed",
            ["instrumentMagnificationMValues"] = "Magnification Values (m)",
            ["contentZoomValues"] = "Extra Zoom Values",
            ["sessionMotionMode"] = "Movement Type",
            ["simulatedSweepSeconds"] = "Dots Shown (s)",
            ["simulatedImageCenterSpeed"] = "Dot Speed In Image Center (deg/s)",
            ["pauseSeconds"] = "Grey Pause Between Trials (s)",
            ["headTurnSafetyDegrees"] = "Head Turn Range (degrees)",
            ["velocityGateAxis"] = "Head Turn Direction",
            ["alternateSweepDirection"] = "One Sweep Per Trial, Alternating",
            ["minimumMovingHeadSpeed"] = "Head Counts As Still Below (°/s)",
            ["dotsFadeInSeconds"] = "Dots Fade In (s)",
            ["answerFlashColor"] = "Flash Colour After Answer",
            ["answerFlashOpacity"] = "Flash Strength",
            ["answerFlashSeconds"] = "Flash Shown (s)",
            ["practiceTextsWaitForPress"] = "Practice Texts Wait For Trackpad",
            ["practiceContinueKey"] = "Continue Key (Experimenter)",
            ["trainActivePanning"] = "Practise Head-Tracked Trials",
            ["showActiveSpeedExample"] = "Show Calm Speed First",
            ["freeMovementPractice"] = "Practice 1: Free Head Movement",
            ["activePracticeKValues"] = "Practice 2: k Values",
            ["activePracticeGoodTrials"] = "Practice 2: Good Trials Needed",
            ["activePracticeMaxTrials"] = "Practice 2: Max Trials",
            ["velocitySmoothingSeconds"] = "Speed Smoothing (s)",
            // Random Dots: Training
            ["showSimulatedExamples"] = "Show Labelled Examples",
            ["exampleConvexK"] = "Convex Example (k)",
            ["exampleConcaveK"] = "Concave Example (k)",
            ["exampleLongSeconds"] = "Long Example (s)",
            ["exampleTextSeconds"] = "Text Before Example (s)",
            ["simulatedTrainingFeedback"] = "Show Right/Wrong After Practice",
            ["feedbackConcaveMaxK"] = "Clearly Concave Up To (k)",
            ["feedbackConvexMinK"] = "Clearly Convex From (k)",
            ["feedbackSeconds"] = "Right/Wrong Shown (s)",
            // Random Dots: Punktfeld und Vorschau
            ["instrumentDistortionK"] = "Distortion (k)",
            ["instrumentMagnificationM"] = "Magnification (m)",
            ["contentZoom"] = "Extra Zoom",
            ["fieldBackgroundColor"] = "Color Inside Window",
            ["lightDotFraction"] = "Share Of Light Dots",
            ["dotDensity"] = "Dots Per Square Degree",
            ["motionMode"] = "Movement Type",
            ["simulatedYawAmplitudeDegrees"] = "Movement Range (degrees)",
            ["previewImageCenterSpeed"] = "Dot Speed In Image Center (deg/s)",
            ["simulatedYawSpeedDegreesPerSecond"] = "Binocular Turn Speed (deg/s)",
            ["loopSweepInPreview"] = "Repeat Movement",
            ["worldCoverageDegrees"] = "Dot Field Size (degrees)",
            ["fieldRadiusMeters"] = "Technical: Field Radius (m)",
        };

        protected abstract bool ConfigurationLocked { get; }
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        protected void BeginSettings(string explanation)
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(explanation, MessageType.Info);
            if (ConfigurationLocked)
                EditorGUILayout.HelpBox("Settings are locked while a session or practice is running.",
                    MessageType.Info);
            EditorGUI.BeginDisabledGroup(ConfigurationLocked);
        }

        protected void EndSettings(params string[] statusFields)
        {
            EditorGUI.EndDisabledGroup();
            if (Section("Current Status", false))
            {
                using (new EditorGUI.DisabledScope(true)) Fields(serializedObject, statusFields);
            }
            serializedObject.ApplyModifiedProperties();
        }

        protected bool Section(string title, bool initiallyOpen = true)
        {
            // Merkt sich offene Gruppen, ohne die Szene zu verändern.
            string key = target.GetType().Name + ".Inspector." + title;
            bool open = SessionState.GetBool(key, initiallyOpen);
            EditorGUILayout.Space(4);
            open = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            SessionState.SetBool(key, open);
            return open;
        }

        protected static void Fields(SerializedObject owner, params string[] names)
        {
            foreach (string name in names)
            {
                SerializedProperty property = owner.FindProperty(name);
                if (property == null) continue;
                if (SimpleLabels.TryGetValue(name, out string label))
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
                else
                    EditorGUILayout.PropertyField(property, true);
            }
        }

        protected void LinkedFields(string reference, params string[] names)
        {
            // Undo, Prefab-Overrides und Szenenspeicherung laufen weiter über Unity.
            Object component = serializedObject.FindProperty(reference).objectReferenceValue;
            if (component == null)
            {
                EditorGUILayout.HelpBox("Missing link: " + reference + ". Set it under \"Links And Advanced\".",
                    MessageType.Warning);
                return;
            }
            var linked = new SerializedObject(component);
            linked.Update();
            // Die Toolbox erzeugt den Provider beim Play-Start. Seine Auswahl
            // danach zu ändern würde nicht den tatsächlich laufenden Tracker wechseln.
            using (new EditorGUI.DisabledScope(reference == "eyeTrackingToolbox" && Application.isPlaying))
                Fields(linked, names);
            linked.ApplyModifiedProperties();
        }

        protected void FixationSettings()
        {
            if (!Section("Gaze Check")) return;
            Fields(serializedObject, "requireFixation");
            if (!serializedObject.FindProperty("requireFixation").boolValue)
                EditorGUILayout.HelpBox("Gaze check is OFF. It is not checked whether the participant looks " +
                    "at the cross.", MessageType.Warning);
            LinkedFields("fixationMonitor", "toleranceDegrees", "requiredSteadySeconds");
            LinkedFields("eyeTrackingToolbox", "provider");
            Fields(serializedObject, "maxLookAwaySeconds", "maxNoDataSeconds", "maxSampleAgeSeconds",
                "maxRepeatsPerTrial");
            EditorGUILayout.HelpBox("Max Repeats Per Trial counts all tries of a trial, including the first. " +
                "0 = no limit.", MessageType.Info);
        }

        protected int Count(string name) => serializedObject.FindProperty(name).arraySize;

        /// <summary>
        /// "Use Preview Values For The Experiment" und "Restore Original Values",
        /// gleich für beide Manager. whatIsTakenOver erklärt, welche Werte wohin gehen.
        /// </summary>
        protected void PreviewTakeoverButtons(MonoBehaviour manager, string whatIsTakenOver)
        {
            var takeover = (IPreviewTakeoverManager)manager;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(whatIsTakenOver + " In Play Mode this only works while the preview is " +
                "on. The values are written to the scene when Play Mode stops; then save the scene.",
                MessageType.Info);
            // Im Play Mode ohne Vorschau trägt der Stimulus Werte des letzten Trials.
            bool canTakeOver = !Application.isPlaying || takeover.IsPreviewActive;
            using (new EditorGUI.DisabledScope(!canTakeOver))
            {
                if (GUILayout.Button("Use Preview Values For The Experiment"))
                {
                    serializedObject.ApplyModifiedProperties();
                    PreviewTakeoverEditor.TakeOver(manager);
                    serializedObject.Update();
                }
            }
            using (new EditorGUI.DisabledScope(!takeover.HasValuesBeforePreviewTakeover))
            {
                if (GUILayout.Button("Restore Original Values"))
                {
                    serializedObject.ApplyModifiedProperties();
                    PreviewTakeoverEditor.Restore(manager);
                    serializedObject.Update();
                }
            }
            if (takeover.HasValuesBeforePreviewTakeover)
                EditorGUILayout.HelpBox("The trial plan uses values from the preview. The original values are " +
                    "saved and can be restored.", MessageType.Warning);
        }

        /// <summary>
        /// Tabelle "Wert | Wiederholungen" für k bzw. l. Beide Listen bleiben gleich lang;
        /// neue Zeilen bekommen Repeats Per Condition. Gibt die Summe der Wiederholungen zurück.
        /// </summary>
        protected long LevelTable(string valuesName, string repeatsName, string label)
        {
            SerializedProperty values = serializedObject.FindProperty(valuesName);
            SerializedProperty repeats = serializedObject.FindProperty(repeatsName);
            int fallback = serializedObject.FindProperty("repeatsPerCondition").intValue;
            while (repeats.arraySize < values.arraySize)
            {
                repeats.arraySize++;
                repeats.GetArrayElementAtIndex(repeats.arraySize - 1).intValue = fallback;
            }
            repeats.arraySize = values.arraySize;

            EditorGUILayout.LabelField($"Tested {label} values × how often", EditorStyles.boldLabel);
            long sum = 0;
            int removeIndex = -1;
            for (int index = 0; index < values.arraySize; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(label, GUILayout.Width(14));
                    SerializedProperty value = values.GetArrayElementAtIndex(index);
                    value.floatValue = EditorGUILayout.FloatField(value.floatValue, GUILayout.MinWidth(50));
                    EditorGUILayout.LabelField("×", GUILayout.Width(12));
                    SerializedProperty count = repeats.GetArrayElementAtIndex(index);
                    count.intValue = Mathf.Max(1, EditorGUILayout.IntField(count.intValue, GUILayout.MinWidth(40)));
                    sum += count.intValue;
                    if (GUILayout.Button("–", GUILayout.Width(22))) removeIndex = index;
                }
            }
            if (removeIndex >= 0)
            {
                values.DeleteArrayElementAtIndex(removeIndex);
                repeats.DeleteArrayElementAtIndex(removeIndex);
            }
            if (GUILayout.Button($"+ Add {label} value"))
            {
                values.arraySize++;
                repeats.arraySize = values.arraySize;
                repeats.GetArrayElementAtIndex(repeats.arraySize - 1).intValue = fallback;
            }
            return sum;
        }

        protected string BlockText(long trials)
        {
            int blocks = serializedObject.FindProperty("blocksPerSession").intValue;
            if (blocks <= 1) return ", no breaks";
            var pauses = new List<string>();
            for (int pause = 1; pause < blocks; pause++)
                pauses.Add(Mathf.RoundToInt(trials * pause / (float)blocks).ToString());
            return $", {blocks} blocks (breaks after {string.Join(", ", pauses)} valid trials, continue with F5)";
        }
    }

    [CustomEditor(typeof(CheckerboardExperimentManager))]
    public sealed class CheckerboardExperimentManagerEditor : ExperimentManagerEditor
    {
        protected override bool ConfigurationLocked =>
            ((CheckerboardExperimentManager)target).IsSessionActive
            || ((CheckerboardExperimentManager)target).IsTrainingActive;

        public override void OnInspectorGUI()
        {
            BeginSettings("Set up the checkerboard experiment here. Window size, eyes and grid distortion " +
                "for the trials come from the trial plan below.");
            if (Section("Session And Trial Plan"))
            {
                Fields(serializedObject, "participantId", "sessionLabel", "randomSeed", "fieldOfViewValues",
                    "eyePresentations", "instrumentMagnificationM", "allowPincushionExtension",
                    "testFixedSourceGrid", "testEdgeNormalizedGrid", "repeatsPerCondition");
                bool extended = serializedObject.FindProperty("allowPincushionExtension").boolValue;
                long repetitions = LevelTable("instrumentDistortionKValues", "repeatsPerKValue",
                    extended ? "k / -κ" : "k");
                if (extended)
                    EditorGUILayout.HelpBox("CUSTOM EXTENSION: negative settings use tanh, NOT Merlitz k. " +
                        "These sessions have a separate mapping version and stimulus-parameter PSE. " +
                        "Random dots are unchanged.", MessageType.Warning);
                EditorGUILayout.HelpBox("Standard: Merlitz instrument mapping with m = 10 and a fixed source " +
                    "grid (variant 2), no extra scaling at the window edge or in the centre. Not the old " +
                    "visual-space l mapping. Use new pilot data. For a random-dot comparison use the same m " +
                    "there; m = 1 makes all k values look straight.", MessageType.Info);
                int mappings = (serializedObject.FindProperty("testFixedSourceGrid").boolValue ? 1 : 0)
                    + (serializedObject.FindProperty("testEdgeNormalizedGrid").boolValue ? 1 : 0);
                long perMapping = (long)Count("fieldOfViewValues") * Count("eyePresentations") * repetitions;
                long trials = perMapping * mappings;
                if (mappings == 0)
                    EditorGUILayout.HelpBox("Select at least one grid variant.", MessageType.Error);
                else
                    EditorGUILayout.HelpBox($"{perMapping} trials per selected variant. Both selected = " +
                        "one jointly shuffled session, with separate PSE fits. Same k values and repeats " +
                        "for both. Practice shows each selected variant.", MessageType.Info);
                Fields(serializedObject, "blocksPerSession", "invertCheckerboardEachTrial");
                EditorGUILayout.HelpBox($"Planned: {trials} valid trials{BlockText(trials)}. " +
                    "Trials that fail (for example looking away) are repeated on top.", MessageType.Info);
            }
            if (Section("Timing"))
            {
                Fields(serializedObject, "trialSequence", "patternSeconds", "answerTimeoutSeconds",
                    "preStimulusSeconds");
                bool noiseResponse = serializedObject.FindProperty("trialSequence").enumValueIndex == 0;
                EditorGUILayout.HelpBox(noiseResponse
                    ? "A: checkerboard → noise while answering → grey → next checkerboard."
                    : "B: checkerboard → grey while answering → noise → next checkerboard.", MessageType.Info);
                EditorGUILayout.LabelField("Time To Answer 0 = no limit. Time Before Checkerboard is a minimum.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            FixationSettings();
            if (Section("Answers And Keys"))
            {
                Fields(serializedObject, "convexResponseKey", "concaveResponseKey", "useVrControllerButtons",
                    "controllerMapping", "categoryALabel", "categoryBLabel");
                LinkedFields("keyboardController", "swapResponseKeys", "logResponses");
                Fields(serializedObject, "startSessionKey", "abortSessionKey");
            }
            if (Section("Practice", false))
                Fields(serializedObject, "requireTrainingBeforeSession", "trainingKey", "continueTrainingKey",
                    "trainingExampleA", "trainingExampleB", "trainingEasyKValues", "trainingHardKValues",
                    "trainingExampleSeconds", "trainingNoiseSeconds", "trainingFixationSeconds");
            if (Section("Live Preview (No Recording)"))
            {
                Fields(serializedObject, "previewKey", "previewShowsNoise");
                EditorGUILayout.HelpBox("In Play Mode press P to start the preview (P or F6 ends it). " +
                    "Change the settings below and see the result right away. " +
                    "Preview Shows Noise switches between checkerboard and noise.", MessageType.Info);
                LinkedFields("stimulus", "fieldOfViewDegrees", "edgeSoftnessDegrees", "useCircularAperture",
                    "allowPincushionExtension", "instrumentDistortionK", "instrumentMagnificationM", "gridMapping",
                    "gridSpacingDegrees",
                    "eyePresentation", "darkColor", "lightColor", "invertCheckerboard",
                    "backgroundColor", "showFixationTarget", "fixationSizeDegrees", "fixationColor");
                NoiseFields();
                var previewStimulus = serializedObject.FindProperty("stimulus").objectReferenceValue
                    as VrCheckerboardStimulus;
                string mappingProblem = previewStimulus != null ? previewStimulus.InstrumentMappingProblem : null;
                if (mappingProblem != null)
                    EditorGUILayout.HelpBox("This combination cannot be drawn, so only grey is shown: " +
                        mappingProblem, MessageType.Error);
                float planM = ((CheckerboardExperimentManager)target).InstrumentMagnificationM;
                if (previewStimulus != null && previewStimulus.InstrumentMagnificationM != planM)
                    EditorGUILayout.HelpBox("The preview shows m = " + previewStimulus.InstrumentMagnificationM +
                        ", the trials use m = " + planM + " from the trial plan.", MessageType.Info);
                EditorGUILayout.HelpBox("Negative preview values require Allow Pincushion Extension. " +
                    "They use a custom tanh formula, not negative Merlitz k. Preview values are restored " +
                    "on exit unless explicitly taken over.", MessageType.Info);
                Fields(serializedObject, "maskDemoKey", "maskDemoPatternSeconds", "maskDemoMaskSeconds",
                    "maskDemoGraySeconds");
                EditorGUILayout.HelpBox("Afterimage test: during the preview press M (or the button). It shows " +
                    "the checkerboard, then the noise, then a grey screen. If you still see the checkerboard " +
                    "on the grey screen, try another noise type. With Invert Checkerboard Each Trial on, each " +
                    "press swaps black and white.", MessageType.Info);
                EditorGUILayout.HelpBox("When the preview ends, all changes are undone – unless you press " +
                    "\"Use Preview Values For The Experiment\".", MessageType.Info);
                var manager = (CheckerboardExperimentManager)target;
                if (Application.isPlaying && GUILayout.Button("Preview On / Off"))
                {
                    serializedObject.ApplyModifiedProperties();
                    manager.TogglePreview();
                }
                using (new EditorGUI.DisabledScope(!Application.isPlaying || !manager.IsPreviewActive))
                {
                    if (GUILayout.Button(manager.IsMaskDemoRunning ? "Afterimage Test Running …" : "Play Afterimage Test"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        manager.PlayMaskDemo();
                    }
                }
                PreviewTakeoverButtons(manager, "\"Use Preview Values\" puts the preview window size and eyes " +
                    "into the trial plan and keeps the look (edge, grid size, colors, cross, noise). " +
                    "The k list is not changed; the fixed magnification is taken over.");
            }
            if (Section("Pattern, Cross And Noise", false))
            {
                LinkedFields("stimulus", "edgeSoftnessDegrees", "useCircularAperture", "gridSpacingDegrees",
                    "darkColor", "lightColor", "showFixationTarget", "fixationSizeDegrees", "fixationColor",
                    "backgroundColor");
                NoiseFields();
            }
            if (Section("Links And Advanced", false))
            {
                Fields(serializedObject, "stimulus", "keyboardController", "eyeTrackingToolbox", "fixationMonitor",
                    "outputRoot", "autoStartOnPlay");
                EditorGUILayout.HelpBox("Auto Start On Play skips the practice. Only use it for technical tests. " +
                    "The eye tracker is chosen on the Eye Tracking Toolbox.", MessageType.Info);
                LinkedFields("stimulus", "observer", "textColor", "textDistanceMeters", "textSize",
                    "visibleAtStart", "materialOverride");
            }
            EndSettings("sessionState", "currentTrialNumber", "totalTrials", "validTrialsCompleted",
                "presentationCount", "activeSessionFolder");
        }

        private void NoiseFields()
        {
            // Nur die Felder des gewählten Noise-Typs zeigen.
            var stimulus = serializedObject.FindProperty("stimulus").objectReferenceValue as VrCheckerboardStimulus;
            LinkedFields("stimulus", "noiseType");
            NoiseMaskType type = stimulus != null ? stimulus.NoiseType : NoiseMaskType.Broadband;
            if (type == NoiseMaskType.Broadband)
                LinkedFields("stimulus", "noiseBroadbandFinestDegrees", "noiseBroadbandCoarsestDegrees");
            else if (type == NoiseMaskType.SingleScaleDegrees)
                LinkedFields("stimulus", "noiseDotSizeDegrees");
            else
                LinkedFields("stimulus", "noiseDotSizePixels");
            LinkedFields("stimulus", "noiseRefreshRateHz");
            if (stimulus != null && type == NoiseMaskType.Broadband)
                EditorGUILayout.HelpBox($"{stimulus.NoiseOctaves} spot sizes from {stimulus.NoiseDotSizeDegrees:0.##}° " +
                    $"to {stimulus.NoiseBroadbandCoarsestDegrees:0.##}°, each twice as big as the one before.",
                    MessageType.None);
            if (type == NoiseMaskType.FinePixels)
                EditorGUILayout.HelpBox("Very fine noise hardly hides the large checkerboard squares, so an " +
                    "afterimage may stay. Fine and coarse spots work better.", MessageType.Warning);
        }
    }

    [CustomEditor(typeof(RandomDotExperimentManager))]
    public sealed class RandomDotExperimentManagerEditor : ExperimentManagerEditor
    {
        protected override bool ConfigurationLocked => ((RandomDotExperimentManager)target).IsSessionActive
            || ((RandomDotExperimentManager)target).IsTrainingActive;

        public override void OnInspectorGUI()
        {
            BeginSettings("Set up the random dot experiment here. Window size, distortion k, magnification " +
                "and movement for the trials come from the trial plan below. Dot size, density and colors are " +
                "set on the dot field. The answer keys are set on the keyboard controller.");
            if (Section("Session And Trial Plan"))
            {
                Fields(serializedObject, "participantId", "sessionLabel", "randomSeed", "dotSeedBase",
                    "fieldOfViewValues", "eyePresentations", "instrumentMagnificationMValues",
                    "contentZoomValues", "sessionMotionMode", "repeatsPerCondition");
                long repetitions = LevelTable("instrumentDistortionKValues", "repeatsPerKValue", "k");
                long trials = (long)Count("fieldOfViewValues") * Count("eyePresentations")
                    * Count("instrumentMagnificationMValues") * Count("contentZoomValues") * repetitions;
                Fields(serializedObject, "blocksPerSession");
                EditorGUILayout.HelpBox($"Planned: {trials} valid trials for one movement type" +
                    $"{BlockText(trials)}. The other movement type is started as a new session (F7).",
                    MessageType.Info);
            }
            if (Section("Simulated Movement"))
            {
                Fields(serializedObject, "simulatedSweepAxis", "simulatedSweepSeconds",
                    "simulatedImageCenterSpeed", "pauseSeconds", "dotsFadeInSeconds");
                EditorGUILayout.HelpBox("The dots in the middle of the image move equally fast at every " +
                    "magnification. Dots Shown, Grey Pause and Fade In also apply to head-tracked trials. " +
                    "Dots Shown starts with the fade in.",
                    MessageType.Info);
            }
            if (Section("Head-Tracked Movement"))
            {
                HeadSpeedSettings();
                Fields(serializedObject, "headTurnSafetyDegrees");
                HeadTurnReachInfo();
            }
            if (Section("Practice", false))
            {
                Fields(serializedObject, "trainingKey", "practiceTextsWaitForPress", "practiceContinueKey",
                    "trainSimulatedMotion",
                    "showSimulatedExamples", "exampleConvexK", "exampleConcaveK", "exampleLongSeconds",
                    "exampleTextSeconds",
                    "simulatedTrainingKValues", "simulatedTrainingRepeatsPerValue", "simulatedTrainingFeedback",
                    "feedbackConcaveMaxK", "feedbackConvexMinK", "feedbackSeconds",
                    "trainActivePanning", "showActiveSpeedExample", "freeMovementPractice", "activePracticeKValues",
                    "activePracticeGoodTrials", "activePracticeMaxTrials");
                EditorGUILayout.HelpBox("T starts a practice of the chosen movement type without saving data. " +
                    "In a session the practice matching the movement type is used. The head-tracked practice " +
                    "uses the real dots: first an example of a calm speed shown by the computer, then practice 1 " +
                    "(free head movement with k = 1, no answers, ended with the Continue Key), then practice 2 " +
                    "(trials exactly like in the test, no texts in between).",
                    MessageType.Info);
            }
            FixationSettings();
            if (Section("Answers And Keys"))
            {
                LinkedFields("keyboardController", "concaveKey", "convexKey", "swapResponseKeys",
                    "useVrControllerButtons", "vrControllerMapping", "logResponses");
                Fields(serializedObject, "startSessionKey", "startOtherMotionSessionKey", "abortSessionKey");
                Fields(serializedObject, "answerFlashColor", "answerFlashOpacity", "answerFlashSeconds");
                EditorGUILayout.HelpBox("While the participant answers, only the grey circle is shown (no cross). " +
                    "After each answer the grey circle flashes briefly, then the cross comes back.",
                    MessageType.Info);
            }
            if (Section("Dot Field And Cross", false))
                LinkedFields("stimulus", "dotDensity", "dotSizeDegrees", "darkColor", "lightColor",
                    "fieldBackgroundColor", "backgroundColor", "lightDotFraction", "edgeSoftnessDegrees", "showFixationTarget",
                    "fixationSizeDegrees", "fixationColor");
            if (Section("Live Preview (No Recording)"))
            {
                Fields(serializedObject, "previewKey");
                EditorGUILayout.HelpBox("In Play Mode press P to start the preview (P or F6 ends it). " +
                    "Change the settings below and see the result right away. The straight reference grid is " +
                    "only for comparing and is never shown in practice or measurement.",
                    MessageType.Info);
                LinkedFields("stimulus", "fieldOfViewDegrees", "edgeSoftnessDegrees", "dotDensity",
                    "dotSizeDegrees", "darkColor", "lightColor", "backgroundColor", "fieldBackgroundColor",
                    "instrumentDistortionK",
                    "instrumentMagnificationM", "contentZoom", "eyePresentation", "motionMode", "sweepAxis",
                    "simulatedYawAmplitudeDegrees", "previewImageCenterSpeed", "sweepDirection",
                    "loopSweepInPreview", "worldCoverageDegrees", "showReferenceGrid",
                    "referenceGridSpacingDegrees", "referenceGridWidthPixels", "referenceGridColor");
                EditorGUILayout.HelpBox("When the preview ends, all changes are undone – unless you press " +
                    "\"Use Preview Values For The Experiment\".", MessageType.Info);
                var manager = (RandomDotExperimentManager)target;
                if (Application.isPlaying && GUILayout.Button("Preview On / Off"))
                {
                    serializedObject.ApplyModifiedProperties();
                    manager.TogglePreview();
                }
                PreviewTakeoverButtons(manager, "\"Use Preview Values\" puts the preview window size, " +
                    "magnification, extra zoom, eyes, movement type, direction and speed into the trial plan and " +
                    "keeps the look (dot density, dot size, edge, colors). The k values are not changed.");
            }
            if (Section("Links And Advanced", false))
            {
                Fields(serializedObject, "stimulus", "keyboardController", "sweepMonitor", "eyeTrackingToolbox",
                    "fixationMonitor", "outputRoot", "autoStartOnPlay");
                LinkedFields("stimulus", "observer", "fieldRadiusMeters", "visibleAtStart", "materialOverride");
            }
            EndSettings("sessionState", "currentTrialNumber", "totalTrials", "validTrialsCompleted",
                "presentationCount", "sessionMotionMode", "activeSessionFolder");
        }

        private void HeadSpeedSettings()
        {
            Fields(serializedObject, "velocityGateAxis", "alternateSweepDirection", "minimumMovingHeadSpeed",
                "velocitySmoothingSeconds");
            var manager = (RandomDotExperimentManager)target;
            float still = serializedObject.FindProperty("minimumMovingHeadSpeed").floatValue;
            float seconds = serializedObject.FindProperty("simulatedSweepSeconds").floatValue;
            bool alternate = serializedObject.FindProperty("alternateSweepDirection").boolValue;
            EditorGUILayout.HelpBox(
                $"One rule: below {still:0.##} °/s the head counts as still. The dots appear as soon as the head " +
                $"turns faster than this{(alternate ? " in the other direction than in the last trial" : "")}, " +
                $"at any speed, and stay {seconds:0.##} s. If the head gets slower than this in the sweep " +
                "direction (stops or turns back), the dots and the cross disappear right away. The participant " +
                "still answers, and the trial is repeated later. Only the speed smoothing softens short dips. " +
                "All head speeds are saved for every trial.", MessageType.Info);
            EditorGUILayout.LabelField($"For comparison: the simulated movement equals {manager.SimulatedHeadSpeedPreview:0.##} " +
                "°/s of head speed.", EditorStyles.wordWrappedMiniLabel);
        }

        private void HeadTurnReachInfo()
        {
            // Wie weit der Kopf drehen kann und wie viele Punkte das kostet, je m.
            // Gerechnet wie vor dem Sitzungsstart: ungünstigstes FOV, Zoom und k.
            var stimulus = serializedObject.FindProperty("stimulus").objectReferenceValue as RandomDotFieldStimulus;
            if (stimulus == null) return;
            float reach = serializedObject.FindProperty("headTurnSafetyDegrees").floatValue;
            SerializedProperty fovs = serializedObject.FindProperty("fieldOfViewValues");
            SerializedProperty ms = serializedObject.FindProperty("instrumentMagnificationMValues");
            SerializedProperty zooms = serializedObject.FindProperty("contentZoomValues");
            SerializedProperty ks = serializedObject.FindProperty("instrumentDistortionKValues");
            var culture = CultureInfo.InvariantCulture;
            var text = new StringBuilder($"Dots are there up to ±{reach:0.#}° head turn from where the head " +
                "pointed at the start of the trial, in every direction.");
            bool tooMuch = false;
            for (int mi = 0; mi < ms.arraySize; mi++)
            {
                float m = ms.GetArrayElementAtIndex(mi).floatValue;
                int mostDots = 0;
                bool impossible = false;
                for (int fi = 0; fi < fovs.arraySize; fi++)
                for (int zi = 0; zi < zooms.arraySize; zi++)
                for (int ki = 0; ki < ks.arraySize; ki++)
                {
                    float zoom = zooms.GetArrayElementAtIndex(zi).floatValue;
                    float coverage = RandomDotFieldStimulus.CoverageNeeded(fovs.GetArrayElementAtIndex(fi).floatValue,
                        zoom, m, ks.GetArrayElementAtIndex(ki).floatValue, reach, stimulus.ApertureEdgeSoftnessDegrees);
                    if (float.IsInfinity(coverage) || coverage > RandomDotFieldStimulus.MaximumCoverageDegrees)
                    {
                        impossible = true;
                        continue;
                    }
                    mostDots = Mathf.Max(mostDots,
                        RandomDotFieldStimulus.DotCountFor(stimulus.DotDensity, m, zoom, coverage));
                }
                bool overLimit = mostDots > RandomDotFieldStimulus.MaximumDotCount;
                tooMuch |= impossible || overLimit;
                text.Append($"\nm = {m.ToString("0.##", culture)}: up to about {mostDots.ToString("N0", culture)} dots");
                if (overLimit)
                    text.Append($" – more than {RandomDotFieldStimulus.MaximumDotCount.ToString("N0", culture)}, " +
                        "the session will not start");
                if (impossible)
                    text.Append(" – dot field larger than 170°, the session will not start");
            }
            EditorGUILayout.HelpBox(text.ToString(), tooMuch ? MessageType.Warning : MessageType.Info);
        }
    }
}
