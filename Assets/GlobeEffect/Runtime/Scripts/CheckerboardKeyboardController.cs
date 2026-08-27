using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Einfache Bedienung fuer technische Tests im Unity Play Mode.
    /// Die Klasse ist bewusst von einer spaeteren Trial-Steuerung getrennt.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VrCheckerboardStimulus))]
    public sealed class CheckerboardKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        [Tooltip("Taste, die den Stimulus vor der aktuellen HMD-Blickrichtung neu platziert.")]
        private Key recenterKey = Key.R;

        [SerializeField]
        [Tooltip("Taste zum Verringern des Merlitz-Parameters k.")]
        private Key decreaseKKey = Key.LeftArrow;

        [SerializeField]
        [Tooltip("Taste zum Erhoehen des Merlitz-Parameters k.")]
        private Key increaseKKey = Key.RightArrow;

        [SerializeField, Range(0.001f, 0.1f)]
        [Tooltip("Aenderung von k pro Tastendruck. Mit Shift wird die Schrittweite verfuenffacht.")]
        private float kStep = 0.01f;

        [SerializeField]
        [Tooltip("Schreibt Recenter- und k-Aenderungen in die Unity Console.")]
        private bool logChanges = true;

        private VrCheckerboardStimulus stimulus;

        public float KStep => kStep;

        private void Awake()
        {
            stimulus = GetComponent<VrCheckerboardStimulus>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[recenterKey].wasPressedThisFrame)
            {
                Recenter();
            }

            float step = IsShiftPressed(keyboard) ? kStep * 5f : kStep;
            if (keyboard[decreaseKKey].wasPressedThisFrame)
            {
                ChangeK(-step);
            }

            if (keyboard[increaseKKey].wasPressedThisFrame)
            {
                ChangeK(step);
            }
        }

        /// <summary>
        /// Platziert den Stimulus in der aktuellen Center-Eye-Blickrichtung.
        /// Die laufende Follow-Einstellung wird dadurch nicht veraendert.
        /// </summary>
        public void Recenter()
        {
            EnsureStimulus();
            stimulus.PlaceInFrontOfObserver();

            if (logChanges)
            {
                Debug.Log("Checkerboard in aktueller Blickrichtung neu platziert.", stimulus);
            }
        }

        public void ChangeK(float delta)
        {
            EnsureStimulus();
            stimulus.SetMerlitzK(stimulus.MerlitzK + delta);

            if (logChanges)
            {
                Debug.Log($"Checkerboard: k = {stimulus.MerlitzK:F3}", stimulus);
            }
        }

        private void EnsureStimulus()
        {
            if (stimulus == null)
            {
                stimulus = GetComponent<VrCheckerboardStimulus>();
            }
        }

        private static bool IsShiftPressed(Keyboard keyboard)
        {
            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }

        private void OnValidate()
        {
            kStep = Mathf.Clamp(kStep, 0.001f, 0.1f);
        }
    }
}
