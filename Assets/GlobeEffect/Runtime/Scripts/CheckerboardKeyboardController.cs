using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Nimmt die beiden Antworten des Checkerboard-Tests entgegen. Für die
    /// Versuchsperson heißen sie neutral Category A und Category B. Intern
    /// bleiben die bisherigen Namen erhalten, damit ältere CSV-Auswertungen
    /// weiterhin funktionieren.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VrCheckerboardStimulus))]
    public sealed class CheckerboardKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        [Tooltip("Antwort für Category B. Die Taste bleibt innerhalb einer Sitzung gleich.")]
        private Key concaveKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Antwort für Category A. Die Taste bleibt innerhalb einer Sitzung gleich.")]
        private Key convexKey = Key.UpArrow;

        [SerializeField]
        [Tooltip("Schreibt die Antwort zusätzlich in die Unity Console.")]
        private bool logResponses;

        [SerializeField]
        [Tooltip("Vertauscht Category A und B zwischen den beiden Antworttasten. Innerhalb einer Sitzung bleibt die Zuordnung fest.")]
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
                Key.UpArrow => "UP",
                Key.DownArrow => "DOWN",
                Key.LeftArrow => "LEFT",
                Key.RightArrow => "RIGHT",
                Key.Space => "SPACE",
                Key.None => "NO KEY",
                _ => key.ToString().ToUpperInvariant()
            };
        }

        private void Awake()
        {
            stimulus = GetComponent<VrCheckerboardStimulus>();
        }

        private void Update()
        {
            // Die Zuordnung wird zwischen Personen über den Inspector vertauscht,
            // während einer Sitzung aber nicht mehr verändert. Dadurch muss die
            // Person nicht bei jedem Trial eine neue Tastenbelegung lesen.
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
