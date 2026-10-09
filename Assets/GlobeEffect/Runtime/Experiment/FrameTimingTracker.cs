using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Zählt während einer Darbietung, ob der Rechner jedes Bild rechtzeitig
    /// geschafft hat.
    ///
    /// In VR fällt Überlast oft nicht auf: Das Headset rechnet fehlende Bilder
    /// hoch, die Bewegung ruckelt dann nur leicht. Genau diese Bewegung soll die
    /// Person aber beurteilen. Deshalb steht pro Trial in der CSV, wie viele
    /// Frames zu lange gedauert haben.
    /// </summary>
    public sealed class FrameTimingTracker
    {
        // Ein Frame gilt als ausgefallen, wenn er mehr als das 1,5-Fache der
        // Bildwiederholzeit braucht (bei 90 Hz also mehr als 16,7 statt 11,1 ms).
        public const float SlowFrameFactor = 1.5f;

        private static readonly List<XRDisplaySubsystem> Displays = new();

        public int Frames { get; private set; }
        public int SlowFrames { get; private set; }
        public float MaxFrameMilliseconds { get; private set; } = float.NaN;
        public float ExpectedFrameMilliseconds { get; private set; } = float.NaN;

        public void Reset(float expectedFrameMilliseconds)
        {
            Frames = 0;
            SlowFrames = 0;
            MaxFrameMilliseconds = float.NaN;
            ExpectedFrameMilliseconds = expectedFrameMilliseconds;
        }

        public void CopyFrom(FrameTimingTracker other)
        {
            Frames = other.Frames;
            SlowFrames = other.SlowFrames;
            MaxFrameMilliseconds = other.MaxFrameMilliseconds;
            ExpectedFrameMilliseconds = other.ExpectedFrameMilliseconds;
        }

        public void Add(float deltaSeconds)
        {
            float milliseconds = deltaSeconds * 1000f;
            Frames++;
            MaxFrameMilliseconds = float.IsNaN(MaxFrameMilliseconds)
                ? milliseconds : Mathf.Max(MaxFrameMilliseconds, milliseconds);
            // Ohne bekannte Bildwiederholrate (z. B. Batchmode) wird nichts gezählt.
            if (!float.IsNaN(ExpectedFrameMilliseconds)
                && milliseconds > SlowFrameFactor * ExpectedFrameMilliseconds)
                SlowFrames++;
        }

        /// <summary>Bildwiederholzeit des Headsets, sonst des Monitors; NaN wenn unbekannt.</summary>
        public static float CurrentExpectedFrameMilliseconds()
        {
            SubsystemManager.GetSubsystems(Displays);
            foreach (XRDisplaySubsystem display in Displays)
            {
                if (display.running && display.TryGetDisplayRefreshRate(out float hertz) && hertz > 1f)
                    return 1000f / hertz;
            }

            double screenHertz = Screen.currentResolution.refreshRateRatio.value;
            return screenHertz > 1d ? (float)(1000d / screenHertz) : float.NaN;
        }
    }
}
