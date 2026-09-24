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
            int dotSeedBase)
        {
            // Erst prüfen, ob die Werte stimmen. Lieber hier abbrechen, als mitten
            // in der Messung zu merken, dass etwas nicht passt.
            ValidateValues(
                angularDiametersDegrees,
                eyePresentations,
                instrumentDistortionKValues,
                instrumentMagnificationMValues,
                contentZoomValues,
                motionModes,
                repetitions,
                repetitionsPerMiniBlock);

            var trials = new List<RandomDotTrial>();
            int conditionIndex = 0;
            int motionBlockIndex = 0;

            // Die Bewegungsarten bleiben als getrennte Blöcke in der Reihenfolge,
            // in der sie im Inspector stehen. Nur innerhalb eines Unterblocks wird
            // gemischt. Dadurch lassen sich die beiden PSEs sauber vergleichen und
            // zwischen den Blöcken kann eine echte Pause stattfinden.
            foreach (RandomDotMotionMode motionMode in motionModes)
            {
                motionBlockIndex++;
                var conditions = new List<ConditionDefinition>();
                int dotSeedContextIndex = 0;

                foreach (float angularDiameter in angularDiametersDegrees)
                {
                    foreach (CheckerboardEyePresentation eye in eyePresentations)
                    {
                        dotSeedContextIndex++;
                        foreach (float contentZoom in contentZoomValues)
                        {
                            foreach (float instrumentMagnificationM in
                                instrumentMagnificationMValues)
                            {
                                foreach (float instrumentDistortionK in
                                    instrumentDistortionKValues)
                                {
                                    conditionIndex++;
                                    conditions.Add(new ConditionDefinition(
                                        conditionIndex,
                                        dotSeedContextIndex,
                                        angularDiameter,
                                        eye,
                                        instrumentDistortionK,
                                        instrumentMagnificationM,
                                        contentZoom));
                                }
                            }
                        }
                    }
                }

                int miniBlockCount =
                    (repetitions + repetitionsPerMiniBlock - 1) /
                    repetitionsPerMiniBlock;
                for (int miniBlockIndex = 1;
                    miniBlockIndex <= miniBlockCount;
                    miniBlockIndex++)
                {
                    int firstRepetition =
                        (miniBlockIndex - 1) * repetitionsPerMiniBlock + 1;
                    int lastRepetition = Math.Min(
                        repetitions,
                        firstRepetition + repetitionsPerMiniBlock - 1);
                    var miniBlockTrials = new List<RandomDotTrial>();

                    foreach (ConditionDefinition condition in conditions)
                    {
                        int directionOffset = unchecked(
                            randomSeed +
                            motionBlockIndex * 7919 +
                            condition.ConditionIndex * 101) & 1;
                        for (int repetition = firstRepetition;
                            repetition <= lastRepetition;
                            repetition++)
                        {
                            // Passende Wiederholungen in beiden Bewegungsblöcken
                            // verwenden dieselbe Punktverteilung. Der Bewegungsmodus
                            // wird deshalb absichtlich nicht in den Seed eingerechnet.
                            int dotSeed = unchecked(
                                dotSeedBase +
                                condition.DotSeedContextIndex * 1009 +
                                repetition * 9176);
                            bool rightFirst =
                                ((repetition + directionOffset) & 1) == 0;

                            miniBlockTrials.Add(new RandomDotTrial(
                                sequenceIndex: 0,
                                conditionIndex: condition.ConditionIndex,
                                repetition: repetition,
                                attemptNumber: 1,
                                motionBlockIndex: motionBlockIndex,
                                miniBlockIndex: miniBlockIndex,
                                angularDiameterDegrees:
                                    condition.AngularDiameterDegrees,
                                eyePresentation: condition.EyePresentation,
                                instrumentDistortionK:
                                    condition.InstrumentDistortionK,
                                instrumentMagnificationM:
                                    condition.InstrumentMagnificationM,
                                contentZoom: condition.ContentZoom,
                                motionMode: motionMode,
                                sweepDirection: rightFirst
                                    ? RandomDotSweepDirection.RightFirst
                                    : RandomDotSweepDirection.LeftFirst,
                                dotSeed: dotSeed));
                        }
                    }

                    // Fisher-Yates nur innerhalb des Unterblocks. Dadurch bleiben
                    // Bewegungsblock und Pausengrenzen erhalten.
                    var random = new Random(unchecked(
                        randomSeed +
                        motionBlockIndex * 104729 +
                        miniBlockIndex * 15485863));
                    for (int index = miniBlockTrials.Count - 1;
                        index > 0;
                        index--)
                    {
                        int swapIndex = random.Next(index + 1);
                        (miniBlockTrials[index], miniBlockTrials[swapIndex]) =
                            (miniBlockTrials[swapIndex], miniBlockTrials[index]);
                    }

                    trials.AddRange(miniBlockTrials);
                }
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
            IReadOnlyList<float> instrumentDistortionKValues,
            IReadOnlyList<float> instrumentMagnificationMValues,
            IReadOnlyList<float> contentZoomValues,
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions,
            int repetitionsPerMiniBlock)
        {
            // In jeder Liste muss mindestens ein Wert stehen, sonst gibt es gar
            // keine Kombinationen und der Plan wäre leer.
            RequireNonEmpty(angularDiametersDegrees, nameof(angularDiametersDegrees));
            RequireNonEmpty(eyePresentations, nameof(eyePresentations));
            RequireNonEmpty(
                instrumentDistortionKValues,
                nameof(instrumentDistortionKValues));
            RequireNonEmpty(
                instrumentMagnificationMValues,
                nameof(instrumentMagnificationMValues));
            RequireNonEmpty(contentZoomValues, nameof(contentZoomValues));
            RequireNonEmpty(motionModes, nameof(motionModes));

            var uniqueMotionModes = new HashSet<RandomDotMotionMode>();
            foreach (RandomDotMotionMode motionMode in motionModes)
            {
                if (!uniqueMotionModes.Add(motionMode))
                {
                    throw new ArgumentException(
                        "Jede Bewegungsart darf nur einmal in der Blockreihenfolge stehen.",
                        nameof(motionModes));
                }
            }

            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(repetitions));
            }

            if (repetitionsPerMiniBlock < 1 ||
                repetitionsPerMiniBlock > repetitions)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repetitionsPerMiniBlock));
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
                    VisualSpaceRadialMapping.ValidateParameters(
                        angularDiameter,
                        value);
                }
            }

            foreach (float value in instrumentMagnificationMValues)
            {
                if (value <
                        RandomDotFieldStimulus.MinimumInstrumentMagnification ||
                    value >
                        RandomDotFieldStimulus.MaximumInstrumentMagnification)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(instrumentMagnificationMValues));
                }
            }

            foreach (float value in contentZoomValues)
            {
                // Dieselben Grenzen wie der Regler im Inspector.
                if (value < RandomDotFieldStimulus.MinimumContentZoom ||
                    value > RandomDotFieldStimulus.MaximumContentZoom)
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

        private readonly struct ConditionDefinition
        {
            public int ConditionIndex { get; }
            public int DotSeedContextIndex { get; }
            public float AngularDiameterDegrees { get; }
            public CheckerboardEyePresentation EyePresentation { get; }
            public float InstrumentDistortionK { get; }
            public float InstrumentMagnificationM { get; }
            public float ContentZoom { get; }

            public ConditionDefinition(
                int conditionIndex,
                int dotSeedContextIndex,
                float angularDiameterDegrees,
                CheckerboardEyePresentation eyePresentation,
                float instrumentDistortionK,
                float instrumentMagnificationM,
                float contentZoom)
            {
                ConditionIndex = conditionIndex;
                DotSeedContextIndex = dotSeedContextIndex;
                AngularDiameterDegrees = angularDiameterDegrees;
                EyePresentation = eyePresentation;
                InstrumentDistortionK = instrumentDistortionK;
                InstrumentMagnificationM = instrumentMagnificationM;
                ContentZoom = contentZoom;
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
        public int SequenceIndex { get; }
        public int ConditionIndex { get; }
        public int Repetition { get; }
        public int AttemptNumber { get; }
        public int MotionBlockIndex { get; }
        public int MiniBlockIndex { get; }
        public float AngularDiameterDegrees { get; }
        public CheckerboardEyePresentation EyePresentation { get; }
        public float InstrumentDistortionK { get; }
        public float InstrumentMagnificationM { get; }
        // Kompatibilitätsname: Der frühere Trialwert visualSpaceL ist im
        // Instrumentenmodus das dargestellte k, nicht das geschätzte Teilnehmer-l.
        public float VisualSpaceL => InstrumentDistortionK;
        public float ContentZoom { get; }
        public RandomDotMotionMode MotionMode { get; }
        public RandomDotSweepDirection SweepDirection { get; }
        public int DotSeed { get; }

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
            int dotSeed)
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
            SweepDirection = sweepDirection;
            DotSeed = dotSeed;
        }

        internal RandomDotTrial WithSequenceIndex(int sequenceIndex)
        {
            // An einem fertigen Durchgang wird nichts mehr geändert. Für die neue
            // Nummer wird deshalb eine Kopie gemacht, bei der sonst alles gleich bleibt.
            return new RandomDotTrial(
                sequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber,
                MotionBlockIndex,
                MiniBlockIndex,
                AngularDiameterDegrees,
                EyePresentation,
                InstrumentDistortionK,
                InstrumentMagnificationM,
                ContentZoom,
                MotionMode,
                SweepDirection,
                DotSeed);
        }

        public RandomDotTrial CreateRepeatedAttempt()
        {
            // Hat die Person danebengeschaut, kommt derselbe Durchgang noch einmal.
            // Punkte und Bedingung bleiben exakt gleich, nur der Zähler für die
            // Versuche geht eins hoch.
            return new RandomDotTrial(
                SequenceIndex,
                ConditionIndex,
                Repetition,
                AttemptNumber + 1,
                MotionBlockIndex,
                MiniBlockIndex,
                AngularDiameterDegrees,
                EyePresentation,
                InstrumentDistortionK,
                InstrumentMagnificationM,
                ContentZoom,
                MotionMode,
                SweepDirection,
                DotSeed);
        }
    }

    /// <summary>
    /// Was bei einem gezeigten Durchgang herausgekommen ist.
    ///
    /// Auch schiefgegangene Versuche landen hier und kommen in die CSV. Sonst
    /// könnte man später nicht mehr nachvollziehen, warum eine Bedingung noch
    /// einmal gezeigt wurde.
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
        public float MaximumAbsoluteYawDegrees { get; }
        public float MeanAbsoluteYawSpeedDegreesPerSecond { get; }
        public float PeakAbsoluteYawSpeedDegreesPerSecond { get; }
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
            float maximumAbsoluteYawDegrees,
            float meanAbsoluteYawSpeedDegreesPerSecond,
            float peakAbsoluteYawSpeedDegreesPerSecond,
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
            MaximumAbsoluteYawDegrees = maximumAbsoluteYawDegrees;
            MeanAbsoluteYawSpeedDegreesPerSecond =
                meanAbsoluteYawSpeedDegreesPerSecond;
            PeakAbsoluteYawSpeedDegreesPerSecond =
                peakAbsoluteYawSpeedDegreesPerSecond;
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

    /// <summary>
    /// Die Warteschlange mit den Durchgängen, die noch kommen.
    ///
    /// Ging etwas schief, wird der Durchgang am Ende seines Unterblocks angehängt
    /// und nicht sofort wiederholt. Sonst käme zweimal hintereinander dasselbe Bild.
    /// </summary>
    public sealed class RandomDotTrialQueue
    {
        private readonly LinkedList<RandomDotTrial> pending = new();

        public int Count => pending.Count;

        public RandomDotTrialQueue(IReadOnlyList<RandomDotTrial> plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            foreach (RandomDotTrial trial in plan)
            {
                pending.AddLast(trial);
            }
        }

        public bool TryTakeNext(out RandomDotTrial trial)
        {
            // Holt den vordersten Eintrag heraus. Kommt false zurück, ist die
            // Sitzung durch.
            if (pending.Count == 0)
            {
                trial = null;
                return false;
            }

            trial = pending.First.Value;
            pending.RemoveFirst();
            return true;
        }

        public bool TryPeekNext(out RandomDotTrial trial)
        {
            if (pending.Count == 0)
            {
                trial = null;
                return false;
            }

            trial = pending.First.Value;
            return true;
        }

        public RandomDotTrial AppendRepeatedAttempt(RandomDotTrial invalidTrial)
        {
            if (invalidTrial == null)
            {
                throw new ArgumentNullException(nameof(invalidTrial));
            }

            // Der Wiederholungsversuch kommt ans Ende seines Unterblocks. So wird
            // weder die Pause übersprungen noch ein Versuch in den anderen
            // Bewegungsmodus verschoben.
            RandomDotTrial repeat = invalidTrial.CreateRepeatedAttempt();
            LinkedListNode<RandomDotTrial> insertionPoint = pending.First;
            while (insertionPoint != null &&
                insertionPoint.Value.MotionBlockIndex ==
                    invalidTrial.MotionBlockIndex &&
                insertionPoint.Value.MiniBlockIndex ==
                    invalidTrial.MiniBlockIndex)
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
