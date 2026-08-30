using System;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Prueft, ob der Blick beim Kopfschwenk auf dem weltfesten roten
    /// Fixationspunkt des Random-Dot-Felds bleibt. Die Augenwahl folgt der
    /// tatsaechlichen mono-/binokularen Darbietung.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomDotFixationMonitor : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [Header("Fixationskriterium")]
        [SerializeField, Range(0.1f, 15f)]
        private float toleranceDegrees = 3f;

        [SerializeField, Min(0f)]
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
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            EyeTrackingToolbox toolbox,
            RandomDotFieldStimulus randomDotStimulus)
        {
            Unsubscribe();
            eyeTrackingToolbox = toolbox;
            stimulus = randomDotStimulus;
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

            if (!stimulus.TryGetRenderedFixationWorldDirection(
                gazeRay.origin,
                out Vector3 targetDirection))
            {
                ResetInvalidSample(gazeData.unityTimestamp);
                NotifyIfStateChanged(previousState);
                return;
            }

            currentSampleValid = true;
            currentAngleDegrees = Vector3.Angle(
                gazeRay.direction,
                targetDirection);
            isInsideTolerance = currentAngleDegrees <= toleranceDegrees;

            double sampleInterval = previousSampleTime > 0d
                ? gazeData.unityTimestamp - previousSampleTime
                : 0d;
            previousSampleTime = gazeData.unityTimestamp;

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

        private void ResolveReferences()
        {
            eyeTrackingToolbox ??= EyeTrackingToolbox.Instance;
            stimulus ??= FindAnyObjectByType<RandomDotFieldStimulus>();
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
