using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Steuert das Checkerboard, das im Headset immer mittig vor der aktuellen
    /// Blickrichtung bleibt. Die Fläche dient nur als Träger für den Shader.
    /// Dessen Eckpunkte werden als Blickrichtungen und nicht als Punkte in einer
    /// endlichen Entfernung gerendert. Linkes und rechtes Auge erhalten dadurch
    /// dieselben Richtungen: Der Stimulus verhält sich wie ein Objekt in
    /// unendlicher Entfernung und erzeugt keine Konvergenz auf eine nahe Ebene.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VrCheckerboardStimulus : MonoBehaviour
    {
        private const string ShaderResourceName = "GlobeEffectHelmholtzCheckerboard";
        private const string ShaderFallbackName = "GlobeEffect/Helmholtz Checkerboard";
        private const float CarrierDistanceMeters = 1f;

        [Header("Blickrichtung und FOV")]
        [SerializeField]
        [Tooltip("XR-Kopf-/Center-Eye-Transform. Normalerweise ist das die Main Camera im XR Origin.")]
        private Transform observer;

        [SerializeField, Range(1f, 170f)]
        [Tooltip("Winkeldurchmesser der kreisrunden Blende. 90 bedeutet 90 Grad von Rand zu Rand.")]
        private float angularDiameterDegrees = 90f;

        [Header("Visual-Space-/Helmholtz-Gitter")]
        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("l = 1 zeigt ein gerades Gitter, l = 0,5 den Helmholtz-Endpunkt. Kleinere Werte setzen die kissenförmige, Werte über 1 die tonnenförmige Richtung fort.")]
        private float visualSpaceL = 0.5f;

        [SerializeField, Range(2, 80)]
        [Tooltip("Anzahl der Schachfelder über den gesamten Kreisdurchmesser.")]
        private int checksAcrossDiameter = 16;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        [Header("Darstellung und Fixation")]
        [SerializeField]
        [Tooltip("Beidäugige oder monokulare Darbietung.")]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [SerializeField]
        [Tooltip("Zeigt in der Mitte ein Fixationskreuz.")]
        private bool showFixationTarget = true;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Gesamte Winkelgröße des Fixationskreuzes in Grad.")]
        private float fixationTargetSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [SerializeField]
        [Tooltip("Hintergrund während der Fixationsphase vor einem Trial.")]
        private Color fixationBackgroundColor = Color.gray;

        [SerializeField]
        [Tooltip("Ist der vollständige Stimulus beim Start im Play Mode sichtbar?")]
        private bool visibleAtStart = true;

        [Header("Technik")]
        [SerializeField]
        [Tooltip("Optionales eigenes Material mit dem Helmholtz-Checkerboard-Shader.")]
        private Material materialOverride;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh ownedMesh;
        private Material ownedMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible = true;
        private bool checkerboardVisible = true;

        public event Action<CheckerboardStimulusSnapshot> StimulusPresented;
        public event Action<CheckerboardStimulusSnapshot> StimulusHidden;
        public event Action<CheckerboardStimulusSnapshot> ParametersChanged;

        public Transform Observer
        {
            get => observer;
            set
            {
                observer = value;
                ApplyObserverPose();
            }
        }

        public float AngularDiameterDegrees => angularDiameterDegrees;
        public float VisualSpaceL => visualSpaceL;
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public bool IsVisible => isVisible;
        public bool IsCheckerboardVisible => isVisible && checkerboardVisible;
        public Vector3 FixationDirectionWorld => observer != null
            ? observer.forward
            : transform.forward;

        private void Reset()
        {
            Camera mainCamera = Camera.main;
            observer = mainCamera != null ? mainCamera.transform : null;
        }

        private void OnEnable()
        {
            Application.onBeforeRender -= HandleBeforeRender;
            Application.onBeforeRender += HandleBeforeRender;
            ValidateSerializedFields();
            EnsureResources();
            isVisible = Application.isPlaying ? visibleAtStart : true;
            checkerboardVisible = true;
            ApplyAll();
        }

        private void OnValidate()
        {
            ValidateSerializedFields();
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureResources();
            ApplyAll();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= HandleBeforeRender;
        }

        private void LateUpdate()
        {
            // Der Stimulus ist absichtlich head-locked. Der Träger folgt nur für
            // Unitys Sichtbarkeitsprüfung; die eigentliche Projektion im Shader
            // benutzt ausschließlich diese Blickrichtungsvektoren.
            ApplyObserverPose();
            ApplyObserverMaterialProperties();
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= HandleBeforeRender;
            DestroyOwnedObject(ownedMesh);
            DestroyOwnedObject(ownedMaterial);
            ownedMesh = null;
            ownedMaterial = null;
        }

        private void HandleBeforeRender()
        {
            // Der Tracked Pose Driver kann kurz vor dem Rendern noch eine neuere
            // HMD-Pose liefern. Dieses Update verhindert, dass das kopffeste
            // Muster bei schnellen Bewegungen einen Frame hinterherhinkt.
            ApplyObserverPose();
            ApplyObserverMaterialProperties();
        }

        public void SetAngularDiameter(float value)
        {
            angularDiameterDegrees = Mathf.Clamp(value, 1f, 170f);
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetVisualSpaceL(float value)
        {
            visualSpaceL = Mathf.Clamp(value, 0f, 1.4f);
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        /// <summary>Zeigt das vollständige Checkerboard mit Fixationskreuz.</summary>
        public void Show()
        {
            isVisible = true;
            checkerboardVisible = true;
            ApplyMaterialProperties();
            ApplyVisibility();
            StimulusPresented?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Zeigt nur das Fixationskreuz auf neutralem Hintergrund. Diese Phase
        /// läuft vor dem eigentlichen Trial, bis die Fixation stabil ist.
        /// </summary>
        public void ShowFixationOnly()
        {
            isVisible = true;
            checkerboardVisible = false;
            ApplyMaterialProperties();
            ApplyVisibility();
        }

        public void Hide()
        {
            isVisible = false;
            ApplyVisibility();
            StimulusHidden?.Invoke(CaptureSnapshot());
        }

        public CheckerboardStimulusSnapshot CaptureSnapshot()
        {
            return new CheckerboardStimulusSnapshot
            {
                timestampSeconds = Time.realtimeSinceStartupAsDouble,
                visible = isVisible,
                checkerboardVisible = checkerboardVisible,
                angularDiameterDegrees = angularDiameterDegrees,
                visualSpaceL = visualSpaceL,
                checksAcrossDiameter = checksAcrossDiameter,
                eyePresentation = eyePresentation
            };
        }

        private void ApplyAll()
        {
            EnsureResources();
            ApplyObserverPose();
            ApplyMaterialProperties();
            ApplyVisibility();
        }

        private void ApplyObserverPose()
        {
            if (observer == null)
            {
                return;
            }

            transform.SetPositionAndRotation(
                observer.position + observer.forward * CarrierDistanceMeters,
                Quaternion.LookRotation(observer.forward, observer.up));
            transform.localScale = Vector3.one;
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
            propertyBlock.SetFloat("_VisualSpaceL", visualSpaceL);
            propertyBlock.SetFloat("_ChecksAcrossDiameter", checksAcrossDiameter);
            propertyBlock.SetColor("_DarkColor", darkColor);
            propertyBlock.SetColor("_LightColor", lightColor);
            propertyBlock.SetColor("_FixationBackgroundColor", fixationBackgroundColor);
            propertyBlock.SetFloat("_CheckerboardEnabled", checkerboardVisible ? 1f : 0f);
            propertyBlock.SetFloat("_EyeMode", (float)eyePresentation);
            propertyBlock.SetFloat("_FixationEnabled", showFixationTarget ? 1f : 0f);
            propertyBlock.SetFloat("_FixationHalfSizeRad",
                0.5f * fixationTargetSizeDegrees * Mathf.Deg2Rad);
            propertyBlock.SetColor("_FixationColor", fixationColor);
            ApplyObserverProperties(propertyBlock);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyObserverMaterialProperties()
        {
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            ApplyObserverProperties(propertyBlock);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyObserverProperties(MaterialPropertyBlock block)
        {
            Transform basis = observer != null ? observer : transform;
            block.SetVector("_ObserverWorldPosition", basis.position);
            block.SetVector("_ObserverWorldRight", basis.right);
            block.SetVector("_ObserverWorldUp", basis.up);
            block.SetVector("_ObserverWorldForward", basis.forward);
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

            if (ownedMesh == null)
            {
                ownedMesh = BuildCarrierQuad();
            }

            if (meshFilter != null && meshFilter.sharedMesh != ownedMesh)
            {
                meshFilter.sharedMesh = ownedMesh;
            }

            Material desiredMaterial = materialOverride;
            if (desiredMaterial == null)
            {
                if (ownedMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>(ShaderResourceName);
                    shader ??= Shader.Find(ShaderFallbackName);
                    if (shader == null)
                    {
                        Debug.LogError($"Shader '{ShaderFallbackName}' wurde nicht gefunden.", this);
                        return;
                    }

                    ownedMaterial = new Material(shader)
                    {
                        name = "Runtime Helmholtz Checkerboard Material",
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

        private static Mesh BuildCarrierQuad()
        {
            var mesh = new Mesh
            {
                name = "Runtime Checkerboard Direction Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ValidateSerializedFields()
        {
            angularDiameterDegrees = Mathf.Clamp(angularDiameterDegrees, 1f, 170f);
            visualSpaceL = Mathf.Clamp(visualSpaceL, 0f, 1.4f);
            checksAcrossDiameter = Mathf.Clamp(checksAcrossDiameter, 2, 80);
            fixationTargetSizeDegrees = Mathf.Clamp(fixationTargetSizeDegrees, 0.05f, 5f);
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
