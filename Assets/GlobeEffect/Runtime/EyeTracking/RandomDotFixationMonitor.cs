using System;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Passt auf, ob die Person während der Punktbewegung auf dem roten Kreuz bleibt.
    ///
    /// Das Kreuz hängt am Kopf und bewegt sich nicht mit den Punkten mit. Wird das
    /// Bild nur einem Auge gezeigt, wird auch nur dieses Auge kontrolliert.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomDotFixationMonitor : MonoBehaviour
    {
        // Für jeden neuen Messwert läuft immer dasselbe ab:
        // das richtige Auge aussuchen -> Winkel zum roten Kreuz ausrechnen ->
        // mitzählen, wie lange der Blick schon ruhig daraufliegt.
        //
        // Die Rohdaten schreibt dieses Skript nicht. Das macht die Lab-Toolbox.
        [Header("Referenzen")]
        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [Header("Fixationskriterium")]
        [SerializeField, Range(0.1f, 15f)]
        [Tooltip("Wie weit der Blick höchstens vom Kreuz weg sein darf, in Grad.")]
        private float toleranceDegrees = 3f;

        [SerializeField, Min(0f)]
        [Tooltip("Wie lange der Blick am Stück ruhig liegen muss, in Sekunden.")]
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
            continuousFixationSeconds >= requiredContinuousSeconds;
        public float CurrentAngleDegrees => currentAngleDegrees;
        public float ContinuousFixationSeconds => continuousFixationSeconds;
        public float ToleranceDegrees => toleranceDegrees;
        public float RequiredContinuousSeconds => requiredContinuousSeconds;
        public float ValidSampleFraction => totalSampleCount > 0
            ? (float)validSampleCount / totalSampleCount
            : 0f;

        public bool HasRecentSample(float maximumAgeSeconds)
        {
            // Kommen gerade gar keine Daten, darf kein Durchgang starten. Sonst
            // würde man losgehen, ohne zu wissen, wohin die Person schaut.
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
            RandomDotFieldStimulus randomDotStimulus)
        {
            // Erst abmelden, dann die neuen Sachen eintragen, dann wieder anmelden.
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

            // Beim verzerrten Punktfeld muss man aufpassen: Geradeaus ist nicht
            // automatisch da, wo das Kreuz am Ende wirklich gezeichnet wird.
            // Deshalb fragen wir den Stimulus selbst, wo sein Kreuz gerade steht.
            if (!stimulus.TryGetRenderedFixationWorldDirection(
                gazeRay.origin,
                out Vector3 targetDirection))
            {
                MarkSampleUnusable(gazeData.unityTimestamp);
                TellOthersIfChanged(previousState);
                return;
            }

            currentSampleValid = true;
            validSampleCount++;
            currentAngleDegrees = Vector3.Angle(
                gazeRay.direction,
                targetDirection);

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
            // Sonst fängt die Zählung wieder bei null an. Damit werden aus mehreren
            // kurzen Blicken nicht versehentlich eine lange ruhige Fixation.
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
            // Das kontrollierte Auge muss dasselbe sein, dem auch das Bild gezeigt
            // wird. Sonst könnte ausgerechnet das Auge, das gar nichts sieht, den
            // Durchgang freigeben.
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
            eyeTrackingToolbox ??= EyeTrackingToolbox.Instance;
            stimulus ??= FindAnyObjectByType<RandomDotFieldStimulus>();
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
            requiredContinuousSeconds = Mathf.Max(0f, requiredContinuousSeconds);
        }
    }
}
