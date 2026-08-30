using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class AlternatingHeadSweepCounterTests
    {
        [Test]
        public void SameSideSamples_CountOnlyOnceAfterOppositeExtreme()
        {
            var counter = new AlternatingHeadSweepCounter(5f);

            Assert.That(counter.Update(6f), Is.False);
            Assert.That(counter.Update(8f), Is.False);
            Assert.That(counter.Update(5.5f), Is.False);
            Assert.That(counter.Update(-6f), Is.True);
            Assert.That(counter.Update(-9f), Is.False);
            Assert.That(counter.CompletedHalfSweeps, Is.EqualTo(1));
        }

        [Test]
        public void AlternatingExtremes_ProduceExpectedHalfSweepsAndRange()
        {
            var counter = new AlternatingHeadSweepCounter(5f);
            float[] yawSamples = { 0f, 6f, 1f, -7f, 0f, 8f, -9f };

            foreach (float sample in yawSamples)
            {
                counter.Update(sample);
            }

            Assert.That(counter.CompletedHalfSweeps, Is.EqualTo(3));
            Assert.That(counter.MinimumYawDegrees, Is.EqualTo(-9f));
            Assert.That(counter.MaximumYawDegrees, Is.EqualTo(8f));
            Assert.That(counter.MaximumAbsoluteYawDegrees, Is.EqualTo(9f));
        }

        [Test]
        public void Reset_RemovesPreviousTrialState()
        {
            var counter = new AlternatingHeadSweepCounter(5f);
            counter.Update(6f);
            counter.Update(-6f);

            counter.Reset();

            Assert.That(counter.CompletedHalfSweeps, Is.Zero);
            Assert.That(counter.MinimumYawDegrees, Is.Zero);
            Assert.That(counter.MaximumYawDegrees, Is.Zero);
            Assert.That(counter.Update(-6f), Is.False);
        }
    }
}
