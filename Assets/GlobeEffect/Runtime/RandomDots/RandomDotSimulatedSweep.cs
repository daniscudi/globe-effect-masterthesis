using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Rechnet die glatte Bewegung des simulierten Schwenks aus. Der Winkel
    /// kann im Shader als horizontaler Yaw oder vertikaler Pitch benutzt werden.
    ///
    /// Die Bewegung startet in der Mitte, erreicht nach einem Viertel der
    /// Periode eine Seite und nach drei Vierteln die Gegenseite. An beiden
    /// Umkehrpunkten ist die Geschwindigkeit null.
    ///
    /// Hier steht nur Mathematik drin und nichts von Unity. Deshalb kann man das
    /// ohne Headset testen.
    /// </summary>
    public static class RandomDotSimulatedSweep
    {
        public static float EvaluateSweepDegrees(
            double elapsedSeconds,
            float amplitudeDegrees,
            float speedDegreesPerSecond,
            RandomDotSweepDirection direction)
        {
            if (elapsedSeconds <= 0d ||
                amplitudeDegrees <= 0f ||
                speedDegreesPerSecond <= 0f)
            {
                return 0f;
            }

            // In einer Periode werden vier Amplituden zurückgelegt. Der Speed-
            // Parameter bezeichnet die mittlere absolute Geschwindigkeit und
            // bleibt dadurch mit der bisherigen Einstellung vergleichbar.
            double phase = elapsedSeconds * Math.PI *
                speedDegreesPerSecond / (2.0 * amplitudeDegrees);
            float positiveFirstAngle = amplitudeDegrees * (float)Math.Sin(phase);

            // Das Vorzeichen bestimmt rechts/links bzw. oben/unten zuerst.
            return direction == RandomDotSweepDirection.LeftFirst
                ? -positiveFirstAngle
                : positiveFirstAngle;
        }

        public static float EvaluateYawDegrees(
            double elapsedSeconds,
            float amplitudeDegrees,
            float speedDegreesPerSecond,
            RandomDotSweepDirection direction)
        {
            return EvaluateSweepDegrees(
                elapsedSeconds, amplitudeDegrees, speedDegreesPerSecond, direction);
        }

        public static string DirectionLabel(
            RandomDotSweepAxis axis,
            RandomDotSweepDirection direction)
        {
            if (axis == RandomDotSweepAxis.Vertical)
            {
                return direction == RandomDotSweepDirection.RightFirst
                    ? "UpFirst"
                    : "DownFirst";
            }

            return direction.ToString();
        }

        public static float FullCycleDurationSeconds(
            float amplitudeDegrees,
            float speedDegreesPerSecond)
        {
            if (amplitudeDegrees <= 0f || speedDegreesPerSecond <= 0f)
            {
                return 0f;
            }

            // Von der Mitte zum ersten Rand, quer zum anderen Rand und zurück zur
            // Mitte sind zusammen vier Amplituden.
            return 4f * amplitudeDegrees / speedDegreesPerSecond;
        }
    }
}
