using System;
using System.IO;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace GlobeEffect.VRCheckerboard.Editor
{
    /// <summary>
    /// Erstellt eine eigenständige Referenzszene für den dynamischen
    /// Random-Dot-k-Test. Checkerboard und Punktfeld bleiben in getrennten
    /// Szenen, teilen aber XR-Rig, Eye-Tracking-Toolbox und Datenformat.
    /// </summary>
    [InitializeOnLoad]
    public static class RandomDotDemoSceneBuilder
    {
        public const string DemoScenePath =
            "Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity";

        private const string MenuPath =
            "Tools/Globe Effect/Create or Reset Random Dot Demo Scene";

        static RandomDotDemoSceneBuilder()
        {
            // Die Prüfung läuft verzögert, weil Unity während des ersten Imports
            // noch Skripte und Pakete laden kann.
            EditorApplication.delayCall += CreateSceneOnFirstImport;
        }

        [MenuItem(MenuPath)]
        public static void CreateOrResetDemoScene()
        {
            CreateDemoScene(replaceExistingScene: true);
        }

        private static void CreateSceneOnFirstImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CreateSceneOnFirstImport;
                return;
            }

            if (!File.Exists(DemoScenePath))
            {
                CreateDemoScene(replaceExistingScene: false);
            }
        }

        private static void CreateDemoScene(bool replaceExistingScene)
        {
            if (!replaceExistingScene && File.Exists(DemoScenePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DemoScenePath));

            Scene previousActiveScene = SceneManager.GetActiveScene();
            // Eigene geöffnete Szenen bleiben unangetastet. Die Demo wird dann
            // additiv erstellt, gespeichert und anschließend wieder geschlossen.
            bool replaceUntitledScene = string.IsNullOrEmpty(previousActiveScene.path);
            bool replaceOpenDemoScene = replaceExistingScene &&
                previousActiveScene.path == DemoScenePath;
            bool replaceActiveScene = replaceUntitledScene || replaceOpenDemoScene;

            if (replaceActiveScene && previousActiveScene.isDirty &&
                previousActiveScene.rootCount > 0)
            {
                if (Application.isBatchMode)
                {
                    throw new InvalidOperationException(
                        "Eine ungespeicherte Szene verhindert das Erstellen der Random-Dot-Demoszene.");
                }

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }
            }

            Scene demoScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                replaceActiveScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(demoScene);

            try
            {
                // Der Sweep-Monitor sitzt direkt am Punktfeld. In der
                // Hauptbedingung protokolliert er die simulierte Bewegung; die
                // Kamera braucht er nur für den optionalen HeadTracked-Modus.
                Camera camera = CreateXrOrigin();
                RandomDotFieldStimulus stimulus = CreateStimulus(camera.transform);
                RandomDotHeadSweepMonitor sweepMonitor =
                    stimulus.gameObject.AddComponent<RandomDotHeadSweepMonitor>();
                sweepMonitor.Configure(camera.transform, stimulus);
                sweepMonitor.ConfigureCriterion(2.5f, 4);

                EyeTrackingToolbox toolbox = CreateEyeTracking(
                    camera,
                    stimulus,
                    out RandomDotFixationMonitor fixationMonitor);
                CreateTrialController(
                    stimulus,
                    sweepMonitor,
                    toolbox,
                    fixationMonitor);
                CreateEnvironment();

                EditorSceneManager.MarkSceneDirty(demoScene);
                if (!EditorSceneManager.SaveScene(demoScene, DemoScenePath))
                {
                    throw new InvalidOperationException(
                        $"Demoszene konnte nicht gespeichert werden: {DemoScenePath}");
                }

                EnsureSceneIsInBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Random-Dot-Demoszene erstellt: {DemoScenePath}");
            }
            finally
            {
                if (!replaceActiveScene &&
                    previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                    EditorSceneManager.CloseScene(demoScene, removeScene: true);
                }
            }
        }

        private static Camera CreateXrOrigin()
        {
            // XR Origin und Camera Offset bilden den stabilen Weltbezug; nur die
            // Main Camera erhält in Play Mode die Center-Eye-Pose des Headsets.
            GameObject originObject = new GameObject("XR Origin");
            XROrigin xrOrigin = originObject.AddComponent<XROrigin>();

            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 90f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            cameraObject.AddComponent<AudioListener>();
            ConfigureTrackedPoseDriver(cameraObject.AddComponent<TrackedPoseDriver>());

            xrOrigin.Origin = originObject;
            xrOrigin.CameraFloorOffsetObject = cameraOffset;
            xrOrigin.Camera = camera;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            xrOrigin.CameraYOffset = 0f;
            return camera;
        }

        private static void ConfigureTrackedPoseDriver(TrackedPoseDriver driver)
        {
            // Es werden nur HMD-Pose und Trackingstatus benötigt. Deshalb kann die
            // Demoszene ohne Controller-Actions oder eigenes Input-Asset auskommen.
            InputAction position = new InputAction(
                name: "HMD Position",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");
            InputAction rotation = new InputAction(
                name: "HMD Rotation",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");
            InputAction trackingState = new InputAction(
                name: "HMD Tracking State",
                type: InputActionType.Value,
                binding: "<XRHMD>/trackingState",
                expectedControlType: "Integer");

            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = false;
            driver.positionInput = new InputActionProperty(position);
            driver.rotationInput = new InputActionProperty(rotation);
            driver.trackingStateInput = new InputActionProperty(trackingState);
        }

        private static RandomDotFieldStimulus CreateStimulus(Transform observer)
        {
            // Diese Werte sind technische Startwerte für die Funktionsprüfung und
            // noch keine festgelegten Bedingungen des späteren Experiments.
            GameObject stimulusObject = new GameObject("Random Dot Field");
            RandomDotFieldStimulus stimulus =
                stimulusObject.AddComponent<RandomDotFieldStimulus>();
            stimulus.Observer = observer;
            stimulus.SetAngularDiameter(70f);
            stimulus.SetApertureEdgeSoftness(1f);
            stimulus.SetMagnification(10f);
            stimulus.SetMerlitzK(0.7f);
            stimulus.SetEyePresentation(CheckerboardEyePresentation.BothEyes);
            stimulus.SetMotionMode(RandomDotMotionMode.SimulatedYaw);
            stimulus.SetSimulatedSweep(5f, 5f);
            stimulus.SetSweepDirection(RandomDotSweepDirection.RightFirst);
            stimulus.ConfigurePointField(4000, 20260828, 20f);
            stimulus.PlaceAroundObserver();
            stimulusObject.AddComponent<RandomDotKeyboardController>();
            Selection.activeGameObject = stimulusObject;
            return stimulus;
        }

        private static EyeTrackingToolbox CreateEyeTracking(
            Camera camera,
            RandomDotFieldStimulus stimulus,
            out RandomDotFixationMonitor fixationMonitor)
        {
            // Die Toolbox schreibt Blickdaten und die Transformationen von Kopf
            // und Punktfeld in zeitlich zuordenbare CSV-Dateien.
            GameObject toolboxObject = new GameObject("Eye Tracking Toolbox");
            EyeTrackingToolbox toolbox =
                toolboxObject.AddComponent<EyeTrackingToolbox>();
            toolbox.SetProvider(EyeTrackingToolbox.ETProvider.Varjo);
            toolbox.SetMainCameraTransform(camera.transform);
            toolbox.AddTrackedObject(
                camera.gameObject,
                EyeTrackingToolbox.TrackingOptions.LocalTransform);
            toolbox.AddTrackedObject(
                stimulus.gameObject,
                EyeTrackingToolbox.TrackingOptions.GlobalTransform);

            fixationMonitor = toolboxObject.AddComponent<RandomDotFixationMonitor>();
            fixationMonitor.Configure(toolbox, stimulus);
            return toolbox;
        }

        private static void CreateTrialController(
            RandomDotFieldStimulus stimulus,
            RandomDotHeadSweepMonitor sweepMonitor,
            EyeTrackingToolbox toolbox,
            RandomDotFixationMonitor fixationMonitor)
        {
            GameObject controllerObject = new GameObject("Random Dot Trial Session");
            RandomDotTrialSessionController controller =
                controllerObject.AddComponent<RandomDotTrialSessionController>();
            controller.Configure(
                stimulus,
                stimulus.GetComponent<RandomDotKeyboardController>(),
                sweepMonitor,
                toolbox,
                fixationMonitor);
        }

        private static void CreateEnvironment()
        {
            GameObject environment = new GameObject("Environment");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (disabled orientation reference)";
            floor.transform.SetParent(environment.transform, false);
            floor.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            floor.GetComponent<MeshRenderer>().enabled = false;
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static void EnsureSceneIsInBuildSettings()
        {
            // Bestehende Build-Szenen werden übernommen; die Demo wird höchstens
            // einmal und standardmäßig aktiviert ergänzt.
            EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in currentScenes)
            {
                if (scene.path == DemoScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] updatedScenes =
                new EditorBuildSettingsScene[currentScenes.Length + 1];
            currentScenes.CopyTo(updatedScenes, 0);
            updatedScenes[currentScenes.Length] =
                new EditorBuildSettingsScene(DemoScenePath, enabled: true);
            EditorBuildSettings.scenes = updatedScenes;
        }
    }
}
