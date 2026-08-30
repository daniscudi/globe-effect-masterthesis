using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Vollstaendiger Parametersatz eines Random-Dot-Stimulus zu einem
    /// bestimmten Zeitpunkt. Der Snapshot kann direkt in Trialdateien und
    /// Eye-Tracking-Marker uebernommen werden.
    /// </summary>
    [Serializable]
    public struct RandomDotStimulusSnapshot
    {
        public double timestampSeconds;
        public bool visible;
        public float angularDiameterDegrees;
        public float fieldRadiusMeters;
        public float worldCoverageDiameterDegrees;
        public int dotCount;
        public int randomSeed;
        public float merlitzK;
        public float magnification;
        public CheckerboardEyePresentation eyePresentation;
        public RandomDotMotionMode motionMode;
        public float simulatedYawDegrees;
    }
}
