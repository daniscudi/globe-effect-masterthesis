using System.Linq;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft Reihenfolge, feste l-Stufen, ausgeglichene Bewegungsrichtungen und
    /// die über l hinweg vergleichbaren Punkt-Seeds.
    /// </summary>
    public sealed class RandomDotTrialPlannerTests
    {
        [Test]
        public void SameSeed_ProducesSameOrderAndDotSeeds()
        {
            var first = CreatePlan(1234);
            var second = CreatePlan(1234);

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(first[index].ConditionIndex,
                    Is.EqualTo(second[index].ConditionIndex));
                Assert.That(first[index].DotSeed,
                    Is.EqualTo(second[index].DotSeed));
                Assert.That(first[index].SweepDirection,
                    Is.EqualTo(second[index].SweepDirection));
                Assert.That(first[index].SequenceIndex, Is.EqualTo(index + 1));
            }
        }

        [Test]
        public void Plan_ContainsEveryFixedLEquallyOften()
        {
            var plan = CreatePlan(7);

            // 2 FOV * 1 Auge * 2 l * 1 Zoom * 1 Bewegung * 4 Wiederholungen.
            Assert.That(plan.Count, Is.EqualTo(16));
            Assert.That(plan.Count(t => t.VisualSpaceL == 0.5f), Is.EqualTo(8));
            Assert.That(plan.Count(t => t.VisualSpaceL == 1f), Is.EqualTo(8));
            Assert.That(plan.All(t => t.AttemptNumber == 1), Is.True);
        }

        [Test]
        public void DirectionsAreBalancedAndSeedsAreMatchedAcrossL()
        {
            var plan = CreatePlan(17);

            foreach (var group in plan.GroupBy(t => new
                     {
                         t.AngularDiameterDegrees,
                         t.VisualSpaceL
                     }))
            {
                Assert.That(group.Count(
                    t => t.SweepDirection == RandomDotSweepDirection.LeftFirst),
                    Is.EqualTo(2));
                Assert.That(group.Count(
                    t => t.SweepDirection == RandomDotSweepDirection.RightFirst),
                    Is.EqualTo(2));
            }

            foreach (float fov in new[] { 40f, 70f })
            {
                int[] helmholtzSeeds = plan
                    .Where(t => t.AngularDiameterDegrees == fov && t.VisualSpaceL == 0.5f)
                    .OrderBy(t => t.Repetition)
                    .Select(t => t.DotSeed)
                    .ToArray();
                int[] straightSeeds = plan
                    .Where(t => t.AngularDiameterDegrees == fov && t.VisualSpaceL == 1f)
                    .OrderBy(t => t.Repetition)
                    .Select(t => t.DotSeed)
                    .ToArray();
                Assert.That(straightSeeds, Is.EqualTo(helmholtzSeeds));
            }
        }

        private static System.Collections.Generic.IReadOnlyList<RandomDotTrial>
            CreatePlan(int randomSeed)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 40f, 70f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.5f, 1f },
                new[] { 1f },
                new[] { RandomDotMotionMode.SimulatedYaw },
                repetitions: 4,
                randomSeed,
                dotSeedBase: 5000);
        }
    }
}
