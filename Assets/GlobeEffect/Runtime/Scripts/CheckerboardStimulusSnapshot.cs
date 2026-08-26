using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Unveraenderlicher Parameter-Snapshot fuer Trial-Logging und spaetere
    /// Eye-Tracking-Integration. Die Zeit basiert auf Unitys Realtime-Uhr.
    /// </summary>
    [Serializable]
    public struct CheckerboardStimulusSnapshot
    {
        public double timestampSeconds;
        public bool visible;
        public float angularDiameterDegrees;
        public float viewingDistanceMeters;
        public float physicalDiameterMeters;
        public float merlitzK;
        public float magnification;
        public int checksAcrossDiameter;
        public CheckerboardEyePresentation eyePresentation;
    }
}
