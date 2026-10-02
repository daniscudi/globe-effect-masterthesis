using System;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard
{
    /// <summary>
    /// Zeigt das Schachbrett im Headset an.
    ///
    /// Das Bild bleibt immer genau vor dem Kopf. Dreht der Teilnehmer den Kopf,
    /// dreht sich das Bild mit.
    ///
    /// Die viereckige Fläche hier ist nur die Leinwand. Das Muster malt der Shader.
    ///
    /// Beide Augen bekommen genau die gleiche Blickrichtung. Dadurch sieht es aus,
    /// als wäre das Muster unendlich weit weg. Die Augen müssen also nicht auf eine
    /// nahe Fläche schielen.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VrCheckerboardStimulus : MonoBehaviour, IFixationStimulus
    {
        // So läuft das hier ab:
        // 1. CreateQuad baut ein einfaches Viereck.
        // 2. Der Shader malt auf dieses Viereck das Bild für beide Augen.
        // 3. SendValuesToShader gibt die Werte aus dem Inspector an den Shader weiter.
        // 4. Jeden Frame bekommt der Shader gesagt, wo der Kopf gerade ist.
        // 5. Der Experiment Manager schaltet um zwischen Fixation, Muster, Noise
        //    und Antwort.
        //
        // Das Schachbrett und die Verzerrung entstehen also komplett im Shader.
        // Es liegen hier keine hundert kleinen schwarzen und weißen Kacheln herum.
        private const string ShaderResourceName = "GlobeEffectHelmholtzCheckerboard";
        private const string ShaderFallbackName = "GlobeEffect/Helmholtz Checkerboard";
        private const float QuadDistanceMeters = 1f;

        [Header("Field Of View")]
        [SerializeField]
        [Tooltip("Der Kopf im XR-Rig. Normalerweise ist das die Main Camera im XR Origin.")]
        private Transform observer;

        [FormerlySerializedAs("angularDiameterDegrees")]
        [SerializeField, Range(1f, 170f)]
        [Tooltip("Wie groß der runde Ausschnitt ist, in Grad. 90 heißt 90 Grad von einem Rand zum anderen.")]
        private float fieldOfViewDegrees = 90f;

        [FormerlySerializedAs("apertureEdgeSoftnessDegrees")]
        [SerializeField, Range(0f, 10f)]
        [Tooltip("Wie weich der Rand des Kreises nach innen ausläuft. 0 gibt eine harte Kante.")]
        private float edgeSoftnessDegrees = 1f;

        [SerializeField]
        [Tooltip("Schaltet den runden Ausschnitt an. Zum Prüfen kann man ihn ausmachen, dann sieht man das ganze viereckige Gitter.")]
        private bool useCircularAperture = true;

        [Header("Grid Distortion")]
        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("l = 1 gibt ein gerades Gitter, l = 0,5 den Helmholtz-Punkt. Kleinere Werte gehen weiter in die kissenförmige Richtung, Werte über 1 in die tonnenförmige.")]
        private float visualSpaceL = 0.5f;

        [FormerlySerializedAs("gridLineSpacingDegrees")]
        [SerializeField, Range(0.5f, 45f)]
        [Tooltip("Abstand zwischen zwei Gitterlinien in Grad. Oomes et al. haben 10 Grad benutzt.")]
        private float gridSpacingDegrees = 10f;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        [Header("Display And Fixation")]
        [SerializeField]
        [Tooltip("Auf beiden Augen zeigen oder nur auf einem.")]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [SerializeField]
        [Tooltip("Zeigt das Kreuz in der Mitte.")]
        private bool showFixationTarget = true;

        [FormerlySerializedAs("fixationTargetSizeDegrees")]
        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Wie groß das Fixationskreuz insgesamt ist, in Grad.")]
        private float fixationSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [SerializeField]
        [Tooltip("Hintergrund in der Fixationsphase, also kurz bevor das Muster kommt.")]
        private Color fixationBackgroundColor = Color.gray;

        [Header("Noise Mask")]
        [FormerlySerializedAs("noiseCellSizeDegrees")]
        [SerializeField, Range(0.25f, 10f)]
        [Tooltip("Wie groß ein einzelnes Noise-Kästchen ist, in Grad. Die Maske kommt direkt nach dem Schachbrett.")]
        private float noiseSizeDegrees = 1f;

        [Header("Response Text")]
        [FormerlySerializedAs("responsePromptColor")]
        [SerializeField]
        private Color textColor = Color.white;

        [FormerlySerializedAs("responsePromptDistanceMeters")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Wie weit vorne der Text liegt. Das hat nichts mit dem Schachbrett zu tun.")]
        private float textDistanceMeters = 10f;

        [FormerlySerializedAs("responsePromptCharacterSize")]
        [SerializeField, Range(0.005f, 0.1f)]
        [Tooltip("Wie groß die Buchstaben im Text sind.")]
        private float textSize = 0.04f;

        [SerializeField]
        [Tooltip("Soll gleich alles sichtbar sein, wenn der Play Mode startet?")]
        private bool visibleAtStart = true;

        [Header("Advanced")]
        [SerializeField]
        [Tooltip("Optional ein eigenes Material mit dem Checkerboard-Shader. Normalerweise bleibt das leer.")]
        private Material materialOverride;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh quadMesh;
        private Material shaderMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible = true;
        private bool checkerboardVisible = true;
        private bool noiseVisible;
        private bool fixationVisible = true;
        private bool responsePromptVisible;
        private int currentNoiseSeed;
        private GameObject textObject;
        private TextMesh textMesh;
        private Material textMaterial;

        // Andere Skripte können hier mithören. Sie bekommen dann genau die Werte,
        // die in dem Moment auf dem Bildschirm waren.
        public event Action<CheckerboardStimulusSnapshot> StimulusPresented;
        public event Action<CheckerboardStimulusSnapshot> StimulusHidden;
        public event Action<CheckerboardStimulusSnapshot> ParametersChanged;

        public Transform Observer
        {
            get => observer;
            set
            {
                observer = value;
                MoveQuadInFrontOfHead();
            }
        }

        public float AngularDiameterDegrees => fieldOfViewDegrees;
        public float ApertureEdgeSoftnessDegrees => edgeSoftnessDegrees;
        public bool UseCircularAperture => useCircularAperture;
        public float VisualSpaceL => visualSpaceL;
        public float GridLineSpacingDegrees => gridSpacingDegrees;
        public float GridLineSpacingUv => DegreesToUv(gridSpacingDegrees);
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public bool IsVisible => isVisible;

        public bool TryGetFixationDirection(out Vector3 directionWorld)
        {
            // Beide Augen schauen parallel geradeaus. Deshalb zählt nur, in welche
            // Richtung geschaut wird. Wo der Blickstrahl anfängt, ist egal.
            directionWorld = observer != null ? observer.forward : transform.forward;
            if (directionWorld.sqrMagnitude <= 1e-8f)
            {
                return false;
            }

            directionWorld = directionWorld.normalized;
            return true;
        }

        private void Reset()
        {
            observer = UnityTools.MainCameraTransform();
        }

        private void OnEnable()
        {
            // Hier werden Viereck, Material und die erste Ansicht vorbereitet.
            Application.onBeforeRender -= UpdateBeforeRendering;
            Application.onBeforeRender += UpdateBeforeRendering;
            ClampInspectorValues();
            isVisible = Application.isPlaying ? visibleAtStart : true;
            checkerboardVisible = true;
            noiseVisible = false;
            fixationVisible = true;
            responsePromptVisible = false;
            UpdateEverything();
        }

        private void OnValidate()
        {
            // Dadurch sieht man Änderungen im Inspector sofort, auch ohne Play Mode.
            // Werte außerhalb des erlaubten Bereichs werden hier gleich zurechtgerückt.
            ClampInspectorValues();
            if (isActiveAndEnabled)
            {
                UpdateEverything();
            }
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateBeforeRendering;
        }

        private void LateUpdate()
        {
            // Das Bild soll mit dem Kopf mitgehen, das ist so gewollt.
            // Das Viereck wird nur nachgezogen, damit Unity es nicht wegoptimiert.
            // Wo das Muster wirklich hinkommt, rechnet allein der Shader aus.
            MoveQuadInFrontOfHead();
            SendHeadPoseToShader();
        }

        private void OnDestroy()
        {
            // Weggeräumt wird nur das, was dieses Skript selbst angelegt hat.
            Application.onBeforeRender -= UpdateBeforeRendering;
            UnityTools.DeleteObject(quadMesh);
            UnityTools.DeleteObject(shaderMaterial);
            UnityTools.DeleteObject(textMaterial);
            UnityTools.DeleteObject(textObject);
        }

        private void UpdateBeforeRendering()
        {
            // Kurz vor dem Zeichnen kann noch eine neuere Kopfposition reinkommen.
            // Wir holen sie hier ab, damit das Bild bei schnellen Kopfbewegungen
            // nicht einen Frame hinterherhängt.
            MoveQuadInFrontOfHead();
            SendHeadPoseToShader();
        }

        public void SetAngularDiameter(float value)
        {
            // Wie groß der runde Ausschnitt ist, von einem Rand zum anderen.
            fieldOfViewDegrees = value;
            ApplyChange();
        }

        public void SetApertureEdgeSoftness(float value)
        {
            edgeSoftnessDegrees = value;
            ApplyChange();
        }

        public void SetVisualSpaceL(float value)
        {
            // l wird hier nicht ausgerechnet. Es wird als Bedingung vorgegeben.
            // Die Formel dazu läuft danach im Shader für jeden Bildpunkt.
            visualSpaceL = value;
            ApplyChange();
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            ApplyChange();
        }

        /// <summary>Zeigt das ganze Schachbrett mit Fixationskreuz.</summary>
        public void Show()
        {
            ShowParts(checkerboard: true, noise: false, fixation: true, prompt: false);
            StimulusPresented?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Zeigt nur das Fixationskreuz vor grauem Hintergrund. Das läuft vor
        /// jedem Durchgang, bis der Blick ruhig genug auf dem Kreuz liegt.
        /// </summary>
        public void ShowFixationOnly()
        {
            ShowParts(checkerboard: false, noise: false, fixation: true, prompt: false);
        }

        // Zeigt direkt nach dem Muster eine neue Schwarz-Weiß-Maske.
        // Der Seed legt fest, wie die Punkte verteilt sind. Deshalb bekommt
        // jeder Durchgang einen neuen Seed.
        public void ShowNoise(int noiseSeed)
        {
            // Das Kreuz bleibt auch während der Maske stehen. So kann die Person
            // bis zur Antwort an derselben Stelle weiterschauen.
            currentNoiseSeed = noiseSeed;
            ShowParts(checkerboard: false, noise: true, fixation: true, prompt: false);
        }

        // Begrüßung, Trainingstexte und Hinweise werden als Text vor dem Kopf
        // eingeblendet. Der Text ist immer auf beiden Augen zu sehen.
        public void ShowResponsePrompt(string promptText)
        {
            SetupTextObject();
            textMesh.text = promptText ?? string.Empty;
            ShowParts(checkerboard: false, noise: false, fixation: false, prompt: true);
        }

        public void Hide()
        {
            isVisible = false;
            responsePromptVisible = false;
            ShowOrHideObjects();
            StimulusHidden?.Invoke(CaptureSnapshot());
        }

        public CheckerboardStimulusSnapshot CaptureSnapshot()
        {
            // Eine Kopie der Werte von genau jetzt. Sie wandert in die Trialdatei
            // und in die Marker der Eye-Tracking-Aufnahme.
            return new CheckerboardStimulusSnapshot
            {
                timestampSeconds = Time.realtimeSinceStartupAsDouble,
                visible = isVisible,
                checkerboardVisible = checkerboardVisible,
                noiseVisible = noiseVisible,
                responsePromptVisible = responsePromptVisible,
                angularDiameterDegrees = fieldOfViewDegrees,
                apertureEdgeSoftnessDegrees = edgeSoftnessDegrees,
                useCircularAperture = useCircularAperture,
                visualSpaceL = visualSpaceL,
                // Das Schachbrett hat keinen Zoom mehr. Das Feld bleibt nur
                // stehen, weil die Eye-Tracking-Toolbox aus dem Labor es
                // ausliest, und steht deshalb fest auf 1.
                contentZoom = 1f,
                gridLineSpacingDegrees = gridSpacingDegrees,
                gridLineSpacingUv = DegreesToUv(gridSpacingDegrees),
                eyePresentation = eyePresentation
            };
        }

        private void ApplyChange()
        {
            // Nach jeder Änderung von außen: Grenzen prüfen, alles an den Shader
            // geben und allen Zuhörern Bescheid sagen.
            ClampInspectorValues();
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        private void ShowParts(bool checkerboard, bool noise, bool fixation, bool prompt)
        {
            // Stellt ein, welche Teile gerade zu sehen sind, und gibt das an den
            // Shader und an den Text weiter.
            isVisible = true;
            checkerboardVisible = checkerboard;
            noiseVisible = noise;
            fixationVisible = fixation;
            responsePromptVisible = prompt;
            SendValuesToShader();
            ShowOrHideObjects();
        }

        private void UpdateEverything()
        {
            // Sammelstelle, wenn die ganze Anzeige neu aufgebaut werden soll.
            SetupMeshAndMaterial();
            MoveQuadInFrontOfHead();
            SendValuesToShader();
            ShowOrHideObjects();
        }

        private void MoveQuadInFrontOfHead()
        {
            if (observer == null)
            {
                return;
            }

            // Das Viereck liegt einen Meter vor der Kamera. Der Shader behandelt
            // seine Ecken aber als reine Richtungen. Deshalb sehen die Augen keine
            // Tiefe von einem Meter, sondern etwas unendlich weit Entferntes.
            transform.SetPositionAndRotation(
                observer.position + observer.forward * QuadDistanceMeters,
                Quaternion.LookRotation(observer.forward, observer.up));
            transform.localScale = Vector3.one;
        }

        private void SendValuesToShader()
        {
            // Der MaterialPropertyBlock schiebt die Werte aus dem Inspector nur zu
            // diesem einen Renderer. Das Material selbst bleibt dabei unverändert.
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_ApparentHalfAngleRad", 0.5f * fieldOfViewDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_ApertureEdgeSoftnessRad", edgeSoftnessDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_UseCircularAperture", useCircularAperture ? 1f : 0f);
            propertyBlock.SetFloat("_VisualSpaceL", visualSpaceL);
            propertyBlock.SetFloat("_GridLineSpacingUv", DegreesToUv(gridSpacingDegrees));
            propertyBlock.SetColor("_DarkColor", darkColor);
            propertyBlock.SetColor("_LightColor", lightColor);
            propertyBlock.SetColor("_FixationBackgroundColor", fixationBackgroundColor);
            propertyBlock.SetFloat("_CheckerboardEnabled", checkerboardVisible ? 1f : 0f);
            propertyBlock.SetFloat("_NoiseEnabled", noiseVisible ? 1f : 0f);
            propertyBlock.SetFloat("_NoiseCellSizeUv", DegreesToUv(noiseSizeDegrees));
            // Als echte ganze Zahl übertragen. Ein Float kann große Seeds wie
            // 20260901 nicht mehr exakt speichern und würde benachbarte Seeds runden.
            propertyBlock.SetInteger("_NoiseSeed", currentNoiseSeed);
            // Beim Antworttext wird immer auf beiden Augen gezeigt, damit der Text
            // auch im Monokular-Durchgang gut lesbar bleibt.
            CheckerboardEyePresentation eyeMode =
                responsePromptVisible ? CheckerboardEyePresentation.BothEyes : eyePresentation;
            propertyBlock.SetFloat("_EyeMode", (float)eyeMode);
            propertyBlock.SetFloat("_FixationEnabled", showFixationTarget && fixationVisible ? 1f : 0f);
            propertyBlock.SetFloat("_FixationHalfSizeRad", 0.5f * fixationSizeDegrees * Mathf.Deg2Rad);
            propertyBlock.SetColor("_FixationColor", fixationColor);
            AddHeadPose(propertyBlock);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void SendHeadPoseToShader()
        {
            // Die Kopfposition ändert sich dauernd, der Rest meist nur am Anfang
            // eines Durchgangs. Deshalb gibt es hier eine kurze eigene Methode.
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            AddHeadPose(propertyBlock);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void AddHeadPose(MaterialPropertyBlock block)
        {
            // Aus diesen vier Angaben baut sich der Shader sein eigenes
            // Koordinatensystem, das am Kopf hängt.
            Transform head = observer != null ? observer : transform;
            block.SetVector("_ObserverWorldPosition", head.position);
            block.SetVector("_ObserverWorldRight", head.right);
            block.SetVector("_ObserverWorldUp", head.up);
            block.SetVector("_ObserverWorldForward", head.forward);
        }

        private float DegreesToUv(float degrees)
        {
            // Eingegeben wird in Grad, weil man sich das besser vorstellen kann.
            // Hier wird das vor dem Zeichnen in u/v-Weite umgerechnet, fürs Gitter
            // und genauso für die Noise-Kästchen.
            // Beispiel: Bei 90 Grad FOV werden aus 10 Grad ungefähr 0,1763
            // in u/v-Koordinaten.
            return (float)VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                fieldOfViewDegrees, degrees);
        }

        private void ShowOrHideObjects()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = isVisible;
            }

            if (textObject != null)
            {
                textObject.SetActive(isVisible && responsePromptVisible);
            }
        }

        private void SetupTextObject()
        {
            if (textObject == null)
            {
                // Der Text wird erst gebaut, wenn er zum ersten Mal gebraucht wird.
                // So muss in der Unity-Szene kein extra UI-Objekt liegen.
                textObject = new GameObject("Runtime Checkerboard Response Prompt")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                textObject.transform.SetParent(transform, false);
                textMesh = textObject.AddComponent<TextMesh>();
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontSize = 64;
                textMesh.lineSpacing = 1f;
                textMesh.richText = false;

                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    textMesh.font = font;
                    // Die hohe Render Queue sorgt dafür, dass der Text immer vor
                    // dem grauen Hintergrund liegt und nicht dahinter verschwindet.
                    textMaterial = new Material(font.material)
                    {
                        name = "Runtime Checkerboard Response Text Material",
                        hideFlags = HideFlags.HideAndDontSave,
                        renderQueue = 5000
                    };
                    MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
                    textRenderer.sharedMaterial = textMaterial;
                    textRenderer.sortingOrder = short.MaxValue;
                }
            }

            textMesh ??= textObject.GetComponent<TextMesh>();
            textMesh.characterSize = textSize;
            textMesh.color = textColor;
            // Der Text steht weiter vorne als das Viereck. Deshalb wird der
            // Abstand des Vierecks hier wieder abgezogen.
            textObject.transform.localPosition =
                Vector3.forward * Mathf.Max(0.01f, textDistanceMeters - QuadDistanceMeters);
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one;
        }

        private void SetupMeshAndMaterial()
        {
            // Der MeshFilter hält das Viereck, der MeshRenderer zeichnet es mit
            // dem Checkerboard-Shader. Beide sitzen auf demselben GameObject.
            meshFilter ??= GetComponent<MeshFilter>();
            meshRenderer ??= GetComponent<MeshRenderer>();

            if (quadMesh == null)
            {
                // Das Viereck sieht immer gleich aus und muss nur einmal gebaut werden.
                quadMesh = CreateQuad();
            }

            if (meshFilter != null && meshFilter.sharedMesh != quadMesh)
            {
                meshFilter.sharedMesh = quadMesh;
            }

            Material materialToUse = materialOverride;
            if (materialToUse == null)
            {
                // Ist im Inspector kein eigenes Material eingetragen, baut sich das
                // Skript selbst eins mit dem richtigen Shader.
                if (shaderMaterial == null)
                {
                    shaderMaterial = UnityTools.CreateShaderMaterial(ShaderResourceName,
                        ShaderFallbackName, "Runtime Helmholtz Checkerboard Material", this);
                    if (shaderMaterial == null)
                    {
                        return;
                    }
                }

                materialToUse = shaderMaterial;
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial != materialToUse)
            {
                meshRenderer.sharedMaterial = materialToUse;
            }
        }

        private static Mesh CreateQuad()
        {
            // Vier Ecken und zwei Dreiecke ergeben ein Quadrat. Auf diesem Quadrat
            // malt der Shader später das Muster. Das Quadrat selbst ist also nicht
            // das Schachbrett, sondern nur die Leinwand.
            var mesh = new Mesh
            {
                name = "Runtime Checkerboard Direction Quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ClampInspectorValues()
        {
            // Fängt Werte ab, die jemand von Hand eingetippt hat. Und alte Szenen,
            // in denen noch Werte von früher stehen. Die Set-Methoden oben nutzen
            // das ebenfalls, damit jede Grenze nur an dieser einen Stelle steht.
            fieldOfViewDegrees = Mathf.Clamp(fieldOfViewDegrees, 1f, 170f);
            edgeSoftnessDegrees = Mathf.Clamp(edgeSoftnessDegrees, 0f, 10f);
            visualSpaceL = Mathf.Clamp(visualSpaceL, 0f, 1.4f);
            gridSpacingDegrees = Mathf.Clamp(gridSpacingDegrees, 0.5f, 45f);
            noiseSizeDegrees = Mathf.Clamp(noiseSizeDegrees, 0.25f, 10f);
            fixationSizeDegrees = Mathf.Clamp(fixationSizeDegrees, 0.05f, 5f);
            textDistanceMeters = Mathf.Max(0.5f, textDistanceMeters);
            textSize = Mathf.Clamp(textSize, 0.005f, 0.1f);
        }
    }

    // Diese beiden kurzen Listen stehen hier mit beim Stimulus. Sie sagen direkt,
    // wie gezeigt wird und wie die Antwort gespeichert wird. Dafür lohnen sich
    // keine eigenen Dateien.
    public enum CheckerboardEyePresentation
    {
        BothEyes = 0,
        LeftEyeOnly = 1,
        RightEyeOnly = 2
    }

    public enum CheckerboardCurvatureResponse
    {
        None = 0,
        Concave = 1,
        Convex = 2
    }

    // Ein Foto von den Werten, die gerade wirklich im Shader stehen.
    // Das wandert in die Marker der Eye-Tracking-Aufnahme.
    [Serializable]
    public struct CheckerboardStimulusSnapshot
    {
        public double timestampSeconds;
        public bool visible;
        public bool checkerboardVisible;
        public bool noiseVisible;
        public bool responsePromptVisible;
        public float angularDiameterDegrees;
        public float apertureEdgeSoftnessDegrees;
        public bool useCircularAperture;
        public float visualSpaceL;
        public float contentZoom;
        public float gridLineSpacingDegrees;
        public float gridLineSpacingUv;
        public CheckerboardEyePresentation eyePresentation;
    }
}
