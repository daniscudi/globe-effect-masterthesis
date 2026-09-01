using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Technische und experimentelle Tastaturbedienung des Punktfelds. Die
    /// Trialsteuerung hört nur auf die Ereignisse und bleibt dadurch später
    /// problemlos durch Controller-Eingaben oder eine UI ersetzbar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RandomDotFieldStimulus))]
    public sealed class RandomDotKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        private Key recenterKey = Key.R;

        [SerializeField]
        private Key decreaseKKey = Key.LeftArrow;

        [SerializeField]
        private Key increaseKKey = Key.RightArrow;

        [SerializeField, Range(0.001f, 0.1f)]
        [Tooltip("Änderung pro Tastendruck; Shift verfünffacht die Schrittweite.")]
        private float kStep = 0.01f;

        [SerializeField]
        private bool logChanges = true;

        private RandomDotFieldStimulus stimulus;

        public event Action<float, float> KChanged;
        public event Action Recentered;

        public float KStep => kStep;

        private void Awake()
        {
            stimulus = GetComponent<RandomDotFieldStimulus>();
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

        public void ChangeK(float delta)
        {
            EnsureStimulus();
            float previous = stimulus.MerlitzK;
            stimulus.SetMerlitzK(previous + delta);
            KChanged?.Invoke(previous, stimulus.MerlitzK);

            if (logChanges)
            {
                Debug.Log($"Random-Dot-Feld: k = {stimulus.MerlitzK:F3}", stimulus);
            }
        }

        public void Recenter()
        {
            EnsureStimulus();
            stimulus.PlaceAroundObserver();
            Recentered?.Invoke();

            if (logChanges)
            {
                Debug.Log("Random-Dot-Feld an aktueller Kopfpose neu verankert.", stimulus);
            }
        }

        private void EnsureStimulus()
        {
            stimulus ??= GetComponent<RandomDotFieldStimulus>();
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
