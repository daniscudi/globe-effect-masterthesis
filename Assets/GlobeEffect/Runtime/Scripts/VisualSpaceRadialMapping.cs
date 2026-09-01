using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Radiale Abbildung für den statischen Checkerboard-Test.
    ///
    /// Merlitz beschreibt den wahrgenommenen radialen Abstand eines Punktes
    /// mit der Visual-Space-Funktion
    ///
    ///     y_l(a) = tan(l a) / l.
    ///
    /// Anders als sein Instrumentenparameter k hängt l nicht von einer
    /// Fernglasvergrößerung ab. Für den Shader wird die Funktion am Rand der
    /// kreisrunden Blende normiert. Dadurch verändert l die Form der Linien,
    /// aber weder den eingestellten Winkeldurchmesser noch den Blendenrand.
    /// </summary>
    public static class VisualSpaceRadialMapping
    {
        private const double LLimitEpsilon = 1e-7;
        private const double SingularityMarginRadians = 1e-5;

        public const double MinimumAngularDiameterDegrees = 1.0;
        public const double MaximumAngularDiameterDegrees = 170.0;
        public const double MinimumVisualSpaceL = 0.0;
        public const double MaximumVisualSpaceL = 1.4;

        /// <summary>
        /// Die Kennung wird in jeder Messdatei gespeichert. So bleiben Daten
        /// unterscheidbar, falls die Abbildung später noch erweitert wird.
        /// </summary>
        public const string MappingVersion =
            "visual-space-l-tangent-normalized-v1";

        /// <summary>
        /// Rechnet einen Radius im sichtbaren Kreis auf die Stelle zurück, an
        /// der das unverzerrte Ausgangsgitter abgetastet werden muss.
        /// </summary>
        /// <param name="displayRadius">
        /// Radius im sichtbaren Kreis: 0 ist die Mitte, 1 der Blendenrand.
        /// </param>
        /// <param name="angularDiameterDegrees">
        /// Gesamter Winkeldurchmesser der kreisrunden Blende.
        /// </param>
        /// <param name="visualSpaceL">
        /// l = 1 ergibt ein gerades gnomonisches Gitter. l = 0,5 ergibt
        /// den stereografischen Helmholtz-Endpunkt. Für l gegen 0 gilt der
        /// äquidistante Grenzfall.
        /// </param>
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
            double visualAngle = Math.Atan(
                displayRadius * Math.Tan(halfAngle));

            // Direkt durch l zu teilen wäre bei l = 0 nicht möglich. Nach der
            // Normierung kürzt sich l für alle anderen Werte heraus. Der hier
            // eingesetzte Grenzfall folgt aus tan(l a) / l -> a.
            if (visualSpaceL < LLimitEpsilon)
            {
                return visualAngle / halfAngle;
            }

            return Math.Tan(visualSpaceL * visualAngle) /
                Math.Tan(visualSpaceL * halfAngle);
        }

        /// <summary>
        /// Übersetzt nur die beiden gemeinsamen Referenzpunkte auf eine
        /// Oomes-ähnliche Skala: l = 1 entspricht 0 und l = 0,5 entspricht 1.
        /// Diese Zahl ist nicht die unveröffentlichte Originalinterpolation
        /// von Oomes und wird deshalb nur als endpoint equivalent gespeichert.
        /// </summary>
        public static double OomesEndpointEquivalent(double visualSpaceL)
        {
            ValidateVisualSpaceL(visualSpaceL);
            return 2.0 * (1.0 - visualSpaceL);
        }

        public static void ValidateParameters(
            double angularDiameterDegrees,
            double visualSpaceL)
        {
            ValidateAngularDiameter(angularDiameterDegrees);
            ValidateVisualSpaceL(visualSpaceL);

            double halfAngle = 0.5 * angularDiameterDegrees * Math.PI / 180.0;
            if (visualSpaceL * halfAngle >=
                Math.PI / 2.0 - SingularityMarginRadians)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visualSpaceL),
                    "Für diese Kombination aus l und FOV wäre die Tangensabbildung nicht mehr monoton.");
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
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
