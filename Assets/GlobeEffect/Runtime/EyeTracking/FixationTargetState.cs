namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Eindeutiger Status fuer die Versuchsleiteranzeige. Ein ungueltiges
    /// Gaze-Sample wird bewusst nicht mit einer gueltigen Fehlfixation
    /// zusammengefasst.
    /// </summary>
    public enum FixationTargetState
    {
        NoValidGaze,
        OffTarget,
        OnTarget
    }

    public static class FixationTargetStateResolver
    {
        public static FixationTargetState Resolve(
            bool currentSampleValid,
            bool isInsideTolerance)
        {
            if (!currentSampleValid)
            {
                return FixationTargetState.NoValidGaze;
            }

            return isInsideTolerance
                ? FixationTargetState.OnTarget
                : FixationTargetState.OffTarget;
        }
    }
}
