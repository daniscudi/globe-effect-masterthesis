using System;
using System.Collections.Generic;
using System.Globalization;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Was der Checkerboard- und der Random-Dot-Versuch gemeinsam haben: die
    /// Sitzungs- und Blickeinstellungen, die Tasten, die Zähler für den Monitor
    /// und ein paar kleine Hilfen (Marker, Eye-Tracking-Aufnahme, Coroutinen).
    ///
    /// Die Feldnamen sind genau die, die vorher in beiden Managern standen. Unity
    /// liest gespeicherte Szenen deshalb unverändert ein.
    /// </summary>
    public abstract class ExperimentManagerBase : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        protected EyeTrackingToolbox eyeTrackingToolbox;

        [Header("Session")]
        [SerializeField]
        [Tooltip("Code for the participant, e.g. pilot_001. Do not enter real names.")]
        protected string participantId = "pilot_001";

        [SerializeField]
        [Tooltip("Same number and same settings = exactly the same trial order again.")]
        protected int randomSeed = 20260901;

        [SerializeField]
        [Tooltip("Folder for the data files. Leave empty to save into the measurements folder of the " +
            "project.")]
        protected string outputRoot = string.Empty;

        [SerializeField]
        protected bool autoStartOnPlay;

        [Header("Trial Plan")]
        [FormerlySerializedAs("angularDiametersDegrees")]
        [SerializeField]
        [Tooltip("Size of the round window in degrees. With several values, each size is tested.")]
        protected List<float> fieldOfViewValues = new() { 90f };

        [SerializeField]
        [Tooltip("Which eyes see the image: both, only left or only right.")]
        protected List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [FormerlySerializedAs("repetitionsPerCondition")]
        [SerializeField, Min(1)]
        [Tooltip("How often each combination is shown, if a value has no own number below.")]
        protected int repeatsPerCondition = 3;

        [SerializeField, Range(1, 10)]
        [Tooltip("Splits the session into parts with a break in between. 4 = breaks after 25, 50 and 75 % " +
            "of the trials (red cross, continue with F5). 1 = no breaks.")]
        protected int blocksPerSession = 4;

        [Header("Gaze Check")]
        [SerializeField]
        [Tooltip("The image only appears when the participant looks steadily at the cross. Must be on for " +
            "real measurements.")]
        protected bool requireFixation = true;

        [FormerlySerializedAs("maximumOffTargetSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("How long the participant may look away from the cross before the trial is repeated later " +
            "(s).")]
        protected float maxLookAwaySeconds = 0.15f;

        [FormerlySerializedAs("maximumInvalidGazeSeconds")]
        [SerializeField, Min(0f)]
        [Tooltip("How long the eye tracker may give no gaze data, e.g. when blinking, before the trial is " +
            "repeated later (s).")]
        protected float maxNoDataSeconds = 0.2f;

        [FormerlySerializedAs("maximumGazeSampleAgeSeconds")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Gaze data older than this counts as missing (s).")]
        protected float maxSampleAgeSeconds = 0.1f;

        [FormerlySerializedAs("maximumAttemptsPerTrial")]
        [SerializeField, Min(0)]
        [Tooltip("How often one trial may be tried in total. 0 = until it works.")]
        protected int maxRepeatsPerTrial;

        [Header("Keys")]
        [SerializeField]
        protected Key startSessionKey = Key.F5;

        [SerializeField]
        protected Key abortSessionKey = Key.F6;

        [SerializeField]
        [Tooltip("Key that starts the practice from the start screen.")]
        protected Key trainingKey = Key.T;

        [SerializeField]
        [Tooltip("Turns the preview on or off. Nothing is recorded.")]
        protected Key previewKey = Key.P;

        // Stand vor dem ersten "Vorschau-Werte übernehmen". Leer heißt: es wurde
        // nichts übernommen. "Originalwerte wiederherstellen" holt ihn zurück.
        [SerializeField, HideInInspector]
        protected string valuesBeforePreviewTakeover = string.Empty;

        [Header("Current Status")]
        [SerializeField]
        protected int currentTrialNumber;

        [SerializeField]
        protected int totalTrials;

        [SerializeField]
        protected int validTrialsCompleted;

        [SerializeField]
        protected int presentationCount;

        [SerializeField]
        protected string activeSessionFolder = string.Empty;

        // Laufzeit: gemeinsam für beide Versuche.
        protected readonly LookAwayTimer lookAway = new();
        protected DateTime trialStartUtc;
        protected double trialStartUnitySeconds;
        protected double stimulusEndUnitySeconds;
        protected int pausesTaken;
        protected bool previewActive;
        protected string settingsBeforePreview;
        protected string lookToKeepAfterPreview;

        // Übungsantworten für das Kontrollfenster des Versuchsleiters. Nur im
        // Speicher, keine Datei; beginnt bei jedem Übungsstart leer.
        protected readonly PracticeAnswerLog practiceLog = new();

        public PracticeAnswerLog PracticeLog => practiceLog;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public int ValidTrialsCompleted => validTrialsCompleted;
        public int PresentationCount => presentationCount;
        public int PausesTaken => pausesTaken;
        public bool RequireFixation => requireFixation;
        public bool IsPreviewActive => previewActive;

        // Für die Editor-Übergabe aus dem Play Mode in die Szene.
        public string ValuesBeforePreviewTakeover
        {
            get => valuesBeforePreviewTakeover;
            set => valuesBeforePreviewTakeover = value ?? string.Empty;
        }

        public bool HasValuesBeforePreviewTakeover => !string.IsNullOrEmpty(valuesBeforePreviewTakeover);

        // Ob gerade Marker in die Eye-Tracking-Aufnahme geschrieben werden dürfen
        // (z. B. nicht bei einer Übung ohne Messdateien).
        protected virtual bool MarkersEnabled => true;

        protected void WriteMarker(string message)
        {
            if (MarkersEnabled && eyeTrackingToolbox != null)
            {
                eyeTrackingToolbox.WriteMessage(message);
            }
        }

        // Wie string.Format, aber immer mit Punkt als Dezimaltrennzeichen, egal
        // welche Sprache Windows eingestellt hat. Sonst stünde mal 0,5 und mal 0.5
        // in der Aufnahme.
        protected void WriteMarker(string format, params object[] values)
        {
            WriteMarker(string.Format(CultureInfo.InvariantCulture, format, values));
        }

        protected void StopEyeTrackingRecording()
        {
            if (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }
        }

        // Hält eine laufende Coroutine an und merkt sich, dass keine mehr läuft.
        protected void StopAndClear(ref Coroutine coroutine)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
                coroutine = null;
            }
        }

        protected virtual void OnValidate()
        {
            repeatsPerCondition = Mathf.Max(1, repeatsPerCondition);
            maxLookAwaySeconds = Mathf.Max(0f, maxLookAwaySeconds);
            maxNoDataSeconds = Mathf.Max(0f, maxNoDataSeconds);
            maxSampleAgeSeconds = Mathf.Max(0.01f, maxSampleAgeSeconds);
            maxRepeatsPerTrial = Mathf.Max(0, maxRepeatsPerTrial);
        }
    }

    /// <summary>
    /// Eine Antwort aus einem Übungstrial. Sie erscheint nur im Experiment
    /// Monitor; die Versuchsperson sieht sie nicht, und sie wird nicht gespeichert.
    /// </summary>
    public sealed class PracticeAnswer
    {
        public int Number;
        public string Part = string.Empty;
        public float K;
        public CheckerboardCurvatureResponse Response;

        // Ab Beginn der Antwortphase. NaN = keine Antwort.
        public double ResponseTimeSeconds = double.NaN;

        // Nur ein Hinweis für den Versuchsleiter, keine richtige Antwort:
        // Welchem Beispiel liegt k näher? None = kein Hinweis (oder genau dazwischen).
        public CheckerboardCurvatureResponse CloserToExample;
        public string Note = string.Empty;
    }

    /// <summary>
    /// Die Übungsantworten seit dem letzten Übungsstart, für das Kontrollfenster.
    /// Damit sieht der Versuchsleiter, ob jemand konvex und konkav verstanden hat,
    /// zum Beispiel wenn bei deutlich konvexem k immer "konkav" kommt. Es gibt
    /// bewusst kein Richtig/Falsch, weil sich die Wahrnehmung zwischen Personen
    /// unterscheidet.
    /// </summary>
    public sealed class PracticeAnswerLog
    {
        private readonly List<PracticeAnswer> answers = new();

        public IReadOnlyList<PracticeAnswer> Answers => answers;

        public void Clear() => answers.Clear();

        public PracticeAnswer Add(string part, float k, CheckerboardCurvatureResponse response,
            double responseTimeSeconds, CheckerboardCurvatureResponse closerToExample, string note = "")
        {
            var answer = new PracticeAnswer
            {
                Number = answers.Count + 1,
                Part = part ?? string.Empty,
                K = k,
                Response = response,
                ResponseTimeSeconds = response == CheckerboardCurvatureResponse.None
                    ? double.NaN : responseTimeSeconds,
                CloserToExample = closerToExample,
                Note = note ?? string.Empty
            };
            answers.Add(answer);
            return answer;
        }

        /// <summary>
        /// Welchem Beispiel k näher liegt. Bei (fast) gleichem Abstand None, damit
        /// in der Mitte kein Hinweis entsteht. Die kleine Toleranz fängt Rundung
        /// der float-Werte ab (z. B. 0,7 zwischen 0 und 1,4).
        /// </summary>
        public static CheckerboardCurvatureResponse CloserToExample(
            float k, float convexExampleK, float concaveExampleK)
        {
            double toConvex = Math.Abs((double)k - convexExampleK);
            double toConcave = Math.Abs((double)k - concaveExampleK);
            if (Math.Abs(toConvex - toConcave) < 1e-4) return CheckerboardCurvatureResponse.None;
            return toConvex < toConcave
                ? CheckerboardCurvatureResponse.Convex
                : CheckerboardCurvatureResponse.Concave;
        }

        /// <summary>
        /// Eine Zeile pro k, nach k sortiert, zum Beispiel
        /// "k 1.40: Convex 0, Concave 3, no answer 0".
        /// </summary>
        public List<string> SummaryByK()
        {
            var counts = new SortedDictionary<double, int[]>();
            foreach (PracticeAnswer answer in answers)
            {
                double key = Math.Round(answer.K, 3);
                if (!counts.TryGetValue(key, out int[] perResponse))
                {
                    perResponse = new int[3];
                    counts.Add(key, perResponse);
                }

                perResponse[(int)answer.Response]++;
            }

            var lines = new List<string>();
            foreach (KeyValuePair<double, int[]> entry in counts)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "k {0:0.00}: Convex {1}, Concave {2}, no answer {3}", entry.Key,
                    entry.Value[(int)CheckerboardCurvatureResponse.Convex],
                    entry.Value[(int)CheckerboardCurvatureResponse.Concave],
                    entry.Value[(int)CheckerboardCurvatureResponse.None]));
            }

            return lines;
        }
    }
}
