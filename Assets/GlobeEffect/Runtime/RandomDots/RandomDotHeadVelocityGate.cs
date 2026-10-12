using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Welche Kopfdrehung für die Geschwindigkeitserkennung zählt
    /// </summary>
    public enum HeadVelocityAxis
    {
        Horizontal = 0, // links, rechts yaw
        Vertical = 1, // oben, unten pitch

    }
    
    /// <summary>
    /// Misst, wie schnell der Kopf sich aktuell bewegt und zählt mit wie lange das Tempo im erlaubten
    /// Bereich liegt bzw. außerhalb davon
    /// Kein MonoBehaviour: der manager füttert die Klasse jden Frame
    /// </summary>
    public sealed class RandomDotHeadVelocityGate
    {
        private Vector3 lastForward;
        private bool hasLastSample;

        // geglättetes Tempo mit Vorzeichen in Grad pro Sekunde
        // psoitiv = nach rechts bzw nach oben
        public float SignedSpeedDegreesPerSecond { get; private set; }

        // dasselbe ohne Vorzeichen: für den Bereich zählt nur, wie schnell
        public float SpeedDegreesPerSecond => Mathf.Abs(SignedSpeedDegreesPerSecond);

        //wie lange das Tempo schon ununterbrochen im Bereich liegt
        public float SecondsInRange { get; private set; }

        // wie lange es schon ununterbrochen außerhalb liegt
        public float SecondsOutOfRange { get; private set; }

        // Statistik für die CSV. Gilt ab BeginPresentation(), also ab dem Einblenden.
        public float OnsetSpeedDegreesPerSecond { get; private set; } = float.NaN;
        public float PresentationMinSpeed { get; private set; } = float.NaN;
        public float PresentationMaxSpeed { get; private set; } = float.NaN;
        public float WaitSeconds { get; private set; }
        public float PresentationMeanSpeed =>
            presentationSeconds > 0f ? speedTimesSeconds / presentationSeconds : float.NaN;

        private bool presenting;
        private float presentationSeconds;
        private float speedTimesSeconds;

        public void Reset()
        {
            hasLastSample = false;
            SignedSpeedDegreesPerSecond = 0f;
            SecondsInRange = 0f;
            SecondsOutOfRange = 0f;
            OnsetSpeedDegreesPerSecond = float.NaN;
            PresentationMinSpeed = float.NaN;
            PresentationMaxSpeed = float.NaN;
            WaitSeconds = 0f;
            presenting = false;
            presentationSeconds = 0f;
            speedTimesSeconds = 0f;
        }

        // Aufrufen, wenn die Punkte erscheinen: Ab jetzt zählt die Statistik der Darbietung.
        public void BeginPresentation()
        {
            presenting = true;
            OnsetSpeedDegreesPerSecond = SpeedDegreesPerSecond;
            PresentationMinSpeed = SpeedDegreesPerSecond;
            PresentationMaxSpeed = SpeedDegreesPerSecond;
            presentationSeconds = 0f;
            speedTimesSeconds = 0f;
        }

        public HeadVelocitySummary Summarize(float targetSpeed) =>
            new(targetSpeed, OnsetSpeedDegreesPerSecond, PresentationMeanSpeed,
                PresentationMinSpeed, PresentationMaxSpeed, WaitSeconds);

        public void AddSample(Vector3 headForward, float deltaSeconds, HeadVelocityAxis axis, float smoothingSeconds, float minimumSpeed, float maximumSpeed)
        {
            // beim allerersten Wert gibt es noch kein Vergleich 
            if (!hasLastSample || deltaSeconds <= 0f)
            {
                lastForward = headForward;
                hasLastSample = true;
                return;
            }
            float angle = SignedAngleOnAxis(lastForward, headForward, axis);
            lastForward = headForward;
            float speed = angle / deltaSeconds;

            // Glätten: ein neuer Wert zählt mehr je länger der Frame ist
            // smoothingSeconds ist die Zeitkonstante (zB 0,1s)
            float blend = 1f - Mathf.Exp(-deltaSeconds / Mathf.Max(0.001f, smoothingSeconds));
            SignedSpeedDegreesPerSecond = Mathf.Lerp(SignedSpeedDegreesPerSecond, speed, blend);

            bool inRange = SpeedDegreesPerSecond >= minimumSpeed && SpeedDegreesPerSecond <= maximumSpeed;
            if (inRange)
            {
                SecondsInRange += deltaSeconds;
                SecondsOutOfRange = 0f;
            }
            else
            {
                SecondsOutOfRange += deltaSeconds;
                SecondsInRange = 0f;
            }

            if (presenting)
            {
                presentationSeconds += deltaSeconds;
                speedTimesSeconds += SpeedDegreesPerSecond * deltaSeconds;
                PresentationMinSpeed = Mathf.Min(PresentationMinSpeed, SpeedDegreesPerSecond);
                PresentationMaxSpeed = Mathf.Max(PresentationMaxSpeed, SpeedDegreesPerSecond);
            }
            else
            {
                WaitSeconds += deltaSeconds;
            }
        }

        /// <summary>
        /// um wie viel grad sich die Blickrichtung zwischen 2 Frames gedreht hat, nur auf der gewählten Achse mit Vorzeichen
        /// </summary>
        public static float SignedAngleOnAxis(Vector3 from, Vector3 to, HeadVelocityAxis axis)
        {
            if (axis == HeadVelocityAxis.Horizontal)
            {
                // von oben betrachtet: Höhe (y) weglassen damit nur links und rechts bleibt.
                // Gierwinkel jeder Richtung einzeln mit atan2, dann die Differenz.
                // Nicht Vector3.SignedAngle: das rechnet über acos und ist bei den
                // winzigen Winkeln pro Frame (z. B. 0,013° bei 1,2 °/s und 90 Hz) zu ungenau.
                float yawFrom = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
                float yawTo = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                return Mathf.DeltaAngle(yawFrom, yawTo);
            }

            // oben und unten: Unterschied der Höhenwinkel über dem Horizont
            float pitchFrom = Mathf.Asin(Mathf.Clamp(from.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            float pitchTo = Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            return pitchTo - pitchFrom;
        }
    }

    /// <summary>
    /// Kopftempo eines Trials für die CSV. Alles NaN, wenn der Trial keine Erkennung hatte.
    /// </summary>
    public readonly struct HeadVelocitySummary
    {
        public readonly float TargetSpeed;
        public readonly float OnsetSpeed;
        public readonly float MeanSpeed;
        public readonly float MinSpeed;
        public readonly float MaxSpeed;
        public readonly float WaitSeconds;

        public HeadVelocitySummary(float targetSpeed, float onsetSpeed, float meanSpeed,
            float minSpeed, float maxSpeed, float waitSeconds)
        {
            TargetSpeed = targetSpeed;
            OnsetSpeed = onsetSpeed;
            MeanSpeed = meanSpeed;
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
            WaitSeconds = waitSeconds;
        }

        public static HeadVelocitySummary None =>
            new(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
    }
}
