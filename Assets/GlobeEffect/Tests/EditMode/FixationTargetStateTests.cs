using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class FixationTargetStateTests
    {
        [TestCase(false, false, FixationTargetState.NoValidGaze)]
        [TestCase(false, true, FixationTargetState.NoValidGaze)]
        [TestCase(true, false, FixationTargetState.OffTarget)]
        [TestCase(true, true, FixationTargetState.OnTarget)]
        public void ResolveSeparatesValidityAndTargetPosition(
            bool valid,
            bool insideTolerance,
            FixationTargetState expected)
        {
            Assert.That(
                FixationTargetStateResolver.Resolve(valid, insideTolerance),
                Is.EqualTo(expected));
        }
    }
}
