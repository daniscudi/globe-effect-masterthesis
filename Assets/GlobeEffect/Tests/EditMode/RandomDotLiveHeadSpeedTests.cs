using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Die Live-Anzeige des Kopftempos im Experiment Monitor, ohne Headset und ohne Play Mode.
    /// </summary>
    public sealed class RandomDotLiveHeadSpeedTests
    {
        private const float FrameSeconds = 1f / 90f;

        [Test]
        public void SteadyHead_ShowsImageSpeedAndWouldShowDots()
        {
            // m = 10: 2 °/s Kopf ergeben 20 °/s in der Bildmitte.
            var live = new RandomDotLiveHeadSpeed();
            Feed(live, degreesPerSecond: 2f, seconds: 3f);

            Assert.That(live.HeadSpeed, Is.EqualTo(2f).Within(0.05f));
            Assert.That(live.AverageHeadSpeed, Is.EqualTo(2f).Within(0.15f));
            Assert.That(live.ImageCenterSpeed, Is.EqualTo(20f).Within(0.5f));
            Assert.That(live.FramesPerSecond, Is.EqualTo(90f).Within(0.5f));
            // 20 °/s bei 90 Bildern pro Sekunde: gut 0,22° pro Frame.
            Assert.That(live.ImageStepPerFrameDegrees, Is.EqualTo(20f / 90f).Within(0.01f));
            Assert.That(live.SimulatedHeadSpeed, Is.EqualTo(2f).Within(0.001f));
            Assert.That(live.SteadySeconds, Is.GreaterThan(2f));
            Assert.That(live.WouldShowDots, Is.True);
        }

        [Test]
        public void AnySteadySpeedCounts_ButAStillHeadDoesNot()
        {
            // Kein Zieltempo: auch dreimal so schnell wie der simulierte Schwenk zählt.
            var fast = new RandomDotLiveHeadSpeed();
            Feed(fast, degreesPerSecond: 6f, seconds: 1f);
            Assert.That(fast.WouldShowDots, Is.True);

            var still = new RandomDotLiveHeadSpeed();
            Feed(still, degreesPerSecond: 0.3f, seconds: 1f);
            Assert.That(still.IsMoving, Is.False);
            Assert.That(still.WouldShowDots, Is.False);
        }

        [Test]
        public void Reset_ClearsTheDisplay()
        {
            var live = new RandomDotLiveHeadSpeed();
            Feed(live, degreesPerSecond: 2f, seconds: 1f);
            live.Reset();

            Assert.That(live.HasData, Is.False);
            Assert.That(live.HeadSpeed, Is.EqualTo(0f));
            Assert.That(live.WouldShowDots, Is.False);
        }

        private static void Feed(RandomDotLiveHeadSpeed live, float degreesPerSecond, float seconds)
        {
            int frames = Mathf.RoundToInt(seconds / FrameSeconds);
            for (int frame = 0; frame <= frames; frame++)
            {
                float yaw = degreesPerSecond * frame * FrameSeconds;
                live.AddSample(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, FrameSeconds,
                    HeadVelocityAxis.Horizontal, 0.1f, simulatedHeadSpeed: 2f, steadyTolerance: 0.25f,
                    minimumMovingSpeed: 0.5f, holdSecondsBeforeDots: 0.3f, imageGain: 10f);
            }
        }
    }
}
