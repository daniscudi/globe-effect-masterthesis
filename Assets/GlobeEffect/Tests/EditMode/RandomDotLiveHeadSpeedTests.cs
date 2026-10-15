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

        [Test]
        public void LeftRightPanning_IsSplitIntoTurnsWithTheirMeanSpeed()
        {
            // 2 s nach rechts mit 2 °/s, 2 s nach links mit 3 °/s, dann stillhalten.
            var live = new RandomDotLiveHeadSpeed();
            float yaw = Feed(live, degreesPerSecond: 2f, seconds: 2f);
            yaw = Feed(live, degreesPerSecond: -3f, seconds: 2f, startYaw: yaw);
            Feed(live, degreesPerSecond: 0f, seconds: 1f, startYaw: yaw);

            Assert.That(live.LastPans.Count, Is.EqualTo(2));
            // Der neueste zuerst: nach links. Anfahren und Abbremsen (Glättung) zählen
            // mit, deshalb liegt das Mittel etwas unter dem gleichmäßigen Tempo.
            Assert.That(live.LastPans[0].Direction, Is.EqualTo(-1));
            Assert.That(live.LastPans[0].MeanHeadSpeed, Is.EqualTo(3f).Within(0.4f));
            Assert.That(live.LastPans[0].MeanImageSpeed, Is.EqualTo(10f * live.LastPans[0].MeanHeadSpeed).Within(0.01f));
            Assert.That(live.LastPans[1].Direction, Is.EqualTo(1));
            Assert.That(live.LastPans[1].MeanHeadSpeed, Is.EqualTo(2f).Within(0.3f));
            Assert.That(live.LastPans[1].Seconds, Is.EqualTo(2f).Within(0.2f));
        }

        [Test]
        public void ShortTwitch_IsNoTurn()
        {
            var live = new RandomDotLiveHeadSpeed();
            float yaw = Feed(live, degreesPerSecond: 3f, seconds: 0.2f);
            Feed(live, degreesPerSecond: 0f, seconds: 1f, startYaw: yaw);

            Assert.That(live.LastPans, Is.Empty);
        }

        [Test]
        public void SaveSpeed_StoresTheLastSecondWhileMovingButNotWhileStill()
        {
            var live = new RandomDotLiveHeadSpeed();
            float yaw = Feed(live, degreesPerSecond: 2.5f, seconds: 4f);

            Assert.That(live.TrySaveSpeed(out RandomDotSavedSpeed saved), Is.True);
            Assert.That(saved.HeadSpeed, Is.EqualTo(2.5f).Within(0.1f));
            Assert.That(saved.ImageSpeed, Is.EqualTo(25f).Within(1f));
            Assert.That(live.SavedSpeeds.Count, Is.EqualTo(1));

            Feed(live, degreesPerSecond: 0f, seconds: 5f, startYaw: yaw);
            Assert.That(live.TrySaveSpeed(out _), Is.False);
            Assert.That(live.SavedSpeeds.Count, Is.EqualTo(1));

            live.ClearHistory();
            Assert.That(live.SavedSpeeds, Is.Empty);
            Assert.That(live.LastPans, Is.Empty);
        }

        // Gibt den Gierwinkel am Ende zurück, damit die nächste Bewegung dort weitergeht.
        private static float Feed(RandomDotLiveHeadSpeed live, float degreesPerSecond, float seconds,
            float startYaw = 0f)
        {
            int frames = Mathf.RoundToInt(seconds / FrameSeconds);
            float yaw = startYaw;
            for (int frame = 0; frame <= frames; frame++)
            {
                yaw = startYaw + degreesPerSecond * frame * FrameSeconds;
                live.AddSample(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, FrameSeconds,
                    HeadVelocityAxis.Horizontal, 0.1f, simulatedHeadSpeed: 2f, steadyTolerance: 0.25f,
                    minimumMovingSpeed: 0.5f, holdSecondsBeforeDots: 0.3f, imageGain: 10f);
            }
            return yaw;
        }
    }
}
