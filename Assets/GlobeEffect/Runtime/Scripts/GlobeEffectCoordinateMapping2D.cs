using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Gemeinsame 2D-Koordinatenbasis fuer die Schwenk-Plots und spaetere
    /// dynamische Globe-Effect-Stimuli.
    ///
    /// Objektseitig gelten die gnomonischen Richtungskoordinaten
    /// x = X/Z und y = Y/Z. Der lineare Bildraum ist u = m*x, v = m*y.
    /// Dieser Raum ist die gemeinsame Eingabe, bevor Schoens separable oder
    /// Merlitz' radiale Winkelabbildung angewendet wird.
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
        /// Instrumentelle Merlitz-Abbildung eines objektseitigen 2D-Strahls.
        /// Bei k=1 ist das Ergebnis exakt der lineare Bildraum (u,v)=m(x,y).
        /// Fuer andere k bleibt der Azimut erhalten und nur der Radius aendert
        /// sich nach tan(k*a)=m*tan(k*A).
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
                    "Die Abbildung liegt ausserhalb des frontalen planaren Bildraums.");
            }

            double imageRadius = Math.Tan(apparentAngle);
            double scale = imageRadius / objectRadius;
            return new Vector2(
                (float)(scale * objectGnomonic.x),
                (float)(scale * objectGnomonic.y));
        }

        /// <summary>
        /// Lineare Bahn eines ruhenden fernen Punktes bei horizontalem
        /// Kameraschwenk psi. Die Eingabe bei psi=0 liegt bereits in (u,v).
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
                    "Der Punkt liegt fuer diesen Schwenkwinkel in der Projektionsebene.");
            }

            double x = (objectAtZero.x * cos - sin) / denominator;
            double y = objectAtZero.y / denominator;
            return ObjectToLinearImage(
                new Vector2((float)x, (float)y),
                magnification);
        }

        /// <summary>
        /// Momentane Ableitung d(u,v)/d(psi) im linearen Bildraum.
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
        /// Separable 2D-Fortsetzung der Schoen-Regel: (atan(u), atan(v)).
        /// Die Rueckgabe besteht aus Winkelkomponenten in Radiant.
        /// </summary>
        public static Vector2 LinearImageToSchoenAngular(Vector2 linearImage)
        {
            return new Vector2(
                (float)Math.Atan(linearImage.x),
                (float)Math.Atan(linearImage.y));
        }

        /// <summary>
        /// Radiale Merlitz-Winkelkoordinaten fuer den Plotfall k=1, l=0:
        /// q = atan(r)/r * (u,v). Diese nachgeschaltete Winkelabbildung ist
        /// nicht mit dem einstellbaren Instrumentparameter k zu verwechseln.
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
                    "Die Vergroesserung muss positiv sein.");
            }
        }
    }
}
