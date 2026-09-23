using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Passt auf, ob die Person wirklich auf das Kreuz in der Mitte schaut.
    ///
    /// Das Schachbrett hängt am Kopf und wirkt unendlich weit weg. Deshalb wird
    /// hier auch nur der Winkel zwischen Blickrichtung und Kreuz gemessen. Eine
    /// Entfernung spielt dabei überhaupt keine Rolle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckerboardFixationMonitor : MonoBehaviour
    {
        // Für jeden neuen Messwert läuft immer dasselbe ab:
        // das richtige Auge aussuchen -> Winkel zum Kreuz ausrechnen -> schauen,
        // ob der Winkel klein genug ist -> mitzählen, wie lange das schon so geht.
        //
        // Rohdaten schreibt dieses Skript keine. Das macht die EyeTrackingToolbox.
        [Header("References")]
        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private VrCheckerboardStimulus stimulus;

        [Header("Fixation Criterion")]
        [SerializeField, Range(0.1f, 15f)]
        [Tooltip("Wie weit der Blick höchstens vom Kreuz weg sein darf, in Grad.")]
        private float toleranceDegrees = 3f;

        [FormerlySerializedAs("requiredContinuousSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("Wie lange der Blick am Stück ruhig liegen muss, in Sekunden.")]
        private float requiredSteadySeconds = 0.3f;

        [Header("Runtime Status")]
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

        // Zeitstempel vom letzten Messwert. Daraus wird der Abstand zum nächsten
        // ausgerechnet, um die Fixationsdauer zusammenzuzählen.
        private double lastSampleTimestamp;
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
            continuousFixationSeconds >= requiredSteadySeconds;
        public float CurrentAngleDegrees => currentAngleDegrees;
        public float ContinuousFixationSeconds => continuousFixationSeconds;
        public float ToleranceDegrees => toleranceDegrees;
        public float RequiredContinuousSeconds => requiredSteadySeconds;
        public double LastSampleRealtimeSeconds => lastSampleRealtimeSeconds;
        public float ValidSampleFraction => totalSampleCount > 0
            ? (float)validSampleCount / totalSampleCount
            : 0f;

        public bool HasRecentSample(float maximumAgeSeconds)
        {
            // Kommen gerade gar keine Daten mehr, darf der letzte alte Wert nicht
            // so tun, als würde die Person immer noch brav auf das Kreuz schauen.
            if (lastSampleRealtimeSeconds <= 0d)
            {
                return false;
            }

            return Time.realtimeSinceStartupAsDouble - lastSampleRealtimeSeconds <=
                Mathf.Max(0f, maximumAgeSeconds);
        }

        private void OnEnable()
        {
            FindReferences();
            Subscribe();
        }

        private void Start()
        {
            // Noch einmal dasselbe wie in OnEnable. Beim ersten Start ist die
            // Toolbox manchmal noch nicht fertig, dann klappt es hier.
            FindReferences();
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
            // Erst abmelden, dann die neuen Sachen eintragen, dann wieder anmelden.
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
            // Vor jedem Durchgang fängt die Messung wieder bei null an.
            currentSampleValid = false;
            isInsideTolerance = false;
            currentAngleDegrees = float.NaN;
            continuousFixationSeconds = 0f;
            lastSampleTimestamp = 0d;
            lastSampleRealtimeSeconds = 0d;
            validSampleCount = 0;
            totalSampleCount = 0;
        }

        private void HandleGazeData(GazeData gazeData)
        {
            // Die Toolbox ruft das hier bei jedem neuen Messwert auf.
            lastSampleRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            totalSampleCount++;
            bool previousState = isInsideTolerance;

            // Ohne Stimulus oder ohne brauchbares Auge geht hier nichts weiter.
            if (stimulus == null || !PickEyeRay(gazeData, out Ray gazeRay))
            {
                MarkSampleUnusable(gazeData.unityTimestamp);
                TellOthersIfChanged(previousState);
                return;
            }

            // Beide Augen schauen parallel geradeaus. Deshalb zählt nur, in welche
            // Richtung geschaut wird. Wo der Blickstrahl anfängt, ist egal.
            Vector3 targetDirection = stimulus.FixationDirectionWorld;
            if (targetDirection.sqrMagnitude <= 1e-8f)
            {
                MarkSampleUnusable(gazeData.unityTimestamp);
                TellOthersIfChanged(previousState);
                return;
            }

            currentSampleValid = true;
            validSampleCount++;
            currentAngleDegrees = Vector3.Angle(
                gazeRay.direction,
                targetDirection.normalized);

            // "Auf dem Kreuz" heißt genau eine Sache: Der Winkel ist klein genug.
            isInsideTolerance = currentAngleDegrees <= toleranceDegrees;

            // Wie viel Zeit ist seit dem letzten Messwert vergangen?
            double sampleInterval = lastSampleTimestamp > 0d
                ? gazeData.unityTimestamp - lastSampleTimestamp
                : 0d;
            lastSampleTimestamp = gazeData.unityTimestamp;

            // Die Fixationsdauer wächst nur, wenn der Blick auch vorher schon auf
            // dem Kreuz lag und die Messwerte dicht genug beieinander liegen.
            //
            // Die Grenze von 100 ms ist mit Absicht streng: War länger nichts da,
            // wissen wir für diese Zeit gar nichts und tun lieber nicht so, als
            // hätte die Person durchgehend geschaut. In jedem anderen Fall fängt
            // die Zählung wieder bei null an.
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

            TellOthersIfChanged(previousState);
        }

        private bool PickEyeRay(GazeData gazeData, out Ray gazeRay)
        {
            // Wird das Bild nur einem Auge gezeigt, muss auch genau dieses Auge
            // kontrolliert werden. Der gemittelte Strahl aus beiden Augen passt
            // nur, wenn beide Augen das Bild sehen.
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

        private void MarkSampleUnusable(double sampleTime)
        {
            // Fehlen die Daten oder taugen sie nichts, ist die Fixation unterbrochen
            // und der Zähler fängt wieder von vorne an.
            currentSampleValid = false;
            isInsideTolerance = false;
            currentAngleDegrees = float.NaN;
            continuousFixationSeconds = 0f;
            lastSampleTimestamp = sampleTime;
        }

        private void TellOthersIfChanged(bool previousState)
        {
            // Nur bei einem echten Wechsel Bescheid geben, nicht bei jedem Messwert.
            if (previousState != isInsideTolerance)
            {
                FixationStateChanged?.Invoke(isInsideTolerance);
            }
        }

        private void FindReferences()
        {
            // Sind die Felder im Inspector leer, wird hier selbst gesucht.
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
            // Ab jetzt bekommt dieses Skript jeden neuen Messwert von der Toolbox.
            if (subscribed || eyeTrackingToolbox == null)
            {
                return;
            }

            eyeTrackingToolbox.GazeDataAvailable += HandleGazeData;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            // Wieder abmelden, sonst kommt derselbe Messwert später doppelt an.
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
            requiredSteadySeconds = Mathf.Max(0f, requiredSteadySeconds);
        }
    }
}
