using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardTrialPlannerTests
    {
        [Test]
        public void SameSeed_ProducesSameCompleteTrialOrder()
        {
            IReadOnlyList<CheckerboardTrial> first = CreatePlan(seed: 1234);
            IReadOnlyList<CheckerboardTrial> second = CreatePlan(seed: 1234);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].ConditionIndex,
                    Is.EqualTo(first[index].ConditionIndex));
                Assert.That(second[index].Repetition,
                    Is.EqualTo(first[index].Repetition));
                Assert.That(second[index].VisualSpaceL,
                    Is.EqualTo(first[index].VisualSpaceL));
                Assert.That(second[index].EyePresentation,
                    Is.EqualTo(first[index].EyePresentation));
            }
        }

        [Test]
        public void AllInspectorCombinations_AppearEquallyOften()
        {
            IReadOnlyList<CheckerboardTrial> plan = CreatePlan(seed: 7);

            // 2 FOV x 2 Augenmodi x 2 l-Werte x 2 Wiederholungen.
            Assert.That(plan.Count, Is.EqualTo(16));

            var occurrenceByCondition = new Dictionary<int, int>();
            foreach (CheckerboardTrial trial in plan)
            {
                occurrenceByCondition.TryGetValue(trial.ConditionIndex, out int count);
                occurrenceByCondition[trial.ConditionIndex] = count + 1;
                Assert.That(trial.SequenceIndex, Is.InRange(1, plan.Count));
                Assert.That(trial.Repetition, Is.InRange(1, 2));
                Assert.That(trial.AttemptNumber, Is.EqualTo(1));
            }

            Assert.That(occurrenceByCondition.Count, Is.EqualTo(8));
            foreach (int count in occurrenceByCondition.Values)
            {
                Assert.That(count, Is.EqualTo(2));
            }
        }

        [Test]
        public void LOutsideConfiguredRange_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                CheckerboardTrialPlanner.CreateRandomizedPlan(
                    new[] { 90f },
                    new[] { CheckerboardEyePresentation.BothEyes },
                    new[] { 1.5f },
                    1,
                    1));
        }

        [TestCase("pilot 01", "pilot_01")]
        [TestCase("../Tolga/Test", "Tolga_Test")]
        [TestCase("", "fallback")]
        public void SessionIdentifier_IsMadeFileSafe(string input, string expected)
        {
            Assert.That(
                CheckerboardExperimentFiles.SanitizeIdentifier(input, "fallback"),
                Is.EqualTo(expected));
        }

        private static IReadOnlyList<CheckerboardTrial> CreatePlan(int seed)
        {
            return CheckerboardTrialPlanner.CreateRandomizedPlan(
                new[] { 70f, 90f },
                new[]
                {
                    CheckerboardEyePresentation.BothEyes,
                    CheckerboardEyePresentation.LeftEyeOnly
                },
                new[] { 0.5f, 1f },
                2,
                seed);
        }
    }
}
