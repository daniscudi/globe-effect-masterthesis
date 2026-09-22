using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Nimmt die beiden Antworten beim Checkerboard-Test entgegen.
    ///
    /// Die Versuchsperson hört nur Category A und Category B. Das sind absichtlich
    /// neutrale Namen, damit nichts vorgegeben wird.
    ///
    /// Im Code und in den CSV-Dateien heißen sie weiter Concave und Convex. So
    /// funktionieren die alten Auswertungen noch.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VrCheckerboardStimulus))]
    public sealed class CheckerboardKeyboardController : MonoBehaviour
    {
        [Header("Tastensteuerung")]
        [SerializeField]
        [Tooltip("Taste für Category B. Sie bleibt während einer Sitzung gleich.")]
        private Key concaveKey = Key.DownArrow;

        [SerializeField]
        [Tooltip("Taste für Category A. Sie bleibt während einer Sitzung gleich.")]
        private Key convexKey = Key.UpArrow;

        [SerializeField]
        [Tooltip("Schreibt jede Antwort zusätzlich in die Unity Console.")]
        private bool logResponses;

        [SerializeField]
        [Tooltip("Dreht Category A und B auf die jeweils andere Taste. Das stellt man pro Person einmal ein und lässt es dann so.")]
        private bool swapResponseKeys;

        private VrCheckerboardStimulus stimulus;

        public event Action<CheckerboardCurvatureResponse> ResponseSubmitted;

        public bool SwapResponseKeys => swapResponseKeys;

        // Der Antwortbildschirm fragt hier nach, welche Taste er anzeigen soll.
        // Dadurch steht auf dem Bildschirm auch dann die richtige Taste, wenn die
        // Belegung für diese Person gedreht ist.
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

        // Macht aus dem Unity-Tastennamen etwas, das man auf dem Bildschirm
        // lesen kann. Aus "UpArrow" wird zum Beispiel "UP".
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
            // Die Belegung dreht man im Inspector, bevor eine Person anfängt.
            // Während der Sitzung wird sie nicht mehr angefasst. So muss die Person
            // nicht bei jedem Durchgang neu nachlesen, welche Taste was bedeutet.
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
            // Hier wird nicht entschieden, ob die Antwort richtig oder falsch ist.
            // Sie wird nur weitergereicht. Der Experiment Manager weiß, zu welchem
            // Durchgang sie gehört, und schreibt sie weg.
            FindStimulus();
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

        private void FindStimulus()
        {
            // Normalerweise steht der Stimulus schon seit Awake fest. Falls nicht,
            // wird er hier nachgeholt.
            if (stimulus == null)
            {
                stimulus = GetComponent<VrCheckerboardStimulus>();
            }
        }
    }
}
