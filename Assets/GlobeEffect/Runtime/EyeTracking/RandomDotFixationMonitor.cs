using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Passt auf, ob die Person während der Punktbewegung auf dem roten Kreuz bleibt.
    ///
    /// Das Kreuz hängt am Kopf und bewegt sich nicht mit den Punkten mit. Wird das
    /// Bild nur einem Auge gezeigt, wird auch nur dieses Auge kontrolliert.
    ///
    /// Die ganze Prüfung steht in FixationMonitorBase. Diese kleine Klasse gibt es,
    /// weil Unity keine Komponente mit Platzhalter-Typ anhängen kann. Hier wird der
    /// Platzhalter auf das Punktfeld festgelegt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RandomDotFixationMonitor : FixationMonitorBase<RandomDotFieldStimulus>
    {
    }
}
