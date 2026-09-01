namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Eindeutiger Status für die Versuchsleiteranzeige. Ein ungültiges
    /// Gaze-Sample wird bewusst nicht mit einer gültigen Fehlfixation
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
