using System.Collections.Generic;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Live-Anzeige des Kopftempos für den Experiment Monitor, vor allem für den
    /// Pilot: Wie schnell dreht der Kopf, wie schnell laufen die Punkte dabei in der
    /// Bildmitte, und dreht der Kopf gleichmäßig genug, dass die Punkte jetzt erscheinen würden?
    ///
    /// Zusätzlich für den Pilot mit Brille auf: Jeder Schwenk wird erkannt und
    /// gemerkt ("Last Pans"), und per Tastendruck lässt sich das Tempo speichern,
    /// das sich gerade natürlich anfühlt ("Saved Speeds").
    ///
    /// Eigene Messung neben dem Velocity Gate des Trials. Sie läuft immer mit (auch
    /// in der Vorschau) und verändert weder den Ablauf noch die CSV-Statistik.
    /// </summary>
    public sealed class RandomDotLiveHeadSpeed
    {
        // Ruhigerer Wert zum Ablesen: gleitender Mittelwert über etwa eine Sekunde.
        private const float AverageSeconds = 1f;

        // Ab diesem Tempo gilt der Kopf als "in Bewegung" (°/s). Darunter ist es
        // Stillhalten oder leichtes Wackeln.
        public const float MovingThreshold = 0.5f;

        // So lange muss der Kopf unter der Schwelle bleiben, damit ein Schwenk endet.
        private const float StopSeconds = 0.3f;

        // Kürzere Bewegungen zählen nicht als Schwenk (Zucken, Nachjustieren).
        private const float MinimumPanSeconds = 0.5f;

        private const int RememberedPans = 5;

        private readonly RandomDotHeadVelocityGate meter = new();
        private readonly RandomDotSteadyHeadDetector steady = new();
        private readonly List<RandomDotPan> lastPans = new();
        private readonly List<RandomDotSavedSpeed> savedSpeeds = new();
        private float holdSeconds;

        // Laufender Schwenk.
        private int panDirection;
        private float panSeconds;
        private float panSpeedTimesSeconds;
        private float panPeak;
        private float stillSeconds;

        public bool HasData { get; private set; }

        /// <summary>Kopftempo jetzt, gleich geglättet wie beim Velocity Gate (°/s).</summary>
        public float HeadSpeed => meter.SpeedDegreesPerSecond;

        /// <summary>Kopftempo über etwa die letzte Sekunde gemittelt (°/s).</summary>
        public float AverageHeadSpeed { get; private set; }

        /// <summary>Kopftempo, das dem simulierten Schwenk entspricht (nur zum Vergleich, °/s).</summary>
        public float SimulatedHeadSpeed { get; private set; }

        /// <summary>Wie lange der Kopf schon am Stück gleichmäßig dreht (s).</summary>
        public float SteadySeconds => steady.SteadySeconds;

        public bool IsMoving => steady.IsMoving;

        /// <summary>m mal Zoom: so viel schneller als der Kopf laufen die Punkte in der Bildmitte.</summary>
        public float ImageGain { get; private set; } = 1f;

        public float ImageCenterSpeed => HeadSpeed * ImageGain;
        public float AverageImageCenterSpeed => AverageHeadSpeed * ImageGain;

        public float FramesPerSecond { get; private set; }

        /// <summary>Um wie viel Grad ein Punkt in der Bildmitte pro Frame springt.</summary>
        public float ImageStepPerFrameDegrees => FramesPerSecond > 0f ? ImageCenterSpeed / FramesPerSecond : 0f;

        /// <summary>Lange genug gleichmäßig: Im Trial würden die Punkte jetzt erscheinen.</summary>
        public bool WouldShowDots => HasData && steady.SteadySeconds >= holdSeconds;

        /// <summary>Die letzten abgeschlossenen Schwenks, der neueste zuerst.</summary>
        public IReadOnlyList<RandomDotPan> LastPans => lastPans;

        /// <summary>Alle gespeicherten "fühlt sich natürlich an"-Werte, der älteste zuerst.</summary>
        public IReadOnlyList<RandomDotSavedSpeed> SavedSpeeds => savedSpeeds;

        /// <summary>Messung neu anfangen. Gemerkte Schwenks und gespeicherte Werte bleiben.</summary>
        public void Reset()
        {
            meter.Reset();
            steady.Reset();
            HasData = false;
            AverageHeadSpeed = 0f;
            FramesPerSecond = 0f;
            ResetPan();
        }

        public void ClearHistory()
        {
            lastPans.Clear();
            savedSpeeds.Clear();
        }

        /// <summary>
        /// Speichert das Kopftempo der letzten Sekunde als "fühlt sich natürlich an".
        /// Falsch, solange es noch keine Messung gibt oder der Kopf stillsteht.
        /// </summary>
        public bool TrySaveSpeed(out RandomDotSavedSpeed saved)
        {
            saved = new RandomDotSavedSpeed(AverageHeadSpeed, ImageGain);
            if (!HasData || AverageHeadSpeed < MovingThreshold) return false;
            savedSpeeds.Add(saved);
            return true;
        }

        public void AddSample(Vector3 headForward, float deltaSeconds, HeadVelocityAxis axis,
            float smoothingSeconds, float simulatedHeadSpeed, float steadyTolerance, float minimumMovingSpeed,
            float holdSecondsBeforeDots, float imageGain)
        {
            SimulatedHeadSpeed = simulatedHeadSpeed;
            holdSeconds = holdSecondsBeforeDots;
            ImageGain = imageGain;

            meter.AddSample(headForward, deltaSeconds, axis, smoothingSeconds, 0f, float.MaxValue);
            if (deltaSeconds <= 0f) return;

            float blend = 1f - Mathf.Exp(-deltaSeconds / AverageSeconds);
            if (!HasData)
            {
                // Erster Frame: noch kein Tempo, nur die Bildrate anfangen.
                HasData = true;
                FramesPerSecond = 1f / deltaSeconds;
                return;
            }
            steady.Add(HeadSpeed, deltaSeconds, steadyTolerance, minimumMovingSpeed);
            AverageHeadSpeed = Mathf.Lerp(AverageHeadSpeed, HeadSpeed, blend);
            FramesPerSecond = Mathf.Lerp(FramesPerSecond, 1f / deltaSeconds, blend);
            UpdatePan(deltaSeconds);
        }

        // Ein Schwenk beginnt, sobald der Kopf schneller als die Schwelle dreht, und
        // endet beim Stillhalten oder bei einem Richtungswechsel. Hin und her sind
        // also zwei Schwenks; die langsamen Wendepunkte zählen nicht mit.
        private void UpdatePan(float deltaSeconds)
        {
            float signed = meter.SignedSpeedDegreesPerSecond;
            bool moving = Mathf.Abs(signed) >= MovingThreshold;
            int direction = signed > 0f ? 1 : -1;

            if (moving && panSeconds > 0f && direction != panDirection) EndPan();

            if (moving)
            {
                panDirection = direction;
                panSeconds += deltaSeconds;
                panSpeedTimesSeconds += HeadSpeed * deltaSeconds;
                panPeak = Mathf.Max(panPeak, HeadSpeed);
                stillSeconds = 0f;
            }
            else if (panSeconds > 0f)
            {
                stillSeconds += deltaSeconds;
                if (stillSeconds >= StopSeconds) EndPan();
            }
        }

        private void EndPan()
        {
            if (panSeconds >= MinimumPanSeconds)
            {
                lastPans.Insert(0, new RandomDotPan(panSpeedTimesSeconds / panSeconds, panPeak, panSeconds,
                    panDirection, ImageGain));
                if (lastPans.Count > RememberedPans) lastPans.RemoveAt(lastPans.Count - 1);
            }
            ResetPan();
        }

        private void ResetPan()
        {
            panDirection = 0;
            panSeconds = 0f;
            panSpeedTimesSeconds = 0f;
            panPeak = 0f;
            stillSeconds = 0f;
        }
    }

    /// <summary>Ein abgeschlossener Schwenk in eine Richtung.</summary>
    public readonly struct RandomDotPan
    {
        public readonly float MeanHeadSpeed;
        public readonly float PeakHeadSpeed;
        public readonly float Seconds;
        /// <summary>+1 = nach rechts bzw. oben, -1 = nach links bzw. unten.</summary>
        public readonly int Direction;
        public readonly float ImageGain;

        public RandomDotPan(float meanHeadSpeed, float peakHeadSpeed, float seconds, int direction, float imageGain)
        {
            MeanHeadSpeed = meanHeadSpeed;
            PeakHeadSpeed = peakHeadSpeed;
            Seconds = seconds;
            Direction = direction;
            ImageGain = imageGain;
        }

        public float MeanImageSpeed => MeanHeadSpeed * ImageGain;
    }

    /// <summary>Ein per Tastendruck gespeichertes Tempo, das sich natürlich anfühlte.</summary>
    public readonly struct RandomDotSavedSpeed
    {
        public readonly float HeadSpeed;
        public readonly float ImageGain;

        public RandomDotSavedSpeed(float headSpeed, float imageGain)
        {
            HeadSpeed = headSpeed;
            ImageGain = imageGain;
        }

        public float ImageSpeed => HeadSpeed * ImageGain;
    }
}
