using System;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Hält die tatsächlich geladenen Einstellungen beim Sitzungsstart fest.
    /// Unity serialisiert die Komponenten selbst: keine zweite Liste derselben
    /// Versuchsparameter, die später mit dem Inspector auseinanderlaufen könnte.
    /// Objektverweise sind Unity-Instance-IDs, keine portablen Szenenverweise.
    /// </summary>
    public static class ExperimentSessionSettings
    {
        [Serializable]
        private sealed class EnvironmentSettings
        {
            public int schemaVersion = 1;
            public string capturedUtc;
            public string unityVersion;
            public string applicationVersion;
            public string scene;
            public string colorSpace;
            public string configuredEyeTracker;
            public string activeEyeTracker;
            public string xrLoader;
            public string keyboardMapping;
            public string controllerMapping;
        }

        [Serializable]
        private sealed class FixationSettings
        {
            public float toleranceDegrees;
            public float requiredSteadySeconds;
        }

        public static string Capture(MonoBehaviour manager, MonoBehaviour stimulus,
            MonoBehaviour fixationMonitor, ResponseInputController controller, EyeTrackingToolbox toolbox)
        {
            var environment = new EnvironmentSettings
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                applicationVersion = Application.version,
                scene = SceneManager.GetActiveScene().path,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                configuredEyeTracker = toolbox != null ? toolbox.Provider.ToString() : "None",
                activeEyeTracker = toolbox != null ? toolbox.ActiveProvider.ToString() : "None",
                xrLoader = EyeTrackerProviderResolver.GetActiveXrLoaderName(),
                keyboardMapping = controller != null ? controller.KeyboardSummary : "None",
                controllerMapping = controller != null ? controller.ControllerSummary : "None"
            };
            // Nur das Kriterium speichern, nicht den momentanen Winkel. Dieser
            // kann ohne Blickdaten NaN sein und gehört nicht in eine JSON-Konfiguration.
            string fixationJson = fixationMonitor is IFixationCriterion criterion
                ? JsonUtility.ToJson(new FixationSettings
                {
                    toleranceDegrees = criterion.ToleranceDegrees,
                    requiredSteadySeconds = criterion.RequiredContinuousSeconds
                }, true) : "null";
            return "{\n\"environment\": " + JsonUtility.ToJson(environment, true)
                + ",\n\"experimentManagerAtStart\": " + JsonUtility.ToJson(manager, true)
                + ",\n\"stimulusAtStart\": " + JsonUtility.ToJson(stimulus, true)
                + ",\n\"fixationCriterion\": " + fixationJson
                + ",\n\"responseControllerAtStart\": " + JsonUtility.ToJson(controller, true) + "\n}";
        }
    }
}
