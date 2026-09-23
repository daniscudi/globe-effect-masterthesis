using System;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Testet die Fernglas-Formel von Merlitz.
    ///
    /// Geprüft wird der Weg vorwärts und der Weg rückwärts, der Sonderfall k = 0
    /// und die normierte Rückrechnung, die genauso auch im Shader steckt.
    /// </summary>
    public sealed class MerlitzBinocularReferenceMathTests
    {
        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(0.7)]
        [TestCase(1.0)]
        [TestCase(1.2)]
        public void ForwardAndInverseMapping_RoundTrip(double k)
        {
            const double magnification = 10.0;
            double objectAngle = 3.0 * Math.PI / 180.0;

            double apparent = MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                objectAngle,
                magnification,
                k);
            double reconstructed = MerlitzBinocularReferenceMath.ObjectAngleFromApparent(
                apparent,
                magnification,
                k);

            Assert.That(reconstructed, Is.EqualTo(objectAngle).Within(1e-12));
        }

        [Test]
        public void TangentCondition_EqualsIdealAngularContentZoom()
        {
            const double objectAngle = 5.0 * Math.PI / 180.0;
            const double magnification = 10.0;

            double apparent =
                MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                    objectAngle,
                    magnification,
                    k: 1.0);
            double expected = Math.Atan(
                magnification * Math.Tan(objectAngle));

            Assert.That(apparent, Is.EqualTo(expected).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(1.2)]
        public void MagnificationOne_CollapsesInstrumentMappingToIdentity(double k)
        {
            const double objectAngle = 5.0 * Math.PI / 180.0;

            double apparent =
                MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                    objectAngle,
                    magnification: 1.0,
                    k: k);

            Assert.That(apparent, Is.EqualTo(objectAngle).Within(1e-12));
        }

        [TestCase(0.0)]
        [TestCase(0.3)]
        [TestCase(1.0)]
        public void NormalizedMapping_MapsBoundaryToBoundary(double k)
        {
            double halfAngle = 35.0 * Math.PI / 180.0;
            double sourceRadius = MerlitzBinocularReferenceMath.NormalizedSourceRadius(
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
            double sourceRadius = MerlitzBinocularReferenceMath.NormalizedSourceRadius(
                displayRadius,
                halfAngle,
                10.0,
                1.0);

            Assert.That(sourceRadius, Is.EqualTo(displayRadius).Within(1e-12));
        }
    }
}
