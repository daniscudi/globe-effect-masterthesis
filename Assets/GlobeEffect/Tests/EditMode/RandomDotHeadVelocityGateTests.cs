using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Das Messinstrument der Geschwindigkeitserkennung, ohne Headset und ohne Play Mode.
    /// </summary>
    public sealed class RandomDotHeadVelocityGateTests
    {
        private const float FrameSeconds = 1f / 90f;

        [Test]
        public void Angle_CountsOnlyTheChosenAxisWithSign()
        {
            Vector3 ahead = Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, 10f, 0f) * ahead;
            Vector3 up = Quaternion.Euler(-5f, 0f, 0f) * ahead;

            Assert.That(RandomDotHeadVelocityGate.SignedAngleOnAxis(ahead, right, HeadVelocityAxis.Horizontal),
                Is.EqualTo(10f).Within(0.01f));
            Assert.That(RandomDotHeadVelocityGate.SignedAngleOnAxis(right, ahead, HeadVelocityAxis.Horizontal),
                Is.EqualTo(-10f).Within(0.01f));
            // Nicken zählt horizontal nicht, vertikal schon.
            Assert.That(RandomDotHeadVelocityGate.SignedAngleOnAxis(ahead, up, HeadVelocityAxis.Horizontal),
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(RandomDotHeadVelocityGate.SignedAngleOnAxis(ahead, up, HeadVelocityAxis.Vertical),
                Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void SteadySweep_IsMeasuredAndCountsAsInRange()
        {
            var gate = new RandomDotHeadVelocityGate();
            gate.Reset();
            Feed(gate, degreesPerSecond: 2f, seconds: 1f, minimum: 1f, maximum: 3f);

            Assert.That(gate.SignedSpeedDegreesPerSecond, Is.EqualTo(2f).Within(0.05f));
            // Die Glättung braucht kurz zum Einschwingen, danach liegt alles im Bereich.
            Assert.That(gate.SecondsInRange, Is.GreaterThan(0.85f));
            Assert.That(gate.SecondsOutOfRange, Is.Zero);
        }

        [Test]
        public void Jitter_AveragesOutBecauseTheSignIsKept()
        {
            // Abwechselnd 0,05° nach links und rechts pro Frame: Momentan wären das
            // 4,5 °/s, im Mittel aber keine Bewegung. Das darf nicht als Schwenk zählen.
            var gate = new RandomDotHeadVelocityGate();
            gate.Reset();
            Vector3 forward = Vector3.forward;
            for (int frame = 0; frame < 90; frame++)
            {
                float yaw = frame % 2 == 0 ? 0.05f : 0f;
                gate.AddSample(Quaternion.Euler(0f, yaw, 0f) * forward, FrameSeconds,
                    HeadVelocityAxis.Horizontal, 0.1f, 1f, 3f);
            }

            Assert.That(gate.SpeedDegreesPerSecond, Is.LessThan(1f));
            Assert.That(gate.SecondsInRange, Is.Zero);
        }

        [Test]
        public void Summary_SeparatesWaitingFromThePresentation()
        {
            var gate = new RandomDotHeadVelocityGate();
            gate.Reset();
            Feed(gate, degreesPerSecond: 2f, seconds: 0.5f, minimum: 1f, maximum: 3f);
            gate.BeginPresentation();
            Feed(gate, degreesPerSecond: 2f, seconds: 0.8f, minimum: 1f, maximum: 3f, startYaw: 1f, firstFrame: 1);

            HeadVelocitySummary summary = gate.Summarize(targetSpeed: 2f);
            Assert.That(summary.TargetSpeed, Is.EqualTo(2f));
            Assert.That(summary.WaitSeconds, Is.EqualTo(0.5f).Within(2f * FrameSeconds));
            Assert.That(summary.OnsetSpeed, Is.EqualTo(2f).Within(0.1f));
            Assert.That(summary.MeanSpeed, Is.EqualTo(2f).Within(0.05f));
            Assert.That(summary.MinSpeed, Is.LessThanOrEqualTo(summary.MeanSpeed));
            Assert.That(summary.MaxSpeed, Is.GreaterThanOrEqualTo(summary.MeanSpeed));

            gate.Reset();
            Assert.That(float.IsNaN(gate.Summarize(2f).OnsetSpeed), Is.True);
            Assert.That(float.IsNaN(HeadVelocitySummary.None.MeanSpeed), Is.True);
        }

        private static void Feed(RandomDotHeadVelocityGate gate, float degreesPerSecond, float seconds,
            float minimum, float maximum, float startYaw = 0f, int firstFrame = 0)
        {
            int frames = Mathf.RoundToInt(seconds / FrameSeconds);
            for (int frame = firstFrame; frame <= frames; frame++)
            {
                float yaw = startYaw + degreesPerSecond * frame * FrameSeconds;
                gate.AddSample(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, FrameSeconds,
                    HeadVelocityAxis.Horizontal, 0.1f, minimum, maximum);
            }
        }
    }
}
