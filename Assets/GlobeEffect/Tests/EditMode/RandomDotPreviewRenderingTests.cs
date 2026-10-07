using System.IO;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class RandomDotPreviewRenderingTests
    {
        [Test]
        public void DiagnosticOverlayShader_IsImportedAndCompiles()
        {
            // Muss in Unity laufen: Der externe C#-Check kann weder den Asset-
            // Import noch Shader prüfen. Eine kaputte .meta darf nicht unbemerkt bleiben.
            Shader shader = Resources.Load<Shader>("GlobeEffectDiagnosticFlowOverlay");
            Assert.That(shader, Is.Not.Null, "Unity muss den Diagnose-Shader aus Resources laden können.");
            Assert.That(shader.name, Is.EqualTo("GlobeEffect/Diagnostic Flow Overlay"));
            var material = new Material(shader);
            try
            {
                // Import allein reicht nicht: Unity kompiliert Varianten oft erst
                // bei der Verwendung. Beide Darstellungsmodi synchron prüfen.
                material.SetFloat("_Arrows", 0f);
                ShaderUtil.CompilePass(material, 0, true);
                material.SetFloat("_Arrows", 1f);
                ShaderUtil.CompilePass(material, 0, true);
            }
            finally { Object.DestroyImmediate(material); }
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False,
                "Der Diagnose-Shader darf keine Compilerfehler haben.");
        }

        [Test]
        public void DiagnosticArrows_RenderWithoutHeatmapAndCanBeHidden()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Pfeiltest braucht Unity mit einer Grafikkarte.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(512, 512, 24);
            var readback = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Arrow Test Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.fieldOfView = 60f;
                camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.targetTexture = target;
                camera.stereoTargetEye = StereoTargetEyeMask.None;

                // Nur Einstellungen benutzen, nicht die komplette Diagnose mit
                // Kamera, Markern und Awake starten. Keine Änderung an offenen Szenen.
                var settingsObject = new GameObject("Arrow Test Settings");
                settingsObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(settingsObject, scene);
                var settings = settingsObject.AddComponent<RandomDotTrajectoryDiagnostic>();
                settings.showSpeedHeatmap = false;
                settings.showVelocityArrows = true;
                settings.arrowSize = 1.5f;
                settings.arrowThickness = 2f;
                settings.edgeSoftnessDegrees = 0f;
                var overlayObject = new GameObject("Arrow Test Overlay");
                SceneManager.MoveGameObjectToScene(overlayObject, scene);
                var overlay = overlayObject.AddComponent<RandomDotDiagnosticFlowOverlay>();
                overlay.Configure(settings);
                Assert.That(overlayObject.transform.Find("Speed heatmap").GetComponent<MeshRenderer>().enabled,
                    Is.False,
                    "Die Heatmap ist ausgeschaltet; die Pfeile haben eine eigene Zeichenebene.");
                int darkPixels = 0;
                foreach (Color32 pixel in Render(camera, target, readback))
                    if (pixel.r < 128 && pixel.g < 128 && pixel.b < 128) darkPixels++;
                Assert.That(darkPixels, Is.GreaterThan(100), "Die separaten Pfeile müssen sichtbar sein.");

                settings.showVelocityArrows = false;
                overlay.Configure(settings);
                foreach (Color32 pixel in Render(camera, target, readback))
                    Assert.That(pixel.r, Is.GreaterThan(250), "Ohne Overlays bleibt der Hintergrund weiß.");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        [Test]
        public void ReferenceGrid_IsStraightFixedAndAbsentFromExperimentalTrials()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Rastertest braucht eine Grafikkarte.");

            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Reference Grid Test Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.fieldOfView = 90f;
                camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.stereoTargetEye = StereoTargetEyeMask.None;

                var field = new GameObject("Reference Grid Test Field");
                SceneManager.MoveGameObjectToScene(field, scene);
                var stimulus = field.AddComponent<RandomDotFieldStimulus>();
                stimulus.Observer = camera.transform;
                stimulus.SetAngularDiameter(70f);
                var settings = new SerializedObject(stimulus);
                // Unsichtbare Punkte isolieren das Raster. Andernfalls würden
                // bewegte Punkte einzelne Rasterpixel verdecken.
                settings.FindProperty("darkColor").colorValue = Color.clear;
                settings.FindProperty("lightColor").colorValue = Color.clear;
                settings.FindProperty("showFixationTarget").boolValue = false;
                settings.FindProperty("referenceGridColor").colorValue = Color.black;
                settings.FindProperty("referenceGridWidthPixels").floatValue = 2f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                stimulus.ConfigurePointField(42, 40f);
                stimulus.Show();
                Color32[] plain = Render(camera, target, readback);
                settings.Update();
                settings.FindProperty("showReferenceGrid").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                stimulus.SetInstrumentDistortionK(0.5f);
                Color32[] grid = Render(camera, target, readback);
                int changed = 0;
                for (int y = 56; y < 200; y++)
                for (int x = 56; x < 200; x++)
                    if (!plain[y * 256 + x].Equals(grid[y * 256 + x])) changed++;
                Assert.That(changed, Is.GreaterThan(1000), "Das Raster muss wirklich sichtbar sein.");

                stimulus.SetInstrumentDistortionK(1.2f);
                stimulus.SetInstrumentMagnification(20f);
                stimulus.SetContentZoom(1.5f);
                Color32[] otherOptics = Render(camera, target, readback);
                CollectionAssert.AreEqual(grid, otherOptics, "k, m und Zoom dürfen das Raster nicht verbiegen.");

                stimulus.SessionRunning = true;
                stimulus.Show();
                CollectionAssert.AreEqual(plain, Render(camera, target, readback),
                    "In einer Sitzung darf das Test-Raster nicht erscheinen.");

                // Kleine Diagnosebilder bleiben nur im ignorierten Library-Ordner.
                string folder = Path.Combine("Library", "RandomDotPreviewAudit");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "reference-grid-off.png"), readback.EncodeToPNG());
                stimulus.SessionRunning = false;
                stimulus.Show();
                Render(camera, target, readback);
                File.WriteAllBytes(Path.Combine(folder, "reference-grid-on.png"), readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        private static Color32[] Render(Camera camera, RenderTexture target, Texture2D readback)
        {
            camera.Render();
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            return readback.GetPixels32();
        }
    }
}
