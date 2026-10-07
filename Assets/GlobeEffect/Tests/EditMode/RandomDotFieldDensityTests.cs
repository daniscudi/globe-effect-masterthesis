using NUnit.Framework;
using Field = GlobeEffect.VRCheckerboard.RandomDots.RandomDotFieldStimulus;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Testet, wie groß die Punktwelt wird und wie viele Punkte hineinkommen.
    ///
    /// Die Idee dahinter: In der Bildmitte soll es bei jedem m gleich dicht
    /// aussehen. Doppeltes m heißt also viermal so viele Punkte pro Fläche in
    /// der Außenwelt.
    /// </summary>
    public sealed class RandomDotFieldDensityTests
    {
        [Test]
        public void DotCount_MatchesTheOldSetupAtMagnificationTen()
        {
            // Früher: 24.500 Punkte auf 40,6 Grad bei m = 10.
            Assert.That(Field.DotCountFor(0.19f, 10f, 1f, 40.6f), Is.EqualTo(24500).Within(1).Percent);
        }

        [Test]
        public void DotCount_GrowsWithTheSquareOfMagnification()
        {
            int atSeven = Field.DotCountFor(0.19f, 7f, 1f, 20f);
            int atFourteen = Field.DotCountFor(0.19f, 14f, 1f, 20f);
            Assert.That(atFourteen / (float)atSeven, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void DotCount_TreatsContentZoomLikeExtraMagnification()
        {
            int zoomed = Field.DotCountFor(0.19f, 5f, 2f, 20f);
            Assert.That(zoomed, Is.EqualTo(Field.DotCountFor(0.19f, 10f, 1f, 20f)));
        }

        [Test]
        public void Coverage_CoversVisibleFieldPlusSweepPlusMargin()
        {
            // FOV 90, m = 10, k = 0,5: Am Rand des Kreises landet ein Weltwinkel
            // von 4,744 Grad. Dazu 1 Grad Reserve, und das Ganze mal zwei.
            Assert.That(Field.CoverageNeeded(90f, 1f, 10f, 0.5f, 0f), Is.EqualTo(11.488f).Within(0.01f));

            // Jedes Grad Schwenk zu jeder Seite macht die Welt 2 Grad breiter.
            Assert.That(Field.CoverageNeeded(90f, 1f, 10f, 0.5f, 3f), Is.EqualTo(17.488f).Within(0.01f));
        }

        [Test]
        public void MagnificationTwenty_FitsIntoThePointLimitWithTheSceneSettings()
        {
            // Szenenwerte: 70 Grad FOV, Dichte 0,19, kein Zusatzzoom. Im HeadTracked-
            // Block kommen 15 Grad Sicherheitsbereich dazu, im simulierten Block nur
            // die Schwenkweite. Geprüft werden alle k-Werte der Szene.
            Assert.That(Field.MaximumInstrumentMagnification, Is.EqualTo(20f));
            foreach (float k in new[] { 1.2f, 1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.2f })
            {
                float headTracked = Field.CoverageNeeded(70f, 1f, 20f, k, 15f);
                float simulated = Field.CoverageNeeded(70f, 1f, 20f, k, 0.24f);
                Assert.That(headTracked, Is.LessThan(Field.MaximumCoverageDegrees));
                Assert.That(Field.DotCountFor(0.19f, 20f, 1f, headTracked),
                    Is.InRange(70000, Field.MaximumDotCount));
                Assert.That(Field.DotCountFor(0.19f, 20f, 1f, simulated), Is.InRange(2000, 3000));
            }
        }

        [Test]
        public void PointWorldForMagnificationTwenty_IsBuiltCompletely()
        {
            // Der größte Fall der Szene: m = 20, k = 1,2, HeadTracked-Block. Das
            // Punktfeld muss alle rund 78.000 Punkte wirklich erzeugen und darf
            // die Zahl nicht an der Obergrenze abschneiden.
            var fieldObject = new UnityEngine.GameObject("Dot field test");
            try
            {
                Field field = fieldObject.AddComponent<Field>();
                field.SetInstrumentMagnification(20f);
                float coverage = Field.CoverageNeeded(70f, 1f, 20f, 1.2f, 15f);

                var watch = System.Diagnostics.Stopwatch.StartNew();
                field.ConfigurePointField(24680, coverage);
                watch.Stop();

                Assert.That(field.InstrumentMagnificationM, Is.EqualTo(20f));
                Assert.That(field.DotCount,
                    Is.EqualTo(Field.DotCountFor(field.DotDensity, 20f, 1f, coverage)));
                UnityEngine.Debug.Log($"Punktwelt für m = 20: {field.DotCount} Punkte, " +
                    $"gebaut in {watch.ElapsedMilliseconds} ms.");

                // Der Aufbau passiert einmal pro Durchgang, während nur das Kreuz
                // zu sehen ist. Eine Sekunde wäre dafür deutlich zu lang.
                Assert.That(watch.ElapsedMilliseconds, Is.LessThan(1000));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fieldObject);
            }
        }

        [Test]
        public void PreviewSpeed_NormalizesMagnificationWithoutChangingTheManagersRuntimeSpeed()
        {
            var fieldObject = new UnityEngine.GameObject("Preview speed test");
            try
            {
                Field field = fieldObject.AddComponent<Field>();
                var settings = new UnityEditor.SerializedObject(field);
                settings.FindProperty("previewImageCenterSpeed").floatValue = 5f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                foreach (float zoom in new[] { 1f, 2f })
                foreach (float m in new[] { 1f, 10f, 20f })
                {
                    field.SetInstrumentMagnification(m);
                    field.SetContentZoom(zoom);
                    Assert.That(field.SweepSpeedDegreesPerSecond, Is.EqualTo(5f / (m * zoom)).Within(1e-5f));
                }

                field.SessionRunning = true;
                field.SetSimulatedSweep(2f, 0.6f);
                Assert.That(field.SweepSpeedDegreesPerSecond, Is.EqualTo(0.6f),
                    "In Training/Messung muss allein die vom Manager berechnete Geschwindigkeit gelten.");
                field.SessionRunning = false;
                Assert.That(field.SweepSpeedDegreesPerSecond, Is.EqualTo(5f / 40f).Within(1e-5f));
                settings.Update();
                settings.FindProperty("previewSpeedReference").intValue =
                    (int)GlobeEffect.VRCheckerboard.RandomDots.RandomDotSweepSpeedReference.ObjectAngle;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(field.SweepSpeedDegreesPerSecond, Is.EqualTo(0.6f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fieldObject);
            }
        }

        [Test]
        public void CenterDensity_StaysTheSameAtEveryMagnification()
        {
            // Punkte pro Quadratgrad Außenwelt, geteilt durch die Flächenvergrößerung
            // m², ergibt die Dichte im Bild. Sie muss bei jedem m 0,19 sein.
            foreach (float m in new[] { 5f, 10f, 14f, 20f })
            {
                const float coverage = 12f;
                float halfAngle = 0.5f * coverage * UnityEngine.Mathf.Deg2Rad;
                float capArea = 2f * UnityEngine.Mathf.PI * (1f - UnityEngine.Mathf.Cos(halfAngle))
                    * UnityEngine.Mathf.Rad2Deg * UnityEngine.Mathf.Rad2Deg;
                float imageDensity = Field.DotCountFor(0.19f, m, 1f, coverage) / capArea / (m * m);
                Assert.That(imageDensity, Is.EqualTo(0.19f).Within(0.001f));
            }
        }

        [Test]
        public void Coverage_IsInfiniteBehindTheTurningPoint()
        {
            // k = 1,4 und 170 Grad FOV: 1,4 * 85 Grad liegt hinter 90 Grad.
            Assert.That(float.IsPositiveInfinity(Field.CoverageNeeded(170f, 1f, 10f, 1.4f, 0f)), Is.True);
        }
    }
}
