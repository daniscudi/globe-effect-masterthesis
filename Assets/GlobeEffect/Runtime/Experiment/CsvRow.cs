using System;
using System.Globalization;
using System.Text;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Baut eine CSV-Zeile Wert für Wert zusammen.
    ///
    /// Jeder Datentyp wird immer gleich geschrieben: Kommazahlen mit Punkt und
    /// voller Genauigkeit, true/false als 1/0. So sehen alle CSV-Dateien gleich aus,
    /// egal auf welchem Rechner mit welcher Spracheinstellung gemessen wurde.
    /// </summary>
    public sealed class CsvRow
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly char[] CharactersNeedingQuotes = { ',', '"', '\n', '\r' };
        private readonly StringBuilder text = new StringBuilder(512);
        private bool hasValues;

        public CsvRow Add(string value)
        {
            if (hasValues)
            {
                text.Append(',');
            }

            hasValues = true;

            // Steht in einem Wert ein Komma, ein Anführungszeichen oder ein
            // Zeilenumbruch, würde die CSV-Datei durcheinanderkommen. Solche Werte
            // werden deshalb in Anführungszeichen gesetzt, und Anführungszeichen
            // darin werden verdoppelt. Das sind die normalen CSV-Regeln.
            string safeValue = value ?? string.Empty;
            bool quote = safeValue.IndexOfAny(CharactersNeedingQuotes) >= 0;
            text.Append(quote ? "\"" + safeValue.Replace("\"", "\"\"") + "\"" : safeValue);
            return this;
        }

        public CsvRow Add(int value) => Add(value.ToString(Invariant));

        public CsvRow Add(float value) => Add(value.ToString("G9", Invariant));

        public CsvRow Add(double value) => Add(value.ToString("G17", Invariant));

        public CsvRow Add(bool value) => Add(value ? "1" : "0");

        public CsvRow Add(DateTime value) => Add(value.ToString("O", Invariant));

        public override string ToString() => text + Environment.NewLine;
    }
}
