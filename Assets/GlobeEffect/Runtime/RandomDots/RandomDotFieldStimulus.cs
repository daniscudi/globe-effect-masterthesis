using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Erzeugt ein reproduzierbares, weltfestes Schwarz-Weiß-Punktfeld auf
    /// einer sphärischen Kappe um die Ausgangsposition des Beobachters.
    ///
    /// Die Punkte selbst werden nicht animiert. Bei realer Kopfrotation
    /// entsteht ihre Bewegung ausschließlich aus der veränderten Blickpose.
    /// Der Shader bildet die momentanen Blickrichtungen anschließend mit der
    /// instrumentellen Merlitz-Gleichung ab. So bleibt der statische
    /// Checkerboard-Test von diesem dynamischen Stabilitätstest getrennt.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RandomDotFieldStimulus : MonoBehaviour
    {
        private const string ShaderResourceName = "GlobeEffectMerlitzRandomDots";
        private const string ShaderFallbackName = "GlobeEffect/Merlitz Random Dots";
        private const float MinimumRadiusMeters = 0.25f;

        [Header("Beobachter und Punktfeld")]
        [SerializeField]
        [Tooltip("XR-Kopf-/Center-Eye-Transform.")]
        private Transform observer;

        [SerializeField, Range(5f, 170f)]
        [Tooltip("Kreisförmiger sichtbarer Winkeldurchmesser des Punktfelds.")]
        private float angularDiameterDegrees = 70f;

        [SerializeField, Min(MinimumRadiusMeters)]
        [Tooltip("Radius der weltfesten Punktkappe um die Startposition in Metern.")]
        private float fieldRadiusMeters = 5f;

        [SerializeField, Range(20f, 170f)]
        [Tooltip("Unverzerrter Weltbereich. Er muss die in das sichtbare FOV abgebildeten Objektwinkel plus Kopfbewegung abdecken.")]
        private float worldCoverageDiameterDegrees = 20f;

        [SerializeField, Range(100, 12000)]
        private int dotCount = 4000;

        [SerializeField, Range(0.02f, 2f)]
        [Tooltip("Winkeldurchmesser eines einzelnen Punktes am Ausgangsort.")]
        private float dotAngularDiameterDegrees = 0.22f;

        [SerializeField]
        [Tooltip("Gleicher Seed erzeugt dieselben Punktpositionen und Farben.")]
        private int randomSeed = 20260828;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Anteil heller Punkte; 0.5 ergibt gleich viele schwarze und weiße Punkte.")]
        private float lightDotFraction = 0.5f;

        [Header("Merlitz-Abbildung")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Instrumentparameter k: 1 = Tangens-, 0.5 = Kreis-, 0 = Winkelbedingung.")]
        private float merlitzK = 0.7f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Paraxiale Vergrößerung m. Ohne Vergrößerung (m = 1) hat k keine Wirkung.")]
        private float magnification = 10f;

        [SerializeField]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [Header("Fixationsziel")]
        [SerializeField]
        private bool showFixationTarget = true;

        [SerializeField, Range(0.05f, 3f)]
        private float fixationTargetSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [Header("Bewegung")]
        [SerializeField]
        private RandomDotMotionMode motionMode = RandomDotMotionMode.HeadTracked;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Nur im Debug-Modus: maximale simulierte Gierbewegung je Seite.")]
        private float simulatedYawAmplitudeDegrees = 3f;

        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Nur im Debug-Modus: vollständige Links-Rechts-Zyklen pro Sekunde.")]
        private float simulatedYawFrequencyHz = 0.5f;

        [Header("Darstellung und Technik")]
        [SerializeField]
        private bool visibleAtStart = true;

        [SerializeField]
        [Tooltip("Optionales Material mit dem Shader 'GlobeEffect/Merlitz Random Dots'.")]
        private Material materialOverride;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh ownedMesh;
        private Material ownedMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible = true;
        private double simulatedMotionStartSeconds;

        public event Action<RandomDotStimulusSnapshot> StimulusPresented;
        public event Action<RandomDotStimulusSnapshot> StimulusHidden;
        public event Action<RandomDotStimulusSnapshot> ParametersChanged;

        public Transform Observer
        {
            get => observer;
            set
            {
                observer = value;
                PlaceAroundObserver();
            }
        }

        public float AngularDiameterDegrees => angularDiameterDegrees;
        public float FieldRadiusMeters => fieldRadiusMeters;
        public float WorldCoverageDiameterDegrees => worldCoverageDiameterDegrees;
        public int DotCount => dotCount;
        public int RandomSeed => randomSeed;
        public float MerlitzK => merlitzK;
        public float Magnification => magnification;
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public RandomDotMotionMode MotionMode => motionMode;
        public bool IsVisible => isVisible;

        /// <summary>
        /// Momentaner technischer Schwenkwinkel. Bei realer Kopfbewegung ist
        /// dieser Wert null; der Sweep-Monitor berechnet dort die HMD-Drehung.
        /// </summary>
        public float CurrentSimulatedYawDegrees
        {
            get
            {
                if (!Application.isPlaying ||
                    motionMode != RandomDotMotionMode.SimulatedYaw)
                {
                    return 0f;
                }

                double elapsed = Time.realtimeSinceStartupAsDouble -
                    simulatedMotionStartSeconds;
                double phase = 2.0 * Math.PI * simulatedYawFrequencyHz * elapsed;
                return simulatedYawAmplitudeDegrees * (float)Math.Sin(phase);
            }
        }

        public Vector3 FixationWorldPosition => transform.TransformPoint(
            Vector3.forward * fieldRadiusMeters);

        /// <summary>
        /// Berechnet den tatsächlich auf dem Display dargestellten Strahl zum
        /// Fixationspunkt. Bei einem verzerrten, seitlich liegenden Weltpunkt
        /// unterscheidet er sich bewusst vom geometrischen Strahl zum Punkt.
        /// Eye-Tracking muss gegen diese dargestellte Richtung geprüft werden.
        /// </summary>
        public bool TryGetRenderedFixationWorldDirection(
            Vector3 gazeOriginWorld,
            out Vector3 renderedDirectionWorld)
        {
            renderedDirectionWorld = Vector3.forward;
            if (observer == null)
            {
                return false;
            }

            Vector3 worldVector = FixationWorldPosition - gazeOriginWorld;
            if (worldVector.sqrMagnitude <= 1e-8f)
            {
                return false;
            }

            Vector3 localDirection = observer.InverseTransformDirection(
                worldVector.normalized);
            if (motionMode == RandomDotMotionMode.SimulatedYaw)
            {
                localDirection = Quaternion.Euler(
                    0f,
                    -CurrentSimulatedYawDegrees,
                    0f) * localDirection;
            }

            if (localDirection.z <= 1e-6f)
            {
                return false;
            }

            Vector2 objectPosition = new Vector2(
                localDirection.x / localDirection.z,
                localDirection.y / localDirection.z);
            float objectRadius = objectPosition.magnitude;
            if (objectRadius <= 1e-7f)
            {
                renderedDirectionWorld = observer.forward;
                return true;
            }

            double objectAngle = Math.Atan(objectRadius);
            double apparentAngle = MerlitzCheckerboardMath.ApparentAngleFromObject(
                objectAngle,
                magnification,
                merlitzK);
            if (apparentAngle >= Math.PI / 2.0 - 0.01)
            {
                return false;
            }

            Vector2 displayedPosition = objectPosition.normalized *
                (float)Math.Tan(apparentAngle);
            Vector3 displayedLocalDirection = new Vector3(
                displayedPosition.x,
                displayedPosition.y,
                1f).normalized;
            renderedDirectionWorld = observer.TransformDirection(
                displayedLocalDirection).normalized;
            return true;
        }

        private void Reset()
        {
            Camera mainCamera = Camera.main;
            observer = mainCamera != null ? mainCamera.transform : null;
        }

        private void OnEnable()
        {
            ValidateSerializedFields();
            EnsureResources(rebuildMesh: true);
            isVisible = Application.isPlaying ? visibleAtStart : true;
            simulatedMotionStartSeconds = Time.realtimeSinceStartupAsDouble;
            if (observer != null)
            {
                PlaceAroundObserver();
            }

            ApplyMaterialProperties();
            ApplyVisibility();
        }

        private void OnValidate()
        {
            ValidateSerializedFields();
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureResources(rebuildMesh: true);
            ApplyMaterialProperties();
            ApplyVisibility();
        }

        private void LateUpdate()
        {
            ApplyFrameProperties();
        }

        private void OnDestroy()
        {
            DestroyOwnedObject(ownedMesh);
            DestroyOwnedObject(ownedMaterial);
            ownedMesh = null;
            ownedMaterial = null;
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

        public void SetAngularDiameter(float value)
        {
            angularDiameterDegrees = Mathf.Clamp(value, 5f, 170f);
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetMotionMode(RandomDotMotionMode value)
        {
            motionMode = value;
            RestartMotionPhase();
            ApplyMaterialProperties();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetSimulatedSweep(float amplitudeDegrees, float frequencyHz)
        {
            simulatedYawAmplitudeDegrees = Mathf.Clamp(amplitudeDegrees, 0.1f, 30f);
            simulatedYawFrequencyHz = Mathf.Clamp(frequencyHz, 0.05f, 2f);
            RestartMotionPhase();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void ConfigurePointField(
            int newDotCount,
            int newRandomSeed,
            float newCoverageDiameterDegrees)
        {
            dotCount = Mathf.Clamp(newDotCount, 100, 12000);
            randomSeed = newRandomSeed;
            worldCoverageDiameterDegrees = Mathf.Clamp(
                newCoverageDiameterDegrees,
                20f,
                170f);
            EnsureResources(rebuildMesh: true);
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Verankert das Feld an der aktuellen Kopfposition und -richtung.
        /// Danach folgt es dem Kopf nicht mehr.
        /// </summary>
        public void PlaceAroundObserver()
        {
            if (observer == null)
            {
                return;
            }

            transform.SetPositionAndRotation(
                observer.position,
                Quaternion.LookRotation(observer.forward, observer.up));
            RestartMotionPhase();
            ApplyFrameProperties();
        }

        public void RestartMotionPhase()
        {
            simulatedMotionStartSeconds = Time.realtimeSinceStartupAsDouble;
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

        public RandomDotStimulusSnapshot CaptureSnapshot()
        {
            return new RandomDotStimulusSnapshot
            {
                timestampSeconds = Time.realtimeSinceStartupAsDouble,
                visible = isVisible,
                angularDiameterDegrees = angularDiameterDegrees,
                fieldRadiusMeters = fieldRadiusMeters,
                worldCoverageDiameterDegrees = worldCoverageDiameterDegrees,
                dotCount = dotCount,
                randomSeed = randomSeed,
                merlitzK = merlitzK,
                magnification = magnification,
                eyePresentation = eyePresentation,
                motionMode = motionMode,
                simulatedYawDegrees = CurrentSimulatedYawDegrees
            };
        }

        private void EnsureResources(bool rebuildMesh)
        {
            meshFilter ??= GetComponent<MeshFilter>();
            meshRenderer ??= GetComponent<MeshRenderer>();

            if (rebuildMesh || ownedMesh == null)
            {
                DestroyOwnedObject(ownedMesh);
                ownedMesh = BuildDotMesh();
                meshFilter.sharedMesh = ownedMesh;
            }
            else if (meshFilter.sharedMesh != ownedMesh)
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
                        Debug.LogError(
                            $"Shader '{ShaderFallbackName}' wurde nicht gefunden.",
                            this);
                        return;
                    }

                    ownedMaterial = new Material(shader)
                    {
                        name = "Runtime Merlitz Random Dot Material",
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

        private Mesh BuildDotMesh()
        {
            int renderedDotCount = dotCount + (showFixationTarget ? 1 : 0);
            var vertices = new Vector3[renderedDotCount * 4];
            var uv = new Vector2[renderedDotCount * 4];
            var uv2 = new Vector2[renderedDotCount * 4];
            var colors = new Color32[renderedDotCount * 4];
            var triangles = new int[renderedDotCount * 6];
            var random = new System.Random(randomSeed);

            float halfCoverage = 0.5f * worldCoverageDiameterDegrees * Mathf.Deg2Rad;
            float minimumCosine = Mathf.Cos(halfCoverage);
            for (int dotIndex = 0; dotIndex < dotCount; dotIndex++)
            {
                // Gleichverteilung nach Raumwinkel auf einer sphärischen Kappe.
                float cosine = 1f - (1f - minimumCosine) * (float)random.NextDouble();
                float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
                float azimuth = 2f * Mathf.PI * (float)random.NextDouble();
                Vector3 direction = new Vector3(
                    sine * Mathf.Cos(azimuth),
                    sine * Mathf.Sin(azimuth),
                    cosine);
                Color color = random.NextDouble() < lightDotFraction
                    ? lightColor
                    : darkColor;
                AddDotQuad(
                    dotIndex,
                    direction,
                    1f,
                    color,
                    vertices,
                    uv,
                    uv2,
                    colors,
                    triangles);
            }

            if (showFixationTarget)
            {
                AddDotQuad(
                    dotCount,
                    Vector3.forward,
                    fixationTargetSizeDegrees / dotAngularDiameterDegrees,
                    fixationColor,
                    vertices,
                    uv,
                    uv2,
                    colors,
                    triangles);
            }

            var mesh = new Mesh
            {
                name = "Runtime Random Dot Spherical Cap",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                uv = uv,
                uv2 = uv2,
                colors32 = colors,
                triangles = triangles,
                bounds = new Bounds(
                    Vector3.forward * fieldRadiusMeters * 0.5f,
                    Vector3.one * fieldRadiusMeters * 2.2f)
            };
            return mesh;
        }

        private void AddDotQuad(
            int dotIndex,
            Vector3 direction,
            float sizeMultiplier,
            Color color,
            Vector3[] vertices,
            Vector2[] uv,
            Vector2[] uv2,
            Color32[] colors,
            int[] triangles)
        {
            Vector3 center = direction * fieldRadiusMeters;
            int vertexIndex = dotIndex * 4;
            // Alle vier Vertices tragen dasselbe weltfeste Zentrum. Erst nach
            // der radialen Abbildung erweitert der Shader daraus einen Kreis
            // in konstanten Winkelkoordinaten. Dadurch verändert k nicht
            // unbeabsichtigt die Punktgröße.
            vertices[vertexIndex] = center;
            vertices[vertexIndex + 1] = center;
            vertices[vertexIndex + 2] = center;
            vertices[vertexIndex + 3] = center;
            uv[vertexIndex] = new Vector2(-1f, -1f);
            uv[vertexIndex + 1] = new Vector2(1f, -1f);
            uv[vertexIndex + 2] = new Vector2(1f, 1f);
            uv[vertexIndex + 3] = new Vector2(-1f, 1f);
            Vector2 sizeData = new Vector2(sizeMultiplier, 0f);
            uv2[vertexIndex] = sizeData;
            uv2[vertexIndex + 1] = sizeData;
            uv2[vertexIndex + 2] = sizeData;
            uv2[vertexIndex + 3] = sizeData;

            Color32 packedColor = color;
            colors[vertexIndex] = packedColor;
            colors[vertexIndex + 1] = packedColor;
            colors[vertexIndex + 2] = packedColor;
            colors[vertexIndex + 3] = packedColor;

            int triangleIndex = dotIndex * 6;
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 2;
        }

        private void ApplyMaterialProperties()
        {
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_ApertureHalfAngleRad",
                0.5f * angularDiameterDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_MerlitzK", merlitzK);
            propertyBlock.SetFloat("_Magnification", magnification);
            propertyBlock.SetFloat("_EyeMode", (float)eyePresentation);
            propertyBlock.SetFloat("_DotHalfSizeRad",
                0.5f * dotAngularDiameterDegrees * Mathf.Deg2Rad);
            meshRenderer.SetPropertyBlock(propertyBlock);
            ApplyFrameProperties();
        }

        private void ApplyFrameProperties()
        {
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_SimulatedYawRad",
                CurrentSimulatedYawDegrees * Mathf.Deg2Rad);

            if (observer != null)
            {
                propertyBlock.SetVector("_ObserverWorldPosition", observer.position);
                propertyBlock.SetVector("_ObserverWorldRight", observer.right);
            }

            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyVisibility()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = isVisible;
            }
        }

        private void ValidateSerializedFields()
        {
            angularDiameterDegrees = Mathf.Clamp(angularDiameterDegrees, 5f, 170f);
            fieldRadiusMeters = Mathf.Max(MinimumRadiusMeters, fieldRadiusMeters);
            worldCoverageDiameterDegrees = Mathf.Clamp(
                worldCoverageDiameterDegrees,
                20f,
                170f);
            dotCount = Mathf.Clamp(dotCount, 100, 12000);
            dotAngularDiameterDegrees = Mathf.Clamp(dotAngularDiameterDegrees, 0.02f, 2f);
            lightDotFraction = Mathf.Clamp01(lightDotFraction);
            merlitzK = Mathf.Clamp01(merlitzK);
            magnification = Mathf.Max(0.01f, magnification);
            fixationTargetSizeDegrees = Mathf.Clamp(fixationTargetSizeDegrees, 0.05f, 3f);
            simulatedYawAmplitudeDegrees = Mathf.Clamp(
                simulatedYawAmplitudeDegrees,
                0.1f,
                30f);
            simulatedYawFrequencyHz = Mathf.Clamp(simulatedYawFrequencyHz, 0.05f, 2f);
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
