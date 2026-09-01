using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class RandomDotSimulatedSweepTests
    {
        [Test]
        public void RightFirstSweep_ReachesExpectedPositions()
        {
            Assert.That(Evaluate(0d, RandomDotSweepDirection.RightFirst), Is.EqualTo(0f));
            Assert.That(Evaluate(1d, RandomDotSweepDirection.RightFirst), Is.EqualTo(5f));
            Assert.That(Evaluate(2d, RandomDotSweepDirection.RightFirst), Is.EqualTo(0f));
            Assert.That(Evaluate(3d, RandomDotSweepDirection.RightFirst), Is.EqualTo(-5f));
            Assert.That(Evaluate(4d, RandomDotSweepDirection.RightFirst), Is.EqualTo(0f));
        }

        [Test]
        public void LeftFirstSweep_IsMirrored()
        {
            Assert.That(Evaluate(1d, RandomDotSweepDirection.LeftFirst), Is.EqualTo(-5f));
            Assert.That(Evaluate(3d, RandomDotSweepDirection.LeftFirst), Is.EqualTo(5f));
        }

        [Test]
        public void FullCycleDuration_UsesAmplitudeAndSpeed()
        {
            Assert.That(
                RandomDotSimulatedSweep.FullCycleDurationSeconds(5f, 5f),
                Is.EqualTo(4f));
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
