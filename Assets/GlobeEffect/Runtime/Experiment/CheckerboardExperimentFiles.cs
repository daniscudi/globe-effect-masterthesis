using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

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

        public double StimulusDurationSeconds =>
            Math.Max(0d, StimulusEndUnitySeconds - TrialStartUnitySeconds);

        public double NoiseMaskDurationSeconds =>
            Math.Max(0d, TrialEndUnitySeconds - ResponseWindowStartUnitySeconds);

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
            string status)
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
        }
    }

    /// <summary>
    /// Schreibt die CSV-Dateien für den Checkerboard-Test.
    ///
    /// Jede Sitzung bekommt einen eigenen Ordner. Geschrieben wird sofort nach
    /// jedem Durchgang. Bricht man später ab oder stürzt Unity ab, sind die bis
    /// dahin gemessenen Daten trotzdem alle da.
    /// </summary>
    public sealed class CheckerboardExperimentFiles
    {
        // Pro Sitzung entstehen zwei Dateien:
        // plan.csv steht vorher fest und zeigt, was in welcher Reihenfolge kommt.
        // trials.csv wächst während der Messung, nach jedem Durchgang eine Zeile mehr.
        //
        // Die vielen Blickdaten pro Sekunde stehen nicht hier drin. Die schreibt
        // die Lab-Toolbox in ihre eigenen Dateien.
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
            // Legt für jede Sitzung einen eigenen Ordner an, benannt nach Person,
            // Datum und Uhrzeit.
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

            // Gibt es den Ordner schon, wird hinten eine Zahl angehängt. So wird
            // niemals eine vorhandene Messung überschrieben.
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

        public void WritePlan(
            IReadOnlyList<CheckerboardTrial> trials,
            float gridLineSpacingDegrees,
            float stimulusDurationSeconds,
            float postResponseNoiseSeconds,
            float responseTimeoutSeconds,
            string categoryAKey,
            string categoryBKey,
            bool responseKeysSwapped)
        {
            // Wird einmal vor der Messung aufgerufen und schreibt die gemischte
            // Reihenfolge weg. Danach wird gleich noch die Kopfzeile für die
            // Ergebnisdatei angelegt.
            if (trials == null)
            {
                throw new ArgumentNullException(nameof(trials));
            }

            var builder = new StringBuilder(2048);
            builder.AppendLine(
                "participant_id,session_label,session_start_utc,random_seed,mapping_version," +
                "sequence_index,total_planned_trials,condition_index,repetition," +
                "eye_presentation,angular_diameter_deg,grid_line_spacing_deg," +
                "grid_line_spacing_uv,visual_space_l,content_zoom," +
                "oomes_endpoint_equivalent,stimulus_duration_s," +
                "noise_until_response,post_response_noise_s,response_timeout_s," +
                "category_a_key,category_b_key,response_keys_swapped");

            foreach (CheckerboardTrial trial in trials)
            {
                AppendSessionPrefix(builder);
                AppendInteger(builder, trial.SequenceIndex);
                AppendInteger(builder, trials.Count);
                AppendInteger(builder, trial.ConditionIndex);
                AppendInteger(builder, trial.Repetition);
                AppendCsv(builder, trial.EyePresentation.ToString());
                AppendFloat(builder, trial.AngularDiameterDegrees);
                AppendFloat(builder, gridLineSpacingDegrees);
                AppendDouble(
                    builder,
                    VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                        trial.AngularDiameterDegrees,
                        gridLineSpacingDegrees));
                AppendFloat(builder, trial.VisualSpaceL);
                AppendFloat(builder, trial.ContentZoom);
                AppendDouble(
                    builder,
                    VisualSpaceRadialMapping.OomesEndpointEquivalent(
                        trial.VisualSpaceL));
                AppendFloat(builder, stimulusDurationSeconds);
                AppendBoolean(builder, true);
                AppendFloat(builder, postResponseNoiseSeconds);
                AppendFloat(builder, responseTimeoutSeconds);
                AppendCsv(builder, categoryAKey);
                AppendCsv(builder, categoryBKey);
                AppendBoolean(builder, responseKeysSwapped, terminateRow: true);
            }

            File.WriteAllText(PlanFile, builder.ToString(), Utf8WithoutBom);
            WriteTrialHeader();
        }

        public void AppendResult(CheckerboardTrialResult result, int plannedTrials)
        {
            // Hängt für einen gezeigten Durchgang genau eine Zeile an. Schon
            // geschriebene Zeilen werden dabei nie wieder angefasst.
            //
            // Die Reihenfolge der Werte hier muss exakt zur Kopfzeile in
            // WriteTrialHeader passen, sonst stehen die Zahlen später in den
            // falschen Spalten.
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var builder = new StringBuilder(768);
            CheckerboardTrial trial = result.Trial;
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
            AppendDouble(builder, result.ResponseWindowStartUnitySeconds);
            AppendDouble(builder, result.TrialEndUnitySeconds);
            AppendDouble(builder, result.StimulusDurationSeconds);
            AppendDouble(builder, result.NoiseMaskDurationSeconds);
            AppendDouble(builder, result.ResponseTimeSeconds);
            AppendCsv(builder, trial.EyePresentation.ToString());
            AppendFloat(builder, trial.AngularDiameterDegrees);
            AppendFloat(builder, result.ApertureEdgeSoftnessDegrees);
            AppendBoolean(builder, result.CircularApertureEnabled);
            AppendFloat(builder, result.GridLineSpacingDegrees);
            AppendFloat(builder, result.GridLineSpacingUv);
            AppendFloat(builder, trial.VisualSpaceL);
            AppendFloat(builder, trial.ContentZoom);
            AppendDouble(
                builder,
                VisualSpaceRadialMapping.OomesEndpointEquivalent(
                    trial.VisualSpaceL));
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

        public static string SanitizeIdentifier(string value, string fallback)
        {
            // Macht aus einer Eingabe einen Namen, der als Datei- oder Ordnername
            // funktioniert. Alles, was Ärger machen könnte, wird zu einem
            // Unterstrich. Mehrere Unterstriche hintereinander werden zu einem
            // zusammengezogen, und am Anfang und Ende fliegen sie ganz weg.
            //
            // Bleibt am Schluss nichts übrig, wird der Ersatzname genommen.
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
            // Die Kopfzeile der Ergebnisdatei. Sie wird einmal am Anfang geschrieben
            // und muss Spalte für Spalte zu AppendResult passen.
            const string header =
                "participant_id,session_label,session_start_utc,random_seed,mapping_version," +
                "presentation_index,sequence_index,total_planned_trials," +
                "condition_index,repetition,attempt_number,trial_start_utc," +
                "trial_start_unity_s,stimulus_end_unity_s,response_window_start_unity_s," +
                "trial_end_unity_s,stimulus_duration_s,noise_mask_duration_s,response_time_s," +
                "eye_presentation,angular_diameter_deg,aperture_edge_softness_deg," +
                "circular_aperture_enabled,grid_line_spacing_deg," +
                "grid_line_spacing_uv,visual_space_l,content_zoom," +
                "oomes_endpoint_equivalent,response," +
                "valid_for_analysis,fixation_sample_valid," +
                "fixation_inside_tolerance,fixation_angle_deg," +
                "continuous_fixation_s,fixation_valid_sample_fraction," +
                "longest_off_target_s," +
                "longest_invalid_gaze_s,status";
            File.WriteAllText(
                TrialResultsFile,
                header + Environment.NewLine,
                Utf8WithoutBom);
        }

        private void AppendSessionPrefix(StringBuilder builder)
        {
            // Diese fünf Angaben stehen am Anfang jeder Zeile, in beiden Dateien.
            // Dadurch weiß man bei jeder einzelnen Zeile, aus welcher Sitzung sie
            // stammt und mit welcher Formel gerechnet wurde.
            AppendCsv(builder, participantId);
            AppendCsv(builder, sessionLabel);
            AppendCsv(builder, sessionStartUtc.ToString("O", CultureInfo.InvariantCulture));
            AppendInteger(builder, randomSeed);
            AppendCsv(builder, VisualSpaceRadialMapping.MappingVersion);
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
            // Steht in einem Wert ein Komma, ein Anführungszeichen oder ein
            // Zeilenumbruch, würde die CSV-Datei durcheinanderkommen. Solche Werte
            // werden deshalb in Anführungszeichen gesetzt, und Anführungszeichen
            // darin werden verdoppelt. Das sind die normalen CSV-Regeln.
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
