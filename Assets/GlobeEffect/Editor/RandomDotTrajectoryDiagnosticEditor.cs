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
            EditorGUILayout.HelpBox("Technical check, not a measurement. Colored markers and their trails " +
                "show how the dots move with the same shader as in the experiment. k = 1 is straight. " +
                "Changing the binoculars or the movement restarts it. Space = pause, R = restart.",
                MessageType.Info);
            EditorGUILayout.HelpBox("Flow Coordinates: A = speeds on the flat screen. S and M show the same " +
                "movement in other units (M is the case l = 0). The arrows only show speed, they are not " +
                "dot paths, and they do not tell which shape a person sees.", MessageType.Info);
            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                var diagnostic = (RandomDotTrajectoryDiagnostic)target;
                if (GUILayout.Button("Pause / Continue")) diagnostic.TogglePause();
                if (GUILayout.Button("Restart And Clear Trails")) diagnostic.Restart();
            }
        }
    }
}
