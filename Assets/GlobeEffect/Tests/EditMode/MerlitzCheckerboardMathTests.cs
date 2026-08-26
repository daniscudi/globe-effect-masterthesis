using System;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class MerlitzCheckerboardMathTests
    {
        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(0.7)]
        [TestCase(1.0)]
        public void ForwardAndInverseMapping_RoundTrip(double k)
        {
            const double magnification = 10.0;
            double objectAngle = 3.0 * Math.PI / 180.0;

            double apparent = MerlitzCheckerboardMath.ApparentAngleFromObject(
                objectAngle,
                magnification,
                k);
            double reconstructed = MerlitzCheckerboardMath.ObjectAngleFromApparent(
                apparent,
                magnification,
                k);

            Assert.That(reconstructed, Is.EqualTo(objectAngle).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(0.3)]
        [TestCase(1.0)]
        public void NormalizedMapping_MapsBoundaryToBoundary(double k)
        {
            double halfAngle = 35.0 * Math.PI / 180.0;
            double sourceRadius = MerlitzCheckerboardMath.NormalizedSourceRadius(
                1.0,
                halfAngle,
                10.0,
                k);

            Assert.That(sourceRadius, Is.EqualTo(1.0).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(0.2)]
        [TestCase(0.75)]
        [TestCase(1.0)]
        public void TangentConditionK1_IsLinearAfterBoundaryNormalization(
            double displayRadius)
        {
            double halfAngle = 35.0 * Math.PI / 180.0;
            double sourceRadius = MerlitzCheckerboardMath.NormalizedSourceRadius(
                displayRadius,
                halfAngle,
                10.0,
                1.0);

            Assert.That(sourceRadius, Is.EqualTo(displayRadius).Within(1e-12));
        }
    }
}
