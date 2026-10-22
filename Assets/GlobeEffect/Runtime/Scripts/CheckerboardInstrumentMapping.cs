using System;

namespace GlobeEffect.VRCheckerboard
{
    // Werte bleiben stabil: gespeicherte Szenen ohne dieses Feld verwenden Variante 2.
    public enum CheckerboardGridMapping
    {
        FixedSourceGrid = 0,
        EdgeNormalized = 1
    }

    /// <summary>
    /// Statisches gerades Ausgangsgitter durch Merlitz' Instrument, Gl. 4–6.
    /// k und m haben dieselbe Bedeutung wie bei den Random Dots. Es wird keine
    /// zusätzliche Wahrnehmungstransformation mit einem persönlichen l angewandt.
    /// </summary>
    public static class CheckerboardInstrumentMapping
    {
        // v2 (19.10.2026): optional invertierte Felder pro Trial. Bei ausgeschalteter
        // Option zeichnet v2 Pixel für Pixel dasselbe Bild wie v1 (Test vorhanden).
        public const string MappingVersion = "merlitz-instrument-checkerboard-fixed-source-grid-v2";
        public const string ExtensionMappingVersion = "custom-tan-tanh-checkerboard-fixed-source-grid-v1";
        public const string EdgeNormalizedMappingVersion = "merlitz-instrument-checkerboard-edge-normalized-v1";
        public const string EdgeNormalizedExtensionVersion = "custom-tan-tanh-checkerboard-edge-normalized-v1";
        public const double MinimumExtendedSetting = -2.0;
        public const double MaximumInstrumentAngleRadians = Math.PI / 2.0 - 0.01;

        // Standard-m des Checkerboards (Manager und Stimulus). Einstellbar 1–20, um
        // anzusehen, wie m die Verzeichnung verändert.
        public const float DefaultMagnificationM = 10f;

        // Koordinaten: Der Shader rechnet in Einheiten des Blendenrands,
        // r = R / tan(FOV/2) mit R = sqrt(u² + v²), u = x/z, v = y/z (perspektivische
        // Bildkoordinaten der Blickrichtung). Damit gilt a = atan(R) = atan(r·tan(FOV/2)).
        // SourceRadius liefert s = m·tan(A)/tan(FOV/2), also im selben Maßstab
        // (u_q, v_q) = [m·tan(A)/R]·(u, v). Die Gitterweite tan(Δ)/tan(FOV/2) steht im
        // gleichen Maßstab; das Verhältnis Quellposition/Gitterweite ist deshalb
        // identisch mit der Darstellung in u, v. Für R -> 0 geht m·tan(A)/R -> 1
        // bei jedem k. Das folgt direkt aus der Instrumentengleichung (Vergrößerung m
        // in der Mitte); es gibt KEINE zusätzliche Zentrums- oder Randkorrektur.
        // Endlich große Kästchen in der Mitte sind deshalb nicht bei jedem k gleich groß.

        public static string VersionFor(bool allowExtension,
            CheckerboardGridMapping gridMapping = CheckerboardGridMapping.FixedSourceGrid)
        {
            ValidateGridMapping(gridMapping);
            return gridMapping == CheckerboardGridMapping.EdgeNormalized
                ? (allowExtension ? EdgeNormalizedExtensionVersion : EdgeNormalizedMappingVersion)
                : (allowExtension ? ExtensionMappingVersion : MappingVersion);
        }

        public static void ValidateGridMapping(CheckerboardGridMapping gridMapping)
        {
            if (gridMapping != CheckerboardGridMapping.FixedSourceGrid
                && gridMapping != CheckerboardGridMapping.EdgeNormalized)
                throw new ArgumentOutOfRangeException(nameof(gridMapping));
        }

        public static string FamilyFor(double setting) => setting < 0.0 ? "custom_tanh" : "merlitz_tan";

