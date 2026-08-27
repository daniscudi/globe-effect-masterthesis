using System;
using UnityEngine;
using UnityEngine.XR;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Erzeugt und steuert einen kreisrunden Merlitz-Checkerboard-Stimulus.
    /// Das Muster wird im Shader analytisch berechnet und bleibt daher auch
    /// bei Laufzeit-Aenderungen von k, Abstand und FOV scharf.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VrCheckerboardStimulus : MonoBehaviour
    {
        private const string ShaderResourceName = "GlobeEffectMerlitzCheckerboard";
        private const string ShaderFallbackName = "GlobeEffect/Merlitz Checkerboard";
        private const float MinimumDistanceMeters = 0.05f;
        private const int MaximumInitialPoseWaitFrames = 120;

        [Header("Geometrie")]
        [SerializeField]
        [Tooltip("XR-Kopf-/Center-Eye-Transform. Bei leerem Feld bleibt das Objekt an seiner aktuellen Position.")]
        private Transform observer;

        [SerializeField, Range(1f, 170f)]
        [Tooltip("Scheinbarer Winkeldurchmesser des kreisrunden Stimulus.")]
        private float angularDiameterDegrees = 70f;

        [SerializeField, Min(MinimumDistanceMeters)]
        [Tooltip("Abstand vom Observer zum Mittelpunkt der ebenen Stimulusflaeche in Metern.")]
        private float viewingDistanceMeters = 1f;

        [SerializeField]
        [Tooltip("Platziert den Stimulus in jedem Frame erneut vor dem Observer. Fuer einen weltfesten Versuch deaktiviert lassen.")]
        private bool followObserverEveryFrame;

        [SerializeField]
        [Tooltip("Wartet im Play Mode bis zum ersten LateUpdate, damit die initiale HMD-Pose bereits vorliegt. Danach bleibt der Stimulus weltfest.")]
        private bool placeOnFirstTrackedPose = true;

        [Header("Merlitz-Stimulus")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Verzeichnungsparameter k: 1 = Tangensbedingung/gerades Gitter, 0.5 = Kreisbedingung, 0 = Winkelbedingung.")]
        private float merlitzK = 0.7f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Paraxiale Instrumentvergroesserung m. Die Referenzkonfiguration des Papers verwendet m = 10.")]
        private float magnification = 10f;

        [SerializeField, Range(2, 80)]
        [Tooltip("Anzahl der Schachfelder ueber den Durchmesser des unverzerrten Ausgangsgitters.")]
        private int checksAcrossDiameter = 16;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        [Header("Darstellung und Fixation")]
        [SerializeField]
        [Tooltip("Beidseitige oder monokulare Darbietung. Die Auswahl erfolgt im XR-kompatiblen Shader.")]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [SerializeField]
        [Tooltip("Zeigt ein kleines zentrales Fixationskreuz.")]
        private bool showFixationTarget = true;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Gesamte Winkelgroesse des Fixationskreuzes in Grad.")]
        private float fixationTargetSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [SerializeField]
        [Tooltip("Ist der Stimulus beim Start einer Play-Mode-Sitzung sichtbar?")]
        private bool visibleAtStart = true;

        [Header("Technik")]
        [SerializeField, Range(32, 256)]
        [Tooltip("Segmentzahl des kreisrunden Traegermeshes.")]
        private int diskSegments = 128;

        [SerializeField]
        [Tooltip("Optionales Material mit dem Shader 'GlobeEffect/Merlitz Checkerboard'.")]
        private Material materialOverride;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh ownedMesh;
        private Material ownedMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible = true;
        private bool initialPlacementPending;
        private int initialPlacementWaitFrames;

        /// <summary>Wird nach Show() mit dem aktuellen Parametersatz ausgeloest.</summary>
        public event Action<CheckerboardStimulusSnapshot> StimulusPresented;

        /// <summary>Wird nach Hide() mit dem aktuellen Parametersatz ausgeloest.</summary>
        public event Action<CheckerboardStimulusSnapshot> StimulusHidden;

        /// <summary>Wird nach einer Aenderung ueber die oeffentliche API ausgeloest.</summary>
        public event Action<CheckerboardStimulusSnapshot> ParametersChanged;

        public Transform Observer
        {
            get => observer;
            set
            {
                observer = value;
                PlaceInFrontOfObserver();
            }
        }

        public float AngularDiameterDegrees => angularDiameterDegrees;
        public float ViewingDistanceMeters => viewingDistanceMeters;
        public float PhysicalDiameterMeters => (float)AngularGeometry.PhysicalDiameter(
            viewingDistanceMeters,
            angularDiameterDegrees);
        public float MerlitzK => merlitzK;
        public float Magnification => magnification;
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public bool IsVisible => isVisible;

        private void Reset()
        {
            Camera mainCamera = Camera.main;
            observer = mainCamera != null ? mainCamera.transform : null;
        }

        private void OnEnable()
        {
            ValidateSerializedFields();
            EnsureResources();
            isVisible = Application.isPlaying ? visibleAtStart : true;

            // Der Tracked Pose Driver uebernimmt die HMD-Pose erst waehrend des
            // ersten Frames. Eine sofortige Platzierung in OnEnable wuerde den
            // Stimulus deshalb haeufig relativ zur Editor-Pose (0, 0, 0)
            // positionieren, bevor die reale Kopfpose bekannt ist.
            initialPlacementPending = Application.isPlaying &&
                placeOnFirstTrackedPose && observer != null;
            initialPlacementWaitFrames = 0;
            ApplyAll(placeAtObserver: observer != null && !initialPlacementPending);
        }

        private void OnValidate()
        {
            ValidateSerializedFields();

            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureResources();
            RebuildDiskIfRequired();
            ApplyAll(placeAtObserver: false);
        }

        private void LateUpdate()
        {
            if (observer == null)
            {
                return;
            }

            bool initialPoseReady = initialPlacementPending &&
                (HasTrackedCenterEyePose() ||
                 initialPlacementWaitFrames >= MaximumInitialPoseWaitFrames);

            if (followObserverEveryFrame || initialPoseReady)
            {
                ApplyTransform(placeAtObserver: true);
                initialPlacementPending = false;
            }

            if (initialPlacementPending)
            {
                initialPlacementWaitFrames++;
            }

            // Im Varjo-Multi-Pass-Modus werden Context- und Focus-Ansicht in
            // getrennten Durchlaeufen gerendert. Die aktuelle Center-Eye-Pose
            // dient dem Shader als robuste Links-/Rechts-Referenz.
            ApplyObserverMaterialProperties();
        }

        private void OnDestroy()
        {
            DestroyOwnedObject(ownedMesh);
            DestroyOwnedObject(ownedMaterial);
            ownedMesh = null;
            ownedMaterial = null;
        }

        /// <summary>
        /// Setzt Winkelgroesse und Abstand gemeinsam. Dadurch gibt es keinen
        /// Zwischenframe mit inkonsistenter Geometrie.
        /// </summary>
        public void SetGeometry(float newAngularDiameterDegrees, float newDistanceMeters)
        {
            angularDiameterDegrees = Mathf.Clamp(newAngularDiameterDegrees, 1f, 170f);
            viewingDistanceMeters = Mathf.Max(MinimumDistanceMeters, newDistanceMeters);
            ApplyAll(placeAtObserver: observer != null);
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetMerlitzK(float value)
        {
            merlitzK = Mathf.Clamp01(value);
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetMagnification(float value)
        {
            magnification = Mathf.Max(0.01f, value);
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void Show()
        {
            isVisible = true;
            ApplyVisibility();
            StimulusPresented?.Invoke(CaptureSnapshot());
        }

        public void Hide()
        {
            isVisible = false;
            ApplyVisibility();
            StimulusHidden?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Platziert die Flaeche orthogonal zur aktuellen Blickrichtung.
        /// Diese Methode kann explizit am Trial-Anfang aufgerufen werden.
        /// </summary>
        public void PlaceInFrontOfObserver()
        {
            ApplyTransform(placeAtObserver: observer != null);
        }

        public CheckerboardStimulusSnapshot CaptureSnapshot()
        {
            return new CheckerboardStimulusSnapshot
            {
                timestampSeconds = Time.realtimeSinceStartupAsDouble,
                visible = isVisible,
                angularDiameterDegrees = angularDiameterDegrees,
                viewingDistanceMeters = viewingDistanceMeters,
                physicalDiameterMeters = PhysicalDiameterMeters,
                merlitzK = merlitzK,
                magnification = magnification,
                checksAcrossDiameter = checksAcrossDiameter,
                eyePresentation = eyePresentation
            };
        }

        private void ApplyAll(bool placeAtObserver)
        {
            EnsureResources();
            ApplyTransform(placeAtObserver);
            ApplyMaterialProperties();
            ApplyVisibility();
        }

        private void ApplyTransform(bool placeAtObserver)
        {
            if (placeAtObserver && observer != null)
            {
                transform.SetPositionAndRotation(
                    observer.position + observer.forward * viewingDistanceMeters,
                    Quaternion.LookRotation(observer.forward, observer.up));
            }

            float diameter = PhysicalDiameterMeters;
            Vector3 parentScale = transform.parent != null
                ? transform.parent.lossyScale
                : Vector3.one;

            // Kompensiert uebliche gleichmaessige Parent-Skalierung. Fuer eine
            // exakte Versuchsanordnung sollte die Parent-Skalierung (1,1,1) sein.
            transform.localScale = new Vector3(
                diameter / SafeScale(parentScale.x),
                diameter / SafeScale(parentScale.y),
                1f / SafeScale(parentScale.z));
        }

        private void ApplyMaterialProperties()
        {
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_ApparentHalfAngleRad",
                0.5f * angularDiameterDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_MerlitzK", merlitzK);
            propertyBlock.SetFloat("_Magnification", magnification);
            propertyBlock.SetFloat("_ChecksAcrossDiameter", checksAcrossDiameter);
            propertyBlock.SetColor("_DarkColor", darkColor);
            propertyBlock.SetColor("_LightColor", lightColor);
            propertyBlock.SetFloat("_EyeMode", (float)eyePresentation);
            propertyBlock.SetFloat("_FixationEnabled", showFixationTarget ? 1f : 0f);
            propertyBlock.SetFloat("_FixationHalfSizeRad",
                0.5f * fixationTargetSizeDegrees * Mathf.Deg2Rad);
            propertyBlock.SetColor("_FixationColor", fixationColor);

            if (observer != null)
            {
                propertyBlock.SetVector("_ObserverWorldPosition", observer.position);
                propertyBlock.SetVector("_ObserverWorldRight", observer.right);
            }

            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyObserverMaterialProperties()
        {
            if (meshRenderer == null || observer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector("_ObserverWorldPosition", observer.position);
            propertyBlock.SetVector("_ObserverWorldRight", observer.right);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private static bool HasTrackedCenterEyePose()
        {
            InputDevice centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!centerEye.isValid)
            {
                return false;
            }

            if (centerEye.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
            {
                return isTracked;
            }

            if (centerEye.TryGetFeatureValue(
                CommonUsages.trackingState,
                out InputTrackingState trackingState))
            {
                const InputTrackingState required =
                    InputTrackingState.Position | InputTrackingState.Rotation;
                return (trackingState & required) == required;
            }

            // Manche Provider melden eine gueltige Center-Eye-Einheit, aber
            // keinen separaten Tracking-State. Dann ist die gueltige Einheit
            // die beste verfuegbare Startfreigabe.
            return true;
        }

        private void ApplyVisibility()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = isVisible;
            }
        }

        private void EnsureResources()
        {
            meshFilter ??= GetComponent<MeshFilter>();
            meshRenderer ??= GetComponent<MeshRenderer>();

            RebuildDiskIfRequired();

            Material desiredMaterial = materialOverride;
            if (desiredMaterial == null)
            {
                if (ownedMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>(ShaderResourceName);
                    shader ??= Shader.Find(ShaderFallbackName);

                    if (shader == null)
                    {
                        Debug.LogError(
                            $"Shader '{ShaderFallbackName}' wurde nicht gefunden.",
                            this);
                        return;
                    }

                    ownedMaterial = new Material(shader)
                    {
                        name = "Runtime Merlitz Checkerboard Material",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                desiredMaterial = ownedMaterial;
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial != desiredMaterial)
            {
                meshRenderer.sharedMaterial = desiredMaterial;
            }
        }

        private void RebuildDiskIfRequired()
        {
            if (meshFilter == null)
            {
                return;
            }

            int expectedVertexCount = diskSegments + 2;
            if (ownedMesh != null && ownedMesh.vertexCount == expectedVertexCount)
            {
                if (meshFilter.sharedMesh != ownedMesh)
                {
                    meshFilter.sharedMesh = ownedMesh;
                }

                return;
            }

            DestroyOwnedObject(ownedMesh);
            ownedMesh = BuildUnitDisk(diskSegments);
            meshFilter.sharedMesh = ownedMesh;
        }

        private static Mesh BuildUnitDisk(int segments)
        {
            var vertices = new Vector3[segments + 2];
            var uv = new Vector2[segments + 2];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                float x = 0.5f * Mathf.Cos(angle);
                float y = 0.5f * Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(x, y, 0f);
                uv[i + 1] = new Vector2(x + 0.5f, y + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                int triangleIndex = 3 * i;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            var mesh = new Mesh
            {
                name = "Runtime Unit Checkerboard Disk",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ValidateSerializedFields()
        {
            angularDiameterDegrees = Mathf.Clamp(angularDiameterDegrees, 1f, 170f);
            viewingDistanceMeters = Mathf.Max(MinimumDistanceMeters, viewingDistanceMeters);
            merlitzK = Mathf.Clamp01(merlitzK);
            magnification = Mathf.Max(0.01f, magnification);
            checksAcrossDiameter = Mathf.Clamp(checksAcrossDiameter, 2, 80);
            fixationTargetSizeDegrees = Mathf.Clamp(fixationTargetSizeDegrees, 0.05f, 5f);
            diskSegments = Mathf.Clamp(diskSegments, 32, 256);
        }

        private static float SafeScale(float value)
        {
            return Mathf.Max(Mathf.Abs(value), 1e-6f);
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedObject);
            }
            else
            {
                DestroyImmediate(ownedObject);
            }
        }
    }
}
