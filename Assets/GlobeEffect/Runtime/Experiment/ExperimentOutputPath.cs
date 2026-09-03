using System.IO;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Bestimmt den gemeinsamen Ausgabeordner für alle Versuchsteile. Der Pfad
    /// wird aus Unitys eigenem Assets-Pfad aufgebaut und enthält deshalb keinen
    /// Laufwerksbuchstaben, der nur auf einem bestimmten Rechner funktioniert.
    /// </summary>
    public static class ExperimentOutputPath
    {
        /// <summary>
        /// Im Unity Editor liegt Application.dataPath im Assets-Ordner. Eine
        /// Ebene darüber befindet sich der Projektordner. Bei einem späteren
        /// Build liegt der Measurements-Ordner entsprechend neben der Anwendung.
        /// </summary>
        public static string DefaultMeasurementsFolder => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "measurements"));

        /// <summary>
        /// Ein im Inspector eingetragener Pfad hat weiterhin Vorrang. Bleibt
        /// das Feld leer, wird automatisch der portable Standardpfad benutzt.
        /// </summary>
        public static string Resolve(string configuredFolder)
        {
            return string.IsNullOrWhiteSpace(configuredFolder)
                ? DefaultMeasurementsFolder
                : Path.GetFullPath(configuredFolder);
        }
    }
}
