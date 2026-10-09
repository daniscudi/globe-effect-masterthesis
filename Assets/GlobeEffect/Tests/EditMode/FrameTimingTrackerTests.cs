using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Ausgefallene Frames während einer Darbietung (Spalte slow_frames in der CSV).
    /// </summary>
    public sealed class FrameTimingTrackerTests
    {
        [Test]
        public void CountsFramesLongerThanOneAndAHalfRefreshIntervals()
        {
            // 90 Hz: 11,1 ms pro Bild, ab 16,7 ms gilt ein Frame als ausgefallen.
            var tracker = new FrameTimingTracker();
            tracker.Reset(1000f / 90f);
            tracker.Add(0.011f);
            tracker.Add(0.012f);
            tracker.Add(0.016f);
            tracker.Add(0.023f);

            Assert.That(tracker.Frames, Is.EqualTo(4));
            Assert.That(tracker.SlowFrames, Is.EqualTo(1));
            Assert.That(tracker.MaxFrameMilliseconds, Is.EqualTo(23f).Within(0.01f));
        }

        [Test]
        public void WithoutKnownRefreshRate_CountsNoSlowFrames()
        {
            var tracker = new FrameTimingTracker();
            tracker.Reset(float.NaN);
            tracker.Add(0.5f);
            Assert.That(tracker.Frames, Is.EqualTo(1));
            Assert.That(tracker.SlowFrames, Is.Zero);
        }

        [Test]
        public void Reset_StartsFreshForTheNextTrial()
        {
            var tracker = new FrameTimingTracker();
            tracker.Reset(10f);
            tracker.Add(0.05f);
            tracker.Reset(10f);
            Assert.That(tracker.Frames, Is.Zero);
            Assert.That(tracker.SlowFrames, Is.Zero);
            Assert.That(float.IsNaN(tracker.MaxFrameMilliseconds), Is.True);
        }
    }
}
