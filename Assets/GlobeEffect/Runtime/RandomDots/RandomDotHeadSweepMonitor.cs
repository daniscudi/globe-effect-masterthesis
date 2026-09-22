using System;
using UnityEngine;

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
        [Header("Referenzen")]
        [SerializeField]
        private Transform observer;

        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [Header("Bewegungsprotokoll")]
        [SerializeField, Range(0.5f, 45f)]
        [Tooltip("Ab wie vielen Grad zu einer Seite das als Rand zählt.")]
        private float yawThresholdDegrees = 2.5f;

        [SerializeField, Range(1, 20)]
        [Tooltip("Nur ein Vergleichswert für die Kontrolle. Der Durchgang wird dadurch nicht blockiert.")]
        private int requiredHalfSweeps = 4;

        [Header("Laufzeitstatus")]
        [SerializeField]
        private float currentYawDegrees;

        [SerializeField]
        private int completedHalfSweeps;

        [SerializeField]
        private float maximumAbsoluteYawDegrees;

        // Die Richtung, in die der Kopf am Anfang des Durchgangs geschaut hat.
        private Vector3 startDirection = Vector3.forward;
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
            FindReferences();
            ResetForTrial();
        }

        private void Update()
        {
            // Solange nichts zu sehen ist, gibt es auch nichts mitzuschreiben.
            if (stimulus == null || !stimulus.IsVisible)
            {
                return;
            }

            // Bei SimulatedYaw kommt der Wert direkt aus der programmierten Bewegung.
            // Bei HeadTracked wird stattdessen die echte Kopfdrehung gemessen.
            currentYawDegrees = stimulus.MotionMode == RandomDotMotionMode.SimulatedYaw
                ? stimulus.CurrentSimulatedYawDegrees
                : MeasureRealHeadYaw();

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
            // Wohin die Person gerade schaut, gilt ab jetzt als Nullstellung.
            // Alles danach wird als Abweichung von dieser Richtung gemessen.
            FindReferences();
            startDirection = RemoveUpDownTilt(
                observer != null ? observer.forward : Vector3.forward);
            counter = new AlternatingHeadSweepCounter(yawThresholdDegrees);
            currentYawDegrees = 0f;
            completedHalfSweeps = 0;
            maximumAbsoluteYawDegrees = 0f;
        }

        private float MeasureRealHeadYaw()
        {
            if (observer == null)
            {
                return 0f;
            }

            // SignedAngle gibt links und rechts mit unterschiedlichem Vorzeichen
            // zurück. Genau das brauchen wir hier.
            Vector3 currentForward = RemoveUpDownTilt(observer.forward);
            return Vector3.SignedAngle(
                startDirection,
                currentForward,
                Vector3.up);
        }

        private void FindReferences()
        {
            // Sind die Felder im Inspector leer, wird hier selbst gesucht.
            // Erst am eigenen Objekt, dann in der ganzen Szene.
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

        private static Vector3 RemoveUpDownTilt(Vector3 direction)
        {
            // Ob der Kopf nach oben oder unten geneigt ist, interessiert hier nicht.
            // Uns interessiert nur das Drehen nach links und rechts. Deshalb wird
            // der Höhenanteil einfach auf null gesetzt.
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
