using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Welche Taste am VR-Controller welche Antwort gibt. Gemeint ist immer ein
    /// Klick auf das Trackpad, eine bloße Berührung zählt nicht.
    /// </summary>
    public enum VrControllerMapping
    {
        [InspectorName("Trackpad Click = Concave, Trigger = Convex")]
        TrackpadConcaveTriggerConvex = 0,

        [InspectorName("Trackpad Click = Convex, Trigger = Concave")]
        TrackpadConvexTriggerConcave = 1
    }

    public enum VrControllerButton
    {
        Trigger = 0,
        TrackpadClick = 1
    }

    /// <summary>
    /// Die Zuordnung zwischen Controller-Tasten und Antworten. Checkerboard und
    /// Random Dots benutzen genau diese eine Stelle. Die beiden Umrechnungen
    /// brauchen keine Hardware und lassen sich deshalb ohne Headset testen.
    /// </summary>
    public static class VrControllerResponses
    {
        // Bei den Vive-Controllern heißen die Bedienelemente je nach aktivem
        // XR-Plugin etwas anders (OpenXR: trackpadClicked, Varjo: trackpadPressed).
        // Deshalb werden die üblichen Namen geprüft. Die Namen für eine bloße
        // Berührung (trackpadTouched, triggerTouched) stehen absichtlich nicht dabei.
        private static readonly string[] triggerControls = { "triggerPressed", "triggerButton" };
        private static readonly string[] trackpadClickControls =
        {
            "trackpadClicked", "trackpadPressed", "primary2DAxisClick"
        };

        public static CheckerboardCurvatureResponse ResponseFor(
            VrControllerButton button, VrControllerMapping mapping)
        {
            bool triggerIsConvex = mapping == VrControllerMapping.TrackpadConcaveTriggerConvex;
            bool convex = (button == VrControllerButton.Trigger) == triggerIsConvex;
            return convex ? CheckerboardCurvatureResponse.Convex : CheckerboardCurvatureResponse.Concave;
        }

        public static VrControllerButton ButtonFor(
            CheckerboardCurvatureResponse response, VrControllerMapping mapping)
        {
            return ResponseFor(VrControllerButton.Trigger, mapping) == response
                ? VrControllerButton.Trigger
                : VrControllerButton.TrackpadClick;
        }

        public static string ButtonName(VrControllerButton button)
        {
            return button == VrControllerButton.Trigger ? "TRIGGER" : "TRACKPAD CLICK";
        }

        /// <summary>
        /// Wurde in diesem Frame an irgendeinem VR-Controller der Trigger oder
        /// das Trackpad neu gedrückt? Nur der Moment des Drückens zählt. Halten
        /// und Loslassen lösen nichts aus.
        /// </summary>
        public static bool TryReadPress(out VrControllerButton button)
        {
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is not XRController)
                {
                    continue;
                }

                if (WasPressedThisFrame(device, triggerControls))
                {
                    button = VrControllerButton.Trigger;
                    return true;
                }

                if (WasPressedThisFrame(device, trackpadClickControls))
                {
                    button = VrControllerButton.TrackpadClick;
                    return true;
                }
            }

            button = default;
            return false;
        }

        private static bool WasPressedThisFrame(InputDevice device, string[] controlNames)
        {
            foreach (string controlName in controlNames)
            {
                ButtonControl button = device.TryGetChildControl<ButtonControl>(controlName);
                if (button != null && button.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Nimmt die beiden Antworten konkav und konvex entgegen. Die gemeinsame
    /// Grundlage für Checkerboard und Random Dots.
    ///
    /// Am Laptop funktionieren die Tasten der Tastatur. Im Headset kann die
    /// Versuchsperson stattdessen Trigger und Trackpad-Klick am VR-Controller
    /// benutzen. Beide Eingabearten lösen genau dieselben beiden Antworten aus.
    ///
    /// Hier wird nicht entschieden, ob eine Antwort gerade erlaubt ist und zu
    /// welchem Durchgang sie gehört. Das macht der jeweilige Experiment Manager.
    /// </summary>
    public abstract class ResponseInputController : MonoBehaviour
    {
        [Header("Response Keys")]
        [SerializeField]
        [Tooltip("Key for concave (bulging in).")]
        private Key concaveKey = Key.LeftArrow;

        [SerializeField]
        [Tooltip("Key for convex (bulging out).")]
        private Key convexKey = Key.RightArrow;

        [SerializeField]
        [Tooltip("Swaps the two keyboard keys. The controller buttons are set separately below.")]
        private bool swapResponseKeys;

        [SerializeField]
        [Tooltip("Also writes every answer to the Unity Console.")]
        private bool logResponses;

        [Header("VR Controller")]
        [SerializeField]
        [Tooltip("Answers can also be given with the VR controller (trigger and trackpad click). The " +
            "keyboard keeps working.")]
        private bool useVrControllerButtons = true;

        [SerializeField]
        [Tooltip("Which controller button means convex and which concave. Do not change it during a " +
            "session. In the checkerboard test the setting on the experiment manager is used.")]
        private VrControllerMapping vrControllerMapping = VrControllerMapping.TrackpadConcaveTriggerConvex;

        public event Action<CheckerboardCurvatureResponse> ResponseSubmitted;

        public bool SwapResponseKeys => swapResponseKeys;
        public bool UseVrControllerButtons => useVrControllerButtons;
        public VrControllerMapping VrControllerMapping => vrControllerMapping;

        // Für den Experimenter Monitor und das Log: beide Eingabewege in Worten.
        public string ControllerSummary => !useVrControllerButtons
            ? "aus"
            : VrButtonName(CheckerboardCurvatureResponse.Convex) + " = konvex, " +
              VrButtonName(CheckerboardCurvatureResponse.Concave) + " = konkav";

        public string KeyboardSummary =>
            GetKeyName(CheckerboardCurvatureResponse.Convex) + " = konvex, " +
            GetKeyName(CheckerboardCurvatureResponse.Concave) + " = konkav";

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
        // Eingabe zurückgegeben: mit VR-Controller die Controller-Taste, sonst die
        // Taste der Tastatur. Die Tastatur bleibt trotzdem immer als
        // Ersatzbedienung aktiv, damit man den Ablauf auch am Laptop testen kann.
        public string GetResponseControlName(CheckerboardCurvatureResponse response)
        {
            return useVrControllerButtons ? VrButtonName(response) : GetKeyName(response);
        }

        public string GetKeyName(CheckerboardCurvatureResponse response)
        {
            // Sind die Tasten vertauscht, gehört zu dieser Antwort die jeweils
            // andere Taste.
            return GetReadableKeyName(Swapped(response) == CheckerboardCurvatureResponse.Concave
                ? concaveKey
                : convexKey);
        }

        /// <summary>
        /// Zwei Zeilen für die Anzeige im Headset, zum Beispiel
        /// "TRIGGER = CONVEX" und "TRACKPAD CLICK = CONCAVE". Sie richten sich
        /// immer nach der gerade eingestellten Zuordnung.
        /// </summary>
        public string BuildResponseLines(string convexLabel, string concaveLabel)
        {
            return GetResponseControlName(CheckerboardCurvatureResponse.Convex) + " = " + convexLabel + "\n" +
                GetResponseControlName(CheckerboardCurvatureResponse.Concave) + " = " + concaveLabel;
        }

        public void SetResponseKeys(Key concaveResponseKey, Key convexResponseKey)
        {
            concaveKey = concaveResponseKey;
            convexKey = convexResponseKey;
        }

        public void SetVrControllerButtonsEnabled(bool value)
        {
            useVrControllerButtons = value;
        }

        public void SetVrControllerMapping(VrControllerMapping value)
        {
            vrControllerMapping = value;
        }

        public void SubmitResponse(CheckerboardCurvatureResponse response)
        {
            // Hier wird nicht entschieden, ob die Antwort richtig oder falsch ist.
            // Sie wird nur weitergereicht. Der Experiment Manager weiß, zu welchem
            // Durchgang sie gehört, und schreibt sie weg.
            if (response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            ResponseSubmitted?.Invoke(response);
            if (logResponses)
            {
                Debug.Log(GetType().Name + ": Antwort " + response, this);
            }
        }

        private void Update()
        {
            // Die Belegung stellt man im Inspector ein, bevor eine Person anfängt.
            // Während der Sitzung wird sie nicht mehr angefasst. So muss die Person
            // nicht bei jedem Durchgang neu nachlesen, welche Taste was bedeutet.
            ReadKeyboard();
            if (useVrControllerButtons && VrControllerResponses.TryReadPress(out VrControllerButton button))
            {
                SubmitResponse(VrControllerResponses.ResponseFor(button, vrControllerMapping));
            }
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
                SubmitResponse(Swapped(CheckerboardCurvatureResponse.Concave));
            }

            if (keyboard[convexKey].wasPressedThisFrame)
            {
                SubmitResponse(Swapped(CheckerboardCurvatureResponse.Convex));
            }
        }

        private string VrButtonName(CheckerboardCurvatureResponse response)
        {
            return VrControllerResponses.ButtonName(
                VrControllerResponses.ButtonFor(response, vrControllerMapping));
        }

        // Sind die Tasten der Tastatur vertauscht, wird aus konkav konvex und
        // umgekehrt. Sonst bleibt die Antwort, wie sie ist.
        private CheckerboardCurvatureResponse Swapped(CheckerboardCurvatureResponse response)
        {
            if (!swapResponseKeys || response == CheckerboardCurvatureResponse.None)
            {
                return response;
            }

            return response == CheckerboardCurvatureResponse.Concave
                ? CheckerboardCurvatureResponse.Convex
                : CheckerboardCurvatureResponse.Concave;
        }
    }
}
