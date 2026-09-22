using System.IO;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Sucht den Ordner, in den die Messdaten geschrieben werden.
    ///
    /// Der Pfad wird aus Unitys eigenem Assets-Pfad gebaut. Deshalb steht hier
    /// nirgends ein fester Laufwerksbuchstabe. Das Projekt läuft also auch dann,
    /// wenn es auf dem Labor-PC auf einem anderen Laufwerk liegt.
    /// </summary>
    public static class ExperimentOutputPath
    {
        /// <summary>
        /// Im Unity Editor zeigt Application.dataPath auf den Assets-Ordner. Eine
        /// Ebene darüber liegt der Projektordner, und da hinein kommt der Ordner
        /// "measurements". Baut man das Projekt später als Programm, landet er
        /// neben der fertigen Anwendung.
        /// </summary>
        public static string DefaultMeasurementsFolder => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "measurements"));

        /// <summary>
        /// Steht im Inspector ein eigener Pfad, wird der genommen. Ist das Feld
        /// leer, nimmt das Skript den Standardordner von oben.
        /// </summary>
        public static string Resolve(string configuredFolder)
        {
            return string.IsNullOrWhiteSpace(configuredFolder)
                ? DefaultMeasurementsFolder
                : Path.GetFullPath(configuredFolder);
        }
    }
}
