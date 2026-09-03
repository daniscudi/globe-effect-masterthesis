using System;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Kopie der Parameter, die in einem bestimmten Moment wirklich im Shader
    /// eingestellt waren. Damit protokolliert die CSV nicht nur den Trialplan,
    /// sondern auch die tatsächlich dargestellte Konfiguration.
    /// </summary>
    [Serializable]
    public struct CheckerboardStimulusSnapshot
    {
        public double timestampSeconds;
        public bool visible;
        public bool checkerboardVisible;
        public float angularDiameterDegrees;
        public float apertureEdgeSoftnessDegrees;
        public bool useCircularAperture;
        public float visualSpaceL;
        public int checksAcrossDiameter;
        public CheckerboardEyePresentation eyePresentation;
    }
}
