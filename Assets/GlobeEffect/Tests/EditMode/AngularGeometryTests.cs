using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class AngularGeometryTests
    {
        [Test]
        public void DoubleDistance_DoublesPhysicalDiameter()
        {
            double diameterAtOneMeter = AngularGeometry.PhysicalDiameter(1.0, 70.0);
            double diameterAtTwoMeters = AngularGeometry.PhysicalDiameter(2.0, 70.0);

            Assert.That(diameterAtTwoMeters, Is.EqualTo(2.0 * diameterAtOneMeter)
                .Within(1e-12));
        }

        [TestCase(0.5, 20.0)]
        [TestCase(1.0, 70.0)]
        [TestCase(4.0, 120.0)]
        public void PhysicalDiameter_RoundTripsToAngularDiameter(
            double distance,
            double angleDegrees)
        {
            double diameter = AngularGeometry.PhysicalDiameter(distance, angleDegrees);
            double reconstructedAngle = AngularGeometry.AngularDiameterDegrees(
                distance,
                diameter);

            Assert.That(reconstructedAngle, Is.EqualTo(angleDegrees).Within(1e-10));
        }
    }
}
