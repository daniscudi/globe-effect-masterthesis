using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Schreibt mit, wie die Links-Rechts-Bewegung im Durchgang verlaufen ist.
    ///
    /// Im normalen Modus "SimulatedYaw" läuft die Bewegung automatisch im Shader.
    /// Dann wird einfach dieser Wert mitgeschrieben.
    ///
    /// Es gibt auch noch den Modus "HeadTracked". Da dreht die Person den Kopf
    /// selbst, und gemessen wird, wie weit sie sich vom Startpunkt weggedreht hat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomDotHeadSweepMonitor : MonoBehaviour
    {
        // Dieses Skript bewegt selbst gar nichts. Es schaut nur zu und zählt,
        // wie oft der linke und der rechte Rand abwechselnd erreicht wurden.
        [Header("References")]
        [SerializeField]
        private Transform observer;

        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [Header("Motion Log")]
        [FormerlySerializedAs("yawThresholdDegrees")]
        [SerializeField, Range(0.5f, 45f)]
        [Tooltip("Ab wie vielen Grad zu einer Seite das als Rand zählt.")]
        private float turnaroundDegrees = 1.5f;

        [SerializeField, Range(1, 20)]
        [Tooltip("Mindestzahl vollständiger Wechsel zwischen linker und rechter Seite. Der Experiment Manager kann unvollständige Durchgänge wiederholen.")]
        private int requiredHalfSweeps = 1;

        [Header("Runtime Status")]
        [SerializeField]
        private float currentYawDegrees;

        [SerializeField]
        private int completedHalfSweeps;

        [FormerlySerializedAs("maximumAbsoluteYawDegrees")]
        [SerializeField]
        private float maxHeadTurnDegrees;

        [FormerlySerializedAs("meanAbsoluteYawSpeedDegreesPerSecond")]
        [SerializeField]
        private float meanHeadSpeed;

        [FormerlySerializedAs("peakAbsoluteYawSpeedDegreesPerSecond")]
        [SerializeField]
        private float peakHeadSpeed;

        // Die Richtung, in die der Kopf am Anfang des Durchgangs geschaut hat.
        private Vector3 startDirection = Vector3.forward;
        private AlternatingHeadSweepCounter counter;
        private float previousYawDegrees;
        private double previousSampleTime;
        private float accumulatedAbsoluteYawDegrees;
        private float accumulatedMeasurementSeconds;
        private float smoothedAbsoluteYawSpeedDegreesPerSecond;
        private bool hasPreviousYawSample;
        private RandomDotSweepProfileEvaluator profileEvaluator;
        private double profileStartSeconds;
        private double previousProfileElapsedSeconds;
        private bool hasPreviousProfileSample;
        private bool trackingProfile;
        private bool trackingYaw;

        public event Action<int, float> HalfSweepCompleted;

        public float CurrentYawDegrees => currentYawDegrees;
        public int CompletedHalfSweeps => completedHalfSweeps;
        public int RequiredHalfSweeps => requiredHalfSweeps;
        public float YawThresholdDegrees => turnaroundDegrees;
        public float MaximumAbsoluteYawDegrees => maxHeadTurnDegrees;
        public float MeanAbsoluteYawSpeedDegreesPerSecond => meanHeadSpeed;
        public float PeakAbsoluteYawSpeedDegreesPerSecond => peakHeadSpeed;
        public float MinimumYawDegrees => counter?.MinimumYawDegrees ?? 0f;
        public float MaximumYawDegrees => counter?.MaximumYawDegrees ?? 0f;
        public bool RequirementMet => completedHalfSweeps >= requiredHalfSweeps;
        public float ProfileErrorDegrees =>
            profileEvaluator?.RootMeanSquareErrorDegrees ?? float.PositiveInfinity;
        public float FirstExtremeErrorDegrees =>
            profileEvaluator?.FirstExtremeErrorDegrees ?? float.PositiveInfinity;
        public float SecondExtremeErrorDegrees =>
            profileEvaluator?.SecondExtremeErrorDegrees ?? float.PositiveInfinity;

        private void Awake()
        {
            FindReferences();
            ResetForTrial();
        }

        private void Update()
        {
            // Solange nichts zu sehen ist, gibt es auch nichts mitzuschreiben.
            if (!trackingYaw || stimulus == null || !stimulus.IsVisible)
            {
                return;
            }

            // Im simulierten Modus ist dies der programmierte Winkel der gewählten
            // Achse (Yaw oder Pitch). HeadTracked misst weiterhin echten Yaw.
            currentYawDegrees = stimulus.MotionMode == RandomDotMotionMode.SimulatedYaw
                ? stimulus.CurrentSimulatedSweepDegrees
                : MeasureRealHeadYaw();
            UpdateYawSpeed(currentYawDegrees);

            if (trackingProfile && profileEvaluator != null)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - profileStartSeconds;
                float deltaSeconds =
                    hasPreviousProfileSample ? (float)(elapsed - previousProfileElapsedSeconds) : 0f;
                profileEvaluator.AddSample(elapsed, currentYawDegrees, deltaSeconds);
                previousProfileElapsedSeconds = elapsed;
                hasPreviousProfileSample = true;
            }

            counter ??= new AlternatingHeadSweepCounter(turnaroundDegrees);
            bool sweepCompleted = counter.Update(currentYawDegrees);
            maxHeadTurnDegrees = counter.MaximumAbsoluteYawDegrees;
            if (sweepCompleted)
            {
                completedHalfSweeps = counter.CompletedHalfSweeps;
                HalfSweepCompleted?.Invoke(completedHalfSweeps, currentYawDegrees);
            }
        }

        public void ConfigureCriterion(float thresholdDegrees, int halfSweeps)
        {
            turnaroundDegrees = Mathf.Clamp(thresholdDegrees, 0.5f, 45f);
            requiredHalfSweeps = Mathf.Clamp(halfSweeps, 1, 20);
            ResetForTrial();
        }

        public void ResetForTrial()
        {
            // Wohin die Person gerade schaut, gilt ab jetzt als Nullstellung.
            // Alles danach wird als Abweichung von dieser Richtung gemessen.
            FindReferences();
            startDirection = RemoveUpDownTilt(observer != null ? observer.forward : Vector3.forward);
            counter = new AlternatingHeadSweepCounter(turnaroundDegrees);
            currentYawDegrees = 0f;
            completedHalfSweeps = 0;
            maxHeadTurnDegrees = 0f;
            meanHeadSpeed = 0f;
            peakHeadSpeed = 0f;
            previousYawDegrees = 0f;
            previousSampleTime = 0d;
            accumulatedAbsoluteYawDegrees = 0f;
            accumulatedMeasurementSeconds = 0f;
            smoothedAbsoluteYawSpeedDegreesPerSecond = 0f;
            hasPreviousYawSample = false;
            profileEvaluator = null;
            trackingProfile = false;
            trackingYaw = true;
            hasPreviousProfileSample = false;
            previousProfileElapsedSeconds = 0d;
        }

        public void StartProfileTracking(
            float amplitudeDegrees, float speedDegreesPerSecond, RandomDotSweepDirection direction)
        {
            profileEvaluator =
                new RandomDotSweepProfileEvaluator(amplitudeDegrees, speedDegreesPerSecond, direction);
            profileStartSeconds = Time.realtimeSinceStartupAsDouble;
            previousProfileElapsedSeconds = 0d;
            hasPreviousProfileSample = false;
            trackingProfile = true;
        }

        public void StopProfileTracking()
        {
            trackingProfile = false;
            // Nach Ausblenden der Punkte bleibt der graue Kreis sichtbar.
            // Antworten und Pausen dürfen die Bewegungskennwerte nicht ändern.
            trackingYaw = false;
        }

        private void UpdateYawSpeed(float yawDegrees)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!hasPreviousYawSample)
            {
                previousYawDegrees = yawDegrees;
                previousSampleTime = now;
                hasPreviousYawSample = true;
                return;
            }

            float deltaSeconds = (float)(now - previousSampleTime);
            float deltaYaw = Mathf.Abs(Mathf.DeltaAngle(previousYawDegrees, yawDegrees));
            previousYawDegrees = yawDegrees;
            previousSampleTime = now;

            // Große Zeitlücken entstehen etwa beim Pausieren des Editors und
            // dürfen nicht als extrem langsame Kopfbewegung in den Trial eingehen.
            if (deltaSeconds <= 0f || deltaSeconds > 0.25f)
            {
                return;
            }

            accumulatedAbsoluteYawDegrees += deltaYaw;
            accumulatedMeasurementSeconds += deltaSeconds;
            meanHeadSpeed = accumulatedMeasurementSeconds > 1e-5f
                ? accumulatedAbsoluteYawDegrees / accumulatedMeasurementSeconds
                : 0f;

            // Die Spitzengeschwindigkeit wird leicht geglättet (etwa über 0,15 s),
            // damit ein einzelner zittriger Frame nicht als Ruck zählt.
            float instantaneousSpeed = deltaYaw / deltaSeconds;
            float smoothingFactor = 1f - Mathf.Exp(-deltaSeconds / 0.15f);
            smoothedAbsoluteYawSpeedDegreesPerSecond = Mathf.Lerp(
                smoothedAbsoluteYawSpeedDegreesPerSecond, instantaneousSpeed, smoothingFactor);
            peakHeadSpeed = Mathf.Max(peakHeadSpeed, smoothedAbsoluteYawSpeedDegreesPerSecond);
        }

        private float MeasureRealHeadYaw()
        {
            if (observer == null)
            {
                return 0f;
            }

            return YawFromStart(startDirection, observer);
        }

        /// <summary>
        /// Wie weit ist der Kopf seit der Startrichtung nach links oder rechts
        /// gedreht, in Grad? Das Kopftraining im Experiment Manager nutzt das auch.
        /// </summary>
        public static float YawFromStart(Vector3 startDirection, Transform head)
        {
            // SignedAngle gibt links und rechts mit unterschiedlichem Vorzeichen
            // zurück. Genau das brauchen wir hier.
            Vector3 currentForward = RemoveUpDownTilt(head.forward);
            return Vector3.SignedAngle(startDirection, currentForward, Vector3.up);
        }

        private void FindReferences()
        {
            // Sind die Felder im Inspector leer, wird hier selbst gesucht.
            // Erst am eigenen Objekt, dann in der ganzen Szene.
            if (stimulus == null)
            {
                stimulus = GetComponent<RandomDotFieldStimulus>();
            }

            stimulus = UnityTools.FindIfMissing(stimulus);

            if (observer == null && stimulus != null)
            {
                observer = stimulus.Observer;
            }

            if (observer == null)
            {
                observer = UnityTools.MainCameraTransform();
            }
        }

        public static Vector3 RemoveUpDownTilt(Vector3 direction)
        {
            // Ob der Kopf nach oben oder unten geneigt ist, interessiert hier nicht.
            // Uns interessiert nur das Drehen nach links und rechts. Deshalb wird
            // der Höhenanteil einfach auf null gesetzt.
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
        }

        private void OnValidate()
        {
            turnaroundDegrees = Mathf.Clamp(turnaroundDegrees, 0.5f, 45f);
            requiredHalfSweeps = Mathf.Clamp(requiredHalfSweeps, 1, 20);
        }
    }
}
