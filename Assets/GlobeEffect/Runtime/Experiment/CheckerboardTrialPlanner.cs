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
            IReadOnlyList<float> contentZoomValues,
            int repetitions,
            int randomSeed)
        {
            // Zuerst abbrechen, wenn eine Liste leer ist oder einen ungültigen
            // Wert enthält. So startet keine Sitzung mit einem fehlerhaften Plan.
            ValidateValues(
                angularDiametersDegrees,
                eyePresentations,
                visualSpaceLValues,
                contentZoomValues,
                repetitions);

            var trials = new List<CheckerboardTrial>();
            int conditionIndex = 0;

            foreach (float angularDiameter in angularDiametersDegrees)
            {
                // Die verschachtelten Schleifen bilden jede mögliche Kombination
                // der Inspector-Listen. Jede Kombination wird gleich oft wiederholt.
                foreach (CheckerboardEyePresentation eye in eyePresentations)
                {
                    foreach (float contentZoom in contentZoomValues)
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
                                    visualSpaceL: visualSpaceL,
                                    contentZoom: contentZoom));
                            }
                        }
                    }
                }
            }

            var random = new Random(randomSeed);
            // Fisher-Yates-Mischung: Jeder Trial tauscht einmal mit einer zufälligen
            // früheren Position. Derselbe Seed ergibt wieder dieselbe Reihenfolge.
            for (int index = trials.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (trials[index], trials[swapIndex]) =
                    (trials[swapIndex], trials[index]);
            }

            for (int index = 0; index < trials.Count; index++)
            {
                // Die Sequenznummer wird erst nach dem Mischen vergeben.
                trials[index] = trials[index].WithSequenceIndex(index + 1);
            }

            return trials;
        }

        private static void ValidateValues(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> visualSpaceLValues,
            IReadOnlyList<float> contentZoomValues,
            int repetitions)
        {
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(visualSpaceLValues, nameof(visualSpaceLValues));
            RequireNonEmpty(contentZoomValues, nameof(contentZoomValues));

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

            foreach (float value in contentZoomValues)
            {
                if (value < 0.25f || value > 4f)
                {
                    throw new ArgumentOutOfRangeException(nameof(contentZoomValues));
                }
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

    // Ein Trial enthält nur die Werte, die vorher im Inspector festgelegt und
    // anschließend vom Planer gemischt wurden. Bei einer Wiederholung bleibt
    // die ursprüngliche Sequenznummer erhalten; nur AttemptNumber steigt.
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
        public float ContentZoom { get; }

        public CheckerboardTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            int attemptNumber,
            float angularDiameterDegrees,
            CheckerboardEyePresentation eyePresentation,
            float visualSpaceL,
            float contentZoom)
        {
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AttemptNumber = attemptNumber;
            AngularDiameterDegrees = angularDiameterDegrees;
            EyePresentation = eyePresentation;
            VisualSpaceL = visualSpaceL;
            ContentZoom = contentZoom;
        }

        internal CheckerboardTrial WithSequenceIndex(int sequenceIndex)
        {
            // Die Trialklasse bleibt unveränderlich. Für die neue Sequenznummer
            // wird deshalb eine Kopie mit ansonsten gleichen Werten angelegt.
            return new CheckerboardTrial(
                sequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber,
                AngularDiameterDegrees,
                EyePresentation,
                VisualSpaceL,
                ContentZoom);
        }

        public CheckerboardTrial CreateRepeatedAttempt()
        {
            // Eine Wiederholung behält Bedingung und ursprüngliche Sequenznummer.
            // Nur AttemptNumber zeigt an, dass es ein neuer Versuch ist.
            return new CheckerboardTrial(
                SequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber + 1,
                AngularDiameterDegrees,
                EyePresentation,
                VisualSpaceL,
                ContentZoom);
        }
    }

    // Ungültige Präsentationen werden nicht sofort wiederholt, sondern hinten
    // angehängt. So folgt nicht direkt noch einmal dieselbe Bedingung.
    public sealed class CheckerboardTrialQueue
    {
        private readonly Queue<CheckerboardTrial> pending = new();

        public int Count => pending.Count;

        public CheckerboardTrialQueue(IReadOnlyList<CheckerboardTrial> plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            foreach (CheckerboardTrial trial in plan)
            {
                pending.Enqueue(trial);
            }
        }

        public bool TryTakeNext(out CheckerboardTrial trial)
        {
            // Dequeue nimmt immer das vorderste Element aus der Warteschlange.
            if (pending.Count == 0)
            {
                trial = null;
                return false;
            }

            trial = pending.Dequeue();
            return true;
        }

        public CheckerboardTrial AppendRepeatedAttempt(CheckerboardTrial invalidTrial)
        {
            if (invalidTrial == null)
            {
                throw new ArgumentNullException(nameof(invalidTrial));
            }

            CheckerboardTrial repeat = invalidTrial.CreateRepeatedAttempt();
            // Enqueue hängt das Element hinten an, nicht direkt als nächsten Trial.
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
