using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Testet die Links-Rechts-Bewegung der Punkte.
    ///
    /// Bei 5 Grad Ausschlag und 5 Grad mittlerer Geschwindigkeit dauert eine
    /// volle Sinusperiode vier Sekunden.
    /// </summary>
    public sealed class RandomDotSimulatedSweepTests
    {
        [Test]
        public void RightFirstSweep_ReachesExpectedPositions()
        {
            Assert.That(Evaluate(0d, RandomDotSweepDirection.RightFirst), Is.EqualTo(0f));
            Assert.That(Evaluate(1d, RandomDotSweepDirection.RightFirst), Is.EqualTo(5f));
            Assert.That(Evaluate(2d, RandomDotSweepDirection.RightFirst), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Evaluate(3d, RandomDotSweepDirection.RightFirst), Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(Evaluate(4d, RandomDotSweepDirection.RightFirst), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void LeftFirstSweep_IsMirrored()
        {
            Assert.That(Evaluate(1d, RandomDotSweepDirection.LeftFirst), Is.EqualTo(-5f));
            Assert.That(Evaluate(3d, RandomDotSweepDirection.LeftFirst), Is.EqualTo(5f));
        }

        [Test]
        public void VerticalSweep_UsesSameSinusProfileAndAxisSpecificLabels()
        {
            Assert.That(RandomDotSimulatedSweep.EvaluateSweepDegrees(
                1d, 5f, 5f, RandomDotSweepDirection.RightFirst), Is.EqualTo(5f));
            Assert.That(RandomDotSimulatedSweep.EvaluateSweepDegrees(
                3d, 5f, 5f, RandomDotSweepDirection.LeftFirst), Is.EqualTo(5f));
            Assert.That(RandomDotSimulatedSweep.DirectionLabel(
                RandomDotSweepAxis.Vertical, RandomDotSweepDirection.RightFirst),
                Is.EqualTo("UpFirst"));
            Assert.That(RandomDotSimulatedSweep.DirectionLabel(
                RandomDotSweepAxis.Vertical, RandomDotSweepDirection.LeftFirst),
                Is.EqualTo("DownFirst"));
            Assert.That(RandomDotSimulatedSweep.DirectionLabel(
                RandomDotSweepAxis.Horizontal, RandomDotSweepDirection.RightFirst),
                Is.EqualTo("RightFirst"));
        }

        [Test]
        public void FullCycleDuration_UsesAmplitudeAndSpeed()
        {
            Assert.That(
                RandomDotSimulatedSweep.FullCycleDurationSeconds(5f, 5f),
                Is.EqualTo(4f));
        }

        [Test]
        public void FiveSecondProfile_ReachesBothExtremesSmoothly()
        {
            float firstExtreme = RandomDotSimulatedSweep.EvaluateYawDegrees(
                5d / 3d, 2f, 1.2f, RandomDotSweepDirection.RightFirst);
            float otherExtreme = RandomDotSimulatedSweep.EvaluateYawDegrees(
                5d, 2f, 1.2f, RandomDotSweepDirection.RightFirst);
            float beforeTurn = RandomDotSimulatedSweep.EvaluateYawDegrees(
                5d / 3d - 0.01d, 2f, 1.2f, RandomDotSweepDirection.RightFirst);
            float afterTurn = RandomDotSimulatedSweep.EvaluateYawDegrees(
                5d / 3d + 0.01d, 2f, 1.2f, RandomDotSweepDirection.RightFirst);

            Assert.That(firstExtreme, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(otherExtreme, Is.EqualTo(-2f).Within(0.0001f));
            Assert.That(beforeTurn, Is.EqualTo(afterTurn).Within(0.0001f));
        }

        [Test]
        public void ProfileEvaluator_SeparatesMatchedMovementFromStationaryHead()
        {
            var matched = new RandomDotSweepProfileEvaluator(
                2f, 1.2f, RandomDotSweepDirection.RightFirst);
            var stationary = new RandomDotSweepProfileEvaluator(
                2f, 1.2f, RandomDotSweepDirection.RightFirst);

            for (int frame = 1; frame <= 300; frame++)
            {
                double elapsed = frame / 60d;
                float target = RandomDotSimulatedSweep.EvaluateYawDegrees(
                    elapsed, 2f, 1.2f, RandomDotSweepDirection.RightFirst);
                matched.AddSample(elapsed, target, 1f / 60f);
                stationary.AddSample(elapsed, 0f, 1f / 60f);
            }

            Assert.That(matched.RootMeanSquareErrorDegrees, Is.LessThan(0.0001f));
            Assert.That(matched.FirstExtremeErrorDegrees, Is.LessThan(0.0001f));
            Assert.That(matched.SecondExtremeErrorDegrees, Is.LessThan(0.0001f));
            Assert.That(stationary.RootMeanSquareErrorDegrees, Is.GreaterThan(1f));
            Assert.That(stationary.SecondExtremeErrorDegrees, Is.EqualTo(2f).Within(0.0001f));
        }

        private static float Evaluate(
            double elapsedSeconds,
            RandomDotSweepDirection direction)
        {
            return RandomDotSimulatedSweep.EvaluateYawDegrees(
                elapsedSeconds,
                amplitudeDegrees: 5f,
                speedDegreesPerSecond: 5f,
                direction);
        }
    }
}
