using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft das Verhalten des SRanipal-Providers auf einem Rechner ohne
    /// SRanipal-Runtime: eine Warnung, ungültige Blickdaten und keine Exception.
    /// Läuft die Runtime (Lab-PC), werden diese Tests übersprungen.
    /// </summary>
    public sealed class SRanipalEyeTrackerTests
    {
        private const string UnavailableWarning = "SRanipal Eye Tracking nicht verfügbar";

        private GameObject trackerObject;
        private SRanipalEyeTracker tracker;
        private int unavailableWarningCount;
        private int errorCount;

        [SetUp]
        public void SetUp()
        {
            Assume.That(EyeTrackerProviderResolver.IsSRanipalRuntimeRunning(), Is.False,
                "Die SRanipal-Runtime läuft; der Test gilt nur für Rechner ohne Runtime.");

            unavailableWarningCount = 0;
            errorCount = 0;
            Application.logMessageReceived += CountLogMessage;
            trackerObject = new GameObject("SRanipal eye tracker test");
            tracker = trackerObject.AddComponent<SRanipalEyeTracker>();
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CountLogMessage;
            if (trackerObject != null)
            {
                Object.DestroyImmediate(trackerObject);
            }
        }

        [Test]
        public void WithoutRuntime_WarnsExactlyOnce()
        {
            tracker.Initialize();
            tracker.Initialize();
            tracker.StartListening();
            tracker.StartListening();
            tracker.StopListening();

            Assert.That(unavailableWarningCount, Is.EqualTo(1));
            Assert.That(errorCount, Is.EqualTo(0));
        }

        [Test]
        public void WithoutRuntime_GazeDataStaysInvalid()
        {
            tracker.Initialize();
            tracker.StartListening();

            GazeData gaze = tracker.GetGazeData();

            Assert.That(gaze.combinedValidity, Is.False);
            Assert.That(gaze.leftValidity, Is.False);
            Assert.That(gaze.rightValidity, Is.False);
            Assert.That(gaze.leftPupilDiameter, Is.NaN);
        }

        [Test]
        public void WithoutRuntime_CalibrateDoesNotThrow()
        {
            tracker.Initialize();

            Assert.DoesNotThrow(() => tracker.Calibrate());
            Assert.That(errorCount, Is.EqualTo(0));
        }

        private void CountLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && condition.Contains(UnavailableWarning))
            {
                unavailableWarningCount++;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errorCount++;
            }
        }
    }
}
