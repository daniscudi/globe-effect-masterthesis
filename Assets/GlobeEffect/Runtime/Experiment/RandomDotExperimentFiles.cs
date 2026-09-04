using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Schreibt den vorab randomisierten Plan und jede tatsächliche
    /// Random-Dot-Präsentation. Die hochfrequenten Blickdaten bleiben in den
    /// Dateien der vorhandenen Lab-Toolbox.
    /// </summary>
    public sealed class RandomDotExperimentFiles
    {
        public const string MappingVersion =
            "visual-space-l-directional-content-zoom-v1";

        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private readonly string participantId;
        private readonly string sessionLabel;
        private readonly DateTime sessionStartUtc;
        private readonly int randomSeed;

        private RandomDotExperimentFiles(
            string sessionFolder,
            string baseFileName,
            string participantId,
            string sessionLabel,
            DateTime sessionStartUtc,
            int randomSeed)
        {
            SessionFolder = sessionFolder;
            BaseFileName = baseFileName;
            PlanFile = Path.Combine(sessionFolder, baseFileName + "_plan.csv");
            TrialResultsFile = Path.Combine(sessionFolder, baseFileName + "_trials.csv");
            this.participantId = participantId;
            this.sessionLabel = sessionLabel;
            this.sessionStartUtc = sessionStartUtc;
            this.randomSeed = randomSeed;
        }

        public string SessionFolder { get; }
        public string BaseFileName { get; }
        public string PlanFile { get; }
        public string TrialResultsFile { get; }

        public static RandomDotExperimentFiles Create(
            string outputRoot,
            string participantId,
            string sessionLabel,
            DateTime sessionStartUtc,
            int randomSeed)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException(
                    "Ein Ausgabeverzeichnis ist erforderlich.",
                    nameof(outputRoot));
            }

            string safeParticipant = CheckerboardExperimentFiles.SanitizeIdentifier(
                participantId,
                "pilot");
            string safeSession = CheckerboardExperimentFiles.SanitizeIdentifier(
                sessionLabel,
                "random_dot");
            string timestamp = sessionStartUtc.ToLocalTime().ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);
            string participantFolder = Path.Combine(
                Path.GetFullPath(outputRoot),
                safeParticipant);
            Directory.CreateDirectory(participantFolder);

            string folderStem = timestamp + "_" + safeSession;
            string sessionFolder = Path.Combine(participantFolder, folderStem);
            int suffix = 1;
            while (Directory.Exists(sessionFolder))
            {
                sessionFolder = Path.Combine(
                    participantFolder,
                    folderStem + "_" + suffix.ToString(CultureInfo.InvariantCulture));
                suffix++;
            }

            Directory.CreateDirectory(sessionFolder);
            string baseName = safeParticipant + "_" + safeSession + "_" + timestamp;
            return new RandomDotExperimentFiles(
                sessionFolder,
                baseName,
                safeParticipant,
                safeSession,
                sessionStartUtc,
                randomSeed);
        }

        public void WritePlan(IReadOnlyList<RandomDotTrial> trials)
        {
            if (trials == null)
            {
                throw new ArgumentNullException(nameof(trials));
            }

            var builder = new StringBuilder(2048);
            builder.AppendLine(
                "participant_id,session_label,session_start_utc,random_seed,mapping_version," +
                "sequence_index,total_planned_trials,condition_index,repetition," +
                "eye_presentation,angular_diameter_deg,visual_space_l," +
                "content_zoom,motion_mode,sweep_direction,dot_seed");

            foreach (RandomDotTrial trial in trials)
            {
                AppendSessionPrefix(builder);
                AppendInteger(builder, trial.SequenceIndex);
                AppendInteger(builder, trials.Count);
                AppendInteger(builder, trial.ConditionIndex);
                AppendInteger(builder, trial.Repetition);
                AppendCsv(builder, trial.EyePresentation.ToString());
                AppendFloat(builder, trial.AngularDiameterDegrees);
                AppendFloat(builder, trial.VisualSpaceL);
                AppendFloat(builder, trial.ContentZoom);
                AppendCsv(builder, trial.MotionMode.ToString());
                AppendCsv(builder, trial.SweepDirection.ToString());
                AppendInteger(builder, trial.DotSeed, terminateRow: true);
            }

            File.WriteAllText(PlanFile, builder.ToString(), Utf8WithoutBom);
            WriteTrialHeader();
        }

        public void AppendResult(RandomDotTrialResult result, int plannedTrials)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var builder = new StringBuilder(1200);
            RandomDotTrial trial = result.Trial;
            AppendSessionPrefix(builder);
            AppendInteger(builder, result.PresentationIndex);
            AppendInteger(builder, trial.SequenceIndex);
            AppendInteger(builder, plannedTrials);
            AppendInteger(builder, trial.ConditionIndex);
            AppendInteger(builder, trial.Repetition);
            AppendInteger(builder, trial.AttemptNumber);
            AppendCsv(builder, result.TrialStartUtc.ToString("O", CultureInfo.InvariantCulture));
            AppendDouble(builder, result.TrialStartUnitySeconds);
            AppendDouble(builder, result.StimulusEndUnitySeconds);
            AppendDouble(builder, result.ResponseUnitySeconds);
            AppendDouble(builder, result.StimulusDurationSeconds);
            AppendDouble(builder, result.ResponseTimeSeconds);
            AppendCsv(builder, trial.EyePresentation.ToString());
            AppendFloat(builder, trial.AngularDiameterDegrees);
            AppendFloat(builder, result.ApertureEdgeSoftnessDegrees);
            AppendFloat(builder, trial.VisualSpaceL);
            AppendFloat(builder, trial.ContentZoom);
            AppendCsv(builder, trial.MotionMode.ToString());
            AppendCsv(builder, trial.SweepDirection.ToString());
            AppendFloat(builder, result.SweepAmplitudeDegrees);
            AppendFloat(builder, result.SweepSpeedDegreesPerSecond);
            AppendInteger(builder, result.CompletedHalfSweeps);
            AppendFloat(builder, result.MinimumYawDegrees);
            AppendFloat(builder, result.MaximumYawDegrees);
            AppendInteger(builder, trial.DotSeed);
            AppendInteger(builder, result.DotCount);
            AppendFloat(builder, result.WorldCoverageDiameterDegrees);
            AppendFloat(builder, result.CarrierRadiusMeters);
            AppendCsv(builder, result.Response.ToString());
            AppendBoolean(builder, result.ValidForAnalysis);
            AppendBoolean(builder, result.FixationSampleValid);
            AppendBoolean(builder, result.FixationInsideTolerance);
            AppendFloat(builder, result.FixationAngleDegrees);
            AppendFloat(builder, result.ContinuousFixationSeconds);
            AppendFloat(builder, result.FixationValidSampleFraction);
            AppendFloat(builder, result.LongestOffTargetSeconds);
            AppendFloat(builder, result.LongestInvalidGazeSeconds);
            AppendCsv(builder, result.Status, terminateRow: true);
            File.AppendAllText(TrialResultsFile, builder.ToString(), Utf8WithoutBom);
        }

        private void WriteTrialHeader()
        {
            const string header =
                "participant_id,session_label,session_start_utc,random_seed,mapping_version," +
                "presentation_index,sequence_index,total_planned_trials," +
                "condition_index,repetition,attempt_number,trial_start_utc," +
                "trial_start_unity_s,stimulus_end_unity_s,response_unity_s," +
                "stimulus_duration_s,response_time_s,eye_presentation," +
                "angular_diameter_deg,aperture_edge_softness_deg,visual_space_l," +
                "content_zoom,motion_mode,sweep_direction,sweep_amplitude_deg," +
                "sweep_speed_deg_per_s,completed_half_sweeps,min_yaw_deg,max_yaw_deg," +
                "dot_seed,dot_count,world_coverage_diameter_deg,carrier_radius_m," +
                "response,valid_for_analysis,fixation_sample_valid," +
                "fixation_inside_tolerance,fixation_angle_deg,continuous_fixation_s," +
                "fixation_valid_sample_fraction,longest_off_target_s," +
                "longest_invalid_gaze_s,status";
            File.WriteAllText(
                TrialResultsFile,
                header + Environment.NewLine,
                Utf8WithoutBom);
        }

        private void AppendSessionPrefix(StringBuilder builder)
        {
            AppendCsv(builder, participantId);
            AppendCsv(builder, sessionLabel);
            AppendCsv(builder, sessionStartUtc.ToString("O", CultureInfo.InvariantCulture));
            AppendInteger(builder, randomSeed);
            AppendCsv(builder, MappingVersion);
        }

        private static void AppendFloat(
            StringBuilder builder,
            float value,
            bool terminateRow = false)
        {
            AppendCsv(builder, value.ToString("G9", CultureInfo.InvariantCulture), terminateRow);
        }

        private static void AppendDouble(
            StringBuilder builder,
            double value,
            bool terminateRow = false)
        {
            AppendCsv(builder, value.ToString("G17", CultureInfo.InvariantCulture), terminateRow);
        }

        private static void AppendInteger(
            StringBuilder builder,
            int value,
            bool terminateRow = false)
        {
            AppendCsv(builder, value.ToString(CultureInfo.InvariantCulture), terminateRow);
        }

        private static void AppendBoolean(
            StringBuilder builder,
            bool value,
            bool terminateRow = false)
        {
            AppendCsv(builder, value ? "1" : "0", terminateRow);
        }

        private static void AppendCsv(
            StringBuilder builder,
            string value,
            bool terminateRow = false)
        {
            string safeValue = value ?? string.Empty;
            bool quote = safeValue.IndexOf(',') >= 0 ||
                safeValue.IndexOf('"') >= 0 ||
                safeValue.IndexOf('\n') >= 0 ||
                safeValue.IndexOf('\r') >= 0;
            if (quote)
            {
                builder.Append('"')
                    .Append(safeValue.Replace("\"", "\"\""))
                    .Append('"');
            }
            else
            {
                builder.Append(safeValue);
            }

            builder.Append(terminateRow ? Environment.NewLine : ",");
        }
    }
}
