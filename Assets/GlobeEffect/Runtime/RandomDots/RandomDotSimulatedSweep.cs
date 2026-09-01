using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Reine Berechnung der automatisch erzeugten Links-Rechts-Bewegung. Die
    /// Funktion startet in der Mitte, läuft mit gleichbleibender
    /// Winkelgeschwindigkeit zu einer Seite, anschließend zur anderen Seite
    /// und wieder zurück. Sie enthält keine Unity-Szenenlogik und lässt sich
    /// deshalb unabhängig vom Headset testen.
    /// </summary>
    public static class RandomDotSimulatedSweep
    {
        public static float EvaluateYawDegrees(
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

            float travelledDegrees = (float)elapsedSeconds *
                speedDegreesPerSecond;

            // Durch den Versatz um eine Amplitude beginnt PingPong genau in der
            // Mitte. Nach einer weiteren Amplitude ist die erste Randposition
            // erreicht, nach drei Amplituden die gegenüberliegende Seite.
            float rightFirstYaw = Mathf.PingPong(
                travelledDegrees + amplitudeDegrees,
                2f * amplitudeDegrees) - amplitudeDegrees;

            return direction == RandomDotSweepDirection.LeftFirst
                ? -rightFirstYaw
                : rightFirstYaw;
        }

        public static float FullCycleDurationSeconds(
            float amplitudeDegrees,
            float speedDegreesPerSecond)
        {
            if (amplitudeDegrees <= 0f || speedDegreesPerSecond <= 0f)
            {
                return 0f;
            }

            return 4f * amplitudeDegrees / speedDegreesPerSecond;
        }
    }
}
