using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Stellt vor der Sitzung die Liste aller Random-Dot-Durchgänge zusammen.
    ///
    /// Das Verfahren heißt "Methode konstanter Reize": Jeder l-Wert kommt gleich
    /// oft dran, und zwar in zufälliger Reihenfolge.
    ///
    /// Die Person kann nichts einstellen. Sie sieht die Bewegung und sagt danach
    /// nur, ob es konkav oder konvex aussah.
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
            // Erst prüfen, ob die Werte stimmen. Lieber hier abbrechen, als mitten
            // in der Messung zu merken, dass etwas nicht passt.
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

            // Die ineinander liegenden Schleifen gehen jede Kombination einmal durch:
            // jedes FOV mit jedem Augenmodus mit jeder Bewegungsart mit jedem Zoom
            // mit jedem l. Jede davon kommt gleich oft dran.
            foreach (float angularDiameter in angularDiametersDegrees)
            {
                foreach (CheckerboardEyePresentation eye in eyePresentations)
                {
                    foreach (RandomDotMotionMode motionMode in motionModes)
                    {
                        contextIndex++;

                        // Damit nicht immer alles nach rechts losgeht, wird hier je
                        // nach Bedingung zwischen links und rechts gewechselt. Das
                        // Ergebnis ist entweder 0 oder 1 und dreht die Startseite um.
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
                                    // Innerhalb einer Wiederholung bekommen alle
                                    // l-Werte denselben Punkt-Seed, also genau
                                    // dieselben Punkte. Sonst könnte es passieren,
                                    // dass eine bestimmte Punktverteilung immer nur
                                    // bei einem bestimmten l auftaucht, und dann
                                    // wüsste man nicht, woran die Antwort lag.
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

            // Jetzt wird gemischt. Das Verfahren heißt Fisher-Yates: Man geht von
            // hinten durch und tauscht jeden Eintrag mit einem zufälligen Eintrag
            // weiter vorne. Durch den Seed kommt dabei immer dasselbe heraus.
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
            IReadOnlyList<RandomDotMotionMode> motionModes,
            int repetitions)
        {
            // In jeder Liste muss mindestens ein Wert stehen, sonst gibt es gar
            // keine Kombinationen und der Plan wäre leer.
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

            // Erst jedes l für sich prüfen, dann jedes l zusammen mit jedem FOV.
            // Einzeln kann beides in Ordnung sein und zusammen trotzdem nicht gehen.
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
            // An einem fertigen Durchgang wird nichts mehr geändert. Für die neue
            // Nummer wird deshalb eine Kopie gemacht, bei der sonst alles gleich bleibt.
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
            // Hat die Person danebengeschaut, kommt derselbe Durchgang noch einmal.
            // Punkte und Bedingung bleiben exakt gleich, nur der Zähler für die
            // Versuche geht eins hoch.
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

    /// <summary>
    /// Die Warteschlange mit den Durchgängen, die noch kommen.
    ///
    /// Ging etwas schief, wird der Durchgang ganz hinten angehängt und nicht
    /// sofort wiederholt. Sonst käme zweimal hintereinander dasselbe Bild.
    /// </summary>
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
            // Holt den vordersten Eintrag heraus. Kommt false zurück, ist die
            // Sitzung durch.
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

            // Enqueue hängt hinten an. Genau das wollen wir hier: Der Durchgang
            // kommt noch einmal dran, aber erst ganz am Schluss.
            RandomDotTrial repeat = invalidTrial.CreateRepeatedAttempt();
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
