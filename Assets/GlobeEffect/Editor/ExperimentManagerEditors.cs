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
    /// </summary>
    public abstract class ExperimentManagerEditor : UnityEditor.Editor
    {
        protected abstract bool ConfigurationLocked { get; }
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        protected void BeginSettings(string explanation)
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox(explanation, MessageType.Info);
            if (ConfigurationLocked)
                EditorGUILayout.HelpBox("Einstellungen erst nach Ende oder Abbruch ändern.", MessageType.Info);
            EditorGUI.BeginDisabledGroup(ConfigurationLocked);
        }

        protected void EndSettings(params string[] statusFields)
        {
            EditorGUI.EndDisabledGroup();
            if (Section("Laufender Status", false))
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
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
        }

        protected void LinkedFields(string reference, params string[] names)
        {
            // Undo, Prefab-Overrides und Szenenspeicherung laufen weiter über Unity.
            Object component = serializedObject.FindProperty(reference).objectReferenceValue;
            if (component == null)
            {
                EditorGUILayout.HelpBox("Verweis fehlt: " + reference + ". Unter Verweise zuweisen.",
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
            if (!Section("Blickkontrolle")) return;
            Fields(serializedObject, "requireFixation");
            if (!serializedObject.FindProperty("requireFixation").boolValue)
                EditorGUILayout.HelpBox("Blickkontrolle ist AUS. Zentralfixation wird nicht geprüft.",
                    MessageType.Warning);
            LinkedFields("fixationMonitor", "toleranceDegrees", "requiredSteadySeconds");
            LinkedFields("eyeTrackingToolbox", "provider");
            Fields(serializedObject, "maxLookAwaySeconds", "maxNoDataSeconds", "maxSampleAgeSeconds",
                "maxRepeatsPerTrial");
            EditorGUILayout.HelpBox("Max Repeats Per Trial begrenzt bisher alle Versuche inklusive " +
                "Erstversuch; 0 bedeutet unbegrenzt.", MessageType.Info);
        }

        protected int Count(string name) => serializedObject.FindProperty(name).arraySize;

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

            EditorGUILayout.LabelField($"{label}-Werte und Wiederholungen", EditorStyles.boldLabel);
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
            if (GUILayout.Button($"+ {label}-Wert"))
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
            if (blocks <= 1) return ", ohne Pausen";
            var pauses = new System.Collections.Generic.List<string>();
            for (int pause = 1; pause < blocks; pause++)
                pauses.Add(Mathf.RoundToInt(trials * pause / (float)blocks).ToString());
            return $", {blocks} Blöcke (Pausen nach {string.Join(", ", pauses)} gültigen Trials, weiter mit F5)";
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
            BeginSettings("Hier den Versuch einstellen. FOV, Augenmodus und Verzerrungswerte kommen " +
                "aus dem Trial Plan. Diese Werte am Stimulus sind nur Vorschau und werden überschrieben.");
            if (Section("Sitzung und Versuchsplan"))
            {
                Fields(serializedObject, "participantId", "sessionLabel", "randomSeed", "fieldOfViewValues",
                    "eyePresentations", "repeatsPerCondition");
                long repetitions = LevelTable("visualSpaceLValues", "repeatsPerLValue", "l");
                long trials = (long)Count("fieldOfViewValues") * Count("eyePresentations") * repetitions;
                Fields(serializedObject, "blocksPerSession");
                EditorGUILayout.HelpBox($"Geplant: {trials} gültige Trials{BlockText(trials)}. " +
                    "Ungültige Darbietungen kommen zusätzlich dazu.", MessageType.Info);
            }
            if (Section("Ablauf und Zeiten"))
            {
                Fields(serializedObject, "trialSequence", "patternSeconds", "answerTimeoutSeconds",
                    "preStimulusSeconds");
                bool noiseResponse = serializedObject.FindProperty("trialSequence").enumValueIndex == 0;
                EditorGUILayout.HelpBox(noiseResponse
                    ? "A: Schachbrett → Noise + Antwort → Grau → nächstes Schachbrett."
                    : "B: Schachbrett → Grau + Antwort → Noise → nächstes Schachbrett.", MessageType.Info);
                EditorGUILayout.LabelField("Antwortzeit 0 = unbegrenzt; Pre Stimulus = Mindestdauer.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            FixationSettings();
            if (Section("Antworten und Tasten"))
            {
                Fields(serializedObject, "convexResponseKey", "concaveResponseKey", "useVrControllerButtons",
                    "controllerMapping", "categoryALabel", "categoryBLabel");
                LinkedFields("keyboardController", "swapResponseKeys", "logResponses");
                Fields(serializedObject, "startSessionKey", "abortSessionKey");
            }
            if (Section("Training", false))
                Fields(serializedObject, "requireTrainingBeforeSession", "trainingKey", "continueTrainingKey",
                    "trainingExampleA", "trainingExampleB", "trainingLValues", "trainingRepeatsPerValue",
                    "trainingExampleSeconds", "trainingNoiseSeconds", "trainingFixationSeconds");
            if (Section("Live-Vorschau ohne Messung"))
            {
                Fields(serializedObject, "previewKey");
                EditorGUILayout.HelpBox("Im Play Mode P drücken (erneut P oder F6 beendet). " +
                    "Diese Werte gelten nur für die Vorschau, nicht für den Trial Plan.", MessageType.Info);
                LinkedFields("stimulus", "fieldOfViewDegrees", "visualSpaceL", "eyePresentation");
                EditorGUILayout.HelpBox("Beim Beenden der Vorschau oder Start von Training/Messung werden " +
                    "alle Stimuluswerte auf den Stand vor der Vorschau zurückgesetzt.", MessageType.Info);
                if (Application.isPlaying && GUILayout.Button("Vorschau ein / aus"))
                {
                    serializedObject.ApplyModifiedProperties();
                    ((CheckerboardExperimentManager)target).TogglePreview();
                }
            }
            if (Section("Muster, Fixationskreuz und Noise", false))
                LinkedFields("stimulus", "edgeSoftnessDegrees", "useCircularAperture", "gridSpacingDegrees",
                    "darkColor", "lightColor", "showFixationTarget", "fixationSizeDegrees", "fixationColor",
                    "backgroundColor", "noiseSizeInPixels", "noiseDotSizePixels", "noiseDotSizeDegrees",
                    "noiseRefreshRateHz");
            if (Section("Verweise und erweiterte Einstellungen", false))
            {
                Fields(serializedObject, "stimulus", "keyboardController", "eyeTrackingToolbox", "fixationMonitor",
                    "outputRoot", "autoStartOnPlay");
                EditorGUILayout.HelpBox("Auto Start umgeht das Training. Nur für technische Tests verwenden. " +
                    "Den Eye-Tracking-Provider an der Toolbox einstellen.", MessageType.Info);
                LinkedFields("stimulus", "observer", "textColor", "textDistanceMeters", "textSize",
                    "visibleAtStart", "materialOverride");
            }
            EndSettings("sessionState", "currentTrialNumber", "totalTrials", "validTrialsCompleted",
                "presentationCount", "activeSessionFolder");
        }
    }

    [CustomEditor(typeof(RandomDotExperimentManager))]
    public sealed class RandomDotExperimentManagerEditor : ExperimentManagerEditor
    {
        protected override bool ConfigurationLocked => ((RandomDotExperimentManager)target).IsSessionActive
            || ((RandomDotExperimentManager)target).IsTrainingActive;

        public override void OnInspectorGUI()
        {
            BeginSettings("Hier den Versuch einstellen. Der Manager setzt FOV, k, m, Zoom und Bewegung pro Trial. " +
                "Am Stimulus bleiben Punktgröße, Dichte, Farben und Kreuz. Antworten stehen am Keyboard Controller.");
            if (Section("Sitzung und Versuchsplan"))
            {
                Fields(serializedObject, "participantId", "sessionLabel", "randomSeed", "dotSeedBase",
                    "fieldOfViewValues", "eyePresentations", "instrumentMagnificationMValues",
                    "contentZoomValues", "sessionMotionMode", "repeatsPerCondition");
                long repetitions = LevelTable("instrumentDistortionKValues", "repeatsPerKValue", "k");
                long trials = (long)Count("fieldOfViewValues") * Count("eyePresentations")
                    * Count("instrumentMagnificationMValues") * Count("contentZoomValues") * repetitions;
                Fields(serializedObject, "blocksPerSession");
                EditorGUILayout.HelpBox($"Geplant: {trials} gültige Trials für genau eine Bewegungsart" +
                    $"{BlockText(trials)}. Die andere Variante startet als neue Sitzung.", MessageType.Info);
            }
            if (Section("Simulierte Bewegung"))
            {
                Fields(serializedObject, "simulatedSweepAxis", "simulatedSweepSeconds", "simulatedSpeedReference",
                    "simulatedImageCenterSpeed", "sweepSpeed", "pauseSeconds");
                bool centerSpeed = serializedObject.FindProperty("simulatedSpeedReference").enumValueIndex == 1;
                EditorGUILayout.HelpBox(centerSpeed
                    ? "Bildmitte: gleiche sichtbare Punktgeschwindigkeit bei jedem m. " +
                        "Virtueller Schwenk = Simulated Image Center Speed / (m × Zoom)."
                    : "Objektwinkel: der Schwenk bleibt gleich schnell. Die Punkte werden bei höherem m schneller.",
                    centerSpeed ? MessageType.Info : MessageType.Warning);
            }
            if (Section("Aktive Kopfbewegung"))
            {
                Fields(serializedObject, "freeHeadMovement");
                bool free = serializedObject.FindProperty("freeHeadMovement").boolValue;
                bool openEnded = free && serializedObject.FindProperty("openEndedActiveTrials").boolValue;
                if (free) Fields(serializedObject, "openEndedActiveTrials");
                if (!openEnded) Fields(serializedObject, "headTrackedSeconds");
                Fields(serializedObject, "headTurnSafetyDegrees");
                HeadTurnReachInfo();
                if (openEnded)
                    EditorGUILayout.HelpBox("OFFEN: Die Punkte bleiben sichtbar, bis die Person antwortet. " +
                        "Frei umschauen, beliebig lange und in jede Richtung. Danach graues Feld " +
                        "(Pause Seconds) und der nächste Trial. Die Punktwelt reicht nur Head Turn " +
                        "Safety Degrees weit um die Startblickrichtung.", MessageType.Info);
                else EditorGUILayout.HelpBox(free
                    ? "FREI: beliebige Richtung, kein Solltempo, keine Sinus-/Wendeprüfung. " +
                        "Blickkontrolle bleibt aktiv. Der Punktvorrat ist endlich: " +
                        "Head Turn Safety Degrees legt die verfügbare Reserve um die Startblickrichtung fest."
                    : "GEFÜHRT: bisherige Links-Rechts-Sinusbahn. Bei aktiver Prüfung werden " +
                        "abweichende Haupttrials wiederholt.", MessageType.Info);
                if (!free)
                    Fields(serializedObject, "checkHeadMotion", "sweepAmplitudeDegrees", "sweepSpeed",
                        "maximumProfileErrorDegrees", "turnaroundDegrees", "requiredSweeps",
                        "maxHeadTurnDegrees", "minMeanHeadSpeed", "maxPeakHeadSpeed");
            }
            if (Section("Training", false))
            {
                Fields(serializedObject, "trainingKey", "trainSimulatedMotion",
                    "showSimulatedExamples", "exampleConvexK", "exampleConcaveK", "exampleLongSeconds",
                    "exampleTextSeconds",
                    "simulatedTrainingKValues", "simulatedTrainingRepeatsPerValue", "simulatedTrainingFeedback",
                    "feedbackConcaveMaxK", "feedbackConvexMinK", "feedbackSeconds", "trainHeadMovement",
                    "requiredGoodTrainingSweeps", "sweepAmplitudeDegrees", "sweepSpeed",
                    "maximumProfileErrorDegrees", "maximumTrainingEndpointErrorDegrees", "turnaroundDegrees",
                    "requiredSweeps", "maxHeadTurnDegrees", "minMeanHeadSpeed", "maxPeakHeadSpeed");
                EditorGUILayout.HelpBox("T übt die ausgewählte Session Motion Mode ohne Messdateien. " +
                    "In einer Sitzung wird das Training passend zur gewählten Bewegungsart gewählt. " +
                    "Das Kopftraining bleibt geführt, auch wenn die Haupttrials frei sind.", MessageType.Info);
            }
            FixationSettings();
            if (Section("Antworten und Tasten"))
            {
                LinkedFields("keyboardController", "concaveKey", "convexKey", "swapResponseKeys",
                    "useVrControllerButtons", "vrControllerMapping", "logResponses");
                Fields(serializedObject, "startSessionKey", "startOtherMotionSessionKey", "abortSessionKey");
            }
            if (Section("Punktfeld und Fixationskreuz", false))
                LinkedFields("stimulus", "dotDensity", "dotSizeDegrees", "darkColor", "lightColor",
                    "fieldBackgroundColor", "backgroundColor", "lightDotFraction", "edgeSoftnessDegrees", "showFixationTarget",
                    "fixationSizeDegrees", "fixationColor");
            if (Section("Live-Vorschau ohne Messung"))
            {
                Fields(serializedObject, "previewKey");
                EditorGUILayout.HelpBox("Im Play Mode P drücken; erneut P oder F6 beendet. " +
                    "Hier live k, m, FOV und Bewegungsart ändern. Die Vorschau ist nicht der Trial Plan. " +
                    "Das gerade Raster ist nur eine feste Referenz und bleibt in Training und Messung aus. " +
                    "Punktwelt: Bei Motion Mode HeadTracked gilt wie in der Sitzung Head Turn Safety Degrees " +
                    "(World Coverage wird dann überschrieben), bei SimulatedYaw World Coverage.",
                    MessageType.Info);
                LinkedFields("stimulus", "fieldOfViewDegrees", "edgeSoftnessDegrees", "dotDensity",
                    "dotSizeDegrees", "darkColor", "lightColor", "backgroundColor", "fieldBackgroundColor",
                    "instrumentDistortionK",
                    "instrumentMagnificationM", "contentZoom", "eyePresentation", "motionMode", "sweepAxis",
                    "simulatedYawAmplitudeDegrees", "previewSpeedReference", "previewImageCenterSpeed",
                    "simulatedYawSpeedDegreesPerSecond", "sweepDirection",
                    "loopSweepInPreview", "worldCoverageDegrees", "showReferenceGrid",
                    "referenceGridSpacingDegrees", "referenceGridWidthPixels", "referenceGridColor");
                EditorGUILayout.HelpBox("Beim Beenden der Vorschau oder Start von Training/Messung werden " +
                    "alle Stimuluswerte auf den Stand vor der Vorschau zurückgesetzt – außer nach " +
                    "„Vorschau-Werte ins Experiment übernehmen“.", MessageType.Info);
                var manager = (RandomDotExperimentManager)target;
                if (Application.isPlaying && GUILayout.Button("Vorschau ein / aus"))
                {
                    serializedObject.ApplyModifiedProperties();
                    manager.TogglePreview();
                }
                PreviewTakeoverButtons(manager);
            }
            if (Section("Verweise und erweiterte Einstellungen", false))
            {
                Fields(serializedObject, "stimulus", "keyboardController", "sweepMonitor", "eyeTrackingToolbox",
                    "fixationMonitor", "outputRoot", "autoStartOnPlay");
                LinkedFields("stimulus", "observer", "fieldRadiusMeters", "visibleAtStart", "materialOverride");
            }
            EndSettings("sessionState", "currentTrialNumber", "totalTrials", "validTrialsCompleted",
                "presentationCount", "sessionMotionMode", "activeSessionFolder");
        }

        private void HeadTurnReachInfo()
        {
            // Wie weit der Kopf drehen kann und wie viele Punkte das kostet, je m.
            // Gerechnet wie vor dem Sitzungsstart: ungünstigstes FOV, Zoom und k.
            var stimulus = serializedObject.FindProperty("stimulus").objectReferenceValue as RandomDotFieldStimulus;
            if (stimulus == null) return;
            float reach = Mathf.Max(serializedObject.FindProperty("sweepAmplitudeDegrees").floatValue,
                serializedObject.FindProperty("headTurnSafetyDegrees").floatValue);
            SerializedProperty fovs = serializedObject.FindProperty("fieldOfViewValues");
            SerializedProperty ms = serializedObject.FindProperty("instrumentMagnificationMValues");
            SerializedProperty zooms = serializedObject.FindProperty("contentZoomValues");
            SerializedProperty ks = serializedObject.FindProperty("instrumentDistortionKValues");
            var culture = CultureInfo.GetCultureInfo("de-DE");
            var text = new StringBuilder($"Punkte bis ±{reach:0.#}° Kopfdrehung ab Trialbeginn, in jede Richtung.");
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
                        zoom, m, ks.GetArrayElementAtIndex(ki).floatValue, reach);
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
                text.Append($"\nm = {m.ToString("0.##", culture)}: bis ca. {mostDots.ToString("N0", culture)} Punkte");
                if (overLimit)
                    text.Append($" – über {RandomDotFieldStimulus.MaximumDotCount.ToString("N0", culture)}, Sitzung startet nicht");
                if (impossible)
                    text.Append(" – Punktwelt größer als 170°, Sitzung startet nicht");
            }
            EditorGUILayout.HelpBox(text.ToString(), tooMuch ? MessageType.Warning : MessageType.Info);
        }

        private void PreviewTakeoverButtons(RandomDotExperimentManager manager)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Übernehmen schreibt FOV, m, Zoom, Auge, Bewegungsart, Achse und " +
                "Geschwindigkeit der Vorschau in „Sitzung und Versuchsplan“ bzw. „Simulierte Bewegung“ " +
                "(Listen werden durch den einen Vorschau-Wert ersetzt) und behält das Punktbild " +
                "(Dichte, Punktgröße, Edge Softness, Farben). Die k-Liste bleibt unverändert. " +
                "Im Play Mode nur bei laufender Vorschau; nach dem Stoppen wird es in die Szene " +
                "übernommen – dann Szene speichern.", MessageType.Info);
            // Im Play Mode ohne Vorschau trägt der Stimulus Werte des letzten Trials.
            bool canTakeOver = !Application.isPlaying || manager.IsPreviewActive;
            using (new EditorGUI.DisabledScope(!canTakeOver))
            {
                if (GUILayout.Button("Vorschau-Werte ins Experiment übernehmen"))
                {
                    serializedObject.ApplyModifiedProperties();
                    RandomDotPreviewTakeoverEditor.TakeOver(manager);
                    serializedObject.Update();
                }
            }
            using (new EditorGUI.DisabledScope(!manager.HasValuesBeforePreviewTakeover))
            {
                if (GUILayout.Button("Originalwerte wiederherstellen"))
                {
                    serializedObject.ApplyModifiedProperties();
                    RandomDotPreviewTakeoverEditor.Restore(manager);
                    serializedObject.Update();
                }
            }
            if (manager.HasValuesBeforePreviewTakeover)
                EditorGUILayout.HelpBox("Versuchsplan enthält übernommene Vorschau-Werte. " +
                    "Originalwerte sind gesichert.", MessageType.Warning);
        }
    }
}
