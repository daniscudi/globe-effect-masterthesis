using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Tests
{
    public sealed class NaturalScenePanoramaMathTests
    {
        [TestCase(0)] [TestCase(0.5)] [TestCase(1)] [TestCase(1.4)] [TestCase(1.8)] [TestCase(2)]
        public void UnitMagnificationIsIdentity(double k)
        {
            Assert.That(NaturalScenePanoramaMath.TryObjectCoordinates(0.3, -0.2, 1, k, 1,
                out double x, out double y), Is.True);
            Assert.That(x, Is.EqualTo(0.3).Within(1e-10));
            Assert.That(y, Is.EqualTo(-0.2).Within(1e-10));
        }

        [TestCase(1)] [TestCase(10)] [TestCase(20)]
        public void TangentConditionIsLinearZoom(double m)
        {
            NaturalScenePanoramaMath.TryObjectCoordinates(0.3, -0.2, m, 1, 2, out double x, out double y);
            Assert.That(x, Is.EqualTo(0.3 / (m * 2)).Within(1e-10));
            Assert.That(y, Is.EqualTo(-0.2 / (m * 2)).Within(1e-10));
        }

        [TestCase(0)] [TestCase(0.5)] [TestCase(1)] [TestCase(1.4)] [TestCase(1.8)] [TestCase(2)]
        public void InverseAgreesWithForwardMapping(double k)
        {
            const double r = 0.4;
            NaturalScenePanoramaMath.TryObjectCoordinates(r, 0, 10, k, 1, out double x, out _);
            double a = MerlitzBinocularReferenceMath.ApparentAngleFromObject(Math.Atan(x), 10, k);
            Assert.That(Math.Tan(a), Is.EqualTo(r).Within(1e-10));
        }

        [Test]
        public void ExtendedRangeKeepsTangentDomainSafe()
        {
            Assert.That(NaturalScenePanoramaMath.TryObjectCoordinates(0.4, 0, 10, 2, 1,
                out _, out _), Is.True);
            Assert.That(NaturalScenePanoramaMath.TryObjectCoordinates(0.4, 0, 10, 2.01, 1,
                out _, out _), Is.False);
            double maximumFov = NaturalScenePanoramaMath.MaximumFieldOfViewDegrees(2, 1);
            Assert.That(maximumFov, Is.InRange(89.0, 90.0));
            double outsideRadius = Math.Tan((maximumFov + 0.01) * Math.PI / 360);
            Assert.That(NaturalScenePanoramaMath.TryObjectCoordinates(outsideRadius, 0, 10, 2, 1,
                out _, out _), Is.False);
        }

        [Test]
        public void CenterIsFiniteAndAmbiguousDomainIsRejected()
        {
            Assert.That(NaturalScenePanoramaMath.TryObjectCoordinates(0, 0, 10, 1, 1,
                out double x, out double y), Is.True);
            Assert.That(x + y, Is.EqualTo(0));
            Assert.That(NaturalScenePanoramaMath.TryObjectCoordinates(10, 0, 10, 1.4, 1,
                out _, out _), Is.False);
        }

        [Test]
        public void AssetsAndShaderAreAvailable()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/GlobeEffect/Demo/NaturalScenePreview.unity"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/GlobeEffect/Demo/NaturalScene/suburban_field_01.jpg"), Is.Not.Null);
            var shader = Resources.Load<Shader>("GlobeEffectNaturalPanorama");
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
