using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Belegte radiale Abbildung aus Merlitz (JOSA A 27, 50-57, 2010).
    ///
    /// Instrumentelle Abbildung:
    ///     tan(k a) = m tan(k A)
    ///
    /// A ist der objektseitige Winkel, a der scheinbare Winkel und m die
    /// paraxiale Vergrößerung. Der Grenzfall k = 0 ist a = m A.
    /// </summary>
    public static class MerlitzCheckerboardMath
    {
        private const double KLimitEpsilon = 1e-7;

        public static double ApparentAngleFromObject(
            double objectAngleRadians,
            double magnification,
            double k)
        {
            Validate(magnification, k);

            if (k < KLimitEpsilon)
            {
                return magnification * objectAngleRadians;
            }

            return Math.Atan(
                magnification * Math.Tan(k * objectAngleRadians)) / k;
        }

        public static double ObjectAngleFromApparent(
            double apparentAngleRadians,
            double magnification,
            double k)
        {
            Validate(magnification, k);

            if (k < KLimitEpsilon)
            {
                return apparentAngleRadians / magnification;
            }

            return Math.Atan(
                Math.Tan(k * apparentAngleRadians) / magnification) / k;
        }

        /// <summary>
        /// Inverse Abtastung für den Shader.
        ///
        /// Ein Radius r im dargestellten Kreis wird in den Radius s des
        /// ursprünglich regelmäßigen Wandgitters zurückgerechnet. Beide
        /// Radien sind auf den jeweiligen Kreisrand normiert.
        /// </summary>
        public static double NormalizedSourceRadius(
            double normalizedDisplayRadius,
            double apparentHalfAngleRadians,
            double magnification,
            double k)
        {
            if (normalizedDisplayRadius < 0.0 || normalizedDisplayRadius > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(normalizedDisplayRadius));
            }

            if (apparentHalfAngleRadians <= 0.0 ||
                apparentHalfAngleRadians >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(nameof(apparentHalfAngleRadians));
            }

            double tangentAtBoundary = Math.Tan(apparentHalfAngleRadians);
            double apparentAngle = Math.Atan(
                normalizedDisplayRadius * tangentAtBoundary);
            double objectAngle = ObjectAngleFromApparent(
                apparentAngle,
                magnification,
                k);
            double maximumObjectAngle = ObjectAngleFromApparent(
                apparentHalfAngleRadians,
                magnification,
                k);

            return Math.Tan(objectAngle) / Math.Tan(maximumObjectAngle);
        }

        private static void Validate(double magnification, double k)
        {
            if (magnification <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(magnification),
                    "Die Vergrößerung muss positiv sein.");
            }

            if (k < 0.0 || k > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(k),
                    "Merlitz parametrisiert k im Bereich 0 bis 1.");
            }
        }
    }
}
