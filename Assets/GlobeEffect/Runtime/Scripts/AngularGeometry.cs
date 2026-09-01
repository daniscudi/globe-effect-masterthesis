using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Geometrie eines ebenen, frontal betrachteten Stimulus.
    /// Alle Längen verwenden dieselbe Einheit; im Unity-Projekt ist das Meter.
    /// </summary>
    public static class AngularGeometry
    {
        private const double DegreesToRadians = Math.PI / 180.0;
        private const double RadiansToDegrees = 180.0 / Math.PI;

        /// <summary>
        /// Berechnet den physischen Durchmesser D = 2 d tan(theta / 2).
        /// </summary>
        public static double PhysicalDiameter(
            double viewingDistance,
            double angularDiameterDegrees)
        {
            if (viewingDistance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(viewingDistance),
                    "Der Betrachtungsabstand muss positiv sein.");
            }

            if (angularDiameterDegrees <= 0.0 || angularDiameterDegrees >= 180.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(angularDiameterDegrees),
                    "Die Winkelgröße muss zwischen 0 und 180 Grad liegen.");
            }

            double halfAngleRadians = 0.5 * angularDiameterDegrees * DegreesToRadians;
            return 2.0 * viewingDistance * Math.Tan(halfAngleRadians);
        }

        /// <summary>
        /// Inverse Beziehung zu <see cref="PhysicalDiameter"/>.
        /// </summary>
        public static double AngularDiameterDegrees(
            double viewingDistance,
            double physicalDiameter)
        {
            if (viewingDistance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(viewingDistance));
            }

            if (physicalDiameter <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(physicalDiameter));
            }

            double halfAngle = Math.Atan(physicalDiameter / (2.0 * viewingDistance));
            return 2.0 * halfAngle * RadiansToDegrees;
        }
    }
}
