using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.EyeTracking;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Was bei einem gezeigten Durchgang herausgekommen ist: die Antwort, die
    /// Zeiten und die Werte dazu, wie gut die Person auf das Kreuz geschaut hat.
    /// Eine Zeile der CSV; der Experiment Manager füllt die Felder direkt aus.
    /// </summary>
    public sealed class CheckerboardTrialResult
    {
        public CheckerboardTrial Trial;
        public int PresentationIndex;
        public DateTime TrialStartUtc;
        public double TrialStartUnitySeconds;
        public double StimulusEndUnitySeconds;
        public double ResponseWindowStartUnitySeconds;
        public double TrialEndUnitySeconds;
        public float ApertureEdgeSoftnessDegrees;
        public bool CircularApertureEnabled;
        public float GridLineSpacingDegrees;
        public float GridLineSpacingUv;
        public CheckerboardCurvatureResponse Response;
        public bool ValidForAnalysis;
        public FixationSnapshot Gaze = FixationSnapshot.None;
        public float LongestOffTargetSeconds;
        public float LongestInvalidGazeSeconds;
        public string Status = string.Empty;
        public CheckerboardTrialSequence TrialSequence;

        // Schwarze und weiße Felder vertauscht (nur mit "Invert Checkerboard Each Trial").
        public bool CheckerboardInverted;

        public double StimulusDurationSeconds =>
            Math.Max(0d, StimulusEndUnitySeconds - TrialStartUnitySeconds);

        // Wie lange die Noise-Maske in der Antwortphase zu sehen war. Bei Ablauf B
        // steht dort die graue Fläche; die Maske kommt erst vor dem nächsten Muster.
        public double NoiseMaskDurationSeconds =>
            TrialSequence.ShowsNoiseDuringResponse() ? ResponseTimeSeconds : 0d;

        public double ResponseTimeSeconds =>
            Math.Max(0d, TrialEndUnitySeconds - ResponseWindowStartUnitySeconds);
    }

    /// <summary>
    /// Schreibt die CSV-Dateien für den Checkerboard-Test.
    /// Ordner, Session-Spalten und CSV-Regeln kommen aus ExperimentFilesBase.
    /// </summary>
    public sealed class CheckerboardExperimentFiles : ExperimentFilesBase
    {
        private const string PlanHeader = SessionColumns + "stimulus_task," +
            "sequence_index,total_planned_trials,condition_index,repetition,eye_presentation," +
            "angular_diameter_deg,grid_line_spacing_deg,grid_line_spacing_uv,instrument_distortion_k," +
            "instrument_magnification_m,stimulus_duration_s,noise_until_response," +
            "post_response_noise_s,response_timeout_s,category_a_key,category_b_key," +
            "response_keys_swapped,trial_sequence,pre_stimulus_s,controller_mapping,invert_checkerboard_each_trial";

        // Die Kopfzeile der Ergebnisdatei. Sie wird einmal am Anfang geschrieben
        // und muss Spalte für Spalte zu AppendResult passen.
        private const string TrialHeader = SessionColumns + "stimulus_task," +
            "presentation_index,sequence_index,total_planned_trials,condition_index,repetition," +
            "attempt_number,trial_start_utc,trial_start_unity_s,stimulus_end_unity_s," +
            "response_window_start_unity_s,trial_end_unity_s,stimulus_duration_s," +
            "noise_mask_duration_s,response_time_s,eye_presentation,angular_diameter_deg," +
            "aperture_edge_softness_deg,circular_aperture_enabled,grid_line_spacing_deg," +
            "grid_line_spacing_uv,instrument_distortion_k,instrument_magnification_m,response," +
            "valid_for_analysis,fixation_sample_valid,fixation_inside_tolerance," +
            "fixation_angle_deg,continuous_fixation_s,fixation_valid_sample_fraction," +
            "longest_off_target_s,longest_invalid_gaze_s,status,trial_sequence,checkerboard_inverted";

        public CheckerboardExperimentFiles(
            string outputRoot,
            string participantId,
            string sessionLabel,
            DateTime sessionStartUtc,
            int randomSeed)
            : base(outputRoot, participantId, sessionLabel, "session", sessionStartUtc, randomSeed,
                CheckerboardInstrumentMapping.MappingVersion)
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
            VrControllerMapping controllerMapping,
            bool invertCheckerboardEachTrial = false)
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
                double gridSpacingUv = CheckerboardInstrumentMapping.GridLineSpacingUv(
                    trial.AngularDiameterDegrees, gridLineSpacingDegrees);

                rows.Add(StartRow().Add("Checkerboard")
                    .Add(trial.SequenceIndex).Add(trials.Count)
                    .Add(trial.ConditionIndex).Add(trial.Repetition)
                    .Add(trial.EyePresentation.ToString()).Add(trial.AngularDiameterDegrees)
                    .Add(gridLineSpacingDegrees).Add(gridSpacingUv).Add(trial.InstrumentDistortionK)
                    .Add(trial.InstrumentMagnificationM)
                    .Add(stimulusDurationSeconds).Add(noiseDuringResponse).Add(postResponseNoiseSeconds)
                    .Add(responseTimeoutSeconds).Add(categoryAKey).Add(categoryBKey)
                    .Add(responseKeysSwapped).Add(trialSequence.ToString()).Add(preStimulusSeconds)
                    .Add(controllerMapping.ToString()).Add(invertCheckerboardEachTrial));
            }

            WritePlanFile(PlanHeader, rows, TrialHeader);
        }

        public void AppendResult(CheckerboardTrialResult result, int plannedTrials)
        {
            // Die Reihenfolge der Werte hier muss exakt zur Kopfzeile in
            // TrialHeader passen, sonst stehen die Zahlen später in den
            // falschen Spalten.
            CheckerboardTrial trial = result.Trial;
            AppendResultRow(StartRow().Add("Checkerboard")
                .Add(result.PresentationIndex).Add(trial.SequenceIndex).Add(plannedTrials)
                .Add(trial.ConditionIndex).Add(trial.Repetition).Add(trial.AttemptNumber)
                .Add(result.TrialStartUtc).Add(result.TrialStartUnitySeconds)
                .Add(result.StimulusEndUnitySeconds).Add(result.ResponseWindowStartUnitySeconds)
                .Add(result.TrialEndUnitySeconds).Add(result.StimulusDurationSeconds)
                .Add(result.NoiseMaskDurationSeconds).Add(result.ResponseTimeSeconds)
                .Add(trial.EyePresentation.ToString()).Add(trial.AngularDiameterDegrees)
                .Add(result.ApertureEdgeSoftnessDegrees).Add(result.CircularApertureEnabled)
                .Add(result.GridLineSpacingDegrees).Add(result.GridLineSpacingUv)
                .Add(trial.InstrumentDistortionK)
                .Add(trial.InstrumentMagnificationM)
                .Add(result.Response.ToString()).Add(result.ValidForAnalysis)
                .Add(result.Gaze.SampleValid).Add(result.Gaze.OnTarget)
                .Add(result.Gaze.AngleDegrees).Add(result.Gaze.SteadySeconds)
                .Add(result.Gaze.ValidSampleFraction).Add(result.LongestOffTargetSeconds)
                .Add(result.LongestInvalidGazeSeconds).Add(result.Status)
                .Add(result.TrialSequence.ToString()).Add(result.CheckerboardInverted));
        }
    }
}
