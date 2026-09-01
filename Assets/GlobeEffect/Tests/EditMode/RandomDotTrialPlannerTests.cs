using System.Linq;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft Reihenfolge, feste k-Stufen, ausgeglichene Bewegungsrichtungen und
    /// die über k hinweg vergleichbaren Punkt-Seeds.
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
        public void Plan_ContainsEveryFixedKEquallyOften()
        {
            var plan = CreatePlan(7);

            // 2 FOV * 1 Auge * 2 k * 1 m * 1 Bewegung * 4 Wiederholungen.
            Assert.That(plan.Count, Is.EqualTo(16));
            Assert.That(plan.Count(t => t.StimulusK == 0.3f), Is.EqualTo(8));
            Assert.That(plan.Count(t => t.StimulusK == 0.9f), Is.EqualTo(8));
            Assert.That(plan.All(t => t.AttemptNumber == 1), Is.True);
        }

        [Test]
        public void DirectionsAreBalancedAndSeedsAreMatchedAcrossK()
        {
            var plan = CreatePlan(17);

            foreach (var group in plan.GroupBy(t => new
                     {
                         t.AngularDiameterDegrees,
                         t.StimulusK
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
                int[] lowKSeeds = plan
                    .Where(t => t.AngularDiameterDegrees == fov && t.StimulusK == 0.3f)
                    .OrderBy(t => t.Repetition)
                    .Select(t => t.DotSeed)
                    .ToArray();
                int[] highKSeeds = plan
                    .Where(t => t.AngularDiameterDegrees == fov && t.StimulusK == 0.9f)
                    .OrderBy(t => t.Repetition)
                    .Select(t => t.DotSeed)
                    .ToArray();
                Assert.That(highKSeeds, Is.EqualTo(lowKSeeds));
            }
        }

        private static System.Collections.Generic.IReadOnlyList<RandomDotTrial>
            CreatePlan(int randomSeed)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 40f, 70f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.3f, 0.9f },
                new[] { 10f },
                new[] { RandomDotMotionMode.SimulatedYaw },
                repetitions: 4,
                randomSeed,
                dotSeedBase: 5000);
        }
    }
}
