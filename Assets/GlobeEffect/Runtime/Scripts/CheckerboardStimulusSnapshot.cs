using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Unveränderlicher Parameter-Snapshot für Trial-Logging und spätere
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
