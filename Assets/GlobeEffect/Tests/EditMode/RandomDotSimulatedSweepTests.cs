using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using Sweep = GlobeEffect.VRCheckerboard.RandomDots.RandomDotSimulatedSweep;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Testet die beiden Schwenkbahnen.
    ///
    /// Einseitig: Bei 2 Grad Amplitude und 5 Grad pro Sekunde geht es in 0,8
    /// Sekunden einmal von -2 nach +2 Grad.
    ///
    /// Hin und her: Bei 5 Grad Ausschlag und 5 Grad mittlerer Geschwindigkeit
    /// dauert eine volle Sinusperiode vier Sekunden.
    /// </summary>
    public sealed class RandomDotSimulatedSweepTests
    {
        private const RandomDotSweepDirection Right = RandomDotSweepDirection.RightFirst;
        private const RandomDotSweepDirection Left = RandomDotSweepDirection.LeftFirst;

        [Test]
        public void OneWaySweep_GoesOnceFromOneSideToTheOther()
        {
            Assert.That(OneWay(0d, Right), Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(OneWay(0.2d, Right), Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(OneWay(0.4d, Right), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(OneWay(0.8d, Right), Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void OneWaySweep_StopsAtTheEndInsteadOfTurningBack()
        {
            Assert.That(OneWay(1.5d, Right), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(OneWay(10d, Right), Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void OneWaySweep_LeftIsMirrored()
        {
            Assert.That(OneWay(0d, Left), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(OneWay(0.8d, Left), Is.EqualTo(-2f).Within(0.0001f));
        }

        [Test]
        public void RightFirstSweep_ReachesExpectedPositions()
        {
            Assert.That(BackAndForth(0d, Right), Is.EqualTo(0f));
            Assert.That(BackAndForth(1d, Right), Is.EqualTo(5f));
            Assert.That(BackAndForth(2d, Right), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(BackAndForth(3d, Right), Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(BackAndForth(4d, Right), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void LeftFirstSweep_IsMirrored()
        {
            Assert.That(BackAndForth(1d, Left), Is.EqualTo(-5f));
            Assert.That(BackAndForth(3d, Left), Is.EqualTo(5f));
        }

        [Test]
        public void DirectionLabels_DependOnModeAndAxis()
        {
            // Einseitig heißt es nur "Right", hin und her "RightFirst".
            const RandomDotMotionMode simulated = RandomDotMotionMode.SimulatedYaw;
            const RandomDotMotionMode head = RandomDotMotionMode.HeadTracked;
            const RandomDotSweepAxis horizontal = RandomDotSweepAxis.Horizontal;
            const RandomDotSweepAxis vertical = RandomDotSweepAxis.Vertical;
            Assert.That(Sweep.DirectionLabel(simulated, horizontal, Right), Is.EqualTo("Right"));
            Assert.That(Sweep.DirectionLabel(simulated, horizontal, Left), Is.EqualTo("Left"));
            Assert.That(Sweep.DirectionLabel(simulated, vertical, Right), Is.EqualTo("Up"));
            Assert.That(Sweep.DirectionLabel(simulated, vertical, Left), Is.EqualTo("Down"));
            Assert.That(Sweep.DirectionLabel(head, horizontal, Right), Is.EqualTo("RightFirst"));
            Assert.That(Sweep.DirectionLabel(head, horizontal, Left), Is.EqualTo("LeftFirst"));
        }

        [Test]
        public void FiveSecondProfile_ReachesBothExtremesSmoothly()
        {
            float firstExtreme = BackAndForth(5d / 3d, Right, 2f, 1.2f);
            float otherExtreme = BackAndForth(5d, Right, 2f, 1.2f);
            float beforeTurn = BackAndForth(5d / 3d - 0.01d, Right, 2f, 1.2f);
            float afterTurn = BackAndForth(5d / 3d + 0.01d, Right, 2f, 1.2f);

            Assert.That(firstExtreme, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(otherExtreme, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(beforeTurn, Is.EqualTo(afterTurn).Within(0.0001f));
        }

        [Test]
        public void ProfileEvaluator_SeparatesMatchedMovementFromStationaryHead()
        {
            var matched = new RandomDotSweepProfileEvaluator(2f, 1.2f, Right);
            var stationary = new RandomDotSweepProfileEvaluator(2f, 1.2f, Right);

            for (int frame = 1; frame <= 300; frame++)
            {
                double elapsed = frame / 60d;
                matched.AddSample(elapsed, BackAndForth(elapsed, Right, 2f, 1.2f), 1f / 60f);
                stationary.AddSample(elapsed, 0f, 1f / 60f);
            }

            Assert.That(matched.RootMeanSquareErrorDegrees, Is.LessThan(0.0001f));
            Assert.That(matched.FirstExtremeErrorDegrees, Is.LessThan(0.0001f));
            Assert.That(matched.SecondExtremeErrorDegrees, Is.LessThan(0.0001f));
            Assert.That(stationary.RootMeanSquareErrorDegrees, Is.GreaterThan(1f));
            Assert.That(stationary.SecondExtremeErrorDegrees, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void ImageCenterReference_GivesTheSameImageSpeedAtEveryMagnification()
        {
            // Gewünscht: 12 Grad pro Sekunde in der Bildmitte. Das Instrument muss
            // dafür bei großem m langsamer über die Außenwelt schwenken.
            foreach (float m in new[] { 5f, 10f, 14f, 20f })
            {
                float objectSpeed = Sweep.ObjectSpeed(
                    RandomDotSweepSpeedReference.ImageCenter, 1.2f, 12f, m, contentZoom: 1f);
                Assert.That(objectSpeed, Is.EqualTo(12f / m).Within(0.0001f));
                Assert.That(Sweep.ImageCenterSpeed(objectSpeed, m, 1f), Is.EqualTo(12f).Within(0.0001f));
            }
        }

        [Test]
        public void ImageCenterReference_ReproducesTheOldSetupAtMagnificationTen()
        {
            // Bisher: m = 10 und 1,2 Grad pro Sekunde. Das sind 12 Grad pro Sekunde
            // in der Bildmitte; mit der Voreinstellung ändert sich dort also nichts.
            Assert.That(
                Sweep.ObjectSpeed(RandomDotSweepSpeedReference.ImageCenter, 1.2f, 12f, 10f, 1f),
                Is.EqualTo(1.2f).Within(0.0001f));
        }

        [Test]
        public void ObjectAngleReference_KeepsTheOldDefinition()
        {
            // Das Instrument schwenkt bei jedem m gleich schnell. Die Punkte laufen
            // dann in der Bildmitte m-mal so schnell.
            foreach (float m in new[] { 5f, 20f })
            {
                float objectSpeed = Sweep.ObjectSpeed(
                    RandomDotSweepSpeedReference.ObjectAngle, 1.2f, 12f, m, contentZoom: 1f);
                Assert.That(objectSpeed, Is.EqualTo(1.2f));
                Assert.That(Sweep.ImageCenterSpeed(objectSpeed, m, 1f), Is.EqualTo(1.2f * m).Within(0.0001f));
            }
        }

        [Test]
        public void ContentZoom_CountsLikeExtraMagnificationForTheImageSpeed()
        {
            Assert.That(
                Sweep.ObjectSpeed(RandomDotSweepSpeedReference.ImageCenter, 1.2f, 12f, 5f, contentZoom: 2f),
                Is.EqualTo(1.2f).Within(0.0001f));
        }

        [Test]
        public void ImageCenterSpeed_MatchesTheInstrumentFormulaForEveryK()
        {
            // Die Aussage "in der Mitte m-mal so schnell" gegen die Merlitz-Formel
            // geprüft: Ein kleiner Objektwinkel nahe der Achse erscheint bei jedem k
            // m-mal so groß.
            const double smallObjectAngle = 1e-4;
            foreach (double k in new[] { 0.0, 0.2, 0.5, 1.0, 1.2 })
            {
                double apparent = MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                    smallObjectAngle, 20.0, k);
                Assert.That(apparent / smallObjectAngle, Is.EqualTo(20.0).Within(0.001));
            }
        }

        private static float OneWay(double elapsedSeconds, RandomDotSweepDirection direction)
        {
            return Sweep.EvaluateOneWayDegrees(elapsedSeconds, 2f, 5f, direction);
        }

        private static float BackAndForth(
            double elapsedSeconds, RandomDotSweepDirection direction,
            float amplitudeDegrees = 5f, float speedDegreesPerSecond = 5f)
        {
            return Sweep.EvaluateBackAndForthDegrees(
                elapsedSeconds, amplitudeDegrees, speedDegreesPerSecond, direction);
        }
    }
}
