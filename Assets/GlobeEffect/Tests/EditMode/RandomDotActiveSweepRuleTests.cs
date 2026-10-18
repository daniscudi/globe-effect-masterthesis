using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Die Kopftempo-Regel der aktiven Trials, ohne Headset und ohne Play Mode:
    /// ein Schwellwert, Tempo in Schwenkrichtung (mit Vorzeichen).
    /// </summary>
    public sealed class RandomDotActiveSweepRuleTests
    {
        private const float FrameSeconds = 1f / 90f;

        private RandomDotActiveSweepRule rule;
        private float yaw;

        [SetUp]
        public void SetUp()
        {
            rule = new RandomDotActiveSweepRule { StillBelow = 1f, SmoothingSeconds = 0.1f, AlternateDirection = true };
            rule.BeginWait();
            yaw = 0f;
        }

        [Test]
        public void StillHead_ShowsNoDots_AnyTurnAboveTheThresholdDoes()
        {
            Turn(degreesPerSecond: 0.5f, seconds: 1f);
            Assert.That(rule.ReadyForDots, Is.False, "Unter dem Schwellwert gilt der Kopf als still.");

            // Erster Trial: Richtung egal, auch nach links. Kein Zieltempo, auch sehr schnell zählt.
            Turn(degreesPerSecond: -20f, seconds: 0.3f);
            Assert.That(rule.ReadyForDots, Is.True);
        }

        [Test]
        public void Alternating_OnlyTheOtherDirectionStartsTheNextTrial()
        {
            Turn(degreesPerSecond: 3f, seconds: 0.5f);
            Assert.That(rule.ReadyForDots, Is.True);
            rule.BeginPresentation(Head(), Vector3.up);
            Assert.That(rule.Direction, Is.EqualTo("Right"));
            Assert.That(rule.NextDirection, Is.EqualTo("Left"));

            rule.BeginWait();
            Turn(degreesPerSecond: 3f, seconds: 1f);
            Assert.That(rule.ReadyForDots, Is.False, "Noch einmal nach rechts zählt wie Stillhalten.");
            Turn(degreesPerSecond: -3f, seconds: 0.5f);
            Assert.That(rule.ReadyForDots, Is.True);
        }

        [Test]
        public void WhileDotsAreShown_SlowButMovingIsFine_StoppingStops()
        {
            Turn(degreesPerSecond: 2f, seconds: 0.5f);
            rule.BeginPresentation(Head(), Vector3.up);

            // Langsamer, aber noch über dem Schwellwert: kein Abbruch.
            Turn(degreesPerSecond: 1.3f, seconds: 0.5f, presenting: true);
            Assert.That(rule.HeadStopped, Is.False);

            Turn(degreesPerSecond: 0f, seconds: 0.3f, presenting: true);
            Assert.That(rule.HeadStopped, Is.True);
        }

        [Test]
        public void SuddenTurnBack_StopsEvenWhenTheSmoothedSpeedSkipsTheStillRange()
        {
            // Nach links schwenken, dann ruckartig nach rechts. Das geglättete Tempo
            // springt dabei in einem Frame über den Bereich ±1 °/s hinweg. Mit dem
            // Betrag würde das nie als "still" erkannt, mit Vorzeichen schon.
            Turn(degreesPerSecond: -5f, seconds: 0.5f);
            rule.BeginPresentation(Head(), Vector3.up);
            Assert.That(rule.Direction, Is.EqualTo("Left"));

            bool stopped = false;
            int frames = Mathf.RoundToInt(0.3f / FrameSeconds);
            for (int frame = 0; frame < frames && !stopped; frame++)
            {
                Step(degreesPerSecond: 60f, presenting: true);
                stopped = rule.HeadStopped;
            }
            Assert.That(stopped, Is.True);
        }

        private void Turn(float degreesPerSecond, float seconds, bool presenting = false)
        {
            int frames = Mathf.RoundToInt(seconds / FrameSeconds);
            for (int frame = 0; frame < frames; frame++) Step(degreesPerSecond, presenting);
        }

        private void Step(float degreesPerSecond, bool presenting)
        {
            yaw += degreesPerSecond * FrameSeconds;
            rule.Sample(Head(), Vector3.up, FrameSeconds, presenting);
        }

        private Vector3 Head() => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    }
}
