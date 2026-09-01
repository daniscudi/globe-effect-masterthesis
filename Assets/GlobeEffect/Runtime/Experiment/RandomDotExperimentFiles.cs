using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Schreibt Plan und Antworten des Random-Dot-Tests getrennt von den
    /// hochfrequenten Gaze-/Head-Dateien der Lab-Toolbox.
    /// </summary>
    public sealed class RandomDotExperimentFiles
    {
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
            string safeParticipant = CheckerboardExperimentFiles.SanitizeIdentifier(
                participantId,
                "pilot");
            string safeSession = CheckerboardExperimentFiles.SanitizeIdentifier(
                sessionLabel,
                "random_dot");
            string participantFolder = Path.Combine(outputRoot, safeParticipant);
            Directory.CreateDirectory(participantFolder);

            string timestamp = sessionStartUtc.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture);
            string folderStem = safeSession + "_" + timestamp;
            string sessionFolder = Path.Combine(participantFolder, folderStem);
            int suffix = 1;
            // Falls zwei Sitzungen dieselbe Sekundenangabe besitzen, verhindert
            // der Suffix ein Überschreiben der ersten Messung.
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
            // Der vollständige randomisierte Plan wird vor der Messung gesichert.
            // Dot-Seed und Bedingungsnummer sind damit unabhängig rekonstruierbar.
            var builder = new StringBuilder(2048);
            builder.AppendLine(
                "participant_id,session_label,session_start_utc,random_seed," +
                "sequence_index,total_trials,condition_index,repetition," +
                "eye_presentation,angular_diameter_deg,magnification," +
                "motion_mode,starting_k,dot_seed");

            foreach (RandomDotTrial trial in trials)
            {
                AppendSessionPrefix(builder);
                AppendInteger(builder, trial.SequenceIndex);
                AppendInteger(builder, trials.Count);
                AppendInteger(builder, trial.ConditionIndex);
                AppendInteger(builder, trial.Repetition);
                AppendCsv(builder, trial.EyePresentation.ToString());
                AppendFloat(builder, trial.AngularDiameterDegrees);
                AppendFloat(builder, trial.Magnification);
                AppendCsv(builder, trial.MotionMode.ToString());
                AppendFloat(builder, trial.StartingK);
                AppendInteger(builder, trial.DotSeed, terminateRow: true);
            }

            File.WriteAllText(PlanFile, builder.ToString(), Utf8WithoutBom);
            WriteTrialHeader();
        }

        public void AppendResult(RandomDotTrialResult result, int totalTrials)
        {
            // Pro abgeschlossenem Trial wird genau eine Zeile geschrieben. Neben
            // der Antwort enthält sie Bewegungsumfang, Sweep-Zahl und Fixationswerte.
            var builder = new StringBuilder(1024);
            RandomDotTrial trial = result.Trial;
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
            AppendFloat(builder, trial.Magnification);
            AppendCsv(builder, trial.MotionMode.ToString());
            AppendFloat(builder, trial.StartingK);
            AppendFloat(builder, result.FinalK);
            AppendInteger(builder, result.KAdjustmentCount);
            AppendInteger(builder, result.RecenterCount);
            AppendInteger(builder, trial.DotSeed);
            AppendInteger(builder, result.DotCount);
            AppendFloat(builder, result.WorldCoverageDiameterDegrees);
            AppendFloat(builder, result.FieldRadiusMeters);
            AppendFloat(builder, result.SweepThresholdDegrees);
            AppendInteger(builder, result.CompletedHalfSweeps);
            AppendFloat(builder, result.MinimumYawDegrees);
            AppendFloat(builder, result.MaximumYawDegrees);
            AppendBoolean(builder, result.FixationSampleValid);
            AppendBoolean(builder, result.FixationInsideTolerance);
            AppendBoolean(builder, result.FixationRequirementMet);
            AppendFloat(builder, result.FixationAngleDegrees);
            AppendFloat(builder, result.ContinuousFixationSeconds);
            AppendCsv(builder, result.Status, terminateRow: true);
            File.AppendAllText(TrialResultsFile, builder.ToString(), Utf8WithoutBom);
        }

        private void WriteTrialHeader()
        {
            const string header =
                "participant_id,session_label,session_start_utc,random_seed," +
                "sequence_index,total_trials,condition_index,repetition," +
                "trial_start_utc,trial_start_unity_s,trial_end_unity_s," +
                "response_time_s,eye_presentation,angular_diameter_deg," +
                "magnification,motion_mode,starting_k,final_k," +
                "k_adjustment_count,recenter_count,dot_seed,dot_count," +
                "world_coverage_diameter_deg,field_radius_m," +
                "sweep_threshold_deg,completed_half_sweeps,min_yaw_deg,max_yaw_deg," +
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
            // Felder werden nur dann in Anführungszeichen gesetzt, wenn CSV-Syntax
            // oder ein Zeilenumbruch es erfordern. Eingebettete Anführungszeichen
            // müssen nach CSV-Regel doppelt geschrieben werden.
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
