using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Enthält dieselben Koordinaten und Schwenkgleichungen, die auch in den
    /// begleitenden Python-Plots verwendet werden. Dadurch rechnen Plot und
    /// Unity-Projekt nicht mit zwei unterschiedlichen Definitionen.
    ///
    /// Ein 3D-Punkt (X,Y,Z) wird zuerst durch die perspektivische Teilung zu
    /// x = X/Z und y = Y/Z. Mit der Vergrößerung m entsteht daraus der lineare
    /// Bildpunkt u = m*x und v = m*y. Erst danach wird verglichen, wie Schön
    /// oder Merlitz denselben Punkt in Winkelkoordinaten umrechnen.
    /// </summary>
    public static class GlobeEffectCoordinateMapping2D
    {
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
        /// Wendet die Merlitz-Gleichung auf einen zweidimensionalen Punkt an.
        /// Die Richtung von der Bildmitte zum Punkt bleibt gleich. Verändert
        /// wird nur sein Abstand zur Mitte. Bei k = 1 ist das Ergebnis genau
        /// der normale lineare Bildpunkt (u,v) = m(x,y).
        /// </summary>
        public static Vector2 ObjectToMerlitzInstrumentImage(
            Vector2 objectGnomonic,
            double magnification,
            double k)
        {
            double objectRadius = Math.Sqrt(
                objectGnomonic.x * objectGnomonic.x +
                objectGnomonic.y * objectGnomonic.y);

            if (objectRadius <= MinimumMagnitude)
            {
                return Vector2.zero;
            }

            double objectAngle = Math.Atan(objectRadius);
            double apparentAngle = MerlitzCheckerboardMath.ApparentAngleFromObject(
                objectAngle,
                magnification,
                k);

            if (apparentAngle >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(objectGnomonic),
                    "Die Abbildung liegt außerhalb des frontalen planaren Bildraums.");
            }

            double imageRadius = Math.Tan(apparentAngle);
            double scale = imageRadius / objectRadius;
            return new Vector2(
                (float)(scale * objectGnomonic.x),
                (float)(scale * objectGnomonic.y));
        }

        /// <summary>
        /// Berechnet die neue lineare Bildposition eines ruhenden fernen Punktes,
        /// nachdem die Kamera horizontal um psi gedreht wurde. Die Ausgangsposition
        /// bei psi = 0 wird bereits als (u,v) übergeben.
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

            if (Math.Abs(denominator) <= MinimumMagnitude)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(panRadians),
                    "Der Punkt liegt für diesen Schwenkwinkel in der Projektionsebene.");
            }

            double x = (objectAtZero.x * cos - sin) / denominator;
            double y = objectAtZero.y / denominator;
            return ObjectToLinearImage(
                new Vector2((float)x, (float)y),
                magnification);
        }

        /// <summary>
        /// Berechnet, wie schnell und in welche Richtung sich der lineare
        /// Bildpunkt in diesem Moment pro Radiant Kameradrehung bewegt.
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
        /// Schöns Regel wird getrennt auf die horizontale und vertikale Achse
        /// angewendet: (atan(u), atan(v)). Horizontal kennt dabei nur u und
        /// vertikal nur v. Das Ergebnis wird in Radiant zurückgegeben.
        /// </summary>
        public static Vector2 LinearImageToSchoenAngular(Vector2 linearImage)
        {
            return new Vector2(
                (float)Math.Atan(linearImage.x),
                (float)Math.Atan(linearImage.y));
        }

        /// <summary>
        /// Radiale Winkelkoordinaten für den Plotfall k = 1 und l = 0. Hier wird
        /// zuerst der gemeinsame Radius r aus u und v berechnet und anschließend
        /// mit atan(r) in einen Winkel umgerechnet. Die Richtung bleibt erhalten.
        /// Das ist die Plotdarstellung und nicht der einstellbare k-Regler des
        /// Checkerboard-Trials.
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
