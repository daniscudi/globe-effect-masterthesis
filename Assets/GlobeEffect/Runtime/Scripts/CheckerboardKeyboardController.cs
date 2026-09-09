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
        [Tooltip("Antwort: Das Muster wölbt sich von mir weg, wie eine Schüssel.")]
        private Key concaveKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Antwort: Das Muster wölbt sich zu mir, wie ein Ball.")]
        private Key convexKey = Key.UpArrow;

        [SerializeField]
        [Tooltip("Schreibt die Antwort zusätzlich in die Unity Console.")]
        private bool logResponses;

        [SerializeField]
        [Tooltip("Vertauscht die Bedeutung der beiden Tasten. Das kann zwischen Versuchspersonen ausbalanciert werden.")]
        private bool swapResponseKeys;

        private VrCheckerboardStimulus stimulus;

        public event Action<CheckerboardCurvatureResponse> ResponseSubmitted;

        public bool SwapResponseKeys => swapResponseKeys;

        // Der Antwortbildschirm fragt diese Methode ab, damit die angezeigte
        // Taste auch bei vertauschter Zuordnung immer mit der Auswertung übereinstimmt.
        public Key GetKeyForResponse(CheckerboardCurvatureResponse response)
        {
            return response switch
            {
                CheckerboardCurvatureResponse.Concave => swapResponseKeys
                    ? convexKey
                    : concaveKey,
                CheckerboardCurvatureResponse.Convex => swapResponseKeys
                    ? concaveKey
                    : convexKey,
                _ => Key.None
            };
        }

        public static string GetReadableKeyName(Key key)
        {
            return key switch
            {
                Key.UpArrow => "PFEIL HOCH",
                Key.DownArrow => "PFEIL RUNTER",
                Key.LeftArrow => "PFEIL LINKS",
                Key.RightArrow => "PFEIL RECHTS",
                Key.Space => "LEERTASTE",
                Key.None => "KEINE TASTE",
                _ => key.ToString().ToUpperInvariant()
            };
        }

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

        public void SetResponseKeys(Key concaveResponseKey, Key convexResponseKey)
        {
            concaveKey = concaveResponseKey;
            convexKey = convexResponseKey;
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
