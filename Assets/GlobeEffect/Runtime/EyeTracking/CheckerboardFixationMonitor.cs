using UnityEngine;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Passt auf, ob die Person wirklich auf das Kreuz in der Mitte schaut.
    ///
    /// Das Schachbrett hängt am Kopf und wirkt unendlich weit weg. Deshalb wird
    /// hier auch nur der Winkel zwischen Blickrichtung und Kreuz gemessen. Eine
    /// Entfernung spielt dabei überhaupt keine Rolle.
    ///
    /// Die ganze Prüfung steht in FixationMonitorBase. Diese kleine Klasse gibt es,
    /// weil Unity keine Komponente mit Platzhalter-Typ anhängen kann. Hier wird der
    /// Platzhalter auf das Schachbrett festgelegt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckerboardFixationMonitor : FixationMonitorBase<VrCheckerboardStimulus>
    {
    }
}
