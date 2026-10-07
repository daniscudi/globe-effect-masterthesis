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
                    "Das Fenster liest die Fixationsdaten automatisch, sobald der Play Mode läuft.",
                    MessageType.Info);
                DrawAutoOpenSetting();
                return;
            }

            RefreshReferences(force: false);
            // In einer Szene liegt normalerweise nur einer der beiden Tests.
            // Sind aus Versehen doch beide drin, wird das Checkerboard angezeigt.
            // Hauptsache, es ist eindeutig und flackert nicht hin und her.
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
                DrawStatusBlock("NO FIXATION MONITOR", new Color(0.65f, 0.36f, 0.08f));
                EditorGUILayout.HelpBox(
                    "In der geöffneten Szene wurde kein Checkerboard- oder " +
                    "Random-Dot-Fixationsmonitor gefunden.",
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
                Row("Session", hasSession ? session.SessionState.ToString() : "Nicht gefunden");
                Row("Trial",
                    hasSession ? FormatTrial(session.CurrentTrialNumber, session.TotalTrials) : Missing);
                Row("Gültig abgeschlossen", hasSession ? Number(session.ValidTrialsCompleted) : Missing);
                Row("Präsentationen", hasSession ? Number(session.PresentationCount) : Missing);
                Row("Visual-Space l", hasStimulus ? Number(stimulus.VisualSpaceL, "F3") : Missing);
                Row("FOV", hasStimulus ? Number(stimulus.AngularDiameterDegrees, "F1", "°") : Missing);
                Row("Augenmodus", hasStimulus ? stimulus.EyePresentation.ToString() : Missing);
                // Konvex ist Category A, konkav Category B.
                Row("Ablauf", hasSession ? session.TrialSequence.ToString() : Missing);
                Row("Antwort Controller", hasSession ? session.ControllerSummary : Missing);
                Row("Antwort Tastatur", hasSession ? session.KeyboardSummary : Missing);
                Row("Training", hasSession && session.TrainingCompleted
                    ? "abgeschlossen"
                    : "noch nicht abgeschlossen");
                Row("Fixationsbruch", hasSession && session.RequireFixation
                    ? "Trial ungültig, Wiederholung hinten"
                    : "Kontrolle ausgeschaltet");
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
                Row("Session", hasSession ? session.SessionState.ToString() : "Nicht gefunden");
                Row("Trial",
                    hasSession ? FormatTrial(session.CurrentTrialNumber, session.TotalTrials) : Missing);
                Row("Bewegungsart", hasSession ? session.SessionMotionMode.ToString() : Missing);
                if (hasSession && session.SessionState == RandomDotSessionState.ResponseInstructions)
                {
                    EditorGUILayout.HelpBox(
                        "Die Person liest die Tastenbelegung – mit F5 starten.", MessageType.Info);
                }

                Row("Instrument k", hasStimulus ? Number(stimulus.InstrumentDistortionK, "F3") : Missing);
                Row("Vergrößerung m",
                    hasStimulus ? Number(stimulus.InstrumentMagnificationM, "F2", "x") : Missing);
                Row("Content Zoom", hasStimulus ? Number(stimulus.ContentZoom, "F2") : Missing);
                Row("Gierwinkel", hasSweep ? Number(sweep.CurrentYawDegrees, "F2", "°") : Missing);
                Row("Bewegungswechsel", hasSweep ? Number(sweep.CompletedHalfSweeps) : Missing);
                Row("Mittlere Kopfgeschwindigkeit",
                    hasSweep ? Number(sweep.MeanAbsoluteYawSpeedDegreesPerSecond, "F2", "°/s") : Missing);
                Row("Spitzen-Kopfgeschwindigkeit",
                    hasSweep ? Number(sweep.PeakAbsoluteYawSpeedDegreesPerSecond, "F2", "°/s") : Missing);
                Row("Maximale Kopfauslenkung",
                    hasSweep ? Number(sweep.MaximumAbsoluteYawDegrees, "F2", "°") : Missing);
                Row("Antwort Controller", hasSession ? session.ControllerSummary : Missing);
                Row("Antwort Tastatur", hasSession ? session.KeyboardSummary : Missing);
                Row("Fixationsbruch", hasSession && session.RequireFixation
                    ? "Trial ungültig, Wiederholung hinten"
                    : "Kontrolle ausgeschaltet");
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
                    label = "NO VALID GAZE";
                    color = new Color(0.78f, 0.52f, 0.04f);
                    break;
            }

            DrawStatusBlock(label, color);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                float angle = fixation.CurrentAngleDegrees;
                Row("Blickabweichung", float.IsNaN(angle) ? Missing : Number(angle, "F2", "°"));
                Row("Toleranz", Number(fixation.ToleranceDegrees, "F2", "°"));
                Row("Kontinuierliche Fixation",
                    Number(fixation.ContinuousFixationSeconds, "F2") + " / " +
                    Number(fixation.RequiredContinuousSeconds, "F2") + " s");
                Row("Fixationskriterium", fixation.RequirementMet ? "ERFÜLLT" : "NICHT ERFÜLLT");
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
                    ? toolbox.ActiveProvider + " (eingestellt: " + toolbox.Provider + ")"
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
            Row("Automatisch bei Play öffnen", AutoOpenEnabled ? "JA" : "NEIN");
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
