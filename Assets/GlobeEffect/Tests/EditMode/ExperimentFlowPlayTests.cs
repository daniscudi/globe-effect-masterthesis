using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Management;
using Object = UnityEngine.Object;
using Response = GlobeEffect.VRCheckerboard.CheckerboardCurvatureResponse;
using Sequence = GlobeEffect.VRCheckerboard.Experiment.CheckerboardTrialSequence;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Lässt kurze Sitzungen in den beiden echten Szenen im Play Mode laufen und
    /// prüft den Ablauf: welche Darstellung in welcher Phase steht, dass eine
    /// Antwort nur einmal zählt und nicht in den nächsten Durchgang rutscht, dass
    /// die Anzeige im Headset die eingestellte Tastenbelegung nennt, und dass die
    /// Punkte bei jedem m gleich schnell durch die Bildmitte laufen.
    ///
    /// Ein Headset ist dafür nicht nötig. Blickkontrolle und Training sind für den
    /// Test ausgeschaltet, die Messdateien landen im Temp-Ordner. Die Szenen
    /// werden dabei nur im Speicher verändert und nie gespeichert. Ist die gerade
    /// geöffnete Szene ungespeichert, werden die Tests übersprungen.
    /// </summary>
    public sealed class ExperimentFlowPlayTests
    {
        private const string CheckerboardScene = "Assets/GlobeEffect/Demo/CheckerboardDemo.unity";
        private const string RandomDotScene = "Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity";
        private const float PreStimulusSeconds = 0.3f;
        private const float SimulatedSweepSeconds = 0.2f;
        private const float ImageCenterSpeed = 12f;

        // Merkt sich über den Neustart der Skripte beim Wechsel in den Play Mode
        // hinweg, dass der Test die geöffnete Szene verändert hat.
        private const string SceneModifiedKey = "GlobeEffect.Tests.SceneModifiedByPlayTest";
        private const string XrStartupKey = "GlobeEffect.Tests.XrStartupBeforePlayTest";

        private static string OutputRoot => Path.Combine(Path.GetTempPath(), "GlobeEffectPlayTests");

        [UnityTest]
        public IEnumerator CheckerboardSequenceA_ShowsNoiseForTheResponseAndGrayBeforeThePattern()
        {
            PrepareCheckerboardScene(Sequence.NoiseResponseThenGray);
            yield return new EnterPlayMode();
            yield return RunCheckerboardSession(Sequence.NoiseResponseThenGray);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CheckerboardSequenceB_ShowsGrayForTheResponseAndNoiseBeforeThePattern()
        {
            PrepareCheckerboardScene(Sequence.GrayResponseThenNoise);
            yield return new EnterPlayMode();
            yield return RunCheckerboardSession(Sequence.GrayResponseThenNoise);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDots_KeepTheImageCenterSpeedAtMagnificationOneFiveAndTwenty()
        {
            PrepareRandomDotScene();
            yield return new EnterPlayMode();
            yield return RunRandomDotSession();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Checkerboard_WithRequiredFixation_AbortsIfTheRecordingStops()
        {
            PrepareCheckerboardScene(Sequence.NoiseResponseThenGray);
            var settings = new SerializedObject(Object.FindAnyObjectByType<CheckerboardExperimentManager>());
            settings.FindProperty("requireFixation").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<CheckerboardExperimentManager>();
            Assert.That(manager.StartSession(), Is.True);
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.WaitingForExperimentReady));
            LogAssert.Expect(LogType.Error,
                "Blickaufzeichnung wurde während der Messung beendet. Sitzung abgebrochen.");
            Object.FindAnyObjectByType<EyeTrackingToolbox>().StopRecording();
            yield return null;
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.Aborted));
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(0));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CheckerboardPreview_IsLiveAndDoesNotCreateASession()
        {
            PrepareCheckerboardScene(Sequence.NoiseResponseThenGray);
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<CheckerboardExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<VrCheckerboardStimulus>();
            string originalSettings = JsonUtility.ToJson(stimulus);
            manager.TogglePreview();
            Assert.That(manager.IsPreviewActive, Is.True);
            Assert.That(manager.IsSessionActive, Is.False);
            Assert.That(stimulus.CaptureSnapshot().checkerboardVisible, Is.True);
            stimulus.SetVisualSpaceL(0.3f);
            var previewSettings = new SerializedObject(stimulus);
            previewSettings.FindProperty("gridSpacingDegrees").floatValue = 3f;
            previewSettings.FindProperty("darkColor").colorValue = Color.blue;
            previewSettings.FindProperty("showFixationTarget").boolValue = false;
            previewSettings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(stimulus.CaptureSnapshot().visualSpaceL, Is.EqualTo(0.3f));
            Object.FindAnyObjectByType<CheckerboardKeyboardController>().SubmitResponse(Response.Convex);
            AssertNoSessionFiles(manager);
            manager.StopPreview();
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.Welcome));
            Assert.That(manager.IsPreviewActive, Is.False);
            Assert.That(JsonUtility.ToJson(stimulus), Is.EqualTo(originalSettings),
                "Alle Vorschauwerte, auch gemeinsame Musterwerte, müssen wiederhergestellt werden.");
            // Auch F5 direkt aus der Vorschau muss erst zurücksetzen, bevor
            // Einstellungen geprüft und die Sitzungsdatei geschrieben werden.
            manager.TogglePreview();
            stimulus.SetAngularDiameter(110f);
            stimulus.SetVisualSpaceL(0.2f);
            previewSettings.Update();
            previewSettings.FindProperty("gridSpacingDegrees").floatValue = 4f;
            previewSettings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(manager.StartSession(), Is.True);
            Assert.That(JsonUtility.ToJson(stimulus), Is.EqualTo(originalSettings));
            manager.TogglePreview();
            Assert.That(manager.IsPreviewActive, Is.False, "Keine Vorschau während einer Messung.");
            manager.AbortSession();
            manager.TogglePreview();
            stimulus.SetVisualSpaceL(0.3f);
            Assert.That(manager.StartTraining(), Is.True);
            Assert.That(JsonUtility.ToJson(stimulus), Is.EqualTo(originalSettings));
            manager.StopTrainingAndReturnToWelcome();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotPreview_ChangesOpticsAndAllowsPitchWithoutStartingASession()
        {
            PrepareRandomDotScene();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            string originalSettings = JsonUtility.ToJson(stimulus);
            manager.TogglePreview();
            Assert.That(manager.IsPreviewActive, Is.True);
            Assert.That(manager.IsSessionActive, Is.False);
            var settings = new SerializedObject(stimulus);
            settings.FindProperty("motionMode").intValue = (int)RandomDotMotionMode.HeadTracked;
            settings.FindProperty("instrumentDistortionK").floatValue = 0.3f;
            settings.FindProperty("showReferenceGrid").boolValue = true;
            settings.FindProperty("dotDensity").floatValue = 0.1f;
            settings.FindProperty("dotSizeDegrees").floatValue = 0.5f;
            settings.FindProperty("darkColor").colorValue = Color.blue;
            settings.FindProperty("showFixationTarget").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return null;
            Quaternion fieldOrientation = stimulus.transform.rotation;
            stimulus.Observer.rotation = Quaternion.Euler(8f, 6f, 0f) * stimulus.Observer.rotation;
            yield return null;
            Assert.That(Quaternion.Angle(fieldOrientation, stimulus.transform.rotation), Is.LessThan(0.01f));
            Assert.That(stimulus.InstrumentDistortionK, Is.EqualTo(0.3f));
            var block = new MaterialPropertyBlock();
            stimulus.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Assert.That(block.GetFloat("_ReferenceGridEnabled"), Is.EqualTo(1f));
            AssertNoSessionFiles(manager);
            manager.StopPreview();
            Assert.That(manager.IsPreviewActive, Is.False);
            Assert.That(JsonUtility.ToJson(stimulus), Is.EqualTo(originalSettings));
            manager.TogglePreview();
            stimulus.SetInstrumentDistortionK(0.2f);
            stimulus.SetInstrumentMagnification(12f);
            settings.Update();
            settings.FindProperty("dotDensity").floatValue = 0.05f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(manager.StartSession(), Is.True);
            Assert.That(JsonUtility.ToJson(stimulus), Is.EqualTo(originalSettings));
            manager.AbortSession();
            manager.TogglePreview();
            stimulus.SetInstrumentMagnification(12f);
            Assert.That(manager.StartTraining(), Is.True);
            Assert.That(JsonUtility.ToJson(stimulus), Is.EqualTo(originalSettings));
            manager.StopTrainingAndReturnToWelcome();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotStandaloneTraining_HasSeparateModesAndDoesNotSaveTrials()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("simulatedTrainingRepeatsPerValue").intValue = 1;
            SetFloats(config.FindProperty("simulatedTrainingKValues"), 0.2f, 1.2f);
            config.FindProperty("simulatedTrainingFeedback").boolValue = true;
            config.FindProperty("feedbackSeconds").floatValue = 0.2f;
            config.FindProperty("showSimulatedExamples").boolValue = true;
            config.FindProperty("exampleLongSeconds").floatValue = 0.5f;
            config.FindProperty("exampleTextSeconds").floatValue = 0.2f;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var controller = Object.FindAnyObjectByType<RandomDotKeyboardController>();
            Assert.That(manager.StartTraining(), Is.True);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.SimulatedTrainingInstructions));
            Assert.That(manager.IsSessionActive, Is.False);
            manager.ConfirmTrainingInstructions();
            // Immer "konvex": bei k = 0,2 falsch, bei k = 1,2 richtig, Reihenfolge gemischt.
            var feedback = new List<bool>();
            for (int trial = 0; trial < 2; trial++)
            {
                yield return WaitForState(manager, RandomDotSessionState.SimulatedTrainingResponse);
                Assert.That(manager.TrainingExamplesShown, Is.EqualTo(2),
                    "Vor der Übung je ein Beispiel für konvex und konkav.");
                controller.SubmitResponse(Response.Convex);
                controller.SubmitResponse(Response.Concave);
                yield return null;
                yield return null;
                Assert.That(manager.LastTrainingAnswerCorrect.HasValue, Is.True,
                    "Bei eindeutigen Extremwerten kommt eine Rückmeldung.");
                feedback.Add(manager.LastTrainingAnswerCorrect.Value);
            }
            Assert.That(feedback, Is.EquivalentTo(new[] { true, false }));
            yield return WaitForState(manager, RandomDotSessionState.Idle);
            Assert.That(manager.ValidTrialsCompleted, Is.Zero);
            AssertNoSessionFiles(manager);
            config = new SerializedObject(manager);
            config.FindProperty("sessionMotionMode").intValue = (int)RandomDotMotionMode.HeadTracked;
            config.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(manager.StartTraining(), Is.True);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.HeadTrainingInstructions));
            manager.StopTrainingAndReturnToWelcome();
            Assert.That(manager.IsTrainingActive, Is.False);
            AssertNoSessionFiles(manager);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotFreeHeadTrial_AcceptsPitchAndYawWithoutASineOrSpeedRequirement()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("sessionMotionMode").intValue =
                (int)RandomDotMotionMode.HeadTracked;
            SetFloats(config.FindProperty("instrumentMagnificationMValues"), 5f);
            config.FindProperty("headTrackedSeconds").floatValue = 0.2f;
            config.FindProperty("freeHeadMovement").boolValue = true;
            config.FindProperty("openEndedActiveTrials").boolValue = false;
            config.FindProperty("checkHeadMotion").boolValue = true;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            Assert.That(manager.StartSession(), Is.True, "Freie Trials brauchen keine Sinus-Mindestdauer.");
            manager.ConfirmResponseInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ActiveMotionInstructions));
            manager.ConfirmActiveMotionInstructions();
            Quaternion startRotation = stimulus.Observer.rotation;
            for (int frame = 0; frame < 4; frame++)
            {
                stimulus.Observer.rotation = startRotation * Quaternion.Euler(frame * 2f, frame * 2f, 0f);
                yield return null;
            }
            yield return WaitForState(manager, RandomDotSessionState.WaitingForResponse);
            Object.FindAnyObjectByType<RandomDotKeyboardController>().SubmitResponse(Response.Convex);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed));
            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0]["valid_for_analysis"], Is.EqualTo("1"));
            Assert.That(rows[0]["head_motion_constraint"], Is.EqualTo("free"));
            Assert.That(float.IsNaN(ParseFloat(rows[0]["profile_rmse_deg"])), Is.True);
            Assert.That(float.IsNaN(ParseFloat(rows[0]["image_center_speed_deg_per_s"])), Is.True);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotOpenEndedActiveTrial_StaysVisibleUntilTheAnswer()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("sessionMotionMode").intValue = (int)RandomDotMotionMode.HeadTracked;
            SetFloats(config.FindProperty("instrumentMagnificationMValues"), 5f);
            config.FindProperty("repeatsPerCondition").intValue = 2;
            config.FindProperty("headTrackedSeconds").floatValue = 0.1f;
            config.FindProperty("freeHeadMovement").boolValue = true;
            config.FindProperty("openEndedActiveTrials").boolValue = true;
            config.FindProperty("trainHeadMovement").boolValue = false;
            config.FindProperty("pauseSeconds").floatValue = 0.2f;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var controller = Object.FindAnyObjectByType<RandomDotKeyboardController>();
            Assert.That(manager.StartSession(), Is.True);
            manager.ConfirmResponseInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ActiveMotionInstructions),
                "Ohne Kopftraining direkt zur Anleitung für die aktiven Trials.");
            manager.ConfirmActiveMotionInstructions();

            // Deutlich länger als Head Tracked Seconds: kein Zeitlimit.
            double waitUntil = Time.realtimeSinceStartupAsDouble + 0.4d;
            while (Time.realtimeSinceStartupAsDouble < waitUntil) yield return null;
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.PresentingMotion));

            controller.SubmitResponse(Response.Convex);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.InterTrial),
                "Nach der Antwort kommt das graue Feld, dann der nächste Trial.");
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(1));
            yield return WaitForState(manager, RandomDotSessionState.PresentingMotion);
            controller.SubmitResponse(Response.Concave);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed));

            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0]["valid_for_analysis"], Is.EqualTo("1"));
            Assert.That(rows[0]["response"], Is.EqualTo("Convex"));
            Assert.That(ParseFloat(rows[0]["stimulus_duration_s"]), Is.GreaterThanOrEqualTo(0.35f));
            Assert.That(ParseFloat(rows[0]["response_time_s"]), Is.EqualTo(0f).Within(0.001f));
            Assert.That(rows[1]["response"], Is.EqualTo("Concave"));
            // Bildzeiten der Darbietung stehen pro Trial in der CSV.
            Assert.That(int.Parse(rows[0]["frames_presented"]), Is.GreaterThan(1));
            Assert.That(int.Parse(rows[0]["slow_frames"]), Is.GreaterThanOrEqualTo(0));
            Assert.That(ParseFloat(rows[0]["max_frame_ms"]), Is.GreaterThan(0f));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotSession_SelectsTheSimulatedTrainingForTheSimulatedBlock()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("trainSimulatedMotion").boolValue = true;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            Assert.That(manager.StartSession(), Is.True);
            manager.ConfirmResponseInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.SimulatedTrainingInstructions));
            Assert.That(manager.ValidTrialsCompleted, Is.Zero);
            manager.AbortSession();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotGuidedHeadTrial_KeepsTheOldDurationGuardAndHeadTraining()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("sessionMotionMode").intValue =
                (int)RandomDotMotionMode.HeadTracked;
            config.FindProperty("headTrackedSeconds").floatValue = 0.2f;
            config.FindProperty("freeHeadMovement").boolValue = false;
            config.FindProperty("checkHeadMotion").boolValue = true;
            config.FindProperty("trainHeadMovement").boolValue = true;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            LogAssert.Expect(LogType.Error, "Die HeadTracked-Dauer muss lang genug sein, damit der Sinus " +
                "beide Umkehrpunkte erreicht (mindestens 3 * Amplitude / mittlere Geschwindigkeit).");
            Assert.That(manager.StartSession(), Is.False);
            AssertNoSessionFiles(manager);
            config = new SerializedObject(manager);
            config.FindProperty("headTrackedSeconds").floatValue = 5f;
            config.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(manager.StartSession(), Is.True);
            manager.ConfirmResponseInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.HeadTrainingInstructions));
            manager.AbortSession();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDots_FinishOneMotionSession_ThenStartTheOtherSeparately()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            SetFloats(config.FindProperty("instrumentMagnificationMValues"), 5f);
            config.FindProperty("repeatsPerCondition").intValue = 6;
            config.FindProperty("sessionMotionMode").intValue = (int)RandomDotMotionMode.SimulatedYaw;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var controller = Object.FindAnyObjectByType<RandomDotKeyboardController>();
            Assert.That(manager.StartSession(), Is.True);
            Assert.That(manager.TotalTrials, Is.EqualTo(6));
            string simulatedFolder = new SerializedObject(manager).FindProperty("activeSessionFolder").stringValue;
            Assert.That(manager.StartOtherMotionSession(), Is.False, "Keine Umschaltung während der Sitzung.");
            manager.ConfirmResponseInstructions();
            for (int trial = 0; trial < 6; trial++)
            {
                Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.PresentingMotion),
                    "Innerhalb eines Bewegungsblocks darf kein zusätzliches F5 nötig sein.");
                yield return WaitForState(manager, RandomDotSessionState.WaitingForResponse);
                controller.SubmitResponse(Response.Convex);
            }
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed));
            Assert.That(manager.IsSessionActive, Is.False);
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(6));
            foreach (Dictionary<string, string> row in ReadTrialRows(manager))
            {
                Assert.That(row["mini_block_index"], Is.EqualTo("1"));
                Assert.That(row["motion_mode"], Is.EqualTo("SimulatedYaw"));
            }
            Assert.That(manager.StartOtherMotionSession(), Is.True);
            Assert.That(manager.TotalTrials, Is.EqualTo(6));
            Assert.That(new SerializedObject(manager).FindProperty("activeSessionFolder").stringValue,
                Is.Not.EqualTo(simulatedFolder));
            Assert.That(manager.ValidTrialsCompleted, Is.Zero);
            manager.ConfirmResponseInstructions();
            manager.ConfirmActiveMotionInstructions();
            // Aktive Trials sind standardmäßig offen: Antwort während die Punkte sichtbar sind.
            yield return WaitForState(manager, RandomDotSessionState.PresentingMotion);
            controller.SubmitResponse(Response.Convex);
            Assert.That(ReadTrialRows(manager)[0]["motion_mode"], Is.EqualTo("HeadTracked"));
            manager.AbortSession();
            yield return new ExitPlayMode();
        }

        private static void AssertNoSessionFiles(Object manager)
        {
            Assert.That(new SerializedObject(manager).FindProperty("activeSessionFolder").stringValue,
                Is.Empty);
            Assert.That(!Directory.Exists(OutputRoot) || Directory.GetFiles(OutputRoot, "*",
                SearchOption.AllDirectories).Length == 0, Is.True);
            Assert.That(Object.FindAnyObjectByType<EyeTrackingToolbox>().IsRecording, Is.False);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAndRestoreTheScene()
        {
            // Auch nach einem fehlgeschlagenen Test soll der Editor wieder im
            // Edit Mode stehen und die unveränderte Szene zeigen.
            if (EditorApplication.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            if (SessionState.GetBool(SceneModifiedKey, false))
            {
                XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget
                    .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (settings != null) settings.InitManagerOnStart = SessionState.GetBool(XrStartupKey, true);
                SessionState.EraseBool(XrStartupKey);
                SessionState.EraseBool(SceneModifiedKey);
                EditorSceneManager.OpenScene(SceneManager.GetActiveScene().path);
            }

            if (Directory.Exists(OutputRoot))
            {
                Directory.Delete(OutputRoot, recursive: true);
            }
        }

        private static IEnumerator RunCheckerboardSession(Sequence sequence)
        {
            yield return null;
            var manager = Object.FindAnyObjectByType<CheckerboardExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<VrCheckerboardStimulus>();
            var controller = Object.FindAnyObjectByType<CheckerboardKeyboardController>();
            bool noiseBeforePattern = sequence.ShowsNoiseBeforeStimulus();
            bool noiseForResponse = sequence.ShowsNoiseDuringResponse();

            // Der Startbildschirm und der Hinweis vor dem Versuch nennen die
            // Tastenbelegung, die für diesen Test im Inspector eingestellt wurde.
            string responseLines = ExpectedResponseLines(MappingFor(sequence), "A / OUTWARD", "B / INWARD");
            TextMesh prompt = stimulus.GetComponentInChildren<TextMesh>(includeInactive: true);
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.Welcome));
            Assert.That(prompt.text, Does.Contain(responseLines));

            Assert.That(manager.TrialSequence, Is.EqualTo(sequence));
            Assert.That(manager.StartSession(), Is.True, "Die Sitzung muss starten.");
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.WaitingForExperimentReady));
            AssertSettingsSaved(manager);
            Assert.That(prompt.text, Does.Contain("MAIN EXPERIMENT"));
            Assert.That(prompt.text, Does.Contain(responseLines));
            controller.SubmitResponse(Response.Convex);
            manager.ConfirmExperimentReady();

            // Erster Durchgang: Phase vor dem Schachbrett.
            AssertCheckerboardDisplay(manager, stimulus, CheckerboardSessionState.PreStimulus, noiseBeforePattern);
            double phaseStart = Time.realtimeSinceStartupAsDouble;
            controller.SubmitResponse(Response.Convex);
            if (noiseBeforePattern)
            {
                yield return AssertNoiseFlickers(stimulus);
            }

            yield return WaitForState(manager, CheckerboardSessionState.RunningTrial);
            Assert.That(Time.realtimeSinceStartupAsDouble - phaseStart,
                Is.GreaterThanOrEqualTo(PreStimulusSeconds - 0.01d),
                "Die Phase vor dem Schachbrett muss mindestens so lange dauern wie eingestellt.");
            Assert.That(stimulus.CaptureSnapshot().checkerboardVisible, Is.True);

            // Eine Antwort während des Schachbretts zählt nicht.
            controller.SubmitResponse(Response.Convex);
            yield return WaitForState(manager, CheckerboardSessionState.WaitingForResponse);
            AssertCheckerboardDisplay(
                manager, stimulus, CheckerboardSessionState.WaitingForResponse, noiseForResponse);
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(0),
                "Antworten vor der Antwortphase dürfen nicht zählen.");
            if (noiseForResponse)
            {
                yield return AssertNoiseFlickers(stimulus);
            }

            // Zwei Antworten im selben Frame: Nur die erste zählt, und danach
            // beginnt sofort die Phase vor dem nächsten Schachbrett.
            controller.SubmitResponse(Response.Convex);
            controller.SubmitResponse(Response.Concave);
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(1));
            AssertCheckerboardDisplay(manager, stimulus, CheckerboardSessionState.PreStimulus, noiseBeforePattern);

            // Weitere Tastendrücke bis zur nächsten Antwortphase beantworten den
            // zweiten Durchgang nicht.
            yield return null;
            controller.SubmitResponse(Response.Convex);
            yield return WaitForState(manager, CheckerboardSessionState.RunningTrial);
            controller.SubmitResponse(Response.Convex);
            yield return WaitForState(manager, CheckerboardSessionState.WaitingForResponse);
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(1));

            controller.SubmitResponse(Response.Concave);
            Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(2));
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.Completed));

            // In der Messdatei stehen genau die beiden gezählten Antworten.
            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0]["response"], Is.EqualTo("Convex"));
            Assert.That(rows[1]["response"], Is.EqualTo("Concave"));
            foreach (Dictionary<string, string> row in rows)
            {
                Assert.That(row["valid_for_analysis"], Is.EqualTo("1"));
                Assert.That(row["trial_sequence"], Is.EqualTo(sequence.ToString()));
            }
        }

        private static IEnumerator RunRandomDotSession()
        {
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            var controller = Object.FindAnyObjectByType<RandomDotKeyboardController>();

            Assert.That(manager.StartSession(), Is.True, "Die Sitzung muss starten.");
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ResponseInstructions),
                "Vor dem ersten Durchgang steht die Tastenbelegung im Headset.");
            AssertSettingsSaved(manager);
            string shownText = string.Empty;
            foreach (TextMesh text in stimulus.Observer.GetComponentsInChildren<TextMesh>())
            {
                shownText += text.text + "\n";
            }

            Assert.That(shownText, Does.Contain(ExpectedResponseLines(
                VrControllerMapping.TrackpadConvexTriggerConcave,
                "CONVEX (curves outward)", "CONCAVE (curves inward)")));
            controller.SubmitResponse(Response.Convex);
            manager.ConfirmResponseInstructions();

            var seenMagnifications = new List<float>();
            for (int trial = 0; trial < 3; trial++)
            {
                Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.PresentingMotion));
                float m = stimulus.InstrumentMagnificationM;
                seenMagnifications.Add(m);

                // Gleiche sichtbare Geschwindigkeit in der Bildmitte heißt: Das
                // Instrument schwenkt bei großem m entsprechend langsamer.
                float expectedObjectSpeed = ImageCenterSpeed / m;
                Assert.That(stimulus.SweepSpeedDegreesPerSecond, Is.EqualTo(expectedObjectSpeed).Within(1e-4f));
                Assert.That(stimulus.SweepAmplitudeDegrees,
                    Is.EqualTo(0.5f * expectedObjectSpeed * SimulatedSweepSeconds).Within(1e-4f));
                Assert.That(stimulus.DotCount, Is.EqualTo(RandomDotFieldStimulus.DotCountFor(
                    stimulus.DotDensity, m, 1f, stimulus.WorldCoverageDiameterDegrees)));

                // Während der Bewegung zählt eine Antwort nicht.
                controller.SubmitResponse(Response.Convex);
                Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(trial));
                yield return WaitForState(manager, RandomDotSessionState.WaitingForResponse);

                // Zwei Antworten im selben Frame: Nur die erste zählt.
                controller.SubmitResponse(Response.Convex);
                controller.SubmitResponse(Response.Concave);
                Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(trial + 1));
            }

            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed));
            Assert.That(seenMagnifications, Is.EquivalentTo(new[] { 1f, 5f, 20f }));

            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows.Count, Is.EqualTo(3));
            foreach (Dictionary<string, string> row in rows)
            {
                float m = ParseFloat(row["instrument_magnification_m"]);
                Assert.That(row["response"], Is.EqualTo("Convex"));
                Assert.That(ParseFloat(row["image_center_speed_deg_per_s"]),
                    Is.EqualTo(ImageCenterSpeed).Within(1e-3f));
                Assert.That(ParseFloat(row["sweep_speed_deg_per_s"]),
                    Is.EqualTo(ImageCenterSpeed / m).Within(1e-4f));
            }
        }

        // Ablauf A läuft mit der ersten Controller-Zuordnung, Ablauf B mit der
        // zweiten. So sind beide Zuordnungen in der echten Szene geprüft.
        private static VrControllerMapping MappingFor(Sequence sequence)
        {
            return sequence == Sequence.NoiseResponseThenGray
                ? VrControllerMapping.TrackpadConcaveTriggerConvex
                : VrControllerMapping.TrackpadConvexTriggerConcave;
        }

        private static string ExpectedResponseLines(
            VrControllerMapping mapping, string convexLabel, string concaveLabel)
        {
            string convexButton = VrControllerResponses.ButtonName(
                VrControllerResponses.ButtonFor(Response.Convex, mapping));
            string concaveButton = VrControllerResponses.ButtonName(
                VrControllerResponses.ButtonFor(Response.Concave, mapping));
            return convexButton + " = " + convexLabel + "\n" + concaveButton + " = " + concaveLabel;
        }

        private static void AssertCheckerboardDisplay(
            CheckerboardExperimentManager manager, VrCheckerboardStimulus stimulus,
            CheckerboardSessionState expectedState, bool expectNoise)
        {
            CheckerboardStimulusSnapshot shown = stimulus.CaptureSnapshot();
            Assert.That(manager.SessionState, Is.EqualTo(expectedState));
            Assert.That(shown.visible, Is.True);
            Assert.That(shown.checkerboardVisible, Is.False);
            Assert.That(shown.noiseVisible, Is.EqualTo(expectNoise),
                expectNoise ? "Hier muss die Noise-Maske stehen." : "Hier muss die graue Fläche stehen.");
        }

        // Die Maske muss sich zeitlich ändern: Nach einem Zehntel einer Sekunde
        // steht bei 30 Hz längst ein anderes Rauschbild im Shader.
        private static IEnumerator AssertNoiseFlickers(VrCheckerboardStimulus stimulus)
        {
            var renderer = stimulus.GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            yield return null;
            renderer.GetPropertyBlock(block);
            int firstSeed = block.GetInteger("_NoiseSeed");

            double end = Time.realtimeSinceStartupAsDouble + 0.1d;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                yield return null;
            }

            yield return null;
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetInteger("_NoiseSeed"), Is.Not.EqualTo(firstSeed),
                "Die Noise-Maske muss flimmern.");
        }

        private static IEnumerator WaitForState(
            CheckerboardExperimentManager manager, CheckerboardSessionState state)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (manager.SessionState != state && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Assert.That(manager.SessionState, Is.EqualTo(state));
        }

        private static IEnumerator WaitForState(RandomDotExperimentManager manager, RandomDotSessionState state)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (manager.SessionState != state && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Assert.That(manager.SessionState, Is.EqualTo(state));
        }

        private static void PrepareCheckerboardScene(Sequence sequence)
        {
            OpenSceneForTest(CheckerboardScene);
            var manager = new SerializedObject(Object.FindAnyObjectByType<CheckerboardExperimentManager>());
            manager.FindProperty("trialSequence").intValue = (int)sequence;
            manager.FindProperty("controllerMapping").intValue = (int)MappingFor(sequence);
            manager.FindProperty("useVrControllerButtons").boolValue = true;
            manager.FindProperty("preStimulusSeconds").floatValue = PreStimulusSeconds;
            manager.FindProperty("patternSeconds").floatValue = 0.1f;
            manager.FindProperty("answerTimeoutSeconds").floatValue = 0f;
            manager.FindProperty("requireTrainingBeforeSession").boolValue = false;
            manager.FindProperty("repeatsPerCondition").intValue = 1;
            SetFloats(manager.FindProperty("visualSpaceLValues"), 0.5f, 1f);
            ApplySharedTestSettings(manager);
            // Der gespeicherte Vorschauwert darf statisches Rauschen wählen.
            // Dieser Test prüft ausdrücklich die flimmernde Variante mit 30 Hz.
            var stimulus = new SerializedObject(Object.FindAnyObjectByType<VrCheckerboardStimulus>());
            stimulus.FindProperty("noiseRefreshRateHz").floatValue = 30f;
            stimulus.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PrepareRandomDotScene()
        {
            OpenSceneForTest(RandomDotScene);
            var manager = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            manager.FindProperty("sessionMotionMode").intValue = (int)RandomDotMotionMode.SimulatedYaw;
            SetFloats(manager.FindProperty("instrumentMagnificationMValues"), 1f, 5f, 20f);
            SetFloats(manager.FindProperty("instrumentDistortionKValues"), 0.5f);
            manager.FindProperty("simulatedSpeedReference").intValue =
                (int)RandomDotSweepSpeedReference.ImageCenter;
            manager.FindProperty("simulatedImageCenterSpeed").floatValue = ImageCenterSpeed;
            manager.FindProperty("simulatedSweepSeconds").floatValue = SimulatedSweepSeconds;
            manager.FindProperty("pauseSeconds").floatValue = 0f;
            manager.FindProperty("trainSimulatedMotion").boolValue = false;
            manager.FindProperty("trainHeadMovement").boolValue = false;
            manager.FindProperty("repeatsPerCondition").intValue = 1;
            ApplySharedTestSettings(manager);

            var controller = new SerializedObject(Object.FindAnyObjectByType<RandomDotKeyboardController>());
            controller.FindProperty("useVrControllerButtons").boolValue = true;
            controller.FindProperty("vrControllerMapping").intValue =
                (int)VrControllerMapping.TrackpadConvexTriggerConcave;
            controller.ApplyModifiedPropertiesWithoutUndo();
        }

        [UnityTest]
        public IEnumerator TrajectoryDiagnostic_BuildsTheConfiguredMarkerGrid()
        {
            // 7 x 5 Punkte bei 10 Grad Abstand passen alle in die 80-Grad-Grenze.
            OpenSceneForTest("Assets/GlobeEffect/Demo/RandomDotTrajectoryDiagnostic.unity");
            var settings = new SerializedObject(Object.FindAnyObjectByType<RandomDotTrajectoryDiagnostic>());
            settings.FindProperty("markerColumns").intValue = 7;
            settings.FindProperty("markerRows").intValue = 5;
            settings.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();

            yield return null;
            yield return null;
            var diagnostic = Object.FindAnyObjectByType<RandomDotTrajectoryDiagnostic>();
            MeshFilter live = null;
            int trailCount = 0;
            foreach (MeshFilter filter in diagnostic.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name == "Live markers") live = filter;
                if (filter.name.StartsWith("Trail sample")) trailCount++;
            }

            Assert.That(live, Is.Not.Null, "Die Live-Punkte fehlen.");
            Assert.That(live.sharedMesh.vertexCount, Is.EqualTo(7 * 5 * 4), "Vier Ecken pro Punkt.");
            Assert.That(trailCount, Is.EqualTo(diagnostic.trailSamples + 1));
            yield return new ExitPlayMode();
        }

        private static void OpenSceneForTest(string scenePath)
        {
            if (SceneManager.GetActiveScene().isDirty)
            {
                Assert.Ignore("Die geöffnete Szene hat ungespeicherte Änderungen. " +
                    "Der Test würde sie verwerfen und wird deshalb übersprungen.");
            }

            if (Directory.Exists(OutputRoot))
            {
                Directory.Delete(OutputRoot, recursive: true);
            }

            EditorSceneManager.OpenScene(scenePath);
            SessionState.SetBool(SceneModifiedKey, true);
            // Ablaufprüfung ohne Hardware: keinen XR-Loader starten. Nur das
            // geladene Objekt ändern, niemals das Asset speichern oder Loader
            // umsortieren. TearDown stellt den ursprünglichen Wert wieder her.
            XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget
                .XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings != null)
            {
                SessionState.SetBool(XrStartupKey, settings.InitManagerOnStart);
                settings.InitManagerOnStart = false;
            }
        }

        private static void ApplySharedTestSettings(SerializedObject manager)
        {
            // Ohne Headset gibt es keine Blickdaten. Die Messdateien gehören in den
            // Temp-Ordner und nicht unter measurements.
            manager.FindProperty("requireFixation").boolValue = false;
            manager.FindProperty("autoStartOnPlay").boolValue = false;
            manager.FindProperty("outputRoot").stringValue = OutputRoot;
            manager.ApplyModifiedPropertiesWithoutUndo();

            // Der Dummy wird hier ausdrücklich gewählt. So hängt der Test nicht
            // davon ab, welches Headset am Rechner gerade läuft.
            var toolbox = new SerializedObject(Object.FindAnyObjectByType<EyeTrackingToolbox>());
            toolbox.FindProperty("provider").intValue = (int)EyeTrackingToolbox.ETProvider.Dummy;
            toolbox.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloats(SerializedProperty list, params float[] values)
        {
            list.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                list.GetArrayElementAtIndex(index).floatValue = values[index];
            }
        }

        // Liest die Ergebnisdatei der gerade gelaufenen Sitzung: eine Zeile pro
        // Durchgang, die Werte unter ihrem Spaltennamen.
        private static List<Dictionary<string, string>> ReadTrialRows(Object manager)
        {
            string folder = new SerializedObject(manager).FindProperty("activeSessionFolder").stringValue;
            string[] files = Directory.GetFiles(folder, "*_trials.csv");
            Assert.That(files.Length, Is.EqualTo(1));

            string[] lines = File.ReadAllLines(files[0]);
            string[] header = lines[0].Split(',');
            var rows = new List<Dictionary<string, string>>();
            for (int line = 1; line < lines.Length; line++)
            {
                string[] values = lines[line].Split(',');
                Assert.That(values.Length, Is.EqualTo(header.Length), "Kopfzeile und Zeile passen nicht.");
                var row = new Dictionary<string, string>();
                for (int column = 0; column < header.Length; column++)
                {
                    row[header[column]] = values[column];
                }

                rows.Add(row);
            }

            return rows;
        }

        private static void AssertSettingsSaved(Object manager)
        {
            string folder = new SerializedObject(manager).FindProperty("activeSessionFolder").stringValue;
            string[] settings = Directory.GetFiles(folder, "*_settings.json");
            Assert.That(settings.Length, Is.EqualTo(1));
            string json = File.ReadAllText(settings[0]);
            Assert.That(json, Does.Contain("\"experimentManagerAtStart\""));
            Assert.That(json, Does.Contain("\"stimulusAtStart\""));
            Assert.That(json, Does.Contain("\"activeEyeTracker\": \"Dummy\""));
            Assert.That(json, Does.Not.Contain(": NaN"));
            Assert.That(json, Does.Not.Contain(": Infinity"));
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
