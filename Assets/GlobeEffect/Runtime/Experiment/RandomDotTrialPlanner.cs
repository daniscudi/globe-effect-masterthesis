using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Erzeugt den Random-Dot-Plan nach der Methode konstanter Reize. Jeder
    /// vorgegebene l-Wert kommt gleich oft vor. Die Person stellt nichts ein,
    /// sondern erhält nach jeder Bewegung eine Konkav-/Konvex-Entscheidung.
    /// </summary>
    public static class RandomDotTrialPlanner
    {
        public static IReadOnlyList<RandomDotTrial> CreateRandomizedPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> visualSpaceLValues,
            IReadOnlyList<float> contentZoomValues,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions,
            int randomSeed,
            int dotSeedBase)
        {
            ValidateValues(
                angularDiametersDegrees,
                eyePresentations,
                visualSpaceLValues,
                contentZoomValues,
                motionModes,
                repetitions);

            var trials = new List<RandomDotTrial>();
            int conditionIndex = 0;
            int contextIndex = 0;

            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (CheckerboardEyePresentation eye in eyePresentations)
                {
                    foreach (RandomDotMotionMode motionMode in motionModes)
                    {
                        contextIndex++;
                        int directionOffset = unchecked(
                            randomSeed + contextIndex * 7919) & 1;

                        foreach (float contentZoom in contentZoomValues)
                        {
                            foreach (float visualSpaceL in visualSpaceLValues)
                            {
                                conditionIndex++;
                                for (int repetition = 1;
                                    repetition <= repetitions;
                                    repetition++)
                                {
                                    // Derselbe Seed wird bei allen l-Werten einer
                                    // Wiederholung verwendet. Dadurch ist keine
                                    // Punktverteilung fest mit einem l verbunden.
                                    int dotSeed = unchecked(
                                        dotSeedBase +
                                        contextIndex * 1009 +
                                        repetition * 9176);
                                    bool rightFirst = ((repetition + directionOffset) & 1) == 0;

                                    trials.Add(new RandomDotTrial(
                                        sequenceIndex: 0,
                                        conditionIndex: conditionIndex,
                                        repetition: repetition,
                                        attemptNumber: 1,
                                        angularDiameterDegrees: angularDiameter,
                                        eyePresentation: eye,
                                        visualSpaceL: visualSpaceL,
                                        contentZoom: contentZoom,
                                        motionMode: motionMode,
                                        sweepDirection: rightFirst
                                            ? RandomDotSweepDirection.RightFirst
                                            : RandomDotSweepDirection.LeftFirst,
                                        dotSeed: dotSeed));
                                }
                            }
                        }
                    }
                }
            }

            var random = new Random(randomSeed);
            for (int index = trials.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (trials[index], trials[swapIndex]) =
                    (trials[swapIndex], trials[index]);
            }

            for (int index = 0; index < trials.Count; index++)
            {
                trials[index] = trials[index].WithSequenceIndex(index + 1);
            }

            return trials;
        }

        private static void ValidateValues(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> visualSpaceLValues,
            IReadOnlyList<float> contentZoomValues,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions)
        {
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(visualSpaceLValues, nameof(visualSpaceLValues));
            RequireNonEmpty(contentZoomValues, nameof(contentZoomValues));
            RequireNonEmpty(motionModes, nameof(motionModes));

            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            foreach (float value in angularDiametersDegrees)
            {
                if (value < 5f || value > 170f)
                {
                    throw new ArgumentOutOfRangeException(nameof(angularDiametersDegrees));
                }
            }

            foreach (float value in visualSpaceLValues)
            {
                VisualSpaceRadialMapping.ValidateVisualSpaceL(value);
                foreach (float angularDiameter in angularDiametersDegrees)
                {
                    VisualSpaceRadialMapping.ValidateParameters(
                        angularDiameter,
                        value);
                }
            }

            foreach (float value in contentZoomValues)
            {
                if (value < 0.25f || value > 4f)
                {
                    throw new ArgumentOutOfRangeException(nameof(contentZoomValues));
                }
            }
        }

        private static void RequireNonEmpty<T>(IReadOnlyCollection<T> values, string name)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("Mindestens ein Wert ist erforderlich.", name);
            }
        }
    }

    [Serializable]
    public sealed class RandomDotTrial
    {
        public int SequenceIndex { get; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public int AttemptNumber { get; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float VisualSpaceL { get; }
        public float ContentZoom { get; }
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
            float visualSpaceL,
            float contentZoom,
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
            VisualSpaceL = visualSpaceL;
            ContentZoom = contentZoom;
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
                VisualSpaceL,
                ContentZoom,
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
                VisualSpaceL,
                ContentZoom,
                MotionMode,
                SweepDirection,
                DotSeed);
        }
    }

    // Enthält das Ergebnis einer tatsächlichen Präsentation. Auch ungültige
    // Versuche werden gespeichert, damit später nachvollziehbar bleibt, warum
    // eine Bedingung erneut gezeigt wurde.
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

    public sealed class RandomDotTrialQueue
    {
        private readonly Queue<RandomDotTrial> pending = new();

        public int Count => pending.Count;

        public RandomDotTrialQueue(IReadOnlyList<RandomDotTrial> plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            foreach (RandomDotTrial trial in plan)
            {
                pending.Enqueue(trial);
            }
        }

        public bool TryTakeNext(out RandomDotTrial trial)
        {
            if (pending.Count == 0)
            {
                trial = null;
                return false;
            }

            trial = pending.Dequeue();
            return true;
        }

        public RandomDotTrial AppendRepeatedAttempt(RandomDotTrial invalidTrial)
        {
            if (invalidTrial == null)
            {
                throw new ArgumentNullException(nameof(invalidTrial));
            }

            RandomDotTrial repeat = invalidTrial.CreateRepeatedAttempt();
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
