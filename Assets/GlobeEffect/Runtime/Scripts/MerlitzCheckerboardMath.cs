using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Hier steht die Merlitz-Gleichung, die wir für das dynamische
    /// Random-Dot-Feld und die zugehörigen Vergleichsrechnungen verwenden. Der
    /// statische Checkerboard-Test besitzt inzwischen eine getrennte
    /// Visual-Space-l-Abbildung. Die Gleichung stammt aus Merlitz
    /// (JOSA A 27, 50-57, 2010):
    ///
    ///     tan(k a) = m tan(k A)
    ///
    /// A ist der ursprüngliche Winkel eines Punktes und a der Winkel, unter dem
    /// der Punkt nach der Abbildung erscheint. m ist die Vergrößerung in der
    /// Nähe der Bildmitte. k bestimmt, wie sich die Abbildung zum Rand hin
    /// verhält. Für k = 0 wird der Grenzfall a = m A verwendet.
    ///
    /// In dieser Datei steht nur die Mathematik. Position, Bewegung und Material
    /// des Punktfelds werden in RandomDotFieldStimulus gesteuert.
    /// </summary>
    public static class MerlitzCheckerboardMath
    {
        private const double KLimitEpsilon = 1e-7;

        /// <summary>
        /// Vorwärtsrichtung der Gleichung: Zu einem ursprünglichen Objektwinkel
        /// A wird berechnet, bei welchem sichtbaren Winkel a der Punkt landet.
        /// Diese Richtung wird zum Beispiel beim Random-Dot-Feld benötigt.
        /// </summary>
        public static double ApparentAngleFromObject(
            double objectAngleRadians,
            double magnification,
            double k)
        {
            Validate(magnification, k);

            // Direkt durch k zu teilen wäre bei k = 0 nicht möglich. Der hier
            // verwendete Grenzfall ist genau das Ergebnis für k gegen null.
            if (k < KLimitEpsilon)
            {
                return magnification * objectAngleRadians;
            }

            return Math.Atan(
                magnification * Math.Tan(k * objectAngleRadians)) / k;
        }

        /// <summary>
        /// Rückwärtsrichtung der Gleichung: Zu einem bereits sichtbaren Winkel a
        /// wird der ursprüngliche Objektwinkel A gesucht. Diese Richtung wird
        /// noch für mathematische Vergleiche und Plot-Abbildungen verwendet.
        /// </summary>
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
        /// Rechnet die Position eines sichtbaren Pixels zurück auf das gerade
        /// Ausgangsmuster des Checkerboards.
        ///
        /// normalizedDisplayRadius beschreibt die Stelle im fertigen Kreis:
        /// 0 ist die Mitte und 1 ist der Rand. Die Rückgabe beschreibt die
        /// passende Stelle im ursprünglichen geraden Schachbrett ebenfalls von
        /// 0 bis 1. Der Shader liest anschließend an dieser Stelle ab, ob das
        /// Feld schwarz oder weiß ist.
        ///
        /// Die Teilung durch den Wert am äußeren Rand sorgt dafür, dass der Rand
        /// bei jedem k an derselben Stelle bleibt. Dadurch ändert k die Form der
        /// Linien, aber nicht den eingestellten Winkeldurchmesser des Kreises.
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

            // Schritt 1: Position im fertigen Kreis in einen sichtbaren Winkel
            // umrechnen. Der Halbwinkel entspricht dem eingestellten FOV-Rand.
            double tangentAtBoundary = Math.Tan(apparentHalfAngleRadians);
            double apparentAngle = Math.Atan(
                normalizedDisplayRadius * tangentAtBoundary);

            // Schritt 2: Mit der rückwärts gelösten Merlitz-Gleichung bestimmen,
            // von welchem ursprünglichen Winkel dieser sichtbare Winkel kommt.
            double objectAngle = ObjectAngleFromApparent(
                apparentAngle,
                magnification,
                k);

            // Schritt 3: Auch den äußeren Rand zurückrechnen. Durch die anschließende
            // Teilung wird die gesuchte Position wieder auf den Bereich 0 bis 1 gesetzt.
            double maximumObjectAngle = ObjectAngleFromApparent(
                apparentHalfAngleRadians,
                magnification,
                k);

            return Math.Tan(objectAngle) / Math.Tan(maximumObjectAngle);
        }

        private static void Validate(double magnification, double k)
        {
            // Ungültige Werte würden keine sinnvolle Winkelabbildung ergeben und
            // sollen deshalb früh mit einer klaren Meldung auffallen.
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
