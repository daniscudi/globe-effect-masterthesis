using System.Collections.Generic;
using System.Linq;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Testet den Planer für den Random-Dot-Versuch.
    ///
    /// Geprüft wird: Kommt bei gleichem Seed wirklich dieselbe Reihenfolge heraus?
    /// Kommt jede k-/m-Kombination gleich oft dran? Geht es ungefähr gleich oft
    /// nach links wie nach rechts los? Und haben gleiche Wiederholungen dieselben
    /// Punkte, damit die Punktverteilung nicht an einem bestimmten k oder m hängt?
    /// </summary>
    public sealed class RandomDotTrialPlannerTests
    {
        private static readonly RandomDotMotionMode[] BothModes =
        {
            RandomDotMotionMode.SimulatedYaw,
            RandomDotMotionMode.HeadTracked
        };

        [TestCase(0.2f, CheckerboardCurvatureResponse.Concave)]
        [TestCase(0.3f, CheckerboardCurvatureResponse.Concave)]
        [TestCase(0.7f, CheckerboardCurvatureResponse.None)]
        [TestCase(1f, CheckerboardCurvatureResponse.None)]
        [TestCase(1.1f, CheckerboardCurvatureResponse.Convex)]
        [TestCase(1.2f, CheckerboardCurvatureResponse.Convex)]
        public void TrainingFeedback_OnlyForClearExtremes(float k, CheckerboardCurvatureResponse expected)
        {
            // Zwischen den Grenzen gibt es keine "richtige" Antwort und keine Rückmeldung.
            Assert.That(RandomDotExperimentManager.ExpectedTrainingResponse(k, 0.3f, 1.1f), Is.EqualTo(expected));
        }

        [TestCase(RandomDotMotionMode.SimulatedYaw)]
        [TestCase(RandomDotMotionMode.HeadTracked)]
        public void SingleMotionSession_Has175TrialsInOneWholeBlock(RandomDotMotionMode mode)
        {
            var plan = RandomDotTrialPlanner.CreateSingleMotionPlan(new[] { 60f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.2f, 0.4f, 0.6f, 0.8f, 1f, 1.2f, 1.4f },
                new[] { 10f }, new[] { 1f }, mode, 25, 23, 100);
            Assert.That(plan.Count, Is.EqualTo(175));
            Assert.That(plan.All(t => t.MotionMode == mode && t.MotionBlockIndex == 1
                && t.MiniBlockIndex == 1), Is.True);
            Assert.That(plan.GroupBy(t => t.InstrumentDistortionK).All(group => group.Count() == 25), Is.True);
        }

        [Test]
        public void SameSeed_ProducesSameOrderAndDotSeeds()
        {
            var first = CreatePlan(1234);
            var second = CreatePlan(1234);

            Assert.That(first.Count, Is.EqualTo(second.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(first[index].ConditionIndex, Is.EqualTo(second[index].ConditionIndex));
                Assert.That(first[index].DotSeed, Is.EqualTo(second[index].DotSeed));
                Assert.That(first[index].SweepDirection, Is.EqualTo(second[index].SweepDirection));
                Assert.That(first[index].SequenceIndex, Is.EqualTo(index + 1));
            }
        }

        [Test]
        public void Plan_ContainsEveryKAndMagnificationEquallyOften()
        {
            var plan = CreatePlan(7);

            // 2 FOV * 1 Auge * 2 k-Werte * 2 m-Werte * 1 Content Zoom *
            // 1 Bewegungsart * 4 Wiederholungen.
            Assert.That(plan.Count, Is.EqualTo(32));
            Assert.That(plan.Count(t => t.InstrumentDistortionK == 0.5f), Is.EqualTo(16));
            Assert.That(plan.Count(t => t.InstrumentDistortionK == 1f), Is.EqualTo(16));
            Assert.That(plan.Count(t => t.InstrumentMagnificationM == 4f), Is.EqualTo(16));
            Assert.That(plan.Count(t => t.InstrumentMagnificationM == 10f), Is.EqualTo(16));
            Assert.That(plan.All(t => t.AttemptNumber == 1), Is.True);
        }

        [Test]
        public void DirectionsAreBalancedAndSeedsAreMatchedAcrossL()
        {
            var plan = CreatePlan(17);

            var groups = plan.GroupBy(t =>
                new { t.AngularDiameterDegrees, t.InstrumentDistortionK, t.InstrumentMagnificationM });
            foreach (var group in groups)
            {
                int leftFirst = group.Count(t => t.SweepDirection == RandomDotSweepDirection.LeftFirst);
                int rightFirst = group.Count(t => t.SweepDirection == RandomDotSweepDirection.RightFirst);
                Assert.That(leftFirst, Is.EqualTo(2));
                Assert.That(rightFirst, Is.EqualTo(2));
            }

            foreach (float fov in new[] { 40f, 70f })
            {
                foreach (float magnification in new[] { 4f, 10f })
                {
                    var sameView = plan.Where(t =>
                        t.AngularDiameterDegrees == fov && t.InstrumentMagnificationM == magnification);
                    int[] helmholtzSeeds = DotSeeds(sameView.Where(t => t.InstrumentDistortionK == 0.5f));
                    int[] straightSeeds = DotSeeds(sameView.Where(t => t.InstrumentDistortionK == 1f));
                    Assert.That(straightSeeds, Is.EqualTo(helmholtzSeeds));
                }
            }
        }

        [Test]
        public void MotionModesStayInSeparateBlocksAndMiniBlocksStayComplete()
        {
            var plan = CreateSimplePlan(
                new[] { 0.5f, 1f }, BothModes, repetitions: 4, perMiniBlock: 2, seed: 23);

            Assert.That(plan.Count, Is.EqualTo(16));
            Assert.That(plan.Take(8).All(t =>
                t.MotionMode == RandomDotMotionMode.SimulatedYaw && t.MotionBlockIndex == 1), Is.True);
            Assert.That(plan.Skip(8).All(t =>
                t.MotionMode == RandomDotMotionMode.HeadTracked && t.MotionBlockIndex == 2), Is.True);
            var miniBlocks = plan.GroupBy(t =>
                new { t.MotionBlockIndex, t.MiniBlockIndex, t.InstrumentDistortionK });
            Assert.That(miniBlocks.All(group => group.Count() == 2), Is.True);

            var simulated = plan.Where(t => t.MotionMode == RandomDotMotionMode.SimulatedYaw);
            var headTracked = plan.Where(t => t.MotionMode == RandomDotMotionMode.HeadTracked);
            Assert.That(DotSeeds(headTracked), Is.EqualTo(DotSeeds(simulated)));
        }

        [Test]
        public void VerticalAxisOnlyAffectsSimulatedBlockAndSurvivesRepeat()
        {
            var plan = CreateSimplePlan(
                new[] { 0.5f }, BothModes, repetitions: 1, perMiniBlock: 1, seed: 23,
                simulatedSweepAxis: RandomDotSweepAxis.Vertical);

            Assert.That(plan[0].SweepAxis, Is.EqualTo(RandomDotSweepAxis.Vertical));
            Assert.That(plan[1].SweepAxis, Is.EqualTo(RandomDotSweepAxis.Horizontal));
            Assert.That(plan[0].CreateRepeatedAttempt().SweepAxis, Is.EqualTo(RandomDotSweepAxis.Vertical));
        }

        [Test]
        public void InvalidTrialRepeatStaysInsideItsMiniBlock()
        {
            var plan = CreateSimplePlan(new[] { 0.5f, 1f }, new[] { RandomDotMotionMode.SimulatedYaw },
                repetitions: 2, perMiniBlock: 1, seed: 31);
            var queue = new RandomDotTrialQueue(plan);

            Assert.That(queue.TryTakeNext(out RandomDotTrial invalid), Is.True);
            RandomDotTrial repeat = queue.AppendRepeatedAttempt(invalid);

            Assert.That(queue.TryTakeNext(out _), Is.True);
            Assert.That(queue.TryTakeNext(out RandomDotTrial inserted), Is.True);
            Assert.That(inserted, Is.SameAs(repeat));
            Assert.That(inserted.MiniBlockIndex, Is.EqualTo(1));
            Assert.That(queue.TryPeekNext(out RandomDotTrial next), Is.True);
            Assert.That(next.MiniBlockIndex, Is.EqualTo(2));
        }

        [Test]
        public void Planner_AcceptsMagnificationTwentyAndRejectsMore()
        {
            Assert.That(CreatePlanWithMagnification(20f).Count, Is.EqualTo(1));
            Assert.That(CreatePlanWithMagnification(20f)[0].InstrumentMagnificationM, Is.EqualTo(20f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CreatePlanWithMagnification(20.5f));
        }

        private static IReadOnlyList<RandomDotTrial> CreatePlanWithMagnification(float magnificationM)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 70f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.5f },
                new[] { magnificationM },
                new[] { 1f },
                new[] { RandomDotMotionMode.SimulatedYaw },
                repetitions: 1,
                repetitionsPerMiniBlock: 1,
                randomSeed: 1,
                dotSeedBase: 5000);
        }

        private static IReadOnlyList<RandomDotTrial> CreatePlan(int randomSeed)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 40f, 70f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.5f, 1f },
                new[] { 4f, 10f },
                new[] { 1f },
                new[] { RandomDotMotionMode.SimulatedYaw },
                repetitions: 4,
                repetitionsPerMiniBlock: 2,
                randomSeed,
                dotSeedBase: 5000);
        }

        // Ein kleiner Plan mit festen Werten: FOV 90, beide Augen, m = 10, kein
        // Zusatzzoom. Nur k, Bewegungsarten, Wiederholungen und Seed ändern sich.
        private static IReadOnlyList<RandomDotTrial> CreateSimplePlan(
            float[] kValues, RandomDotMotionMode[] motionModes, int repetitions, int perMiniBlock, int seed,
            RandomDotSweepAxis simulatedSweepAxis = RandomDotSweepAxis.Horizontal)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 90f },
                new[] { CheckerboardEyePresentation.BothEyes },
                kValues,
                new[] { 10f },
                new[] { 1f },
                motionModes,
                repetitions,
                perMiniBlock,
                seed,
                dotSeedBase: 5000,
                simulatedSweepAxis);
        }

        // Die Punkt-Seeds der Durchgänge, sortiert nach k und dann nach Wiederholung.
        private static int[] DotSeeds(IEnumerable<RandomDotTrial> trials)
        {
            return trials
                .OrderBy(t => t.InstrumentDistortionK)
                .ThenBy(t => t.Repetition)
                .Select(t => t.DotSeed)
                .ToArray();
        }
    }
}
