using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>Messwerte und Antwort eines beendeten oder abgebrochenen Trials.</summary>
    public sealed class CheckerboardTrialResult
    {
        public CheckerboardTrial Trial { get; }
        public DateTime TrialStartUtc { get; }
        public double TrialStartUnitySeconds { get; }
        public double TrialEndUnitySeconds { get; }
        public float PhysicalDiameterMeters { get; }
        public float FinalK { get; }
        public int KAdjustmentCount { get; }
        public int RecenterCount { get; }
        public bool FixationSampleValid { get; }
        public bool FixationInsideTolerance { get; }
        public bool FixationRequirementMet { get; }
        public float FixationAngleDegrees { get; }
        public float ContinuousFixationSeconds { get; }
        public string Status { get; }

        public double ResponseTimeSeconds =>
            TrialEndUnitySeconds - TrialStartUnitySeconds;

        public CheckerboardTrialResult(
            CheckerboardTrial trial,
            DateTime trialStartUtc,
            double trialStartUnitySeconds,
            double trialEndUnitySeconds,
            float physicalDiameterMeters,
            float finalK,
            int kAdjustmentCount,
            int recenterCount,
            bool fixationSampleValid,
            bool fixationInsideTolerance,
            bool fixationRequirementMet,
            float fixationAngleDegrees,
            float continuousFixationSeconds,
            string status)
        {
            Trial = trial ?? throw new ArgumentNullException(nameof(trial));
            TrialStartUtc = trialStartUtc;
            TrialStartUnitySeconds = trialStartUnitySeconds;
            TrialEndUnitySeconds = trialEndUnitySeconds;
            PhysicalDiameterMeters = physicalDiameterMeters;
            FinalK = finalK;
            KAdjustmentCount = kAdjustmentCount;
            RecenterCount = recenterCount;
            FixationSampleValid = fixationSampleValid;
            FixationInsideTolerance = fixationInsideTolerance;
            FixationRequirementMet = fixationRequirementMet;
            FixationAngleDegrees = fixationAngleDegrees;
            ContinuousFixationSeconds = continuousFixationSeconds;
            Status = status ?? string.Empty;
        }
    }

    /// <summary>
    /// Legt pro Sitzung einen eindeutigen Ordner, den randomisierten Plan und
    /// eine fortlaufend gesicherte Antwortdatei an.
    /// </summary>
    public sealed class CheckerboardExperimentFiles
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);

        private readonly string participantId;
        private readonly string sessionLabel;
        private readonly DateTime sessionStartUtc;
        private readonly int randomSeed;

        public string SessionFolder { get; }
        public string BaseFileName { get; }
        public string PlanFile { get; }
        public string TrialResultsFile { get; }

        private CheckerboardExperimentFiles(
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

        public static CheckerboardExperimentFiles Create(
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

            string safeParticipant = SanitizeIdentifier(participantId, "pilot");
            string safeSession = SanitizeIdentifier(sessionLabel, "session");
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

            return new CheckerboardExperimentFiles(
                sessionFolder,
                baseName,
                safeParticipant,
                safeSession,
                sessionStartUtc,
                randomSeed);
        }

        public void WritePlan(IReadOnlyList<CheckerboardTrial> trials)
        {
            if (trials == null)
            {
                throw new ArgumentNullException(nameof(trials));
            }

            var builder = new StringBuilder(2048);
            builder.AppendLine(
                "participant_id,session_label,session_start_utc,random_seed," +
                "sequence_index,total_trials,condition_index,repetition," +
                "eye_presentation,angular_diameter_deg,viewing_distance_m," +
                "magnification,starting_k");

            foreach (CheckerboardTrial trial in trials)
            {
                AppendSessionPrefix(builder);
                AppendInteger(builder, trial.SequenceIndex);
                AppendInteger(builder, trials.Count);
                AppendInteger(builder, trial.ConditionIndex);
                AppendInteger(builder, trial.Repetition);
                AppendCsv(builder, trial.EyePresentation.ToString());
                AppendFloat(builder, trial.AngularDiameterDegrees);
                AppendFloat(builder, trial.ViewingDistanceMeters);
                AppendFloat(builder, trial.Magnification);
                AppendFloat(builder, trial.StartingK, terminateRow: true);
            }

            File.WriteAllText(PlanFile, builder.ToString(), Utf8WithoutBom);
            WriteTrialHeader();
        }

        public void AppendResult(CheckerboardTrialResult result, int totalTrials)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var builder = new StringBuilder(768);
            CheckerboardTrial trial = result.Trial;
            AppendSessionPrefix(builder);
            AppendInteger(builder, trial.SequenceIndex);
            AppendInteger(builder, totalTrials);
            AppendInteger(builder, trial.ConditionIndex);
            AppendInteger(builder, trial.Repetition);
            AppendCsv(builder, result.TrialStartUtc.ToString("O", CultureInfo.InvariantCulture));
            AppendDouble(builder, result.TrialStartUnitySeconds);
            AppendDouble(builder, result.TrialEndUnitySeconds);
            AppendDouble(builder, result.ResponseTimeSeconds);
            AppendCsv(builder, trial.EyePresentation.ToString());
            AppendFloat(builder, trial.AngularDiameterDegrees);
            AppendFloat(builder, trial.ViewingDistanceMeters);
            AppendFloat(builder, result.PhysicalDiameterMeters);
            AppendFloat(builder, trial.Magnification);
            AppendFloat(builder, trial.StartingK);
            AppendFloat(builder, result.FinalK);
            AppendInteger(builder, result.KAdjustmentCount);
            AppendInteger(builder, result.RecenterCount);
            AppendBoolean(builder, result.FixationSampleValid);
            AppendBoolean(builder, result.FixationInsideTolerance);
            AppendBoolean(builder, result.FixationRequirementMet);
            AppendFloat(builder, result.FixationAngleDegrees);
            AppendFloat(builder, result.ContinuousFixationSeconds);
            AppendCsv(builder, result.Status, terminateRow: true);

            File.AppendAllText(TrialResultsFile, builder.ToString(), Utf8WithoutBom);
        }

        public static string SanitizeIdentifier(string value, string fallback)
        {
            string source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            var builder = new StringBuilder(source.Length);
            bool previousWasSeparator = false;

            foreach (char character in source)
            {
                bool isAllowed = char.IsLetterOrDigit(character) ||
                    character == '-' || character == '_';
                char output = isAllowed ? character : '_';

                if (output == '_' && previousWasSeparator)
                {
                    continue;
                }

                builder.Append(output);
                previousWasSeparator = output == '_';
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        private void WriteTrialHeader()
        {
            const string header =
                "participant_id,session_label,session_start_utc,random_seed," +
                "sequence_index,total_trials,condition_index,repetition," +
                "trial_start_utc,trial_start_unity_s,trial_end_unity_s," +
                "response_time_s,eye_presentation,angular_diameter_deg," +
                "viewing_distance_m,physical_diameter_m,magnification," +
                "starting_k,final_k,k_adjustment_count,recenter_count," +
                "fixation_sample_valid,fixation_inside_tolerance," +
                "fixation_requirement_met,fixation_angle_deg," +
                "continuous_fixation_s,status";
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
        }

        private static void AppendFloat(
            StringBuilder builder,
            float value,
            bool terminateRow = false)
        {
            AppendCsv(
                builder,
                value.ToString("G9", CultureInfo.InvariantCulture),
                terminateRow);
        }

        private static void AppendDouble(
            StringBuilder builder,
            double value,
            bool terminateRow = false)
        {
            AppendCsv(
                builder,
                value.ToString("G17", CultureInfo.InvariantCulture),
                terminateRow);
        }

        private static void AppendInteger(
            StringBuilder builder,
            int value,
            bool terminateRow = false)
        {
            AppendCsv(
                builder,
                value.ToString(CultureInfo.InvariantCulture),
                terminateRow);
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
