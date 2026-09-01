using System.Linq;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft die reproduzierbare Reihenfolge und die stabilen Punkt-Seeds des
    /// Random-Dot-Plans sowie die Anzahl der vollfaktoriellen Bedingungen.
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
                Assert.That(first[index].SequenceIndex, Is.EqualTo(index + 1));
            }
        }

        [Test]
        public void FullFactorialPlan_ContainsEveryCompleteCondition()
        {
            var plan = CreatePlan(7);

            // 2 FOV * 1 Auge * 2 Start-k * 2 m * 1 Bewegung * 2 Wiederholungen.
            Assert.That(plan.Count, Is.EqualTo(16));
            Assert.That(plan.Count(t => t.StartingK == 0.3f), Is.EqualTo(8));
            Assert.That(plan.Count(t => t.StartingK == 0.9f), Is.EqualTo(8));
            Assert.That(plan.Select(t => t.DotSeed).Distinct().Count(),
                Is.EqualTo(plan.Count));
        }

        private static System.Collections.Generic.IReadOnlyList<RandomDotTrial>
            CreatePlan(int randomSeed)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 40f, 70f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.3f, 0.9f },
                new[] { 5f, 10f },
                new[] { RandomDotMotionMode.HeadTracked },
                repetitions: 2,
                randomSeed,
                dotSeedBase: 5000);
        }
    }
}
