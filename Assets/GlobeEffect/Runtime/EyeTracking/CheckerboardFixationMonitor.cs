using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Uebertraegt die Fixationskontrolle aus dem Lab-Projekt auf den aktuellen
    /// Checkerboard-Mittelpunkt. Es gibt keine fest codierte Weltposition oder
    /// Distanz; auch der aktive mono-/binokulare Augenmodus wird beruecksichtigt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckerboardFixationMonitor : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private VrCheckerboardStimulus stimulus;

        [Header("Fixationskriterium")]
        [SerializeField, Range(0.1f, 15f)]
        [Tooltip("Maximaler Winkel zwischen Blickstrahl und Fixationsziel.")]
        private float toleranceDegrees = 3f;

        [SerializeField, Min(0f)]
        [Tooltip("Erforderliche ununterbrochene Fixationsdauer in Sekunden.")]
        private float requiredContinuousSeconds = 0.3f;

        [Header("Laufzeitstatus")]
        [SerializeField]
        private bool currentSampleValid;

        [SerializeField]
        private bool isInsideTolerance;

        [SerializeField]
        private float currentAngleDegrees = float.NaN;

        [SerializeField]
        private float continuousFixationSeconds;

        private double previousSampleTime;
        private bool subscribed;

        public event Action<bool> FixationStateChanged;

        public bool CurrentSampleValid => currentSampleValid;
        public bool IsInsideTolerance => isInsideTolerance;
        public bool RequirementMet => currentSampleValid && isInsideTolerance &&
            continuousFixationSeconds >= requiredContinuousSeconds;
        public float CurrentAngleDegrees => currentAngleDegrees;
        public float ContinuousFixationSeconds => continuousFixationSeconds;

        private void OnEnable()
        {
            TryResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            TryResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            EyeTrackingToolbox toolbox,
            VrCheckerboardStimulus checkerboardStimulus)
        {
            Unsubscribe();
            eyeTrackingToolbox = toolbox;
            stimulus = checkerboardStimulus;
            if (Application.isPlaying)
            {
                Subscribe();
            }
        }

        public void ResetFixationWindow()
        {
            currentSampleValid = false;
            isInsideTolerance = false;
            currentAngleDegrees = float.NaN;
            continuousFixationSeconds = 0f;
            previousSampleTime = 0d;
        }

        private void HandleGazeData(GazeData gazeData)
        {
            bool previousState = isInsideTolerance;
            if (stimulus == null || !TrySelectGazeRay(gazeData, out Ray gazeRay))
            {
                ResetInvalidSample(gazeData.unityTimestamp);
                NotifyIfStateChanged(previousState);
                return;
            }

            Vector3 targetDirection = stimulus.transform.position - gazeRay.origin;
            if (targetDirection.sqrMagnitude <= 1e-8f)
            {
                ResetInvalidSample(gazeData.unityTimestamp);
                NotifyIfStateChanged(previousState);
                return;
            }

            currentSampleValid = true;
            currentAngleDegrees = Vector3.Angle(
                gazeRay.direction,
                targetDirection.normalized);
            isInsideTolerance = currentAngleDegrees <= toleranceDegrees;

            double sampleInterval = previousSampleTime > 0d
                ? gazeData.unityTimestamp - previousSampleTime
                : 0d;
            previousSampleTime = gazeData.unityTimestamp;

            // Lange Datenluecken duerfen nicht als kontinuierliche Fixation
            // gewertet werden. 100 ms ist bewusst konservativ gewaehlt.
            if (isInsideTolerance && previousState &&
                sampleInterval >= 0d && sampleInterval <= 0.1d)
            {
                continuousFixationSeconds += (float)sampleInterval;
            }
            else if (isInsideTolerance)
            {
                continuousFixationSeconds = 0f;
            }
            else
            {
                continuousFixationSeconds = 0f;
            }

            NotifyIfStateChanged(previousState);
        }

        private bool TrySelectGazeRay(GazeData gazeData, out Ray gazeRay)
        {
            switch (stimulus.EyePresentation)
            {
                case CheckerboardEyePresentation.LeftEyeOnly:
                    gazeRay = gazeData.leftRayWorld;
                    return gazeData.leftValidity;

                case CheckerboardEyePresentation.RightEyeOnly:
                    gazeRay = gazeData.rightRayWorld;
                    return gazeData.rightValidity;

                default:
                    gazeRay = gazeData.combinedRayWorld;
                    return gazeData.combinedValidity;
            }
        }

        private void ResetInvalidSample(double sampleTime)
        {
            currentSampleValid = false;
            isInsideTolerance = false;
            currentAngleDegrees = float.NaN;
            continuousFixationSeconds = 0f;
            previousSampleTime = sampleTime;
        }

        private void NotifyIfStateChanged(bool previousState)
        {
            if (previousState != isInsideTolerance)
            {
                FixationStateChanged?.Invoke(isInsideTolerance);
            }
        }

        private void TryResolveReferences()
        {
            if (eyeTrackingToolbox == null)
            {
                eyeTrackingToolbox = EyeTrackingToolbox.Instance;
            }

            if (stimulus == null)
            {
                stimulus = FindAnyObjectByType<VrCheckerboardStimulus>();
            }
        }

        private void Subscribe()
        {
            if (subscribed || eyeTrackingToolbox == null)
            {
                return;
            }

            eyeTrackingToolbox.GazeDataAvailable += HandleGazeData;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || eyeTrackingToolbox == null)
            {
                subscribed = false;
                return;
            }

            eyeTrackingToolbox.GazeDataAvailable -= HandleGazeData;
            subscribed = false;
        }

        private void OnValidate()
        {
            toleranceDegrees = Mathf.Clamp(toleranceDegrees, 0.1f, 15f);
            requiredContinuousSeconds = Mathf.Max(0f, requiredContinuousSeconds);
        }
    }
}
