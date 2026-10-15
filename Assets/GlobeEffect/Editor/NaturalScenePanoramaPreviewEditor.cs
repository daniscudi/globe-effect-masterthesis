using UnityEditor;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Editor
{
    [CustomEditor(typeof(NaturalScenePanoramaPreview))]
    public sealed class NaturalScenePanoramaPreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("panorama"), new GUIContent("Source Image"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("observerCamera"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("usePerspectivePhoto"));
            bool photo = serializedObject.FindProperty("usePerspectivePhoto").boolValue;
            if (photo)
            {
                EditorGUILayout.HelpBox("Limited perspective photo, not a 360 panorama. Image angle is an EXIF " +
                    "estimate, not optical calibration. Amber ring = photo edge reached. R recenters in Play Mode.",
                    MessageType.Info);
                Fields("photoHorizontalFieldOfView", "photoRotationDegrees", "showPhotoBoundaryWarning");
            }
            else Fields("panoramaYawDegrees");
            Fields("distortionK", "magnification", "contentZoom", "fieldOfViewDegrees", "edgeSoftnessDegrees",
                "backgroundColor", "motionMode", "sweepAxis", "sweepDirection", "imageCenterSpeed",
                "imageSweepAmplitudeDegrees", "paused", "showFixationTarget", "fixationSizeDegrees",
                "fixationColor", "showDesktopInstructions");
            serializedObject.ApplyModifiedProperties();
            var preview = (NaturalScenePanoramaPreview)target;
            if (photo)
            {
                EditorGUILayout.LabelField("Estimated Photo Vertical Angle", preview.PhotoVerticalFieldOfView.ToString("0.00") + "°");
                if (Application.isPlaying && preview.PhotoEdgeVisible)
                    EditorGUILayout.HelpBox("PHOTO EDGE: turn back or recenter the photo.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("Center Photo On Current Head Direction")) preview.CenterPhoto();
            }
        }

        private void Fields(params string[] names)
        {
            foreach (string name in names) EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
        }
    }
}
