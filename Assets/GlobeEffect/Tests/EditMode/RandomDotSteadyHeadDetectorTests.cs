using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Erkennung "Kopf dreht gleichmäßig" und Einblenden der Punkte, ohne Headset.
    /// </summary>
    public sealed class RandomDotSteadyHeadDetectorTests
    {
        private const float FrameSeconds = 1f / 90f;
        private const float Tolerance = 0.25f;
        private const float MinimumMoving = 0.5f;

        [Test]
        public void ConstantSpeed_CountsAsSteadyAtAnySpeed()
        {
            foreach (float speed in new[] { 1f, 2.5f, 8f })
            {
                var detector = new RandomDotSteadyHeadDetector();
                Feed(detector, speed, seconds: 0.5f);
                Assert.That(detector.SteadySeconds, Is.EqualTo(0.5f).Within(0.02f), $"{speed} °/s");
                Assert.That(detector.ReferenceSpeed, Is.EqualTo(speed).Within(0.001f));
            }
        }

        [Test]
        public void StillHead_IsNeverSteady()
        {
            var detector = new RandomDotSteadyHeadDetector();
            Feed(detector, 0.2f, seconds: 2f);
            Assert.That(detector.IsMoving, Is.False);
            Assert.That(detector.SteadySeconds, Is.Zero);
            Assert.That(float.IsNaN(detector.ReferenceSpeed), Is.True);
        }

        [Test]
        public void SpeedJump_StartsCountingAgain()
        {
            var detector = new RandomDotSteadyHeadDetector();
            Feed(detector, 2f, seconds: 0.5f);
            // +50 % ist mehr als ±25 %: neuer Abschnitt mit neuem Bezugstempo.
            Feed(detector, 3f, seconds: 0.1f);
            Assert.That(detector.SteadySeconds, Is.EqualTo(0.1f).Within(0.02f));
            Assert.That(detector.ReferenceSpeed, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void SmallWobble_StaysSteady()
        {
            var detector = new RandomDotSteadyHeadDetector();
            // ±10 % Schwankung um 2 °/s bleibt innerhalb von ±25 %.
            for (int frame = 0; frame < 90; frame++)
                detector.Add(frame % 2 == 0 ? 2.2f : 1.8f, FrameSeconds, Tolerance, MinimumMoving);
            Assert.That(detector.SteadySeconds, Is.EqualTo(1f).Within(0.02f));
            Assert.That(detector.ReferenceSpeed, Is.EqualTo(2f).Within(0.05f));
        }

        [Test]
        public void FadeIn_RisesFromZeroToOneAndZeroMeansNoFade()
        {
            Assert.That(RandomDotFieldStimulus.FadeInOpacity(0d, 0.05f), Is.EqualTo(0f));
            Assert.That(RandomDotFieldStimulus.FadeInOpacity(0.025d, 0.05f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(RandomDotFieldStimulus.FadeInOpacity(0.2d, 0.05f), Is.EqualTo(1f));
            Assert.That(RandomDotFieldStimulus.FadeInOpacity(0d, 0f), Is.EqualTo(1f), "0 = kein Einblenden");
        }

        private static void Feed(RandomDotSteadyHeadDetector detector, float speed, float seconds)
        {
            int frames = UnityEngine.Mathf.RoundToInt(seconds / FrameSeconds);
            for (int frame = 0; frame < frames; frame++)
                detector.Add(speed, FrameSeconds, Tolerance, MinimumMoving);
        }
    }
}
