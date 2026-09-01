using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class CheckerboardKeyboardControllerTests
    {
        [TestCase(CheckerboardCurvatureResponse.Concave)]
        [TestCase(CheckerboardCurvatureResponse.Convex)]
        public void SubmitResponse_RaisesSelectedAnswer(
            CheckerboardCurvatureResponse expected)
        {
            GameObject gameObject = new GameObject("Keyboard controller test");

            try
            {
                gameObject.AddComponent<VrCheckerboardStimulus>();
                CheckerboardKeyboardController controller =
                    gameObject.AddComponent<CheckerboardKeyboardController>();
                CheckerboardCurvatureResponse received =
                    CheckerboardCurvatureResponse.None;
                controller.ResponseSubmitted += response => received = response;

                controller.SubmitResponse(expected);

                Assert.That(received, Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SubmitNone_DoesNotRaiseAnswer()
        {
            GameObject gameObject = new GameObject("Keyboard controller test");

            try
            {
                gameObject.AddComponent<VrCheckerboardStimulus>();
                CheckerboardKeyboardController controller =
                    gameObject.AddComponent<CheckerboardKeyboardController>();
                int eventCount = 0;
                controller.ResponseSubmitted += _ => eventCount++;

                controller.SubmitResponse(CheckerboardCurvatureResponse.None);

                Assert.That(eventCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
