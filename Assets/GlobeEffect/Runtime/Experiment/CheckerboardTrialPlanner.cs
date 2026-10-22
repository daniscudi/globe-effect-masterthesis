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
            IReadOnlyList<float> instrumentDistortionKValues,
            int repetitions,
            int randomSeed,
            IReadOnlyList<int> repetitionsPerK = null,
            float instrumentMagnificationM = 10f,
            IReadOnlyList<CheckerboardGridMapping> gridMappings = null)
        {
            gridMappings ??= new[] { CheckerboardGridMapping.FixedSourceGrid };
            PlannerTools.RequireNonEmpty(gridMappings, nameof(gridMappings));
            var selectedMappings = new HashSet<CheckerboardGridMapping>();
            foreach (CheckerboardGridMapping mapping in gridMappings)
            {
                CheckerboardInstrumentMapping.ValidateGridMapping(mapping);
                if (!selectedMappings.Add(mapping))
                    throw new ArgumentException("Gittervariante darf nur einmal im Plan stehen.", nameof(gridMappings));
            }
            // Erst einmal prüfen, ob die Werte überhaupt stimmen. Lieber hier
            // abbrechen, als mitten in der Messung zu merken, dass etwas fehlt.
            ValidateValues(
                angularDiametersDegrees, eyePresentations, instrumentDistortionKValues, repetitions,
                instrumentMagnificationM);
            for (int index = 0; index < instrumentDistortionKValues.Count; index++)
            {
                PlannerTools.RepetitionsFor(repetitionsPerK, index, repetitions);
            }

            var trials = new List<CheckerboardTrial>();
            int conditionIndex = 0;

            // Jede gewählte Variante bekommt denselben k-Satz und dieselben
            // Wiederholungen. Anschließend gemeinsam mischen, nicht als zwei Sitzungen.
            foreach (CheckerboardGridMapping gridMapping in gridMappings)
            {
                // Die ineinander liegenden Schleifen gehen jede Kombination einmal
                // durch: jedes FOV mit jedem Augenmodus mit jedem k. Jedes k kann eine
                // eigene Wiederholungszahl haben (z. B. Randwerte seltener als die Mitte).
                foreach (float angularDiameter in angularDiametersDegrees)
                {
                    foreach (CheckerboardEyePresentation eye in eyePresentations)
                    {
                        for (int kIndex = 0; kIndex < instrumentDistortionKValues.Count; kIndex++)
                        {
                            float instrumentDistortionK = instrumentDistortionKValues[kIndex];
                            int repetitionsForK = PlannerTools.RepetitionsFor(repetitionsPerK, kIndex, repetitions);
                            conditionIndex++;
                            for (int repetition = 1; repetition <= repetitionsForK; repetition++)
                            {
                                trials.Add(new CheckerboardTrial(sequenceIndex: 0, conditionIndex,
                                    repetition, attemptNumber: 1, angularDiameter, eye, instrumentDistortionK,
                                    instrumentMagnificationM, gridMapping));
                            }
                        }
                    }
                }
            }

            // Jetzt wird gemischt (Fisher-Yates, Erklärung in PlannerTools).
            PlannerTools.Shuffle(trials, randomSeed);

            // Die Nummer 1, 2, 3 ... wird erst jetzt vergeben, nach dem Mischen.
            // So passt sie zu der Reihenfolge, die später wirklich gezeigt wird.
            for (int index = 0; index < trials.Count; index++)
            {
                trials[index].SequenceIndex = index + 1;
            }

            return trials;
        }

        private static void ValidateValues(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> instrumentDistortionKValues,
            int repetitions,
            float instrumentMagnificationM)
        {
            PlannerTools.RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            PlannerTools.RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            PlannerTools.RequireNonEmpty(instrumentDistortionKValues, nameof(instrumentDistortionKValues));

            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            foreach (float value in angularDiametersDegrees)
            {
                VisualSpaceRadialMapping.ValidateAngularDiameter(value);
            }

            // Jetzt noch die Paare prüfen. Einzeln kann ein großes FOV in Ordnung
            // sein und ein großes k auch. Zusammen kann es trotzdem nicht gehen,
            // weil der Tangens dann umkippt. Deshalb wird hier jede Kombination
            // durchgerechnet, bevor die Sitzung losgeht.
            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (float instrumentDistortionK in instrumentDistortionKValues)
                {
                    CheckerboardInstrumentMapping.ValidateParameters(
                        angularDiameter, instrumentMagnificationM, instrumentDistortionK);
                }
            }
        }
    }

    /// <summary>
    /// Wann schwarze und weiße Felder vertauscht gezeigt werden ("Invert
    /// Checkerboard Each Trial"). Die Polarität hängt an der Darbietung, nicht am
    /// Plan: Wiederholte Trials werden hinten angehängt und zählen mit.
    /// </summary>
    public static class CheckerboardPolarity
    {
        /// <summary>
        /// Im Versuch: abwechselnd pro gezeigtem Trial, der erste normal.
        /// presentationIndex zählt ab 1, einschließlich wiederholter Trials.
        /// </summary>
        public static bool InvertedForPresentation(bool enabled, int presentationIndex)
        {
            return enabled && presentationIndex % 2 == 0;
        }

        /// <summary>
        /// In den Übungstrials: N, I, I, N, N, I, I, N ... (ab 1 gezählt). Die
        /// Übungs-k wechseln selbst niedrig/hoch ab; ein Wechsel bei jedem Trial
        /// würde "invertiert" genau mit "hoch" zusammenfallen lassen. So kommt
        /// jede Kombination gleich oft vor. Die beiden Beispiele bleiben normal.
        /// </summary>
        public static bool InvertedForPractice(bool enabled, int practiceIndex)
        {
            return enabled && (practiceIndex / 2) % 2 == 1;
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
        // Die Nummer setzt nur der Planer, einmal direkt nach dem Mischen.
        public int SequenceIndex { get; internal set; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public int AttemptNumber { get; private set; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float InstrumentDistortionK { get; }
        public float InstrumentMagnificationM { get; }
        public CheckerboardGridMapping GridMapping { get; }

        public CheckerboardTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            int attemptNumber,
            float angularDiameterDegrees,
            CheckerboardEyePresentation eyePresentation,
            float instrumentDistortionK,
            float instrumentMagnificationM = 10f,
            CheckerboardGridMapping gridMapping = CheckerboardGridMapping.FixedSourceGrid)
        {
            CheckerboardInstrumentMapping.ValidateGridMapping(gridMapping);
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AttemptNumber = attemptNumber;
            AngularDiameterDegrees = angularDiameterDegrees;
            EyePresentation = eyePresentation;
            InstrumentDistortionK = instrumentDistortionK;
            InstrumentMagnificationM = instrumentMagnificationM;
            GridMapping = gridMapping;
        }

        public CheckerboardTrial CreateRepeatedAttempt()
        {
            // Für eine Wiederholung: alles bleibt gleich, nur der Zähler für die
            // Versuche geht eins hoch. MemberwiseClone macht dafür eine Kopie mit
            // allen Werten, das Original bleibt unverändert.
            var repeat = (CheckerboardTrial)MemberwiseClone();
            repeat.AttemptNumber++;
            return repeat;
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
        private readonly Queue<CheckerboardTrial> pending;

        public CheckerboardTrialQueue(IReadOnlyList<CheckerboardTrial> plan)
        {
            pending = new Queue<CheckerboardTrial>(plan);
        }

        public int Count => pending.Count;

        // Dequeue holt immer den vordersten Eintrag heraus.
        public bool TryTakeNext(out CheckerboardTrial trial) => pending.TryDequeue(out trial);

        public CheckerboardTrial AppendRepeatedAttempt(CheckerboardTrial invalidTrial)
        {
            // Enqueue hängt hinten an. Genau das wollen wir hier: Der Durchgang
            // kommt noch einmal dran, aber erst ganz am Schluss.
            CheckerboardTrial repeat = invalidTrial.CreateRepeatedAttempt();
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
