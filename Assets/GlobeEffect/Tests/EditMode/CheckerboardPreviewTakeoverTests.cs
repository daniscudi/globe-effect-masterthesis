using GlobeEffect.VRCheckerboard.Experiment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// "Vorschau-Werte übernehmen" und "Originalwerte wiederherstellen" am
    /// Checkerboard Experiment Manager, ohne Play Mode.
    /// </summary>
    public sealed class CheckerboardPreviewTakeoverTests
    {
        private GameObject stimulusObject;
        private GameObject managerObject;
        private VrCheckerboardStimulus stimulus;
        private CheckerboardExperimentManager manager;

        [SetUp]
        public void SetUp()
        {
            stimulusObject = new GameObject("Takeover Test Checkerboard");
            stimulus = stimulusObject.AddComponent<VrCheckerboardStimulus>();
            managerObject = new GameObject("Takeover Test Manager");
            manager = managerObject.AddComponent<CheckerboardExperimentManager>();

            var plan = new SerializedObject(manager);
            plan.FindProperty("stimulus").objectReferenceValue = stimulus;
            SetFloats(plan, "fieldOfViewValues", 90f, 60f);
            SetFloats(plan, "instrumentDistortionKValues", 0.2f, 0.7f, 1.2f);
            SetEyes(plan, CheckerboardEyePresentation.BothEyes);
            plan.ApplyModifiedPropertiesWithoutUndo();

            // Das, was man in der Vorschau eingestellt hätte.
            var preview = new SerializedObject(stimulus);
            preview.FindProperty("fieldOfViewDegrees").floatValue = 70f;
            preview.FindProperty("eyePresentation").enumValueIndex = (int)CheckerboardEyePresentation.LeftEyeOnly;
            preview.FindProperty("instrumentDistortionK").floatValue = 0.9f;
            preview.FindProperty("instrumentMagnificationM").floatValue = 12f;
            preview.FindProperty("edgeSoftnessDegrees").floatValue = 5f;
            preview.FindProperty("gridSpacingDegrees").floatValue = 8f;
            preview.FindProperty("noiseRefreshRateHz").floatValue = 45f;
            preview.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(stimulusObject);
        }

        [Test]
        public void TakeOver_CopiesFovEyeAndMagnificationIntoThePlanButKeepsKList()
        {
            Assert.That(manager.HasValuesBeforePreviewTakeover, Is.False);
            Assert.That(manager.TakeOverPreviewValues(), Is.True);

            var plan = new SerializedObject(manager);
            Assert.That(Floats(plan, "fieldOfViewValues"), Is.EqualTo(new[] { 70f }));
            Assert.That(plan.FindProperty("eyePresentations").arraySize, Is.EqualTo(1));
            Assert.That(plan.FindProperty("eyePresentations").GetArrayElementAtIndex(0).enumValueIndex,
                Is.EqualTo((int)CheckerboardEyePresentation.LeftEyeOnly));
            Assert.That(Floats(plan, "instrumentDistortionKValues"), Is.EqualTo(new[] { 0.2f, 0.7f, 1.2f }));
            Assert.That(plan.FindProperty("instrumentMagnificationM").floatValue, Is.EqualTo(12f));
            Assert.That(manager.HasValuesBeforePreviewTakeover, Is.True);
        }

        [Test]
        public void Restore_BringsBackPlanAndLookFromBeforeTheFirstTakeover()
        {
            manager.TakeOverPreviewValues();

            // Zweites Übernehmen mit anderen Werten darf das Original nicht ersetzen.
            var preview = new SerializedObject(stimulus);
            preview.FindProperty("fieldOfViewDegrees").floatValue = 50f;
            preview.FindProperty("instrumentMagnificationM").floatValue = 20f;
            preview.FindProperty("edgeSoftnessDegrees").floatValue = 9f;
            preview.FindProperty("gridSpacingDegrees").floatValue = 12f;
            preview.ApplyModifiedPropertiesWithoutUndo();
            manager.TakeOverPreviewValues();
            Assert.That(Floats(new SerializedObject(manager), "fieldOfViewValues"), Is.EqualTo(new[] { 50f }));

            Assert.That(manager.RestoreValuesBeforePreviewTakeover(), Is.True);

            var plan = new SerializedObject(manager);
            Assert.That(Floats(plan, "fieldOfViewValues"), Is.EqualTo(new[] { 90f, 60f }));
            Assert.That(plan.FindProperty("instrumentMagnificationM").floatValue, Is.EqualTo(10f));
            Assert.That(plan.FindProperty("eyePresentations").GetArrayElementAtIndex(0).enumValueIndex,
                Is.EqualTo((int)CheckerboardEyePresentation.BothEyes));
            var look = new SerializedObject(stimulus);
            Assert.That(look.FindProperty("instrumentMagnificationM").floatValue, Is.EqualTo(12f));
            Assert.That(look.FindProperty("edgeSoftnessDegrees").floatValue, Is.EqualTo(5f));
            Assert.That(look.FindProperty("gridSpacingDegrees").floatValue, Is.EqualTo(8f));
            Assert.That(look.FindProperty("noiseRefreshRateHz").floatValue, Is.EqualTo(45f));
            Assert.That(manager.HasValuesBeforePreviewTakeover, Is.False);
            Assert.That(manager.RestoreValuesBeforePreviewTakeover(), Is.False);
        }

        private static void SetFloats(SerializedObject owner, string name, params float[] values)
        {
            SerializedProperty list = owner.FindProperty(name);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetEyes(SerializedObject owner, params CheckerboardEyePresentation[] values)
        {
            SerializedProperty list = owner.FindProperty("eyePresentations");
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).enumValueIndex = (int)values[i];
        }

        private static float[] Floats(SerializedObject owner, string name)
        {
            SerializedProperty list = owner.FindProperty(name);
            var values = new float[list.arraySize];
            for (int i = 0; i < values.Length; i++)
                values[i] = list.GetArrayElementAtIndex(i).floatValue;
            return values;
        }
    }
}
