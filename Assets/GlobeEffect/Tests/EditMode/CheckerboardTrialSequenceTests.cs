using System;
using System.IO;
using GlobeEffect.VRCheckerboard.Experiment;
using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;
using Sequence = GlobeEffect.VRCheckerboard.Experiment.CheckerboardTrialSequence;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft die beiden Abläufe des Checkerboard-Tests und was davon in den
    /// Messdateien landet.
    ///
    /// A: Schachbrett, Noise-Maske mit Antwort, graue Fläche, nächstes Schachbrett.
    /// B: Schachbrett, graue Fläche mit Antwort, Noise-Maske, nächstes Schachbrett.
    /// </summary>
    public sealed class CheckerboardTrialSequenceTests
    {
        private string outputRoot;

        [SetUp]
        public void SetUp()
        {
            // Die Testdateien landen im Temp-Ordner und nie unter measurements.
            outputRoot = Path.Combine(Path.GetTempPath(), "GlobeEffectTests_" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }

        [Test]
        public void SequenceA_ShowsNoiseDuringResponseAndGrayBeforeTheNextPattern()
        {
            Assert.That(Sequence.NoiseResponseThenGray.ShowsNoiseDuringResponse(), Is.True);
            Assert.That(Sequence.NoiseResponseThenGray.ShowsNoiseBeforeStimulus(), Is.False);
        }

        [Test]
        public void SequenceB_ShowsGrayDuringResponseAndNoiseBeforeTheNextPattern()
        {
            Assert.That(Sequence.GrayResponseThenNoise.ShowsNoiseDuringResponse(), Is.False);
            Assert.That(Sequence.GrayResponseThenNoise.ShowsNoiseBeforeStimulus(), Is.True);
        }

        [TestCase(Sequence.NoiseResponseThenGray, 1.25d)]
        [TestCase(Sequence.GrayResponseThenNoise, 0d)]
        public void Result_ReportsNoiseDurationOnlyWhenNoiseIsTheResponsePhase(
            Sequence sequence, double expectedNoiseSeconds)
        {
            CheckerboardTrialResult result = CreateResult(sequence);

            Assert.That(result.ResponseTimeSeconds, Is.EqualTo(1.25d).Within(1e-9));
            Assert.That(result.NoiseMaskDurationSeconds, Is.EqualTo(expectedNoiseSeconds).Within(1e-9));
        }

        [TestCase(Sequence.NoiseResponseThenGray, "1", "0")]
        [TestCase(Sequence.GrayResponseThenNoise, "0", "0.400000006")]
        public void Files_StoreTheSequenceAndKeepHeaderAndRowsAligned(
            Sequence sequence, string noiseUntilResponse, string postResponseNoiseSeconds)
        {
            var files = new CheckerboardExperimentFiles(
                outputRoot, "pilot_test", "sequence_test", DateTime.UtcNow, randomSeed: 1);
            CheckerboardTrial trial = CreateTrial();
            files.WritePlan(new[] { trial }, gridLineSpacingDegrees: 10f, stimulusDurationSeconds: 0.6f,
                sequence, preStimulusSeconds: 0.4f, responseTimeoutSeconds: 5f, "TRIGGER", "TRACKPAD CLICK",
                responseKeysSwapped: false, VrControllerMapping.TrackpadConcaveTriggerConvex);
            files.AppendResult(CreateResult(sequence), plannedTrials: 1);

            string[] plan = File.ReadAllLines(files.PlanFile);
            string[] planHeader = plan[0].Split(',');
            string[] planRow = plan[1].Split(',');
            Assert.That(planRow.Length, Is.EqualTo(planHeader.Length));
            Assert.That(Column(planHeader, planRow, "trial_sequence"), Is.EqualTo(sequence.ToString()));
            Assert.That(Column(planHeader, planRow, "pre_stimulus_s"), Is.EqualTo("0.400000006"));
            Assert.That(Column(planHeader, planRow, "noise_until_response"), Is.EqualTo(noiseUntilResponse));
            Assert.That(Column(planHeader, planRow, "post_response_noise_s"),
                Is.EqualTo(postResponseNoiseSeconds));
            Assert.That(Column(planHeader, planRow, "controller_mapping"),
                Is.EqualTo("TrackpadConcaveTriggerConvex"));
            Assert.That(Column(planHeader, planRow, "category_b_key"), Is.EqualTo("TRACKPAD CLICK"));

            string[] trials = File.ReadAllLines(files.TrialResultsFile);
            string[] trialHeader = trials[0].Split(',');
            string[] trialRow = trials[1].Split(',');
            Assert.That(trialRow.Length, Is.EqualTo(trialHeader.Length));
            Assert.That(Column(trialHeader, trialRow, "trial_sequence"), Is.EqualTo(sequence.ToString()));
            Assert.That(Column(trialHeader, trialRow, "response"), Is.EqualTo("Convex"));
            Assert.That(Column(trialHeader, trialRow, "status"), Is.EqualTo("valid"));
            Assert.That(Column(trialHeader, trialRow, "stimulus_task"), Is.EqualTo("Checkerboard"));
            Assert.That(Column(trialHeader, trialRow, "mapping_version"),
                Is.EqualTo(CheckerboardInstrumentMapping.MappingVersion));
            Assert.That(Column(trialHeader, trialRow, "instrument_distortion_k"), Is.EqualTo("0.5"));
            Assert.That(Column(trialHeader, trialRow, "instrument_magnification_m"), Is.EqualTo("10"));
            Assert.That(trialHeader, Does.Not.Contain("visual_space_l"));
            Assert.That(trialHeader, Does.Not.Contain("oomes_endpoint_equivalent"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Files_StorePolarityPerTrialAndTheSettingInThePlan(bool inverted)
        {
            var files = new CheckerboardExperimentFiles(
                outputRoot, "pilot_test", "polarity_test", DateTime.UtcNow, randomSeed: 1);
            files.WritePlan(new[] { CreateTrial() }, 10f, 0.6f, Sequence.NoiseResponseThenGray,
                0.4f, 0f, "TRIGGER", "TRACKPAD CLICK", false,
                VrControllerMapping.TrackpadConcaveTriggerConvex, invertCheckerboardEachTrial: true);
            CheckerboardTrialResult result = CreateResult(Sequence.NoiseResponseThenGray);
            result.CheckerboardInverted = inverted;
            files.AppendResult(result, plannedTrials: 1);

            string[] plan = File.ReadAllLines(files.PlanFile);
            string[] planHeader = plan[0].Split(',');
            string[] planRow = plan[1].Split(',');
            Assert.That(planRow.Length, Is.EqualTo(planHeader.Length));
            Assert.That(Column(planHeader, planRow, "invert_checkerboard_each_trial"), Is.EqualTo("1"));

            string[] trials = File.ReadAllLines(files.TrialResultsFile);
            string[] trialHeader = trials[0].Split(',');
            string[] trialRow = trials[1].Split(',');
            Assert.That(trialRow.Length, Is.EqualTo(trialHeader.Length));
            Assert.That(Column(trialHeader, trialRow, "checkerboard_inverted"), Is.EqualTo(inverted ? "1" : "0"));
            Assert.That(Column(trialHeader, trialRow, "mapping_version"),
                Is.EqualTo("merlitz-instrument-checkerboard-fixed-source-grid-v2"));
        }

        private static string Column(string[] header, string[] row, string name)
        {
            int index = Array.IndexOf(header, name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Spalte fehlt: " + name);
            return row[index];
        }

        [Test]
        public void Files_SettingsSnapshotIsSeparateFromTheUnchangedCsvFiles()
        {
            var files = new CheckerboardExperimentFiles(
                outputRoot, "pilot_test", "settings_test", DateTime.UtcNow, randomSeed: 1);
            files.WritePlan(new[] { CreateTrial() }, 10f, 0.6f, Sequence.NoiseResponseThenGray,
                0.4f, 0f, "TRIGGER", "TRACKPAD CLICK", false,
                VrControllerMapping.TrackpadConcaveTriggerConvex);
            string planBefore = File.ReadAllText(files.PlanFile);
            string trialsBefore = File.ReadAllText(files.TrialResultsFile);
            const string settings = "{\"schemaVersion\":1,\"requireFixation\":true}";

            files.WriteSessionSettings(settings);

            Assert.That(File.ReadAllText(Path.Combine(files.SessionFolder,
                files.BaseFileName + "_settings.json")), Is.EqualTo(settings));
            Assert.That(File.ReadAllText(files.PlanFile), Is.EqualTo(planBefore));
            Assert.That(File.ReadAllText(files.TrialResultsFile), Is.EqualTo(trialsBefore));
        }

        private static CheckerboardTrial CreateTrial()
        {
            return new CheckerboardTrial(sequenceIndex: 1, conditionIndex: 1, repetition: 1, attemptNumber: 1,
                angularDiameterDegrees: 60f, eyePresentation: CheckerboardEyePresentation.BothEyes,
                instrumentDistortionK: 0.5f);
        }

        // Muster von 10,0 bis 10,6 s, Antwort 1,25 s nach Beginn der Antwortphase.
        private static CheckerboardTrialResult CreateResult(Sequence sequence)
        {
            return new CheckerboardTrialResult
            {
                Trial = CreateTrial(),
                PresentationIndex = 1,
                TrialStartUtc = DateTime.UtcNow,
                TrialStartUnitySeconds = 10d,
                StimulusEndUnitySeconds = 10.6d,
                ResponseWindowStartUnitySeconds = 10.6d,
                TrialEndUnitySeconds = 11.85d,
                ApertureEdgeSoftnessDegrees = 1f,
                CircularApertureEnabled = true,
                GridLineSpacingDegrees = 10f,
                GridLineSpacingUv = 0.3f,
                Response = CheckerboardCurvatureResponse.Convex,
                ValidForAnalysis = true,
                Gaze = new FixationSnapshot(true, true, 0.5f, 1f, 1f),
                Status = "valid",
                TrialSequence = sequence
            };
        }
    }
}
