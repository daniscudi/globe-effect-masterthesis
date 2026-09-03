using System;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class VisualSpaceRadialMappingTests
    {
        [TestCase(0.0)]
        [TestCase(0.25)]
        [TestCase(0.8)]
        [TestCase(1.0)]
        public void LOne_LeavesStraightGridRadiusUnchanged(double radius)
        {
            double source = VisualSpaceRadialMapping.SourceRadius(
                radius,
                90.0,
                visualSpaceL: 1.0);

            Assert.That(source, Is.EqualTo(radius).Within(1e-12));
        }

        [Test]
        public void LHalf_UsesNormalizedHelmholtzRadius()
        {
            double source = VisualSpaceRadialMapping.SourceRadius(
                displayRadius: 0.5,
                angularDiameterDegrees: 90.0,
                visualSpaceL: 0.5);

            double expected = Math.Tan(0.5 * Math.Atan(0.5)) /
                Math.Tan(Math.PI / 8.0);
            Assert.That(source, Is.EqualTo(expected).Within(1e-12));
        }

        [Test]
        public void LZero_UsesEquidistantLimit()
        {
            const double radius = 0.6;
            const double diameterDegrees = 70.0;
            double halfAngle = 0.5 * diameterDegrees * Math.PI / 180.0;
            double expected = Math.Atan(radius * Math.Tan(halfAngle)) /
                halfAngle;

            double source = VisualSpaceRadialMapping.SourceRadius(
                radius,
                diameterDegrees,
                visualSpaceL: 0.0);

            Assert.That(source, Is.EqualTo(expected).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(0.8)]
        [TestCase(1.0)]
        [TestCase(1.4)]
        public void ApertureBoundary_RemainsBoundaryForEveryL(double visualSpaceL)
        {
            double source = VisualSpaceRadialMapping.SourceRadius(
                1.0,
                90.0,
                visualSpaceL);
            Assert.That(source, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void TenDegreeSpacingAtNinetyDegreeFov_HasExpectedUvWidth()
        {
            double spacingUv = VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                angularDiameterDegrees: 90.0,
                gridLineSpacingDegrees: 10.0);

            double expected = Math.Tan(10.0 * Math.PI / 180.0) /
                Math.Tan(45.0 * Math.PI / 180.0);
            Assert.That(spacingUv, Is.EqualTo(expected).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(90.0)]
        public void InvalidAngularGridSpacing_IsRejected(double spacingDegrees)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                    angularDiameterDegrees: 90.0,
                    gridLineSpacingDegrees: spacingDegrees));
        }

        [TestCase(1.0, 0.0)]
        [TestCase(0.5, 1.0)]
        [TestCase(0.0, 2.0)]
        [TestCase(1.4, -0.8)]
        public void EndpointEquivalent_MapsReferenceScale(
            double visualSpaceL,
            double expected)
        {
            Assert.That(
                VisualSpaceRadialMapping.OomesEndpointEquivalent(visualSpaceL),
                Is.EqualTo(expected).Within(1e-12));
        }

        [TestCase(-0.01)]
        [TestCase(1.01)]
        public void RadiusOutsideCircularAperture_IsRejected(double radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VisualSpaceRadialMapping.SourceRadius(radius, 90.0, 0.5));
        }

        [TestCase(-0.01)]
        [TestCase(1.41)]
        public void LOutsideConfiguredRange_IsRejected(double visualSpaceL)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VisualSpaceRadialMapping.SourceRadius(
                    0.5,
                    90.0,
                    visualSpaceL));
        }

        [Test]
        public void NonMonotonicFovAndLCombination_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                VisualSpaceRadialMapping.SourceRadius(
                    0.5,
                    170.0,
                    visualSpaceL: 1.4));
        }
    }
}