        /// <summary>
        /// Vom Bildradius zurück auf das gerade Ausgangsgitter. Standard (Variante 2):
        /// festes Ausgangsgitter, unabhängig ausgeschnittene Blende, dieselbe Optik
        /// wie bei den Random-Dot-Mittelpunkten. Variante 3 normiert den Quellradius
        /// zusätzlich an der nominalen Blende; die optische Gleichung bleibt gleich.
        /// </summary>
        public static double SourceRadius(double displayRadius, double angularDiameterDegrees,
            double magnification, double k, bool allowPincushionExtension = false,
            CheckerboardGridMapping gridMapping = CheckerboardGridMapping.FixedSourceGrid)
        {
            ValidateGridMapping(gridMapping);
            ValidateParameters(angularDiameterDegrees, magnification, k,
                allowPincushionExtension: allowPincushionExtension);
            if (!IsFinite(displayRadius) || displayRadius < 0.0)
                throw new ArgumentOutOfRangeException(nameof(displayRadius));

            double tangentAtBoundary = Math.Tan(angularDiameterDegrees * Math.PI / 360.0);
            double apparentAngle = Math.Atan(displayRadius * tangentAtBoundary);
            if (k * apparentAngle >= MaximumInstrumentAngleRadians)
                throw new ArgumentOutOfRangeException(nameof(displayRadius));
            double objectAngle = k < 0.0
                ? CheckerboardPincushionExtension.ObjectAngleFromApparent(apparentAngle, magnification, -k)
                : MerlitzBinocularReferenceMath.ObjectAngleFromApparent(apparentAngle, magnification, k);
            return magnification * Math.Tan(objectAngle) / tangentAtBoundary
                * GridSourceScale(angularDiameterDegrees, magnification, k,
                    allowPincushionExtension, gridMapping);
        }

        /// <summary>
        /// Variante 3: S(r)/S(1), normiert am nominalen Kreisrand (50% im weichen Rand).
        /// Die optische Gleichung bleibt gleich; die Quellgitterweite wird angepasst.
        /// Keine nachträgliche Skalierung des fertigen Bildes (HTML-Variante 5).
        /// </summary>
        public static double GridSourceScale(double angularDiameterDegrees, double magnification,
            double k, bool allowPincushionExtension, CheckerboardGridMapping gridMapping)
        {
            ValidateGridMapping(gridMapping);
            if (gridMapping == CheckerboardGridMapping.FixedSourceGrid) return 1.0;
            ValidateParameters(angularDiameterDegrees, magnification, k,
                allowPincushionExtension: allowPincushionExtension);
            double half = angularDiameterDegrees * Math.PI / 360.0;
            double objectEdge = k < 0.0
                ? CheckerboardPincushionExtension.ObjectAngleFromApparent(half, magnification, -k)
                : MerlitzBinocularReferenceMath.ObjectAngleFromApparent(half, magnification, k);
            return Math.Tan(half) / (magnification * Math.Tan(objectEdge));
        }

        /// <summary>
        /// Gitterabstand im unverzeichneten Referenzbild (k = 1). m*tan(A) ist
        /// die Referenzkoordinate: Der Abstand auf der gedachten Ausgangsebene ist
        /// deshalb tan(gridSpacing)/m. Dieser Abstand bleibt bei wechselndem k fest.
        /// </summary>
        public static double GridLineSpacingUv(double angularDiameterDegrees, double gridSpacingDegrees)
        {
            return VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                angularDiameterDegrees, gridSpacingDegrees);
        }

        public static void ValidateParameters(double angularDiameterDegrees, double magnification,
            double k, double edgeSoftnessDegrees = 0.0, bool circularAperture = true,
            bool allowPincushionExtension = false)
        {
            VisualSpaceRadialMapping.ValidateAngularDiameter(angularDiameterDegrees);
            if (!IsFinite(magnification) || magnification < 1.0 || magnification > 20.0)
                throw new ArgumentOutOfRangeException(nameof(magnification), "m muss zwischen 1 und 20 liegen.");
            if (!IsFinite(k) || k < (allowPincushionExtension ? MinimumExtendedSetting : 0.0) || k > 2.0)
                throw new ArgumentOutOfRangeException(nameof(k),
                    "Merlitz-k muss zwischen 0 und 2 liegen. Negative Reizwerte (-2 bis 0) "
                    + "brauchen die ausdrücklich aktivierte eigene tanh-Erweiterung.");
            if (!IsFinite(edgeSoftnessDegrees) || edgeSoftnessDegrees < 0.0)
                throw new ArgumentOutOfRangeException(nameof(edgeSoftnessDegrees));

            double outerAngle = Math.Min((angularDiameterDegrees + edgeSoftnessDegrees)
                * Math.PI / 360.0, 1.55);
            if (!circularAperture)
                outerAngle = Math.Atan(Math.Sqrt(2.0) * Math.Tan(outerAngle));
            if (k * outerAngle >= MaximumInstrumentAngleRadians)
                throw new ArgumentOutOfRangeException(nameof(k),
                    "k und FOV (einschließlich weichem Rand bzw. Ecken) überschreiten den "
                    + "eindeutigen Bereich der Instrumentenabbildung. k oder FOV verkleinern.");
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
