using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Nimmt die beiden Antworten beim Random-Dot-Test entgegen.
    ///
    /// Was in einem Durchgang gezeigt wird, stellt vorher der Experiment Manager
    /// ein. Die Versuchsperson kann daran nichts verändern, sie drückt nur eine
    /// der beiden Tasten: Pfeil links und rechts auf der Tastatur oder Trigger
    /// und Trackpad-Klick am VR-Controller. Die ganze Logik dazu steht in
    /// ResponseInputController und ist dieselbe wie beim Checkerboard.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RandomDotFieldStimulus))]
    public sealed class RandomDotKeyboardController : ResponseInputController
    {
    }
}
