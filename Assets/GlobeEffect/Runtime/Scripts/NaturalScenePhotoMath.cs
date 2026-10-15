using UnityEngine;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>Perspective source photograph projected from viewing directions, not a nearby 3D plane.</summary>
    public static class NaturalScenePhotoMath
    {
        public static bool TryPhotoUv(Vector3 ray, Vector2 tangentHalfSize, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (ray.z <= 0.00001f || tangentHalfSize.x <= 0 || tangentHalfSize.y <= 0) return false;
            uv = new Vector2(0.5f + 0.5f * ray.x / (ray.z * tangentHalfSize.x),
                0.5f + 0.5f * ray.y / (ray.z * tangentHalfSize.y));
            return uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1;
        }
    }
}
