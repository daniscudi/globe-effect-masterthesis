using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardNoiseRenderingTests
    {
        // Liest das tatsächlich gerenderte Bild aus. Ein reiner Rechentest auf
        // der CPU würde Fehler der Shaderberechnung auf der Grafikkarte übersehen.
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void NoiseContainsBothColorsAndRespondsToLargeSeeds(float cellSize)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Bildtest braucht eine aktive Grafikausgabe.");

            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Noise Test Camera");
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

                var stimulusObject = new GameObject("Noise Test Stimulus");
                SceneManager.MoveGameObjectToScene(stimulusObject, scene);
                var stimulus = stimulusObject.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = camera.transform;
                stimulus.SetAngularDiameter(70f);
                stimulus.SetApertureEdgeSoftness(0f);
                var settings = new SerializedObject(stimulus);
                settings.FindProperty("noiseSizeDegrees").floatValue = cellSize;
                settings.ApplyModifiedPropertiesWithoutUndo();

                // Zwei benachbarte große Zahlen dürfen nicht zum gleichen Float
                // gerundet werden. Dazu prüfen wir die Grenzen des Seed-Bereichs.
                int[] seeds = { 20260900, 20260901, 20310902, 0, int.MinValue, int.MaxValue };
                Color32[] previous = null;
                foreach (int seed in seeds)
                {
                    stimulus.ShowNoise(seed);
                    Color32[] pixels = ReadImage(camera, target, readback);
                    int white = 0;
                    int black = 0;
                    int changed = 0;
                    // Nur die Mitte auswerten: kein Kreisrand und kein rotes Kreuz.
                    for (int y = 72; y < 184; y++)
                    for (int x = 72; x < 184; x++)
                    {
                        Color32 pixel = pixels[y * 256 + x];
                        if (pixel.r > 240 && pixel.g > 240 && pixel.b > 240) white++;
                        if (pixel.r < 15 && pixel.g < 15 && pixel.b < 15) black++;
                        if (previous != null && !pixel.Equals(previous[y * 256 + x])) changed++;
                    }
                    Assert.That(white, Is.GreaterThan(4000), "Weiße Noise-Felder fehlen, Seed " + seed);
                    Assert.That(black, Is.GreaterThan(4000), "Schwarze Noise-Felder fehlen, Seed " + seed);
                    if (previous != null)
                        Assert.That(changed, Is.GreaterThan(3000), "Neuer Seed muss das Muster ändern.");
                    stimulus.ShowNoise(seed);
                    CollectionAssert.AreEqual(pixels, ReadImage(camera, target, readback),
                        "Derselbe Seed muss dasselbe Bild ergeben.");
                    previous = pixels;
                }
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

        private static Color32[] ReadImage(Camera camera, RenderTexture target, Texture2D readback)
        {
            camera.Render();
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            readback.Apply();
            return readback.GetPixels32();
        }
    }
}
