using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>Rückrechnung vom angezeigten Bildstrahl zum Panorama, ohne Wahrnehmungsmodell.</summary>
    public static class NaturalScenePanoramaMath
    {
        public const double TangentLimit = 1.560796;

        public static double MaximumFieldOfViewDegrees(double k, double zoom)
        {
            double rawLimit = k > 1.0 ? TangentLimit / k : TangentLimit;
            return 2.0 * Math.Atan(Math.Tan(rawLimit) * Math.Max(zoom, 0.01)) * 180.0 / Math.PI;
        }

        public static bool TryObjectCoordinates(
            double u, double v, double m, double k, double zoom, out double x, out double y)
        {
            x = y = 0;
            if (m <= 0 || zoom <= 0 || k < MerlitzBinocularReferenceMath.MinimumDistortionK
                || k > MerlitzBinocularReferenceMath.MaximumDistortionK) return false;
            double radius = Math.Sqrt(u * u + v * v);
            double angle = Math.Atan(radius / zoom);
            if (k * angle >= TangentLimit) return false;
            double sourceAngle = MerlitzBinocularReferenceMath.ObjectAngleFromApparent(angle, m, k);
            if (sourceAngle >= TangentLimit) return false;
            double scale = radius > 1e-9 ? Math.Tan(sourceAngle) / radius : 1.0 / (m * zoom);
            x = u * scale;
            y = v * scale;
            return true;
        }
    }
}
