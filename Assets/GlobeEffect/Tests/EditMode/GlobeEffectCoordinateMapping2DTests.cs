using System;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class GlobeEffectCoordinateMapping2DTests
    {
        [Test]
        public void LinearObjectMapping_RoundTrips()
        {
            var objectCoordinates = new Vector2(0.04f, -0.025f);
            Vector2 image = GlobeEffectCoordinateMapping2D.ObjectToLinearImage(
                objectCoordinates,
                8.0);
            Vector2 reconstructed = GlobeEffectCoordinateMapping2D.LinearImageToObject(
                image,
                8.0);

            Assert.That(reconstructed.x, Is.EqualTo(objectCoordinates.x).Within(1e-7f));
            Assert.That(reconstructed.y, Is.EqualTo(objectCoordinates.y).Within(1e-7f));
        }

        [Test]
        public void MerlitzInstrumentMapping_AtK1EqualsLinearUvMapping()
        {
            var objectCoordinates = new Vector2(0.03f, 0.02f);
            Vector2 linear = GlobeEffectCoordinateMapping2D.ObjectToLinearImage(
                objectCoordinates,
                10.0);
            Vector2 merlitz =
                GlobeEffectCoordinateMapping2D.ObjectToMerlitzInstrumentImage(
                    objectCoordinates,
                    10.0,
                    1.0);

            Assert.That(merlitz.x, Is.EqualTo(linear.x).Within(1e-6f));
            Assert.That(merlitz.y, Is.EqualTo(linear.y).Within(1e-6f));
        }

        [Test]
        public void HorizontalPan_AtZeroKeepsInputCoordinates()
        {
            var input = new Vector2(0.4f, -0.2f);
            Vector2 output = GlobeEffectCoordinateMapping2D.LinearImageAfterHorizontalPan(
                input,
                0.0,
                4.0);

            Assert.That(output.x, Is.EqualTo(input.x).Within(1e-6f));
            Assert.That(output.y, Is.EqualTo(input.y).Within(1e-6f));
        }

        [Test]
        public void LinearVelocity_MatchesFiniteDifferenceOfPanTrajectory()
        {
            var input = new Vector2(0.35f, 0.18f);
            const double magnification = 4.0;
            const double step = 1e-4;

            Vector2 before = GlobeEffectCoordinateMapping2D.LinearImageAfterHorizontalPan(
                input,
                -step,
                magnification);
            Vector2 after = GlobeEffectCoordinateMapping2D.LinearImageAfterHorizontalPan(
                input,
                step,
                magnification);
            Vector2 numerical = (after - before) / (float)(2.0 * step);
            Vector2 analytical =
                GlobeEffectCoordinateMapping2D.LinearImageVelocityForHorizontalPan(
                    input,
                    magnification);

            Assert.That(numerical.x, Is.EqualTo(analytical.x).Within(2e-3f));
            Assert.That(numerical.y, Is.EqualTo(analytical.y).Within(2e-3f));
        }

        [Test]
        public void MerlitzAngularMapping_PreservesAzimuthAndUsesAtanRadius()
        {
            var input = new Vector2(0.4f, 0.3f);
            Vector2 angular =
                GlobeEffectCoordinateMapping2D.LinearImageToMerlitzAngular(input);

            Assert.That(Vector2.Angle(input, angular), Is.LessThan(1e-4f));
            Assert.That(
                angular.magnitude,
                Is.EqualTo((float)Math.Atan(input.magnitude)).Within(1e-6f));
        }
    }
}
