using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardTrialQueueTests
    {
        [Test]
        public void InvalidTrial_IsAppendedAfterAllStillPendingTrials()
        {
            CheckerboardTrial first = CreateTrial(sequence: 1, visualSpaceL: 0.5f);
            CheckerboardTrial second = CreateTrial(sequence: 2, visualSpaceL: 1f);
            var queue = new CheckerboardTrialQueue(new[] { first, second });

            Assert.That(queue.TryTakeNext(out CheckerboardTrial shown), Is.True);
            CheckerboardTrial repeat = queue.AppendRepeatedAttempt(shown);

            Assert.That(queue.TryTakeNext(out CheckerboardTrial next), Is.True);
            Assert.That(next.SequenceIndex, Is.EqualTo(2));
            Assert.That(queue.TryTakeNext(out CheckerboardTrial last), Is.True);
            Assert.That(last.SequenceIndex, Is.EqualTo(1));
            Assert.That(last.AttemptNumber, Is.EqualTo(2));
            Assert.That(repeat, Is.SameAs(last));
        }

        private static CheckerboardTrial CreateTrial(
            int sequence,
            float visualSpaceL)
        {
            return new CheckerboardTrial(
                sequence,
                conditionIndex: sequence,
                repetition: 1,
                attemptNumber: 1,
                angularDiameterDegrees: 90f,
                eyePresentation: CheckerboardEyePresentation.BothEyes,
                visualSpaceL: visualSpaceL);
        }
    }
}
