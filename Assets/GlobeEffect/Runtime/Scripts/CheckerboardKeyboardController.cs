using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Nimmt die beiden Antworten des Checkerboard-Tests entgegen. Die Person
    /// verändert l nicht selbst. Sie entscheidet nur, ob das gerade gezeigte
    /// Muster konkav oder konvex wirkt.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VrCheckerboardStimulus))]
    public sealed class CheckerboardKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        [Tooltip("Antwort: Das Muster wirkt konkav.")]
        private Key concaveKey = Key.LeftArrow;

        [SerializeField]
        [Tooltip("Antwort: Das Muster wirkt konvex.")]
        private Key convexKey = Key.RightArrow;

        [SerializeField]
        [Tooltip("Schreibt die Antwort zusätzlich in die Unity Console.")]
        private bool logResponses;

        [SerializeField]
        [Tooltip("Vertauscht die Bedeutung der beiden Tasten. Das kann zwischen Versuchspersonen ausbalanciert werden.")]
        private bool swapResponseKeys;

        private VrCheckerboardStimulus stimulus;

        public event Action<CheckerboardCurvatureResponse> ResponseSubmitted;

        public bool SwapResponseKeys => swapResponseKeys;

        private void Awake()
        {
            stimulus = GetComponent<VrCheckerboardStimulus>();
        }

        private void Update()
        {
            // Die Zuordnung kann zwischen Personen vertauscht werden, damit nicht
            // immer dieselbe Hand mit derselben Antwort verbunden ist.
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
            // Der Controller bewertet die Antwort nicht. Er meldet sie nur an den
            // Experiment Manager, der den aktuellen Trial kennt und speichert.
            EnsureStimulus();
            if (response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            ResponseSubmitted?.Invoke(response);

            if (logResponses)
            {
                Debug.Log("Checkerboard-Antwort: " + response, stimulus);
            }
        }

        private void EnsureStimulus()
        {
            if (stimulus == null)
            {
                stimulus = GetComponent<VrCheckerboardStimulus>();
            }
        }
    }
}
