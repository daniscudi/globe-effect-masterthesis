using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Misst horizontale Kopfbewegungen relativ zur Trial-Startpose. Im
    /// simulierten Debug-Modus wird stattdessen der technische Shader-Schwenk
    /// ausgewertet. Dadurch bleibt die Trialsteuerung fuer beide Modi gleich.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomDotHeadSweepMonitor : MonoBehaviour
    {
        [Header("Referenzen")]
        [SerializeField]
        private Transform observer;

        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [Header("Schwenkkriterium")]
        [SerializeField, Range(0.5f, 45f)]
        [Tooltip("Gierwinkel je Seite, ab dem eine linke/rechte Extremposition gilt.")]
        private float yawThresholdDegrees = 2.5f;

        [SerializeField, Range(1, 20)]
        [Tooltip("Erforderliche Wechsel zwischen den beiden Seiten vor der Antwort.")]
        private int requiredHalfSweeps = 4;

        [Header("Laufzeitstatus")]
        [SerializeField]
        private float currentYawDegrees;

        [SerializeField]
        private int completedHalfSweeps;

        [SerializeField]
        private float maximumAbsoluteYawDegrees;

        private Vector3 referenceForward = Vector3.forward;
        private AlternatingHeadSweepCounter counter;

        public event Action<int, float> HalfSweepCompleted;

        public float CurrentYawDegrees => currentYawDegrees;
        public int CompletedHalfSweeps => completedHalfSweeps;
        public int RequiredHalfSweeps => requiredHalfSweeps;
        public float YawThresholdDegrees => yawThresholdDegrees;
        public float MaximumAbsoluteYawDegrees => maximumAbsoluteYawDegrees;
        public float MinimumYawDegrees => counter?.MinimumYawDegrees ?? 0f;
        public float MaximumYawDegrees => counter?.MaximumYawDegrees ?? 0f;
        public bool RequirementMet => completedHalfSweeps >= requiredHalfSweeps;

        private void Awake()
        {
            ResolveReferences();
            ResetForTrial();
        }

        private void Update()
        {
            if (stimulus == null || !stimulus.IsVisible)
            {
                return;
            }

            currentYawDegrees = stimulus.MotionMode == RandomDotMotionMode.SimulatedYaw
                ? stimulus.CurrentSimulatedYawDegrees
                : CalculateTrackedYaw();

            counter ??= new AlternatingHeadSweepCounter(yawThresholdDegrees);
            if (counter.Update(currentYawDegrees))
            {
                completedHalfSweeps = counter.CompletedHalfSweeps;
                maximumAbsoluteYawDegrees = counter.MaximumAbsoluteYawDegrees;
                HalfSweepCompleted?.Invoke(completedHalfSweeps, currentYawDegrees);
            }
            else
            {
                maximumAbsoluteYawDegrees = counter.MaximumAbsoluteYawDegrees;
            }
        }

        public void Configure(
            Transform observerTransform,
            RandomDotFieldStimulus randomDotStimulus)
        {
            observer = observerTransform;
            stimulus = randomDotStimulus;
            ResetForTrial();
        }

        public void ConfigureCriterion(float thresholdDegrees, int halfSweeps)
        {
            yawThresholdDegrees = Mathf.Clamp(thresholdDegrees, 0.5f, 45f);
            requiredHalfSweeps = Mathf.Clamp(halfSweeps, 1, 20);
            ResetForTrial();
        }

        public void ResetForTrial()
        {
            ResolveReferences();
            referenceForward = FlattenForward(
                observer != null ? observer.forward : Vector3.forward);
            counter = new AlternatingHeadSweepCounter(yawThresholdDegrees);
            currentYawDegrees = 0f;
            completedHalfSweeps = 0;
            maximumAbsoluteYawDegrees = 0f;
        }

        private float CalculateTrackedYaw()
        {
            if (observer == null)
            {
                return 0f;
            }

            Vector3 currentForward = FlattenForward(observer.forward);
            return Vector3.SignedAngle(
                referenceForward,
                currentForward,
                Vector3.up);
        }

        private void ResolveReferences()
        {
            if (stimulus == null)
            {
                stimulus = GetComponent<RandomDotFieldStimulus>();
                stimulus ??= FindAnyObjectByType<RandomDotFieldStimulus>();
            }

            if (observer == null)
            {
                observer = stimulus != null ? stimulus.Observer : null;
                Camera mainCamera = Camera.main;
                observer ??= mainCamera != null ? mainCamera.transform : null;
            }
        }

        private static Vector3 FlattenForward(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-8f
                ? direction.normalized
                : Vector3.forward;
        }

        private void OnValidate()
        {
            yawThresholdDegrees = Mathf.Clamp(yawThresholdDegrees, 0.5f, 45f);
            requiredHalfSweeps = Mathf.Clamp(requiredHalfSweeps, 1, 20);
        }
    }
}
