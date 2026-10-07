using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
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
        public float ProfileErrorDegrees { get; }
        public float FirstExtremeErrorDegrees { get; }
        public float SecondExtremeErrorDegrees { get; }
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
        public bool FreeHeadMovement { get; }
        public string MotionConstraint => Trial.MotionMode == RandomDotMotionMode.SimulatedYaw
            ? "simulated" : FreeHeadMovement ? "free" : "guided";

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
            float profileErrorDegrees,
            float firstExtremeErrorDegrees,
            float secondExtremeErrorDegrees,
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
            string status,
            bool freeHeadMovement = false)
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
            MeanAbsoluteYawSpeedDegreesPerSecond = meanAbsoluteYawSpeedDegreesPerSecond;
            PeakAbsoluteYawSpeedDegreesPerSecond = peakAbsoluteYawSpeedDegreesPerSecond;
            ProfileErrorDegrees = profileErrorDegrees;
            FirstExtremeErrorDegrees = firstExtremeErrorDegrees;
            SecondExtremeErrorDegrees = secondExtremeErrorDegrees;
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
            FreeHeadMovement = freeHeadMovement;
        }
    }

    /// <summary>
    /// Schreibt die CSV-Dateien für den Random-Dot-Test.
    /// Ordner, Session-Spalten und CSV-Regeln kommen aus ExperimentFilesBase.
    /// </summary>
    public sealed class RandomDotExperimentFiles : ExperimentFilesBase
    {
        public const string MappingVersion = "merlitz-instrument-k-m-free-or-guided-head-v8";

        private const string PlanHeader = SessionColumns +
            "sequence_index,total_planned_trials,condition_index,repetition,motion_block_index," +
            "mini_block_index,eye_presentation,angular_diameter_deg,instrument_distortion_k," +
            "instrument_magnification_m,content_zoom,motion_mode,sweep_axis,sweep_direction," +
            "dot_seed";

        // Die Kopfzeile der Ergebnisdatei. Sie wird einmal am Anfang geschrieben
        // und muss Spalte für Spalte zu AppendResult passen.
        private const string TrialHeader = SessionColumns +
            "presentation_index,sequence_index,total_planned_trials,condition_index,repetition," +
            "attempt_number,motion_block_index,mini_block_index,trial_start_utc," +
            "trial_start_unity_s,stimulus_end_unity_s,response_unity_s,stimulus_duration_s," +
            "response_time_s,eye_presentation,angular_diameter_deg,aperture_edge_softness_deg," +
            "instrument_distortion_k,instrument_magnification_m,content_zoom,motion_mode," +
            "sweep_axis,sweep_direction,sweep_amplitude_deg,sweep_speed_deg_per_s," +
            "completed_half_sweeps,min_sweep_deg,max_sweep_deg,max_abs_sweep_deg," +
            "mean_abs_sweep_speed_deg_per_s,peak_abs_sweep_speed_deg_per_s,profile_rmse_deg," +
            "first_turn_error_deg,second_turn_error_deg,dot_seed,dot_count," +
            "world_coverage_diameter_deg,carrier_radius_m,response,valid_for_analysis," +
            "fixation_sample_valid,fixation_inside_tolerance,fixation_angle_deg," +
            "continuous_fixation_s,fixation_valid_sample_fraction,longest_off_target_s," +
            "longest_invalid_gaze_s,status,image_center_speed_deg_per_s,head_motion_constraint";

        public RandomDotExperimentFiles(
            string outputRoot,
            string participantId,
            string sessionLabel,
            DateTime sessionStartUtc,
            int randomSeed)
            : base(outputRoot, participantId, sessionLabel, "random_dot", sessionStartUtc,
                randomSeed, MappingVersion)
        {
        }

        public void WritePlan(IReadOnlyList<RandomDotTrial> trials)
        {
            var rows = new List<CsvRow>();
            foreach (RandomDotTrial trial in trials)
            {
                rows.Add(StartRow()
                    .Add(trial.SequenceIndex).Add(trials.Count)
                    .Add(trial.ConditionIndex).Add(trial.Repetition)
                    .Add(trial.MotionBlockIndex).Add(trial.MiniBlockIndex)
                    .Add(trial.EyePresentation.ToString()).Add(trial.AngularDiameterDegrees)
                    .Add(trial.InstrumentDistortionK).Add(trial.InstrumentMagnificationM)
                    .Add(trial.ContentZoom).Add(trial.MotionMode.ToString())
                    .Add(trial.SweepAxis.ToString()).Add(trial.DirectionLabel).Add(trial.DotSeed));
            }

            WritePlanFile(PlanHeader, rows, TrialHeader);
        }

        public void AppendResult(RandomDotTrialResult result, int plannedTrials)
        {
            // Die Reihenfolge der Werte hier muss exakt zur Kopfzeile in
            // TrialHeader passen, sonst stehen die Zahlen später in den
            // falschen Spalten.
            RandomDotTrial trial = result.Trial;
            AppendResultRow(StartRow()
                .Add(result.PresentationIndex).Add(trial.SequenceIndex).Add(plannedTrials)
                .Add(trial.ConditionIndex).Add(trial.Repetition).Add(trial.AttemptNumber)
                .Add(trial.MotionBlockIndex).Add(trial.MiniBlockIndex).Add(result.TrialStartUtc)
                .Add(result.TrialStartUnitySeconds).Add(result.StimulusEndUnitySeconds)
                .Add(result.ResponseUnitySeconds).Add(result.StimulusDurationSeconds)
                .Add(result.ResponseTimeSeconds).Add(trial.EyePresentation.ToString())
                .Add(trial.AngularDiameterDegrees).Add(result.ApertureEdgeSoftnessDegrees)
                .Add(trial.InstrumentDistortionK).Add(trial.InstrumentMagnificationM)
                .Add(trial.ContentZoom).Add(trial.MotionMode.ToString())
                .Add(trial.SweepAxis.ToString()).Add(trial.DirectionLabel)
                .Add(result.SweepAmplitudeDegrees).Add(result.SweepSpeedDegreesPerSecond)
                .Add(result.CompletedHalfSweeps).Add(result.MinimumYawDegrees)
                .Add(result.MaximumYawDegrees).Add(result.MaximumAbsoluteYawDegrees)
                .Add(result.MeanAbsoluteYawSpeedDegreesPerSecond)
                .Add(result.PeakAbsoluteYawSpeedDegreesPerSecond).Add(result.ProfileErrorDegrees)
                .Add(result.FirstExtremeErrorDegrees).Add(result.SecondExtremeErrorDegrees)
                .Add(trial.DotSeed).Add(result.DotCount).Add(result.WorldCoverageDiameterDegrees)
                .Add(result.CarrierRadiusMeters).Add(result.Response.ToString())
                .Add(result.ValidForAnalysis).Add(result.FixationSampleValid)
                .Add(result.FixationInsideTolerance).Add(result.FixationAngleDegrees)
                .Add(result.ContinuousFixationSeconds).Add(result.FixationValidSampleFraction)
                .Add(result.LongestOffTargetSeconds).Add(result.LongestInvalidGazeSeconds)
                .Add(result.Status)
                // sweep_speed_deg_per_s ist der Objektwinkel. Hier steht zusätzlich,
                // wie schnell die Punkte dabei in der Bildmitte liefen.
                .Add(RandomDotSimulatedSweep.ImageCenterSpeed(result.SweepSpeedDegreesPerSecond,
                    trial.InstrumentMagnificationM, trial.ContentZoom))
                .Add(result.MotionConstraint));
        }
    }
}
