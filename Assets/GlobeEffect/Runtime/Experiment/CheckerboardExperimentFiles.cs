using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Was bei einem gezeigten Durchgang herausgekommen ist: die Antwort, die
    /// Zeiten und die Werte dazu, wie gut die Person auf das Kreuz geschaut hat.
    /// </summary>
    public sealed class CheckerboardTrialResult
    {
        public CheckerboardTrial Trial { get; }
        public int PresentationIndex { get; }
        public DateTime TrialStartUtc { get; }
        public double TrialStartUnitySeconds { get; }
        public double StimulusEndUnitySeconds { get; }
        public double ResponseWindowStartUnitySeconds { get; }
        public double TrialEndUnitySeconds { get; }
        public float ApertureEdgeSoftnessDegrees { get; }
        public bool CircularApertureEnabled { get; }
        public float GridLineSpacingDegrees { get; }
        public float GridLineSpacingUv { get; }
        public CheckerboardCurvatureResponse Response { get; }
        public bool ValidForAnalysis { get; }
        public bool FixationSampleValid { get; }
        public bool FixationInsideTolerance { get; }
        public float FixationAngleDegrees { get; }
        public float ContinuousFixationSeconds { get; }
        public float FixationValidSampleFraction { get; }
        public float LongestOffTargetSeconds { get; }
        public float LongestInvalidGazeSeconds { get; }
        public string Status { get; }
        public CheckerboardTrialSequence TrialSequence { get; }

        public double StimulusDurationSeconds =>
            Math.Max(0d, StimulusEndUnitySeconds - TrialStartUnitySeconds);

        // Wie lange die Noise-Maske in der Antwortphase zu sehen war. Bei Ablauf B
        // steht dort die graue Fläche; die Maske kommt erst vor dem nächsten Muster.
        public double NoiseMaskDurationSeconds =>
            TrialSequence.ShowsNoiseDuringResponse() ? ResponseTimeSeconds : 0d;

        public double ResponseTimeSeconds =>
            Math.Max(0d, TrialEndUnitySeconds - ResponseWindowStartUnitySeconds);

        public CheckerboardTrialResult(
            CheckerboardTrial trial,
            int presentationIndex,
            DateTime trialStartUtc,
            double trialStartUnitySeconds,
            double stimulusEndUnitySeconds,
            double responseWindowStartUnitySeconds,
            double trialEndUnitySeconds,
            float apertureEdgeSoftnessDegrees,
            bool circularApertureEnabled,
            float gridLineSpacingDegrees,
            float gridLineSpacingUv,
            CheckerboardCurvatureResponse response,
            bool validForAnalysis,
            bool fixationSampleValid,
            bool fixationInsideTolerance,
            float fixationAngleDegrees,
            float continuousFixationSeconds,
            float fixationValidSampleFraction,
            float longestOffTargetSeconds,
            float longestInvalidGazeSeconds,
            string status,
            CheckerboardTrialSequence trialSequence)
        {
            Trial = trial ?? throw new ArgumentNullException(nameof(trial));
            PresentationIndex = presentationIndex;
            TrialStartUtc = trialStartUtc;
            TrialStartUnitySeconds = trialStartUnitySeconds;
            StimulusEndUnitySeconds = stimulusEndUnitySeconds;
            ResponseWindowStartUnitySeconds = responseWindowStartUnitySeconds;
            TrialEndUnitySeconds = trialEndUnitySeconds;
            ApertureEdgeSoftnessDegrees = apertureEdgeSoftnessDegrees;
            CircularApertureEnabled = circularApertureEnabled;
            GridLineSpacingDegrees = gridLineSpacingDegrees;
            GridLineSpacingUv = gridLineSpacingUv;
            Response = response;
            ValidForAnalysis = validForAnalysis;
            FixationSampleValid = fixationSampleValid;
            FixationInsideTolerance = fixationInsideTolerance;
            FixationAngleDegrees = fixationAngleDegrees;
            ContinuousFixationSeconds = continuousFixationSeconds;
            FixationValidSampleFraction = fixationValidSampleFraction;
            LongestOffTargetSeconds = longestOffTargetSeconds;
            LongestInvalidGazeSeconds = longestInvalidGazeSeconds;
            Status = status ?? string.Empty;
            TrialSequence = trialSequence;
        }
    }

    /// <summary>
    /// Schreibt die CSV-Dateien für den Checkerboard-Test.
    /// Ordner, Session-Spalten und CSV-Regeln kommen aus ExperimentFilesBase.
    /// </summary>
    public sealed class CheckerboardExperimentFiles : ExperimentFilesBase
    {
        private const string PlanHeader = SessionColumns +
            "sequence_index,total_planned_trials,condition_index,repetition,eye_presentation," +
            "angular_diameter_deg,grid_line_spacing_deg,grid_line_spacing_uv,visual_space_l," +
            "oomes_endpoint_equivalent,stimulus_duration_s,noise_until_response," +
            "post_response_noise_s,response_timeout_s,category_a_key,category_b_key," +
            "response_keys_swapped,trial_sequence,pre_stimulus_s,controller_mapping";

        // Die Kopfzeile der Ergebnisdatei. Sie wird einmal am Anfang geschrieben
        // und muss Spalte für Spalte zu AppendResult passen.
        private const string TrialHeader = SessionColumns +
            "presentation_index,sequence_index,total_planned_trials,condition_index,repetition," +
            "attempt_number,trial_start_utc,trial_start_unity_s,stimulus_end_unity_s," +
            "response_window_start_unity_s,trial_end_unity_s,stimulus_duration_s," +
            "noise_mask_duration_s,response_time_s,eye_presentation,angular_diameter_deg," +
            "aperture_edge_softness_deg,circular_aperture_enabled,grid_line_spacing_deg," +
            "grid_line_spacing_uv,visual_space_l,oomes_endpoint_equivalent,response," +
            "valid_for_analysis,fixation_sample_valid,fixation_inside_tolerance," +
            "fixation_angle_deg,continuous_fixation_s,fixation_valid_sample_fraction," +
            "longest_off_target_s,longest_invalid_gaze_s,status,trial_sequence";

        public CheckerboardExperimentFiles(
            string outputRoot,
            string participantId,
            string sessionLabel,
            DateTime sessionStartUtc,
            int randomSeed)
            : base(outputRoot, participantId, sessionLabel, "session", sessionStartUtc, randomSeed,
                VisualSpaceRadialMapping.MappingVersion)
        {
        }

        public void WritePlan(
            IReadOnlyList<CheckerboardTrial> trials,
            float gridLineSpacingDegrees,
            float stimulusDurationSeconds,
            CheckerboardTrialSequence trialSequence,
            float preStimulusSeconds,
            float responseTimeoutSeconds,
            string categoryAKey,
            string categoryBKey,
            bool responseKeysSwapped,
            VrControllerMapping controllerMapping)
        {
            // Die beiden alten Spalten bleiben für bestehende Auswertungen stehen:
            // noise_until_response sagt, ob die Maske in der Antwortphase zu sehen
            // ist (Ablauf A), post_response_noise_s nennt die Mindestdauer der
            // Maske nach der Antwort (Ablauf B, sonst 0).
            bool noiseDuringResponse = trialSequence.ShowsNoiseDuringResponse();
            float postResponseNoiseSeconds = noiseDuringResponse ? 0f : preStimulusSeconds;
            var rows = new List<CsvRow>();
            foreach (CheckerboardTrial trial in trials)
            {
                double gridSpacingUv = VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                    trial.AngularDiameterDegrees, gridLineSpacingDegrees);

                rows.Add(StartRow()
                    .Add(trial.SequenceIndex).Add(trials.Count)
                    .Add(trial.ConditionIndex).Add(trial.Repetition)
                    .Add(trial.EyePresentation.ToString()).Add(trial.AngularDiameterDegrees)
                    .Add(gridLineSpacingDegrees).Add(gridSpacingUv).Add(trial.VisualSpaceL)
                    .Add(VisualSpaceRadialMapping.OomesEndpointEquivalent(trial.VisualSpaceL))
                    .Add(stimulusDurationSeconds).Add(noiseDuringResponse).Add(postResponseNoiseSeconds)
                    .Add(responseTimeoutSeconds).Add(categoryAKey).Add(categoryBKey)
                    .Add(responseKeysSwapped).Add(trialSequence.ToString()).Add(preStimulusSeconds)
                    .Add(controllerMapping.ToString()));
            }

            WritePlanFile(PlanHeader, rows, TrialHeader);
        }

        public void AppendResult(CheckerboardTrialResult result, int plannedTrials)
        {
            // Die Reihenfolge der Werte hier muss exakt zur Kopfzeile in
            // TrialHeader passen, sonst stehen die Zahlen später in den
            // falschen Spalten.
            CheckerboardTrial trial = result.Trial;
            AppendResultRow(StartRow()
                .Add(result.PresentationIndex).Add(trial.SequenceIndex).Add(plannedTrials)
                .Add(trial.ConditionIndex).Add(trial.Repetition).Add(trial.AttemptNumber)
                .Add(result.TrialStartUtc).Add(result.TrialStartUnitySeconds)
                .Add(result.StimulusEndUnitySeconds).Add(result.ResponseWindowStartUnitySeconds)
                .Add(result.TrialEndUnitySeconds).Add(result.StimulusDurationSeconds)
                .Add(result.NoiseMaskDurationSeconds).Add(result.ResponseTimeSeconds)
                .Add(trial.EyePresentation.ToString()).Add(trial.AngularDiameterDegrees)
                .Add(result.ApertureEdgeSoftnessDegrees).Add(result.CircularApertureEnabled)
                .Add(result.GridLineSpacingDegrees).Add(result.GridLineSpacingUv)
                .Add(trial.VisualSpaceL)
                .Add(VisualSpaceRadialMapping.OomesEndpointEquivalent(trial.VisualSpaceL))
                .Add(result.Response.ToString()).Add(result.ValidForAnalysis)
                .Add(result.FixationSampleValid).Add(result.FixationInsideTolerance)
                .Add(result.FixationAngleDegrees).Add(result.ContinuousFixationSeconds)
                .Add(result.FixationValidSampleFraction).Add(result.LongestOffTargetSeconds)
                .Add(result.LongestInvalidGazeSeconds).Add(result.Status)
                .Add(result.TrialSequence.ToString()));
        }
    }
}
