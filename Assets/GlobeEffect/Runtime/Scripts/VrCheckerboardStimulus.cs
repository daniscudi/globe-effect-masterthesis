using System;
using UnityEngine;
using UnityEngine.XR;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Steuert das sichtbare Checkerboard-Objekt in Unity. Diese Klasse setzt
    /// Position und echte Größe der runden Fläche und übergibt k, m, FOV,
    /// Felderzahl und Augenmodus an den Shader. Das Schwarz-Weiß-Muster selbst
    /// wird erst im Shader für jeden Pixel berechnet. Deshalb bleibt es auch
    /// bei Änderungen während Play scharf.
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
        [Tooltip("Abstand vom Observer zum Mittelpunkt der ebenen Stimulusfläche in Metern.")]
        private float viewingDistanceMeters = 1f;

        [SerializeField]
        [Tooltip("Platziert den Stimulus in jedem Frame erneut vor dem Observer. Für einen weltfesten Versuch deaktiviert lassen.")]
        private bool followObserverEveryFrame;

        [SerializeField]
        [Tooltip("Wartet im Play Mode bis zum ersten LateUpdate, damit die initiale HMD-Pose bereits vorliegt. Danach bleibt der Stimulus weltfest.")]
        private bool placeOnFirstTrackedPose = true;

        [Header("Merlitz-Stimulus")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Verzeichnungsparameter k: 1 = Tangensbedingung/gerades Gitter, 0.5 = Kreisbedingung, 0 = Winkelbedingung.")]
        private float merlitzK = 0.7f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Paraxiale Instrumentvergrößerung m. Die Referenzkonfiguration des Papers verwendet m = 10.")]
        private float magnification = 10f;

        [SerializeField, Range(2, 80)]
        [Tooltip("Anzahl der Schachfelder über den Durchmesser des unverzerrten Ausgangsgitters.")]
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
        [Tooltip("Gesamte Winkelgröße des Fixationskreuzes in Grad.")]
        private float fixationTargetSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [SerializeField]
        [Tooltip("Ist der Stimulus beim Start einer Play-Mode-Sitzung sichtbar?")]
        private bool visibleAtStart = true;

        [Header("Technik")]
        [SerializeField, Range(32, 256)]
        [Tooltip("Segmentzahl des kreisrunden Trägermeshes.")]
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

        /// <summary>Wird nach Show() mit dem aktuellen Parametersatz ausgelöst.</summary>
        public event Action<CheckerboardStimulusSnapshot> StimulusPresented;

        /// <summary>Wird nach Hide() mit dem aktuellen Parametersatz ausgelöst.</summary>
        public event Action<CheckerboardStimulusSnapshot> StimulusHidden;

        /// <summary>Wird nach einer Änderung über die öffentliche API ausgelöst.</summary>
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

            // Beim Start kennt Unity die echte Kopfposition manchmal erst nach
            // dem ersten Frame. Würde das Checkerboard sofort platziert, könnte
            // es noch an der alten Editor-Kameraposition ausgerichtet werden.
            // Deshalb wartet es kurz auf eine gültige HMD-Pose.
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

            // Varjo rendert im Multi-Pass-Modus mehrere Ansichten pro Auge. Die
            // aktuelle Center-Eye-Position hilft dem Shader dabei, auch in diesen
            // einzelnen Durchläufen links und rechts auseinanderzuhalten.
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
        /// Ändert Winkelgröße und Abstand zusammen. Die echte Größe der Fläche
        /// wird direkt neu berechnet, sodass nicht kurz ein Frame mit altem
        /// Abstand und neuer Größe oder umgekehrt gezeigt wird.
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
        /// Setzt das Checkerboard in die aktuelle Blickrichtung. Der Mittelpunkt
        /// liegt viewingDistanceMeters vor dem Observer und die Fläche zeigt
        /// direkt zur aktuellen Kopfpose. Diese Methode wird auch mit R aufgerufen.
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

            // Falls ein übergeordnetes Unity-Objekt skaliert wurde, wird diese
            // Skalierung hier gegengerechnet. Am sichersten bleibt trotzdem eine
            // Parent-Skalierung von (1,1,1), besonders vor echten Messungen.
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

            // Manche XR-Provider liefern eine gültige Center-Eye-Einheit, aber
            // keinen eigenen Trackingstatus. In diesem Fall reicht die gültige
            // Einheit als Zeichen, dass die erste Platzierung stattfinden kann.
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
