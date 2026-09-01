using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Varjo.XR;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Zentrale Eye-Tracking-Toolbox nach dem Aufbau der Lab-Version.
    /// Sie wählt den Provider, transformiert HMD-lokale Rays in Weltkoordinaten
    /// und schreibt Blick- sowie Transformdaten in getrennte CSV-Dateien.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class EyeTrackingToolbox : MonoBehaviour
    {
        public enum ETProvider
        {
            Dummy,
            Varjo
        }

        public enum TrackingOptions
        {
            LocalTransform,
            GlobalTransform
        }

        [Serializable]
        public sealed class TrackedObjectOptions
        {
            public TrackingOptions trackingOptions;
            public GameObject gameObject;
        }

        private readonly struct GazeRecord
        {
            public readonly GazeData Data;
            public readonly string Message;

            public GazeRecord(GazeData data, string message)
            {
                Data = data;
                Message = message;
            }
        }

        public static EyeTrackingToolbox Instance { get; private set; }

        [Header("Eye-Tracking-Einstellungen")]
        [SerializeField]
        private ETProvider provider = ETProvider.Varjo;

        [SerializeField]
        [Tooltip("Transform der getrackten Center-Eye-/Main-Camera.")]
        private Transform mainCameraTransform;

        [SerializeField]
        [Tooltip("Bleibt die Toolbox bei einem Szenenwechsel erhalten?")]
        private bool persistAcrossScenes = true;

        [Header("Varjo XR-4")]
        [SerializeField]
        private VarjoEyeTracking.GazeCalibrationMode calibrationMode =
            VarjoEyeTracking.GazeCalibrationMode.Fast;

        [SerializeField]
        private VarjoEyeTracking.GazeOutputFilterType outputFilterType =
            VarjoEyeTracking.GazeOutputFilterType.Standard;

        [SerializeField]
        private VarjoEyeTracking.GazeOutputFrequency outputFrequency =
            VarjoEyeTracking.GazeOutputFrequency.MaximumSupported;

        [Header("Bedienung im Play Mode")]
        [SerializeField]
        [Tooltip("Startet die Varjo-Blickkalibrierung.")]
        private Key calibrateKey = Key.C;

        [SerializeField]
        [Tooltip("Startet oder beendet eine technische Testaufzeichnung.")]
        private Key recordingToggleKey = Key.F9;

        [Header("Aufzeichnung")]
        [SerializeField]
        [Tooltip("Leer = Application.persistentDataPath/Measurements.")]
        private string outputFolder = string.Empty;

        [SerializeField]
        private bool recordHeadAndObjects = true;

        [SerializeField]
        private bool saveRaycastHitpoint;

        [SerializeField]
        private List<TrackedObjectOptions> trackedObjectList = new();

        [Header("Checkerboard-Marker")]
        [SerializeField]
        [Tooltip("Optionaler Stimulus, dessen Show/Hide/Parameter-Ereignisse protokolliert werden.")]
        private VrCheckerboardStimulus stimulusForMarkers;

        private readonly ConcurrentQueue<GazeRecord> gazeTrackingQueue = new();
        private readonly ConcurrentQueue<string> trackingDataQueue = new();
        // Unity-Objekte dürfen nur im Hauptthread gelesen werden. Deshalb werden
        // dort fertige Textzeilen erzeugt und erst danach vom Schreibthread gespeichert.
        private readonly object markerLock = new();
        private readonly AutoResetEvent writerWakeUp = new(false);

        private IEyeTracker eyeTracker;
        private GazeData currentGazeData;
        private Thread savingThread;
        private volatile bool writerThreadRunning;
        private volatile bool recording;
        private bool started;
        private bool stimulusEventsSubscribed;
        private string objectTrackingFile;
        private string gazeTrackingFile;
        private string pendingHeadMessage = string.Empty;
        private string pendingGazeMessage = string.Empty;
        private string backgroundWriterError;

        public event Action<GazeData> GazeDataAvailable;

        public ETProvider Provider => provider;
        public bool IsRecording => recording;
        public string ObjectTrackingFile => objectTrackingFile;
        public string GazeTrackingFile => gazeTrackingFile;
        public string OutputFolder => string.IsNullOrWhiteSpace(outputFolder)
            ? Path.Combine(Application.persistentDataPath, "Measurements")
            : outputFolder;

        private void Awake()
        {
            // Es darf nur eine Toolbox aktiv sein, weil Provider-Events sonst
            // doppelt verarbeitet und dieselben Samples mehrfach gespeichert würden.
            if (Instance != null && Instance != this)
            {
                Debug.Log("EyeTrackingToolbox-Instanz existiert bereits.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureMainCamera();
            CreateProvider();
        }

        private void OnEnable()
        {
            EyeTrackingEvent.OnDataAvailable += HandleProviderData;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (started)
            {
                SubscribeStimulusEvents();
                eyeTracker?.StartListening();
            }
        }

        private void Start()
        {
            started = true;
            EnsureMainCamera();
            SubscribeStimulusEvents();
            eyeTracker?.StartListening();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[calibrateKey].wasPressedThisFrame)
                {
                    Calibrate();
                }

                if (keyboard[recordingToggleKey].wasPressedThisFrame)
                {
                    if (recording)
                    {
                        StopRecording();
                    }
                    else
                    {
                        StartRecording($"gaze_{DateTime.Now:yyyyMMdd_HHmmss}");
                    }
                }
            }

            if (recording && recordHeadAndObjects)
            {
                QueueTrackingData();
            }

            string error = Interlocked.Exchange(ref backgroundWriterError, null);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError(error, this);
            }
        }

        private void OnDisable()
        {
            EyeTrackingEvent.OnDataAvailable -= HandleProviderData;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeStimulusEvents();
            eyeTracker?.StopListening();

            if (recording || writerThreadRunning)
            {
                StopRecording();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            writerWakeUp.Dispose();
        }

        public void SetProvider(ETProvider value)
        {
            if (Application.isPlaying && value != provider)
            {
                Debug.LogWarning(
                    "Eye-Tracking-Provider nur außerhalb des Play Mode wechseln.",
                    this);
                return;
            }

            provider = value;
        }

        public void SetMainCameraTransform(Transform value)
        {
            mainCameraTransform = value;
        }

        public void SetStimulusForMarkers(VrCheckerboardStimulus value)
        {
            UnsubscribeStimulusEvents();
            stimulusForMarkers = value;
            if (started)
            {
                SubscribeStimulusEvents();
            }
        }

        public void AddTrackedObject(
            GameObject trackedObject,
            TrackingOptions options = TrackingOptions.LocalTransform)
        {
            if (trackedObject == null)
            {
                return;
            }

            foreach (TrackedObjectOptions entry in trackedObjectList)
            {
                if (entry.gameObject == trackedObject)
                {
                    entry.trackingOptions = options;
                    return;
                }
            }

            trackedObjectList.Add(new TrackedObjectOptions
            {
                gameObject = trackedObject,
                trackingOptions = options
            });
        }

        public void SetOutputFolder(string folder)
        {
            outputFolder = folder ?? string.Empty;
        }

        public GazeData GetGazeData()
        {
            return currentGazeData;
        }

        public void Calibrate()
        {
            Debug.Log("Eye-Tracking-Kalibrierung wird gestartet.", this);
            eyeTracker?.Calibrate();
        }

        public void StartRecording(string outputFileName)
        {
            if (recording)
            {
                Debug.LogWarning("Eye Tracking zeichnet bereits auf.", this);
                return;
            }

            string folder = OutputFolder;
            Directory.CreateDirectory(folder);
            string baseName = NormalizeFileName(outputFileName);
            // Dateinamen werden vor dem Start eindeutig gewählt und die Kopfzeilen
            // synchron geschrieben. Erst danach dürfen Samples in die Queues gelangen.
            ResolveAvailableFileNames(folder, baseName);
            ClearQueues();
            WriteHeaders();

            recording = true;
            WriteMessage("RecordingStarted");
            StartBackgroundWriter();

            Debug.Log(
                $"Eye-Tracking-Aufzeichnung gestartet:\n{gazeTrackingFile}\n{objectTrackingFile}",
                this);
        }

        public void StopRecording()
        {
            if (!recording && !writerThreadRunning)
            {
                return;
            }

            if (recording)
            {
                WriteMessage("RecordingStopped");
                QueueFinalSamples();
            }

            recording = false;
            writerThreadRunning = false;
            writerWakeUp.Set();

            if (savingThread != null && savingThread.IsAlive &&
                !savingThread.Join(millisecondsTimeout: 3000))
            {
                Debug.LogWarning(
                    "Eye-Tracking-Schreibthread wurde nicht innerhalb von 3 Sekunden beendet.",
                    this);
            }

            savingThread = null;
            Debug.Log("Eye-Tracking-Aufzeichnung beendet.", this);
        }

        public void WriteMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (markerLock)
            {
                // Derselbe Marker wird einmal an die nächste Gaze-Zeile und einmal
                // an die nächste Head-Zeile gehängt. Mehrere Marker werden mit | verbunden.
                pendingHeadMessage = AppendMarker(pendingHeadMessage, message);
                pendingGazeMessage = AppendMarker(pendingGazeMessage, message);
            }
        }

        public static Ray TransformRayToWorld(Ray localRay, Transform reference)
        {
            if (reference == null)
            {
                return localRay;
            }

            return new Ray(
                reference.TransformPoint(localRay.origin),
                reference.TransformDirection(localRay.direction).normalized);
        }

        private void CreateProvider()
        {
            switch (provider)
            {
                case ETProvider.Dummy:
                    var dummy = GetComponent<DummyEyeTracker>();
                    if (dummy == null)
                    {
                        dummy = gameObject.AddComponent<DummyEyeTracker>();
                    }

                    eyeTracker = dummy;
                    break;

                case ETProvider.Varjo:
                    var varjo = GetComponent<VarjoEyeTracker>();
                    if (varjo == null)
                    {
                        varjo = gameObject.AddComponent<VarjoEyeTracker>();
                    }

                    varjo.Configure(calibrationMode, outputFilterType, outputFrequency);
                    eyeTracker = varjo;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            eyeTracker.Initialize();
            Debug.Log($"Eye-Tracking-Provider initialisiert: {provider}", this);
        }

        private void HandleProviderData(GazeData gazeData)
        {
            EnsureMainCamera();
            // Der Varjo-Provider liefert Strahlen im HMD-lokalen Raum. Für Fixation
            // benötigen wir zusätzlich die Weltstrahlen der aktuellen Main Camera.
            gazeData.unityTimestamp = Time.realtimeSinceStartupAsDouble;
            gazeData.leftRayWorld = TransformRayToWorld(
                gazeData.leftRayLocal,
                mainCameraTransform);
            gazeData.rightRayWorld = TransformRayToWorld(
                gazeData.rightRayLocal,
                mainCameraTransform);
            gazeData.combinedRayWorld = TransformRayToWorld(
                gazeData.combinedRayLocal,
                mainCameraTransform);

            currentGazeData = gazeData;
            if (recording)
            {
                gazeTrackingQueue.Enqueue(new GazeRecord(
                    gazeData,
                    TakePendingGazeMessage()));
            }

            GazeDataAvailable?.Invoke(gazeData);
        }

        private void QueueTrackingData()
        {
            // Transformdaten werden einmal pro Unity-Frame aufgenommen. Die Gaze-
            // Datei kann dagegen mehrere Varjo-Samples desselben Frames enthalten.
            var builder = new StringBuilder(768);
            AppendDouble(builder, Time.realtimeSinceStartupAsDouble);
            AppendLong(builder, currentGazeData.deviceTimestamp);

            foreach (TrackedObjectOptions trackedObject in trackedObjectList)
            {
                AppendTrackedObject(builder, trackedObject);
            }

            if (saveRaycastHitpoint)
            {
                AppendRaycast(builder);
            }

            AppendCsv(builder, TakePendingHeadMessage(), terminateRow: true);
            trackingDataQueue.Enqueue(builder.ToString());
        }

        private void QueueFinalSamples()
        {
            if (currentGazeData.frameNumber != 0)
            {
                gazeTrackingQueue.Enqueue(new GazeRecord(
                    currentGazeData,
                    TakePendingGazeMessage()));
            }

            if (recordHeadAndObjects)
            {
                QueueTrackingData();
            }
        }

        private static void AppendTrackedObject(
            StringBuilder builder,
            TrackedObjectOptions trackedObject)
        {
            if (trackedObject == null || trackedObject.gameObject == null)
            {
                builder.Append(",,,,,,,");
                return;
            }

            Transform trackedTransform = trackedObject.gameObject.transform;
            Vector3 position;
            Quaternion rotation;

            if (trackedObject.trackingOptions == TrackingOptions.LocalTransform)
            {
                position = trackedTransform.localPosition;
                rotation = trackedTransform.localRotation;
            }
            else
            {
                position = trackedTransform.position;
                rotation = trackedTransform.rotation;
            }

            AppendVector3(builder, position);
            AppendQuaternion(builder, rotation);
        }

        private void AppendRaycast(StringBuilder builder)
        {
            if (!currentGazeData.combinedValidity ||
                !Physics.Raycast(currentGazeData.combinedRayWorld, out RaycastHit hit))
            {
                AppendCsv(builder, "NA");
                builder.Append(",,,");
                return;
            }

            AppendCsv(builder, hit.transform.name);
            AppendVector3(builder, hit.point);
        }

        private void ResolveAvailableFileNames(string folder, string baseName)
        {
            string candidate = baseName;
            int counter = 0;

            // Weder Gaze- noch Head-Datei darf eine frühere Messung überschreiben.
            // Beide erhalten immer gemeinsam denselben nummerierten Basisnamen.
            do
            {
                objectTrackingFile = Path.Combine(folder, candidate + "_head.csv");
                gazeTrackingFile = Path.Combine(folder, candidate + "_gaze.csv");
                counter++;
                candidate = $"{baseName}_{counter:D2}";
            }
            while (File.Exists(objectTrackingFile) || File.Exists(gazeTrackingFile));
        }

        private void WriteHeaders()
        {
            File.WriteAllText(gazeTrackingFile, BuildGazeHeader() + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var headHeader = new StringBuilder(512);
            headHeader.Append("unity_timestamp_s,eye_timestamp_ns,");
            foreach (TrackedObjectOptions trackedObject in trackedObjectList)
            {
                AppendTrackedObjectHeader(headHeader, trackedObject);
            }

            if (saveRaycastHitpoint)
            {
                headHeader.Append("hit_object,hit_point_x,hit_point_y,hit_point_z,");
            }

            headHeader.Append("message");
            File.WriteAllText(objectTrackingFile, headHeader + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string BuildGazeHeader()
        {
            return "unity_timestamp_s,device_timestamp_ns,frame_number," +
                "tracking_status,combined_valid," +
                "left_tracking_status,left_valid,left_eye_openness,left_pupil_diameter_mm," +
                "left_origin_x,left_origin_y,left_origin_z,left_direction_x,left_direction_y,left_direction_z," +
                "right_tracking_status,right_valid,right_eye_openness,right_pupil_diameter_mm," +
                "right_origin_x,right_origin_y,right_origin_z,right_direction_x,right_direction_y,right_direction_z," +
                "combined_origin_x,combined_origin_y,combined_origin_z," +
                "combined_direction_x,combined_direction_y,combined_direction_z," +
                "focus_distance_m,ipd_mm,message";
        }

        private static void AppendTrackedObjectHeader(
            StringBuilder builder,
            TrackedObjectOptions trackedObject)
        {
            string objectName = trackedObject?.gameObject != null
                ? trackedObject.gameObject.name
                : "missing_object";
            string coordinateSpace = trackedObject != null &&
                trackedObject.trackingOptions == TrackingOptions.GlobalTransform
                ? "world"
                : "local";

            string prefix = SanitizeHeaderName(objectName) + "_" + coordinateSpace;
            builder.Append(prefix).Append("_position_x,");
            builder.Append(prefix).Append("_position_y,");
            builder.Append(prefix).Append("_position_z,");
            builder.Append(prefix).Append("_rotation_x,");
            builder.Append(prefix).Append("_rotation_y,");
            builder.Append(prefix).Append("_rotation_z,");
            builder.Append(prefix).Append("_rotation_w,");
        }

        private void StartBackgroundWriter()
        {
            if (writerThreadRunning)
            {
                return;
            }

            // Langsame Dateizugriffe sollen die Render- und Eye-Tracking-Schleife
            // nicht anhalten. Der Thread verarbeitet ausschließlich fertige Daten.
            writerThreadRunning = true;
            savingThread = new Thread(BackgroundWriterLoop)
            {
                IsBackground = true,
                Name = "EyeTracker-Writer"
            };
            savingThread.Start();
        }

        private void BackgroundWriterLoop()
        {
            // Spätestens alle 250 ms wird geschrieben. Beim Stoppen folgt ein
            // letzter Flush, damit die Abschlussmarker nicht in der Queue bleiben.
            while (writerThreadRunning)
            {
                FlushQueues();
                writerWakeUp.WaitOne(millisecondsTimeout: 250);
            }

            FlushQueues();
        }

        private void FlushQueues()
        {
            try
            {
                if (recordHeadAndObjects)
                {
                    using var headWriter = new StreamWriter(
                        objectTrackingFile,
                        append: true,
                        new UTF8Encoding(false));
                    while (trackingDataQueue.TryDequeue(out string line))
                    {
                        headWriter.WriteLine(line);
                    }
                }

                using var gazeWriter = new StreamWriter(
                    gazeTrackingFile,
                    append: true,
                    new UTF8Encoding(false));
                while (gazeTrackingQueue.TryDequeue(out GazeRecord record))
                {
                    gazeWriter.WriteLine(BuildGazeDataString(record));
                }
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(
                    ref backgroundWriterError,
                    "Fehler beim Schreiben der Eye-Tracking-Dateien: " +
                    exception.Message);
            }
        }

        private static string BuildGazeDataString(GazeRecord record)
        {
            GazeData data = record.Data;
            var builder = new StringBuilder(640);
            AppendDouble(builder, data.unityTimestamp);
            AppendLong(builder, data.deviceTimestamp);
            AppendLong(builder, data.frameNumber);
            AppendInt(builder, data.trackingStatus);
            AppendBool(builder, data.combinedValidity);

            AppendInt(builder, data.leftTrackingStatus);
            AppendBool(builder, data.leftValidity);
            AppendFloat(builder, data.leftEyeOpenness);
            AppendFloat(builder, data.leftPupilDiameter);
            AppendVector3(builder, data.leftRayLocal.origin);
            AppendVector3(builder, data.leftRayLocal.direction);

            AppendInt(builder, data.rightTrackingStatus);
            AppendBool(builder, data.rightValidity);
            AppendFloat(builder, data.rightEyeOpenness);
            AppendFloat(builder, data.rightPupilDiameter);
            AppendVector3(builder, data.rightRayLocal.origin);
            AppendVector3(builder, data.rightRayLocal.direction);

            AppendVector3(builder, data.combinedRayLocal.origin);
            AppendVector3(builder, data.combinedRayLocal.direction);
            AppendFloat(builder, data.gazeDistance);
            AppendFloat(builder, data.interPupillaryDistanceMillimeters);
            AppendCsv(builder, record.Message, terminateRow: true);
            return builder.ToString();
        }

        private void SubscribeStimulusEvents()
        {
            if (stimulusEventsSubscribed || stimulusForMarkers == null)
            {
                return;
            }

            stimulusForMarkers.StimulusPresented += OnStimulusPresented;
            stimulusForMarkers.StimulusHidden += OnStimulusHidden;
            stimulusForMarkers.ParametersChanged += OnStimulusParametersChanged;
            stimulusEventsSubscribed = true;
        }

        private void UnsubscribeStimulusEvents()
        {
            if (!stimulusEventsSubscribed || stimulusForMarkers == null)
            {
                stimulusEventsSubscribed = false;
                return;
            }

            stimulusForMarkers.StimulusPresented -= OnStimulusPresented;
            stimulusForMarkers.StimulusHidden -= OnStimulusHidden;
            stimulusForMarkers.ParametersChanged -= OnStimulusParametersChanged;
            stimulusEventsSubscribed = false;
        }

        private void OnStimulusPresented(CheckerboardStimulusSnapshot snapshot)
        {
            WriteMessage(BuildStimulusMarker("StimulusPresented", snapshot));
        }

        private void OnStimulusHidden(CheckerboardStimulusSnapshot snapshot)
        {
            WriteMessage(BuildStimulusMarker("StimulusHidden", snapshot));
        }

        private void OnStimulusParametersChanged(CheckerboardStimulusSnapshot snapshot)
        {
            WriteMessage(BuildStimulusMarker("StimulusChanged", snapshot));
        }

        private static string BuildStimulusMarker(
            string eventName,
            CheckerboardStimulusSnapshot snapshot)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0};k={1:F3};fov={2:F2};distance={3:F3};eye={4}",
                eventName,
                snapshot.merlitzK,
                snapshot.angularDiameterDegrees,
                snapshot.viewingDistanceMeters,
                snapshot.eyePresentation);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureMainCamera(forceRefresh: true);

            if (stimulusForMarkers == null)
            {
                stimulusForMarkers = FindAnyObjectByType<VrCheckerboardStimulus>();
                SubscribeStimulusEvents();
            }
        }

        private void EnsureMainCamera(bool forceRefresh = false)
        {
            if (!forceRefresh && mainCameraTransform != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCameraTransform = mainCamera.transform;
            }
        }

        private string TakePendingHeadMessage()
        {
            lock (markerLock)
            {
                string result = pendingHeadMessage;
                pendingHeadMessage = string.Empty;
                return result;
            }
        }

        private string TakePendingGazeMessage()
        {
            lock (markerLock)
            {
                string result = pendingGazeMessage;
                pendingGazeMessage = string.Empty;
                return result;
            }
        }

        private static string AppendMarker(string existing, string addition)
        {
            return string.IsNullOrEmpty(existing)
                ? addition
                : existing + "|" + addition;
        }

        private void ClearQueues()
        {
            while (gazeTrackingQueue.TryDequeue(out _))
            {
            }

            while (trackingDataQueue.TryDequeue(out _))
            {
            }

            lock (markerLock)
            {
                pendingHeadMessage = string.Empty;
                pendingGazeMessage = string.Empty;
            }
        }

        private static string NormalizeFileName(string requestedName)
        {
            string baseName = Path.GetFileNameWithoutExtension(requestedName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "gaze_recording";
            }

            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(invalidCharacter, '_');
            }

            return baseName;
        }

        private static string SanitizeHeaderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "object";
            }

            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }

            return builder.ToString();
        }

        private static void AppendVector3(StringBuilder builder, Vector3 value)
        {
            AppendFloat(builder, value.x);
            AppendFloat(builder, value.y);
            AppendFloat(builder, value.z);
        }

        private static void AppendQuaternion(StringBuilder builder, Quaternion value)
        {
            AppendFloat(builder, value.x);
            AppendFloat(builder, value.y);
            AppendFloat(builder, value.z);
            AppendFloat(builder, value.w);
        }

        private static void AppendFloat(StringBuilder builder, float value)
        {
            builder.Append(value.ToString("F10", CultureInfo.InvariantCulture)).Append(',');
        }

        private static void AppendDouble(StringBuilder builder, double value)
        {
            builder.Append(value.ToString("F10", CultureInfo.InvariantCulture)).Append(',');
        }

        private static void AppendLong(StringBuilder builder, long value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append(',');
        }

        private static void AppendInt(StringBuilder builder, int value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append(',');
        }

        private static void AppendBool(StringBuilder builder, bool value)
        {
            builder.Append(value ? '1' : '0').Append(',');
        }

        private static void AppendCsv(
            StringBuilder builder,
            string value,
            bool terminateRow = false)
        {
            string safeValue = value ?? string.Empty;
            bool quote = safeValue.IndexOf(',') >= 0 ||
                safeValue.IndexOf('"') >= 0 ||
                safeValue.IndexOf('\n') >= 0 ||
                safeValue.IndexOf('\r') >= 0;

            if (quote)
            {
                builder.Append('"').Append(safeValue.Replace("\"", "\"\"")).Append('"');
            }
            else
            {
                builder.Append(safeValue);
            }

            if (!terminateRow)
            {
                builder.Append(',');
            }
        }
    }
}
