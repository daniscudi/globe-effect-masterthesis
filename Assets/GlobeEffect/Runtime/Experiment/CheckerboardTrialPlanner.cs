using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Erzeugt einen balancierten vollfaktoriellen Plan und mischt komplette
    /// Trialbedingungen mit einem dokumentierten Seed.
    /// </summary>
    public static class CheckerboardTrialPlanner
    {
        public static IReadOnlyList<CheckerboardTrial> CreateRandomizedPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<float> viewingDistancesMeters,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> startingKValues,
            float magnification,
            int repetitions,
            int randomSeed)
        {
            ValidateValues(
                angularDiametersDegrees,
                viewingDistancesMeters,
                eyePresentations,
                startingKValues,
                magnification,
                repetitions);

            var trials = new List<CheckerboardTrial>();
            int conditionIndex = 0;

            // Die verschachtelten Schleifen bilden das kartesische Produkt aller
            // Inspector-Listen. Eine Bedingung bleibt dabei immer als Ganzes erhalten.
            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (float distance in viewingDistancesMeters)
                {
                    foreach (CheckerboardEyePresentation eye in eyePresentations)
                    {
                        foreach (float startingK in startingKValues)
                        {
                            conditionIndex++;
                            for (int repetition = 1; repetition <= repetitions; repetition++)
                            {
                                trials.Add(new CheckerboardTrial(
                                    sequenceIndex: 0,
                                    conditionIndex,
                                    repetition,
                                    angularDiameter,
                                    distance,
                                    eye,
                                    startingK,
                                    magnification));
                            }
                        }
                    }
                }
            }

            var random = new Random(randomSeed);
            // Fisher-Yates mischt gleichverteilt und ist mit demselben Seed exakt
            // reproduzierbar. Erst nach dem Mischen werden die Laufnummern vergeben.
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
            IReadOnlyList<float> viewingDistancesMeters,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> startingKValues,
            float magnification,
            int repetitions)
        {
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(viewingDistancesMeters, nameof(viewingDistancesMeters));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(startingKValues, nameof(startingKValues));

            if (magnification <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(magnification));
            }

            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            foreach (float value in angularDiametersDegrees)
            {
                if (value < 1f || value > 170f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(angularDiametersDegrees),
                        "Winkeldurchmesser müssen zwischen 1 und 170 Grad liegen.");
                }
            }

            foreach (float value in viewingDistancesMeters)
            {
                if (value < 0.05f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(viewingDistancesMeters),
                        "Abstände müssen mindestens 0.05 Meter betragen.");
                }
            }

            foreach (float value in startingKValues)
            {
                if (value < 0f || value > 1f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(startingKValues),
                        "Startwerte für k müssen zwischen 0 und 1 liegen.");
                }
            }
        }

        private static void RequireNonEmpty<T>(IReadOnlyCollection<T> values, string name)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException(
                    "Mindestens ein Wert ist erforderlich.",
                    name);
            }
        }
    }
}
