using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Live-Anzeige des Kopftempos für den Experiment Monitor: Wie schnell dreht der
    /// Kopf, wie schnell laufen die Punkte dabei in der Bildmitte, und dreht der Kopf
    /// schnell genug, dass die Punkte jetzt erscheinen würden?
    ///
    /// Eigene Messung neben dem Velocity Gate des Trials. Sie läuft immer mit (auch
    /// in der Vorschau) und verändert weder den Ablauf noch die CSV-Statistik.
    /// </summary>
    public sealed class RandomDotLiveHeadSpeed
    {
        // Ruhigerer Wert zum Ablesen: gleitender Mittelwert über etwa eine Sekunde.
        private const float AverageSeconds = 1f;

        private readonly RandomDotHeadVelocityGate meter = new();
        private float stillBelow;

        public bool HasData { get; private set; }

        /// <summary>Kopftempo jetzt, gleich geglättet wie beim Velocity Gate (°/s).</summary>
        public float HeadSpeed => meter.SpeedDegreesPerSecond;

        /// <summary>Kopftempo über etwa die letzte Sekunde gemittelt (°/s).</summary>
        public float AverageHeadSpeed { get; private set; }

        /// <summary>Kopftempo, das dem simulierten Schwenk entspricht (nur zum Vergleich, °/s).</summary>
        public float SimulatedHeadSpeed { get; private set; }

        /// <summary>Schneller als Head Counts As Still Below (Richtung egal).</summary>
        public bool IsMoving => HasData && HeadSpeed >= stillBelow;

        /// <summary>m mal Zoom: so viel schneller als der Kopf laufen die Punkte in der Bildmitte.</summary>
        public float ImageGain { get; private set; } = 1f;

        public float ImageCenterSpeed => HeadSpeed * ImageGain;
        public float AverageImageCenterSpeed => AverageHeadSpeed * ImageGain;

        public float FramesPerSecond { get; private set; }

        /// <summary>Um wie viel Grad ein Punkt in der Bildmitte pro Frame springt.</summary>
        public float ImageStepPerFrameDegrees => FramesPerSecond > 0f ? ImageCenterSpeed / FramesPerSecond : 0f;

        /// <summary>Schnell genug: Im Trial würden die Punkte jetzt erscheinen (ohne Richtungsregel).</summary>
        public bool WouldShowDots => IsMoving;

        public void Reset()
        {
            meter.Reset();
            HasData = false;
            AverageHeadSpeed = 0f;
            FramesPerSecond = 0f;
        }

        public void AddSample(Vector3 headForward, float deltaSeconds, HeadVelocityAxis axis,
            float smoothingSeconds, float simulatedHeadSpeed, float stillBelowSpeed, float imageGain)
        {
            SimulatedHeadSpeed = simulatedHeadSpeed;
            stillBelow = stillBelowSpeed;
            ImageGain = imageGain;

            meter.AddSample(headForward, deltaSeconds, axis, smoothingSeconds);
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
