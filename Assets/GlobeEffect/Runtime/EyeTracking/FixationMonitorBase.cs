using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Das, was die Blickkontrolle vom Stimulus wissen muss: welchem Auge das
    /// Bild gezeigt wird und in welche Richtung das Kreuz gerade zeigt.
    /// </summary>
    public interface IFixationStimulus
    {
        CheckerboardEyePresentation EyePresentation { get; }

        bool TryGetFixationDirection(out Vector3 directionWorld);
    }

    public interface IFixationCriterion
    {
        float ToleranceDegrees { get; }
        float RequiredContinuousSeconds { get; }
    }

    /// <summary>
    /// Die Blickwerte aus einem bestimmten Moment. Einmal festgehalten, ändern
    /// sie sich nicht mehr, auch wenn die Person danach woanders hinschaut.
    /// </summary>
    public readonly struct FixationSnapshot
    {
        // Das steht in der CSV, wenn es gar keine Blickkontrolle gibt.
        public static readonly FixationSnapshot None =
            new FixationSnapshot(false, false, float.NaN, 0f, float.NaN);

        public FixationSnapshot(bool sampleValid, bool onTarget, float angleDegrees,
            float steadySeconds, float validSampleFraction)
        {
            SampleValid = sampleValid;
            OnTarget = onTarget;
            AngleDegrees = angleDegrees;
            SteadySeconds = steadySeconds;
            ValidSampleFraction = validSampleFraction;
        }

        // Kam ein brauchbarer Blickwert?
        public bool SampleValid { get; }

        // Lag der Blick auf dem Kreuz?
        public bool OnTarget { get; }

        // Wie weit war der Blick vom Kreuz weg, in Grad?
        public float AngleDegrees { get; }

        // Wie lange lag der Blick schon am Stück ruhig auf dem Kreuz?
        public float SteadySeconds { get; }

        // Welcher Anteil der Blickwerte war brauchbar (0 bis 1)?
        public float ValidSampleFraction { get; }
    }

    /// <summary>
    /// Die gemeinsame Blickkontrolle für Checkerboard und Random Dot.
    ///
    /// Beide Tests prüfen genau gleich, ob die Person auf das Kreuz schaut. Nur
    /// der Stimulus ist ein anderer. TStimulus ist der Platzhalter dafür, also
    /// VrCheckerboardStimulus oder RandomDotFieldStimulus.
    ///
    /// Es wird nur der Winkel zwischen Blickrichtung und Kreuz gemessen. Eine
    /// Entfernung spielt dabei keine Rolle, weil beide Bilder am Kopf hängen.
    /// </summary>
    public abstract class FixationMonitorBase<TStimulus> : MonoBehaviour, IFixationCriterion
        where TStimulus : MonoBehaviour, IFixationStimulus
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
        private TStimulus stimulus;

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
            FixationTargetStateResolver.Resolve(currentSampleValid, isInsideTolerance);
        public bool RequirementMet => currentSampleValid && isInsideTolerance
            && continuousFixationSeconds >= requiredSteadySeconds;
        public float CurrentAngleDegrees => currentAngleDegrees;
        public float ContinuousFixationSeconds => continuousFixationSeconds;
        public float ToleranceDegrees => toleranceDegrees;
        public float RequiredContinuousSeconds => requiredSteadySeconds;
        public double LastSampleRealtimeSeconds => lastSampleRealtimeSeconds;
        public float ValidSampleFraction =>
            totalSampleCount > 0 ? (float)validSampleCount / totalSampleCount : 0f;

        public bool HasRecentSample(float maximumAgeSeconds)
        {
            // Kommen gerade gar keine Daten mehr, darf der letzte alte Wert nicht
            // so tun, als würde die Person immer noch brav auf das Kreuz schauen.
            // Dann darf auch kein Durchgang starten, sonst würde man losgehen,
            // ohne zu wissen, wohin die Person schaut.
            if (lastSampleRealtimeSeconds <= 0d)
            {
                return false;
            }

            return Time.realtimeSinceStartupAsDouble - lastSampleRealtimeSeconds
                <= Mathf.Max(0f, maximumAgeSeconds);
        }

        // Ein alter, damals ruhiger Blick reicht nicht zum Start. Wenn der Tracker
        // keine neuen Werte mehr liefert, muss die nächste Darbietung warten.
        public bool IsReadyForPresentation(float maximumAgeSeconds)
        {
            return RequirementMet && HasRecentSample(maximumAgeSeconds);
        }

        public bool Tracks(TStimulus expectedStimulus, EyeTrackingToolbox expectedToolbox)
        {
            return stimulus == expectedStimulus && eyeTrackingToolbox == expectedToolbox;
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

        /// <summary>
        /// Hält die aktuellen Blickwerte fest, zum Beispiel am Ende eines
        /// Durchgangs. Die Werte landen später in der CSV-Zeile.
        /// </summary>
        public FixationSnapshot TakeSnapshot()
        {
            return new FixationSnapshot(currentSampleValid, isInsideTolerance, currentAngleDegrees,
                continuousFixationSeconds, ValidSampleFraction);
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

            // Ohne Stimulus, ohne brauchbares Auge oder ohne Richtung zum Kreuz
            // geht hier nichts weiter. Wo das Kreuz gerade wirklich steht, weiß
            // nur der Stimulus selbst, deshalb wird er gefragt.
            //
            // Beim verzerrten Punktfeld muss man nämlich aufpassen: Geradeaus ist
            // nicht automatisch da, wo das Kreuz am Ende wirklich gezeichnet wird.
            if (stimulus == null
                || !PickEyeRay(gazeData, out Ray gazeRay)
                || !stimulus.TryGetFixationDirection(out Vector3 targetDirection))
            {
                MarkSampleUnusable(gazeData.unityTimestamp);
                TellOthersIfChanged(previousState);
                return;
            }

            currentSampleValid = true;
            validSampleCount++;
            currentAngleDegrees = Vector3.Angle(gazeRay.direction, targetDirection);

            // "Auf dem Kreuz" heißt genau eine Sache: Der Winkel ist klein genug.
            isInsideTolerance = currentAngleDegrees <= toleranceDegrees;

            // Wie viel Zeit ist seit dem letzten Messwert vergangen?
            double sampleInterval =
                lastSampleTimestamp > 0d ? gazeData.unityTimestamp - lastSampleTimestamp : 0d;
            lastSampleTimestamp = gazeData.unityTimestamp;

            // Die Fixationsdauer wächst nur, wenn der Blick auch vorher schon auf
            // dem Kreuz lag und die Messwerte dicht genug beieinander liegen.
            //
            // Die Grenze von 100 ms ist mit Absicht streng: War länger nichts da,
            // wissen wir für diese Zeit gar nichts und tun lieber nicht so, als
            // hätte die Person durchgehend geschaut. In jedem anderen Fall fängt
            // die Zählung wieder bei null an. Damit werden aus mehreren kurzen
            // Blicken nicht versehentlich eine lange ruhige Fixation.
            bool noGap = sampleInterval >= 0d && sampleInterval <= 0.1d;
            if (isInsideTolerance && previousState && noGap)
            {
                continuousFixationSeconds += (float)sampleInterval;
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
            // kontrolliert werden. Sonst könnte ausgerechnet das Auge, das gar
            // nichts sieht, den Durchgang freigeben. Der gemittelte Strahl aus
            // beiden Augen passt nur, wenn beide Augen das Bild sehen.
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
            // Bewusst mit == null statt ??=, weil nur der Unity-Vergleich auch
            // gelöschte Objekte als leer erkennt.
            if (eyeTrackingToolbox == null)
            {
                eyeTrackingToolbox = EyeTrackingToolbox.Instance;
            }

            if (stimulus == null)
            {
                stimulus = FindAnyObjectByType<TStimulus>();
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
            if (subscribed && eyeTrackingToolbox != null)
            {
                eyeTrackingToolbox.GazeDataAvailable -= HandleGazeData;
            }

            subscribed = false;
        }

        private void OnValidate()
        {
            toleranceDegrees = Mathf.Clamp(toleranceDegrees, 0.1f, 15f);
            requiredSteadySeconds = Mathf.Max(0f, requiredSteadySeconds);
        }
    }
}
