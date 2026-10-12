using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardPerLevelRepetitionTests
    {
        [Test]
        public void PilotDesign_HasFewerTrialsAtTheEdgesAnd249InTotal()
        {
            // l 0,2 16-mal, 0,3 bis 1,1 je 25-mal, 1,2 8-mal.
            float[] l = { 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.1f, 1.2f };
            int[] repeats = { 16, 25, 25, 25, 25, 25, 25, 25, 25, 25, 8 };
            IReadOnlyList<CheckerboardTrial> plan = CheckerboardTrialPlanner.CreateRandomizedPlan(
                new[] { 70f }, new[] { CheckerboardEyePresentation.BothEyes }, l, 25, 1234, repeats);

            Assert.That(plan.Count, Is.EqualTo(249));
            for (int index = 0; index < l.Length; index++)
            {
                int count = 0;
                foreach (CheckerboardTrial trial in plan)
                    if (trial.VisualSpaceL == l[index]) count++;
                Assert.That(count, Is.EqualTo(repeats[index]), $"l = {l[index]}");
            }
        }

        [Test]
        public void MissingRepetitions_FallBackToRepeatsPerCondition()
        {
            IReadOnlyList<CheckerboardTrial> plan = CheckerboardTrialPlanner.CreateRandomizedPlan(
                new[] { 70f }, new[] { CheckerboardEyePresentation.BothEyes }, new[] { 0.5f, 1f }, 3, 1,
                new[] { 5 });
            Assert.That(plan.Count, Is.EqualTo(5 + 3));
        }
    }

    /// <summary>
    /// Testet den Planer für den Checkerboard-Versuch.
    ///
    /// Geprüft wird: Kommt bei gleichem Seed wirklich dieselbe Reihenfolge heraus?
    /// Kommt jede Kombination gleich oft dran? Und merkt der Planer, wenn eine
    /// Kombination aus l und FOV gar nicht geht?
    /// </summary>
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
                Assert.That(second[index].ConditionIndex, Is.EqualTo(first[index].ConditionIndex));
                Assert.That(second[index].Repetition, Is.EqualTo(first[index].Repetition));
                Assert.That(second[index].VisualSpaceL, Is.EqualTo(first[index].VisualSpaceL));
                Assert.That(second[index].EyePresentation, Is.EqualTo(first[index].EyePresentation));
            }
        }

        [Test]
        public void AllInspectorCombinations_AppearEquallyOften()
        {
            IReadOnlyList<CheckerboardTrial> plan = CreatePlan(seed: 7);

            // So kommt die 16 zustande:
            // 2 FOV * 2 Augenmodi * 2 l-Werte * 2 Wiederholungen.
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
                    new[] { 90f }, new[] { CheckerboardEyePresentation.BothEyes }, new[] { 1.5f }, 1, 1));
        }

        [TestCase("pilot 01", "pilot_01")]
        [TestCase("../Tolga/Test", "Tolga_Test")]
        [TestCase("", "fallback")]
        public void SessionIdentifier_IsMadeFileSafe(string input, string expected)
        {
            Assert.That(ExperimentFilesBase.SanitizeIdentifier(input, "fallback"), Is.EqualTo(expected));
        }

        private static IReadOnlyList<CheckerboardTrial> CreatePlan(int seed)
        {
            var eyes = new[]
            {
                CheckerboardEyePresentation.BothEyes,
                CheckerboardEyePresentation.LeftEyeOnly
            };
            return CheckerboardTrialPlanner.CreateRandomizedPlan(
                new[] { 70f, 90f }, eyes, new[] { 0.5f, 1f }, 2, seed);
        }
    }
}
