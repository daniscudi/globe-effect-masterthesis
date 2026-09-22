using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Nimmt die beiden Antworten beim Checkerboard-Test entgegen.
    ///
    /// Am Laptop funktionieren weiterhin die Pfeiltasten. Im Headset kann die
    /// Versuchsperson stattdessen Trigger und Trackpad am VR-Controller benutzen.
    /// Beide Eingabearten lösen genau dieselben beiden Antworten aus.
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
        [Tooltip("Vertauscht beide Antworten. Das betrifft Pfeiltasten und VR-Controller und bleibt während einer Sitzung gleich.")]
        private bool swapResponseKeys;

        [Header("VR-Controller")]
        [SerializeField]
        [Tooltip("Nimmt zusätzlich Trigger und Trackpad von einem angeschlossenen VR-Controller an.")]
        private bool useVrControllerButtons = true;

        private VrCheckerboardStimulus stimulus;

        public event Action<CheckerboardCurvatureResponse> ResponseSubmitted;

        public bool SwapResponseKeys => swapResponseKeys;
        public bool UseVrControllerButtons => useVrControllerButtons;

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

        // Für Texte und CSV-Dateien wird hier der Name der tatsächlich vorgesehenen
        // Eingabe zurückgegeben. Die Pfeiltasten bleiben trotzdem immer als
        // Ersatzbedienung aktiv, damit man den Ablauf auch am Laptop testen kann.
        public string GetResponseControlName(
            CheckerboardCurvatureResponse response)
        {
            if (!useVrControllerButtons)
            {
                return GetReadableKeyName(GetKeyForResponse(response));
            }

            bool triggerMeansConvex = !swapResponseKeys;
            bool responseUsesTrigger = response == CheckerboardCurvatureResponse.Convex
                ? triggerMeansConvex
                : !triggerMeansConvex;
            return responseUsesTrigger ? "TRIGGER" : "TRACKPAD";
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
            ReadKeyboard();
            ReadVrControllers();
        }

        private void ReadKeyboard()
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

        private void ReadVrControllers()
        {
            if (!useVrControllerButtons)
            {
                return;
            }

            // Bei den Vive-Controllern heißen die Bedienelemente je nach aktivem
            // XR-Plugin etwas anders. Deshalb werden die üblichen Namen geprüft.
            // So funktioniert derselbe Code mit OpenXR und mit dem Varjo-Layout.
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is not XRController)
                {
                    continue;
                }

                if (WasPressedThisFrame(
                        device,
                        "triggerPressed",
                        "triggerButton"))
                {
                    SubmitResponse(swapResponseKeys
                        ? CheckerboardCurvatureResponse.Concave
                        : CheckerboardCurvatureResponse.Convex);
                    return;
                }

                if (WasPressedThisFrame(
                        device,
                        "trackpadClicked",
                        "trackpadPressed",
                        "primary2DAxisClick"))
                {
                    SubmitResponse(swapResponseKeys
                        ? CheckerboardCurvatureResponse.Convex
                        : CheckerboardCurvatureResponse.Concave);
                    return;
                }
            }
        }

        private static bool WasPressedThisFrame(
            InputDevice device,
            params string[] controlNames)
        {
            foreach (string controlName in controlNames)
            {
                ButtonControl button =
                    device.TryGetChildControl<ButtonControl>(controlName);
                if (button != null && button.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetSwapResponseKeys(bool value)
        {
            swapResponseKeys = value;
        }

        public void SetVrControllerButtonsEnabled(bool value)
        {
            useVrControllerButtons = value;
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
