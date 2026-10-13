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
        // Bei 90 Grad Blickwinkel ist m11 = 1. Dann ist ein Pixel in der Bildmitte
        // 2 / Höhe im Bogenmaß groß, bei 1000 Pixeln also etwa 0,1146 Grad.
        [Test]
        public void DegreesPerPixel_MatchesHandCalculation()
        {
            Matrix4x4 projection = Matrix4x4.Perspective(90f, 1f, 0.1f, 100f);
            float degreesPerPixel = VrCheckerboardStimulus.DegreesPerPixel(projection, 1000);
            Assert.That(degreesPerPixel, Is.EqualTo(Mathf.Rad2Deg * 0.002f).Within(1e-4f));
        }

        // Das Testbild ist nur 256 Pixel groß, ein Pixel sind hier etwa 0,45 Grad.
        // Die Punkte müssen deshalb mehrere Pixel groß sein. Kleinere Punkte
        // werden durch die Kantenglättung richtigerweise grau.
        [TestCase(3f)]
        [TestCase(4f)]
        [TestCase(6f)]
        public void NoiseContainsBothColorsAndRespondsToLargeSeeds(float dotSize)
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
                // Hier wird bewusst in Grad getestet, damit die drei Größen fest sind.
                settings.FindProperty("noiseType").enumValueIndex = (int)NoiseMaskType.SingleScaleDegrees;
                settings.FindProperty("noiseDotSizeDegrees").floatValue = dotSize;
                settings.ApplyModifiedPropertiesWithoutUndo();

                // Zwei benachbarte große Zahlen dürfen nicht zum gleichen Float
                // gerundet werden. Dazu prüfen wir die Grenzen des Seed-Bereichs.
                // Die beiden letzten Einträge sind zwei aufeinanderfolgende
                // Rauschbilder derselben Maske: Auch sie müssen sich unterscheiden,
                // sonst würde die Maske im Versuch nicht flimmern.
                int[] seeds =
                {
                    20260900, 20260901, 20310902, 0, int.MinValue, int.MaxValue,
                    VrCheckerboardStimulus.NoiseFrameSeed(20260901, 1),
                    VrCheckerboardStimulus.NoiseFrameSeed(20260901, 2)
                };
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
                    Assert.That(white, Is.GreaterThan(3000), "Weiße Noise-Felder fehlen, Seed " + seed);
                    Assert.That(black, Is.GreaterThan(3000), "Schwarze Noise-Felder fehlen, Seed " + seed);
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

        [Test]
        public void NoiseFrameIndex_AdvancesWithTheRefreshRate()
        {
            // 30 Bilder pro Sekunde: Ein Bild steht 33 ms.
            Assert.That(VrCheckerboardStimulus.NoiseFrameIndex(0d, 30f), Is.EqualTo(0));
            Assert.That(VrCheckerboardStimulus.NoiseFrameIndex(0.03d, 30f), Is.EqualTo(0));
            Assert.That(VrCheckerboardStimulus.NoiseFrameIndex(0.04d, 30f), Is.EqualTo(1));
            Assert.That(VrCheckerboardStimulus.NoiseFrameIndex(1d, 30f), Is.EqualTo(30));

            // Bei 0 Hz bleibt die Maske stehen.
            Assert.That(VrCheckerboardStimulus.NoiseFrameIndex(100d, 0f), Is.EqualTo(0));
        }

        [Test]
        public void NoiseFrameSeed_StartsWithTheMaskSeedAndChangesEveryFrame()
        {
            const int maskSeed = 20260901;
            Assert.That(VrCheckerboardStimulus.NoiseFrameSeed(maskSeed, 0), Is.EqualTo(maskSeed));

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int frame = 0; frame < 1000; frame++)
            {
                seen.Add(VrCheckerboardStimulus.NoiseFrameSeed(maskSeed, frame));
            }

            Assert.That(seen.Count, Is.EqualTo(1000), "Jedes Rauschbild braucht einen eigenen Seed.");
        }

        [Test]
        public void BroadbandNoise_HasCoarseStructureThatFineNoiseLacks()
        {
            // Wie oft haben zwei Pixel im Abstand von 2 Pixeln (knapp 1°) dieselbe Farbe?
            // Feines Rauschen (1,5 Pixel): etwa halb so oft wie zufällig, also 0,5.
            // Breitband (0,5° bis 8°): deutlich öfter, weil grobe Zellen mitwirken.
            // Genau diese groben Anteile braucht es, um das grobe Schachbrett zu maskieren.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Bildtest braucht eine aktive Grafikausgabe.");

            float fine = SameColorFraction(NoiseMaskType.FinePixels);
            float broadband = SameColorFraction(NoiseMaskType.Broadband);
            Assert.That(fine, Is.LessThan(0.56f), "Feines Rauschen hat kaum grobe Struktur.");
            Assert.That(broadband, Is.GreaterThan(0.6f), "Breitband muss grobe Struktur enthalten.");
        }

        private static float SameColorFraction(NoiseMaskType type)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Noise Structure Camera");
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

                var stimulusObject = new GameObject("Noise Structure Stimulus");
                SceneManager.MoveGameObjectToScene(stimulusObject, scene);
                var stimulus = stimulusObject.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = camera.transform;
                stimulus.SetAngularDiameter(80f);
                stimulus.SetApertureEdgeSoftness(0f);
                var settings = new SerializedObject(stimulus);
                settings.FindProperty("noiseType").enumValueIndex = (int)type;
                settings.FindProperty("noiseDotSizePixels").floatValue = 1.5f;
                settings.FindProperty("noiseBroadbandFinestDegrees").floatValue = 0.5f;
                settings.FindProperty("noiseBroadbandCoarsestDegrees").floatValue = 8f;
                settings.FindProperty("showFixationTarget").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();

                int same = 0;
                int pairs = 0;
                int light = 0;
                int pixels = 0;
                // Mehrere Seeds, damit die Schätzung bei groben Zellen stabil ist.
                foreach (int seed in new[] { 11, 22, 33, 44, 55, 66 })
                {
                    stimulus.ShowNoise(seed);
                    Color32[] image = ReadImage(camera, target, readback);
                    for (int y = 48; y < 208; y++)
                    for (int x = 48; x < 206; x++)
                    {
                        bool here = image[y * 256 + x].r > 127;
                        bool there = image[y * 256 + x + 2].r > 127;
                        if (here == there) same++;
                        if (here) light++;
                        pairs++;
                        pixels++;
                    }
                }
                float lightFraction = light / (float)pixels;
                Assert.That(lightFraction, Is.InRange(0.3f, 0.7f), $"{type}: Hell und dunkel etwa gleich häufig.");
                return same / (float)pairs;
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
        public void SoftEdge_IsCenteredOnTheFieldOfView()
        {
            // FOV 70, Softness 10: voll sichtbar bis 30°, halb bei 35°, weg ab 40°.
            // Kamera 90° auf 256 Pixel: Pixel x liegt bei atan((x + 0,5 - 128) / 128).
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Randtest braucht eine aktive Grafikausgabe.");

            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Soft Edge Camera");
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

                var stimulusObject = new GameObject("Soft Edge Stimulus");
                SceneManager.MoveGameObjectToScene(stimulusObject, scene);
                var stimulus = stimulusObject.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = camera.transform;
                stimulus.SetAngularDiameter(70f);
                stimulus.SetApertureEdgeSoftness(10f);
                var settings = new SerializedObject(stimulus);
                // Weißes Muster auf schwarzem Hintergrund: nur der Rand macht Grau.
                settings.FindProperty("darkColor").colorValue = Color.white;
                settings.FindProperty("lightColor").colorValue = Color.white;
                settings.FindProperty("backgroundColor").colorValue = Color.black;
                settings.FindProperty("showFixationTarget").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                stimulus.ShowNoise(1);

                Color32[] pixels = ReadImage(camera, target, readback);
                Assert.That(pixels[128 * 256 + 198].r, Is.GreaterThan(245), "28,9° liegt vor dem Rand.");
                // 35,0°: Deckkraft 0,5, gemischt in linearer Helligkeit -> gamma etwa 0,73.
                Assert.That(pixels[128 * 256 + 217].r, Is.InRange(165, 205), "Bei FOV/2 halb sichtbar.");
                Assert.That(pixels[128 * 256 + 240].r, Is.LessThan(10), "41,3° liegt hinter dem Rand.");
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
