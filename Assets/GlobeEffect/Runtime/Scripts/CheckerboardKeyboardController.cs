using UnityEngine;
using UnityEngine.InputSystem;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Nimmt die beiden Antworten beim Checkerboard-Test entgegen.
    ///
    /// Am Laptop funktionieren weiterhin die Pfeiltasten. Im Headset kann die
    /// Versuchsperson stattdessen Trigger und Trackpad-Klick am VR-Controller
    /// benutzen. Die ganze Logik dazu steht in ResponseInputController und ist
    /// dieselbe wie beim Random-Dot-Test.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VrCheckerboardStimulus))]
    public sealed class CheckerboardKeyboardController : ResponseInputController
    {
        private void Reset()
        {
            // Beim Checkerboard sind Pfeil runter (Category B) und Pfeil hoch
            // (Category A) üblich. Der Experiment Manager setzt die Tasten beim
            // Start ohnehin noch einmal selbst.
            SetResponseKeys(Key.DownArrow, Key.UpArrow);
        }
    }
}
