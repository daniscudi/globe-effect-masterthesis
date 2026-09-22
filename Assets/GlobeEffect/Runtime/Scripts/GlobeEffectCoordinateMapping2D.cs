using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Die Umrechnungen für die Vergleichsplots.
    ///
    /// Genau dieselben Formeln stecken auch in den Python-Plots. Dadurch rechnen
    /// die Plots und das Unity-Projekt nicht aus Versehen verschieden.
    ///
    /// Der Weg ist immer gleich: Ein Punkt im Raum (X,Y,Z) wird erst durch Z
    /// geteilt, das gibt x = X/Z und y = Y/Z. Mit der Vergrößerung m kommt man
    /// dann auf den Bildpunkt u = m*x und v = m*y. Erst danach wird verglichen,
    /// was Schön und was Merlitz aus demselben Punkt machen.
    /// </summary>
    public static class GlobeEffectCoordinateMapping2D
    {
        // Kleiner als das behandeln wir als null. Sonst würde gleich durch
        // fast nichts geteilt und die Zahlen würden explodieren.
        private const double MinimumMagnitude = 1e-12;

        public static Vector2 ObjectToLinearImage(
            Vector2 objectGnomonic,
            double magnification)
        {
            ValidateMagnification(magnification);
            return new Vector2(
                (float)(magnification * objectGnomonic.x),
                (float)(magnification * objectGnomonic.y));
        }

        public static Vector2 LinearImageToObject(
            Vector2 linearImage,
            double magnification)
        {
            ValidateMagnification(magnification);
            return new Vector2(
                (float)(linearImage.x / magnification),
                (float)(linearImage.y / magnification));
        }

        /// <summary>
        /// Wendet die Merlitz-Formel auf einen Punkt im Bild an.
        ///
        /// In welche Richtung der Punkt von der Mitte aus liegt, bleibt gleich.
        /// Es ändert sich nur, wie weit er von der Mitte weg ist.
        ///
        /// Bei k = 1 kommt genau der normale Bildpunkt (u,v) = m(x,y) heraus.
        /// </summary>
        public static Vector2 ObjectToMerlitzInstrumentImage(
            Vector2 objectGnomonic,
            double magnification,
            double k)
        {
            // Wie weit ist der Punkt von der Mitte weg?
            double objectRadius = Math.Sqrt(
                objectGnomonic.x * objectGnomonic.x +
                objectGnomonic.y * objectGnomonic.y);

            // Genau in der Mitte gibt es keine Richtung, also bleibt er da.
            if (objectRadius <= MinimumMagnitude)
            {
                return Vector2.zero;
            }

            double objectAngle = Math.Atan(objectRadius);
            double apparentAngle = MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                objectAngle,
                magnification,
                k);

            // Ab 90 Grad läge der Punkt seitlich oder hinter einem. Auf einer
            // flachen Bildfläche lässt sich das nicht mehr darstellen.
            if (apparentAngle >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(objectGnomonic),
                    "Dieser Punkt liegt zu weit außen und passt nicht mehr aufs flache Bild.");
            }

            // Der Punkt wird auf seiner Linie zur Mitte nur nach außen oder innen
            // geschoben. Deshalb reicht ein gemeinsamer Faktor für x und y.
            double imageRadius = Math.Tan(apparentAngle);
            double scale = imageRadius / objectRadius;
            return new Vector2(
                (float)(scale * objectGnomonic.x),
                (float)(scale * objectGnomonic.y));
        }

        /// <summary>
        /// Ein weit entfernter Punkt steht still, die Kamera dreht sich um psi
        /// zur Seite. Wo liegt der Punkt danach im Bild?
        ///
        /// Wo er vor der Drehung lag, wird schon als (u,v) übergeben.
        /// </summary>
        public static Vector2 LinearImageAfterHorizontalPan(
            Vector2 linearImageAtZero,
            double panRadians,
            double magnification)
        {
            Vector2 objectAtZero = LinearImageToObject(
                linearImageAtZero,
                magnification);
            double sin = Math.Sin(panRadians);
            double cos = Math.Cos(panRadians);
            double denominator = objectAtZero.x * sin + cos;

            // Wird der Nenner null, liegt der Punkt nach der Drehung genau seitlich.
            // Dann gibt es keine Stelle im Bild mehr für ihn.
            if (Math.Abs(denominator) <= MinimumMagnitude)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(panRadians),
                    "Bei diesem Drehwinkel liegt der Punkt genau seitlich und hat keine Bildposition mehr.");
            }

            double x = (objectAtZero.x * cos - sin) / denominator;
            double y = objectAtZero.y / denominator;
            return ObjectToLinearImage(
                new Vector2((float)x, (float)y),
                magnification);
        }

        /// <summary>
        /// Wie schnell und in welche Richtung wandert ein Bildpunkt gerade,
        /// wenn sich die Kamera dreht? Der Wert gilt pro Radiant Drehung.
        /// </summary>
        public static Vector2 LinearImageVelocityForHorizontalPan(
            Vector2 linearImage,
            double magnification)
        {
            ValidateMagnification(magnification);
            double u = linearImage.x;
            double v = linearImage.y;
            return new Vector2(
                (float)(-(magnification + u * u / magnification)),
                (float)(-u * v / magnification));
        }

        /// <summary>
        /// Die Regel von Schön: atan wird einzeln auf u und auf v angewendet.
        ///
        /// Waagerecht zählt also nur u und senkrecht nur v. Die beiden Achsen
        /// wissen nichts voneinander. Zurück kommt der Winkel im Bogenmaß.
        /// </summary>
        public static Vector2 LinearImageToSchoenAngular(Vector2 linearImage)
        {
            return new Vector2(
                (float)Math.Atan(linearImage.x),
                (float)Math.Atan(linearImage.y));
        }

        /// <summary>
        /// Dasselbe, aber nach Merlitz: Hier wird erst der gemeinsame Abstand r
        /// aus u und v gebildet und dann atan(r) gerechnet. Die Richtung bleibt.
        ///
        /// Das gilt für den Plotfall k = 1 und l = 0. Es hat nichts mit dem
        /// k-Regler im Checkerboard-Versuch zu tun.
        /// </summary>
        public static Vector2 LinearImageToMerlitzAngular(Vector2 linearImage)
        {
            double radius = Math.Sqrt(
                linearImage.x * linearImage.x +
                linearImage.y * linearImage.y);

            if (radius <= MinimumMagnitude)
            {
                return Vector2.zero;
            }

            double scale = Math.Atan(radius) / radius;
            return new Vector2(
                (float)(scale * linearImage.x),
                (float)(scale * linearImage.y));
        }

        private static void ValidateMagnification(double magnification)
        {
            if (magnification <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(magnification),
                    "Die Vergrößerung muss positiv sein.");
            }
        }
    }
}
