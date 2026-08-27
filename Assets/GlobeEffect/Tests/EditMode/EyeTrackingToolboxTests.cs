using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class EyeTrackingToolboxTests
    {
        [Test]
        public void TransformRayToWorld_TransformsOriginAndDirection()
        {
            var referenceObject = new GameObject("Gaze reference");
            try
            {
                referenceObject.transform.SetPositionAndRotation(
                    new Vector3(1f, 2f, 3f),
                    Quaternion.Euler(0f, 90f, 0f));
                var localRay = new Ray(
                    new Vector3(0.03f, 0f, 0.02f),
                    Vector3.forward);

                Ray worldRay = EyeTrackingToolbox.TransformRayToWorld(
                    localRay,
                    referenceObject.transform);

                Assert.That(worldRay.origin, Is.EqualTo(
                    referenceObject.transform.TransformPoint(localRay.origin)));
                Assert.That(Vector3.Angle(worldRay.direction, Vector3.right),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(referenceObject);
            }
        }

        [Test]
        public void TransformRayToWorld_NormalizesDirection()
        {
            var referenceObject = new GameObject("Gaze reference");
            try
            {
                Ray worldRay = EyeTrackingToolbox.TransformRayToWorld(
                    new Ray(Vector3.zero, new Vector3(0f, 0f, 5f)),
                    referenceObject.transform);

                Assert.That(worldRay.direction.magnitude, Is.EqualTo(1f)
                    .Within(1e-6f));
            }
            finally
            {
                Object.DestroyImmediate(referenceObject);
            }
        }
    }
}
