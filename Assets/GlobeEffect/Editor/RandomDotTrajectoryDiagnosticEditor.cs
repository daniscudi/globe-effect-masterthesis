using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEditor;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Editor
{
    [CustomEditor(typeof(RandomDotTrajectoryDiagnostic))]
    public sealed class RandomDotTrajectoryDiagnosticEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Technische Diagnose, keine Messung. Farbige Punkte (Raster unter Marker Columns/Rows) und ihre " +
                "Spuren verwenden den Originalshader. k = 1: Tangensbedingung. " +
                "Anordnung gilt bei Schwenkwinkel null; am Anfang steht das Feld seitlich versetzt. " +
                "Instrument-/Bewegungsänderungen starten neu. Das Messlineal verändert keine Bahnen. " +
                "Leertaste pausiert, R startet neu.", MessageType.Info);
            EditorGUILayout.HelpBox("Flow Coordinates: A ist die flache Bildschirmgeometrie. S und M " +
                "rechnen dieselbe Bewegung in andere Koordinaten um; M ist der Spezialfall l = 0. " +
                "Pfeile sind Messsymbole auf demselben Bildgitter, keine neuen Punktbahnen. " +
                "Daraus folgt noch keine wahrgenommene Zylinder- oder Kugelform.", MessageType.Info);
            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var diagnostic = (RandomDotTrajectoryDiagnostic)target;
                if (GUILayout.Button("Pause / Weiter")) diagnostic.TogglePause();
                if (GUILayout.Button("Neustart und Spuren löschen")) diagnostic.Restart();
            }
        }
    }
}
