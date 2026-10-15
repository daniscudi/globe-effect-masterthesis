using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Bewegung neben der Schwenkachse und Schwenkrichtung bei aktiven Trials, ohne Headset.
    /// </summary>
    public sealed class RandomDotHeadOffAxisMeterTests
    {
        private const float FrameSeconds = 1f / 90f;

        [Test]
        public void CleanHorizontalSweep_HasNoOffAxisMovement()
        {
            var meter = new RandomDotHeadOffAxisMeter();
            Feed(meter, yawPerSecond: 2.5f, pitchPerSecond: 0f, rollPerSecond: 0f, seconds: 0.8f);
            Assert.That(meter.MaxOffAxisDeviationDegrees, Is.EqualTo(0f).Within(0.01f));
            Assert.That(meter.MeanOffAxisSpeed, Is.EqualTo(0f).Within(0.01f));
            Assert.That(meter.MaxRollDeviationDegrees, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void DiagonalSweepWithTilt_IsMeasured()
        {
            // Schräg: 2,5 °/s zur Seite, dabei 1 °/s nach oben und 2 °/s seitlich gekippt.
            var meter = new RandomDotHeadOffAxisMeter();
            Feed(meter, yawPerSecond: 2.5f, pitchPerSecond: 1f, rollPerSecond: 2f, seconds: 0.8f);
            Assert.That(meter.MeanOffAxisSpeed, Is.EqualTo(1f).Within(0.05f));
            Assert.That(meter.MaxOffAxisDeviationDegrees, Is.EqualTo(0.8f).Within(0.05f));
            Assert.That(meter.MaxRollDeviationDegrees, Is.EqualTo(1.6f).Within(0.1f));
        }

        [Test]
        public void Direction_FollowsTheSignOfTheSpeed()
        {
            Assert.That(RandomDotHeadOffAxisMeter.Direction(2f, HeadVelocityAxis.Horizontal), Is.EqualTo("Right"));
            Assert.That(RandomDotHeadOffAxisMeter.Direction(-2f, HeadVelocityAxis.Horizontal), Is.EqualTo("Left"));
            Assert.That(RandomDotHeadOffAxisMeter.Direction(2f, HeadVelocityAxis.Vertical), Is.EqualTo("Up"));
            Assert.That(RandomDotHeadOffAxisMeter.Direction(-2f, HeadVelocityAxis.Vertical), Is.EqualTo("Down"));
        }

        [Test]
        public void WithoutBegin_NothingIsMeasured()
        {
            var meter = new RandomDotHeadOffAxisMeter();
            meter.Add(Vector3.forward, Vector3.up, FrameSeconds);
            Assert.That(float.IsNaN(meter.MeanOffAxisSpeed), Is.True);
            Assert.That(float.IsNaN(meter.MaxOffAxisDeviationDegrees), Is.True);
        }

        private static void Feed(RandomDotHeadOffAxisMeter meter, float yawPerSecond, float pitchPerSecond,
            float rollPerSecond, float seconds)
        {
            int frames = Mathf.RoundToInt(seconds / FrameSeconds);
            for (int frame = 0; frame <= frames; frame++)
            {
                float t = frame * FrameSeconds;
                // Unity: negative X-Drehung hebt den Blick, Z-Drehung kippt den Kopf zur Seite.
                Quaternion head = Quaternion.Euler(-pitchPerSecond * t, yawPerSecond * t, rollPerSecond * t);
                Vector3 forward = head * Vector3.forward;
                Vector3 up = head * Vector3.up;
                if (frame == 0) meter.Begin(forward, up, HeadVelocityAxis.Horizontal);
                else meter.Add(forward, up, FrameSeconds);
            }
        }
    }
}
