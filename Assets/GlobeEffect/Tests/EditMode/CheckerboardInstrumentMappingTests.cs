using System;
using System.IO;
using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardInstrumentMappingTests
    {
        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(1.4)]
        [TestCase(2.0)]
        public void InverseSampling_RecoversSourcePointAfterTheRandomDotInstrumentMapping(double k)
        {
            const double m = 10.0;
            double half = Math.PI / 6.0;
            foreach (double sourceRadius in new[] { 0.0, 0.15, 0.4, 0.7 })
            {
                double objectAngle = Math.Atan(sourceRadius * Math.Tan(half) / m);
                double apparent = MerlitzBinocularReferenceMath.ApparentAngleFromObject(objectAngle, m, k);
                double displayRadius = Math.Tan(apparent) / Math.Tan(half);
                double reconstructed = CheckerboardInstrumentMapping.SourceRadius(displayRadius, 60.0, m, k);
                Assert.That(reconstructed, Is.EqualTo(sourceRadius).Within(1e-12));
            }
        }

        [TestCase(1.0)]
        [TestCase(10.0)]
        [TestCase(20.0)]
        public void K1_IsStraightForEveryMagnification(double m)
        {
            foreach (double r in new[] { 0.0, 0.3, 0.8, 1.0 })
                Assert.That(CheckerboardInstrumentMapping.SourceRadius(r, 60.0, m, 1.0),
                    Is.EqualTo(r).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.4)]
        public void M1_IsIdentityNotTheLegacyVisualSpaceMapping(double k)
        {
            Assert.That(CheckerboardInstrumentMapping.SourceRadius(0.6, 60.0, 1.0, k),
                Is.EqualTo(0.6).Within(1e-12));
        }

        [Test]
        public void K0_UsesFiniteMagnificationLimitAndDoesNotRenormalizeSourceAtTheAperture()
        {
            double expected = 10.0 * Math.Tan(Math.PI / 60.0) / Math.Tan(Math.PI / 6.0);
            Assert.That(CheckerboardInstrumentMapping.SourceRadius(1.0, 60.0, 10.0, 0.0),
                Is.EqualTo(expected).Within(1e-12));
            Assert.That(expected, Is.LessThan(1.0));
            Assert.That(CheckerboardInstrumentMapping.MappingVersion,
                Is.Not.EqualTo(VisualSpaceRadialMapping.MappingVersion));
        }

        [TestCase(90.0, 2.0, 0.0, true)]
        [TestCase(88.0, 2.0, 5.0, true)]
        [TestCase(70.0, 2.0, 0.0, false)]
        public void UnsafeApertureIncludingFadeAndCorners_IsRejected(double fov, double k,
            double softness, bool circle)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CheckerboardInstrumentMapping.ValidateParameters(fov, 10, k, softness, circle));
        }

        [TestCase(double.NaN, 0.5)]
        [TestCase(10.0, double.NaN)]
        [TestCase(0.0, 0.5)]
        [TestCase(10.0, -0.5)]
        [TestCase(10.0, 2.1)]
        public void InvalidInstrumentParameters_AreRejected(double m, double k)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CheckerboardInstrumentMapping.SourceRadius(0.5, 60.0, m, k));
        }

        [Test]
        public void TrialPlanAndRepeatedAttempts_PreserveFixedMagnification()
        {
            var plan = CheckerboardTrialPlanner.CreateRandomizedPlan(new[] { 60f },
                new[] { CheckerboardEyePresentation.BothEyes }, new[] { 0f, 1f, 2f }, 2, 17,
                instrumentMagnificationM: 20f);
            foreach (CheckerboardTrial trial in plan)
            {
                Assert.That(trial.InstrumentMagnificationM, Is.EqualTo(20f));
                Assert.That(trial.CreateRepeatedAttempt().InstrumentMagnificationM, Is.EqualTo(20f));
            }
        }

        [TestCase(0f, 10f)]
        [TestCase(0.5f, 10f)]
        [TestCase(1f, 20f)]
        [TestCase(1.4f, 10f)]
        [TestCase(2f, 10f)]
        public void RenderedCheckerboard_MatchesInstrumentReferenceNotLegacyL(float k, float m)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Bildtest braucht eine aktive Grafikausgabe.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Instrument Test Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.fieldOfView = 90f;
                camera.aspect = 1f;
                camera.targetTexture = target;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                var surface = new GameObject("Instrument Test Checkerboard");
                SceneManager.MoveGameObjectToScene(surface, scene);
                var stimulus = surface.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = camera.transform;
                stimulus.SetAngularDiameter(60f);
                stimulus.SetApertureEdgeSoftness(0f);
                stimulus.SetInstrumentMagnificationM(m);
                stimulus.SetInstrumentDistortionK(k);
                var settings = new SerializedObject(stimulus);
                settings.FindProperty("showFixationTarget").boolValue = false;
                settings.FindProperty("gridSpacingDegrees").floatValue = 8f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                stimulus.Show();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                int checkedPixels = 0, wrongPixels = 0;
                double halfTangent = Math.Tan(Math.PI / 6.0);
                double spacing = CheckerboardInstrumentMapping.GridLineSpacingUv(60, 8);
                for (int y = 57; y < 200; y += 7)
                    for (int x = 57; x < 200; x += 7)
                    {
                        double u = (2.0 * (x + 0.5) / 256.0 - 1.0) / halfTangent;
                        double v = (2.0 * (y + 0.5) / 256.0 - 1.0) / halfTangent;
                        double r = Math.Sqrt(u * u + v * v);
                        if (r < 0.05 || r > 0.9) continue;
                        double s = CheckerboardInstrumentMapping.SourceRadius(r, 60, m, k);
                        double signal = Math.Sin(Math.PI * u * s / r / spacing)
                            * Math.Sin(Math.PI * v * s / r / spacing);
                        if (Math.Abs(signal) < 0.5) continue; // Nicht die geglätteten Kanten vergleichen.
                        bool white = image.GetPixel(x, y).r > 0.5f;
                        checkedPixels++;
                        if (white != (signal > 0)) wrongPixels++;
                    }
                Assert.That(checkedPixels, Is.GreaterThan(30));
                Assert.That(wrongPixels, Is.LessThanOrEqualTo(2), "Shader und Instrumentenreferenz weichen ab.");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Unveränderte Kopie des Checkerboard-Shaders, wie er mit MappingVersion v1
        // ausgeliefert wurde (Stand 18.10.2026, vor der Polaritätsoption).
        private const string ShaderV1ReferencePath = "Assets/GlobeEffect/Tests/EditMode/CheckerboardShaderV1Reference.txt";
        private const string MerlitzIncludePath = "Assets/GlobeEffect/Runtime/Resources/GlobeEffectMerlitzInstrument.cginc";

        [Test]
        public void ShaderV2_WithInvertOff_RendersPixelForPixelTheSameAsV1()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Bildtest braucht eine aktive Grafikausgabe.");

            // Für den Test wird nur der Shadername geändert (sonst gäbe es ihn zweimal)
            // und die gemeinsame Include-Datei direkt eingesetzt. Der Code bleibt v1.
            string v1Source = File.ReadAllText(ShaderV1ReferencePath);
            const string v1Name = "Shader \"GlobeEffect/Helmholtz Checkerboard\"";
            const string include = "#include \"GlobeEffectMerlitzInstrument.cginc\"";
            Assert.That(v1Source, Does.Contain(v1Name).And.Contain(include));
            Assert.That(v1Source, Does.Not.Contain("_CheckerboardInverted"), "Die Referenz muss v1 sein.");
            v1Source = v1Source
                .Replace(v1Name, "Shader \"Hidden/GlobeEffect/Helmholtz Checkerboard V1 Reference\"")
                .Replace(include, File.ReadAllText(MerlitzIncludePath));

            Shader v1Shader = ShaderUtil.CreateShaderAsset(v1Source, true);
            var v1Material = new Material(v1Shader);
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Assert.That(ShaderUtil.ShaderHasError(v1Shader), Is.False, "v1-Referenz kompiliert nicht.");
                var cameraObject = new GameObject("V1 Comparison Camera");
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
                var surface = new GameObject("V1 Comparison Checkerboard");
                SceneManager.MoveGameObjectToScene(surface, scene);
                var stimulus = surface.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = camera.transform;
                // Werte wie in der CheckerboardDemo-Szene: FOV 60, weicher Rand 5, Gitter 8, m 10.
                stimulus.SetAngularDiameter(60f);
                stimulus.SetApertureEdgeSoftness(5f);
                stimulus.SetInstrumentMagnificationM(10f);
                var settings = new SerializedObject(stimulus);
                settings.FindProperty("gridSpacingDegrees").floatValue = 8f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var renderer = stimulus.GetComponent<MeshRenderer>();
                Material v2Material = renderer.sharedMaterial;
                Assert.That(v2Material.shader.name, Is.EqualTo("GlobeEffect/Helmholtz Checkerboard"));
                ShaderUtil.CompilePass(v1Material, 0, true);
                ShaderUtil.CompilePass(v2Material, 0, true);
                Assert.That(stimulus.CheckerboardInverted, Is.False, "Standard: nicht invertiert.");

                Color32[] RenderWith(Material material)
                {
                    renderer.sharedMaterial = material;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    image.Apply();
                    return image.GetPixels32();
                }

                foreach (float k in new[] { 0f, 0.2f, 0.5f, 1f, 1.2f, 1.4f, 2f })
                {
                    stimulus.SetInstrumentDistortionK(k);
                    stimulus.Show();
                    CollectionAssert.AreEqual(RenderWith(v1Material), RenderWith(v2Material),
                        "Schachbrett v1/v2 weicht ab, k = " + k);
                }

                stimulus.ShowNoise(20260901);
                Color32[] v1Noise = RenderWith(v1Material);
                CollectionAssert.AreEqual(v1Noise, RenderWith(v2Material), "Noise-Maske v1/v2 weicht ab.");
                stimulus.ShowFixationOnly();
                CollectionAssert.AreEqual(RenderWith(v1Material), RenderWith(v2Material),
                    "Graue Fläche v1/v2 weicht ab.");

                // Gegenprobe: Invertiert tauscht Schwarz und Weiß, die Maske bleibt gleich.
                stimulus.SetInstrumentDistortionK(0.5f);
                stimulus.Show();
                Color32[] normal = RenderWith(v2Material);
                stimulus.SetCheckerboardInverted(true);
                stimulus.Show();
                Color32[] inverted = RenderWith(v2Material);
                int swapped = 0, unchangedPure = 0;
                for (int y = 72; y < 184; y++)
                    for (int x = 72; x < 184; x++)
                    {
                        Color32 a = normal[y * 256 + x], b = inverted[y * 256 + x];
                        bool aWhite = a.r > 240 && a.g > 240 && a.b > 240, aBlack = a.r < 15 && a.g < 15 && a.b < 15;
                        bool bWhite = b.r > 240 && b.g > 240 && b.b > 240, bBlack = b.r < 15 && b.g < 15 && b.b < 15;
                        if ((aWhite && bBlack) || (aBlack && bWhite)) swapped++;
                        if ((aWhite && bWhite) || (aBlack && bBlack)) unchangedPure++;
                    }
                Assert.That(swapped, Is.GreaterThan(6000), "Invertiert muss Schwarz und Weiß tauschen.");
                Assert.That(unchangedPure, Is.Zero, "Kein reines Feld darf gleich bleiben.");
                stimulus.ShowNoise(20260901);
                CollectionAssert.AreEqual(v1Noise, RenderWith(v2Material),
                    "Die Noise-Maske hängt nicht von der Polarität ab.");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(v1Material);
                UnityEngine.Object.DestroyImmediate(v1Shader);
            }
        }
    }
}
