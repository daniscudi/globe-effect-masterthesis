using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GlobeEffect.VRCheckerboard.EyeTracking;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    public enum RandomDotSessionState
    {
        Idle,
        InterTrial,
        WaitingForFixation,
        PresentingMotion,
        WaitingForResponse,
        Completed,
        Aborted
    }

    /// <summary>
    /// Steuert den ganzen Random-Dot-Versuch.
    ///
    /// So läuft ein Durchgang ab: Die Person schaut auf das Kreuz. Liegt der Blick
    /// ruhig genug, laufen die Punkte für eine feste Zeit nach links und rechts.
    /// Erst wenn die Bewegung vorbei ist, antwortet die Person konkav oder konvex.
    ///
    /// Welches l gezeigt wird, steht vorher fest. Schaut die Person zwischendurch
    /// zu lange weg, zählt der Durchgang nicht und kommt später noch einmal dran.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class RandomDotExperimentManager : MonoBehaviour
    {
        // Das läuft fast genauso wie beim Checkerboard:
        //
        // StartSession          baut den gemischten Plan und speichert ihn.
        // BeginNextAttempt      zeigt erst mal nur das Kreuz.
        // PresentCurrentTrial   startet die Punkte und die Bewegung.
        //
        // Danach wird auf konkav oder konvex gewartet. Hat die Person
        // danebengeschaut, kommt der Durchgang ganz hinten noch einmal dran.
        [Header("Referenzen")]
        [SerializeField]
        private RandomDotFieldStimulus stimulus;

        [SerializeField]
        private RandomDotKeyboardController keyboardController;

        [SerializeField]
        private RandomDotHeadSweepMonitor sweepMonitor;

        [SerializeField]
        private EyeTrackingToolbox eyeTrackingToolbox;

        [SerializeField]
        private RandomDotFixationMonitor fixationMonitor;

        [Header("Sitzung")]
        [SerializeField]
        [Tooltip("Kennung der Versuchsperson. Hier gehören keine echten Namen rein, sondern zum Beispiel pilot_001.")]
        private string participantId = "pilot_001";

        [SerializeField]
        private string sessionLabel = "random_dot_l_pilot";

        [SerializeField]
        [Tooltip("Mit derselben Zahl und denselben Einstellungen kommt wieder genau dieselbe Reihenfolge heraus.")]
        private int randomSeed = 20260901;

        [SerializeField]
        private int dotSeedBase = 24680;

        [SerializeField]
        [Tooltip("Wohin die Messdaten kommen. Leer lassen ist normal, dann landen sie im Ordner measurements im Projekt.")]
        private string outputRoot = string.Empty;

        [SerializeField]
        private bool autoStartOnPlay;

        [Header("Trialplan")]
        [SerializeField]
        [Tooltip("Wie groß der runde Ausschnitt sein soll, in Grad. Es können auch mehrere Werte drinstehen, dann wird jeder davon gezeigt.")]
        private List<float> angularDiametersDegrees = new() { 90f };

        [SerializeField]
        [Tooltip("Auf welchen Augen gezeigt wird: beide, nur links oder nur rechts.")]
        private List<CheckerboardEyePresentation> eyePresentations = new()
        {
            CheckerboardEyePresentation.BothEyes
        };

        [SerializeField]
        [FormerlySerializedAs("stimulusKValues")]
        [Tooltip("Alle l-Werte, die gezeigt werden sollen. Es sind dieselben wie beim Checkerboard. Die Person kann daran nichts verändern.")]
        private List<float> visualSpaceLValues = new()
        {
            1.2f,
            1f,
            0.8f,
            0.6f,
            0.5f,
            0.4f,
            0.2f
        };

        [SerializeField]
        [FormerlySerializedAs("magnifications")]
        [Tooltip("Zoom für das Punktfeld. 1 heißt: unverändert. Hat nichts mit l zu tun.")]
        private List<float> contentZoomValues = new() { 1f };

        [SerializeField]
        [Tooltip("SimulatedYaw heißt, der Computer macht die Bewegung. Das ist der normale Fall. Bei HeadTracked dreht die Person den Kopf selbst.")]
        private List<RandomDotMotionMode> motionModes = new()
        {
            RandomDotMotionMode.SimulatedYaw
        };

        [SerializeField, Min(1)]
        [Tooltip("Wie oft jede Kombination gezeigt wird. Mehr Wiederholungen heißt sicherere Ergebnisse, aber auch eine längere Sitzung.")]
        private int repetitionsPerCondition = 3;

        [Header("Simulierter Schwenk")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Wie lange die Punkte zu sehen sind und sich bewegen, in Sekunden.")]
        private float motionDurationSeconds = 4f;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Wie weit die Bewegung zu jeder Seite geht, in Grad. Dieser Wert gilt, sobald die Sitzung startet, und überschreibt den Vorschauwert am Random Dot Field.")]
        private float sweepAmplitudeDegrees = 5f;

        [SerializeField, Range(0.1f, 60f)]
        [Tooltip("Wie schnell die Bewegung läuft, in Grad pro Sekunde. Das Tempo bleibt dabei immer gleich. Überschreibt ebenfalls den Vorschauwert am Random Dot Field.")]
        private float sweepSpeedDegreesPerSecond = 5f;

        [Header("Fixation und Wiederholung")]
        [SerializeField]
        [Tooltip("Die Punkte kommen erst, wenn der Blick ruhig auf dem Kreuz liegt, und werden dabei auch weiter überwacht. Für eine echte Messung muss das an sein.")]
        private bool requireFixation = true;

        [SerializeField, Min(0f)]
        [Tooltip("So lange darf die Person am Stück vom Kreuz wegschauen. Danach wird der Durchgang ungültig.")]
        private float maximumOffTargetSeconds = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("So lange darf am Stück gar kein brauchbarer Blickwert kommen, zum Beispiel beim Blinzeln.")]
        private float maximumInvalidGazeSeconds = 0.2f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Ist der letzte Blickwert älter als das hier, gilt er als nicht mehr aktuell und zählt wie gar kein Wert.")]
        private float maximumGazeSampleAgeSeconds = 0.1f;

        [SerializeField, Min(0)]
        [Tooltip("Wie oft derselbe Durchgang höchstens wiederholt werden darf. 0 heißt: so lange, bis es klappt.")]
        private int maximumAttemptsPerTrial;

        [Header("Ablauf")]
        [SerializeField, Min(0f)]
        private float interTrialSeconds = 0.5f;

        [Header("Tasten")]
        [SerializeField]
        private Key startSessionKey = Key.F5;

        [SerializeField]
        private Key abortSessionKey = Key.F6;

        [Header("Laufzeitstatus (nur Anzeige)")]
        [SerializeField]
        private RandomDotSessionState sessionState = RandomDotSessionState.Idle;

        [SerializeField]
        private int currentTrialNumber;

        [SerializeField]
        private int totalTrials;

        [SerializeField]
        private int validTrialsCompleted;

        [SerializeField]
        private int presentationCount;

        [SerializeField]
        private string activeSessionFolder = string.Empty;

        private IReadOnlyList<RandomDotTrial> trialPlan;
        private RandomDotTrialQueue trialQueue;
        private RandomDotTrial currentTrial;
        private RandomDotExperimentFiles experimentFiles;
        private DateTime trialStartUtc;
        private double trialStartUnitySeconds;
        private double stimulusEndUnitySeconds;
        private float currentOffTargetSeconds;
        private float currentInvalidGazeSeconds;
        private float longestOffTargetSeconds;
        private float longestInvalidGazeSeconds;
        private Coroutine interTrialCoroutine;
        private Coroutine motionCoroutine;
        private bool eventsSubscribed;

        public event Action<RandomDotTrial> TrialStarted;
        public event Action<RandomDotTrialResult> TrialEnded;
        public event Action<RandomDotSessionState> SessionFinished;

        public RandomDotSessionState SessionState => sessionState;
        public RandomDotTrial CurrentTrial => currentTrial;
        public int CurrentTrialNumber => currentTrialNumber;
        public int TotalTrials => totalTrials;
        public int ValidTrialsCompleted => validTrialsCompleted;
        public int PresentationCount => presentationCount;
        public int PendingTrialCount => trialQueue?.Count ?? 0;
        public float CurrentOffTargetSeconds => currentOffTargetSeconds;
        public float CurrentInvalidGazeSeconds => currentInvalidGazeSeconds;
        public string ActiveSessionFolder => activeSessionFolder;
        public bool RequireFixation => requireFixation;
        public bool ResponseKeysSwapped =>
            keyboardController != null && keyboardController.SwapResponseKeys;
        public bool IsSessionActive =>
            sessionState == RandomDotSessionState.InterTrial ||
            sessionState == RandomDotSessionState.WaitingForFixation ||
            sessionState == RandomDotSessionState.PresentingMotion ||
            sessionState == RandomDotSessionState.WaitingForResponse;

        private void Awake()
        {
            // Möglichst früh suchen, damit später beim Start alles da ist.
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void Start()
        {
            if (autoStartOnPlay)
            {
                StartSession();
            }
        }

        private void Update()
        {
            // Hier werden die Tasten vom Versuchsleiter abgefragt: starten und
            // abbrechen. Läuft gerade die Bewegung, wird nebenher in jedem Frame
            // geprüft, ob der Blick noch auf dem Kreuz liegt.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!IsSessionActive && keyboard[startSessionKey].wasPressedThisFrame)
                {
                    StartSession();
                    return;
                }

                if (IsSessionActive && keyboard[abortSessionKey].wasPressedThisFrame)
                {
                    AbortSession("ManualAbort");
                    return;
                }
            }

            if (sessionState == RandomDotSessionState.WaitingForFixation &&
                fixationMonitor != null && fixationMonitor.RequirementMet)
            {
                PresentCurrentTrial();
                return;
            }

            if (sessionState == RandomDotSessionState.PresentingMotion &&
                requireFixation)
            {
                MonitorFixationDuringMotion();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            if (Application.isPlaying && IsSessionActive)
            {
                AbortSession("ControllerDisabled");
            }
        }

        public void Configure(
            RandomDotFieldStimulus randomDotStimulus,
            RandomDotKeyboardController randomDotKeyboard,
            RandomDotHeadSweepMonitor headSweepMonitor,
            EyeTrackingToolbox toolbox,
            RandomDotFixationMonitor randomDotFixationMonitor)
        {
            UnsubscribeEvents();
            stimulus = randomDotStimulus;
            keyboardController = randomDotKeyboard;
            sweepMonitor = headSweepMonitor;
            eyeTrackingToolbox = toolbox;
            fixationMonitor = randomDotFixationMonitor;
            if (isActiveAndEnabled)
            {
                SubscribeEvents();
            }
        }

        public bool StartSession()
        {
            // Das ist der große Startknopf. Der Reihe nach passiert hier:
            // Einstellungen prüfen -> alle Durchgänge bauen und mischen ->
            // die Messdateien anlegen -> Eye Tracking starten ->
            // den ersten Durchgang zeigen.
            if (IsSessionActive)
            {
                Debug.LogWarning("Eine Random-Dot-Sitzung läuft bereits.", this);
                return false;
            }

            ResolveReferences();
            SubscribeEvents();
            if (stimulus == null || keyboardController == null || sweepMonitor == null)
            {
                Debug.LogError(
                    "Random-Dot-Stimulus, Tastatursteuerung und Sweep-Monitor müssen zugewiesen sein.",
                    this);
                return false;
            }

            if (requireFixation && fixationMonitor == null)
            {
                Debug.LogError(
                    "Fixationskontrolle ist aktiv, aber der Random-Dot Fixation Monitor fehlt.",
                    this);
                return false;
            }

            try
            {
                trialPlan = RandomDotTrialPlanner.CreateRandomizedPlan(
                    angularDiametersDegrees,
                    eyePresentations,
                    visualSpaceLValues,
                    contentZoomValues,
                    motionModes,
                    repetitionsPerCondition,
                    randomSeed,
                    dotSeedBase);
                trialQueue = new RandomDotTrialQueue(trialPlan);

                DateTime sessionStartUtc = DateTime.UtcNow;
                string resolvedRoot = ExperimentOutputPath.Resolve(outputRoot);
                experimentFiles = RandomDotExperimentFiles.Create(
                    resolvedRoot,
                    participantId,
                    sessionLabel,
                    sessionStartUtc,
                    randomSeed);
                experimentFiles.WritePlan(trialPlan);
                activeSessionFolder = experimentFiles.SessionFolder;
                StartEyeTracking(sessionStartUtc);
            }
            catch (Exception exception)
            {
                sessionState = RandomDotSessionState.Aborted;
                Debug.LogError(
                    "Random-Dot-Sitzung konnte nicht gestartet werden: " +
                    exception.Message,
                    this);
                return false;
            }

            StopPendingCoroutines();
            currentTrial = null;
            currentTrialNumber = 0;
            totalTrials = trialPlan.Count;
            validTrialsCompleted = 0;
            presentationCount = 0;
            sessionState = RandomDotSessionState.InterTrial;

            Debug.Log(
                $"Random-Dot-Sitzung gestartet: {totalTrials} gültige Trials geplant.\n" +
                activeSessionFolder,
                this);
            BeginNextAttempt();
            return true;
        }

        public void AbortSession(string reason = "ManualAbort")
        {
            // Stoppt Bewegung und Aufnahme sauber. Beim Abbrechen geht nichts
            // verloren, alles bisher Gemessene bleibt im Sitzungsordner liegen.
            if (!IsSessionActive)
            {
                return;
            }

            StopPendingCoroutines();
            if ((sessionState == RandomDotSessionState.PresentingMotion ||
                 sessionState == RandomDotSessionState.WaitingForResponse) &&
                currentTrial != null && experimentFiles != null)
            {
                if (stimulusEndUnitySeconds <= trialStartUnitySeconds)
                {
                    stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
                }

                TryAppendResult(CaptureCurrentResult(
                    CheckerboardCurvatureResponse.None,
                    validForAnalysis: false,
                    "aborted:" + (reason ?? string.Empty)));
            }

            WriteMarker("SessionAborted;task=random_dot_l;reason=" +
                CheckerboardExperimentFiles.SanitizeIdentifier(reason, "unspecified"));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = RandomDotSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void BeginNextAttempt()
        {
            // Holt den nächsten Durchgang aus der Warteschlange. Erst ist nur das
            // Kreuz zu sehen. Die Punkte kommen erst, wenn der Blick ruhig liegt.
            interTrialCoroutine = null;
            if (trialQueue == null || !trialQueue.TryTakeNext(out currentTrial))
            {
                CompleteSession();
                return;
            }

            presentationCount++;
            currentTrialNumber = validTrialsCompleted + 1;
            stimulus.Hide();
            stimulus.SetAngularDiameter(currentTrial.AngularDiameterDegrees);
            stimulus.SetContentZoom(currentTrial.ContentZoom);
            stimulus.SetEyePresentation(currentTrial.EyePresentation);
            stimulus.SetMotionMode(currentTrial.MotionMode);
            stimulus.SetVisualSpaceL(currentTrial.VisualSpaceL);
            stimulus.SetSimulatedSweep(
                sweepAmplitudeDegrees,
                sweepSpeedDegreesPerSecond);
            stimulus.SetSweepDirection(currentTrial.SweepDirection);
            stimulus.ConfigurePointField(
                stimulus.DotCount,
                currentTrial.DotSeed,
                stimulus.WorldCoverageDiameterDegrees);
            stimulus.PlaceAroundObserver();

            sweepMonitor.ResetForTrial();
            fixationMonitor?.ResetFixationWindow();
            ResetFixationCounters();
            stimulusEndUnitySeconds = 0d;

            if (requireFixation)
            {
                sessionState = RandomDotSessionState.WaitingForFixation;
                stimulus.ShowFixationOnly();
                WriteMarker(string.Format(
                    CultureInfo.InvariantCulture,
                    "FixationAcquisitionStart;task=random_dot_l;sequence={0};attempt={1}",
                    currentTrial.SequenceIndex,
                    currentTrial.AttemptNumber));
            }
            else
            {
                PresentCurrentTrial();
            }
        }

        private void PresentCurrentTrial()
        {
            // Gibt l, Zoom, FOV, Augenmodus und Bewegungsart an das Punktfeld weiter
            // und lässt die Bewegung von vorne losgehen.
            if (currentTrial == null)
            {
                return;
            }

            fixationMonitor?.ResetFixationWindow();
            ResetFixationCounters();
            sweepMonitor?.ResetForTrial();
            stimulus.RestartMotionPhase();
            trialStartUtc = DateTime.UtcNow;
            trialStartUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulusEndUnitySeconds = 0d;
            sessionState = RandomDotSessionState.PresentingMotion;

            WriteMarker(BuildTrialStartMarker(currentTrial));
            stimulus.Show();
            TrialStarted?.Invoke(currentTrial);
            motionCoroutine = StartCoroutine(EndMotionAfterDuration());

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "Random-Dot-Trial {0}/{1}, Präsentation {2}: l={3:F3}, Zoom={4:F2}, " +
                "{5}, zuerst {6}, Versuch {7}. Fixationskreuz anschauen; Antwort folgt nach der Bewegung.",
                currentTrialNumber,
                totalTrials,
                presentationCount,
                currentTrial.VisualSpaceL,
                currentTrial.ContentZoom,
                currentTrial.MotionMode,
                currentTrial.SweepDirection,
                currentTrial.AttemptNumber),
                this);
        }

        private IEnumerator EndMotionAfterDuration()
        {
            // Eine Coroutine wartet, ohne dass dabei alles andere stehen bleibt.
            // Unity zeichnet weiter und das Eye Tracking misst weiter.
            yield return new WaitForSecondsRealtime(motionDurationSeconds);
            motionCoroutine = null;
            EndMotionPresentation();
        }

        private void EndMotionPresentation()
        {
            // Die Bewegung ist vorbei. Ab jetzt wird nur noch auf die Antwort gewartet.
            if (sessionState != RandomDotSessionState.PresentingMotion ||
                currentTrial == null)
            {
                return;
            }

            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            stimulus.Hide();
            sessionState = RandomDotSessionState.WaitingForResponse;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "StimulusEnded;task=random_dot_l;sequence={0};attempt={1};duration_s={2:F4}",
                currentTrial.SequenceIndex,
                currentTrial.AttemptNumber,
                stimulusEndUnitySeconds - trialStartUnitySeconds));

            string responseHint = ResponseKeysSwapped
                ? "Links = konvex, rechts = konkav."
                : "Links = konkav, rechts = konvex.";
            Debug.Log("Random-Dot-Antwort: " + responseHint, this);
        }

        private void HandleResponseSubmitted(CheckerboardCurvatureResponse response)
        {
            // Drückt die Person schon während der Bewegung, zählt das nicht. Sie
            // soll sich erst die ganze Bewegung ansehen und dann entscheiden.
            if (sessionState != RandomDotSessionState.WaitingForResponse ||
                currentTrial == null ||
                response == CheckerboardCurvatureResponse.None)
            {
                return;
            }

            RandomDotTrialResult result = CaptureCurrentResult(
                response,
                validForAnalysis: true,
                "valid");
            if (!TryAppendResult(result))
            {
                return;
            }

            validTrialsCompleted++;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialResponse;task=random_dot_l;sequence={0};attempt={1};response={2};response_s={3:F4};valid=1",
                currentTrial.SequenceIndex,
                currentTrial.AttemptNumber,
                response,
                result.ResponseTimeSeconds));
            TrialEnded?.Invoke(result);
            FinishAttemptAndScheduleNext();
        }

        private void MonitorFixationDuringMotion()
        {
            // Zwei Dinge werden getrennt mitgezählt: wie lange die Person am Kreuz
            // vorbeigeschaut hat, und wie lange gar keine Daten da waren.
            //
            // Getrennt deshalb, weil ein Blinzeln etwas anderes ist als wegschauen.
            // Wird eine der beiden Zeiten am Stück überschritten, zählt nur dieser
            // eine Durchgang nicht. Die Sitzung läuft normal weiter.
            if (fixationMonitor == null)
            {
                InvalidateCurrentTrial("missing_fixation_monitor");
                return;
            }

            float delta = Time.unscaledDeltaTime;
            bool sampleRecent = fixationMonitor.HasRecentSample(
                maximumGazeSampleAgeSeconds);

            if (!sampleRecent || !fixationMonitor.CurrentSampleValid)
            {
                currentInvalidGazeSeconds += delta;
                currentOffTargetSeconds = 0f;
            }
            else if (!fixationMonitor.IsInsideTolerance)
            {
                currentOffTargetSeconds += delta;
                currentInvalidGazeSeconds = 0f;
            }
            else
            {
                currentOffTargetSeconds = 0f;
                currentInvalidGazeSeconds = 0f;
            }

            longestOffTargetSeconds = Mathf.Max(
                longestOffTargetSeconds,
                currentOffTargetSeconds);
            longestInvalidGazeSeconds = Mathf.Max(
                longestInvalidGazeSeconds,
                currentInvalidGazeSeconds);

            if (currentOffTargetSeconds > maximumOffTargetSeconds)
            {
                InvalidateCurrentTrial("off_target");
            }
            else if (currentInvalidGazeSeconds > maximumInvalidGazeSeconds)
            {
                InvalidateCurrentTrial("invalid_gaze_data");
            }
        }

        private void InvalidateCurrentTrial(string reason)
        {
            // Bewegung stoppen, den misslungenen Versuch trotzdem speichern und
            // denselben Durchgang mit höherer Versuchsnummer ganz hinten wieder
            // in die Warteschlange hängen.
            if (sessionState != RandomDotSessionState.PresentingMotion ||
                currentTrial == null)
            {
                return;
            }

            StopMotionCoroutine();
            stimulusEndUnitySeconds = Time.realtimeSinceStartupAsDouble;
            RandomDotTrial invalidTrial = currentTrial;
            RandomDotTrialResult result = CaptureCurrentResult(
                CheckerboardCurvatureResponse.None,
                validForAnalysis: false,
                "invalid_fixation:" + reason);
            if (!TryAppendResult(result))
            {
                return;
            }

            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialInvalid;task=random_dot_l;sequence={0};attempt={1};reason={2};off_target_s={3:F4};invalid_gaze_s={4:F4}",
                invalidTrial.SequenceIndex,
                invalidTrial.AttemptNumber,
                reason,
                longestOffTargetSeconds,
                longestInvalidGazeSeconds));
            TrialEnded?.Invoke(result);

            bool limitReached = maximumAttemptsPerTrial > 0 &&
                invalidTrial.AttemptNumber >= maximumAttemptsPerTrial;
            if (limitReached)
            {
                stimulus.Hide();
                WriteMarker("SessionAborted;task=random_dot_l;reason=maximum_repeat_attempts_reached");
                StopEyeTrackingRecording();
                currentTrial = null;
                sessionState = RandomDotSessionState.Aborted;
                SessionFinished?.Invoke(sessionState);
                Debug.LogError(
                    "Die maximale Zahl an Wiederholungen wurde erreicht. Die Sitzung wurde beendet.",
                    this);
                return;
            }

            RandomDotTrial repeat = trialQueue.AppendRepeatedAttempt(invalidTrial);
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "TrialRepeatQueued;task=random_dot_l;sequence={0};next_attempt={1};queue_position={2}",
                repeat.SequenceIndex,
                repeat.AttemptNumber,
                trialQueue.Count));
            FinishAttemptAndScheduleNext();
        }

        private RandomDotTrialResult CaptureCurrentResult(
            CheckerboardCurvatureResponse response,
            bool validForAnalysis,
            string status)
        {
            // Hier wird alles zu diesem Durchgang in ein Objekt gepackt: die
            // Bedingung, die Antwort, die Bewegung, die Zeiten und die Blickwerte.
            // Dieses Objekt geht danach an die Dateiklasse, die daraus eine
            // CSV-Zeile macht.
            double responseTime = Time.realtimeSinceStartupAsDouble;
            double resolvedStimulusEnd = stimulusEndUnitySeconds > trialStartUnitySeconds
                ? stimulusEndUnitySeconds
                : responseTime;

            return new RandomDotTrialResult(
                currentTrial,
                presentationCount,
                trialStartUtc,
                trialStartUnitySeconds,
                resolvedStimulusEnd,
                responseTime,
                response,
                validForAnalysis,
                sweepMonitor?.CompletedHalfSweeps ?? 0,
                sweepMonitor?.MinimumYawDegrees ?? 0f,
                sweepMonitor?.MaximumYawDegrees ?? 0f,
                stimulus.SweepAmplitudeDegrees,
                stimulus.SweepSpeedDegreesPerSecond,
                stimulus.ApertureEdgeSoftnessDegrees,
                fixationMonitor != null && fixationMonitor.CurrentSampleValid,
                fixationMonitor != null && fixationMonitor.IsInsideTolerance,
                fixationMonitor != null ? fixationMonitor.CurrentAngleDegrees : float.NaN,
                fixationMonitor != null ? fixationMonitor.ContinuousFixationSeconds : 0f,
                fixationMonitor != null ? fixationMonitor.ValidSampleFraction : float.NaN,
                longestOffTargetSeconds,
                longestInvalidGazeSeconds,
                stimulus.DotCount,
                stimulus.WorldCoverageDiameterDegrees,
                stimulus.FieldRadiusMeters,
                status);
        }

        private bool TryAppendResult(RandomDotTrialResult result)
        {
            // Wenn das Schreiben schiefgeht, zum Beispiel weil die Datei noch in
            // Excel offen ist, wird abgebrochen. Sonst würde die Messung munter
            // weiterlaufen und am Ende wäre nichts gespeichert.
            try
            {
                experimentFiles.AppendResult(result, totalTrials);
                return true;
            }
            catch (Exception exception)
            {
                FailAfterWriteError(exception);
                return false;
            }
        }

        private void FinishAttemptAndScheduleNext()
        {
            // Punkte ausblenden, kurz warten und dann den nächsten Durchgang holen.
            // Der Bildschirm bleibt in der Pause leer, damit die vorige Bewegung
            // nicht in die nächste hineinwirkt.
            StopMotionCoroutine();
            stimulus.Hide();
            currentTrial = null;
            sessionState = RandomDotSessionState.InterTrial;

            if (interTrialSeconds <= 0f)
            {
                BeginNextAttempt();
            }
            else
            {
                interTrialCoroutine = StartCoroutine(BeginNextAttemptAfterDelay());
            }
        }

        private IEnumerator BeginNextAttemptAfterDelay()
        {
            yield return new WaitForSecondsRealtime(interTrialSeconds);
            BeginNextAttempt();
        }

        private void CompleteSession()
        {
            // Zum Schluss: Marker in die Aufnahme schreiben und die
            // Eye-Tracking-Aufzeichnung beenden.
            currentTrial = null;
            currentTrialNumber = totalTrials;
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "SessionCompleted;task=random_dot_l;valid_trials={0};presentations={1}",
                validTrialsCompleted,
                presentationCount));
            stimulus?.Hide();
            StopEyeTrackingRecording();
            sessionState = RandomDotSessionState.Completed;
            SessionFinished?.Invoke(sessionState);
            Debug.Log(
                $"Random-Dot-Sitzung vollständig gespeichert: {validTrialsCompleted} gültige Trials aus {presentationCount} Präsentationen.\n" +
                activeSessionFolder,
                this);
        }

        private void HandleHalfSweepCompleted(int count, float yawDegrees)
        {
            // Der Sweep Monitor sagt Bescheid, sobald die Bewegung wieder an einem
            // Rand angekommen ist. So kann man hinterher nachzählen, wie oft die
            // Punkte in einem Durchgang hin und her gelaufen sind.
            if (sessionState != RandomDotSessionState.PresentingMotion ||
                currentTrial == null)
            {
                return;
            }

            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "MotionHalfSweep;task=random_dot_l;sequence={0};count={1};yaw={2:F3};l={3:F4}",
                currentTrial.SequenceIndex,
                count,
                yawDegrees,
                currentTrial.VisualSpaceL));
        }

        private void StartEyeTracking(DateTime sessionStartUtc)
        {
            // Die Blickdaten selbst schreibt die Lab-Toolbox. Von hier wird die
            // Aufnahme nur an- und ausgeschaltet, und es werden Marker gesetzt.
            if (eyeTrackingToolbox == null)
            {
                Debug.LogWarning("Sitzung läuft ohne Eye-Tracking-Aufzeichnung.", this);
                return;
            }

            if (eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }

            eyeTrackingToolbox.SetOutputFolder(activeSessionFolder);
            eyeTrackingToolbox.StartRecording(experimentFiles.BaseFileName);
            WriteMarker(string.Format(
                CultureInfo.InvariantCulture,
                "SessionStart;task=random_dot_l;participant={0};session={1};seed={2};planned_trials={3};utc={4};mapping={5}",
                CheckerboardExperimentFiles.SanitizeIdentifier(participantId, "pilot"),
                CheckerboardExperimentFiles.SanitizeIdentifier(sessionLabel, "random_dot"),
                randomSeed,
                trialPlan.Count,
                sessionStartUtc.ToString("O", CultureInfo.InvariantCulture),
                RandomDotExperimentFiles.MappingVersion));
        }

        private string BuildTrialStartMarker(RandomDotTrial trial)
        {
            // Ein Marker ist eine kurze Notiz mitten in der Eye-Tracking-Aufnahme.
            // Hier stehen alle wichtigen Werte des Durchgangs mit drin. Dadurch
            // weiß man beim Auswerten, welcher Blickwert zu welcher Bedingung gehört.
            return string.Format(
                CultureInfo.InvariantCulture,
                "TrialStart;task=random_dot_l;presentation={0};sequence={1};condition={2};repetition={3};" +
                "attempt={4};eye={5};fov_deg={6:F3};edge_softness_deg={7:F3};" +
                "visual_space_l={8:F4};content_zoom={9:F4};motion={10};direction={11};" +
                "duration_s={12:F3};amplitude_deg={13:F3};speed_deg_s={14:F3};dot_seed={15}",
                presentationCount,
                trial.SequenceIndex,
                trial.ConditionIndex,
                trial.Repetition,
                trial.AttemptNumber,
                trial.EyePresentation,
                trial.AngularDiameterDegrees,
                stimulus.ApertureEdgeSoftnessDegrees,
                trial.VisualSpaceL,
                trial.ContentZoom,
                trial.MotionMode,
                trial.SweepDirection,
                motionDurationSeconds,
                sweepAmplitudeDegrees,
                sweepSpeedDegreesPerSecond,
                trial.DotSeed);
        }

        private void ResolveReferences()
        {
            // Felder, die im Inspector leer geblieben sind, werden hier in der Szene
            // gesucht. Was schon eingetragen ist, wird nicht angefasst.
            stimulus ??= FindAnyObjectByType<RandomDotFieldStimulus>();
            keyboardController ??= stimulus != null
                ? stimulus.GetComponent<RandomDotKeyboardController>()
                : null;
            sweepMonitor ??= stimulus != null
                ? stimulus.GetComponent<RandomDotHeadSweepMonitor>()
                : null;
            eyeTrackingToolbox ??= EyeTrackingToolbox.Instance;
            eyeTrackingToolbox ??= FindAnyObjectByType<EyeTrackingToolbox>();
            fixationMonitor ??= FindAnyObjectByType<RandomDotFixationMonitor>();
        }

        private void SubscribeEvents()
        {
            if (eventsSubscribed || keyboardController == null || sweepMonitor == null)
            {
                return;
            }

            keyboardController.ResponseSubmitted += HandleResponseSubmitted;
            sweepMonitor.HalfSweepCompleted += HandleHalfSweepCompleted;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed)
            {
                return;
            }

            if (keyboardController != null)
            {
                keyboardController.ResponseSubmitted -= HandleResponseSubmitted;
            }

            if (sweepMonitor != null)
            {
                sweepMonitor.HalfSweepCompleted -= HandleHalfSweepCompleted;
            }

            eventsSubscribed = false;
        }

        private void ResetFixationCounters()
        {
            // Jeder Versuch fängt mit frischen Zählern bei null an. Sonst würde man
            // die Aussetzer vom vorigen Durchgang mitschleppen.
            currentOffTargetSeconds = 0f;
            currentInvalidGazeSeconds = 0f;
            longestOffTargetSeconds = 0f;
            longestInvalidGazeSeconds = 0f;
        }

        private void WriteMarker(string message)
        {
            eyeTrackingToolbox?.WriteMessage(message);
        }

        private void StopEyeTrackingRecording()
        {
            if (eyeTrackingToolbox != null && eyeTrackingToolbox.IsRecording)
            {
                eyeTrackingToolbox.StopRecording();
            }
        }

        private void StopMotionCoroutine()
        {
            if (motionCoroutine == null)
            {
                return;
            }

            StopCoroutine(motionCoroutine);
            motionCoroutine = null;
        }

        private void StopPendingCoroutines()
        {
            StopMotionCoroutine();
            if (interTrialCoroutine == null)
            {
                return;
            }

            StopCoroutine(interTrialCoroutine);
            interTrialCoroutine = null;
        }

        private void FailAfterWriteError(Exception exception)
        {
            Debug.LogError(
                "Random-Dot-Trial konnte nicht gespeichert werden; die Sitzung wird beendet: " +
                exception.Message,
                this);
            WriteMarker("SessionAborted;task=random_dot_l;reason=result_write_error");
            StopPendingCoroutines();
            stimulus?.Hide();
            StopEyeTrackingRecording();
            currentTrial = null;
            sessionState = RandomDotSessionState.Aborted;
            SessionFinished?.Invoke(sessionState);
        }

        private void OnValidate()
        {
            repetitionsPerCondition = Mathf.Max(1, repetitionsPerCondition);
            motionDurationSeconds = Mathf.Max(0.1f, motionDurationSeconds);
            sweepAmplitudeDegrees = Mathf.Clamp(sweepAmplitudeDegrees, 0.1f, 30f);
            sweepSpeedDegreesPerSecond = Mathf.Clamp(
                sweepSpeedDegreesPerSecond,
                0.1f,
                60f);
            maximumOffTargetSeconds = Mathf.Max(0f, maximumOffTargetSeconds);
            maximumInvalidGazeSeconds = Mathf.Max(0f, maximumInvalidGazeSeconds);
            maximumGazeSampleAgeSeconds = Mathf.Max(0.01f, maximumGazeSampleAgeSeconds);
            maximumAttemptsPerTrial = Mathf.Max(0, maximumAttemptsPerTrial);
            interTrialSeconds = Mathf.Max(0f, interTrialSeconds);
        }
    }
}
