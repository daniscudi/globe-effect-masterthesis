using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class NaturalScenePhotoPlayTests
    {
        [Test]
        public void PhotoHasFullResolutionAndClampSampling()
        {
            const string path = "Assets/GlobeEffect/Demo/NaturalPhoto/Puno_cityscape.jpg";
            var photo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(photo, Is.Not.Null);
            Assert.That(photo.width, Is.EqualTo(6000));
            Assert.That(photo.height, Is.EqualTo(4000));
            Assert.That(photo.wrapModeU, Is.EqualTo(TextureWrapMode.Clamp));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [UnityTest]
        public IEnumerator PhotoPreviewRendersAtTenTimesAndTracksHeadWithoutParallax()
        {
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode("Assets/GlobeEffect/Demo/NaturalPhotoPreview.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null;
            var preview = Object.FindAnyObjectByType<NaturalScenePanoramaPreview>();
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.usePerspectivePhoto, Is.True);
            Assert.That(preview.magnification, Is.EqualTo(10));
            Assert.That(preview.motionMode, Is.EqualTo(RandomDots.RandomDotMotionMode.HeadTracked));
            Assert.That(preview.PhotoEdgeVisible, Is.False);
            Assert.That(preview.PhotoVerticalFieldOfView, Is.InRange(26f, 28f));
            preview.showFixationTarget = false;
            yield return null;
            Color[] initial = Render(preview, "NaturalPhoto_m10_k1.png");
            float min = 1, max = 0;
            foreach (Color pixel in initial)
            {
                min = Mathf.Min(min, pixel.grayscale);
                max = Mathf.Max(max, pixel.grayscale);
            }
            Assert.That(max - min, Is.GreaterThan(0.2f), "Photo must contain visible detail.");

            preview.distortionK = 0.5f;
            yield return null;
            Assert.That(Difference(initial, Render(preview, "NaturalPhoto_m10_k05.png")), Is.GreaterThan(0.002f));
            preview.distortionK = 1;
            var camera = preview.observerCamera;
            camera.transform.rotation = Quaternion.Euler(0, 10, 0);
            yield return null;
            Assert.That(preview.PhotoEdgeVisible, Is.False);
            Color[] turned = Render(preview, "NaturalPhoto_m10_lookRight.png");
            Assert.That(Difference(initial, turned), Is.GreaterThan(0.01f));

            camera.transform.position += new Vector3(1, 0.5f, 0);
            yield return null;
            Assert.That(Difference(turned, Render(preview, null)), Is.LessThan(0.001f),
                "Translation must not create near parallax.");

            camera.transform.rotation = Quaternion.Euler(0, 90, 0);
            preview.showPhotoBoundaryWarning = false;
            yield return null;
            Assert.That(preview.PhotoEdgeVisible, Is.True);
            Color[] outside = Render(preview, "NaturalPhoto_outside.png");
            Color center = outside[256 * 512 + 256];
            Assert.That(center.r, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(center.g, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(center.b, Is.EqualTo(0.5f).Within(0.03f));

            preview.CenterPhoto();
            yield return null;
            Assert.That(preview.PhotoEdgeVisible, Is.False);
            Assert.That(Difference(initial, Render(preview, null)), Is.LessThan(0.002f));
            preview.enabled = false;
            yield return null;
            Assert.That(GameObject.Find("Natural Panorama Surface"), Is.Null);
            preview.enabled = true;
            yield return null;
            Assert.That(GameObject.Find("Natural Panorama Surface"), Is.Not.Null);
            yield return new ExitPlayMode();
        }

        private static Color[] Render(NaturalScenePanoramaPreview preview, string filename)
        {
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
                if (filename != null)
                {
                    System.IO.Directory.CreateDirectory("Library/CheckerboardAudit");
                    System.IO.File.WriteAllBytes("Library/CheckerboardAudit/" + filename, picture.EncodeToPNG());
                }
                return picture.GetPixels();
            }
            finally
            {
                preview.observerCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(picture);
            }
        }

        private static float Difference(Color[] a, Color[] b)
        {
            double total = 0;
            for (int i = 0; i < a.Length; i++)
                total += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
            return (float)(total / (a.Length * 3));
        }
    }
}
