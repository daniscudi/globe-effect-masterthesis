using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardKeyboardControllerTests
    {
        [Test]
        public void ChangeK_ClampsToDocumentedRange()
        {
            GameObject gameObject = new GameObject("Keyboard controller test");

            try
            {
                VrCheckerboardStimulus stimulus =
                    gameObject.AddComponent<VrCheckerboardStimulus>();
                CheckerboardKeyboardController controller =
                    gameObject.AddComponent<CheckerboardKeyboardController>();

                stimulus.SetMerlitzK(0.99f);
                controller.ChangeK(0.05f);
                Assert.That(stimulus.MerlitzK, Is.EqualTo(1f));

                stimulus.SetMerlitzK(0.01f);
                controller.ChangeK(-0.05f);
                Assert.That(stimulus.MerlitzK, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Recenter_PlacesStimulusOnCurrentViewAxis()
        {
            GameObject observerObject = new GameObject("Observer");
            GameObject stimulusObject = new GameObject("Stimulus");

            try
            {
                observerObject.transform.SetPositionAndRotation(
                    new Vector3(1f, 1.7f, -2f),
                    Quaternion.Euler(8f, 25f, 0f));

                VrCheckerboardStimulus stimulus =
                    stimulusObject.AddComponent<VrCheckerboardStimulus>();
                stimulus.Observer = observerObject.transform;
                stimulus.SetGeometry(70f, 1.5f);

                CheckerboardKeyboardController controller =
                    stimulusObject.AddComponent<CheckerboardKeyboardController>();
                stimulusObject.transform.position = Vector3.zero;
                controller.Recenter();

                Vector3 expectedPosition = observerObject.transform.position +
                    observerObject.transform.forward * 1.5f;
                Assert.That(
                    Vector3.Distance(stimulusObject.transform.position, expectedPosition),
                    Is.LessThan(1e-5f));
                Assert.That(
                    Vector3.Angle(stimulusObject.transform.forward,
                        observerObject.transform.forward),
                    Is.LessThan(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(stimulusObject);
                Object.DestroyImmediate(observerObject);
            }
        }
    }
}
