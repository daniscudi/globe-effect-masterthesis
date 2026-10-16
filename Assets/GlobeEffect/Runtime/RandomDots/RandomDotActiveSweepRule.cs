using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Die Kopftempo-Regel für aktive Trials und die Active Practice.
    ///
    /// Kein Zieltempo: Die Person schwenkt so schnell, wie es sich natürlich anfühlt.
    /// Die Punkte dürfen kommen, wenn alles gleichzeitig stimmt:
    /// - seit Beginn des Wartens ist die Pflicht-Wartezeit vorbei,
    /// - der Kopf dreht in die richtige Richtung (abwechselnd, falls eingeschaltet),
    /// - er dreht gleichmäßig (RandomDotSteadyHeadDetector),
    /// - er hat in diesem Schwenk schon die Mindeststrecke gedreht.
    /// Während der Punkte ist das Tempo direkt davor der Maßstab: Weicht der Kopf
    /// länger als die Toleranzzeit stärker als erlaubt davon ab, wird abgebrochen.
    ///
    /// Kein MonoBehaviour: Der Experiment Manager füttert die Klasse jeden Frame
    /// mit der Kopfrichtung und übernimmt die Einstellungen aus dem Inspector.
    /// </summary>
    public sealed class RandomDotActiveSweepRule
    {
        // Einstellungen (kommen aus dem Inspector des Experiment Managers).
        public HeadVelocityAxis Axis = HeadVelocityAxis.Horizontal;
        public float ChangeTolerance = 0.25f;
        public float MinimumMovingSpeed = 0.5f;
        public float MinimumTurnDegrees = 3f;
        public float HoldSeconds = 0.3f;
        public float MinimumWaitSeconds = 1f;
        public float GraceSeconds = 0.1f;
        public float SmoothingSeconds = 0.1f;
        public bool AlternateDirection = true;

        private readonly RandomDotHeadVelocityGate gate = new();
        private readonly RandomDotSteadyHeadDetector steady = new();
        private readonly RandomDotHeadOffAxisMeter offAxis = new();
        private double waitStartSeconds;
        // Vorzeichen des letzten Schwenks mit Punkten (+1 rechts/oben, -1 links/unten, 0 = noch keiner).
        private int lastSweepSign;
        // Laufender Schwenk vor den Punkten: Richtung und schon gedrehte Strecke (°).
        private int currentSweepSign;
        private float currentSweepTravelDegrees;

        /// <summary>Tempo direkt vor den Punkten, der Maßstab während der Punkte (°/s).</summary>
        public float ReferenceSpeed { get; private set; } = float.NaN;

        /// <summary>Tatsächliche Schwenkrichtung beim Einblenden (Right/Left bzw. Up/Down).</summary>
        public string Direction { get; private set; } = string.Empty;

        /// <summary>Wie weit der Kopf im Schwenk schon gedreht hatte, als die Punkte kamen (°).</summary>
        public float TurnBeforeDots { get; private set; } = float.NaN;

        /// <summary>Nicken/Drehen neben der Schwenkachse und Kippen während der Punkte.</summary>
        public RandomDotHeadOffAxisMeter OffAxis => offAxis;

        /// <summary>+1/-1: in diese Richtung muss der nächste Schwenk gehen, 0 = egal.</summary>
        public int RequiredSign => AlternateDirection ? -lastSweepSign : 0;

        /// <summary>Richtung des nächsten Schwenks (Right/Left bzw. Up/Down) oder "any".</summary>
        public string NextDirection => RequiredSign == 0
            ? "any"
            : RandomDotHeadOffAxisMeter.Direction(RequiredSign, Axis);

        /// <summary>Lange genug außerhalb des erlaubten Tempos: Trial abbrechen.</summary>
        public bool SpeedChangedTooLong => gate.SecondsOutOfRange > GraceSeconds;

        /// <summary>Kopftempo für die CSV, mit dem Simulated-Tempo zum Vergleich.</summary>
        public HeadVelocitySummary Summarize(float simulatedHeadSpeed) => gate.Summarize(simulatedHeadSpeed);

        /// <summary>Neue Sitzung oder Übung: Der erste Schwenk darf in beide Richtungen gehen.</summary>
        public void ResetDirection()
        {
            lastSweepSign = 0;
        }

        /// <summary>Messung neu beginnen; ab jetzt läuft auch die Pflicht-Wartezeit.</summary>
        public void BeginWait(double nowSeconds)
        {
            gate.Reset();
            steady.Reset();
            offAxis.Reset();
            ReferenceSpeed = float.NaN;
            Direction = string.Empty;
            TurnBeforeDots = float.NaN;
            currentSweepSign = 0;
            currentSweepTravelDegrees = 0f;
            waitStartSeconds = nowSeconds;
        }

        /// <summary>Einmal pro Frame aufrufen, vor und während der Punkte.</summary>
        public void Sample(Vector3 headForward, Vector3 headUp, float deltaSeconds, bool presenting)
        {
            float minimum = presenting ? ReferenceSpeed * (1f - ChangeTolerance) : 0f;
            float maximum = presenting ? ReferenceSpeed * (1f + ChangeTolerance) : float.MaxValue;
            gate.AddSample(headForward, deltaSeconds, Axis, SmoothingSeconds, minimum, maximum);
            if (presenting)
            {
                // Während der Punkte auch Nicken/Drehen neben der Schwenkachse und Kippen messen.
                offAxis.Add(headForward, headUp, deltaSeconds);
                return;
            }

            // Abwechselnde Richtung: Ein Schwenk in die Richtung des letzten Trials
            // zählt wie Stillhalten, die Punkte kommen erst beim Zurückschwenken.
            float signed = gate.SignedSpeedDegreesPerSecond;
            bool rightWay = RequiredSign == 0 || Math.Sign(signed) == RequiredSign;
            steady.Add(rightWay ? gate.SpeedDegreesPerSecond : 0f, deltaSeconds, ChangeTolerance, MinimumMovingSpeed);

            // Strecke des laufenden Schwenks: zählt nur, solange der Kopf in eine (erlaubte)
            // Richtung dreht. Stillhalten oder Umkehren beginnt einen neuen Schwenk.
            bool moving = Mathf.Abs(signed) >= MinimumMovingSpeed;
            if (moving && rightWay)
            {
                int sign = signed >= 0f ? 1 : -1;
                if (sign != currentSweepSign)
                {
                    currentSweepSign = sign;
                    currentSweepTravelDegrees = 0f;
                }
                currentSweepTravelDegrees += Mathf.Abs(signed) * deltaSeconds;
            }
            else
            {
                currentSweepSign = 0;
                currentSweepTravelDegrees = 0f;
            }
        }

        /// <summary>Alle Bedingungen zusammen; die Mindeststrecke allein löst nie etwas aus.</summary>
        public bool ReadyForDots(double nowSeconds) =>
            nowSeconds - waitStartSeconds >= MinimumWaitSeconds
            && steady.SteadySeconds >= HoldSeconds
            && currentSweepTravelDegrees >= MinimumTurnDegrees;

        /// <summary>
        /// Die Punkte erscheinen. Das (geglättete) Tempo direkt davor wird zum Maßstab.
        /// Nicht der Mittelwert des ganzen gleichmäßigen Abschnitts: Der enthält noch
        /// einen Teil des Anfahrens und läge dadurch etwas unter dem echten Tempo.
        /// </summary>
        public void BeginPresentation(Vector3 headForward, Vector3 headUp)
        {
            ReferenceSpeed = gate.SpeedDegreesPerSecond;
            TurnBeforeDots = currentSweepTravelDegrees;
            Direction = RandomDotHeadOffAxisMeter.Direction(gate.SignedSpeedDegreesPerSecond, Axis);
            // Auch ein abgebrochener Schwenk zählt: Der nächste geht in die andere Richtung.
            lastSweepSign = gate.SignedSpeedDegreesPerSecond >= 0f ? 1 : -1;
            gate.BeginPresentation();
            offAxis.Begin(headForward, headUp, Axis);
        }
    }
}
