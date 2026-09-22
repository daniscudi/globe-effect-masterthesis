using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Die Formel, mit der das Gitter beim statischen Checkerboard verzogen wird.
    ///
    /// Merlitz beschreibt, wie weit ein Punkt von der Mitte weg zu sein scheint, mit
    ///
    ///     y_l(a) = tan(l a) / l.
    ///
    /// Anders als sein k hat l nichts mit einer Fernglasvergrößerung zu tun.
    /// Für den Shader wird die Formel am Rand des Kreises auf 1 gebracht.
    /// Dadurch ändert l nur die Form der Linien. Wie groß der Kreis ist und wo
    /// sein Rand liegt, bleibt gleich.
    /// </summary>
    public static class VisualSpaceRadialMapping
    {
        // Wichtig: Hier wird l nicht aus Daten geschätzt und auch nicht neu
        // ausgerechnet. l wird im Inspector als Bedingung vorgegeben.
        // In dieser Datei steht nur dieselbe Formel noch einmal in C#. So kann man
        // Werte prüfen, mitschreiben und testen, ohne den Shader zu brauchen.
        private const double AlmostZero = 1e-7;
        private const double SafetyMarginRadians = 1e-5;

        public const double MinimumAngularDiameterDegrees = 1.0;
        public const double MaximumAngularDiameterDegrees = 170.0;
        public const double MinimumVisualSpaceL = 0.0;
        public const double MaximumVisualSpaceL = 1.4;

        /// <summary>
        /// Diese Kennung steht in jeder Messdatei. Wenn die Formel später noch
        /// einmal geändert wird, sieht man an den alten Dateien trotzdem, womit
        /// sie aufgenommen wurden.
        /// </summary>
        public const string MappingVersion =
            "visual-space-l-tangent-normalized-cartesian-grid-v2";

        /// <summary>
        /// Rechnet einen Radius im sichtbaren Kreis zurück auf die Stelle, an der
        /// das gerade Ausgangsgitter abgelesen werden muss.
        /// </summary>
        /// displayRadius: 0 ist die Mitte, 1 ist der Rand.
        /// angularDiameterDegrees: wie groß der Kreis insgesamt ist, in Grad.
        /// visualSpaceL: l = 1 gibt ein gerades Gitter, l = 0,5 den Helmholtz-Punkt.
        /// Geht l gegen 0, sind die Abstände überall gleich groß.
        public static double SourceRadius(
            double displayRadius,
            double angularDiameterDegrees,
            double visualSpaceL)
        {
            ValidateFinite(displayRadius, nameof(displayRadius));
            ValidateParameters(angularDiameterDegrees, visualSpaceL);

            if (displayRadius < 0.0 || displayRadius > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(displayRadius));
            }

            if (displayRadius == 0.0)
            {
                return 0.0;
            }

            double halfAngle = 0.5 * angularDiameterDegrees * Math.PI / 180.0;

            // displayRadius ist erst mal nur ein gerader Abstand zwischen Mitte und
            // Rand. atan macht daraus den Winkel, den dieser Bildpunkt im Headset
            // wirklich einnimmt.
            double visualAngle = Math.Atan(
                displayRadius * Math.Tan(halfAngle));

            // Direkt durch l zu teilen geht bei l = 0 nicht. Nach dem Normieren
            // kürzt sich l für alle anderen Werte sowieso weg. Der Sonderfall hier
            // kommt daher, dass tan(l a) / l für ganz kleine l einfach gegen a geht.
            if (visualSpaceL < AlmostZero)
            {
                return visualAngle / halfAngle;
            }

            // Das Ergebnis liegt wieder zwischen 0 und 1: 0 ist die Mitte, 1 ist der
            // Rand. Dazwischen verschiebt l, an welcher Stelle das Gitter abgelesen wird.
            return Math.Tan(visualSpaceL * visualAngle) /
                Math.Tan(visualSpaceL * halfAngle);
        }

        /// <summary>
        /// Rechnet nur die beiden gemeinsamen Eckpunkte auf eine Oomes-ähnliche
        /// Skala um: l = 1 wird zu 0 und l = 0,5 wird zu 1.
        /// Das ist nicht die Originalformel von Oomes, die steht nirgends in der
        /// Veröffentlichung. Deshalb wird die Zahl nur als endpoint equivalent
        /// mitgeschrieben und nicht weiter ausgewertet.
        /// </summary>
        public static double OomesEndpointEquivalent(double visualSpaceL)
        {
            ValidateVisualSpaceL(visualSpaceL);
            return 2.0 * (1.0 - visualSpaceL);
        }

        /// <summary>
        /// Rechnet den gewünschten Abstand der Gitterlinien von Grad in die Weite
        /// im u/v-Koordinatensystem um. Der Winkel legt also nur fest, wie groß die
        /// Karos sind. Das Ausgangsgitter bleibt danach ein ganz normales
        /// gleichmäßiges Gitter.
        /// </summary>
        public static double NormalizedGridLineSpacing(
            double angularDiameterDegrees,
            double gridLineSpacingDegrees)
        {
            ValidateAngularDiameter(angularDiameterDegrees);
            ValidateFinite(
                gridLineSpacingDegrees,
                nameof(gridLineSpacingDegrees));

            if (gridLineSpacingDegrees <= 0.0 || gridLineSpacingDegrees >= 90.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gridLineSpacingDegrees),
                    "Der Abstand der Gitterlinien muss zwischen " +
                    "0 und 90 Grad liegen.");
            }

            double halfAngle = 0.5 * angularDiameterDegrees * Math.PI / 180.0;
            double spacingRadians = gridLineSpacingDegrees * Math.PI / 180.0;

            // Beide Tangenswerte liegen im selben geraden Bildraum. Teilt man sie,
            // kommt die Gitterweite im Verhältnis zum Radius des Kreises heraus.
            return Math.Tan(spacingRadians) / Math.Tan(halfAngle);
        }

        public static void ValidateParameters(
            double angularDiameterDegrees,
            double visualSpaceL)
        {
            ValidateAngularDiameter(angularDiameterDegrees);
            ValidateVisualSpaceL(visualSpaceL);

            // Bei sehr großen l und sehr großem FOV läuft der Tangens in seinen
            // Umkehrpunkt. Dann wäre das Gitter nicht mehr eindeutig, deshalb wird
            // so eine Kombination hier abgefangen.
            double halfAngle = 0.5 * angularDiameterDegrees * Math.PI / 180.0;
            if (visualSpaceL * halfAngle >=
                Math.PI / 2.0 - SafetyMarginRadians)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visualSpaceL),
                    "Diese Kombination aus l und FOV geht nicht. Der Tangens kippt " +
                    "dabei um und das Gitter wäre nicht mehr eindeutig.");
            }
        }

        public static void ValidateAngularDiameter(double angularDiameterDegrees)
        {
            ValidateFinite(angularDiameterDegrees, nameof(angularDiameterDegrees));
            if (angularDiameterDegrees < MinimumAngularDiameterDegrees ||
                angularDiameterDegrees > MaximumAngularDiameterDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(angularDiameterDegrees),
                    $"Der Winkeldurchmesser muss zwischen " +
                    $"{MinimumAngularDiameterDegrees} und " +
                    $"{MaximumAngularDiameterDegrees} Grad liegen.");
            }
        }

        public static void ValidateVisualSpaceL(double visualSpaceL)
        {
            ValidateFinite(visualSpaceL, nameof(visualSpaceL));
            if (visualSpaceL < MinimumVisualSpaceL ||
                visualSpaceL > MaximumVisualSpaceL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visualSpaceL),
                    $"l muss zwischen {MinimumVisualSpaceL} und " +
                    $"{MaximumVisualSpaceL} liegen.");
            }
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            // Fängt kaputte Zahlen ab, also NaN und unendlich.
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
