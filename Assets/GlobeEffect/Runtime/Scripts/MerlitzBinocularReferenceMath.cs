using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Die Fernglas-Formel von Merlitz.
    ///
    /// Der Random-Dot-Versuch benutzt dieselbe Gleichung im Shader. Diese C#-
    /// Fassung bleibt die prüfbare Referenz für Vorwärts- und Rückrechnungen.
    /// Das statische Checkerboard arbeitet weiterhin mit seiner normierten
    /// Visual-Space-Abbildung.
    ///
    /// Die Gleichung steht bei Merlitz (JOSA A 27, 50-57, 2010):
    ///
    ///     tan(k a) = m tan(k A)
    ///
    /// A ist der Winkel, unter dem ein Punkt in Wirklichkeit liegt. a ist der
    /// Winkel, unter dem man ihn durch das Fernglas sieht. m ist die Vergrößerung
    /// in der Mitte des Bildes. k sagt, wie sich das Ganze zum Rand hin verhält.
    /// Bei k = 0 bleibt einfach a = m A übrig.
    ///
    /// Hier steht nur die Mathematik. Wie so ein Fernglas später aussehen und sich
    /// bewegen würde, käme in den Stimulus.
    /// </summary>
    public static class MerlitzBinocularReferenceMath
    {
        private const double AlmostZero = 1e-7;
        public const double MinimumDistortionK = 0.0;
        public const double MaximumDistortionK = 1.4;

        /// <summary>
        /// Der Weg vorwärts: Ein Punkt liegt in Wirklichkeit beim Winkel A.
        /// Unter welchem Winkel a sieht man ihn durch das Fernglas?
        ///
        /// Diese Richtung braucht man für die Vergleichsrechnungen und später
        /// vielleicht für einen eigenen Fernglasmodus beim Random-Dot-Test.
        /// </summary>
        public static double ApparentAngleFromObject(
            double objectAngleRadians,
            double magnification,
            double k)
        {
            Validate(magnification, k);

            // Durch k teilen geht bei k = 0 nicht. Der Wert hier ist genau das,
            // was herauskommt, wenn k immer kleiner wird.
            if (k < AlmostZero)
            {
                return magnification * objectAngleRadians;
            }

            return Math.Atan(
                magnification * Math.Tan(k * objectAngleRadians)) / k;
        }

        /// <summary>
        /// Der Weg rückwärts: Man sieht einen Punkt unter dem Winkel a.
        /// Wo liegt er in Wirklichkeit, also bei welchem Winkel A?
        ///
        /// Diese Richtung wird für die Vergleichsrechnungen und die Plots benutzt.
        /// </summary>
        public static double ObjectAngleFromApparent(
            double apparentAngleRadians,
            double magnification,
            double k)
        {
            Validate(magnification, k);

            if (k < AlmostZero)
            {
                return apparentAngleRadians / magnification;
            }

            return Math.Atan(
                Math.Tan(k * apparentAngleRadians) / magnification) / k;
        }

        /// <summary>
        /// Rechnet eine Stelle im fertigen Bild zurück auf das gerade Ausgangsmuster.
        ///
        /// normalizedDisplayRadius sagt, wo im Kreis man ist: 0 ist die Mitte,
        /// 1 ist der Rand. Zurück kommt die passende Stelle im geraden Schachbrett,
        /// auch wieder von 0 bis 1. Der Shader schaut dann dort nach, ob das Feld
        /// schwarz oder weiß ist.
        ///
        /// Am Ende wird durch den Wert am Rand geteilt. Dadurch bleibt der Rand bei
        /// jedem k an derselben Stelle. k ändert also nur die Form der Linien, aber
        /// nicht, wie groß der Kreis ist.
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

            // Schritt 1: Von der Stelle im Kreis auf den Winkel kommen, unter dem
            // man diesen Punkt sieht. Der halbe Winkel ist der eingestellte Rand.
            double tangentAtBoundary = Math.Tan(apparentHalfAngleRadians);
            double apparentAngle = Math.Atan(
                normalizedDisplayRadius * tangentAtBoundary);

            // Schritt 2: Mit der Formel rückwärts nachschauen, von welchem
            // wirklichen Winkel dieser gesehene Winkel kommt.
            double objectAngle = ObjectAngleFromApparent(
                apparentAngle,
                magnification,
                k);

            // Schritt 3: Dasselbe noch einmal für den äußeren Rand. Durch das
            // Teilen landet das Ergebnis wieder sauber zwischen 0 und 1.
            double maximumObjectAngle = ObjectAngleFromApparent(
                apparentHalfAngleRadians,
                magnification,
                k);

            return Math.Tan(objectAngle) / Math.Tan(maximumObjectAngle);
        }

        private static void Validate(double magnification, double k)
        {
            // Falsche Werte würden hier nur Unsinn ergeben. Besser gleich meckern,
            // als später komische Bilder zu suchen.
            if (magnification <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(magnification),
                    "Die Vergrößerung muss positiv sein.");
            }

            if (k < MinimumDistortionK || k > MaximumDistortionK)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(k),
                    "k muss zwischen 0 und 1,4 liegen. Der Bereich über 1 " +
                    "setzt die Abbildungsfamilie in die tonnenförmige " +
                    "Richtung fort.");
            }
        }
    }
}
