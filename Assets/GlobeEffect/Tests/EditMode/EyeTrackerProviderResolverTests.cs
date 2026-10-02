using System.Diagnostics;
using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;
using ETProvider = GlobeEffect.VRCheckerboard.EyeTracking.EyeTrackingToolbox.ETProvider;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft die Auswahl des Eye Trackers bei "Auto" und die Sperre, die eine
    /// Messung mit dem Dummy verhindert. Alles reine Logik ohne Hardware.
    /// </summary>
    public sealed class EyeTrackerProviderResolverTests
    {
        [Test]
        public void ProviderValues_KeepTheNumbersStoredInTheScenes()
        {
            // Die Szenen speichern den Provider als Zahl. Neue Werte dürfen deshalb
            // nur hinten angehängt werden.
            Assert.That((int)ETProvider.Dummy, Is.EqualTo(0));
            Assert.That((int)ETProvider.Varjo, Is.EqualTo(1));
            Assert.That((int)ETProvider.SRanipal, Is.EqualTo(2));
            Assert.That((int)ETProvider.Auto, Is.EqualTo(3));
        }

        [TestCase(true, true, ETProvider.Varjo)]
        [TestCase(true, false, ETProvider.Varjo)]
        [TestCase(false, true, ETProvider.SRanipal)]
        [TestCase(false, false, ETProvider.Dummy)]
        public void Resolve_AutoPrefersVarjoThenSRanipalThenDummy(
            bool varjoLoaderActive,
            bool sranipalRuntimeRunning,
            ETProvider expected)
        {
            Assert.That(
                EyeTrackerProviderResolver.Resolve(
                    ETProvider.Auto, varjoLoaderActive, sranipalRuntimeRunning),
                Is.EqualTo(expected));
        }

        [TestCase(ETProvider.Dummy)]
        [TestCase(ETProvider.Varjo)]
        [TestCase(ETProvider.SRanipal)]
        public void Resolve_KeepsAnExplicitlyConfiguredProvider(ETProvider configured)
        {
            Assert.That(EyeTrackerProviderResolver.Resolve(configured, true, true),
                Is.EqualTo(configured));
            Assert.That(EyeTrackerProviderResolver.Resolve(configured, false, false),
                Is.EqualTo(configured));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void DummyFallback_IsBlockedWithXrDeviceOrRequiredFixation(
            bool xrDeviceActive,
            bool requireFixation)
        {
            Assert.That(
                EyeTrackerProviderResolver.IsDummyFallbackBlocked(
                    ETProvider.Auto, ETProvider.Dummy, xrDeviceActive, requireFixation),
                Is.True);
        }

        [Test]
        public void DummyFallback_IsAllowedForAKeyboardTestWithoutHeadset()
        {
            Assert.That(
                EyeTrackerProviderResolver.IsDummyFallbackBlocked(
                    ETProvider.Auto, ETProvider.Dummy,
                    xrDeviceActive: false, requireFixation: false),
                Is.False);
        }

        [TestCase(ETProvider.Auto, ETProvider.Varjo)]
        [TestCase(ETProvider.Auto, ETProvider.SRanipal)]
        [TestCase(ETProvider.Varjo, ETProvider.Varjo)]
        [TestCase(ETProvider.SRanipal, ETProvider.SRanipal)]
        [TestCase(ETProvider.Dummy, ETProvider.Dummy)]
        public void SessionStart_IsNotBlockedWithARealOrExplicitlyChosenProvider(
            ETProvider configured,
            ETProvider active)
        {
            Assert.That(
                EyeTrackerProviderResolver.IsDummyFallbackBlocked(
                    configured, active, xrDeviceActive: true, requireFixation: true),
                Is.False);
        }

        [Test]
        public void TryGetSessionBlockReason_WithoutToolboxDoesNotBlock()
        {
            bool blocked = EyeTrackerProviderResolver.TryGetSessionBlockReason(
                null, requireFixation: true, out string reason);

            Assert.That(blocked, Is.False);
            Assert.That(reason, Is.Null);
        }

        [Test]
        public void BuildProviderMarker_NamesConfiguredAndActiveProvider()
        {
            string marker = EyeTrackerProviderResolver.BuildProviderMarker(
                ETProvider.Auto, ETProvider.SRanipal);

            Assert.That(marker, Does.StartWith(
                "EyeTrackingProvider;configured=Auto;active=SRanipal;xr_loader="));
            Assert.That(marker, Does.Not.Contain(","));
        }

        [Test]
        public void IsProcessRunning_FindsTheCurrentProcessButNoInventedOne()
        {
            // Dieselbe Abfrage erkennt später sr_runtime.exe. Hier wird sie mit dem
            // Prozess geprüft, der sicher läuft: dem Unity-Editor selbst.
            string ownProcessName;
            using (Process ownProcess = Process.GetCurrentProcess())
            {
                ownProcessName = ownProcess.ProcessName;
            }

            Assert.That(EyeTrackerProviderResolver.IsProcessRunning(ownProcessName), Is.True);
            Assert.That(
                EyeTrackerProviderResolver.IsProcessRunning("globe_effect_no_such_process"),
                Is.False);
        }
    }
}
