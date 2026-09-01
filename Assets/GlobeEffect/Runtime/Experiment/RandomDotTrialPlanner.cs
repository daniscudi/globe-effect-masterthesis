using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Erzeugt und randomisiert vollständige Random-Dot-Bedingungen. Die
    /// Punkt-Seeds hängen von Bedingung und Wiederholung ab, nicht von der
    /// später gemischten Reihenfolge.
    /// </summary>
    public static class RandomDotTrialPlanner
    {
        public static IReadOnlyList<RandomDotTrial> CreateRandomizedPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> startingKValues,
            IReadOnlyList<float> magnifications,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions,
            int randomSeed,
            int dotSeedBase)
        {
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(startingKValues, nameof(startingKValues));
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

            foreach (float value in startingKValues)
            {
                if (value < 0f || value > 1f)
                {
                    throw new ArgumentOutOfRangeException(nameof(startingKValues));
                }
            }

            foreach (float value in magnifications)
            {
                if (value <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(magnifications));
                }
            }

            var trials = new List<RandomDotTrial>();
            int conditionIndex = 0;
            // Jede Kombination aus FOV, Auge, Start-k, Vergrößerung und Bewegung
            // wird für jede Wiederholung genau einmal angelegt.
            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (CheckerboardEyePresentation eye in eyePresentations)
                {
                    foreach (float startingK in startingKValues)
                    {
                        foreach (float magnification in magnifications)
                        {
                            foreach (RandomDotMotionMode motionMode in motionModes)
                            {
                                conditionIndex++;
                                for (int repetition = 1; repetition <= repetitions; repetition++)
                                {
                                    int dotSeed = unchecked(
                                        dotSeedBase + conditionIndex * 1009 + repetition * 9176);
                                    // Der Punkt-Seed hängt von der fachlichen Bedingung
                                    // ab und ändert sich daher nicht durch die Mischung.
                                    trials.Add(new RandomDotTrial(
                                        sequenceIndex: 0,
                                        conditionIndex,
                                        repetition,
                                        angularDiameter,
                                        eye,
                                        startingK,
                                        magnification,
                                        motionMode,
                                        dotSeed));
                                }
                            }
                        }
                    }
                }
            }

            var random = new Random(randomSeed);
            // Reproduzierbare Fisher-Yates-Mischung der vollständigen Trials.
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

        private static void RequireNonEmpty<T>(IReadOnlyCollection<T> values, string name)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("Mindestens ein Wert ist erforderlich.", name);
            }
        }
    }
}
