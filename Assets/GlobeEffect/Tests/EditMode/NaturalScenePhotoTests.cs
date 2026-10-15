using NUnit.Framework;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class NaturalScenePhotoTests
    {
        [Test]
        public void CentralRayMapsToImageCenter()
        {
            Assert.That(NaturalScenePhotoMath.TryPhotoUv(Vector3.forward, new Vector2(0.358f, 0.239f),
                out Vector2 uv), Is.True);
            Assert.That(uv, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [TestCase(-1f, 0f)] [TestCase(1f, 1f)]
        public void HorizontalEdgesMapToTextureEdges(float sign, float expected)
        {
            Assert.That(NaturalScenePhotoMath.TryPhotoUv(new Vector3(sign * 0.358f, 0, 1),
                new Vector2(0.358f, 0.239f), out Vector2 uv), Is.True);
            Assert.That(uv.x, Is.EqualTo(expected).Within(1e-6));
            Assert.That(uv.y, Is.EqualTo(0.5f));
        }

        [TestCase(1f, 0f, 1f)] [TestCase(0f, 1f, 1f)] [TestCase(0f, 0f, -1f)]
        public void OutsideAndBehindPhotoAreRejected(float x, float y, float z)
        {
            Assert.That(NaturalScenePhotoMath.TryPhotoUv(new Vector3(x, y, z),
                new Vector2(0.358f, 0.239f), out _), Is.False);
        }

        [Test]
        public void RayLengthDoesNotChangeCoordinates()
        {
            var halfSize = new Vector2(0.358f, 0.239f);
            NaturalScenePhotoMath.TryPhotoUv(new Vector3(0.1f, -0.1f, 1), halfSize, out Vector2 a);
            NaturalScenePhotoMath.TryPhotoUv(new Vector3(1, -1, 10), halfSize, out Vector2 b);
            Assert.That(Vector2.Distance(a, b), Is.LessThan(1e-6));
        }
    }
}
