using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Rechnet aus, wie weit die Bewegung gerade nach links oder rechts gelaufen ist.
    ///
    /// Die Bewegung startet in der Mitte, läuft gleichmäßig zu einer Seite, dann
    /// ganz auf die andere Seite und wieder zurück.
    ///
    /// Hier steht nur Mathematik drin und nichts von Unity. Deshalb kann man das
    /// ohne Headset testen.
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

            // Wie viel Weg schon zurückgelegt wurde.
            // Beispiel: 5 Grad pro Sekunde sind nach 2 Sekunden 10 Grad.
            float travelledDegrees = (float)elapsedSeconds *
                speedDegreesPerSecond;

            // PingPong läuft normalerweise von 0 los. Wir wollen aber in der Mitte
            // anfangen, deshalb wird eine Amplitude draufgerechnet und hinterher
            // wieder abgezogen. Nach einer weiteren Amplitude ist der erste Rand
            // erreicht, nach drei Amplituden der Rand auf der anderen Seite.
            float rightFirstYaw = Mathf.PingPong(
                travelledDegrees + amplitudeDegrees,
                2f * amplitudeDegrees) - amplitudeDegrees;

            // Soll es nach links losgehen, drehen wir einfach das Vorzeichen um.
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

            // Von der Mitte zum ersten Rand, quer zum anderen Rand und zurück zur
            // Mitte sind zusammen vier Amplituden.
            return 4f * amplitudeDegrees / speedDegreesPerSecond;
        }
    }
}
