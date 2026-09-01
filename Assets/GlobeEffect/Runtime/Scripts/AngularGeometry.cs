using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Rechnet zwischen der sichtbaren Winkelgröße und der echten Größe der
    /// ebenen Checkerboard-Fläche um. Im Unity-Projekt werden alle Längen in
    /// Metern angegeben.
    /// </summary>
    public static class AngularGeometry
    {
        private const double DegreesToRadians = Math.PI / 180.0;
        private const double RadiansToDegrees = 180.0 / Math.PI;

        /// <summary>
        /// Berechnet, welchen echten Durchmesser die Fläche bei einem bestimmten
        /// Abstand und Winkeldurchmesser haben muss:
        ///
        ///     D = 2 d tan(theta / 2)
        ///
        /// Deshalb wird die Fläche bei doppeltem Abstand auch doppelt so groß.
        /// Für die Versuchsperson bleibt die sichtbare Winkelgröße trotzdem gleich.
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
        /// Macht die Rechnung in die andere Richtung: Aus Abstand und echtem
        /// Durchmesser wird bestimmt, wie groß die Fläche als Winkel erscheint.
        /// Diese Methode wird vor allem für Tests und Kontrollen verwendet.
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
