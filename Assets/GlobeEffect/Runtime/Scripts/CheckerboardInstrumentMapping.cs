using System;

namespace GlobeEffect.VRCheckerboard
{
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
        public const double MaximumInstrumentAngleRadians = Math.PI / 2.0 - 0.01;

        /// <summary>
        /// Vom Bildradius zurück auf das gerade Ausgangsgitter. Anders als beim
        /// alten l-Muster wird NICHT für jedes k neu am Kreisrand normiert:
        /// Ein festes Ausgangsgitter wird optisch abgebildet, die Blende schneidet
        /// das Ergebnis unabhängig davon aus. So gilt wirklich dieselbe Gleichung
        /// für jeden Gitterpunkt und für jeden Random-Dot-Mittelpunkt.
        /// </summary>
        public static double SourceRadius(double displayRadius, double angularDiameterDegrees,
            double magnification, double k)
        {
            ValidateParameters(angularDiameterDegrees, magnification, k);
            if (!IsFinite(displayRadius) || displayRadius < 0.0)
                throw new ArgumentOutOfRangeException(nameof(displayRadius));

            double tangentAtBoundary = Math.Tan(angularDiameterDegrees * Math.PI / 360.0);
            double apparentAngle = Math.Atan(displayRadius * tangentAtBoundary);
            if (k * apparentAngle >= MaximumInstrumentAngleRadians)
                throw new ArgumentOutOfRangeException(nameof(displayRadius));
            double objectAngle = MerlitzBinocularReferenceMath.ObjectAngleFromApparent(
                apparentAngle, magnification, k);
            return magnification * Math.Tan(objectAngle) / tangentAtBoundary;
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
            double k, double edgeSoftnessDegrees = 0.0, bool circularAperture = true)
        {
            VisualSpaceRadialMapping.ValidateAngularDiameter(angularDiameterDegrees);
            if (!IsFinite(magnification) || magnification < 1.0 || magnification > 20.0)
                throw new ArgumentOutOfRangeException(nameof(magnification), "m muss zwischen 1 und 20 liegen.");
            if (!IsFinite(k) || k < 0.0 || k > 2.0)
                throw new ArgumentOutOfRangeException(nameof(k), "k muss zwischen 0 und 2 liegen.");
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
