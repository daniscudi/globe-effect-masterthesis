using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Prüft den Blick auf die Mitte des head-locked Checkerboards. Da der
    /// Stimulus als Richtung in unendlicher Entfernung dargestellt wird, wird
    /// auch hier nur die Winkelabweichung von der Center-Eye-Blickrichtung
    /// berechnet. Eine künstliche Entfernung geht nicht mehr in die Prüfung ein.
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

        [SerializeField]
        private int validSampleCount;

        [SerializeField]
        private int totalSampleCount;

        private double previousSampleTime;
        private double lastSampleRealtimeSeconds;
        private bool subscribed;

        public event Action<bool> FixationStateChanged;

        public bool CurrentSampleValid => currentSampleValid;
        public bool IsInsideTolerance => isInsideTolerance;
        public FixationTargetState TargetState =>
            FixationTargetStateResolver.Resolve(
                currentSampleValid,
                isInsideTolerance);
        public bool RequirementMet => currentSampleValid && isInsideTolerance &&
            continuousFixationSeconds >= requiredContinuousSeconds;
        public float CurrentAngleDegrees => currentAngleDegrees;
        public float ContinuousFixationSeconds => continuousFixationSeconds;
        public float ToleranceDegrees => toleranceDegrees;
        public float RequiredContinuousSeconds => requiredContinuousSeconds;
        public double LastSampleRealtimeSeconds => lastSampleRealtimeSeconds;
        public float ValidSampleFraction => totalSampleCount > 0
            ? (float)validSampleCount / totalSampleCount
            : 0f;

        public bool HasRecentSample(float maximumAgeSeconds)
        {
            if (lastSampleRealtimeSeconds <= 0d)
            {
                return false;
            }

            return Time.realtimeSinceStartupAsDouble - lastSampleRealtimeSeconds <=
                Mathf.Max(0f, maximumAgeSeconds);
        }

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
            lastSampleRealtimeSeconds = 0d;
            validSampleCount = 0;
            totalSampleCount = 0;
        }

        private void HandleGazeData(GazeData gazeData)
        {
            lastSampleRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            totalSampleCount++;
            bool previousState = isInsideTolerance;
            if (stimulus == null || !TrySelectGazeRay(gazeData, out Ray gazeRay))
            {
                ResetInvalidSample(gazeData.unityTimestamp);
                NotifyIfStateChanged(previousState);
                return;
            }

            // Beide Augen sollen parallel in die aktuelle Vorwärtsrichtung blicken.
            // Der Ursprung des Blickstrahls ist deshalb für das Ziel nicht wichtig.
            Vector3 targetDirection = stimulus.FixationDirectionWorld;
            if (targetDirection.sqrMagnitude <= 1e-8f)
            {
                ResetInvalidSample(gazeData.unityTimestamp);
                NotifyIfStateChanged(previousState);
                return;
            }

            currentSampleValid = true;
            validSampleCount++;
            currentAngleDegrees = Vector3.Angle(
                gazeRay.direction,
                targetDirection.normalized);
            isInsideTolerance = currentAngleDegrees <= toleranceDegrees;

            double sampleInterval = previousSampleTime > 0d
                ? gazeData.unityTimestamp - previousSampleTime
                : 0d;
            previousSampleTime = gazeData.unityTimestamp;

            // Lange Datenlücken dürfen nicht als kontinuierliche Fixation
            // gewertet werden. 100 ms ist bewusst konservativ gewählt.
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
            // Bei monokularer Darbietung muss dasselbe Auge für die Kontrolle
            // verwendet werden. Nur im binokularen Modus gilt der kombinierte Strahl.
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
