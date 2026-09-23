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
        public void Plan_ContainsEveryKAndMagnificationEquallyOften()
        {
            var plan = CreatePlan(7);

            // 2 FOV * 1 Auge * 2 k-Werte * 2 m-Werte * 1 Content Zoom *
            // 1 Bewegungsart * 4 Wiederholungen.
            Assert.That(plan.Count, Is.EqualTo(32));
            Assert.That(plan.Count(t => t.InstrumentDistortionK == 0.5f),
                Is.EqualTo(16));
            Assert.That(plan.Count(t => t.InstrumentDistortionK == 1f),
                Is.EqualTo(16));
            Assert.That(plan.Count(t => t.InstrumentMagnificationM == 4f),
                Is.EqualTo(16));
            Assert.That(plan.Count(t => t.InstrumentMagnificationM == 10f),
                Is.EqualTo(16));
            Assert.That(plan.All(t => t.AttemptNumber == 1), Is.True);
        }

        [Test]
        public void DirectionsAreBalancedAndSeedsAreMatchedAcrossL()
        {
            var plan = CreatePlan(17);

            foreach (var group in plan.GroupBy(t => new
                     {
                         t.AngularDiameterDegrees,
                         t.InstrumentDistortionK,
                         t.InstrumentMagnificationM
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
                foreach (float magnification in new[] { 4f, 10f })
                {
                    int[] helmholtzSeeds = plan
                        .Where(t => t.AngularDiameterDegrees == fov &&
                            t.InstrumentDistortionK == 0.5f &&
                            t.InstrumentMagnificationM == magnification)
                        .OrderBy(t => t.Repetition)
                        .Select(t => t.DotSeed)
                        .ToArray();
                    int[] straightSeeds = plan
                        .Where(t => t.AngularDiameterDegrees == fov &&
                            t.InstrumentDistortionK == 1f &&
                            t.InstrumentMagnificationM == magnification)
                        .OrderBy(t => t.Repetition)
                        .Select(t => t.DotSeed)
                        .ToArray();
                    Assert.That(straightSeeds, Is.EqualTo(helmholtzSeeds));
                }
            }
        }

        private static System.Collections.Generic.IReadOnlyList<RandomDotTrial>
            CreatePlan(int randomSeed)
        {
            return RandomDotTrialPlanner.CreateRandomizedPlan(
                new[] { 40f, 70f },
                new[] { CheckerboardEyePresentation.BothEyes },
                new[] { 0.5f, 1f },
                new[] { 4f, 10f },
                new[] { 1f },
                new[] { RandomDotMotionMode.SimulatedYaw },
                repetitions: 4,
                randomSeed,
                dotSeedBase: 5000);
        }
    }
}
