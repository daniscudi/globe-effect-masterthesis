using System;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Eine vorab geplante Random-Dot-Bedingung. k ist ein fester Reizwert und
    /// wird während des Trials nicht verändert. Nach einer ungültigen Fixation
    /// bleibt SequenceIndex gleich und nur AttemptNumber wird erhöht.
    /// </summary>
    [Serializable]
    public sealed class RandomDotTrial
    {
        public int SequenceIndex { get; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public int AttemptNumber { get; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float StimulusK { get; }
        public float Magnification { get; }
        public RandomDotMotionMode MotionMode { get; }
        public RandomDotSweepDirection SweepDirection { get; }
        public int DotSeed { get; }

        public RandomDotTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            int attemptNumber,
            float angularDiameterDegrees,
            CheckerboardEyePresentation eyePresentation,
            float stimulusK,
            float magnification,
            RandomDotMotionMode motionMode,
            RandomDotSweepDirection sweepDirection,
            int dotSeed)
        {
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AttemptNumber = attemptNumber;
            AngularDiameterDegrees = angularDiameterDegrees;
            EyePresentation = eyePresentation;
            StimulusK = stimulusK;
            Magnification = magnification;
            MotionMode = motionMode;
            SweepDirection = sweepDirection;
            DotSeed = dotSeed;
        }

        internal RandomDotTrial WithSequenceIndex(int sequenceIndex)
        {
            return new RandomDotTrial(
                sequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber,
                AngularDiameterDegrees,
                EyePresentation,
                StimulusK,
                Magnification,
                MotionMode,
                SweepDirection,
                DotSeed);
        }

        public RandomDotTrial CreateRepeatedAttempt()
        {
            return new RandomDotTrial(
                SequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber + 1,
                AngularDiameterDegrees,
                EyePresentation,
                StimulusK,
                Magnification,
                MotionMode,
                SweepDirection,
                DotSeed);
        }
    }

    /// <summary>
    /// Antwort, Bewegungsdaten und Fixationsqualität einer tatsächlichen
    /// Random-Dot-Präsentation. Auch ungültige Versuche werden gespeichert.
    /// </summary>
    public sealed class RandomDotTrialResult
    {
        public RandomDotTrial Trial { get; }
        public int PresentationIndex { get; }
        public DateTime TrialStartUtc { get; }
        public double TrialStartUnitySeconds { get; }
        public double StimulusEndUnitySeconds { get; }
        public double ResponseUnitySeconds { get; }
        public CheckerboardCurvatureResponse Response { get; }
        public bool ValidForAnalysis { get; }
        public int CompletedHalfSweeps { get; }
        public float MinimumYawDegrees { get; }
        public float MaximumYawDegrees { get; }
        public float SweepAmplitudeDegrees { get; }
        public float SweepSpeedDegreesPerSecond { get; }
        public float ApertureEdgeSoftnessDegrees { get; }
        public bool FixationSampleValid { get; }
        public bool FixationInsideTolerance { get; }
        public float FixationAngleDegrees { get; }
        public float ContinuousFixationSeconds { get; }
        public float FixationValidSampleFraction { get; }
        public float LongestOffTargetSeconds { get; }
        public float LongestInvalidGazeSeconds { get; }
        public int DotCount { get; }
        public float WorldCoverageDiameterDegrees { get; }
        public float CarrierRadiusMeters { get; }
        public string Status { get; }

        public double StimulusDurationSeconds =>
            Math.Max(0d, StimulusEndUnitySeconds - TrialStartUnitySeconds);

        public double ResponseTimeSeconds =>
            Math.Max(0d, ResponseUnitySeconds - StimulusEndUnitySeconds);

        public RandomDotTrialResult(
            RandomDotTrial trial,
            int presentationIndex,
            DateTime trialStartUtc,
            double trialStartUnitySeconds,
            double stimulusEndUnitySeconds,
            double responseUnitySeconds,
            CheckerboardCurvatureResponse response,
            bool validForAnalysis,
            int completedHalfSweeps,
            float minimumYawDegrees,
            float maximumYawDegrees,
            float sweepAmplitudeDegrees,
            float sweepSpeedDegreesPerSecond,
            float apertureEdgeSoftnessDegrees,
            bool fixationSampleValid,
            bool fixationInsideTolerance,
            float fixationAngleDegrees,
            float continuousFixationSeconds,
            float fixationValidSampleFraction,
            float longestOffTargetSeconds,
            float longestInvalidGazeSeconds,
            int dotCount,
            float worldCoverageDiameterDegrees,
            float carrierRadiusMeters,
            string status)
        {
            Trial = trial ?? throw new ArgumentNullException(nameof(trial));
            PresentationIndex = presentationIndex;
            TrialStartUtc = trialStartUtc;
            TrialStartUnitySeconds = trialStartUnitySeconds;
            StimulusEndUnitySeconds = stimulusEndUnitySeconds;
            ResponseUnitySeconds = responseUnitySeconds;
            Response = response;
            ValidForAnalysis = validForAnalysis;
            CompletedHalfSweeps = completedHalfSweeps;
            MinimumYawDegrees = minimumYawDegrees;
            MaximumYawDegrees = maximumYawDegrees;
            SweepAmplitudeDegrees = sweepAmplitudeDegrees;
            SweepSpeedDegreesPerSecond = sweepSpeedDegreesPerSecond;
            ApertureEdgeSoftnessDegrees = apertureEdgeSoftnessDegrees;
            FixationSampleValid = fixationSampleValid;
            FixationInsideTolerance = fixationInsideTolerance;
            FixationAngleDegrees = fixationAngleDegrees;
            ContinuousFixationSeconds = continuousFixationSeconds;
            FixationValidSampleFraction = fixationValidSampleFraction;
            LongestOffTargetSeconds = longestOffTargetSeconds;
            LongestInvalidGazeSeconds = longestInvalidGazeSeconds;
            DotCount = dotCount;
            WorldCoverageDiameterDegrees = worldCoverageDiameterDegrees;
            CarrierRadiusMeters = carrierRadiusMeters;
            Status = status ?? string.Empty;
        }
    }
}
