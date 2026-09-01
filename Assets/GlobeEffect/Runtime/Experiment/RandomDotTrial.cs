using System;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Unveränderliche Bedingung eines dynamischen Random-Dot-k-Trials.
    /// </summary>
    [Serializable]
    public sealed class RandomDotTrial
    {
        public int SequenceIndex { get; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float StartingK { get; }
        public float Magnification { get; }
        public RandomDotMotionMode MotionMode { get; }
        public int DotSeed { get; }

        public RandomDotTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            float angularDiameterDegrees,
            CheckerboardEyePresentation eyePresentation,
            float startingK,
            float magnification,
            RandomDotMotionMode motionMode,
            int dotSeed)
        {
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AngularDiameterDegrees = angularDiameterDegrees;
            EyePresentation = eyePresentation;
            StartingK = startingK;
            Magnification = magnification;
            MotionMode = motionMode;
            DotSeed = dotSeed;
        }

        internal RandomDotTrial WithSequenceIndex(int sequenceIndex)
        {
            return new RandomDotTrial(
                sequenceIndex,
                ConditionIndex,
                Repetition,
                AngularDiameterDegrees,
                EyePresentation,
                StartingK,
                Magnification,
                MotionMode,
                DotSeed);
        }
    }

    /// <summary>Gespeicherte Antwort und Qualitätswerte eines Trials.</summary>
    public sealed class RandomDotTrialResult
    {
        public RandomDotTrial Trial { get; }
        public DateTime TrialStartUtc { get; }
        public double TrialStartUnitySeconds { get; }
        public double TrialEndUnitySeconds { get; }
        public float FinalK { get; }
        public int KAdjustmentCount { get; }
        public int RecenterCount { get; }
        public int CompletedHalfSweeps { get; }
        public float SweepThresholdDegrees { get; }
        public float MinimumYawDegrees { get; }
        public float MaximumYawDegrees { get; }
        public bool FixationSampleValid { get; }
        public bool FixationInsideTolerance { get; }
        public bool FixationRequirementMet { get; }
        public float FixationAngleDegrees { get; }
        public float ContinuousFixationSeconds { get; }
        public int DotCount { get; }
        public float WorldCoverageDiameterDegrees { get; }
        public float FieldRadiusMeters { get; }
        public string Status { get; }

        public double ResponseTimeSeconds =>
            TrialEndUnitySeconds - TrialStartUnitySeconds;

        public RandomDotTrialResult(
            RandomDotTrial trial,
            DateTime trialStartUtc,
            double trialStartUnitySeconds,
            double trialEndUnitySeconds,
            float finalK,
            int kAdjustmentCount,
            int recenterCount,
            int completedHalfSweeps,
            float sweepThresholdDegrees,
            float minimumYawDegrees,
            float maximumYawDegrees,
            bool fixationSampleValid,
            bool fixationInsideTolerance,
            bool fixationRequirementMet,
            float fixationAngleDegrees,
            float continuousFixationSeconds,
            int dotCount,
            float worldCoverageDiameterDegrees,
            float fieldRadiusMeters,
            string status)
        {
            Trial = trial;
            TrialStartUtc = trialStartUtc;
            TrialStartUnitySeconds = trialStartUnitySeconds;
            TrialEndUnitySeconds = trialEndUnitySeconds;
            FinalK = finalK;
            KAdjustmentCount = kAdjustmentCount;
            RecenterCount = recenterCount;
            CompletedHalfSweeps = completedHalfSweeps;
            SweepThresholdDegrees = sweepThresholdDegrees;
            MinimumYawDegrees = minimumYawDegrees;
            MaximumYawDegrees = maximumYawDegrees;
            FixationSampleValid = fixationSampleValid;
            FixationInsideTolerance = fixationInsideTolerance;
            FixationRequirementMet = fixationRequirementMet;
            FixationAngleDegrees = fixationAngleDegrees;
            ContinuousFixationSeconds = continuousFixationSeconds;
            DotCount = dotCount;
            WorldCoverageDiameterDegrees = worldCoverageDiameterDegrees;
            FieldRadiusMeters = fieldRadiusMeters;
            Status = status ?? string.Empty;
        }
    }
}
