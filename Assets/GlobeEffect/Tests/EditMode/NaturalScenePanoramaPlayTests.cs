using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class NaturalScenePanoramaPlayTests
    {
        [UnityTest]
        public IEnumerator PreviewStartsAndUpdatesLive()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/GlobeEffect/Demo/NaturalScenePreview.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null;
            var preview = Object.FindFirstObjectByType<NaturalScenePanoramaPreview>();
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.enabled, Is.True);
            Assert.That(preview.observerCamera, Is.Not.Null);
            Assert.That(GameObject.Find("Natural Panorama Surface"), Is.Not.Null);
            var target = new RenderTexture(512, 512, 24);
            var picture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previousTarget = preview.observerCamera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                preview.observerCamera.targetTexture = target;
                preview.observerCamera.Render();
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                picture.Apply();
                float min = 1, max = 0;
                foreach (var pixel in picture.GetPixels())
                {
                    min = Mathf.Min(min, pixel.grayscale);
                    max = Mathf.Max(max, pixel.grayscale);
                }
                Assert.That(max - min, Is.GreaterThan(0.15f), "Panorama darf nicht leer sein.");
                System.IO.File.WriteAllBytes(
                    "Library/CheckerboardAudit/NaturalSceneRebuildPreview.png", picture.EncodeToPNG());
            }
            finally
            {
                preview.observerCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(picture);
            }
            preview.backgroundColor = Color.white;
            preview.magnification = 10;
            yield return null;
            Assert.That(preview.observerCamera.backgroundColor, Is.EqualTo(Color.white));
            preview.paused = true;
            yield return null;
            float sweep = preview.CurrentSweepDegrees;
            yield return null;
            Assert.That(preview.CurrentSweepDegrees, Is.EqualTo(sweep));
            preview.motionMode = RandomDots.RandomDotMotionMode.HeadTracked;
            yield return null;
            Assert.That(preview.CurrentSweepDegrees, Is.EqualTo(0));
            yield return new ExitPlayMode();
        }
    }
}
