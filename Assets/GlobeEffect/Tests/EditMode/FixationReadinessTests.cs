using System.Reflection;
using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class FixationReadinessTests
    {
        [TestCase(0d, true)]
        [TestCase(10d, false)]
        public void PreviouslySteadyFixation_StartsOnlyWithAFreshSample(double sampleAge, bool expected)
        {
            var owner = new GameObject("FixationReadinessTest");
            owner.SetActive(false);
            try
            {
                var monitor = owner.AddComponent<CheckerboardFixationMonitor>();
                Set(monitor, "currentSampleValid", true);
                Set(monitor, "isInsideTolerance", true);
                Set(monitor, "continuousFixationSeconds", 1f);
                Set(monitor, "lastSampleRealtimeSeconds", Time.realtimeSinceStartupAsDouble - sampleAge);

                Assert.That(monitor.RequirementMet, Is.True, "Das bisherige Kriterium ist erfüllt.");
                Assert.That(monitor.IsReadyForPresentation(0.1f), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void Set(CheckerboardFixationMonitor monitor, string name, object value)
        {
            typeof(FixationMonitorBase<VrCheckerboardStimulus>)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(monitor, value);
        }
    }
}
