using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Nimmt die beiden Antworten beim Random-Dot-Test entgegen.
    ///
    /// Was in einem Durchgang gezeigt wird, stellt vorher der Experiment Manager
    /// ein. Die Versuchsperson kann daran nichts verändern, sie drückt nur eine
    /// der beiden Tasten.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RandomDotFieldStimulus))]
    public sealed class RandomDotKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        [Tooltip("Taste für: die Bewegung wirkt konkav, also nach innen gewölbt.")]
        private Key concaveKey = Key.LeftArrow;

        [SerializeField]
        [Tooltip("Taste für: die Bewegung wirkt konvex, also nach außen gewölbt.")]
        private Key convexKey = Key.RightArrow;

        [SerializeField]
        [Tooltip("Dreht die Bedeutung der beiden Tasten um. Das stellt man pro Person einmal ein.")]
        private bool swapResponseKeys;

        [SerializeField]
        private bool logResponses;

        private RandomDotFieldStimulus stimulus;

        public event Action<CheckerboardCurvatureResponse> ResponseSubmitted;

        public bool SwapResponseKeys => swapResponseKeys;

        private void Awake()
        {
            stimulus = GetComponent<RandomDotFieldStimulus>();
        }

        private void Update()
        {
            // Links und rechts können im Inspector vertauscht sein. Das macht man,
            // damit nicht bei allen Personen dieselbe Seite dieselbe Antwort ist.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[concaveKey].wasPressedThisFrame)
            {
                SubmitResponse(swapResponseKeys
                    ? CheckerboardCurvatureResponse.Convex
                    : CheckerboardCurvatureResponse.Concave);
            }

            if (keyboard[convexKey].wasPressedThisFrame)
            {
                SubmitResponse(swapResponseKeys
                    ? CheckerboardCurvatureResponse.Concave
                    : CheckerboardCurvatureResponse.Convex);
            }
        }

        public void SetSwapResponseKeys(bool value)
        {
            swapResponseKeys = value;
        }

        public void SubmitResponse(CheckerboardCurvatureResponse response)
        {
            // Hier wird die gedrückte Taste nur weitergemeldet. Ob die Antwort
            // gerade überhaupt erlaubt ist und zu welchem Durchgang sie gehört,
            // entscheidet der Experiment Manager.
            if (response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            ResponseSubmitted?.Invoke(response);
            if (logResponses)
            {
                stimulus ??= GetComponent<RandomDotFieldStimulus>();
                Debug.Log("Random-Dot-Antwort: " + response, stimulus);
            }
        }
    }
}
