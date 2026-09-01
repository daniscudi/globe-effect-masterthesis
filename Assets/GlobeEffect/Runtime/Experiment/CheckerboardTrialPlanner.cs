using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Baut alle im Inspector gewählten Kombinationen auf und mischt sie mit
    /// einem festen Seed. Dadurch lässt sich ein genauer Trialplan später erneut
    /// erzeugen, ohne die Reihenfolge von Hand speichern zu müssen.
    /// </summary>
    public static class CheckerboardTrialPlanner
    {
        public static IReadOnlyList<CheckerboardTrial> CreateRandomizedPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> visualSpaceLValues,
            int repetitions,
            int randomSeed)
        {
            ValidateValues(
                angularDiametersDegrees,
                eyePresentations,
                visualSpaceLValues,
                repetitions);

            var trials = new List<CheckerboardTrial>();
            int conditionIndex = 0;

            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (CheckerboardEyePresentation eye in eyePresentations)
                {
                    foreach (float visualSpaceL in visualSpaceLValues)
                    {
                        conditionIndex++;
                        for (int repetition = 1;
                            repetition <= repetitions;
                            repetition++)
                        {
                            trials.Add(new CheckerboardTrial(
                                sequenceIndex: 0,
                                conditionIndex: conditionIndex,
                                repetition: repetition,
                                attemptNumber: 1,
                                angularDiameterDegrees: angularDiameter,
                                eyePresentation: eye,
                                visualSpaceL: visualSpaceL));
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
            int repetitions)
        {
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(visualSpaceLValues, nameof(visualSpaceLValues));

            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            foreach (float value in angularDiametersDegrees)
            {
                VisualSpaceRadialMapping.ValidateAngularDiameter(value);
            }

            foreach (float value in visualSpaceLValues)
            {
                VisualSpaceRadialMapping.ValidateVisualSpaceL(value);
            }

            // Die Tangensfunktion muss über den gesamten sichtbaren Winkel
            // monoton bleiben. Bei sehr großem FOV kann deshalb nicht jeder
            // extrapolierte l-Wert verwendet werden.
            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (float visualSpaceL in visualSpaceLValues)
                {
                    VisualSpaceRadialMapping.ValidateParameters(
                        angularDiameter,
                        visualSpaceL);
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
