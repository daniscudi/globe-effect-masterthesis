using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Erkennt, ob sich der Kopf gleichmäßig bewegt: schneller als eine kleine
    /// Mindestgeschwindigkeit und seit einer Weile ohne größere Tempowechsel.
    /// Ein bestimmtes Tempo wird nicht verlangt; die Person schwenkt so schnell,
    /// wie es sich für sie natürlich anfühlt.
    ///
    /// Das Bezugstempo ist der Mittelwert des laufenden gleichmäßigen Abschnitts.
    /// Weicht ein neuer Wert mehr als die Toleranz davon ab, beginnt ein neuer
    /// Abschnitt. Kein MonoBehaviour: Manager und Live-Anzeige füttern die Klasse
    /// jeden Frame mit dem geglätteten Tempo des Velocity Gate.
    /// </summary>
    public sealed class RandomDotSteadyHeadDetector
    {
        private float sumSpeedTimesSeconds;

        /// <summary>Wie lange der Kopf schon am Stück gleichmäßig dreht (s).</summary>
        public float SteadySeconds { get; private set; }

        /// <summary>Mittleres Tempo des laufenden gleichmäßigen Abschnitts (°/s), NaN ohne Abschnitt.</summary>
        public float ReferenceSpeed { get; private set; } = float.NaN;

        public bool IsMoving { get; private set; }

        public void Reset()
        {
            SteadySeconds = 0f;
            sumSpeedTimesSeconds = 0f;
            ReferenceSpeed = float.NaN;
            IsMoving = false;
        }

        /// <param name="speed">Geglättetes Kopftempo ohne Vorzeichen (°/s).</param>
        /// <param name="tolerance">Erlaubte Abweichung vom Bezugstempo als Anteil, 0,25 = ±25 %.</param>
        /// <param name="minimumMovingSpeed">Darunter gilt der Kopf als still (°/s).</param>
        public void Add(float speed, float deltaSeconds, float tolerance, float minimumMovingSpeed)
        {
            if (deltaSeconds <= 0f) return;

            IsMoving = speed >= minimumMovingSpeed;
            if (!IsMoving)
            {
                // Stillhalten ist kein gleichmäßiger Schwenk.
                SteadySeconds = 0f;
                sumSpeedTimesSeconds = 0f;
                ReferenceSpeed = float.NaN;
                return;
            }

            bool changed = SteadySeconds > 0f && Mathf.Abs(speed - ReferenceSpeed) > tolerance * ReferenceSpeed;
            if (changed)
            {
                // Tempo hat sich geändert: ab diesem Wert neu zählen.
                SteadySeconds = 0f;
                sumSpeedTimesSeconds = 0f;
            }

            SteadySeconds += deltaSeconds;
            sumSpeedTimesSeconds += speed * deltaSeconds;
            ReferenceSpeed = sumSpeedTimesSeconds / SteadySeconds;
        }
    }
}
