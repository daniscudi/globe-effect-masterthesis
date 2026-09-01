using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Nimmt die beiden Antworten des Random-Dot-Tests entgegen. k wird vor dem
    /// Trial von der Sitzungssteuerung gesetzt und kann von der Versuchsperson
    /// nicht verändert werden.
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
