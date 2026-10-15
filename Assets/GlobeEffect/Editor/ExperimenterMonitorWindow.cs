using System.Globalization;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEditor;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Editor
{
    /// <summary>
    /// Das Kontrollfenster für den Versuchsleiter. Es gilt für beide Tests.
    ///
    /// Weil es ein Editorfenster ist, sieht man es nur auf dem Monitor am PC.
    /// Im Headset taucht davon nichts auf, die Versuchsperson sieht es also nicht.
    ///
    /// Hier wird nur zugeschaut. Am Versuch selbst ändert dieses Fenster nichts.
    /// </summary>
    public sealed class ExperimenterMonitorWindow : EditorWindow
    {
        // Das Fenster liest nur ab, was die Manager und Monitore gerade melden.
        // Es fasst weder den Plan noch die Antworten noch den Stimulus an.
        internal const string AutoOpenPreference = "GlobeEffect.ExperimentMonitor.AutoOpenOnPlay";

        private const string WindowTitle = "Experiment Monitor";
        private const string OpenMenuPath = "Tools/Globe Effect/Open Experiment Monitor";
        private const string AutoOpenMenuPath = "Tools/Globe Effect/Auto Open Experiment Monitor on Play";

        // Das steht da, wo es gerade keinen Wert gibt.
        private const string Missing = "–";

        private CheckerboardFixationMonitor checkerboardFixation;
        private RandomDotFixationMonitor randomDotFixation;
        private CheckerboardExperimentManager checkerboardSession;
        private RandomDotExperimentManager randomDotSession;
        private VrCheckerboardStimulus checkerboardStimulus;
        private RandomDotFieldStimulus randomDotStimulus;
        private RandomDotHeadSweepMonitor sweepMonitor;
        private double nextReferenceRefresh;
        private GUIStyle statusStyle;
        private GUIStyle centeredLabelStyle;
        private Vector2 scrollPosition;

        internal static bool AutoOpenEnabled => EditorPrefs.GetBool(AutoOpenPreference, true);

        [MenuItem(OpenMenuPath)]
        public static void OpenWindow()
        {
            OpenWindow(focus: true);
        }

        internal static void OpenWindow(bool focus)
        {
            // Titel und Mindestgröße setzt OnEnable, sobald das Fenster entsteht.
            GetWindow<ExperimenterMonitorWindow>(utility: false, title: WindowTitle, focus: focus).Repaint();
        }

        [MenuItem(AutoOpenMenuPath)]
        private static void ToggleAutoOpen()
        {
            bool nextValue = !AutoOpenEnabled;
            EditorPrefs.SetBool(AutoOpenPreference, nextValue);
            Menu.SetChecked(AutoOpenMenuPath, nextValue);
        }

        [MenuItem(AutoOpenMenuPath, true)]
        private static bool ValidateAutoOpenMenu()
        {
            Menu.SetChecked(AutoOpenMenuPath, AutoOpenEnabled);
            return true;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(360f, 390f);
            RefreshReferences(force: true);
        }

        private void OnInspectorUpdate()
        {
            // Ein Editorfenster bekommt kein normales Update wie ein Skript in der
            // Szene. Stattdessen meldet man sich hier an und wird regelmäßig
            // aufgerufen. Nur so bleiben die angezeigten Werte aktuell.
            RefreshReferences(force: false);
            Repaint();
        }

        private void OnGUI()
        {
            // OnGUI malt den Inhalt des Fensters. Das läuft immer wieder von vorne.
            EnsureStyles();
            DrawHeader();

            if (!EditorApplication.isPlaying)
            {
                DrawStatusBlock("PLAY MODE STOPPED", new Color(0.27f, 0.32f, 0.38f));
                EditorGUILayout.HelpBox(
                    "The gaze data shows up here automatically when Play Mode runs.",
                    MessageType.Info);
                DrawAutoOpenSetting();
                return;
            }

            // Unity ruft OnGUI zweimal hintereinander auf: erst zum Ausmessen
            // (Layout), dann zum Zeichnen (Repaint). Beide Male müssen genau
            // dieselben Zeilen kommen. Deshalb wird nur beim Ausmessen neu gesucht,
            // sonst gibt es "Getting control ... position" Fehler.
            if (Event.current.type == EventType.Layout)
            {
                RefreshReferences(force: false);
            }

            // In einer Szene liegt normalerweise nur einer der beiden Tests.
            // Sind aus Versehen doch beide drin, wird das Checkerboard angezeigt.
            // Hauptsache, es ist eindeutig und flackert nicht hin und her.
            // Scrollbar, weil beim Random-Dot-Test mit Kopftempo viele Zeilen zusammenkommen.
            using var view = new EditorGUILayout.ScrollViewScope(scrollPosition);
            scrollPosition = view.scrollPosition;
            if (checkerboardFixation != null)
            {
                DrawCheckerboardMonitor();
            }
            else if (randomDotFixation != null)
            {
                DrawRandomDotMonitor();
            }
            else
            {
                DrawStatusBlock("NO TEST FOUND", new Color(0.65f, 0.36f, 0.08f));
                EditorGUILayout.HelpBox(
                    "No checkerboard or random dot test was found in the open scene.",
                    MessageType.Warning);
            }

            DrawAutoOpenSetting();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(
                "Globe Effect – Experimenter Monitor", centeredLabelStyle, GUILayout.Height(28f));
            EditorGUILayout.Space(3f);
        }

        private void DrawCheckerboardMonitor()
        {
            // Zeigt an: Wo schaut die Person hin, wie weit ist die Sitzung, und
            // welche Bedingung läuft gerade.
            DrawFixationStatus(checkerboardFixation);
            DrawEyeTrackerStatus(checkerboardSession != null && checkerboardSession.RequireFixation);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Fehlt der Manager oder der Stimulus, steht in den Zeilen nur ein Strich.
                CheckerboardExperimentManager session = checkerboardSession;
                VrCheckerboardStimulus stimulus = checkerboardStimulus;
                bool hasSession = session != null;
                bool hasStimulus = stimulus != null;

                Row("Test", "Checkerboard");
                Row("Session", hasSession ? session.SessionState.ToString() : "Not found");
                Row("Trial",
                    hasSession ? FormatTrial(session.CurrentTrialNumber, session.TotalTrials) : Missing);
                Row("Valid Trials Done", hasSession ? Number(session.ValidTrialsCompleted) : Missing);
                Row("Trials Shown (With Repeats)", hasSession ? Number(session.PresentationCount) : Missing);
                Row("Grid Distortion l", hasStimulus ? Number(stimulus.VisualSpaceL, "F3") : Missing);
                Row("FOV", hasStimulus ? Number(stimulus.AngularDiameterDegrees, "F1", "°") : Missing);
                Row("Eyes", hasStimulus ? stimulus.EyePresentation.ToString() : Missing);
                // Konvex ist Category A, konkav Category B.
                Row("Trial Order", hasSession ? session.TrialSequence.ToString() : Missing);
                Row("Controller Answers", hasSession ? session.ControllerSummary : Missing);
                Row("Keyboard Answers", hasSession ? session.KeyboardSummary : Missing);
                Row("Practice", hasSession && session.TrainingCompleted
                    ? "done"
                    : "not done yet");
                Row("Looking Away", hasSession && session.RequireFixation
                    ? "trial is repeated later"
                    : "gaze check is off");
            }
        }

        private void DrawRandomDotMonitor()
        {
            // Zeigt an: Wo schaut die Person hin, wie weit ist die Sitzung, und wo
            // steht die Bewegung der Punkte gerade.
            DrawFixationStatus(randomDotFixation);
            DrawEyeTrackerStatus(randomDotSession != null && randomDotSession.RequireFixation);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // Fehlt eines der Objekte, steht in den passenden Zeilen nur ein Strich.
                RandomDotExperimentManager session = randomDotSession;
                RandomDotFieldStimulus stimulus = randomDotStimulus;
                RandomDotHeadSweepMonitor sweep = sweepMonitor;
                bool hasSession = session != null;
                bool hasStimulus = stimulus != null;
                bool hasSweep = sweep != null;

                Row("Test", "Random Dot Instrument");
                Row("Session", hasSession ? session.SessionState.ToString() : "Not found");
                Row("Trial",
                    hasSession ? FormatTrial(session.CurrentTrialNumber, session.TotalTrials) : Missing);
                Row("Movement Type", hasSession ? session.SessionMotionMode.ToString() : Missing);
                if (hasSession && session.SessionState == RandomDotSessionState.ResponseInstructions)
                {
                    EditorGUILayout.HelpBox(
                        "The participant is reading the answer keys. Press F5 to start.", MessageType.Info);
                }

                Row("Distortion k", hasStimulus ? Number(stimulus.InstrumentDistortionK, "F3") : Missing);
                Row("Magnification m",
                    hasStimulus ? Number(stimulus.InstrumentMagnificationM, "F2", "x") : Missing);
                Row("Extra Zoom", hasStimulus ? Number(stimulus.ContentZoom, "F2") : Missing);
                Row("Head Turn Left/Right", hasSweep ? Number(sweep.CurrentYawDegrees, "F2", "°") : Missing);
                Row("Left-Right Turns", hasSweep ? Number(sweep.CompletedHalfSweeps) : Missing);
                Row("Average Head Speed",
                    hasSweep ? Number(sweep.MeanAbsoluteYawSpeedDegreesPerSecond, "F2", "°/s") : Missing);
                Row("Top Head Speed",
                    hasSweep ? Number(sweep.PeakAbsoluteYawSpeedDegreesPerSecond, "F2", "°/s") : Missing);
                Row("Largest Head Turn",
                    hasSweep ? Number(sweep.MaximumAbsoluteYawDegrees, "F2", "°") : Missing);
                Row("Controller Answers", hasSession ? session.ControllerSummary : Missing);
                Row("Keyboard Answers", hasSession ? session.KeyboardSummary : Missing);
                Row("Looking Away", hasSession && session.RequireFixation
                    ? "trial is repeated later"
                    : "gaze check is off");
            }

            if (randomDotSession != null) DrawLiveHeadSpeed(randomDotSession, randomDotStimulus);
        }

        // Live-Kopftempo für den Pilot: Wie schnell dreht der Kopf, wie schnell laufen
        // die Punkte dabei, und dreht der Kopf gleichmäßig genug, dass sie jetzt erscheinen würden?
        private static void DrawLiveHeadSpeed(RandomDotExperimentManager session, RandomDotFieldStimulus stimulus)
        {
            RandomDotLiveHeadSpeed live = session.LiveHeadSpeed;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Head Speed (Live)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!live.HasData)
                {
                    EditorGUILayout.LabelField("No head data yet.");
                    return;
                }

                Row("Head Speed Now", Number(live.HeadSpeed, "F2", " °/s"));
                Row("Head Speed (1 s Average)", Number(live.AverageHeadSpeed, "F2", " °/s"));
                Row("Steady For", Number(live.SteadySeconds, "F2", " s"));
                Row("Simulated Equals", Number(live.SimulatedHeadSpeed, "F2", " °/s head (for comparison)"));
                Row("Dot Speed In Image Center", Number(live.ImageCenterSpeed, "F1", " °/s") +
                    "  (1 s average " + Number(live.AverageImageCenterSpeed, "F1", " °/s)"));
                string dotSize = stimulus != null ? "  (dot size " + Number(stimulus.DotSizeDegrees, "F2", "°)") : "";
                Row("Dot Jump Per Frame", Number(live.ImageStepPerFrameDegrees, "F2", "°") + dotSize);
                Row("Frames Per Second", Number(live.FramesPerSecond, "F0"));

                string state = live.WouldShowDots ? "YES"
                    : !live.IsMoving ? "NO – head is still"
                    : "NOT YET – keep turning steadily";
                Row("Dots Would Appear", state);

                if (stimulus != null && stimulus.DotSizeDegrees > 0f
                    && live.ImageStepPerFrameDegrees > stimulus.DotSizeDegrees)
                    EditorGUILayout.HelpBox("The dots jump further than their own size each frame. " +
                        "They may look jerky.", MessageType.Warning);
            }
            if (session.IsPreviewActive && stimulus != null
                && stimulus.MotionMode != RandomDotMotionMode.HeadTracked)
                EditorGUILayout.HelpBox("To feel the speed yourself, set Movement Type to Head-Tracked in the " +
                    "preview. Then the dots move with your head.", MessageType.Info);

            DrawSavedSpeeds(live);
            DrawLastPans(live);
        }

        // Pilot: per Taste gespeicherte "fühlt sich natürlich an"-Werte und ihr Mittel.
        private static void DrawSavedSpeeds(RandomDotLiveHeadSpeed live)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Saved Natural Speeds", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var saved = live.SavedSpeeds;
                if (saved.Count == 0)
                {
                    EditorGUILayout.LabelField("None yet. In the preview press Space or a controller button " +
                        "while turning at a speed that feels natural.", EditorStyles.wordWrappedLabel);
                    return;
                }

                float sum = 0f, imageSum = 0f, min = float.MaxValue, max = 0f;
                foreach (RandomDotSavedSpeed speed in saved)
                {
                    sum += speed.HeadSpeed;
                    imageSum += speed.ImageSpeed;
                    min = Mathf.Min(min, speed.HeadSpeed);
                    max = Mathf.Max(max, speed.HeadSpeed);
                }
                Row("Saved", Number(saved.Count));
                Row("Average Head Speed", Number(sum / saved.Count, "F2", " °/s") + "  (" +
                    Number(min, "F2") + " – " + Number(max, "F2", " °/s)"));
                Row("Average Dot Speed", Number(imageSum / saved.Count, "F1", " °/s") + " in the image center");
                for (int index = saved.Count - 1; index >= Mathf.Max(0, saved.Count - 5); index--)
                    Row("  #" + (index + 1), Number(saved[index].HeadSpeed, "F2", " °/s head, ") +
                        Number(saved[index].ImageSpeed, "F1", " °/s dots"));
                EditorGUILayout.LabelField("To use it: put the average dot speed into Dot Speed In Image Center " +
                    "(Simulated Movement). The wanted head speed follows automatically.",
                    EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Clear Saved Speeds And Turns")) live.ClearHistory();
            }
        }

        // Pilot: die letzten erkannten Schwenks, auch ohne Tastendruck.
        private static void DrawLastPans(RandomDotLiveHeadSpeed live)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Last Turns", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (live.LastPans.Count == 0)
                {
                    EditorGUILayout.LabelField("No turns yet. A turn ends when the head stops or changes " +
                        "direction.", EditorStyles.wordWrappedLabel);
                    return;
                }
                foreach (RandomDotPan pan in live.LastPans)
                    Row((pan.Direction > 0 ? "→ " : "← ") + Number(pan.Seconds, "F1", " s"),
                        Number(pan.MeanHeadSpeed, "F2", " °/s head (top ") + Number(pan.PeakHeadSpeed, "F1", "), ") +
                        Number(pan.MeanImageSpeed, "F1", " °/s dots"));
            }
        }

        // Klappt mit beiden Fixation Monitoren, weil beide dieselbe Basisklasse
        // haben. TStimulus ist nur der Platzhalter für den jeweiligen Stimulus.
        private void DrawFixationStatus<TStimulus>(FixationMonitorBase<TStimulus> fixation)
            where TStimulus : MonoBehaviour, IFixationStimulus
        {
            string label;
            Color color;
            switch (fixation.TargetState)
            {
                case FixationTargetState.OnTarget:
                    label = "ON TARGET";
                    color = new Color(0.08f, 0.55f, 0.20f);
                    break;
                case FixationTargetState.OffTarget:
                    label = "OFF TARGET";
                    color = new Color(0.72f, 0.10f, 0.10f);
                    break;
                default:
                    label = "NO GAZE DATA";
                    color = new Color(0.78f, 0.52f, 0.04f);
                    break;
            }

            DrawStatusBlock(label, color);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                float angle = fixation.CurrentAngleDegrees;
                Row("Distance From Cross", float.IsNaN(angle) ? Missing : Number(angle, "F2", "°"));
                Row("Allowed Distance", Number(fixation.ToleranceDegrees, "F2", "°"));
                Row("Steady Look",
                    Number(fixation.ContinuousFixationSeconds, "F2") + " / " +
                    Number(fixation.RequiredContinuousSeconds, "F2") + " s");
                Row("Gaze Check", fixation.RequirementMet ? "OK" : "NOT YET");
            }
        }

        // Zeigt, über welchen XR-Loader das Headset läuft und welcher Eye Tracker
        // gewählt wurde. Hat die Auswahl "Auto" nur den Dummy gefunden und startet
        // F5 deshalb keine Sitzung, steht der Grund hier und nicht nur in der Konsole.
        private static void DrawEyeTrackerStatus(bool requireFixation)
        {
            EyeTrackingToolbox toolbox = EyeTrackingToolbox.Instance;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Row("XR-Loader", EyeTrackerProviderResolver.GetActiveXrLoaderName());
                Row("Eye Tracker", toolbox != null
                    ? toolbox.ActiveProvider + " (chosen: " + toolbox.Provider + ")"
                    : Missing);
            }

            if (EyeTrackerProviderResolver.TryGetSessionBlockReason(
                toolbox, requireFixation, out string blockReason))
            {
                EditorGUILayout.HelpBox(blockReason, MessageType.Error);
            }
        }

        private void DrawStatusBlock(string label, Color backgroundColor)
        {
            Rect statusRect = GUILayoutUtility.GetRect(100f, 88f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(statusRect, backgroundColor);
            GUI.Label(statusRect, label, statusStyle);
        }

        // Eine Zeile im Fenster: links die Beschriftung, rechts der Wert.
        private static void Row(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        // Zahlen immer mit Punkt als Dezimalzeichen, dahinter die Einheit.
        private static string Number(float value, string format, string unit = "")
        {
            return value.ToString(format, CultureInfo.InvariantCulture) + unit;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatTrial(int current, int total)
        {
            return total > 0 ? $"{current} / {total}" : Missing;
        }

        private static void DrawAutoOpenSetting()
        {
            EditorGUILayout.Space(6f);
            Row("Open Automatically On Play", AutoOpenEnabled ? "YES" : "NO");
        }

        private void EnsureStyles()
        {
            statusStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                wordWrap = true
            };
            statusStyle.normal.textColor = Color.white;

            centeredLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15
            };
        }

        private void RefreshReferences(bool force)
        {
            double now = EditorApplication.timeSinceStartup;
            // FindAnyObjectByType sucht die ganze Szene ab und ist deshalb langsam.
            // Zweimal pro Sekunde reicht dafür völlig. Würde man das in jedem Frame
            // machen, würde der Editor unnötig ausgebremst.
            if (!force && now < nextReferenceRefresh)
            {
                return;
            }

            nextReferenceRefresh = now + 0.5d;
            checkerboardFixation = Object.FindAnyObjectByType<CheckerboardFixationMonitor>();
            randomDotFixation = Object.FindAnyObjectByType<RandomDotFixationMonitor>();
            checkerboardSession = Object.FindAnyObjectByType<CheckerboardExperimentManager>();
            randomDotSession = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            checkerboardStimulus = Object.FindAnyObjectByType<VrCheckerboardStimulus>();
            randomDotStimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            sweepMonitor = Object.FindAnyObjectByType<RandomDotHeadSweepMonitor>();
        }
    }

    /// <summary>
    /// Macht das Kontrollfenster automatisch auf, sobald der Play Mode startet.
    ///
    /// So kann man im Labor nicht aus Versehen eine Messung fahren, ohne die
    /// Anzeige offen zu haben.
    /// </summary>
    [InitializeOnLoad]
    internal static class ExperimenterMonitorBootstrap
    {
        static ExperimenterMonitorBootstrap()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && ExperimenterMonitorWindow.AutoOpenEnabled)
            {
                EditorApplication.delayCall += OpenIfExperimentSceneIsActive;
            }
        }

        private static void OpenIfExperimentSceneIsActive()
        {
            // Nur aufmachen, wenn in der Szene wirklich einer der beiden Tests
            // liegt. Sonst würde das Fenster bei jeder beliebigen Szene aufgehen.
            bool hasFixationMonitor =
                Object.FindAnyObjectByType<CheckerboardFixationMonitor>() != null ||
                Object.FindAnyObjectByType<RandomDotFixationMonitor>() != null;
            if (hasFixationMonitor)
            {
                // Wichtig: Das Fenster darf sich nicht nach vorne drängeln. Sonst
                // gehen die Tastendrücke an dieses Fenster statt an den Game View,
                // und F5 oder die Pfeiltasten kommen nicht mehr im Versuch an.
                ExperimenterMonitorWindow.OpenWindow(focus: false);
            }
        }
    }
}
