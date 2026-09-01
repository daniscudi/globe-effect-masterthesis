using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Einheitliches Datenformat für reale und simulierte Eye Tracker.
    /// Die lokalen Rays liegen im Koordinatensystem der HMD-Kamera; die
    /// World-Rays werden von der EyeTrackingToolbox ergänzt.
    /// </summary>
    [Serializable]
    public struct GazeData
    {
        public long frameNumber;
        public long deviceTimestamp;
        public double unityTimestamp;

        public Ray leftRayLocal;
        public Ray rightRayLocal;
        public Ray combinedRayLocal;

        public Ray leftRayWorld;
        public Ray rightRayWorld;
        public Ray combinedRayWorld;

        public float gazeDistance;
        public float interPupillaryDistanceMillimeters;
        public float leftPupilDiameter;
        public float rightPupilDiameter;
        public float leftEyeOpenness;
        public float rightEyeOpenness;

        public bool combinedValidity;
        public bool leftValidity;
        public bool rightValidity;

        // Die Rohstatuswerte bleiben erhalten, damit spätere Auswertungen
        // strengere oder lockerere Qualitätskriterien anwenden können.
        public int trackingStatus;
        public int leftTrackingStatus;
        public int rightTrackingStatus;
    }

    /// <summary>
    /// Provider-Abstraktion aus der Lab-Toolbox. Experiment- und Logging-Code
    /// bleiben dadurch unabhängig vom verwendeten Headset.
    /// </summary>
    public interface IEyeTracker
    {
        void Initialize();
        void Calibrate();
        GazeData GetGazeData();
        void StartListening();
        void StopListening();
    }
}
