using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Was bei einem gezeigten Durchgang herausgekommen ist: eine Zeile der CSV.
    ///
    /// Auch schiefgegangene Versuche landen hier und kommen in die CSV. Sonst
    /// könnte man später nicht mehr nachvollziehen, warum eine Bedingung noch
    /// einmal gezeigt wurde. Der Experiment Manager füllt die Felder direkt aus.
    /// </summary>
    public sealed class RandomDotTrialResult
    {
        public RandomDotTrial Trial;
        public int PresentationIndex;
        public DateTime TrialStartUtc;
        public double TrialStartUnitySeconds;
        public double StimulusEndUnitySeconds;
        public double ResponseUnitySeconds;
        public CheckerboardCurvatureResponse Response;
        public bool ValidForAnalysis;
        public string Status = string.Empty;

        // Bewegung: bei Simulated der programmierte Schwenk, bei Active der echte Kopf.
        public float MinimumYawDegrees;
        public float MaximumYawDegrees;
        public float MaximumAbsoluteYawDegrees;
        public float MeanAbsoluteYawSpeedDegreesPerSecond;
        public float PeakAbsoluteYawSpeedDegreesPerSecond;
        public float SweepAmplitudeDegrees = float.NaN;
        public float SweepSpeedDegreesPerSecond = float.NaN;

        // Punktfeld und Blick.
        public float ApertureEdgeSoftnessDegrees;
        public int DotCount;
        public float WorldCoverageDiameterDegrees;
        public float CarrierRadiusMeters;
        public FixationSnapshot Gaze = FixationSnapshot.None;
        public float LongestOffTargetSeconds;
        public float LongestInvalidGazeSeconds;
        public FrameTimingTracker FrameTiming = new();

        // Nur aktive Trials (Kopftempo-Erkennung); bei Simulated NaN bzw. leer.
        public HeadVelocitySummary HeadVelocity = HeadVelocitySummary.None;
        /// <summary>Tatsächliche Schwenkrichtung (Right/Left bzw. Up/Down).</summary>
        public string HeadDirection = string.Empty;
        /// <summary>Mittleres Tempo neben der Schwenkachse während der Punkte (°/s).</summary>
        public float HeadOffAxisSpeed = float.NaN;
        /// <summary>Größte Abweichung neben der Schwenkachse während der Punkte (°).</summary>
        public float HeadOffAxisDeviation = float.NaN;
        /// <summary>Größtes seitliches Kippen des Kopfes während der Punkte (°).</summary>
        public float HeadRollDeviation = float.NaN;

        public string MotionConstraint =>
            Trial.MotionMode == RandomDotMotionMode.SimulatedYaw ? "simulated" : "free";

        public double StimulusDurationSeconds =>
            Math.Max(0d, StimulusEndUnitySeconds - TrialStartUnitySeconds);

        public double ResponseTimeSeconds =>
            Math.Max(0d, ResponseUnitySeconds - StimulusEndUnitySeconds);
    }

    /// <summary>
    /// Schreibt die CSV-Dateien für den Random-Dot-Test.
    /// Ordner, Session-Spalten und CSV-Regeln kommen aus ExperimentFilesBase.
    /// </summary>
    public sealed class RandomDotExperimentFiles : ExperimentFilesBase
    {
        public const string MappingVersion = "merlitz-instrument-k-m-free-head-aperture-halfmax-v10";

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
            "min_sweep_deg,max_sweep_deg,max_abs_sweep_deg," +
            "mean_abs_sweep_speed_deg_per_s,peak_abs_sweep_speed_deg_per_s,dot_seed,dot_count," +
            "world_coverage_diameter_deg,carrier_radius_m,response,valid_for_analysis," +
            "fixation_sample_valid,fixation_inside_tolerance,fixation_angle_deg," +
            "continuous_fixation_s,fixation_valid_sample_fraction,longest_off_target_s," +
            "longest_invalid_gaze_s,status,image_center_speed_deg_per_s,head_motion_constraint," +
            "frames_presented,slow_frames,max_frame_ms,expected_frame_ms," +
            "velocity_gate_target_deg_per_s,head_speed_onset_deg_per_s,head_speed_mean_deg_per_s," +
            "head_speed_min_deg_per_s,head_speed_max_deg_per_s,head_motion_wait_s," +
            "head_direction,head_offaxis_speed_mean_deg_per_s," +
            "head_offaxis_max_deviation_deg,head_roll_max_deviation_deg";

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
            FixationSnapshot gaze = result.Gaze;
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
                .Add(result.MinimumYawDegrees).Add(result.MaximumYawDegrees)
                .Add(result.MaximumAbsoluteYawDegrees).Add(result.MeanAbsoluteYawSpeedDegreesPerSecond)
                .Add(result.PeakAbsoluteYawSpeedDegreesPerSecond)
                .Add(trial.DotSeed).Add(result.DotCount).Add(result.WorldCoverageDiameterDegrees)
                .Add(result.CarrierRadiusMeters).Add(result.Response.ToString())
                .Add(result.ValidForAnalysis).Add(gaze.SampleValid)
                .Add(gaze.OnTarget).Add(gaze.AngleDegrees)
                .Add(gaze.SteadySeconds).Add(gaze.ValidSampleFraction)
                .Add(result.LongestOffTargetSeconds).Add(result.LongestInvalidGazeSeconds)
                .Add(result.Status)
                // sweep_speed_deg_per_s ist der Objektwinkel. Hier steht zusätzlich,
                // wie schnell die Punkte dabei in der Bildmitte liefen.
                .Add(RandomDotSimulatedSweep.ImageCenterSpeed(result.SweepSpeedDegreesPerSecond,
                    trial.InstrumentMagnificationM, trial.ContentZoom))
                .Add(result.MotionConstraint)
                // Ausgefallene Frames während der Darbietung (siehe FrameTimingTracker).
                .Add(result.FrameTiming.Frames).Add(result.FrameTiming.SlowFrames)
                .Add(result.FrameTiming.MaxFrameMilliseconds)
                .Add(result.FrameTiming.ExpectedFrameMilliseconds)
                .Add(result.HeadVelocity.TargetSpeed).Add(result.HeadVelocity.OnsetSpeed)
                .Add(result.HeadVelocity.MeanSpeed).Add(result.HeadVelocity.MinSpeed)
                .Add(result.HeadVelocity.MaxSpeed).Add(result.HeadVelocity.WaitSeconds)
                // Aktive Trials: echte Richtung und Bewegung neben der Schwenkachse.
                .Add(result.HeadDirection).Add(result.HeadOffAxisSpeed)
                .Add(result.HeadOffAxisDeviation).Add(result.HeadRollDeviation));
        }
    }
}
