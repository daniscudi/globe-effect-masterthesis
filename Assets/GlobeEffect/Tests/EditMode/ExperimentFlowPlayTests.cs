using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            // Aktive Vorschau: Punktwelt wie in der Sitzung aus Head Turn Safety Degrees.
            // Inklusive äußerer Hälfte des weichen Rands (FOV/2 + Softness/2).
            float expectedCoverage = RandomDotFieldStimulus.CoverageNeeded(stimulus.FieldOfViewDegrees,
                stimulus.ContentZoom, stimulus.InstrumentMagnificationM, stimulus.InstrumentDistortionK,
                manager.HeadTrackedReachDegrees, stimulus.ApertureEdgeSoftnessDegrees);
            Assert.That(stimulus.WorldCoverageDiameterDegrees, Is.EqualTo(expectedCoverage).Within(0.06f));
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
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ActivePracticeInstructions));
            manager.StopTrainingAndReturnToWelcome();
            Assert.That(manager.IsTrainingActive, Is.False);
            AssertNoSessionFiles(manager);
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
            // Aktive Trials: die Punkte kommen erst beim gleichmäßigen Schwenk.
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.WaitingForHeadMotion));
            yield return SweepHead(Object.FindAnyObjectByType<RandomDotFieldStimulus>(), 2.5f, manager,
                RandomDotSessionState.PresentingMotion);
            manager.AbortSession();
            Assert.That(ReadTrialRows(manager)[0]["motion_mode"], Is.EqualTo("HeadTracked"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDots_PauseAfterEachQuarterUntilTheExperimenterContinues()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            SetFloats(config.FindProperty("instrumentMagnificationMValues"), 5f);
            SetFloats(config.FindProperty("instrumentDistortionKValues"), 0.5f, 1f);
            SetInts(config.FindProperty("repeatsPerKValue"), 3, 1);
            config.FindProperty("blocksPerSession").intValue = 4;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var controller = Object.FindAnyObjectByType<RandomDotKeyboardController>();
            Assert.That(manager.StartSession(), Is.True);
            Assert.That(manager.TotalTrials, Is.EqualTo(4), "k = 0,5 dreimal, k = 1 einmal.");
            manager.ConfirmResponseInstructions();
            for (int trial = 1; trial <= 4; trial++)
            {
                yield return WaitForState(manager, RandomDotSessionState.WaitingForResponse);
                controller.SubmitResponse(Response.Convex);
                if (trial < 4)
                {
                    // Nach jedem Viertel (1, 2, 3 von 4 gültigen Trials) wartet die Sitzung.
                    Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.PausedBetweenMiniBlocks));
                    Assert.That(manager.PausesTaken, Is.EqualTo(trial));
                    controller.SubmitResponse(Response.Convex);
                    Assert.That(manager.ValidTrialsCompleted, Is.EqualTo(trial), "Antworten in der Pause zählen nicht.");
                    manager.ContinueAfterBlockPause();
                }
            }
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed), "Am Ende keine Pause.");
            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows.Count(row => row["instrument_distortion_k"] == "0.5"), Is.EqualTo(3));
            Assert.That(rows.Count(row => row["instrument_distortion_k"] == "1"), Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Checkerboard_PausesAtHalfTimeUntilTheExperimenterContinues()
        {
            PrepareCheckerboardScene(Sequence.NoiseResponseThenGray);
            var config = new SerializedObject(Object.FindAnyObjectByType<CheckerboardExperimentManager>());
            SetInts(config.FindProperty("repeatsPerLValue"), 1, 1);
            config.FindProperty("blocksPerSession").intValue = 2;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<CheckerboardExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<VrCheckerboardStimulus>();
            var controller = Object.FindAnyObjectByType<CheckerboardKeyboardController>();
            Assert.That(manager.StartSession(), Is.True);
            manager.ConfirmExperimentReady();
            yield return WaitForState(manager, CheckerboardSessionState.WaitingForResponse);
            controller.SubmitResponse(Response.Convex);
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.BlockPause));
            Assert.That(manager.IsSessionActive, Is.True);
            var block = new MaterialPropertyBlock();
            stimulus.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FixationEnabled"), Is.EqualTo(1f), "Das rote Kreuz bleibt in der Pause.");
            TextMesh prompt = stimulus.GetComponentInChildren<TextMesh>(includeInactive: true);
            Assert.That(prompt.text, Does.Contain("SHORT BREAK"));
            // Der Text steht auf derselben Tiefe wie das Kreuz: kurz vor der Fernebene.
            Transform head = Camera.main.transform;
            Assert.That(Vector3.Distance(prompt.transform.position, head.position),
                Is.GreaterThan(0.8f * Camera.main.farClipPlane));
            manager.ContinueAfterBlockPause();
            yield return WaitForState(manager, CheckerboardSessionState.WaitingForResponse);
            controller.SubmitResponse(Response.Concave);
            Assert.That(manager.SessionState, Is.EqualTo(CheckerboardSessionState.Completed));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotVelocityGate_ShowsDotsAsSoonAsTheHeadTurns()
        {
            // EnterPlayMode muss direkt in der Testmethode stehen, nicht verschachtelt.
            PrepareVelocityGatedScene();
            yield return new EnterPlayMode();
            yield return null;
            RandomDotExperimentManager manager = StartVelocityGatedSession();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            float target = manager.SimulatedHeadSpeedPreview;

            // Kopf still: Die Punkte dürfen nicht erscheinen.
            double waitUntil = Time.realtimeSinceStartupAsDouble + 0.6d;
            while (Time.realtimeSinceStartupAsDouble < waitUntil) yield return null;
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.WaitingForHeadMotion));
            Assert.That(stimulus.CrossVisible, Is.True);

            // Schwenk über dem Schwellwert: Punkte erscheinen, nach der Darbietung Antwort.
            // Keine Lambdas in diesen Tests: Sie überleben den Domain Reload beim
            // Wechsel in den Play Mode nicht (eingefangene Variablen werden null).
            yield return SweepHead(stimulus, target, manager, RandomDotSessionState.PresentingMotion);
            yield return SweepHead(stimulus, target, manager, RandomDotSessionState.WaitingForResponse);
            // Antwortphase: nur die graue Fläche, ohne Punkte und ohne Kreuz.
            Assert.That(stimulus.PointsVisible, Is.False);
            Assert.That(stimulus.CrossVisible, Is.False);
            Object.FindAnyObjectByType<RandomDotKeyboardController>().SubmitResponse(Response.Convex);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed));

            Dictionary<string, string> row = ReadTrialRows(manager)[0];
            Assert.That(row["status"], Is.EqualTo("valid"));
            Assert.That(ParseFloat(row["velocity_gate_target_deg_per_s"]), Is.EqualTo(target).Within(0.001f));
            Assert.That(ParseFloat(row["head_speed_mean_deg_per_s"]), Is.EqualTo(target).Within(0.3f * target));
            Assert.That(ParseFloat(row["head_motion_wait_s"]), Is.GreaterThan(0f));
            // Der Test dreht den Kopf sauber nach rechts: Richtung gespeichert, nichts daneben.
            Assert.That(row["head_direction"], Is.EqualTo("Right"));
            Assert.That(ParseFloat(row["head_offaxis_max_deviation_deg"]), Is.EqualTo(0f).Within(0.05f));
            Assert.That(ParseFloat(row["head_roll_max_deviation_deg"]), Is.EqualTo(0f).Within(0.05f));
            Assert.That(ParseFloat(row["stimulus_duration_s"]), Is.EqualTo(SimulatedSweepSeconds).Within(0.1f));
            Assert.That(row["head_motion_constraint"], Is.EqualTo("free"));
            // Die alte Regel (Bezugstempo, Mindeststrecke) gibt es nicht mehr.
            Assert.That(row.ContainsKey("head_speed_reference_deg_per_s"), Is.False);
            Assert.That(row.ContainsKey("head_turn_before_dots_deg"), Is.False);
            // Bildzeiten der Darbietung stehen pro Trial in der CSV.
            Assert.That(int.Parse(row["frames_presented"]), Is.GreaterThan(1));
            Assert.That(int.Parse(row["slow_frames"]), Is.GreaterThanOrEqualTo(0));
            Assert.That(ParseFloat(row["max_frame_ms"]), Is.GreaterThan(0f));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotVelocityGate_StoppingDuringTheDots_AnswerAnywayThenRepeated()
        {
            // EnterPlayMode muss direkt in der Testmethode stehen, nicht verschachtelt.
            PrepareVelocityGatedScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("simulatedSweepSeconds").floatValue = 0.8f;
            config.FindProperty("answerFlashOpacity").floatValue = 0.1f;
            config.FindProperty("answerFlashSeconds").floatValue = 0.2f;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            RandomDotExperimentManager manager = StartVelocityGatedSession();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();

            // Schwenken, bis die Punkte da sind, dann anhalten: Punkte und Kreuz weg.
            yield return SweepHead(stimulus, manager.SimulatedHeadSpeedPreview, manager,
                RandomDotSessionState.PresentingMotion);
            yield return WaitForState(manager, RandomDotSessionState.WaitingForResponse);
            Assert.That(stimulus.PointsVisible, Is.False);
            Assert.That(stimulus.CrossVisible, Is.False);
            Assert.That(manager.HeadStoppedAttempts, Is.EqualTo(1));

            // Trotzdem antworten: dann der Cyan-Flash auf der grauen Fläche, noch ohne Kreuz.
            Object.FindAnyObjectByType<RandomDotKeyboardController>().SubmitResponse(Response.Concave);
            Assert.That(stimulus.FieldFlash.a, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(stimulus.FieldFlash.r, Is.Zero, "cyan");
            Assert.That(stimulus.FieldFlash.g, Is.EqualTo(1f), "cyan");
            Assert.That(stimulus.CrossVisible, Is.False);

            // Nach dem Flash ist das Kreuz wieder da, und der Trial kommt noch einmal.
            yield return WaitForState(manager, RandomDotSessionState.WaitingForHeadMotion);
            Assert.That(stimulus.CrossVisible, Is.True);
            Dictionary<string, string> row = ReadTrialRows(manager)[0];
            Assert.That(row["status"], Is.EqualTo("invalid:head_stopped"));
            Assert.That(row["response"], Is.EqualTo("Concave"), "Die Antwort wird mitgespeichert.");
            Assert.That(row["valid_for_analysis"], Is.EqualTo("0"));
            Assert.That(ParseFloat(row["stimulus_duration_s"]), Is.LessThan(0.7f), "Früh abgebrochen.");
            Assert.That(manager.ValidTrialsCompleted, Is.Zero, "Der Trial wird später wiederholt.");
            manager.AbortSession();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotVelocityGate_AnySpeedStartsButTurningBackStopsTheTrial()
        {
            // EnterPlayMode muss direkt in der Testmethode stehen, nicht verschachtelt.
            PrepareVelocityGatedScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("simulatedSweepSeconds").floatValue = 0.8f;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            RandomDotExperimentManager manager = StartVelocityGatedSession();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();

            // Viermal so schnell wie der simulierte Schwenk: Es gibt kein Zieltempo.
            // Dann ruckartig zurück: Das Tempo in Schwenkrichtung fällt unter den
            // Schwellwert, also Abbruch, obwohl der Kopf schnell dreht.
            float speed = 4f * manager.SimulatedHeadSpeedPreview;
            yield return SweepHead(stimulus, speed, manager, RandomDotSessionState.PresentingMotion);
            yield return SweepHead(stimulus, -speed, manager, RandomDotSessionState.WaitingForResponse);
            Object.FindAnyObjectByType<RandomDotKeyboardController>().SubmitResponse(Response.Convex);

            Dictionary<string, string> row = ReadTrialRows(manager)[0];
            Assert.That(row["status"], Is.EqualTo("invalid:head_stopped"));
            Assert.That(row["head_direction"], Is.EqualTo("Right"));
            Assert.That(ParseFloat(row["stimulus_duration_s"]), Is.LessThan(0.7f));
            Assert.That(manager.ValidTrialsCompleted, Is.Zero);
            manager.AbortSession();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotActivePractice_StandaloneRunsBothPracticesAndReturnsToWelcome()
        {
            // EnterPlayMode muss direkt in der Testmethode stehen, nicht verschachtelt.
            PrepareActivePracticeScene(goodTrials: 1, maxTrials: 3, freeMovement: true);
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();

            // T im Head-Tracked-Modus: Active Practice.
            Assert.That(manager.StartTraining(), Is.True);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ActivePracticeInstructions));
            manager.ConfirmTrainingInstructions();

            // Practice 1: k = 1, Punkte nur solange der Kopf dreht.
            yield return WaitForState(manager, RandomDotSessionState.FreeMovementPractice);
            Assert.That(stimulus.InstrumentDistortionK, Is.EqualTo(1f));
            double waitUntil = Time.realtimeSinceStartupAsDouble + 0.3d;
            while (Time.realtimeSinceStartupAsDouble < waitUntil) yield return null;
            Assert.That(stimulus.PointsVisible, Is.False, "Kopf still: keine Punkte.");
            yield return SweepHeadUntilDots(stimulus, 2.5f);
            yield return SweepHeadFor(stimulus, 2.5f, 1f);
            Assert.That(stimulus.PointsVisible, Is.True, "Ohne Zeitgrenze, solange der Kopf dreht.");
            yield return WaitUntilDotsGone(stimulus);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.FreeMovementPractice),
                "Practice 1 endet erst mit der Taste des Versuchsleiters.");
            manager.ContinuePractice();

            // Practice 2: wie im Test. Antwortphase ohne Kreuz.
            yield return SweepHead(stimulus, 2.5f, manager, RandomDotSessionState.ActivePracticeResponse);
            Assert.That(stimulus.CrossVisible, Is.False);
            Object.FindAnyObjectByType<RandomDotKeyboardController>().SubmitResponse(Response.Convex);
            yield return null;
            yield return null;
            Assert.That(manager.LastActivePracticeResult, Is.EqualTo("good"));
            Assert.That(manager.ActivePracticeGoodTrials, Is.EqualTo(1));

            // Ein guter Trial reicht hier: zurück zum Startbildschirm, ohne Messdateien.
            yield return WaitForState(manager, RandomDotSessionState.Idle);
            AssertNoSessionFiles(manager);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotActivePractice_StoppingEarlyStillNeedsAnAnswerBeforeTheSessionTrials()
        {
            // EnterPlayMode muss direkt in der Testmethode stehen, nicht verschachtelt.
            PrepareActivePracticeScene(goodTrials: 1, maxTrials: 1, freeMovement: false);
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("simulatedSweepSeconds").floatValue = 0.8f;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();

            // In der Sitzung kommt die Active Practice nach der Tastenbelegung.
            Assert.That(manager.StartSession(), Is.True);
            manager.ConfirmResponseInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ActivePracticeInstructions));
            manager.ConfirmTrainingInstructions();

            // Schwenken, bis die Punkte da sind, dann anhalten: Abbruch, aber Antwort wie im Test.
            yield return SweepHeadUntilDots(stimulus, 2.5f);
            yield return WaitForState(manager, RandomDotSessionState.ActivePracticeResponse);
            Assert.That(stimulus.CrossVisible, Is.False);
            Object.FindAnyObjectByType<RandomDotKeyboardController>().SubmitResponse(Response.Convex);
            yield return null;
            Assert.That(manager.LastActivePracticeResult, Is.EqualTo("stopped"));
            Assert.That(manager.ActivePracticeGoodTrials, Is.Zero);

            // Höchstzahl erreicht: weiter zur Anleitung der echten aktiven Trials.
            yield return WaitForState(manager, RandomDotSessionState.ActiveMotionInstructions);
            manager.AbortSession();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RandomDotVelocityGate_AlternatesTheSweepDirectionBetweenTrials()
        {
            // EnterPlayMode muss direkt in der Testmethode stehen, nicht verschachtelt.
            PrepareVelocityGatedScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            SetFloats(config.FindProperty("instrumentDistortionKValues"), 0.5f, 1f);
            config.FindProperty("alternateSweepDirection").boolValue = true;
            config.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            yield return null;
            RandomDotExperimentManager manager = StartVelocityGatedSession();
            var stimulus = Object.FindAnyObjectByType<RandomDotFieldStimulus>();
            var controller = Object.FindAnyObjectByType<RandomDotKeyboardController>();
            float speed = 2.5f;

            // Erster Trial: Richtung egal, hier nach rechts.
            Assert.That(manager.NextSweepDirection, Is.EqualTo("any"));
            yield return SweepHead(stimulus, speed, manager, RandomDotSessionState.PresentingMotion);
            yield return SweepHead(stimulus, speed, manager, RandomDotSessionState.WaitingForResponse);
            controller.SubmitResponse(Response.Convex);
            yield return WaitForState(manager, RandomDotSessionState.WaitingForHeadMotion);
            Assert.That(manager.NextSweepDirection, Is.EqualTo("Left"));

            // Noch einmal nach rechts: keine Punkte.
            yield return SweepHeadFor(stimulus, speed, 1.5f);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.WaitingForHeadMotion));
            Assert.That(stimulus.PointsVisible, Is.False);

            // Zurück nach links: jetzt kommen die Punkte.
            yield return SweepHead(stimulus, -speed, manager, RandomDotSessionState.PresentingMotion);
            yield return SweepHead(stimulus, -speed, manager, RandomDotSessionState.WaitingForResponse);
            controller.SubmitResponse(Response.Concave);
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.Completed));

            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows[0]["head_direction"], Is.EqualTo("Right"));
            Assert.That(rows[1]["head_direction"], Is.EqualTo("Left"));
            yield return new ExitPlayMode();
        }

        private static void PrepareActivePracticeScene(int goodTrials, int maxTrials, bool freeMovement)
        {
            PrepareVelocityGatedScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("trainActivePanning").boolValue = true;
            config.FindProperty("showActiveSpeedExample").boolValue = false;
            config.FindProperty("freeMovementPractice").boolValue = freeMovement;
            config.FindProperty("activePracticeGoodTrials").intValue = goodTrials;
            config.FindProperty("activePracticeMaxTrials").intValue = maxTrials;
            config.FindProperty("exampleTextSeconds").floatValue = 0.2f;
            config.FindProperty("practiceTextsWaitForPress").boolValue = false;
            config.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator SweepHeadUntilDots(RandomDotFieldStimulus stimulus, float degreesPerSecond)
        {
            // Dreht die Kopfkamera gleichmäßig nach rechts, bis die Punkte erscheinen.
            TestHeadMover.Turn(stimulus.Observer, degreesPerSecond);
            double startTime = Time.realtimeSinceStartupAsDouble;
            while (!stimulus.PointsVisible && Time.realtimeSinceStartupAsDouble - startTime < 5d) yield return null;
            TestHeadMover.Turn(stimulus.Observer, 0f);
            Assert.That(stimulus.PointsVisible, Is.True, "Die Punkte sind nicht erschienen.");
        }

        private static IEnumerator WaitUntilDotsGone(RandomDotFieldStimulus stimulus)
        {
            // Kopf still halten, bis die Punkte ausgeblendet sind.
            double startTime = Time.realtimeSinceStartupAsDouble;
            while (stimulus.PointsVisible && Time.realtimeSinceStartupAsDouble - startTime < 2d) yield return null;
            Assert.That(stimulus.PointsVisible, Is.False, "Die Punkte sind nicht verschwunden.");
        }

        private static IEnumerator SweepHeadFor(RandomDotFieldStimulus stimulus, float degreesPerSecond, float seconds)
        {
            // Dreht die Kopfkamera eine feste Zeit lang gleichmäßig nach rechts.
            TestHeadMover.Turn(stimulus.Observer, degreesPerSecond);
            double startTime = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - startTime < seconds) yield return null;
            TestHeadMover.Turn(stimulus.Observer, 0f);
        }

        private static void PrepareVelocityGatedScene()
        {
            PrepareRandomDotScene();
            var config = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            config.FindProperty("sessionMotionMode").intValue = (int)RandomDotMotionMode.HeadTracked;
            SetFloats(config.FindProperty("instrumentMagnificationMValues"), 5f);
            config.FindProperty("minimumMovingHeadSpeed").floatValue = 1f;
            config.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RandomDotExperimentManager StartVelocityGatedSession()
        {
            var manager = Object.FindAnyObjectByType<RandomDotExperimentManager>();
            Assert.That(manager.StartSession(), Is.True);
            manager.ConfirmResponseInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.ActiveMotionInstructions));
            manager.ConfirmActiveMotionInstructions();
            Assert.That(manager.SessionState, Is.EqualTo(RandomDotSessionState.WaitingForHeadMotion));
            return manager;
        }

        private static IEnumerator SweepHead(RandomDotFieldStimulus stimulus, float degreesPerSecond,
            RandomDotExperimentManager manager, RandomDotSessionState until)
        {
            // Dreht die Kopfkamera gleichmäßig nach rechts, bis der Zustand erreicht ist.
            // Folgt direkt der nächste Schwenk, dreht der Kopf ohne Pause weiter.
            TestHeadMover.Turn(stimulus.Observer, degreesPerSecond);
            double deadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (manager.SessionState != until && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            TestHeadMover.Turn(stimulus.Observer, 0f);
            Assert.That(manager.SessionState, Is.EqualTo(until));
        }

        private static void SetInts(SerializedProperty list, params int[] values)
        {
            list.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                list.GetArrayElementAtIndex(index).intValue = values[index];
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
                // Texte stehen auf derselben Tiefe wie das Kreuz: kurz vor der Fernebene.
                Assert.That(Vector3.Distance(text.transform.position, stimulus.Observer.position),
                    Is.GreaterThan(0.8f * stimulus.Observer.GetComponent<Camera>().farClipPlane));
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
            manager.FindProperty("simulatedImageCenterSpeed").floatValue = ImageCenterSpeed;
            manager.FindProperty("simulatedSweepSeconds").floatValue = SimulatedSweepSeconds;
            manager.FindProperty("pauseSeconds").floatValue = 0f;
            manager.FindProperty("trainSimulatedMotion").boolValue = false;
            manager.FindProperty("trainActivePanning").boolValue = false;
            manager.FindProperty("practiceTextsWaitForPress").boolValue = false;
            // Ohne Flash geht es nach der Antwort sofort weiter; der Flash hat einen eigenen Test.
            manager.FindProperty("answerFlashOpacity").floatValue = 0f;
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
            // Die Ablauftests rechnen mit Repeats Per Condition und ohne Pausen.
            // Wiederholungen pro Wert und Viertelpausen haben eigene Tests.
            foreach (string perValue in new[] { "repeatsPerKValue", "repeatsPerLValue" })
            {
                SerializedProperty list = manager.FindProperty(perValue);
                if (list != null) list.arraySize = 0;
            }
            manager.FindProperty("blocksPerSession").intValue = 1;
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
