using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Live-Anzeige des Kopftempos für den Experiment Monitor, vor allem für den
    /// Pilot: Wie schnell dreht der Kopf, wie schnell laufen die Punkte dabei in der
    /// Bildmitte, und würde die Geschwindigkeitserkennung die Punkte jetzt zeigen?
    ///
    /// Eigene Messung neben dem Velocity Gate des Trials. Sie läuft immer mit (auch
    /// in der Vorschau) und verändert weder den Ablauf noch die CSV-Statistik.
    /// </summary>
    public sealed class RandomDotLiveHeadSpeed
    {
        // Ruhigerer Wert zum Ablesen: gleitender Mittelwert über etwa eine Sekunde.
        private const float AverageSeconds = 1f;

        private readonly RandomDotHeadVelocityGate meter = new();
        private float holdSeconds;

        public bool HasData { get; private set; }

        /// <summary>Kopftempo jetzt, gleich geglättet wie beim Velocity Gate (°/s).</summary>
        public float HeadSpeed => meter.SpeedDegreesPerSecond;

        /// <summary>Kopftempo über etwa die letzte Sekunde gemittelt (°/s).</summary>
        public float AverageHeadSpeed { get; private set; }

        public float TargetHeadSpeed { get; private set; }
        public float MinimumHeadSpeed { get; private set; }
        public float MaximumHeadSpeed { get; private set; }

        /// <summary>m mal Zoom: so viel schneller als der Kopf laufen die Punkte in der Bildmitte.</summary>
        public float ImageGain { get; private set; } = 1f;

        public float ImageCenterSpeed => HeadSpeed * ImageGain;
        public float AverageImageCenterSpeed => AverageHeadSpeed * ImageGain;

        public float FramesPerSecond { get; private set; }

        /// <summary>Um wie viel Grad ein Punkt in der Bildmitte pro Frame springt.</summary>
        public float ImageStepPerFrameDegrees => FramesPerSecond > 0f ? ImageCenterSpeed / FramesPerSecond : 0f;

        public bool TooSlow => HeadSpeed < MinimumHeadSpeed;
        public bool TooFast => HeadSpeed > MaximumHeadSpeed;

        /// <summary>Lange genug im richtigen Tempo: Der Velocity Gate würde die Punkte jetzt zeigen.</summary>
        public bool WouldShowDots => HasData && !TooSlow && !TooFast && meter.SecondsInRange >= holdSeconds;

        public void Reset()
        {
            meter.Reset();
            HasData = false;
            AverageHeadSpeed = 0f;
            FramesPerSecond = 0f;
        }

        public void AddSample(Vector3 headForward, float deltaSeconds, HeadVelocityAxis axis,
            float smoothingSeconds, float targetHeadSpeed, float tolerance, float holdSecondsBeforeDots,
            float imageGain)
        {
            TargetHeadSpeed = targetHeadSpeed;
            MinimumHeadSpeed = targetHeadSpeed * (1f - tolerance);
            MaximumHeadSpeed = targetHeadSpeed * (1f + tolerance);
            holdSeconds = holdSecondsBeforeDots;
            ImageGain = imageGain;

            meter.AddSample(headForward, deltaSeconds, axis, smoothingSeconds, MinimumHeadSpeed, MaximumHeadSpeed);
            if (deltaSeconds <= 0f) return;

            float blend = 1f - Mathf.Exp(-deltaSeconds / AverageSeconds);
            if (!HasData)
            {
                // Erster Frame: noch kein Tempo, nur die Bildrate anfangen.
                HasData = true;
                FramesPerSecond = 1f / deltaSeconds;
                return;
            }
            AverageHeadSpeed = Mathf.Lerp(AverageHeadSpeed, HeadSpeed, blend);
            FramesPerSecond = Mathf.Lerp(FramesPerSecond, 1f / deltaSeconds, blend);
        }
    }
}
