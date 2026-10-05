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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
        public IEnumerator RandomDots_KeepTheImageCenterSpeedAtMagnificationFiveAndTwenty()
        {
            PrepareRandomDotScene();
            yield return new EnterPlayMode();
            yield return RunRandomDotSession();
            yield return new ExitPlayMode();
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
            for (int trial = 0; trial < 2; trial++)
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
            Assert.That(seenMagnifications, Is.EquivalentTo(new[] { 5f, 20f }));

            List<Dictionary<string, string>> rows = ReadTrialRows(manager);
            Assert.That(rows.Count, Is.EqualTo(2));
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
        }

        private static void PrepareRandomDotScene()
        {
            OpenSceneForTest(RandomDotScene);
            var manager = new SerializedObject(Object.FindAnyObjectByType<RandomDotExperimentManager>());
            SerializedProperty motionModes = manager.FindProperty("motionModes");
            motionModes.arraySize = 1;
            motionModes.GetArrayElementAtIndex(0).intValue = (int)RandomDotMotionMode.SimulatedYaw;
            SetFloats(manager.FindProperty("instrumentMagnificationMValues"), 5f, 20f);
            SetFloats(manager.FindProperty("instrumentDistortionKValues"), 0.5f);
            manager.FindProperty("simulatedSpeedReference").intValue =
                (int)RandomDotSweepSpeedReference.ImageCenter;
            manager.FindProperty("simulatedImageCenterSpeed").floatValue = ImageCenterSpeed;
            manager.FindProperty("simulatedSweepSeconds").floatValue = SimulatedSweepSeconds;
            manager.FindProperty("pauseSeconds").floatValue = 0f;
            manager.FindProperty("repeatsPerCondition").intValue = 1;
            manager.FindProperty("repeatsPerBlock").intValue = 1;
            ApplySharedTestSettings(manager);

            var controller = new SerializedObject(Object.FindAnyObjectByType<RandomDotKeyboardController>());
            controller.FindProperty("useVrControllerButtons").boolValue = true;
            controller.FindProperty("vrControllerMapping").intValue =
                (int)VrControllerMapping.TrackpadConvexTriggerConcave;
            controller.ApplyModifiedPropertiesWithoutUndo();
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

        private static float ParseFloat(string value)
        {
            return float.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}
