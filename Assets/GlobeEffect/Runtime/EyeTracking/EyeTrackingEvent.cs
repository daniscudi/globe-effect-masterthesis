using System;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Entkoppelt die Provider von der zentralen Toolbox. Provider senden hier
    /// ausschließlich HMD-lokale Rohdaten; die Toolbox ergänzt die World-Rays.
    /// </summary>
    public static class EyeTrackingEvent
    {
        public static event Action<GazeData> OnDataAvailable;

        public static void TriggerEvent(GazeData data)
        {
            OnDataAvailable?.Invoke(data);
        }
    }
}
