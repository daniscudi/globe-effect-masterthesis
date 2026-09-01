using System;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Eine vorab geplante Checkerboard-Bedingung. SequenceIndex bezeichnet die
    /// Stelle im ursprünglichen randomisierten Plan. Wenn die Fixation ungültig
    /// war, bleibt diese Nummer gleich und nur AttemptNumber wird erhöht.
    /// </summary>
    [Serializable]
    public sealed class CheckerboardTrial
    {
        public int SequenceIndex { get; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public int AttemptNumber { get; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float VisualSpaceL { get; }

        public CheckerboardTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            int attemptNumber,
            float angularDiameterDegrees,
            CheckerboardEyePresentation eyePresentation,
            float visualSpaceL)
        {
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AttemptNumber = attemptNumber;
            AngularDiameterDegrees = angularDiameterDegrees;
            EyePresentation = eyePresentation;
            VisualSpaceL = visualSpaceL;
        }

        internal CheckerboardTrial WithSequenceIndex(int sequenceIndex)
        {
            return new CheckerboardTrial(
                sequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber,
                AngularDiameterDegrees,
                EyePresentation,
                VisualSpaceL);
        }

        public CheckerboardTrial CreateRepeatedAttempt()
        {
            return new CheckerboardTrial(
                SequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber + 1,
                AngularDiameterDegrees,
                EyePresentation,
                VisualSpaceL);
        }
    }
}
