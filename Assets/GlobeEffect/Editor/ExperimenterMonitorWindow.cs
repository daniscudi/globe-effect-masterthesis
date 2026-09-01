using System.Globalization;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEditor;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Editor
{
    /// <summary>
    /// Versuchsleiteranzeige für beide Tests. Als EditorWindow wird sie nur
    /// auf dem Kontrollmonitor dargestellt und nicht in das XR-Bild gerendert.
    /// Sie verändert keine Trialparameter und dient ausschließlich der
    /// laufenden Qualitätskontrolle.
    /// </summary>
    public sealed class ExperimenterMonitorWindow : EditorWindow
    {
        internal const string AutoOpenPreference =
            "GlobeEffect.ExperimentMonitor.AutoOpenOnPlay";

        private const string OpenMenuPath =
            "Tools/Globe Effect/Open Experiment Monitor";
        private const string AutoOpenMenuPath =
            "Tools/Globe Effect/Auto Open Experiment Monitor on Play";

        private CheckerboardFixationMonitor checkerboardFixation;
        private RandomDotFixationMonitor randomDotFixation;
        private CheckerboardTrialSessionController checkerboardSession;
        private RandomDotTrialSessionController randomDotSession;
        private VrCheckerboardStimulus checkerboardStimulus;
        private RandomDotFieldStimulus randomDotStimulus;
        private RandomDotHeadSweepMonitor sweepMonitor;
        private double nextReferenceRefresh;
        private GUIStyle statusStyle;
        private GUIStyle centeredLabelStyle;

        [MenuItem(OpenMenuPath)]
        public static void OpenWindow()
        {
            OpenWindow(focus: true);
        }

        internal static void OpenWindow(bool focus)
        {
            ExperimenterMonitorWindow window =
                GetWindow<ExperimenterMonitorWindow>(
                    utility: false,
                    title: "Experiment Monitor",
                    focus: focus);
            window.titleContent = new GUIContent("Experiment Monitor");
            window.minSize = new Vector2(360f, 390f);
            window.Repaint();
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

        internal static bool AutoOpenEnabled =>
            EditorPrefs.GetBool(AutoOpenPreference, true);

        private void OnEnable()
        {
            titleContent = new GUIContent("Experiment Monitor");
            minSize = new Vector2(360f, 390f);
            RefreshReferences(force: true);
        }

        private void OnInspectorUpdate()
        {
            RefreshReferences(force: false);
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();

            if (!EditorApplication.isPlaying)
            {
                DrawStatusBlock(
                    "PLAY MODE STOPPED",
                    new Color(0.27f, 0.32f, 0.38f));
                EditorGUILayout.HelpBox(
                    "Das Fenster liest die Fixationsdaten automatisch, sobald der Play Mode läuft.",
                    MessageType.Info);
                DrawAutoOpenSetting();
                return;
            }

            RefreshReferences(force: false);
            // Normalerweise enthält eine Demoszene genau einen Fixationsmonitor.
            // Falls beide vorhanden sind, hat das Checkerboard Vorrang, damit die
            // Anzeige auch in einer versehentlich kombinierten Szene eindeutig bleibt.
            if (randomDotFixation != null && checkerboardFixation == null)
            {
                DrawRandomDotMonitor();
            }
            else if (checkerboardFixation != null)
            {
                DrawCheckerboardMonitor();
            }
            else if (randomDotFixation != null)
            {
                DrawRandomDotMonitor();
            }
            else
            {
                DrawStatusBlock(
                    "NO FIXATION MONITOR",
                    new Color(0.65f, 0.36f, 0.08f));
                EditorGUILayout.HelpBox(
                    "In der geöffneten Szene wurde kein Checkerboard- oder Random-Dot-Fixationsmonitor gefunden.",
                    MessageType.Warning);
            }

            DrawAutoOpenSetting();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(
                "Globe Effect – Experimenter Monitor",
                centeredLabelStyle,
                GUILayout.Height(28f));
            EditorGUILayout.Space(3f);
        }

        private void DrawCheckerboardMonitor()
        {
            DrawFixationStatus(
                checkerboardFixation.TargetState,
                checkerboardFixation.CurrentAngleDegrees,
                checkerboardFixation.ToleranceDegrees,
                checkerboardFixation.ContinuousFixationSeconds,
                checkerboardFixation.RequiredContinuousSeconds,
                checkerboardFixation.RequirementMet);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Test", "Checkerboard");
                EditorGUILayout.LabelField(
                    "Session",
                    checkerboardSession != null
                        ? checkerboardSession.SessionState.ToString()
                        : "Nicht gefunden");
                EditorGUILayout.LabelField(
                    "Trial",
                    FormatTrial(
                        checkerboardSession?.CurrentTrialNumber ?? 0,
                        checkerboardSession?.TotalTrials ?? 0));
                EditorGUILayout.LabelField(
                    "Gültig abgeschlossen",
                    checkerboardSession != null
                        ? checkerboardSession.ValidTrialsCompleted.ToString(
                            CultureInfo.InvariantCulture)
                        : "–");
                EditorGUILayout.LabelField(
                    "Präsentationen",
                    checkerboardSession != null
                        ? checkerboardSession.PresentationCount.ToString(
                            CultureInfo.InvariantCulture)
                        : "–");
                EditorGUILayout.LabelField(
                    "Visual-Space l",
                    checkerboardStimulus != null
                        ? checkerboardStimulus.VisualSpaceL.ToString(
                            "F3",
                            CultureInfo.InvariantCulture)
                        : "–");
                EditorGUILayout.LabelField(
                    "FOV",
                    checkerboardStimulus != null
                        ? checkerboardStimulus.AngularDiameterDegrees.ToString(
                            "F1",
                            CultureInfo.InvariantCulture) + "°"
                        : "–");
                EditorGUILayout.LabelField(
                    "Augenmodus",
                    checkerboardStimulus != null
                        ? checkerboardStimulus.EyePresentation.ToString()
                        : "–");
                EditorGUILayout.LabelField(
                    "Antwort",
                    checkerboardSession != null &&
                    checkerboardSession.ResponseKeysSwapped
                        ? "← konvex   |   konkav →"
                        : "← konkav   |   konvex →");
                EditorGUILayout.LabelField(
                    "Fixationsbruch",
                    checkerboardSession != null && checkerboardSession.RequireFixation
                        ? "Trial ungültig, Wiederholung hinten"
                        : "Kontrolle ausgeschaltet");
            }
        }

        private void DrawRandomDotMonitor()
        {
            DrawFixationStatus(
                randomDotFixation.TargetState,
                randomDotFixation.CurrentAngleDegrees,
                randomDotFixation.ToleranceDegrees,
                randomDotFixation.ContinuousFixationSeconds,
                randomDotFixation.RequiredContinuousSeconds,
                randomDotFixation.RequirementMet);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Test", "Random Dot k");
                EditorGUILayout.LabelField(
                    "Session",
                    randomDotSession != null
                        ? randomDotSession.SessionState.ToString()
                        : "Nicht gefunden");
                EditorGUILayout.LabelField(
                    "Trial",
                    FormatTrial(
                        randomDotSession?.CurrentTrialNumber ?? 0,
                        randomDotSession?.TotalTrials ?? 0));
                EditorGUILayout.LabelField(
                    "k",
                    randomDotStimulus != null
                        ? randomDotStimulus.MerlitzK.ToString("F3", CultureInfo.InvariantCulture)
                        : "–");
                EditorGUILayout.LabelField(
                    "Gierwinkel",
                    sweepMonitor != null
                        ? sweepMonitor.CurrentYawDegrees.ToString("F2", CultureInfo.InvariantCulture) + "°"
                        : "–");
                EditorGUILayout.LabelField(
                    "Bewegungswechsel",
                    sweepMonitor != null
                        ? sweepMonitor.CompletedHalfSweeps.ToString(
                            CultureInfo.InvariantCulture)
                        : "–");
                EditorGUILayout.LabelField(
                    "Antwort",
                    randomDotSession != null && randomDotSession.ResponseKeysSwapped
                        ? "← konvex   |   konkav →"
                        : "← konkav   |   konvex →");
                EditorGUILayout.LabelField(
                    "Fixationsbruch",
                    randomDotSession != null && randomDotSession.RequireFixation
                        ? "Trial ungültig, Wiederholung hinten"
                        : "Kontrolle ausgeschaltet");
            }
        }

        private void DrawFixationStatus(
            FixationTargetState state,
            float angleDegrees,
            float toleranceDegrees,
            float continuousSeconds,
            float requiredSeconds,
            bool requirementMet)
        {
            string label;
            Color color;
            switch (state)
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
                EditorGUILayout.LabelField(
                    "Blickabweichung",
                    float.IsNaN(angleDegrees)
                        ? "–"
                        : angleDegrees.ToString("F2", CultureInfo.InvariantCulture) + "°");
                EditorGUILayout.LabelField(
                    "Toleranz",
                    toleranceDegrees.ToString("F2", CultureInfo.InvariantCulture) + "°");
                EditorGUILayout.LabelField(
                    "Kontinuierliche Fixation",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:F2} / {1:F2} s",
                        continuousSeconds,
                        requiredSeconds));
                EditorGUILayout.LabelField(
                    "Fixationskriterium",
                    requirementMet ? "ERFÜLLT" : "NICHT ERFÜLLT");
            }
        }

        private void DrawStatusBlock(string label, Color backgroundColor)
        {
            Rect statusRect = GUILayoutUtility.GetRect(
                100f,
                88f,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(statusRect, backgroundColor);
            GUI.Label(statusRect, label, statusStyle);
        }

        private static string FormatTrial(int current, int total)
        {
            return total > 0 ? $"{current} / {total}" : "–";
        }

        private static void DrawAutoOpenSetting()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "Automatisch bei Play öffnen",
                AutoOpenEnabled ? "JA" : "NEIN");
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
            // FindAnyObjectByType durchsucht die geladene Szene. Eine Aktualisierung
            // zweimal pro Sekunde reicht für Referenzwechsel und entlastet den Editor.
            if (!force && now < nextReferenceRefresh)
            {
                return;
            }

            nextReferenceRefresh = now + 0.5d;
            checkerboardFixation =
                Object.FindAnyObjectByType<CheckerboardFixationMonitor>();
            randomDotFixation =
                Object.FindAnyObjectByType<RandomDotFixationMonitor>();
            checkerboardSession =
                Object.FindAnyObjectByType<CheckerboardTrialSessionController>();
            randomDotSession =
                Object.FindAnyObjectByType<RandomDotTrialSessionController>();
            checkerboardStimulus =
                Object.FindAnyObjectByType<VrCheckerboardStimulus>();
            randomDotStimulus =
                Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            sweepMonitor =
                Object.FindAnyObjectByType<RandomDotHeadSweepMonitor>();
        }
    }

    /// <summary>
    /// Öffnet die reine Editoranzeige automatisch, nachdem die Szene in den
    /// Play Mode gewechselt ist. Dadurch kann die Statusanzeige im Labor nicht
    /// versehentlich vergessen werden.
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
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !ExperimenterMonitorWindow.AutoOpenEnabled)
            {
                return;
            }

            EditorApplication.delayCall += OpenIfExperimentSceneIsActive;
        }

        private static void OpenIfExperimentSceneIsActive()
        {
            bool hasFixationMonitor =
                Object.FindAnyObjectByType<CheckerboardFixationMonitor>() != null ||
                Object.FindAnyObjectByType<RandomDotFixationMonitor>() != null;
            if (hasFixationMonitor)
            {
                // Automatisches Öffnen darf der Game View nicht den
                // Tastaturfokus für F5, Pfeiltasten oder Enter nehmen.
                ExperimenterMonitorWindow.OpenWindow(focus: false);
            }
        }
    }
}
