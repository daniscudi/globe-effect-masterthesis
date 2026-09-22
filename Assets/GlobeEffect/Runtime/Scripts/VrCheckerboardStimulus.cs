using System;
using UnityEngine;

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
    public sealed class VrCheckerboardStimulus : MonoBehaviour
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

        [Header("Blickrichtung und FOV")]
        [SerializeField]
        [Tooltip("Der Kopf im XR-Rig. Normalerweise ist das die Main Camera im XR Origin.")]
        private Transform observer;

        [SerializeField, Range(1f, 170f)]
        [Tooltip("Wie groß der runde Ausschnitt ist, in Grad. 90 heißt 90 Grad von einem Rand zum anderen.")]
        private float angularDiameterDegrees = 90f;

        [SerializeField, Range(0f, 10f)]
        [Tooltip("Wie weich der Rand des Kreises nach innen ausläuft. 0 gibt eine harte Kante.")]
        private float apertureEdgeSoftnessDegrees = 1f;

        [SerializeField]
        [Tooltip("Schaltet den runden Ausschnitt an. Zum Prüfen kann man ihn ausmachen, dann sieht man das ganze viereckige Gitter.")]
        private bool useCircularAperture = true;

        [Header("Visual-Space-/Helmholtz-Gitter")]
        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("l = 1 gibt ein gerades Gitter, l = 0,5 den Helmholtz-Punkt. Kleinere Werte gehen weiter in die kissenförmige Richtung, Werte über 1 in die tonnenförmige.")]
        private float visualSpaceL = 0.5f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Zoom für das Muster. Macht die Karos größer oder kleiner. l und der Rand bleiben dabei gleich.")]
        private float contentZoom = 1f;

        [SerializeField, Range(0.5f, 45f)]
        [Tooltip("Abstand zwischen zwei Gitterlinien in Grad. Oomes et al. haben 10 Grad benutzt.")]
        private float gridLineSpacingDegrees = 10f;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        [Header("Darstellung und Fixation")]
        [SerializeField]
        [Tooltip("Auf beiden Augen zeigen oder nur auf einem.")]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [SerializeField]
        [Tooltip("Zeigt das Kreuz in der Mitte.")]
        private bool showFixationTarget = true;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Wie groß das Fixationskreuz insgesamt ist, in Grad.")]
        private float fixationTargetSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [SerializeField]
        [Tooltip("Hintergrund in der Fixationsphase, also kurz bevor das Muster kommt.")]
        private Color fixationBackgroundColor = Color.gray;

        [Header("Noise-Maske")]
        [SerializeField, Range(0.25f, 10f)]
        [Tooltip("Wie groß ein einzelnes Noise-Kästchen ist, in Grad. Die Maske kommt direkt nach dem Schachbrett.")]
        private float noiseCellSizeDegrees = 1f;

        [Header("Antwortanzeige")]
        [SerializeField]
        private Color responsePromptColor = Color.white;

        [SerializeField, Min(0.5f)]
        [Tooltip("Wie weit vorne der Text liegt. Das hat nichts mit dem Schachbrett zu tun.")]
        private float responsePromptDistanceMeters = 10f;

        [SerializeField, Range(0.005f, 0.1f)]
        [Tooltip("Wie groß die Buchstaben im Text sind.")]
        private float responsePromptCharacterSize = 0.04f;

        [SerializeField]
        [Tooltip("Soll gleich alles sichtbar sein, wenn der Play Mode startet?")]
        private bool visibleAtStart = true;

        [Header("Technik")]
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

        public float AngularDiameterDegrees => angularDiameterDegrees;
        public float ApertureEdgeSoftnessDegrees => apertureEdgeSoftnessDegrees;
        public bool UseCircularAperture => useCircularAperture;
        public float VisualSpaceL => visualSpaceL;
        public float ContentZoom => contentZoom;
        public float GridLineSpacingDegrees => gridLineSpacingDegrees;
        public float GridLineSpacingUv => GridSpacingInUv();
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public bool IsVisible => isVisible;
        public bool IsCheckerboardVisible => isVisible && checkerboardVisible;
        public bool IsNoiseVisible => isVisible && noiseVisible;
        public bool IsResponsePromptVisible => isVisible && responsePromptVisible;
        public Vector3 FixationDirectionWorld => observer != null
            ? observer.forward
            : transform.forward;

        private void Reset()
        {
            // Wenn man die Komponente neu ans Objekt hängt, wird gleich die Main
            // Camera eingetragen. In VR ist das die Kamera im Headset.
            Camera mainCamera = Camera.main;
            observer = mainCamera != null ? mainCamera.transform : null;
        }

        private void OnEnable()
        {
            // Hier werden Viereck, Material und die erste Ansicht vorbereitet.
            Application.onBeforeRender -= UpdateBeforeRendering;
            Application.onBeforeRender += UpdateBeforeRendering;
            ClampInspectorValues();
            SetupMeshAndMaterial();
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
            if (!isActiveAndEnabled)
            {
                return;
            }

            SetupMeshAndMaterial();
            UpdateEverything();
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
            DeleteObject(quadMesh);
            DeleteObject(shaderMaterial);
            DeleteObject(textMaterial);
            DeleteObject(textObject);
            quadMesh = null;
            shaderMaterial = null;
            textMaterial = null;
            textObject = null;
            textMesh = null;
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
            angularDiameterDegrees = Mathf.Clamp(value, 1f, 170f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetApertureEdgeSoftness(float value)
        {
            apertureEdgeSoftnessDegrees = Mathf.Clamp(value, 0f, 10f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetCircularApertureEnabled(bool value)
        {
            // Aus heißt: man sieht das ganze viereckige Gitter. Das ist nur zum
            // Nachschauen. Im Versuch ist der Kreis normalerweise an.
            useCircularAperture = value;
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetVisualSpaceL(float value)
        {
            // l wird hier nicht ausgerechnet. Es wird als Bedingung vorgegeben.
            // Die Formel dazu läuft danach im Shader für jeden Bildpunkt.
            visualSpaceL = Mathf.Clamp(value, 0f, 1.4f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetContentZoom(float value)
        {
            // Der Zoom ändert nur, wie groß man die Karos sieht.
            // Er hat nichts mit l zu tun und ist auch kein Fernglas.
            contentZoom = Mathf.Clamp(value, 0.25f, 4f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetGridLineSpacing(float value)
        {
            // Eingegeben wird in Grad, weil man sich das besser vorstellen kann.
            // GridSpacingInUv rechnet das vor dem Zeichnen in u/v-Weite um.
            gridLineSpacingDegrees = Mathf.Clamp(value, 0.5f, 45f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        /// <summary>Zeigt das ganze Schachbrett mit Fixationskreuz.</summary>
        public void Show()
        {
            isVisible = true;
            checkerboardVisible = true;
            noiseVisible = false;
            fixationVisible = true;
            responsePromptVisible = false;
            SendValuesToShader();
            ShowOrHideObjects();
            StimulusPresented?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Zeigt nur das Fixationskreuz vor grauem Hintergrund. Das läuft vor
        /// jedem Durchgang, bis der Blick ruhig genug auf dem Kreuz liegt.
        /// </summary>
        public void ShowFixationOnly()
        {
            isVisible = true;
            checkerboardVisible = false;
            noiseVisible = false;
            fixationVisible = true;
            responsePromptVisible = false;
            SendValuesToShader();
            ShowOrHideObjects();
        }

        // Zeigt direkt nach dem Muster eine neue Schwarz-Weiß-Maske.
        // Der Seed legt fest, wie die Punkte verteilt sind. Deshalb bekommt
        // jeder Durchgang einen neuen Seed.
        public void ShowNoise(int noiseSeed)
        {
            isVisible = true;
            checkerboardVisible = false;
            noiseVisible = true;
            // Das Kreuz bleibt auch während der Maske stehen. So kann die Person
            // bis zur Antwort an derselben Stelle weiterschauen.
            fixationVisible = true;
            responsePromptVisible = false;
            currentNoiseSeed = noiseSeed;
            SendValuesToShader();
            ShowOrHideObjects();
        }

        // Begrüßung, Trainingstexte und Hinweise werden als Text vor dem Kopf
        // eingeblendet. Der Text ist immer auf beiden Augen zu sehen.
        public void ShowResponsePrompt(string promptText)
        {
            isVisible = true;
            checkerboardVisible = false;
            noiseVisible = false;
            fixationVisible = false;
            responsePromptVisible = true;
            SetupTextObject();
            if (textMesh != null)
            {
                textMesh.text = promptText ?? string.Empty;
                textMesh.color = responsePromptColor;
            }

            SendValuesToShader();
            ShowOrHideObjects();
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
                angularDiameterDegrees = angularDiameterDegrees,
                apertureEdgeSoftnessDegrees = apertureEdgeSoftnessDegrees,
                useCircularAperture = useCircularAperture,
                visualSpaceL = visualSpaceL,
                contentZoom = contentZoom,
                gridLineSpacingDegrees = gridLineSpacingDegrees,
                gridLineSpacingUv = GridSpacingInUv(),
                eyePresentation = eyePresentation
            };
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
            propertyBlock.SetFloat("_ApparentHalfAngleRad",
                0.5f * angularDiameterDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_ApertureEdgeSoftnessRad",
                apertureEdgeSoftnessDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_UseCircularAperture",
                useCircularAperture ? 1f : 0f);
            propertyBlock.SetFloat("_VisualSpaceL", visualSpaceL);
            propertyBlock.SetFloat("_ContentZoom", contentZoom);
            propertyBlock.SetFloat(
                "_GridLineSpacingUv",
                GridSpacingInUv());
            propertyBlock.SetColor("_DarkColor", darkColor);
            propertyBlock.SetColor("_LightColor", lightColor);
            propertyBlock.SetColor("_FixationBackgroundColor", fixationBackgroundColor);
            propertyBlock.SetFloat("_CheckerboardEnabled", checkerboardVisible ? 1f : 0f);
            propertyBlock.SetFloat("_NoiseEnabled", noiseVisible ? 1f : 0f);
            propertyBlock.SetFloat("_NoiseCellSizeUv", NoiseSizeInUv());
            // Als echte ganze Zahl übertragen. Ein Float kann große Seeds wie
            // 20260901 nicht mehr exakt speichern und würde benachbarte Seeds runden.
            propertyBlock.SetInteger("_NoiseSeed", currentNoiseSeed);
            // Beim Antworttext wird immer auf beiden Augen gezeigt, damit der Text
            // auch im Monokular-Durchgang gut lesbar bleibt.
            propertyBlock.SetFloat(
                "_EyeMode",
                responsePromptVisible
                    ? (float)CheckerboardEyePresentation.BothEyes
                    : (float)eyePresentation);
            propertyBlock.SetFloat(
                "_FixationEnabled",
                showFixationTarget && fixationVisible ? 1f : 0f);
            propertyBlock.SetFloat("_FixationHalfSizeRad",
                0.5f * fixationTargetSizeDegrees * Mathf.Deg2Rad);
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

        private float GridSpacingInUv()
        {
            // Beispiel: Bei 90 Grad FOV werden aus 10 Grad ungefähr 0,1763
            // in u/v-Koordinaten.
            return (float)VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                angularDiameterDegrees,
                gridLineSpacingDegrees);
        }

        private float NoiseSizeInUv()
        {
            // Läuft genauso wie beim Gitter: von Grad in u/v-Koordinaten umrechnen.
            return (float)VisualSpaceRadialMapping.NormalizedGridLineSpacing(
                angularDiameterDegrees,
                noiseCellSizeDegrees);
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
            textMesh.characterSize = responsePromptCharacterSize;
            textMesh.color = responsePromptColor;
            // Der Text steht weiter vorne als das Viereck. Deshalb wird der
            // Abstand des Vierecks hier wieder abgezogen.
            textObject.transform.localPosition = Vector3.forward *
                Mathf.Max(0.01f, responsePromptDistanceMeters - QuadDistanceMeters);
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
                // Skript selbst eins mit dem richtigen Shader. Das wird nicht
                // gespeichert und ist nach dem Schließen wieder weg.
                if (shaderMaterial == null)
                {
                    Shader shader = Resources.Load<Shader>(ShaderResourceName);
                    shader ??= Shader.Find(ShaderFallbackName);
                    if (shader == null)
                    {
                        Debug.LogError($"Shader '{ShaderFallbackName}' wurde nicht gefunden.", this);
                        return;
                    }

                    shaderMaterial = new Material(shader)
                    {
                        name = "Runtime Helmholtz Checkerboard Material",
                        hideFlags = HideFlags.HideAndDontSave
                    };
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

        private void ClampInspectorValues()
        {
            // Fängt Werte ab, die jemand von Hand eingetippt hat. Und alte Szenen,
            // in denen noch Werte von früher stehen.
            angularDiameterDegrees = Mathf.Clamp(angularDiameterDegrees, 1f, 170f);
            apertureEdgeSoftnessDegrees = Mathf.Clamp(
                apertureEdgeSoftnessDegrees,
                0f,
                10f);
            visualSpaceL = Mathf.Clamp(visualSpaceL, 0f, 1.4f);
            contentZoom = Mathf.Clamp(contentZoom, 0.25f, 4f);
            gridLineSpacingDegrees = Mathf.Clamp(gridLineSpacingDegrees, 0.5f, 45f);
            noiseCellSizeDegrees = Mathf.Clamp(noiseCellSizeDegrees, 0.25f, 10f);
            fixationTargetSizeDegrees = Mathf.Clamp(fixationTargetSizeDegrees, 0.05f, 5f);
            responsePromptDistanceMeters = Mathf.Max(0.5f, responsePromptDistanceMeters);
            responsePromptCharacterSize = Mathf.Clamp(
                responsePromptCharacterSize,
                0.005f,
                0.1f);
        }

        private static void DeleteObject(UnityEngine.Object objectToDelete)
        {
            if (objectToDelete == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                // Im Play Mode räumt Unity das am Ende vom Frame weg.
                Destroy(objectToDelete);
            }
            else
            {
                // Außerhalb vom Play Mode sofort, damit die Vorschau gleich stimmt.
                DestroyImmediate(objectToDelete);
            }
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
