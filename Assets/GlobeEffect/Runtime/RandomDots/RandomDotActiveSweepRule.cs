using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Die Kopftempo-Regel für aktive Trials und Active Practice 2.
    ///
    /// Ein einziger Schwellwert (StillBelow): Darunter gilt der Kopf als still.
    /// Gezählt wird das geglättete Tempo in Schwenkrichtung, also mit Vorzeichen.
    /// - Die Punkte erscheinen, sobald das Tempo in der erlaubten Richtung über dem
    ///   Schwellwert liegt (abwechselnd, falls eingeschaltet, sonst in beide Richtungen).
    ///   Wie schnell, spielt keine Rolle.
    /// - Während der Punkte zählt die Richtung beim Einblenden. Fällt das Tempo in
    ///   dieser Richtung unter den Schwellwert (Kopf bleibt stehen oder dreht zurück),
    ///   wird der Trial abgebrochen. Die Glättung ist der einzige Puffer.
    ///
    /// Kein MonoBehaviour: Der Experiment Manager füttert die Klasse jeden Frame
    /// mit der Kopfrichtung und übernimmt die Einstellungen aus dem Inspector.
    /// </summary>
    public sealed class RandomDotActiveSweepRule
    {
        // Einstellungen (kommen aus dem Inspector des Experiment Managers).
        public HeadVelocityAxis Axis = HeadVelocityAxis.Horizontal;
        public float StillBelow = 1f;
        public float SmoothingSeconds = 0.1f;
        public bool AlternateDirection = true;

        private readonly RandomDotHeadVelocityGate gate = new();
        private readonly RandomDotHeadOffAxisMeter offAxis = new();
        // Vorzeichen des letzten Schwenks mit Punkten (+1 rechts/oben, -1 links/unten, 0 = noch keiner).
        private int lastSweepSign;
        // Richtung des laufenden Schwenks mit Punkten, 0 = noch keine Punkte.
        private int sweepSign;

        /// <summary>Tatsächliche Schwenkrichtung beim Einblenden (Right/Left bzw. Up/Down).</summary>
        public string Direction { get; private set; } = string.Empty;

        /// <summary>Nicken/Drehen neben der Schwenkachse und Kippen während der Punkte.</summary>
        public RandomDotHeadOffAxisMeter OffAxis => offAxis;

        /// <summary>+1/-1: in diese Richtung muss der nächste Schwenk gehen, 0 = egal.</summary>
        public int RequiredSign => AlternateDirection ? -lastSweepSign : 0;

        /// <summary>Richtung des nächsten Schwenks (Right/Left bzw. Up/Down) oder "any".</summary>
        public string NextDirection => RequiredSign == 0
            ? "any"
            : RandomDotHeadOffAxisMeter.Direction(RequiredSign, Axis);

        /// <summary>Kopftempo für die CSV, mit dem Simulated-Tempo zum Vergleich.</summary>
        public HeadVelocitySummary Summarize(float simulatedHeadSpeed) => gate.Summarize(simulatedHeadSpeed);

        /// <summary>Neue Sitzung oder Übung: Der erste Schwenk darf in beide Richtungen gehen.</summary>
        public void ResetDirection()
        {
            lastSweepSign = 0;
        }

        /// <summary>Messung neu beginnen (vor jedem Trial).</summary>
        public void BeginWait()
        {
            gate.Reset();
            offAxis.Reset();
            Direction = string.Empty;
            sweepSign = 0;
        }

        /// <summary>Einmal pro Frame aufrufen, vor und während der Punkte.</summary>
        public void Sample(Vector3 headForward, Vector3 headUp, float deltaSeconds, bool presenting)
        {
            gate.AddSample(headForward, deltaSeconds, Axis, SmoothingSeconds);
            // Während der Punkte auch Nicken/Drehen neben der Schwenkachse und Kippen messen.
            if (presenting) offAxis.Add(headForward, headUp, deltaSeconds);
        }

        /// <summary>Tempo in der erlaubten Richtung über dem Schwellwert: Die Punkte dürfen kommen.</summary>
        public bool ReadyForDots
        {
            get
            {
                float signed = gate.SignedSpeedDegreesPerSecond;
                float speed = RequiredSign == 0 ? Mathf.Abs(signed) : signed * RequiredSign;
                return speed >= StillBelow;
            }
        }

        /// <summary>
        /// Während der Punkte: Tempo in Schwenkrichtung unter dem Schwellwert. Das gilt
        /// auch, wenn der Kopf zurückdreht (dann ist das Tempo in Schwenkrichtung negativ).
        /// </summary>
        public bool HeadStopped => sweepSign != 0 && gate.SignedSpeedDegreesPerSecond * sweepSign < StillBelow;

        /// <summary>Die Punkte erscheinen: Ab jetzt zählt die Richtung dieses Schwenks.</summary>
        public void BeginPresentation(Vector3 headForward, Vector3 headUp)
        {
            sweepSign = gate.SignedSpeedDegreesPerSecond >= 0f ? 1 : -1;
            Direction = RandomDotHeadOffAxisMeter.Direction(sweepSign, Axis);
            // Auch ein abgebrochener Schwenk zählt: Der nächste geht in die andere Richtung.
            lastSweepSign = sweepSign;
            gate.BeginPresentation();
            offAxis.Begin(headForward, headUp, Axis);
        }
    }
}
