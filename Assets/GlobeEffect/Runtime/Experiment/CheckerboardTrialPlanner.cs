using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Stellt vor der Sitzung die Liste aller Durchgänge zusammen.
    ///
    /// Aus den Listen im Inspector wird jede mögliche Kombination gebaut, jede so
    /// oft, wie Wiederholungen eingestellt sind. Danach wird alles gemischt.
    ///
    /// Gemischt wird mit einem festen Seed. Mit demselben Seed und denselben
    /// Einstellungen kommt später wieder genau dieselbe Reihenfolge heraus. Man
    /// muss die Reihenfolge also nicht extra aufschreiben.
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
            // Erst einmal prüfen, ob die Werte überhaupt stimmen. Lieber hier
            // abbrechen, als mitten in der Messung zu merken, dass etwas fehlt.
            ValidateValues(
                angularDiametersDegrees,
                eyePresentations,
                visualSpaceLValues,
                contentZoomValues,
                repetitions);

            var trials = new List<CheckerboardTrial>();
            int conditionIndex = 0;

            // Die ineinander liegenden Schleifen gehen jede Kombination einmal
            // durch: jedes FOV mit jedem Augenmodus mit jedem Zoom mit jedem l.
            foreach (float angularDiameter in angularDiametersDegrees)
            {
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

            // Jetzt wird gemischt. Das Verfahren heißt Fisher-Yates: Man geht von
            // hinten durch und tauscht jeden Eintrag mit einem zufälligen Eintrag
            // weiter vorne. Der Seed sorgt dafür, dass dabei immer dasselbe
            // Mischergebnis herauskommt.
            var random = new Random(randomSeed);
            for (int index = trials.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (trials[index], trials[swapIndex]) =
                    (trials[swapIndex], trials[index]);
            }

            // Die Nummer 1, 2, 3 ... wird erst jetzt vergeben, nach dem Mischen.
            // So passt sie zu der Reihenfolge, die später wirklich gezeigt wird.
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
            int repetitions)
        {
            // In jeder Liste muss mindestens ein Wert stehen, sonst gibt es gar
            // keine Kombinationen und der Plan wäre leer.
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

            // Jetzt noch die Paare prüfen. Einzeln kann ein großes FOV in Ordnung
            // sein und ein großes l auch. Zusammen kann es trotzdem nicht gehen,
            // weil der Tangens dann umkippt. Deshalb wird hier jede Kombination
            // durchgerechnet, bevor die Sitzung losgeht.
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

    /// <summary>
    /// Ein einzelner Durchgang.
    ///
    /// Hier stehen nur die Werte drin, die vorher im Inspector eingestellt wurden.
    /// Nach dem Anlegen wird nichts mehr verändert.
    ///
    /// Muss ein Durchgang wiederholt werden, behält er seine alte Nummer. Nur
    /// AttemptNumber zählt hoch. So sieht man später in der CSV, dass es derselbe
    /// Durchgang war und der wievielte Versuch.
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
            // An einem fertigen Durchgang wird nichts mehr geändert. Für die neue
            // Nummer wird deshalb eine Kopie gemacht, bei der sonst alles gleich bleibt.
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
            // Für eine Wiederholung: alles bleibt gleich, nur der Zähler für die
            // Versuche geht eins hoch.
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

    /// <summary>
    /// Die Warteschlange mit den Durchgängen, die noch kommen.
    ///
    /// Ging bei einem Durchgang etwas schief, wird er nicht sofort noch einmal
    /// gezeigt, sondern ganz hinten angehängt. Sonst käme direkt zweimal
    /// hintereinander dasselbe Bild, und das würde die Antwort beeinflussen.
    /// </summary>
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
            // Dequeue holt immer den vordersten Eintrag heraus.
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

            // Enqueue hängt hinten an. Genau das wollen wir hier: Der Durchgang
            // kommt noch einmal dran, aber erst ganz am Schluss.
            CheckerboardTrial repeat = invalidTrial.CreateRepeatedAttempt();
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
