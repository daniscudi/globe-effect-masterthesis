using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// "Vorschau-Werte übernehmen" und "Originalwerte wiederherstellen" am
    /// Random Dot Experiment Manager, ohne Play Mode.
    /// </summary>
    public sealed class RandomDotPreviewTakeoverTests
    {
        private GameObject fieldObject;
        private GameObject managerObject;
        private RandomDotFieldStimulus stimulus;
        private RandomDotExperimentManager manager;

        [SetUp]
        public void SetUp()
        {
            fieldObject = new GameObject("Takeover Test Field");
            stimulus = fieldObject.AddComponent<RandomDotFieldStimulus>();
            managerObject = new GameObject("Takeover Test Manager");
            manager = managerObject.AddComponent<RandomDotExperimentManager>();

            var plan = new SerializedObject(manager);
            plan.FindProperty("stimulus").objectReferenceValue = stimulus;
            SetFloats(plan, "fieldOfViewValues", 90f, 70f);
            SetFloats(plan, "instrumentMagnificationMValues", 10f);
            SetFloats(plan, "contentZoomValues", 1f);
            SetFloats(plan, "instrumentDistortionKValues", 1.2f, 1f, 0.5f);
            plan.FindProperty("sessionMotionMode").enumValueIndex = (int)RandomDotMotionMode.SimulatedYaw;
            plan.FindProperty("simulatedSweepAxis").enumValueIndex = (int)RandomDotSweepAxis.Horizontal;
            plan.FindProperty("simulatedSpeedReference").enumValueIndex = (int)RandomDotSweepSpeedReference.ImageCenter;
            plan.FindProperty("simulatedImageCenterSpeed").floatValue = 12f;
            plan.FindProperty("sweepSpeed").floatValue = 1.2f;
            plan.ApplyModifiedPropertiesWithoutUndo();

            // Das, was man in der Vorschau eingestellt hätte.
            var preview = new SerializedObject(stimulus);
            preview.FindProperty("fieldOfViewDegrees").floatValue = 60f;
            preview.FindProperty("instrumentMagnificationM").floatValue = 15f;
            preview.FindProperty("contentZoom").floatValue = 1f;
            preview.FindProperty("motionMode").enumValueIndex = (int)RandomDotMotionMode.HeadTracked;
            preview.FindProperty("sweepAxis").enumValueIndex = (int)RandomDotSweepAxis.Vertical;
            preview.FindProperty("previewSpeedReference").enumValueIndex = (int)RandomDotSweepSpeedReference.ImageCenter;
            preview.FindProperty("previewImageCenterSpeed").floatValue = 20f;
            preview.FindProperty("simulatedYawSpeedDegreesPerSecond").floatValue = 5f;
            preview.FindProperty("edgeSoftnessDegrees").floatValue = 2f;
            preview.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(fieldObject);
        }

        [Test]
        public void TakeOver_CopiesPreviewIntoPlanButKeepsK()
        {
            Assert.That(manager.HasValuesBeforePreviewTakeover, Is.False);
            Assert.That(manager.TakeOverPreviewValues(), Is.True);

            var plan = new SerializedObject(manager);
            Assert.That(Floats(plan, "fieldOfViewValues"), Is.EqualTo(new[] { 60f }));
            Assert.That(Floats(plan, "instrumentMagnificationMValues"), Is.EqualTo(new[] { 15f }));
            Assert.That(Floats(plan, "instrumentDistortionKValues"), Is.EqualTo(new[] { 1.2f, 1f, 0.5f }));
            Assert.That(manager.SessionMotionMode, Is.EqualTo(RandomDotMotionMode.HeadTracked));
            Assert.That(plan.FindProperty("simulatedSweepAxis").enumValueIndex,
                Is.EqualTo((int)RandomDotSweepAxis.Vertical));
            Assert.That(plan.FindProperty("simulatedImageCenterSpeed").floatValue, Is.EqualTo(20f));
            // Bei "Bildmitte" bleibt Sweep Speed (auch fürs Kopftraining) unverändert.
            Assert.That(plan.FindProperty("sweepSpeed").floatValue, Is.EqualTo(1.2f));
            Assert.That(manager.HasValuesBeforePreviewTakeover, Is.True);
        }

        [Test]
        public void Restore_BringsBackTheValuesBeforeTheFirstTakeover()
        {
            manager.TakeOverPreviewValues();

            // Zweites Übernehmen mit anderen Werten darf das Original nicht ersetzen.
            var preview = new SerializedObject(stimulus);
            preview.FindProperty("fieldOfViewDegrees").floatValue = 50f;
            preview.FindProperty("edgeSoftnessDegrees").floatValue = 3f;
            preview.ApplyModifiedPropertiesWithoutUndo();
            manager.TakeOverPreviewValues();
            Assert.That(Floats(new SerializedObject(manager), "fieldOfViewValues"), Is.EqualTo(new[] { 50f }));

            Assert.That(manager.RestoreValuesBeforePreviewTakeover(), Is.True);

            var plan = new SerializedObject(manager);
            Assert.That(Floats(plan, "fieldOfViewValues"), Is.EqualTo(new[] { 90f, 70f }));
            Assert.That(Floats(plan, "instrumentMagnificationMValues"), Is.EqualTo(new[] { 10f }));
            Assert.That(manager.SessionMotionMode, Is.EqualTo(RandomDotMotionMode.SimulatedYaw));
            Assert.That(plan.FindProperty("simulatedImageCenterSpeed").floatValue, Is.EqualTo(12f));
            Assert.That(new SerializedObject(stimulus).FindProperty("edgeSoftnessDegrees").floatValue,
                Is.EqualTo(2f));
            Assert.That(manager.HasValuesBeforePreviewTakeover, Is.False);
            Assert.That(manager.RestoreValuesBeforePreviewTakeover(), Is.False);
        }

        [Test]
        public void TakeOver_WithObjectAngleCopiesSweepSpeed()
        {
            var preview = new SerializedObject(stimulus);
            preview.FindProperty("previewSpeedReference").enumValueIndex = (int)RandomDotSweepSpeedReference.ObjectAngle;
            preview.ApplyModifiedPropertiesWithoutUndo();

            manager.TakeOverPreviewValues();

            var plan = new SerializedObject(manager);
            Assert.That(plan.FindProperty("simulatedSpeedReference").enumValueIndex,
                Is.EqualTo((int)RandomDotSweepSpeedReference.ObjectAngle));
            Assert.That(plan.FindProperty("sweepSpeed").floatValue, Is.EqualTo(5f));
            Assert.That(plan.FindProperty("simulatedImageCenterSpeed").floatValue, Is.EqualTo(12f));
        }

        private static void SetFloats(SerializedObject owner, string name, params float[] values)
        {
            SerializedProperty list = owner.FindProperty(name);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).floatValue = values[i];
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
