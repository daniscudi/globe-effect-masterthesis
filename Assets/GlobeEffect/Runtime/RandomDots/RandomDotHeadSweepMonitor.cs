using UnityEngine;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Schreibt mit, wie die Links-Rechts-Bewegung im Durchgang verlaufen ist.
    ///
    /// Im Modus "SimulatedYaw" läuft die Bewegung automatisch im Shader. Dann wird
    /// einfach dieser Wert mitgeschrieben. Im Modus "HeadTracked" dreht die Person
    /// den Kopf selbst, und gemessen wird, wie weit sie sich vom Startpunkt
    /// weggedreht hat. Die Werte landen in der CSV (min/max Winkel, Tempo).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomDotHeadSweepMonitor : MonoBehaviour
    {
        // Dieses Skript bewegt selbst gar nichts. Es schaut nur zu.
        [Header("References")]
        [SerializeField]
        private Transform observer;

        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [Header("Current Status")]
        [SerializeField]
        private float currentYawDegrees;

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
        private float minimumYawDegrees;
        private float maximumYawDegrees;
        private float previousYawDegrees;
        private double previousSampleTime;
        private float accumulatedAbsoluteYawDegrees;
        private float accumulatedMeasurementSeconds;
        private float smoothedAbsoluteYawSpeedDegreesPerSecond;
        private bool hasPreviousYawSample;
        private bool trackingYaw;

        public float CurrentYawDegrees => currentYawDegrees;
        public float MaximumAbsoluteYawDegrees => maxHeadTurnDegrees;
        public float MeanAbsoluteYawSpeedDegreesPerSecond => meanHeadSpeed;
        public float PeakAbsoluteYawSpeedDegreesPerSecond => peakHeadSpeed;
        public float MinimumYawDegrees => minimumYawDegrees;
        public float MaximumYawDegrees => maximumYawDegrees;

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
            // Achse (Yaw oder Pitch). HeadTracked misst echten Yaw.
            currentYawDegrees = stimulus.MotionMode == RandomDotMotionMode.SimulatedYaw
                ? stimulus.CurrentSimulatedSweepDegrees
                : MeasureRealHeadYaw();
            UpdateYawSpeed(currentYawDegrees);
            minimumYawDegrees = Mathf.Min(minimumYawDegrees, currentYawDegrees);
            maximumYawDegrees = Mathf.Max(maximumYawDegrees, currentYawDegrees);
            maxHeadTurnDegrees = Mathf.Max(maxHeadTurnDegrees, Mathf.Abs(currentYawDegrees));
        }

        public void ResetForTrial()
        {
            // Wohin die Person gerade schaut, gilt ab jetzt als Nullstellung.
            // Alles danach wird als Abweichung von dieser Richtung gemessen.
            FindReferences();
            startDirection = RemoveUpDownTilt(observer != null ? observer.forward : Vector3.forward);
            currentYawDegrees = 0f;
            minimumYawDegrees = 0f;
            maximumYawDegrees = 0f;
            maxHeadTurnDegrees = 0f;
            meanHeadSpeed = 0f;
            peakHeadSpeed = 0f;
            previousYawDegrees = 0f;
            previousSampleTime = 0d;
            accumulatedAbsoluteYawDegrees = 0f;
            accumulatedMeasurementSeconds = 0f;
            smoothedAbsoluteYawSpeedDegreesPerSecond = 0f;
            hasPreviousYawSample = false;
            trackingYaw = true;
        }

        /// <summary>
        /// Nach dem Ausblenden der Punkte bleibt der graue Kreis sichtbar. Antworten
        /// und Pausen dürfen die Bewegungskennwerte nicht mehr ändern.
        /// </summary>
        public void StopTracking()
        {
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

            // SignedAngle gibt links und rechts mit unterschiedlichem Vorzeichen zurück.
            Vector3 currentForward = RemoveUpDownTilt(observer.forward);
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

        private static Vector3 RemoveUpDownTilt(Vector3 direction)
        {
            // Ob der Kopf nach oben oder unten geneigt ist, interessiert hier nicht.
            // Uns interessiert nur das Drehen nach links und rechts. Deshalb wird
            // der Höhenanteil einfach auf null gesetzt.
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.forward;
        }
    }
}
