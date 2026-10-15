using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Separate Foto-Vorschau, keine Messung. Das Panorama liegt optisch in der Ferne.
    /// Simuliert: feste Ausgangsblickrichtung. Aktiv: freie Kopfbewegung im festen Panorama.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class NaturalScenePanoramaPreview : MonoBehaviour
    {
        [Header("Photo And Camera")]
        public Texture2D panorama;
        public Camera observerCamera;
        [Range(-180, 180)] public float panoramaYawDegrees = 90;

        [Header("Perspective Photo (Optional; Not A 360 Panorama)")]
        public bool usePerspectivePhoto;
        [Tooltip("Estimated horizontal image angle. Must describe the original uncropped photo, not the headset FOV.")]
        [Range(5, 120)] public float photoHorizontalFieldOfView = 39.41037f;
        [Tooltip("Rotate the source photo relative to the starting head direction.")]
        public Vector2 photoRotationDegrees = new Vector2(0, -7);
        [Tooltip("Conservative warning if the visible aperture reaches the photo edge. Does not constrain the head.")]
        public bool showPhotoBoundaryWarning = true;

        [Header("Binoculars (Change Live)")]
        [Range(0, 2)] public float distortionK = 1;
        [Range(1, 20)] public float magnification = 1;
        [Range(0.1f, 4)] public float contentZoom = 1;
        [Range(5, 120)] public float fieldOfViewDegrees = 60;
        [Range(0, 10)] public float edgeSoftnessDegrees = 1;
        public Color backgroundColor = Color.black;

        [Header("Movement (No Trials)")]
        public RandomDotMotionMode motionMode = RandomDotMotionMode.SimulatedYaw;
        public RandomDotSweepAxis sweepAxis = RandomDotSweepAxis.Horizontal;
        public RandomDotSweepDirection sweepDirection = RandomDotSweepDirection.RightFirst;
        [Tooltip("Speed in the image center (degrees per second).")]
        [Range(0, 60)] public float imageCenterSpeed = 12;
        [Tooltip("How far the image moves to each side, in degrees.")]
        [Range(1, 40)] public float imageSweepAmplitudeDegrees = 12;
        public bool paused;
        public bool showFixationTarget = true;
        [Range(0.1f, 2)] public float fixationSizeDegrees = 0.45f;
        public Color fixationColor = Color.red;
        public bool showDesktopInstructions = true;

        private GameObject surface;
        private Mesh mesh;
        private Material material;
        private MaterialPropertyBlock properties;
        private bool ownsCamera;
        private Quaternion anchor;
        private Quaternion photoAnchor;
        private float elapsed;
        private float previousM, previousZoom;
        private RandomDotMotionMode previousMode;

        public float CurrentSweepDegrees { get; private set; }
        public bool PhotoEdgeVisible { get; private set; }
        public float PhotoVerticalFieldOfView => panorama
            ? 2 * Mathf.Atan(Mathf.Tan(photoHorizontalFieldOfView * Mathf.Deg2Rad * 0.5f)
                * panorama.height / panorama.width) * Mathf.Rad2Deg : 0;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (!observerCamera) observerCamera = Camera.main;
            if (!observerCamera)
            {
                var cameraObject = new GameObject("Natural Scene Camera", typeof(Camera));
                cameraObject.transform.SetParent(transform, false);
                observerCamera = cameraObject.GetComponent<Camera>();
                observerCamera.tag = "MainCamera";
                observerCamera.fieldOfView = 90;
                observerCamera.nearClipPlane = 0.01f;
                ownsCamera = true;
            }
            var shader = Resources.Load<Shader>("GlobeEffectNaturalPanorama");
            if (!shader || !panorama)
            {
                Debug.LogError("Panorama oder Natural-Panorama-Shader fehlt.", this);
                enabled = false;
                return;
            }
            material = new Material(shader);
            material.mainTexture = panorama;
            material.enableInstancing = true;
            mesh = new Mesh { name = "Direction-only panorama surface" };
            mesh.vertices = new[] {
                new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
            mesh.uv = new[] {
                new Vector2(-1, -1), new Vector2(1, -1),
                new Vector2(1, 1), new Vector2(-1, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
            surface = new GameObject("Natural Panorama Surface", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(transform, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            surface.GetComponent<MeshRenderer>().sharedMaterial = material;
            surface.GetComponent<MeshRenderer>().sortingOrder = -10;
            Restart();
            CenterPhoto();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += Refresh;
            if (surface) surface.SetActive(true);
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= Refresh;
            if (surface) surface.SetActive(false);
        }

        public void Restart()
        {
            UpdateHeadPose();
            anchor = observerCamera ? observerCamera.transform.rotation : Quaternion.identity;
            elapsed = 0;
            previousM = magnification;
            previousZoom = contentZoom;
            previousMode = motionMode;
        }

        [ContextMenu("Center Photo On Current Head Direction")]
        public void CenterPhoto()
        {
            UpdateHeadPose();
            photoAnchor = observerCamera ? observerCamera.transform.rotation : Quaternion.identity;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) paused = !paused;
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    Restart();
                    CenterPhoto();
                }
                if (ownsCamera && motionMode == RandomDotMotionMode.HeadTracked && !XRSettings.isDeviceActive)
                {
                    float yaw = (keyboard.rightArrowKey.isPressed ? 1 : 0) -
                        (keyboard.leftArrowKey.isPressed ? 1 : 0);
                    float pitch = (keyboard.downArrowKey.isPressed ? 1 : 0) -
                        (keyboard.upArrowKey.isPressed ? 1 : 0);
                    observerCamera.transform.Rotate(pitch * 20 * Time.deltaTime,
                        yaw * 20 * Time.deltaTime, 0, Space.Self);
                }
            }
            if (previousM != magnification || previousZoom != contentZoom || previousMode != motionMode) Restart();
            if (!paused) elapsed += Time.deltaTime;
        }

        private void UpdateHeadPose()
        {
            if (!ownsCamera || !XRSettings.isDeviceActive) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation))
                observerCamera.transform.localRotation = rotation;
            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position))
                observerCamera.transform.localPosition = position;
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (!surface || !observerCamera) return;
            UpdateHeadPose();
            float m = Mathf.Max(1, magnification);
            float zoom = Mathf.Max(0.1f, contentZoom);
            float k = Mathf.Clamp(distortionK, (float)MerlitzBinocularReferenceMath.MinimumDistortionK,
                (float)MerlitzBinocularReferenceMath.MaximumDistortionK);
            float fov = Mathf.Min(Mathf.Clamp(fieldOfViewDegrees, 5, 120),
                (float)NaturalScenePanoramaMath.MaximumFieldOfViewDegrees(k, zoom) - 0.01f);
            CurrentSweepDegrees = motionMode == RandomDotMotionMode.SimulatedYaw
                ? RandomDotSimulatedSweep.EvaluateBackAndForthDegrees(elapsed,
                    imageSweepAmplitudeDegrees / (m * zoom),
                    imageCenterSpeed / (m * zoom), sweepDirection) : 0;
            Quaternion basis = motionMode == RandomDotMotionMode.SimulatedYaw
                ? anchor : observerCamera.transform.rotation;
            observerCamera.clearFlags = CameraClearFlags.SolidColor;
            observerCamera.backgroundColor = backgroundColor;
            properties.SetFloat("_HalfAngle", fov * Mathf.Deg2Rad * 0.5f);
            properties.SetFloat("_Softness", Mathf.Max(0, edgeSoftnessDegrees) * Mathf.Deg2Rad);
            properties.SetFloat("_K", k);
            properties.SetFloat("_M", m);
            properties.SetFloat("_Zoom", zoom);
            properties.SetFloat("_Sweep", CurrentSweepDegrees * Mathf.Deg2Rad);
            properties.SetFloat("_Axis", (float)sweepAxis);
            properties.SetFloat("_Yaw", panoramaYawDegrees * Mathf.Deg2Rad);
            properties.SetVector("_HeadForward", basis * Vector3.forward);
            properties.SetVector("_HeadRight", basis * Vector3.right);
            properties.SetVector("_HeadUp", basis * Vector3.up);
            properties.SetFloat("_PerspectivePhoto", usePerspectivePhoto ? 1 : 0);
            Vector2 photoHalfSize = new Vector2(
                Mathf.Tan(Mathf.Clamp(photoHorizontalFieldOfView, 5, 120) * Mathf.Deg2Rad * 0.5f), 0);
            photoHalfSize.y = photoHalfSize.x * (panorama ? (float)panorama.height / panorama.width : 1);
            Quaternion photoRotation = photoAnchor
                * Quaternion.Euler(photoRotationDegrees.y, photoRotationDegrees.x, 0);
            properties.SetMatrix("_WorldToPhoto", Matrix4x4.Rotate(Quaternion.Inverse(photoRotation)));
            properties.SetVector("_PhotoHalfSize", photoHalfSize);
            PhotoEdgeVisible = usePerspectivePhoto
                && ApertureReachesPhotoEdge(basis, photoRotation, fov, m, k, zoom, photoHalfSize);
            properties.SetFloat("_PhotoWarning", showPhotoBoundaryWarning && PhotoEdgeVisible ? 1 : 0);
            properties.SetFloat("_FixationEnabled", showFixationTarget ? 1 : 0);
            properties.SetFloat("_FixationSize", fixationSizeDegrees * Mathf.Deg2Rad);
            properties.SetColor("_FixationColor", fixationColor);
            material.mainTexture = panorama;
            surface.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        }

        // Same inverse instrument mapping as in the shader. Test the boundary and center.
        // A warning is diagnostic only: no head clamping or automatic image re-centering.
        private bool ApertureReachesPhotoEdge(Quaternion basis, Quaternion photoRotation,
            float fov, float m, float k, float zoom, Vector2 halfSize)
        {
            Quaternion worldToPhoto = Quaternion.Inverse(photoRotation);
            float radius = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
            for (int index = -1; index < 64; index++)
            {
                float phase = index * 2 * Mathf.PI / 64;
                double u = index < 0 ? 0 : radius * Mathf.Cos(phase);
                double v = index < 0 ? 0 : radius * Mathf.Sin(phase);
                if (!NaturalScenePanoramaMath.TryObjectCoordinates(u, v, m, k, zoom,
                    out double x, out double y)) return true;
                Vector3 ray = new Vector3((float)x, (float)y, 1);
                float c = Mathf.Cos(CurrentSweepDegrees * Mathf.Deg2Rad);
                float s = Mathf.Sin(CurrentSweepDegrees * Mathf.Deg2Rad);
                ray = sweepAxis == RandomDotSweepAxis.Horizontal
                    ? new Vector3(c * ray.x + s * ray.z, ray.y, -s * ray.x + c * ray.z)
                    : new Vector3(ray.x, c * ray.y + s * ray.z, -s * ray.y + c * ray.z);
                ray = worldToPhoto * (basis * ray);
                if (!NaturalScenePhotoMath.TryPhotoUv(ray, halfSize, out _)) return true;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!showDesktopInstructions) return;
            GUI.Box(new Rect(10, 10, 660, 125),
                (usePerspectivePhoto ? "Puno – 10x photo preview, no measurement\n"
                    : "Field 01 – natürliche Szene, keine Messung\n") +
                "Leertaste: Pause | R: Neustart | Einstellungen live im Inspector\n" +
                "Aktiv: freie Kopfbewegung; am Desktop Pfeiltasten.\n" +
                (usePerspectivePhoto
                    ? $"Foto: {photoHorizontalFieldOfView:0.0}° × {PhotoVerticalFieldOfView:0.0}°. " +
                      "R = Ausschnitt vor dem Kopf neu zentrieren.\n" +
                      (PhotoEdgeVisible ? "PHOTO EDGE: Kopf zurückdrehen oder R drücken."
                          : "Begrenzter Fotoausschnitt, kein 3D-Modell; Bildwinkel aus EXIF geschätzt.")
                    : "360°-Foto, kein 3D-Modell. Hoher Zoom kann unscharf werden."));
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= Refresh;
            if (surface) Destroy(surface);
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
            if (ownsCamera && observerCamera) Destroy(observerCamera.gameObject);
        }
    }
}
