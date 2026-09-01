using System;
using System.Collections.Generic;
using UnityEngine;
using Varjo.XR;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Varjo-Implementierung der im Lab verwendeten IEyeTracker-Schnittstelle.
    /// GetGazeList wird in jedem Unity-Frame geleert, damit auch bei 200 Hz kein
    /// Sample nur wegen einer niedrigeren Render-Framerate verloren geht.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VarjoEyeTracker : MonoBehaviour, IEyeTracker
    {
        private VarjoEyeTracking.GazeCalibrationMode calibrationMode =
            VarjoEyeTracking.GazeCalibrationMode.Fast;
        private VarjoEyeTracking.GazeOutputFilterType outputFilterType =
            VarjoEyeTracking.GazeOutputFilterType.Standard;
        private VarjoEyeTracking.GazeOutputFrequency outputFrequency =
            VarjoEyeTracking.GazeOutputFrequency.MaximumSupported;

        private bool initialized;
        private bool listening;
        private bool apiFailureReported;
        private bool streamSettingsApplied;
        private bool streamSettingsWarningReported;
        private GazeData currentGazeData;

        public void Configure(
            VarjoEyeTracking.GazeCalibrationMode newCalibrationMode,
            VarjoEyeTracking.GazeOutputFilterType newOutputFilterType,
            VarjoEyeTracking.GazeOutputFrequency newOutputFrequency)
        {
            calibrationMode = newCalibrationMode;
            outputFilterType = newOutputFilterType;
            outputFrequency = newOutputFrequency;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            TryApplyStreamSettings();
        }

        public void Calibrate()
        {
            try
            {
                if (!VarjoEyeTracking.IsGazeAllowed())
                {
                    Debug.LogWarning(
                        "Varjo Eye Tracking ist in Varjo Base nicht freigegeben.",
                        this);
                    return;
                }

                if (!VarjoEyeTracking.RequestGazeCalibration(calibrationMode))
                {
                    Debug.LogWarning(
                        "Varjo-Blickkalibrierung konnte nicht gestartet werden.",
                        this);
                }
            }
            catch (Exception exception)
            {
                ReportApiFailure("Kalibrierung", exception);
            }
        }

        public GazeData GetGazeData()
        {
            return currentGazeData;
        }

        public void StartListening()
        {
            if (!initialized)
            {
                Initialize();
            }

            if (!streamSettingsApplied)
            {
                TryApplyStreamSettings();
            }

            listening = true;
        }

        public void StopListening()
        {
            listening = false;
        }

        private void Update()
        {
            if (!listening)
            {
                return;
            }

            try
            {
                if (!VarjoEyeTracking.IsGazeAllowed() ||
                    !VarjoEyeTracking.IsGazeCalibrated())
                {
                    return;
                }

                // GetGazeList liefert alle seit der letzten Abfrage gepufferten
                // Samples. GetGaze würde bei hoher Abtastrate Zwischenwerte verlieren.
                int sampleCount = VarjoEyeTracking.GetGazeList(
                    out List<VarjoEyeTracking.GazeData> gazeSamples,
                    out List<VarjoEyeTracking.EyeMeasurements> measurements);

                for (int i = 0; i < sampleCount; i++)
                {
                    VarjoEyeTracking.EyeMeasurements eyeMeasurements =
                        i < measurements.Count ? measurements[i] : default;

                    currentGazeData = ConvertSample(gazeSamples[i], eyeMeasurements);
                    EyeTrackingEvent.TriggerEvent(currentGazeData);
                }
            }
            catch (Exception exception)
            {
                ReportApiFailure("Datenabfrage", exception);
            }
        }

        private void TryApplyStreamSettings()
        {
            try
            {
                bool filterSet = VarjoEyeTracking.SetGazeOutputFilterType(
                    outputFilterType);
                bool frequencySet = VarjoEyeTracking.SetGazeOutputFrequency(
                    outputFrequency);

                streamSettingsApplied = filterSet && frequencySet;
                if (!streamSettingsApplied && !streamSettingsWarningReported)
                {
                    streamSettingsWarningReported = true;
                    Debug.LogWarning(
                        "Varjo Eye Tracking ist noch nicht bereit; die Stream-Einstellungen werden von Varjo Base bestimmt.",
                        this);
                }
            }
            catch (Exception exception)
            {
                ReportApiFailure("Initialisierung", exception);
            }
        }

        private static GazeData ConvertSample(
            VarjoEyeTracking.GazeData sample,
            VarjoEyeTracking.EyeMeasurements measurements)
        {
            // Die Rohstatuswerte bleiben zusätzlich erhalten. Die booleschen Werte
            // sind die unmittelbar verwendbare Lab-Definition für gültige Strahlen.
            bool combinedValid = sample.status == VarjoEyeTracking.GazeStatus.Valid;

            return new GazeData
            {
                frameNumber = sample.frameNumber,
                deviceTimestamp = sample.captureTime,
                leftRayLocal = new Ray(sample.left.origin, sample.left.forward),
                rightRayLocal = new Ray(sample.right.origin, sample.right.forward),
                combinedRayLocal = new Ray(sample.gaze.origin, sample.gaze.forward),
                gazeDistance = sample.focusDistance,
                interPupillaryDistanceMillimeters =
                    measurements.interPupillaryDistanceInMM,
                leftPupilDiameter = measurements.leftPupilDiameterInMM,
                rightPupilDiameter = measurements.rightPupilDiameterInMM,
                leftEyeOpenness = measurements.leftEyeOpenness,
                rightEyeOpenness = measurements.rightEyeOpenness,
                combinedValidity = combinedValid,
                leftValidity = combinedValid &&
                    sample.leftStatus >= VarjoEyeTracking.GazeEyeStatus.Compensated,
                rightValidity = combinedValid &&
                    sample.rightStatus >= VarjoEyeTracking.GazeEyeStatus.Compensated,
                trackingStatus = (int)sample.status,
                leftTrackingStatus = (int)sample.leftStatus,
                rightTrackingStatus = (int)sample.rightStatus
            };
        }

        private void ReportApiFailure(string operation, Exception exception)
        {
            if (apiFailureReported)
            {
                return;
            }

            apiFailureReported = true;
            Debug.LogWarning(
                $"Varjo Eye Tracking: {operation} nicht verfügbar. " +
                $"Der Checkerboard-Stimulus läuft weiter. {exception.Message}",
                this);
        }
    }
}
