using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Eigene tanh-Erweiterung zu stärkerer Kissenverzeichnung. NICHT Merlitz-k.
    /// Der negative Inspectorwert s bedeutet kappa = -s > 0:
    /// tanh(kappa*a) = m*tanh(kappa*A).
    /// Bei kappa -> 0 ist A = a/m; der Anschluss an den k=0-Zweig ist stetig.
    /// </summary>
    public static class CheckerboardPincushionExtension
    {
        public static double ObjectAngleFromApparent(double apparentAngle, double magnification, double kappa)
        {
            Validate(magnification, kappa);
            if (double.IsNaN(apparentAngle) || double.IsInfinity(apparentAngle))
                throw new ArgumentOutOfRangeException(nameof(apparentAngle));
            if (kappa < 1e-6) return apparentAngle / magnification;
            return Atanh(Math.Tanh(kappa * apparentAngle) / magnification) / kappa;
        }

        public static double ApparentAngleFromObject(double objectAngle, double magnification, double kappa)
        {
            Validate(magnification, kappa);
            if (double.IsNaN(objectAngle) || double.IsInfinity(objectAngle))
                throw new ArgumentOutOfRangeException(nameof(objectAngle));
            if (kappa < 1e-6) return magnification * objectAngle;
            // Vorwärts ist der gültige Objektbereich begrenzt: |m*tanh(kappa*A)| < 1.
            // Die inverse Darstellung innerhalb unseres sichtbaren FOV bleibt gültig.
            return Atanh(magnification * Math.Tanh(kappa * objectAngle)) / kappa;
        }

        private static double Atanh(double value)
        {
            if (Math.Abs(value) >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Die tanh-Vorwärtsabbildung ist hier undefiniert.");
            return 0.5 * Math.Log((1.0 + value) / (1.0 - value));
        }

        private static void Validate(double m, double kappa)
        {
            if (double.IsNaN(m) || double.IsInfinity(m) || m < 1.0 || m > 20.0)
                throw new ArgumentOutOfRangeException(nameof(m));
            // 2 ist nur die Softwaregrenze dieses Reglers, keine mathematische Modellgrenze.
            if (double.IsNaN(kappa) || double.IsInfinity(kappa) || kappa < 0.0 || kappa > 2.0)
                throw new ArgumentOutOfRangeException(nameof(kappa));
        }
    }
}
