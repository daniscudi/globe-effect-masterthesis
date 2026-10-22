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
        [TestCase(-1.0)]
        public void EdgeNormalized_IsExactlyTheHtmlVariant3_NotOutputImageScaling(double k)
        {
            double edge = CheckerboardInstrumentMapping.SourceRadius(1, 60, 10, k, k < 0);
            foreach (double r in new[] { 0.0, 0.2, 0.5, 1.0, 1.05 })
            {
                double fixedValue = CheckerboardInstrumentMapping.SourceRadius(r, 60, 10, k, k < 0);
                double normalized = CheckerboardInstrumentMapping.SourceRadius(r, 60, 10, k, k < 0,
                    CheckerboardGridMapping.EdgeNormalized);
                Assert.That(normalized, Is.EqualTo(fixedValue / edge).Within(1e-12));
            }
            Assert.That(CheckerboardInstrumentMapping.SourceRadius(1, 60, 10, k, k < 0,
                CheckerboardGridMapping.EdgeNormalized), Is.EqualTo(1).Within(1e-12));
        }

        [Test]
        public void MixedGridPlan_IsBalancedReproducibleAndKeepsTheVariantOnRepeats()
        {
            var modes = new[] { CheckerboardGridMapping.FixedSourceGrid, CheckerboardGridMapping.EdgeNormalized };
            float[] levels = { 0.2f, 0.4f, 0.6f, 0.8f, 1f, 1.2f, 1.4f };
            var first = CheckerboardTrialPlanner.CreateRandomizedPlan(new[] { 60f },
                new[] { CheckerboardEyePresentation.BothEyes }, levels, 10, 1234, gridMappings: modes);
            var second = CheckerboardTrialPlanner.CreateRandomizedPlan(new[] { 60f },
                new[] { CheckerboardEyePresentation.BothEyes }, levels, 10, 1234, gridMappings: modes);
            Assert.That(first.Count, Is.EqualTo(140));
            var conditions = new System.Collections.Generic.Dictionary<int, int>();
            int switches = 0;
            for (int i = 0; i < first.Count; i++)
            {
                var trial = first[i];
                Assert.That(trial.GridMapping, Is.EqualTo(second[i].GridMapping));
                Assert.That(trial.InstrumentDistortionK, Is.EqualTo(second[i].InstrumentDistortionK));
                Assert.That(trial.CreateRepeatedAttempt().GridMapping, Is.EqualTo(trial.GridMapping));
                conditions.TryGetValue(trial.ConditionIndex, out int count);
                conditions[trial.ConditionIndex] = count + 1;
                if (i > 0 && trial.GridMapping != first[i - 1].GridMapping) switches++;
            }
            Assert.That(conditions.Count, Is.EqualTo(14));
            foreach (int count in conditions.Values) Assert.That(count, Is.EqualTo(10));
            Assert.That(switches, Is.GreaterThan(20), "Varianten müssen gemischt sein, nicht zwei Blöcke.");
        }

        [Test]
        public void InvalidOrDuplicateGridVariants_AreRejected()
        {
            foreach (var modes in new[]
            {
                Array.Empty<CheckerboardGridMapping>(),
                new[] { (CheckerboardGridMapping)99 },
                new[] { CheckerboardGridMapping.EdgeNormalized, CheckerboardGridMapping.EdgeNormalized }
            })
                Assert.That(() => CheckerboardTrialPlanner.CreateRandomizedPlan(
                    new[] { 60f }, new[] { CheckerboardEyePresentation.BothEyes }, new[] { 0.5f }, 1, 7,
                    gridMappings: modes), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void EyeTrackingSnapshotMarker_UsesTheActualTrialVariant()
        {
            var method = typeof(EyeTracking.EyeTrackingToolbox).GetMethod("BuildStimulusMarker",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var snapshot = new CheckerboardStimulusSnapshot
            {
                instrumentDistortionK = 0.5f, instrumentMagnificationM = 10f,
                gridMapping = CheckerboardGridMapping.EdgeNormalized, gridSourceScale = 1.1f
            };
            string marker = (string)method.Invoke(null, new object[] { "StimulusShown", snapshot });
            Assert.That(marker, Does.Contain("mapping=" + CheckerboardInstrumentMapping.EdgeNormalizedMappingVersion));
            Assert.That(marker, Does.Contain("grid_mapping=EdgeNormalized"));
            Assert.That(marker, Does.Contain("grid_source_scale=1.100000"));
        }

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

        [TestCase(-2f, 10f)]
        [TestCase(-1f, 10f)]
        [TestCase(-0.5f, 10f)]
        [TestCase(0f, 10f)]
        [TestCase(0.5f, 10f)]
        [TestCase(1f, 20f)]
        [TestCase(1.4f, 10f)]
        [TestCase(2f, 10f)]
        [TestCase(0f, 10f, CheckerboardGridMapping.EdgeNormalized)]
        [TestCase(0.5f, 10f, CheckerboardGridMapping.EdgeNormalized)]
        [TestCase(1f, 10f, CheckerboardGridMapping.EdgeNormalized)]
        [TestCase(1.4f, 10f, CheckerboardGridMapping.EdgeNormalized)]
        public void RenderedCheckerboard_MatchesInstrumentReferenceNotLegacyL(float k, float m,
            CheckerboardGridMapping gridMapping = CheckerboardGridMapping.FixedSourceGrid)
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
                stimulus.SetPincushionExtensionEnabled(k < 0f);
                stimulus.SetInstrumentDistortionK(k);
                stimulus.SetGridMapping(gridMapping);
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
                        double s = CheckerboardInstrumentMapping.SourceRadius(r, 60, m, k, k < 0f, gridMapping);
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

        // ---- Standardvariante: Instrument, Standard-m = 10, festes Quellgitter (22.10.2026) ----

        [Test]
        public void DefaultMagnification_IsTen()
        {
            Assert.That(CheckerboardInstrumentMapping.DefaultMagnificationM, Is.EqualTo(10f));
        }

        [TestCase(0.0)]
        [TestCase(1e-9)]
        [TestCase(1e-7)]
        public void KNearZero_UsesTheLimitAOverMWithoutDivisionByZero(double k)
        {
            // Grenzwert A = a/m. Auch winzige k > 0 dürfen nicht durch 0 teilen
            // und müssen stetig an k = 0 anschließen.
            foreach (double a in new[] { 0.0, 0.1, 0.5 })
            {
                double objectAngle = MerlitzBinocularReferenceMath.ObjectAngleFromApparent(a, 10, k);
                Assert.That(double.IsNaN(objectAngle) || double.IsInfinity(objectAngle), Is.False);
                Assert.That(objectAngle, Is.EqualTo(a / 10.0).Within(1e-9));
            }

            foreach (double r in new[] { 0.0, 0.3, 1.0 })
                Assert.That(CheckerboardInstrumentMapping.SourceRadius(r, 60, 10, k),
                    Is.EqualTo(CheckerboardInstrumentMapping.SourceRadius(r, 60, 10, 0.0)).Within(1e-9));
        }

        [TestCase(0.0)]
        [TestCase(0.2)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(1.2)]
        [TestCase(1.4)]
        [TestCase(2.0)]
        public void CentralLocalScale_FollowsFromTheEquationForEveryKAndM(double k)
        {
            // Keine Korrektur im Code: Die Instrumentengleichung selbst ergibt im Zentrum
            // die Vergrößerung m, also in Quellgitter-Einheiten m·tan(A)/R -> 1.
            // Perspektivische Form: (u_q, v_q) = [m·tan(A)/R]·(u, v), a = atan(R).
            foreach (double m in new[] { 1.0, 5.0, 10.0, 20.0 })
            {
                foreach (double tinyR in new[] { 1e-4, 1e-6 })
                {
                    double objectAngle = MerlitzBinocularReferenceMath.ObjectAngleFromApparent(
                        Math.Atan(tinyR), m, k);
                    Assert.That(m * Math.Tan(objectAngle) / tinyR, Is.EqualTo(1.0).Within(1e-6));
                }

                // Dieselbe Aussage in den Blendenrand-Einheiten des Shaders: s(r)/r -> 1.
                const double r = 1e-5;
                Assert.That(CheckerboardInstrumentMapping.SourceRadius(r, 60, m, k) / r,
                    Is.EqualTo(1.0).Within(1e-6));
            }
        }

        [Test]
        public void EdgeFieldSpan_VariesWithKAndIsNotNormalizedAway()
        {
            // Szene: FOV 60°, Gitter 8°, m = 10. Felder über den Durchmesser bei FOV/2.
            double spacing = CheckerboardInstrumentMapping.GridLineSpacingUv(60, 8);
            double Checks(double k) => 2.0 * CheckerboardInstrumentMapping.SourceRadius(1.0, 60, 10, k) / spacing;
            Assert.That(Checks(0.0), Is.EqualTo(7.46).Within(0.01));
            Assert.That(Checks(0.2), Is.EqualTo(7.49).Within(0.01));
            Assert.That(Checks(1.0), Is.EqualTo(8.22).Within(0.01));
            Assert.That(Checks(1.2), Is.EqualTo(8.61).Within(0.01));
            Assert.That(Checks(1.4), Is.EqualTo(9.14).Within(0.01));
            Assert.That(Checks(1.2) / Checks(0.2), Is.GreaterThan(1.1), "Die Feldspanne am Rand darf mit k variieren.");

            // Keine nachträgliche Randnormierung in der Standardvariante.
            foreach (double k in new[] { 0.0, 0.5, 1.0, 1.4, 2.0 })
                Assert.That(CheckerboardInstrumentMapping.GridSourceScale(60, 10, k, false,
                    CheckerboardGridMapping.FixedSourceGrid), Is.EqualTo(1.0));
        }

        [Test]
        public void InvalidValues_AreRejectedOrReportedButNeverSilentlyClamped()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var surface = new GameObject("Invalid Values Checkerboard");
                SceneManager.MoveGameObjectToScene(surface, scene);
                var stimulus = surface.AddComponent<VrCheckerboardStimulus>();
                stimulus.SetAngularDiameter(60f);
                stimulus.SetApertureEdgeSoftness(5f);
                Assert.That(stimulus.InstrumentMappingProblem, Is.Null);

                Assert.Throws<ArgumentOutOfRangeException>(() => stimulus.SetInstrumentDistortionK(2.5f));
                Assert.Throws<ArgumentOutOfRangeException>(() => stimulus.SetInstrumentDistortionK(-0.5f));
                Assert.Throws<ArgumentOutOfRangeException>(() => stimulus.SetInstrumentDistortionK(float.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(() => stimulus.SetInstrumentMagnificationM(25f));
                Assert.That(stimulus.InstrumentDistortionK, Is.EqualTo(0.5f), "Abgewiesen, nicht geklemmt.");

                // Negativer Wert der Erweiterung bleibt beim Ausschalten stehen und wird gemeldet,
                // statt still zu k = 0 zu werden. Das Muster wird dann nicht gezeichnet.
                stimulus.SetPincushionExtensionEnabled(true);
                stimulus.SetInstrumentDistortionK(-1f);
                Assert.That(stimulus.InstrumentMappingProblem, Is.Null);
                stimulus.SetPincushionExtensionEnabled(false);
                Assert.That(stimulus.InstrumentDistortionK, Is.EqualTo(-1f));
                Assert.That(stimulus.InstrumentMappingProblem, Is.Not.Null);
                stimulus.Show();
                var block = new MaterialPropertyBlock();
                stimulus.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_CheckerboardEnabled"), Is.EqualTo(0f));

                // FOV + Rand jenseits der Tangens-Grenze: gemeldet, kein Muster.
                stimulus.SetPincushionExtensionEnabled(true);
                stimulus.SetInstrumentDistortionK(2f);
                stimulus.SetAngularDiameter(120f);
                Assert.That(stimulus.InstrumentMappingProblem, Is.Not.Null);
                stimulus.Show();
                stimulus.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_CheckerboardEnabled"), Is.EqualTo(0f));
                Assert.That(stimulus.AngularDiameterDegrees, Is.EqualTo(120f));
                Assert.That(stimulus.InstrumentDistortionK, Is.EqualTo(2f));

                stimulus.SetAngularDiameter(60f);
                Assert.That(stimulus.InstrumentMappingProblem, Is.Null);
                stimulus.Show();
                stimulus.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_CheckerboardEnabled"), Is.EqualTo(1f));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void ApertureAndFov_StayIdenticalForEveryK()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Bildtest braucht eine aktive Grafikausgabe.");

            // Schwarz und Weiß beide weiß: Dann zeigt das Bild nur noch die Blende
            // (weiß innen, weicher Rand, schwarzer Hintergrund). Sie wird in
            // Bildkoordinaten ausgewertet und darf sich mit k nicht ändern.
            RenderCheckerboard(settings =>
            {
                settings.FindProperty("darkColor").colorValue = Color.white;
                settings.FindProperty("lightColor").colorValue = Color.white;
                settings.FindProperty("backgroundColor").colorValue = Color.black;
                settings.FindProperty("showFixationTarget").boolValue = false;
            }, stimulus =>
            {
                stimulus.SetAngularDiameter(60f);
                stimulus.SetApertureEdgeSoftness(5f);
            }, (stimulus, render) =>
            {
                stimulus.SetInstrumentDistortionK(1f);
                Color32[] reference = render();
                int inside = 0;
                foreach (Color32 pixel in reference) if (pixel.r > 250) inside++;
                Assert.That(inside, Is.GreaterThan(5000), "Die Blende muss sichtbar sein.");
                foreach (float k in new[] { 0f, 0.2f, 0.5f, 1.2f, 1.4f, 2f })
                {
                    stimulus.SetInstrumentDistortionK(k);
                    CollectionAssert.AreEqual(reference, render(), "Blende/FOV ändern sich mit k = " + k);
                }
            });
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(1.4f)]
        [TestCase(2f)]
        public void ShaderEdgePositions_MatchTheCpuReferenceWithinOnePixel(float k)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Der Bildtest braucht eine aktive Grafikausgabe.");

            // Numerischer Vergleich: Wo wechselt in einer Bildzeile Schwarz und Weiß?
            // Die C#-Referenz sagt die Wechsel aus dem Vorzeichen des Musters voraus;
            // im gerenderten Bild muss jeder Wechsel höchstens 1 Pixel daneben liegen.
            double halfTangent = Math.Tan(Math.PI / 6.0);
            double spacing = CheckerboardInstrumentMapping.GridLineSpacingUv(60, 8);
            RenderCheckerboard(settings =>
            {
                settings.FindProperty("showFixationTarget").boolValue = false;
                settings.FindProperty("gridSpacingDegrees").floatValue = 8f;
            }, stimulus =>
            {
                stimulus.SetAngularDiameter(60f);
                stimulus.SetApertureEdgeSoftness(0f);
                stimulus.SetInstrumentMagnificationM(CheckerboardInstrumentMapping.DefaultMagnificationM);
                stimulus.SetInstrumentDistortionK(k);
            }, (stimulus, render) =>
            {
                Color32[] image = render();
                int compared = 0;
                foreach (int y in new[] { 70, 95, 160, 185 })
                {
                    var expected = new System.Collections.Generic.List<int>();
                    var actual = new System.Collections.Generic.List<int>();
                    int previousSign = 0;
                    bool previousWhite = false;
                    bool first = true;
                    for (int x = 40; x < 216; x++)
                    {
                        double u = (2.0 * (x + 0.5) / 256.0 - 1.0) / halfTangent;
                        double v = (2.0 * (y + 0.5) / 256.0 - 1.0) / halfTangent;
                        double r = Math.Sqrt(u * u + v * v);
                        // Nur innerhalb der Blende vergleichen (pro Zeile ein zusammenhängendes Stück).
                        if (r >= 0.95) continue;
                        double s = CheckerboardInstrumentMapping.SourceRadius(r, 60,
                            CheckerboardInstrumentMapping.DefaultMagnificationM, k);
                        double signal = Math.Sin(Math.PI * u * s / r / spacing)
                            * Math.Sin(Math.PI * v * s / r / spacing);
                        int sign = Math.Sign(signal);
                        bool white = image[y * 256 + x].r >= 128;
                        if (!first)
                        {
                            if (sign != previousSign) expected.Add(x);
                            if (white != previousWhite) actual.Add(x);
                        }

                        first = false;
                        previousSign = sign;
                        previousWhite = white;
                    }

                    Assert.That(actual.Count, Is.EqualTo(expected.Count), $"Anzahl der Wechsel, Zeile {y}");
                    for (int i = 0; i < expected.Count; i++)
                        Assert.That(Math.Abs(actual[i] - expected[i]), Is.LessThanOrEqualTo(1),
                            $"Wechsel {i} in Zeile {y}: Shader {actual[i]}, Referenz {expected[i]}");
                    compared += expected.Count;
                }

                Assert.That(compared, Is.GreaterThan(12), "Zu wenige Wechsel verglichen.");
            });
        }

        // Rendert den Stimulus in eine 256er-Textur (Kamera 90°, quadratisch).
        // Erst die serialisierten Felder anwenden, danach die Setter: Ein früher
        // angelegtes SerializedObject würde FOV und k sonst wieder zurückschreiben.
        private static void RenderCheckerboard(Action<SerializedObject> serializedSetup,
            Action<VrCheckerboardStimulus> setup, Action<VrCheckerboardStimulus, Func<Color32[]>> check)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(256, 256, 24);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Mapping Test Camera");
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
                var surface = new GameObject("Mapping Test Checkerboard");
                SceneManager.MoveGameObjectToScene(surface, scene);
                var stimulus = surface.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = camera.transform;
                var settings = new SerializedObject(stimulus);
                serializedSetup(settings);
                settings.ApplyModifiedPropertiesWithoutUndo();
                setup(stimulus);
                check(stimulus, () =>
                {
                    stimulus.Show();
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    image.Apply();
                    return image.GetPixels32();
                });
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
