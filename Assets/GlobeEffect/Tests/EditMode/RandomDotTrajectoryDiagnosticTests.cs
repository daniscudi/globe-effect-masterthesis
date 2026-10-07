using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class RandomDotTrajectoryDiagnosticTests
    {
        [TestCase(4, 5)]
        [TestCase(9, 9)]
        [TestCase(12, 13)]
        [TestCase(18, 17)]
        public void ArrowGrid_AlwaysContainsTheCenter(int requested, int expected)
        {
            Assert.That(RandomDotDiagnosticFlowOverlay.OddGridSize(requested), Is.EqualTo(expected));
        }

        [Test]
        public void ArrowLength_PreservesSpeedRatiosAndRespondsOnlyToSize()
        {
            Vector2 center = RandomDotDiagnosticFlowOverlay.ArrowVector(-10d, 0d, 10d, 1f,
                RandomDotSweepDirection.LeftFirst);
            Vector2 slower = RandomDotDiagnosticFlowOverlay.ArrowVector(-7.5d, 0d, 10d, 1f,
                RandomDotSweepDirection.LeftFirst);
            Vector2 larger = RandomDotDiagnosticFlowOverlay.ArrowVector(-7.5d, 0d, 10d, 2f,
                RandomDotSweepDirection.LeftFirst);
            Assert.That(center.x, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(slower.magnitude / center.magnitude, Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(larger.magnitude / slower.magnitude, Is.EqualTo(2f).Within(1e-6f));
        }

        [Test]
        public void ArrowDirection_IsTheFlowDirectionAndReversesWithPanning()
        {
            Vector2 left = RandomDotDiagnosticFlowOverlay.ArrowVector(-8d, 2d, 10d, 1f,
                RandomDotSweepDirection.LeftFirst);
            Vector2 right = RandomDotDiagnosticFlowOverlay.ArrowVector(-8d, 2d, 10d, 1f,
                RandomDotSweepDirection.RightFirst);
            Assert.That(left.y / left.x, Is.EqualTo(-0.25f).Within(1e-6f));
            Assert.That(right.x, Is.EqualTo(-left.x).Within(1e-6f));
            Assert.That(right.y, Is.EqualTo(-left.y).Within(1e-6f));
        }

        [Test]
        public void AllMeasuringSpaces_HaveTheSameCenterArrowScale()
        {
            foreach (RandomDotDiagnosticCoordinates coordinates in
                System.Enum.GetValues(typeof(RandomDotDiagnosticCoordinates)))
            {
                var velocity = RandomDotDiagnosticFlowMath.Velocity(0d, 0d, 20d, 1d,
                    RandomDotSweepAxis.Horizontal, coordinates);
                Vector2 arrow = RandomDotDiagnosticFlowOverlay.ArrowVector(velocity.x, velocity.y,
                    20d, 1f, RandomDotSweepDirection.LeftFirst);
                Assert.That(arrow.magnitude, Is.EqualTo(0.12f).Within(1e-6f));
            }
        }

        [TestCase(1f, 0f)]
        [TestCase(10f, 0f)]
        [TestCase(10f, 0.5f)]
        [TestCase(10f, 1f)]
        [TestCase(20f, 1.4f)]
        public void MarkerStartArrangement_MapsBackToThreeStraightColumns(float m, float k)
        {
            for (int column = -1; column <= 1; column++)
            for (int row = -1; row <= 1; row++)
            {
                Vector3 direction = RandomDotTrajectoryDiagnostic.MarkerObjectDirection(
                    column, row, 10f, m, k);
                Vector2 source = new Vector2(direction.x, direction.y) / direction.z;
                float radius = source.magnitude;
                float angle = (float)MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                    Mathf.Atan(radius), m, k);
                Vector2 displayed = radius > 1e-6f
                    ? source * (Mathf.Tan(angle) / radius) : Vector2.zero;
                Assert.That(displayed.x, Is.EqualTo(Mathf.Tan(column * 10f * Mathf.Deg2Rad))
                    .Within(1e-5f));
                Assert.That(displayed.y, Is.EqualTo(Mathf.Tan(row * 10f * Mathf.Deg2Rad))
                    .Within(1e-5f));
            }
        }

        [Test]
        public void MarkerGrid_WithEvenCount_LiesSymmetricAroundTheCenter()
        {
            // Vier Spalten: die Mitte liegt zwischen der zweiten und dritten Spalte.
            Vector3 left = RandomDotTrajectoryDiagnostic.MarkerObjectDirection(-0.5f, 0f, 10f, 10f, 1f);
            Vector3 right = RandomDotTrajectoryDiagnostic.MarkerObjectDirection(0.5f, 0f, 10f, 10f, 1f);
            Assert.That(left.x, Is.LessThan(0f));
            Assert.That(right.x, Is.EqualTo(-left.x).Within(1e-6f));
            Assert.That(right.y, Is.EqualTo(0f).Within(1e-6f));
        }

        [TestCase(8f, 0f, 10f)]
        [TestCase(0f, 4f, 25f)]
        [TestCase(4f, 0f, 20f)]
        public void MarkerGrid_DropsPointsBeyondEightyDegrees(float column, float row, float spacing)
        {
            Assert.That(RandomDotTrajectoryDiagnostic.MarkerObjectDirection(column, row, spacing, 10f, 1f),
                Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MarkerGrid_DropsPointsBehindTheTurningPointOfK()
        {
            // k = 1,4 und 70 Grad: 1,4 * 70 Grad liegt hinter 90 Grad.
            Assert.That(RandomDotTrajectoryDiagnostic.MarkerObjectDirection(7f, 0f, 10f, 10f, 1.4f),
                Is.EqualTo(Vector3.zero));
            Assert.That(RandomDotTrajectoryDiagnostic.MarkerObjectDirection(6f, 0f, 10f, 10f, 1f),
                Is.Not.EqualTo(Vector3.zero));
        }

        [TestCase(1d)]
        [TestCase(4d)]
        [TestCase(8d)]
        [TestCase(20d)]
        public void FlowAtKOne_MatchesTheExactLinearImageVelocity(double m)
        {
            const double u = 0.4;
            const double v = 0.3;
            var rate = RandomDotDiagnosticFlowMath.Velocity(u, v, m, 1d,
                RandomDotSweepAxis.Horizontal, RandomDotDiagnosticCoordinates.LinearImage);
            Assert.That(rate.x, Is.EqualTo(-(m + u * u / m)).Within(2e-6));
            Assert.That(rate.y, Is.EqualTo(-u * v / m).Within(2e-6));
        }

        [TestCase(RandomDotDiagnosticCoordinates.LinearImage, 1.005208333333, 1d)]
        [TestCase(RandomDotDiagnosticCoordinates.SeparateAngles, 0.75390625, 1d)]
        [TestCase(RandomDotDiagnosticCoordinates.RadialAngles, 0.75390625, 0.906899682117)]
        public void FlowAtEightTimesAndSixtyDegrees_ReproducesPlotEdges(
            RandomDotDiagnosticCoordinates coordinates, double horizontal, double vertical)
        {
            double radius = System.Math.Tan(System.Math.PI / 6d);
            Assert.That(RandomDotDiagnosticFlowMath.RelativeSpeed(radius, 0d, 8d, 1d,
                RandomDotSweepAxis.Horizontal, coordinates), Is.EqualTo(horizontal).Within(2e-6));
            Assert.That(RandomDotDiagnosticFlowMath.RelativeSpeed(0d, radius, 8d, 1d,
                RandomDotSweepAxis.Horizontal, coordinates), Is.EqualTo(vertical).Within(2e-6));
        }

        [TestCase(0d)]
        [TestCase(0.5d)]
        [TestCase(1d)]
        [TestCase(1.4d)]
        public void MagnificationOne_LeavesFlowIndependentOfK(double k)
        {
            var rate = RandomDotDiagnosticFlowMath.Velocity(0.4, 0.3, 1d, k,
                RandomDotSweepAxis.Horizontal, RandomDotDiagnosticCoordinates.LinearImage);
            Assert.That(rate.x, Is.EqualTo(-1.16d).Within(1e-6));
            Assert.That(rate.y, Is.EqualTo(-0.12d).Within(1e-6));
        }

        [TestCase(0d)]
        [TestCase(0.5d)]
        [TestCase(1d)]
        [TestCase(1.4d)]
        public void AllMeasuringSpaces_HaveUnitSpeedAtTheCenter(double k)
        {
            foreach (RandomDotDiagnosticCoordinates coordinates in
                System.Enum.GetValues(typeof(RandomDotDiagnosticCoordinates)))
                Assert.That(RandomDotDiagnosticFlowMath.RelativeSpeed(0d, 0d, 20d, k,
                    RandomDotSweepAxis.Horizontal, coordinates), Is.EqualTo(1d).Within(2e-6));
        }

        [TestCase(RandomDotDiagnosticCoordinates.LinearImage)]
        [TestCase(RandomDotDiagnosticCoordinates.SeparateAngles)]
        [TestCase(RandomDotDiagnosticCoordinates.RadialAngles)]
        public void VerticalPanning_IsTheAxisSwappedHorizontalField(
            RandomDotDiagnosticCoordinates coordinates)
        {
            var horizontal = RandomDotDiagnosticFlowMath.Velocity(0.4, 0.3, 10d, 0.6d,
                RandomDotSweepAxis.Horizontal, coordinates);
            var vertical = RandomDotDiagnosticFlowMath.Velocity(0.3, 0.4, 10d, 0.6d,
                RandomDotSweepAxis.Vertical, coordinates);
            Assert.That(vertical.x, Is.EqualTo(horizontal.y).Within(1e-6));
            Assert.That(vertical.y, Is.EqualTo(horizontal.x).Within(1e-6));
        }
    }
}
