using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Der gemeinsame Teil der beiden CSV-Schreiber für Checkerboard und Random Dot.
    ///
    /// Jede Sitzung bekommt einen eigenen Ordner. Geschrieben wird sofort nach
    /// jedem Durchgang. Bricht man später ab oder stürzt Unity ab, sind die bis
    /// dahin gemessenen Daten trotzdem alle da.
    /// </summary>
    public abstract class ExperimentFilesBase
    {
        // Pro Sitzung entstehen zwei Dateien:
        // plan.csv steht vorher fest und zeigt, was in welcher Reihenfolge kommt.
        // trials.csv wächst während der Messung, nach jedem Durchgang eine Zeile mehr.
        // Auch schiefgegangene Versuche kommen rein.
        //
        // Die vielen Blickdaten pro Sekunde stehen nicht hier drin. Die schreibt
        // die Lab-Toolbox in ihre eigenen Dateien.

        // Diese fünf Spalten stehen am Anfang jeder Zeile, in allen Dateien.
        // Dadurch weiß man bei jeder einzelnen Zeile, aus welcher Sitzung sie
        // stammt, auch wenn man die Datei später woanders hinkopiert.
        protected const string SessionColumns =
            "participant_id,session_label,session_start_utc,random_seed,mapping_version,";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private readonly string participantId;
        private readonly string sessionLabel;
        private readonly DateTime sessionStartUtc;
        private readonly int randomSeed;
        private readonly string mappingVersion;

        protected ExperimentFilesBase(
            string outputRoot,
            string participantId,
            string sessionLabel,
            string fallbackSessionLabel,
            DateTime sessionStartUtc,
            int randomSeed,
            string mappingVersion)
        {
            // Legt für jede Sitzung einen eigenen Ordner an, benannt nach Person,
            // Datum und Uhrzeit.
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException(
                    "Ein Ausgabeverzeichnis ist erforderlich.", nameof(outputRoot));
            }

            this.participantId = SanitizeIdentifier(participantId, "pilot");
            this.sessionLabel = SanitizeIdentifier(sessionLabel, fallbackSessionLabel);
            this.sessionStartUtc = sessionStartUtc;
            this.randomSeed = randomSeed;
            this.mappingVersion = mappingVersion;

            string timestamp = sessionStartUtc.ToLocalTime()
                .ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string personFolder = Path.Combine(Path.GetFullPath(outputRoot), this.participantId);
            Directory.CreateDirectory(personFolder);

            // Gibt es den Ordner schon, wird hinten eine Zahl angehängt. So wird
            // niemals eine vorhandene Messung überschrieben.
            string folderStem = timestamp + "_" + this.sessionLabel;
            string sessionFolder = Path.Combine(personFolder, folderStem);
            for (int suffix = 1; Directory.Exists(sessionFolder); suffix++)
            {
                sessionFolder = Path.Combine(personFolder, folderStem + "_" + suffix);
            }

            Directory.CreateDirectory(sessionFolder);
            SessionFolder = sessionFolder;
            BaseFileName = this.participantId + "_" + this.sessionLabel + "_" + timestamp;
            PlanFile = Path.Combine(sessionFolder, BaseFileName + "_plan.csv");
            TrialResultsFile = Path.Combine(sessionFolder, BaseFileName + "_trials.csv");
        }

        public string SessionFolder { get; }
        public string BaseFileName { get; }
        public string PlanFile { get; }
        public string TrialResultsFile { get; }

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
                bool isAllowed = char.IsLetterOrDigit(character)
                    || character == '-' || character == '_';
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

        protected CsvRow StartRow()
        {
            // Diese fünf Angaben stehen am Anfang jeder Zeile, in beiden Dateien.
            // Dadurch weiß man bei jeder einzelnen Zeile, aus welcher Sitzung sie
            // stammt und mit welcher Formel gerechnet wurde.
            return new CsvRow()
                .Add(participantId)
                .Add(sessionLabel)
                .Add(sessionStartUtc)
                .Add(randomSeed)
                .Add(mappingVersion);
        }

        protected void WritePlanFile(
            string planHeader, IEnumerable<CsvRow> planRows, string trialHeader)
        {
            // Wird einmal vor der Messung aufgerufen und schreibt die gemischte
            // Reihenfolge weg. Danach wird gleich noch die Kopfzeile für die
            // Ergebnisdatei angelegt.
            var plan = new StringBuilder(4096);
            plan.AppendLine(planHeader);
            foreach (CsvRow row in planRows)
            {
                plan.Append(row);
            }

            File.WriteAllText(PlanFile, plan.ToString(), Utf8WithoutBom);
            File.WriteAllText(TrialResultsFile, trialHeader + Environment.NewLine, Utf8WithoutBom);
        }

        protected void AppendResultRow(CsvRow row)
        {
            // Hängt für einen gezeigten Durchgang genau eine Zeile an. Schon
            // geschriebene Zeilen werden dabei nie wieder angefasst.
            File.AppendAllText(TrialResultsFile, row.ToString(), Utf8WithoutBom);
        }
    }
}
