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
                    if (trial.InstrumentDistortionK == l[index]) count++;
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
                Assert.That(second[index].InstrumentDistortionK, Is.EqualTo(first[index].InstrumentDistortionK));
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
        public void KOutsideConfiguredRange_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                CheckerboardTrialPlanner.CreateRandomizedPlan(
                    new[] { 60f }, new[] { CheckerboardEyePresentation.BothEyes }, new[] { 2.1f }, 1, 1));
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

    /// <summary>
    /// "Invert Checkerboard Each Trial" und die Live-Liste der Übungsantworten
    /// für den Experiment Monitor.
    /// </summary>
    public sealed class CheckerboardPolarityAndPracticeLogTests
    {
        [Test]
        public void Polarity_IsAlwaysNormalWhenTheOptionIsOff()
        {
            for (int index = 1; index <= 8; index++)
            {
                Assert.That(CheckerboardPolarity.InvertedForPresentation(false, index), Is.False);
                Assert.That(CheckerboardPolarity.InvertedForPractice(false, index), Is.False);
            }
        }

        [Test]
        public void Polarity_AlternatesPerShownTrialStartingNormal()
        {
            bool[] expected = { false, true, false, true, false, true };
            for (int index = 1; index <= expected.Length; index++)
                Assert.That(CheckerboardPolarity.InvertedForPresentation(true, index),
                    Is.EqualTo(expected[index - 1]), "Darbietung " + index);
        }

        [Test]
        public void PracticePolarity_FollowsNIINAndIsBalancedAgainstLowHighAlternation()
        {
            bool[] expected = { false, true, true, false, false, true, true, false };
            for (int index = 1; index <= expected.Length; index++)
                Assert.That(CheckerboardPolarity.InvertedForPractice(true, index),
                    Is.EqualTo(expected[index - 1]), "Übungstrial " + index);

            // 12 Übungstrials, niedrig und hoch abwechselnd: Jede Kombination aus
            // niedrig/hoch und normal/invertiert kommt gleich oft vor.
            var counts = new int[2, 2];
            for (int index = 1; index <= 12; index++)
            {
                int high = index % 2 == 0 ? 1 : 0;
                int inverted = CheckerboardPolarity.InvertedForPractice(true, index) ? 1 : 0;
                counts[high, inverted]++;
            }

            foreach (int count in counts)
                Assert.That(count, Is.EqualTo(3));
        }

        [TestCase(0.2f, 1.4f, 0f, CheckerboardCurvatureResponse.Concave)]
        [TestCase(1.2f, 1.4f, 0f, CheckerboardCurvatureResponse.Convex)]
        [TestCase(0.7f, 1.4f, 0f, CheckerboardCurvatureResponse.None)]
        [TestCase(1.8f, 1.2f, 0.2f, CheckerboardCurvatureResponse.Convex)]
        [TestCase(0.7f, 1.2f, 0.2f, CheckerboardCurvatureResponse.None)]
        public void CloserToExample_IsOnlyAHintAndEmptyWhenInTheMiddle(
            float k, float convexExample, float concaveExample, CheckerboardCurvatureResponse expected)
        {
            Assert.That(PracticeAnswerLog.CloserToExample(k, convexExample, concaveExample),
                Is.EqualTo(expected));
        }

        [Test]
        public void PracticeLog_NumbersAnswersSummarisesPerKAndCanBeCleared()
        {
            var log = new PracticeAnswerLog();
            log.Add("Part 1", 1.4f, CheckerboardCurvatureResponse.Concave, 0.8, CheckerboardCurvatureResponse.Convex);
            log.Add("Part 1", 0f, CheckerboardCurvatureResponse.Concave, 0.6, CheckerboardCurvatureResponse.Concave);
            log.Add("Part 1", 1.4f, CheckerboardCurvatureResponse.Concave, 0.7, CheckerboardCurvatureResponse.Convex);
            log.Add("Part 2", 0.4f, CheckerboardCurvatureResponse.None, 5.0, CheckerboardCurvatureResponse.None,
                "no answer");

            Assert.That(log.Answers.Count, Is.EqualTo(4));
            Assert.That(log.Answers[3].Number, Is.EqualTo(4));
            Assert.That(double.IsNaN(log.Answers[3].ResponseTimeSeconds), Is.True,
                "Ohne Antwort gibt es keine Antwortzeit.");
            Assert.That(log.Answers[0].ResponseTimeSeconds, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(log.SummaryByK(), Is.EqualTo(new[]
            {
                "k 0.00: Convex 0, Concave 1, no answer 0",
                "k 0.40: Convex 0, Concave 0, no answer 1",
                "k 1.40: Convex 0, Concave 2, no answer 0"
            }));

            log.Clear();
            Assert.That(log.Answers, Is.Empty);
            Assert.That(log.Add("Part 1", 0f, CheckerboardCurvatureResponse.Convex, 0.5,
                CheckerboardCurvatureResponse.Concave).Number, Is.EqualTo(1));
        }
    }
}
