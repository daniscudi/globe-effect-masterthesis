using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Simulierter Provider fuer Editor-Tests ohne Headset. Der Blick folgt
    /// der Mausposition in der Game View oder, ohne Maus, der Kameramitte.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DummyEyeTracker : MonoBehaviour, IEyeTracker
    {
        [SerializeField, Min(0.001f)]
        private float samplingIntervalSeconds = 0.008f;

        [SerializeField, Range(0.04f, 0.09f)]
        private float simulatedIpdMeters = 0.064f;

        [SerializeField, Min(0f)]
        private float simulatedGazeDistanceMeters = 2f;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private GazeData currentGazeData;
        private Coroutine samplingCoroutine;
        private long frameNumber;

        public void Initialize()
        {
            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
            }
        }

        public void Calibrate()
        {
            Debug.Log("Dummy Eye Tracker: keine Kalibrierung erforderlich.", this);
        }

        public GazeData GetGazeData()
        {
            return currentGazeData;
        }

        public void StartListening()
        {
            Initialize();
            if (samplingCoroutine == null)
            {
                samplingCoroutine = StartCoroutine(SampleContinuously());
            }
        }

        public void StopListening()
        {
            if (samplingCoroutine == null)
            {
                return;
            }

            StopCoroutine(samplingCoroutine);
            samplingCoroutine = null;
        }

        private IEnumerator SampleContinuously()
        {
            var wait = new WaitForSecondsRealtime(samplingIntervalSeconds);
            while (true)
            {
                if (TryCreateSample(out currentGazeData))
                {
                    EyeTrackingEvent.TriggerEvent(currentGazeData);
                }

                yield return wait;
            }
        }

        private bool TryCreateSample(out GazeData sample)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                sample = default;
                return false;
            }

            Ray worldRay = new Ray(camera.transform.position, camera.transform.forward);
            if (Mouse.current != null)
            {
                worldRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            }

            Vector3 localDirection = camera.transform
                .InverseTransformDirection(worldRay.direction)
                .normalized;
            float halfIpd = 0.5f * simulatedIpdMeters;

            sample = new GazeData
            {
                frameNumber = ++frameNumber,
                deviceTimestamp = (long)(
                    stopwatch.ElapsedTicks *
                    (1_000_000_000.0 / Stopwatch.Frequency)),
                leftRayLocal = new Ray(new Vector3(-halfIpd, 0f, 0f), localDirection),
                rightRayLocal = new Ray(new Vector3(halfIpd, 0f, 0f), localDirection),
                combinedRayLocal = new Ray(Vector3.zero, localDirection),
                gazeDistance = simulatedGazeDistanceMeters,
                interPupillaryDistanceMillimeters = simulatedIpdMeters * 1000f,
                leftPupilDiameter = 4f,
                rightPupilDiameter = 4f,
                leftEyeOpenness = 1f,
                rightEyeOpenness = 1f,
                combinedValidity = true,
                leftValidity = true,
                rightValidity = true,
                trackingStatus = 2,
                leftTrackingStatus = 3,
                rightTrackingStatus = 3
            };
            return true;
        }

        private void OnValidate()
        {
            samplingIntervalSeconds = Mathf.Max(0.001f, samplingIntervalSeconds);
            simulatedGazeDistanceMeters = Mathf.Max(0f, simulatedGazeDistanceMeters);
        }
    }
}
