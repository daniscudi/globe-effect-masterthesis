using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Vergleicht die gemessene Kopfbewegung mit der Sinusbahn hin und her, der
    /// die Person im HeadTracked-Block folgen soll. Zeitgewichtung verhindert,
    /// dass die Bildrate die Bewertung verändert.
    /// </summary>
    public sealed class RandomDotSweepProfileEvaluator
    {
        private readonly float amplitudeDegrees;
        private readonly float speedDegreesPerSecond;
        private readonly RandomDotSweepDirection direction;
        private double weightedSquaredError;
        private double measuredSeconds;
        private double nearestFirstExtremeTimeError = double.PositiveInfinity;
        private double nearestSecondExtremeTimeError = double.PositiveInfinity;

        public RandomDotSweepProfileEvaluator(
            float amplitudeDegrees, float speedDegreesPerSecond, RandomDotSweepDirection direction)
        {
            if (amplitudeDegrees <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amplitudeDegrees));
            }

            if (speedDegreesPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(speedDegreesPerSecond));
            }

            this.amplitudeDegrees = amplitudeDegrees;
            this.speedDegreesPerSecond = speedDegreesPerSecond;
            this.direction = direction;
        }

        public float RootMeanSquareErrorDegrees => measuredSeconds > 0d
            ? (float)Math.Sqrt(weightedSquaredError / measuredSeconds)
            : float.PositiveInfinity;

        public float FirstExtremeErrorDegrees { get; private set; } = float.PositiveInfinity;

        public float SecondExtremeErrorDegrees { get; private set; } = float.PositiveInfinity;

        public void AddSample(double elapsedSeconds, float yawDegrees, float deltaSeconds)
        {
            // Frames mit einer Lücke von mehr als einer Viertelsekunde zählen
            // nicht mit, sonst würde ein einzelner Hänger das Ergebnis dominieren.
            if (elapsedSeconds < 0d || deltaSeconds <= 0f || deltaSeconds > 0.25f)
            {
                return;
            }

            float target = RandomDotSimulatedSweep.EvaluateBackAndForthDegrees(
                elapsedSeconds, amplitudeDegrees, speedDegreesPerSecond, direction);
            double error = yawDegrees - target;
            weightedSquaredError += error * error * deltaSeconds;
            measuredSeconds += deltaSeconds;

            // Für die beiden Umkehrpunkte zählt jeweils der Messwert, der zeitlich
            // am nächsten daran liegt.
            double firstExtremeSeconds = amplitudeDegrees / speedDegreesPerSecond;
            double timingError = Math.Abs(elapsedSeconds - firstExtremeSeconds);
            if (timingError < nearestFirstExtremeTimeError)
            {
                nearestFirstExtremeTimeError = timingError;
                FirstExtremeErrorDegrees = (float)Math.Abs(error);
            }

            double secondExtremeSeconds = 3d * firstExtremeSeconds;
            timingError = Math.Abs(elapsedSeconds - secondExtremeSeconds);
            if (timingError < nearestSecondExtremeTimeError)
            {
                nearestSecondExtremeTimeError = timingError;
                SecondExtremeErrorDegrees = (float)Math.Abs(error);
            }
        }
    }
}
