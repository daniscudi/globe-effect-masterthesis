using System;
using System.IO;
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
    /// Erstellt eine sofort nutzbare Referenzszene. Die Szene wird beim ersten
    /// Import automatisch angelegt und kann ueber das Tools-Menue zurueckgesetzt
    /// werden. Dadurch bleibt der eigentliche Stimulus frei von XR-Rig-Details.
    /// </summary>
    [InitializeOnLoad]
    public static class CheckerboardDemoSceneBuilder
    {
        public const string DemoScenePath =
            "Assets/GlobeEffect/Demo/CheckerboardDemo.unity";

        private const string MenuPath =
            "Tools/Globe Effect/Create or Reset Demo Scene";

        private const string RenderProbeMaterialPath =
            "Assets/GlobeEffect/Demo/XrRenderProbe.mat";

        static CheckerboardDemoSceneBuilder()
        {
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
                        "Eine ungespeicherte Szene verhindert das Erstellen der Demoszene.");
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
                Camera camera = CreateXrOrigin();
                CreateStimulus(camera.transform);
                CreateRenderProbe(camera.transform);
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

                Debug.Log($"Checkerboard-Demoszene erstellt: {DemoScenePath}");
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
            GameObject originObject = new GameObject("XR Origin");
            XROrigin xrOrigin = originObject.AddComponent<XROrigin>();

            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);

            Camera camera = cameraObject.AddComponent<Camera>();
            // Wird nur fuer die flache Game-View-Vorschau verwendet. Im XR-Betrieb
            // liefert das Headset seine eigenen Projektionsmatrizen und Winkel.
            camera.fieldOfView = 90f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f);
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
            // Direkte Actions vermeiden eine Abhaengigkeit von einem bestimmten
            // Controller- oder Headset-Profil. Sie lesen nur die Center-Eye-Pose.
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

        private static void CreateStimulus(Transform observer)
        {
            GameObject stimulusObject = new GameObject("Checkerboard Stimulus");
            VrCheckerboardStimulus stimulus =
                stimulusObject.AddComponent<VrCheckerboardStimulus>();

            // Referenzwerte aus der dokumentierten Ausgangskonfiguration.
            stimulus.Observer = observer;
            stimulus.SetGeometry(70f, 1f);
            stimulus.SetMerlitzK(0.7f);
            stimulus.SetMagnification(10f);
            stimulus.SetEyePresentation(CheckerboardEyePresentation.BothEyes);
            stimulus.PlaceInFrontOfObserver();

            Selection.activeGameObject = stimulusObject;
        }

        private static void CreateEnvironment()
        {
            GameObject environment = new GameObject("Environment");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (orientation reference)";
            floor.transform.SetParent(environment.transform, false);
            floor.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
            renderer.enabled = false;

            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        }

        private static void CreateRenderProbe(Transform cameraTransform)
        {
            // Dieser Test umgeht den Checkerboard-Shader vollstaendig. Wird der
            // magentafarbene Wuerfel in XR sichtbar, funktioniert der allgemeine
            // Kamera-/XR-Renderpfad und die Fehlersuche kann sich auf den
            // Stimulus-Shader konzentrieren.
            GameObject probeRoot = new GameObject(
                "XR Render Probe (enable for diagnostics)");
            probeRoot.transform.SetParent(cameraTransform, false);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Camera-locked magenta cube";
            cube.transform.SetParent(probeRoot.transform, false);
            // Vor dem Stimulus platzieren, damit der Probe auch bei weiterhin
            // aktivem Checkerboard nicht von dessen Flaeche verdeckt wird.
            cube.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            cube.transform.localScale = Vector3.one * 0.1f;

            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateRenderProbeMaterial();
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());

            // Der Probe gehoert nicht zum Experiment und wird nur bei Bedarf
            // manuell in der Hierarchy aktiviert.
            probeRoot.SetActive(false);
        }

        private static Material GetOrCreateRenderProbeMaterial()
        {
            Material existingMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(RenderProbeMaterialPath);
            if (existingMaterial != null)
            {
                return existingMaterial;
            }

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Der eingebaute Shader 'Unlit/Color' wurde nicht gefunden.");
            }

            Material material = new Material(shader)
            {
                name = "XR Render Probe",
                color = Color.magenta
            };
            AssetDatabase.CreateAsset(material, RenderProbeMaterialPath);
            return material;
        }

        private static void EnsureSceneIsInBuildSettings()
        {
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
