using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Stellt vor der Sitzung die Liste aller Random-Dot-Durchgänge zusammen.
    ///
    /// Das Verfahren heißt "Methode konstanter Reize": Jede Kombination aus
    /// Instrumentenverzeichnung k und Vergrößerung m kommt gleich oft dran,
    /// und zwar in zufälliger Reihenfolge innerhalb kurzer Unterblöcke. Die
    /// Bewegungsarten selbst bleiben als getrennte, ausbalancierte Blöcke erhalten.
    ///
    /// Die Person kann nichts einstellen. Sie sieht die Bewegung und sagt danach
    /// nur, ob es konkav oder konvex aussah.
    /// </summary>
    public static class RandomDotTrialPlanner
    {
        /// <summary>Aktueller Versuch: genau eine Bewegungsart, ein vollständig gemischter Block.</summary>
        public static IReadOnlyList<RandomDotTrial> CreateSingleMotionPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> instrumentDistortionKValues,
            IReadOnlyList<float> instrumentMagnificationMValues,
            IReadOnlyList<float> contentZoomValues,
            RandomDotMotionMode motionMode, int repetitions, int randomSeed, int dotSeedBase,
            RandomDotSweepAxis simulatedSweepAxis = RandomDotSweepAxis.Horizontal)
        {
            if (!Enum.IsDefined(typeof(RandomDotMotionMode), motionMode))
                throw new ArgumentOutOfRangeException(nameof(motionMode));
            return CreateRandomizedPlan(angularDiametersDegrees, eyePresentations,
                instrumentDistortionKValues, instrumentMagnificationMValues, contentZoomValues,
                new[] { motionMode }, repetitions, repetitions, randomSeed, dotSeedBase, simulatedSweepAxis);
        }

        // Ältere Plan-API bleibt für vorhandene Tests und historische Blockpläne erhalten.
        // Der Experiment Manager verwendet ausschließlich CreateSingleMotionPlan.
        public static IReadOnlyList<RandomDotTrial> CreateRandomizedPlan(
            IReadOnlyList<float> angularDiametersDegrees,
            IReadOnlyList<CheckerboardEyePresentation> eyePresentations,
            IReadOnlyList<float> instrumentDistortionKValues,
            IReadOnlyList<float> instrumentMagnificationMValues,
            IReadOnlyList<float> contentZoomValues,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions,
            int repetitionsPerMiniBlock,
            int randomSeed,
            int dotSeedBase,
            RandomDotSweepAxis simulatedSweepAxis = RandomDotSweepAxis.Horizontal)
        {
            // Erst prüfen, ob die Werte stimmen. Lieber hier abbrechen, als mitten
            // in der Messung zu merken, dass etwas nicht passt.
            ValidateValues(angularDiametersDegrees, eyePresentations, instrumentDistortionKValues,
                instrumentMagnificationMValues, contentZoomValues, motionModes, repetitions,
                repetitionsPerMiniBlock);

            var trials = new List<RandomDotTrial>();
            int conditionIndex = 0;
            int motionBlockIndex = 0;
            // Ganzzahlig aufgerundet: 25 Wiederholungen in 5er-Unterblöcken sind
            // 5 Unterblöcke, 26 Wiederholungen wären 6.
            int miniBlockCount =
                (repetitions + repetitionsPerMiniBlock - 1) / repetitionsPerMiniBlock;

            // Die Bewegungsarten bleiben als getrennte Blöcke in der Reihenfolge,
            // in der sie im Inspector stehen. Nur innerhalb eines Unterblocks wird
            // gemischt. Dadurch lassen sich die beiden PSEs sauber vergleichen und
            // zwischen den Blöcken kann eine echte Pause stattfinden.
            foreach (RandomDotMotionMode motionMode in motionModes)
            {
                motionBlockIndex++;
                var conditions = new List<Condition>();
                int dotSeedContext = 0;

                foreach (float angularDiameter in angularDiametersDegrees)
                {
                    foreach (CheckerboardEyePresentation eye in eyePresentations)
                    {
                        dotSeedContext++;
                        foreach (float contentZoom in contentZoomValues)
                        {
                            foreach (float magnificationM in instrumentMagnificationMValues)
                            {
                                foreach (float distortionK in instrumentDistortionKValues)
                                {
                                    conditionIndex++;
                                    conditions.Add(new Condition
                                    {
                                        Index = conditionIndex,
                                        MotionBlockIndex = motionBlockIndex,
                                        MotionMode = motionMode,
                                        SweepAxis = motionMode == RandomDotMotionMode.SimulatedYaw
                                            ? simulatedSweepAxis
                                            : RandomDotSweepAxis.Horizontal,
                                        DotSeedContext = dotSeedContext,
                                        DirectionOffset = unchecked(randomSeed +
                                            motionBlockIndex * 7919 + conditionIndex * 101) & 1,
                                        AngularDiameter = angularDiameter,
                                        Eye = eye,
                                        DistortionK = distortionK,
                                        MagnificationM = magnificationM,
                                        ContentZoom = contentZoom,
                                    });
                                }
                            }
                        }
                    }
                }

                for (int miniBlockIndex = 1; miniBlockIndex <= miniBlockCount; miniBlockIndex++)
                {
                    int first = (miniBlockIndex - 1) * repetitionsPerMiniBlock + 1;
                    int last = Math.Min(repetitions, first + repetitionsPerMiniBlock - 1);
                    var miniBlockTrials = new List<RandomDotTrial>();
                    foreach (Condition condition in conditions)
                    {
                        for (int repetition = first; repetition <= last; repetition++)
                        {
                            miniBlockTrials.Add(
                                condition.CreateTrial(repetition, miniBlockIndex, dotSeedBase));
                        }
                    }

                    // Fisher-Yates nur innerhalb des Unterblocks. Dadurch bleiben
                    // Bewegungsblock und Pausengrenzen erhalten.
                    int blockSeed = unchecked(
                        randomSeed + motionBlockIndex * 104729 + miniBlockIndex * 15485863);
                    PlannerTools.Shuffle(miniBlockTrials, blockSeed);
                    trials.AddRange(miniBlockTrials);
                }
            }

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
            IReadOnlyList<float> instrumentMagnificationMValues,
            IReadOnlyList<float> contentZoomValues,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions,
            int repetitionsPerMiniBlock)
        {
            PlannerTools.RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            PlannerTools.RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            PlannerTools.RequireNonEmpty(
                instrumentDistortionKValues, nameof(instrumentDistortionKValues));
            PlannerTools.RequireNonEmpty(
                instrumentMagnificationMValues, nameof(instrumentMagnificationMValues));
            PlannerTools.RequireNonEmpty(contentZoomValues, nameof(contentZoomValues));
            PlannerTools.RequireNonEmpty(motionModes, nameof(motionModes));

            if (new HashSet<RandomDotMotionMode>(motionModes).Count != motionModes.Count)
            {
                throw new ArgumentException(
                    "Jede Bewegungsart darf nur einmal in der Blockreihenfolge stehen.",
                    nameof(motionModes));
            }

            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            if (repetitionsPerMiniBlock < 1 || repetitionsPerMiniBlock > repetitions)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitionsPerMiniBlock));
            }

            foreach (float value in angularDiametersDegrees)
            {
                if (value < 5f || value > 170f)
                {
                    throw new ArgumentOutOfRangeException(nameof(angularDiametersDegrees));
                }
            }

            // Erst jedes k für sich prüfen, dann jedes k zusammen mit jedem FOV.
            // Einzeln kann beides in Ordnung sein und zusammen trotzdem nicht gehen.
            foreach (float value in instrumentDistortionKValues)
            {
                VisualSpaceRadialMapping.ValidateVisualSpaceL(value);
                foreach (float angularDiameter in angularDiametersDegrees)
                {
                    VisualSpaceRadialMapping.ValidateParameters(angularDiameter, value);
                }
            }

            foreach (float value in instrumentMagnificationMValues)
            {
                if (value < RandomDotFieldStimulus.MinimumInstrumentMagnification
                    || value > RandomDotFieldStimulus.MaximumInstrumentMagnification)
                {
                    throw new ArgumentOutOfRangeException(nameof(instrumentMagnificationMValues));
                }
            }

            foreach (float value in contentZoomValues)
            {
                // Dieselben Grenzen wie der Regler im Inspector.
                if (value < RandomDotFieldStimulus.MinimumContentZoom
                    || value > RandomDotFieldStimulus.MaximumContentZoom)
                {
                    throw new ArgumentOutOfRangeException(nameof(contentZoomValues));
                }
            }
        }

        /// <summary>
        /// Eine Bedingung, also eine Kombination aus allen Inspector-Listen.
        /// Sie weiß selbst, wie aus ihr ein Durchgang für eine bestimmte
        /// Wiederholung wird.
        /// </summary>
        private sealed class Condition
        {
            public int Index;
            public int MotionBlockIndex;
            public RandomDotMotionMode MotionMode;
            public RandomDotSweepAxis SweepAxis;
            public int DotSeedContext;
            public int DirectionOffset;
            public float AngularDiameter;
            public CheckerboardEyePresentation Eye;
            public float DistortionK;
            public float MagnificationM;
            public float ContentZoom;

            public RandomDotTrial CreateTrial(int repetition, int miniBlockIndex, int dotSeedBase)
            {
                // Passende Wiederholungen in beiden Bewegungsblöcken
                // verwenden dieselbe Punktverteilung. Der Bewegungsmodus
                // wird deshalb absichtlich nicht in den Seed eingerechnet.
                int dotSeed = unchecked(dotSeedBase + DotSeedContext * 1009 + repetition * 9176);

                // Die Richtung wechselt von Wiederholung zu Wiederholung, damit links
                // und rechts gleich oft drankommen. Beim einseitigen Schwenk ist das
                // die Schwenkrichtung, beim Kopfschwenk die Seite, zu der es zuerst geht.
                // Weil danach im Unterblock gemischt wird, ist die Reihenfolge zufällig.
                bool rightFirst = ((repetition + DirectionOffset) & 1) == 0;
                RandomDotSweepDirection direction = rightFirst
                    ? RandomDotSweepDirection.RightFirst
                    : RandomDotSweepDirection.LeftFirst;

                return new RandomDotTrial(sequenceIndex: 0, Index, repetition, attemptNumber: 1,
                    MotionBlockIndex, miniBlockIndex, AngularDiameter, Eye, DistortionK,
                    MagnificationM, ContentZoom, MotionMode, direction, dotSeed, SweepAxis);
            }
        }
    }

    /// <summary>
    /// Ein einzelner Durchgang.
    ///
    /// Hier stehen nur Werte drin, sonst nichts. Nach dem Anlegen wird daran auch
    /// nichts mehr verändert. Zeigen tut das Ganze später der RandomDotFieldStimulus.
    /// </summary>
    [Serializable]
    public sealed class RandomDotTrial
    {
        // Die Nummer setzt nur der Planer, einmal direkt nach dem Mischen.
        public int SequenceIndex { get; internal set; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public int AttemptNumber { get; private set; }
        public int MotionBlockIndex { get; }
        public int MiniBlockIndex { get; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float InstrumentDistortionK { get; }
        public float InstrumentMagnificationM { get; }
        public float ContentZoom { get; }
        public RandomDotMotionMode MotionMode { get; }
        public RandomDotSweepAxis SweepAxis { get; }
        public RandomDotSweepDirection SweepDirection { get; }
        public int DotSeed { get; }

        // So steht die Richtung in den Dateien und Markern, zum Beispiel "Right" für
        // den einseitigen Schwenk oder "RightFirst" für den Kopfschwenk hin und her.
        public string DirectionLabel =>
            RandomDotSimulatedSweep.DirectionLabel(MotionMode, SweepAxis, SweepDirection);

        public RandomDotTrial(
            int sequenceIndex,
            int conditionIndex,
            int repetition,
            int attemptNumber,
            int motionBlockIndex,
            int miniBlockIndex,
            float angularDiameterDegrees,
            CheckerboardEyePresentation eyePresentation,
            float instrumentDistortionK,
            float instrumentMagnificationM,
            float contentZoom,
            RandomDotMotionMode motionMode,
            RandomDotSweepDirection sweepDirection,
            int dotSeed,
            RandomDotSweepAxis sweepAxis = RandomDotSweepAxis.Horizontal)
        {
            SequenceIndex = sequenceIndex;
            ConditionIndex = conditionIndex;
            Repetition = repetition;
            AttemptNumber = attemptNumber;
            MotionBlockIndex = motionBlockIndex;
            MiniBlockIndex = miniBlockIndex;
            AngularDiameterDegrees = angularDiameterDegrees;
            EyePresentation = eyePresentation;
            InstrumentDistortionK = instrumentDistortionK;
            InstrumentMagnificationM = instrumentMagnificationM;
            ContentZoom = contentZoom;
            MotionMode = motionMode;
            SweepAxis = sweepAxis;
            SweepDirection = sweepDirection;
            DotSeed = dotSeed;
        }

        public RandomDotTrial CreateRepeatedAttempt()
        {
            // Hat die Person danebengeschaut, kommt derselbe Durchgang noch einmal.
            // Punkte und Bedingung bleiben exakt gleich, nur der Zähler für die
            // Versuche geht eins hoch. MemberwiseClone macht dafür eine Kopie mit
            // allen Werten, das Original bleibt unverändert.
            var repeat = (RandomDotTrial)MemberwiseClone();
            repeat.AttemptNumber++;
            return repeat;
        }
    }

    /// <summary>
    /// Die Warteschlange mit den Durchgängen, die noch kommen.
    ///
    /// Ging etwas schief, wird der Durchgang am Ende seines Unterblocks angehängt
    /// und nicht sofort wiederholt. Sonst käme zweimal hintereinander dasselbe Bild.
    /// </summary>
    public sealed class RandomDotTrialQueue
    {
        private readonly LinkedList<RandomDotTrial> pending;

        public RandomDotTrialQueue(IReadOnlyList<RandomDotTrial> plan)
        {
            pending = new LinkedList<RandomDotTrial>(plan);
        }

        public int Count => pending.Count;

        public bool TryTakeNext(out RandomDotTrial trial)
        {
            // Holt den vordersten Eintrag heraus. Kommt false zurück, ist die
            // Sitzung durch.
            if (!TryPeekNext(out trial))
            {
                return false;
            }

            pending.RemoveFirst();
            return true;
        }

        public bool TryPeekNext(out RandomDotTrial trial)
        {
            trial = pending.First?.Value;
            return trial != null;
        }

        public RandomDotTrial AppendRepeatedAttempt(RandomDotTrial invalidTrial)
        {
            // Der Wiederholungsversuch kommt ans Ende seines Unterblocks. So wird
            // weder die Pause übersprungen noch ein Versuch in den anderen
            // Bewegungsmodus verschoben.
            RandomDotTrial repeat = invalidTrial.CreateRepeatedAttempt();
            LinkedListNode<RandomDotTrial> insertionPoint = pending.First;
            while (insertionPoint != null
                && insertionPoint.Value.MotionBlockIndex == invalidTrial.MotionBlockIndex
                && insertionPoint.Value.MiniBlockIndex == invalidTrial.MiniBlockIndex)
            {
                insertionPoint = insertionPoint.Next;
            }

            if (insertionPoint == null)
            {
                pending.AddLast(repeat);
            }
            else
            {
                pending.AddBefore(insertionPoint, repeat);
            }

            return repeat;
        }
    }
}
