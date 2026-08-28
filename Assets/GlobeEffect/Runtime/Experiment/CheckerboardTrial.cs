using System;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Vollstaendige, unveraenderliche Bedingung eines Checkerboard-Trials.
    /// Alle gemeinsam randomisierten Werte bleiben dadurch als Einheit erhalten.
    /// </summary>
    [Serializable]
    public sealed class CheckerboardTrial
    {
        public int SequenceIndex { get; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public float AngularDiameterDegrees { get; }
        public float ViewingDistanceMeters { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float StartingK { get; }
        public float Magnification { get; }

        public CheckerboardTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            float angularDiameterDegrees,
            float viewingDistanceMeters,
            CheckerboardEyePresentation eyePresentation,
            float startingK,
            float magnification)
        {
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AngularDiameterDegrees = angularDiameterDegrees;
            ViewingDistanceMeters = viewingDistanceMeters;
            EyePresentation = eyePresentation;
            StartingK = startingK;
            Magnification = magnification;
        }

        internal CheckerboardTrial WithSequenceIndex(int sequenceIndex)
        {
            return new CheckerboardTrial(
                sequenceIndex,
                ConditionIndex,
                Repetition,
                AngularDiameterDegrees,
                ViewingDistanceMeters,
                EyePresentation,
                StartingK,
                Magnification);
        }
    }
}
