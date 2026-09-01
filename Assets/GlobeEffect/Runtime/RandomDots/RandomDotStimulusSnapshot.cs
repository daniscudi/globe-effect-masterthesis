using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Vollständiger Parametersatz eines Random-Dot-Stimulus zu einem
    /// bestimmten Zeitpunkt. Der Snapshot kann direkt in Trialdateien und
    /// Eye-Tracking-Marker übernommen werden.
    /// </summary>
    [Serializable]
    public struct RandomDotStimulusSnapshot
    {
        public double timestampSeconds;
        public bool visible;
        public bool pointsVisible;
        public float angularDiameterDegrees;
        public float apertureEdgeSoftnessDegrees;
        public float fieldRadiusMeters;
        public float worldCoverageDiameterDegrees;
        public int dotCount;
        public int randomSeed;
        public float merlitzK;
        public float magnification;
        public CheckerboardEyePresentation eyePresentation;
        public RandomDotMotionMode motionMode;
        public RandomDotSweepDirection sweepDirection;
        public float sweepAmplitudeDegrees;
        public float sweepSpeedDegreesPerSecond;
        public float simulatedYawDegrees;
    }
}
