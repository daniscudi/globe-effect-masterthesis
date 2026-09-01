using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Erzeugt den Random-Dot-Plan nach der Methode konstanter Reize. Jeder
    /// vorgegebene k-Wert kommt gleich oft vor. Die Person stellt nichts ein,
    /// sondern erhält nach jeder Bewegung eine Konkav-/Konvex-Entscheidung.
    /// </summary>
    public static class RandomDotTrialPlanner
    {
        public static IReadOnlyList<RandomDotTrial> CreateRandomizedPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> stimulusKValues,
            IReadOnlyList<float> magnifications,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions,
            int randomSeed,
            int dotSeedBase)
        {
            ValidateValues(
                angularDiametersDegrees,
                eyePresentations,
                stimulusKValues,
                magnifications,
                motionModes,
                repetitions);

            var trials = new List<RandomDotTrial>();
            int conditionIndex = 0;
            int contextIndex = 0;

            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (CheckerboardEyePresentation eye in eyePresentations)
                {
                    foreach (float magnification in magnifications)
                    {
                        foreach (RandomDotMotionMode motionMode in motionModes)
                        {
                            contextIndex++;
                            int directionOffset = unchecked(
                                randomSeed + contextIndex * 7919) & 1;

                            foreach (float stimulusK in stimulusKValues)
                            {
                                conditionIndex++;
                                for (int repetition = 1;
                                    repetition <= repetitions;
                                    repetition++)
                                {
                                    // Derselbe Repetition-Seed wird bei allen
                                    // k-Werten wiederverwendet. So ist keine
                                    // bestimmte Punktverteilung mit nur einem k
                                    // verknüpft. Die spätere Reihenfolge bleibt
                                    // trotzdem vollständig randomisiert.
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
                                        stimulusK: stimulusK,
                                        magnification: magnification,
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
            IReadOnlyList<float> stimulusKValues,
            IReadOnlyList<float> magnifications,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions)
        {
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(stimulusKValues, nameof(stimulusKValues));
            RequireNonEmpty(magnifications, nameof(magnifications));
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

            foreach (float value in stimulusKValues)
            {
                if (value < 0f || value > 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(stimulusKValues));
                }
            }

            foreach (float value in magnifications)
            {
                if (value <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(magnifications));
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
}
