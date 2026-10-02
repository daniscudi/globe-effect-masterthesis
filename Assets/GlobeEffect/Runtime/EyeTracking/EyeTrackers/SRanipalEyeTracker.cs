using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using UnityEngine;
using ViveSR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// SRanipal-Implementierung (HTC Vive Pro Eye) der im Lab verwendeten
    /// IEyeTracker-Schnittstelle. Die Samples kommen über den Callback der
    /// Eye-v2-API mit etwa 120 Hz aus einem Thread der SRanipal-DLL. Sie werden dort
    /// nur umgerechnet und gepuffert; Update gibt in jedem Unity-Frame alle
    /// gepufferten Samples auf dem Main-Thread weiter. So geht wie bei
    /// GetGazeList im VarjoEyeTracker kein Sample wegen einer niedrigeren
    /// Render-Framerate verloren.
    ///
    /// Das SDK-Prefab "SRanipal Eye Framework" wird nicht gebraucht. Dessen
    /// Initialisierung blockiert den Main-Thread; hier läuft sie im Hintergrund.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SRanipalEyeTracker : MonoBehaviour, IEyeTracker
    {
        private enum RuntimeState
        {
            NotStarted,
            Initializing,
            Working,
            Failed
        }

        // Etwa 17 Sekunden bei 120 Hz. Mehr sammelt sich nur an, wenn Unity so
        // lange gar kein Update ausführt; dann werden die ältesten Samples verworfen.
        private const int MaximumBufferedSamples = 2048;

        private const int CalibrationIdle = 0;
        private const int CalibrationRunning = 1;
        private const int CalibrationFinished = 2;

        // Der Callback wird aus nativem Code aufgerufen. Delegate und Funktionszeiger
        // bleiben statisch am Leben, damit der Garbage Collector sie nicht
        // einsammelt, solange die DLL sie noch kennt.
        private static readonly SRanipal_Eye_v2.CallbackBasic eyeDataCallback = OnEyeData;
        private static readonly IntPtr eyeDataCallbackPointer =
            Marshal.GetFunctionPointerForDelegate(eyeDataCallback);

        private static readonly ConcurrentQueue<GazeData> sampleQueue = new();
        private static int bufferedSampleCount;
        private static int droppedSampleCount;
        private static volatile bool acceptSamples;
        private static bool callbackRegistered;

        private bool initialized;
        private bool listening;
        private bool failureReported;
        private bool overflowReported;
        private volatile RuntimeState runtimeState = RuntimeState.NotStarted;
        private volatile string failureMessage = string.Empty;
        private int calibrationState = CalibrationIdle;
        private volatile int calibrationResult;
        private GazeData currentGazeData = SRanipalGazeConversion.CreateInvalidSample();

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            BeginRuntimeInitialization();
        }

        public void Calibrate()
        {
            if (runtimeState == RuntimeState.Initializing)
            {
                Debug.LogWarning(
                    "SRanipal-Blickkalibrierung noch nicht möglich: Eye Tracking wird " +
                    "gerade initialisiert.",
                    this);
                return;
            }

            if (runtimeState != RuntimeState.Working)
            {
                // Wurde die SRanipal-Runtime erst nach dem Start des Play Mode
                // gestartet, lässt sich die Verbindung so ohne Neustart nachholen.
                // Läuft sie weiterhin nicht, meldet der neue Versuch das selbst.
                BeginRuntimeInitialization();
                if (runtimeState == RuntimeState.Initializing)
                {
                    Debug.LogWarning(
                        "SRanipal Eye Tracking war nicht bereit und wird neu " +
                        "initialisiert. Danach C erneut drücken.",
                        this);
                }

                return;
            }

            if (Interlocked.CompareExchange(
                ref calibrationState, CalibrationRunning, CalibrationIdle) != CalibrationIdle)
            {
                Debug.LogWarning("SRanipal-Blickkalibrierung läuft bereits.", this);
                return;
            }

            // LaunchEyeCalibration kehrt erst zurück, wenn die Kalibrierung im
            // Headset beendet ist. Auf dem Main-Thread würde Unity so lange stehen.
            var thread = new Thread(RunCalibration)
            {
                IsBackground = true,
                Name = "SRanipal-Calibration"
            };
            thread.Start();
        }

        public GazeData GetGazeData()
        {
            return currentGazeData;
        }

        public void StartListening()
        {
            if (!initialized)
            {
                Initialize();
            }

            if (!listening)
            {
                // Reste aus einer früheren Aufnahme sollen nicht als neue Samples
                // erscheinen.
                ClearBufferedSamples();
            }

            listening = true;
        }

        public void StopListening()
        {
            listening = false;
            UnregisterCallback();
        }

        private void Update()
        {
            if (!listening)
            {
                return;
            }

            ReportCalibrationResult();

            switch (runtimeState)
            {
                case RuntimeState.Working:
                    if (!callbackRegistered)
                    {
                        RegisterCallback();
                    }

                    break;

                case RuntimeState.Failed:
                    ReportFailureOnce();
                    return;

                default:
                    return;
            }

            // Alle seit dem letzten Frame eingetroffenen Samples weitergeben, nicht
            // nur das neueste. Die World-Rays ergänzt danach die Toolbox.
            while (sampleQueue.TryDequeue(out GazeData sample))
            {
                Interlocked.Decrement(ref bufferedSampleCount);
                currentGazeData = sample;
                EyeTrackingEvent.TriggerEvent(currentGazeData);
            }

            ReportOverflowOnce();
        }

        private void OnDisable()
        {
            // Solange der Callback registriert ist, ruft die DLL verwalteten Code
            // auf. Vor dem Ende des Play Mode muss er deshalb sicher abgemeldet sein.
            UnregisterCallback();
        }

        private void OnDestroy()
        {
            UnregisterCallback();
        }

        private void BeginRuntimeInitialization()
        {
            if (runtimeState == RuntimeState.Initializing ||
                runtimeState == RuntimeState.Working)
            {
                return;
            }

            failureReported = false;

            // Ohne laufende Runtime wird die SRanipal-DLL gar nicht erst angefasst.
            // So bleibt der Start auf einem Rechner ohne Vive Pro Eye schnell und
            // sicher, und es entsteht genau eine Warnung.
            if (!EyeTrackerProviderResolver.IsSRanipalRuntimeRunning())
            {
                failureMessage =
                    "Die SRanipal-Runtime (sr_runtime.exe) läuft nicht. VIVE Console " +
                    "bzw. SRanipal starten und danach C drücken oder den Play Mode " +
                    "neu starten.";
                runtimeState = RuntimeState.Failed;
                ReportFailureOnce();
                return;
            }

            runtimeState = RuntimeState.Initializing;
            var thread = new Thread(InitializeRuntime)
            {
                IsBackground = true,
                Name = "SRanipal-Init"
            };
            thread.Start();
        }

        // Läuft im Hintergrund-Thread: hier keine Unity-API verwenden.
        private void InitializeRuntime()
        {
            try
            {
                // SRanipal_API.Initial kann mehrere Sekunden dauern. Das SDK ruft
                // es im Framework-Prefab auf dem Main-Thread auf; hier nicht.
                Error result = SRanipal_API.Initial(
                    SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2,
                    IntPtr.Zero);
                if (result == Error.WORK)
                {
                    runtimeState = RuntimeState.Working;
                    return;
                }

                failureMessage =
                    $"SRanipal_API.Initial (Eye v2) meldet {result}. Läuft SteamVR und " +
                    "ist die Vive Pro Eye verbunden?";
            }
            catch (Exception exception)
            {
                failureMessage = $"{exception.GetType().Name}: {exception.Message}";
            }

            runtimeState = RuntimeState.Failed;
        }

        // Läuft im Hintergrund-Thread: hier keine Unity-API verwenden.
        private void RunCalibration()
        {
            int result;
            try
            {
                result = SRanipal_Eye_API.LaunchEyeCalibration(IntPtr.Zero);
            }
            catch (Exception)
            {
                result = (int)Error.FAILED;
            }

            calibrationResult = result;
            Volatile.Write(ref calibrationState, CalibrationFinished);
        }

        private void ReportCalibrationResult()
        {
            if (Interlocked.CompareExchange(
                ref calibrationState, CalibrationIdle, CalibrationFinished) != CalibrationFinished)
            {
                return;
            }

            var result = (Error)calibrationResult;
            if (result == Error.WORK)
            {
                Debug.Log("SRanipal-Blickkalibrierung abgeschlossen.", this);
            }
            else
            {
                Debug.LogWarning(
                    $"SRanipal-Blickkalibrierung nicht erfolgreich beendet: {result}.",
                    this);
            }
        }

        private void RegisterCallback()
        {
            try
            {
                acceptSamples = true;
                int result = SRanipal_Eye_API.RegisterEyeDataCallback_v2(
                    eyeDataCallbackPointer);
                if (result == (int)Error.WORK)
                {
                    callbackRegistered = true;
                    Debug.Log(
                        "SRanipal Eye Tracking bereit (Eye v2, Callback registriert).",
                        this);
                    return;
                }

                failureMessage = $"RegisterEyeDataCallback_v2 meldet {(Error)result}.";
            }
            catch (Exception exception)
            {
                failureMessage = $"{exception.GetType().Name}: {exception.Message}";
            }

            acceptSamples = false;
            runtimeState = RuntimeState.Failed;
            ReportFailureOnce();
        }

        private static void UnregisterCallback()
        {
            acceptSamples = false;
            if (!callbackRegistered)
            {
                return;
            }

            callbackRegistered = false;
            try
            {
                SRanipal_Eye_API.UnregisterEyeDataCallback_v2(eyeDataCallbackPointer);
            }
            catch (Exception)
            {
                // Beim Abmelden lässt sich ein Fehler der DLL nicht mehr beheben.
            }

            // SRanipal_API.Release wird wie im SDK (SRanipal_Eye_Framework.OnDestroy)
            // bewusst nicht aufgerufen: Der Aufruf kann den Editor beim Beenden des
            // Play Mode blockieren. Ein erneutes Initial ist ohne Release möglich.
        }

        // Wird von der SRanipal-DLL in deren eigenem Thread aufgerufen. Hier darf
        // weder die Unity-API verwendet noch eine Exception nach außen gelangen.
        [MonoPInvokeCallback(typeof(SRanipal_Eye_v2.CallbackBasic))]
        private static void OnEyeData(ref EyeData_v2 eyeData)
        {
            try
            {
                if (!acceptSamples)
                {
                    return;
                }

                GazeData sample = SRanipalGazeConversion.Convert(eyeData);
                if (Interlocked.Increment(ref bufferedSampleCount) > MaximumBufferedSamples)
                {
                    if (sampleQueue.TryDequeue(out _))
                    {
                        Interlocked.Decrement(ref bufferedSampleCount);
                    }

                    Interlocked.Increment(ref droppedSampleCount);
                }

                sampleQueue.Enqueue(sample);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref droppedSampleCount);
            }
        }

        private static void ClearBufferedSamples()
        {
            while (sampleQueue.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref bufferedSampleCount, 0);
            Interlocked.Exchange(ref droppedSampleCount, 0);
        }

        private void ReportFailureOnce()
        {
            if (failureReported)
            {
                return;
            }

            failureReported = true;
            Debug.LogWarning(
                $"SRanipal Eye Tracking nicht verfügbar. {failureMessage} " +
                "Der Stimulus läuft weiter; es kommen keine gültigen Blickdaten.",
                this);
        }

        private void ReportOverflowOnce()
        {
            if (overflowReported || Volatile.Read(ref droppedSampleCount) == 0)
            {
                return;
            }

            overflowReported = true;
            Debug.LogWarning(
                "SRanipal Eye Tracking: Der Sample-Puffer ist übergelaufen oder ein " +
                "Sample ließ sich nicht umrechnen. Einzelne Samples fehlen in der " +
                "Aufzeichnung.",
                this);
        }
    }
}
