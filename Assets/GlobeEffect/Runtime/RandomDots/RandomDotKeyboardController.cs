using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Nimmt die beiden Antworten des Random-Dot-Tests entgegen. Die jeweilige
    /// Reizbedingung wird vor dem Trial vom Experiment Manager gesetzt und kann
    /// von der Versuchsperson nicht verändert werden.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RandomDotFieldStimulus))]
    public sealed class RandomDotKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        [Tooltip("Antwort: Die Bewegung beziehungsweise Fläche wirkt konkav.")]
        private Key concaveKey = Key.LeftArrow;

        [SerializeField]
        [Tooltip("Antwort: Die Bewegung beziehungsweise Fläche wirkt konvex.")]
        private Key convexKey = Key.RightArrow;

        [SerializeField]
        [Tooltip("Vertauscht die Bedeutung der beiden Tasten zwischen Versuchspersonen.")]
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
            // Je nach Gegenbalancierung kann links/rechts im Inspector vertauscht sein.
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
            // Das Event gibt nur die gedrückte Antwort weiter. Ob sie gerade erlaubt
            // ist und zu welchem Trial sie gehört, entscheidet der Experiment Manager.
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
