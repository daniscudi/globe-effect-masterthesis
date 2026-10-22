using System;
using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardPincushionExtensionTests
    {
        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        [TestCase(2.0)]
        public void InverseAndForward_RecoverTheViewingAngle(double kappa)
        {
            foreach (double m in new[] { 1.0, 10.0, 20.0 })
                foreach (double a in new[] { -0.5, 0.0, 0.2, 0.7 })
                {
                    double objectAngle = CheckerboardPincushionExtension.ObjectAngleFromApparent(a, m, kappa);
                    Assert.That(CheckerboardPincushionExtension.ApparentAngleFromObject(objectAngle, m, kappa),
                        Is.EqualTo(a).Within(1e-10));
                }
        }

        [TestCase(0.0, -0.01886460494)]
        [TestCase(-1.0, -0.03611393874)]
        [TestCase(-2.0, -0.08002010664)]
        public void FixedSourceGrid_MatchesIndependentlyCalculatedReference(double setting, double deviation)
        {
            Assert.That(CheckerboardInstrumentMapping.SourceRadius(0.5, 70, 10, setting, true) - 0.5,
                Is.EqualTo(deviation).Within(1e-10));
        }

        [Test]
        public void Extension_JoinsContinuouslyAtZeroAndKeepsPositiveBranchUnchanged()
        {
            double zero = CheckerboardInstrumentMapping.SourceRadius(0.6, 60, 10, 0);
            Assert.That(CheckerboardInstrumentMapping.SourceRadius(0.6, 60, 10, -1e-7, true),
                Is.EqualTo(zero).Within(1e-12));
            foreach (double k in new[] { 0.0, 0.2, 0.5, 1.0, 1.4, 2.0 })
                Assert.That(CheckerboardInstrumentMapping.SourceRadius(0.6, 60, 10, k, true),
                    Is.EqualTo(CheckerboardInstrumentMapping.SourceRadius(0.6, 60, 10, k)));
        }

        [Test]
        public void M1_RemainsIdentityAlsoInCustomBranch()
        {
            Assert.That(CheckerboardInstrumentMapping.SourceRadius(0.6, 60, 1, -2, true),
                Is.EqualTo(0.6).Within(1e-12));
        }

        [Test]
        public void ForwardMapping_RejectsItsLimitedObjectDomain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CheckerboardPincushionExtension.ApparentAngleFromObject(0.5, 10, 2));
        }

        [Test]
        public void NegativeTrials_RequireExplicitOptInAndPreserveItWhenRepeated()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CheckerboardTrialPlanner.CreateRandomizedPlan(new[] { 60f },
                    new[] { CheckerboardEyePresentation.BothEyes }, new[] { -1f }, 1, 1));
            var plan = CheckerboardTrialPlanner.CreateRandomizedPlan(new[] { 60f },
                new[] { CheckerboardEyePresentation.BothEyes }, new[] { -2f, 0f, 1f }, 1, 1,
                allowPincushionExtension: true);
            foreach (CheckerboardTrial trial in plan)
            {
                Assert.That(trial.AllowPincushionExtension, Is.True);
                Assert.That(trial.CreateRepeatedAttempt().AllowPincushionExtension, Is.True);
            }
            Assert.That(CheckerboardInstrumentMapping.FamilyFor(-1), Is.EqualTo("custom_tanh"));
            Assert.That(CheckerboardInstrumentMapping.FamilyFor(0), Is.EqualTo("merlitz_tan"));
        }
    }
}
