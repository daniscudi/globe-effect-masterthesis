using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Varjo.XR;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Sichert die committeten XR-Einstellungen ab, mit denen derselbe Stand auf
    /// dem Varjo-PC und auf dem Vive-PC läuft. XR Plug-in Management versucht die
    /// Loader in Listenreihenfolge; der erste, der startet, gewinnt.
    ///
    /// Schlägt ein Test hier fehl, wurde vermutlich in Project Settings -> XR
    /// Plug-in Management ein Häkchen umgeschaltet. Unity sortiert die Loader
    /// dabei alphabetisch neu (OpenXR vor Varjo).
    /// </summary>
    public sealed class XrLoaderSetupTests
    {
        [Test]
        public void StandaloneLoaders_TryVarjoFirstAndOpenXrSecond()
        {
            XRGeneralSettings settings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                    BuildTargetGroup.Standalone);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.InitManagerOnStart, Is.True);
            Assert.That(settings.Manager, Is.Not.Null);

            IReadOnlyList<XRLoader> loaders = settings.Manager.activeLoaders;
            Assert.That(loaders.Count, Is.EqualTo(2));
            Assert.That(loaders[0], Is.InstanceOf<VarjoLoader>());
            Assert.That(loaders[1], Is.InstanceOf<OpenXRLoader>());
        }

        [Test]
        public void OpenXr_UsesMultiPassLikeTheVarjoSetup()
        {
            OpenXRSettings settings =
                OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.renderMode, Is.EqualTo(OpenXRSettings.RenderMode.MultiPass));
        }

        [Test]
        public void OpenXr_HasTheViveControllerProfileEnabled()
        {
            OpenXRSettings settings =
                OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);

            Assert.That(settings, Is.Not.Null);
            HTCViveControllerProfile profile = settings.GetFeature<HTCViveControllerProfile>();
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.enabled, Is.True);
        }
    }
}
