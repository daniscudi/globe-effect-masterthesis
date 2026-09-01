using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class RandomDotTrialQueueTests
    {
        [Test]
        public void InvalidTrial_IsRepeatedAtEndWithHigherAttemptNumber()
        {
            RandomDotTrial first = CreateTrial(sequenceIndex: 1);
            RandomDotTrial second = CreateTrial(sequenceIndex: 2);
            var queue = new RandomDotTrialQueue(new[] { first, second });

            Assert.That(queue.TryTakeNext(out RandomDotTrial taken), Is.True);
            Assert.That(taken, Is.SameAs(first));

            RandomDotTrial repeat = queue.AppendRepeatedAttempt(taken);
            Assert.That(repeat.SequenceIndex, Is.EqualTo(1));
            Assert.That(repeat.AttemptNumber, Is.EqualTo(2));

            Assert.That(queue.TryTakeNext(out taken), Is.True);
            Assert.That(taken, Is.SameAs(second));
            Assert.That(queue.TryTakeNext(out taken), Is.True);
            Assert.That(taken, Is.SameAs(repeat));
        }

        private static RandomDotTrial CreateTrial(int sequenceIndex)
        {
            return new RandomDotTrial(
                sequenceIndex,
                conditionIndex: sequenceIndex,
                repetition: 1,
                attemptNumber: 1,
                angularDiameterDegrees: 70f,
                eyePresentation: CheckerboardEyePresentation.BothEyes,
                stimulusK: 0.6f,
                magnification: 10f,
                motionMode: RandomDotMotionMode.SimulatedYaw,
                sweepDirection: RandomDotSweepDirection.RightFirst,
                dotSeed: 123);
        }
    }
}
