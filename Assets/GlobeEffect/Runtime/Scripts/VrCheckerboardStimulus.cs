using System;
using GlobeEffect.VRCheckerboard.EyeTracking;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;

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
        [Tooltip("The head camera of the XR rig, usually the Main Camera.")]
        private Transform observer;

        [FormerlySerializedAs("angularDiameterDegrees")]
        [SerializeField, Range(1f, 170f)]
        [Tooltip("Size of the round window in degrees, from edge to edge.")]
        private float fieldOfViewDegrees = 90f;

        [FormerlySerializedAs("apertureEdgeSoftnessDegrees")]
        [SerializeField, Range(0f, 10f)]
        [Tooltip("Width of the soft edge of the round window, in degrees. The edge fades from full to " +
            "nothing; the window size is measured at the middle of this fade. 0 = sharp edge.")]
        private float edgeSoftnessDegrees = 1f;

        [SerializeField]
        [Tooltip("Shows the checkerboard in a round window. Turn off to see the whole grid.")]
        private bool useCircularAperture = true;

        [Header("Instrument Mapping (Merlitz)")]
        [SerializeField, Range(-2f, 2f)]
        [Tooltip("Nonnegative = Merlitz k. With Allow Pincushion Extension on: negative = custom " +
            "tanh strength (-kappa), stronger pincushion. Negative values are NOT Merlitz k.")]
        [FormerlySerializedAs("visualSpaceL")]
        private float instrumentDistortionK = 0.5f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Magnification m of the instrument for the preview. Default 10. Trials use the m of the " +
            "trial plan. m = 1 makes every k look straight.")]
        private float instrumentMagnificationM = 10f;

        [SerializeField]
        [Tooltip("Variant 2: fixed source grid. Variant 3: normalize the source grid at the nominal " +
            "window edge so the field span stays constant. Trials are set by the Experiment Manager.")]
        private CheckerboardGridMapping gridMapping;

        [SerializeField]
        [Tooltip("Explicitly allow a custom tanh formula for negative settings (-2 to 0). " +
            "Not Merlitz's instrument mapping. Applies only to checkerboards.")]
        private bool allowPincushionExtension;

        [FormerlySerializedAs("gridLineSpacingDegrees")]
        [SerializeField, Range(0.5f, 45f)]
        [Tooltip("Grid spacing in the straight reference image (k = 1), in degrees near the centre. " +
            "The same source grid is used for every k; distorted cells need not keep this angular size.")]
        private float gridSpacingDegrees = 10f;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        // Der Experiment Manager setzt das pro Trial, wenn "Invert Checkerboard
        // Each Trial" an ist. Hier lässt es sich zum Anschauen in der Vorschau
        // umschalten. Die Noise-Maske bleibt davon unberührt.
        [SerializeField]
        [Tooltip("Swaps the black and white squares. The noise does not change.")]
        private bool invertCheckerboard;

        [Header("Display And Fixation")]
        [SerializeField]
        [Tooltip("Show to both eyes or only one.")]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [SerializeField]
        [Tooltip("Shows the cross in the center.")]
        private bool showFixationTarget = true;

        [FormerlySerializedAs("fixationTargetSizeDegrees")]
        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Size of the cross in degrees.")]
        private float fixationSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        // Dieses Grau ist überall zu sehen, wo gerade kein Muster ist: in der
        // Fixationsphase, rundherum um den Kreis und am weichen Rand. Es sollte
        // genauso hell sein wie Schwarz und Weiß im Schnitt. Dann bleibt die
        // mittlere Helligkeit überall gleich, und der Rand wirkt nicht wie eine
        // Kugel. Das Projekt läuft im Gamma-Farbraum, deshalb ist dieses Grau
        // nicht 0,5, sondern ungefähr 0,73 (0,73 hoch 2,2 ergibt 0,5).
        [FormerlySerializedAs("fixationBackgroundColor")]
        [SerializeField]
        [Tooltip("Grey around the window and between images. 0.73 is as bright as black and white on " +
            "average.")]
        private Color backgroundColor = new Color(0.73f, 0.73f, 0.73f);

        // Die Maske soll ein Nachbild des Schachbretts verhindern. Eine Maske wirkt
        // aber nur in Größenordnungen nahe ihrer eigenen (etwa plus/minus eine
        // Oktave; Stromeyer & Julesz 1972, JOSA 62, 1221-1232). Das Schachbrett ist
        // grob (10-Grad-Felder, Grundfrequenz etwa 0,07 Zyklen pro Grad), ein ganz
        // feines "Ameisenmuster" maskiert es deshalb kaum. Standard ist darum das
        // breitbandige Rauschen: mehrere Zellgrößen, gleiche Energie pro Oktave.
        //
        // Feines Rauschen nahe der Pixelgröße bleibt zum Vergleich wählbar. Kleiner
        // als ein Bildpixel geht nicht: Dann trifft jedes Pixel zufällig nur einen
        // von mehreren Punkten. Das gibt ein Raster und Flackern bei jeder kleinen
        // Kopfbewegung.
        [Header("Noise Mask")]
        [SerializeField]
        [Tooltip("Kind of noise between images. Fine and coarse spots: covers the checkerboard best. One " +
            "spot size: all spots the same size. Fine pixels: very fine 'TV static'.")]
        private NoiseMaskType noiseType = NoiseMaskType.Broadband;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Size of the smallest spots, in degrees.")]
        private float noiseBroadbandFinestDegrees = 0.5f;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Size of the largest spots, in degrees. About 8 fits 10 degree checkerboard squares.")]
        private float noiseBroadbandCoarsestDegrees = 8f;

        [SerializeField, Range(1f, 10f)]
        [Tooltip("Size of one spot in screen pixels. 1 = finest possible.")]
        private float noiseDotSizePixels = 1.5f;

        [SerializeField, Range(0.02f, 10f)]
        [Tooltip("Size of one spot in degrees.")]
        private float noiseDotSizeDegrees = 0.15f;

        [SerializeField, Range(0f, 120f)]
        [Tooltip("How often per second the noise changes. 0 = still image. More than 90 has no effect.")]
        private float noiseRefreshRateHz = 30f;

        [Header("Text")]
        [FormerlySerializedAs("responsePromptColor")]
        [SerializeField]
        private Color textColor = Color.white;

        [FormerlySerializedAs("responsePromptDistanceMeters")]
        [SerializeField, Min(0.5f)]
        [Tooltip("The text looks as big as if it stood this far away (m). It is always drawn very far away, " +
            "at the same depth as the cross, so the eyes do not refocus between text and cross.")]
        private float textDistanceMeters = 10f;

        [FormerlySerializedAs("responsePromptCharacterSize")]
        [SerializeField, Range(0.005f, 0.1f)]
        [Tooltip("Size of the letters.")]
        private float textSize = 0.04f;

        [SerializeField]
        [Tooltip("Show everything right when Play Mode starts.")]
        private bool visibleAtStart = true;

        [Header("Advanced")]
        [SerializeField]
        [Tooltip("Own material with the checkerboard shader. Usually leave empty.")]
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

        // Der Seed der Maske, ab wann sie zu sehen ist, und der Seed des gerade
        // gezeigten Rauschbilds. Der letzte wechselt mit der Aktualisierungsrate.
        private int currentNoiseSeed;
        private double noiseStartSeconds;
        private int noiseFrameSeed;
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
        public float InstrumentDistortionK => instrumentDistortionK;
        public float InstrumentMagnificationM => instrumentMagnificationM;
        public CheckerboardGridMapping GridMapping => gridMapping;
        // NaN, wenn die Kombination ungültig ist (dann wird kein Muster gezeigt).
        public float GridSourceScale => InstrumentMappingProblem != null ? float.NaN
            : (float)CheckerboardInstrumentMapping.GridSourceScale(fieldOfViewDegrees, instrumentMagnificationM,
                instrumentDistortionK, allowPincushionExtension, gridMapping);
        public bool AllowPincushionExtension => allowPincushionExtension;

        /// <summary>
        /// Warum die eingestellte Kombination nicht gezeichnet werden kann, sonst null.
        /// Dieselbe Prüfung wie vor Trials und Übungen: FOV samt weichem Rand, k, m und
        /// die Tangens-Grenze. Ungültige Werte werden nicht still auf einen gültigen
        /// Wert gesetzt; der Stimulus zeigt dann nur die graue Fläche.
        /// </summary>
        public string InstrumentMappingProblem
        {
            get
            {
                try
                {
                    CheckerboardInstrumentMapping.ValidateParameters(fieldOfViewDegrees, instrumentMagnificationM,
                        instrumentDistortionK, edgeSoftnessDegrees, useCircularAperture, allowPincushionExtension);
                    CheckerboardInstrumentMapping.ValidateGridMapping(gridMapping);
                    return null;
                }
                catch (ArgumentException exception)
                {
                    return exception.Message;
                }
            }
        }
        public float GridLineSpacingDegrees => gridSpacingDegrees;
        public float GridLineSpacingUv => DegreesToUv(gridSpacingDegrees);
        public bool CheckerboardInverted => invertCheckerboard;
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public float NoiseRefreshRateHz => noiseRefreshRateHz;
        public Color BackgroundColor => backgroundColor;
        public bool IsVisible => isVisible;
        public bool FixationTargetEnabled => showFixationTarget;

        // Die Punktgröße, die gerade wirklich benutzt wird, in Grad in der
        // Bildmitte. Im Pixel-Modus ohne Kamera wird der Gradwert genommen.
        public float NoiseDotSizeDegrees =>
            noiseType == NoiseMaskType.Broadband ? noiseBroadbandFinestDegrees
            : noiseType == NoiseMaskType.FinePixels && TryGetDegreesPerPixel(out float degreesPerPixel)
                ? noiseDotSizePixels * degreesPerPixel
                : noiseDotSizeDegrees;

        public NoiseMaskType NoiseType => noiseType;
        public float NoiseBroadbandCoarsestDegrees => noiseBroadbandCoarsestDegrees;

        // Breitband: Zahl der Zellgrößen von Finest bis Coarsest, je Faktor 2.
        public int NoiseOctaves => noiseType != NoiseMaskType.Broadband ? 1
            : Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(
                noiseBroadbandCoarsestDegrees / noiseBroadbandFinestDegrees, 2f) + 0.001f) + 1, 1, 8);

        // Dieselbe Größe in Bildpixeln. NaN, wenn keine Kamera da ist.
        public float NoiseDotSizePixels =>
            TryGetDegreesPerPixel(out float degreesPerPixel)
                ? NoiseDotSizeDegrees / degreesPerPixel
                : float.NaN;

        /// <summary>
        /// Wie viele Grad ein Bildpixel in der Bildmitte groß ist.
        ///
        /// Die Projektion streckt tan(Winkel) mit dem Faktor m11 auf die Bildhöhe
        /// von -1 bis +1. In der Bildmitte ist ein Pixel deshalb 2 / (m11 * Höhe)
        /// im Bogenmaß groß.
        /// </summary>
        public static float DegreesPerPixel(Matrix4x4 projection, int pixelHeight)
        {
            return Mathf.Rad2Deg * 2f / (projection.m11 * pixelHeight);
        }

        /// <summary>
        /// Das wievielte Rauschbild gerade dran ist. 0 ist das erste; bei einer
        /// Rate von 0 bleibt es dabei.
        /// </summary>
        public static int NoiseFrameIndex(double elapsedSeconds, float refreshRateHz)
        {
            return refreshRateHz <= 0f || elapsedSeconds <= 0d
                ? 0
                : (int)Math.Floor(elapsedSeconds * refreshRateHz);
        }

        /// <summary>
        /// Der Seed für ein bestimmtes Rauschbild. Das erste Bild benutzt den
        /// Seed der Maske selbst. Für jedes weitere werden Seed und Bildnummer so
        /// vermischt, dass aufeinanderfolgende Bilder nichts miteinander zu tun haben.
        /// </summary>
        public static int NoiseFrameSeed(int maskSeed, int frameIndex)
        {
            if (frameIndex == 0)
            {
                return maskSeed;
            }

            // Dieselbe Bitmischung wie im Shader. Die Überläufe sind Absicht.
            uint value = unchecked((uint)maskSeed + (uint)frameIndex * 0x9e3779b9u);
            value ^= value >> 16;
            value = unchecked(value * 0x7feb352du);
            value ^= value >> 15;
            value = unchecked(value * 0x846ca68bu);
            return unchecked((int)(value ^ (value >> 16)));
        }

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
            //
            // Das Rauschbild wird hier einmal pro Frame festgelegt und nicht erst
            // kurz vor dem Zeichnen. So bekommen beide Augen sicher dasselbe Bild.
            if (noiseVisible)
            {
                noiseFrameSeed = NoiseFrameSeed(currentNoiseSeed, NoiseFrameIndex(
                    Time.realtimeSinceStartupAsDouble - noiseStartSeconds, noiseRefreshRateHz));
            }

            MoveQuadInFrontOfHead();
            SendFrameValuesToShader();
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
            SendFrameValuesToShader();
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

        public void SetInstrumentDistortionK(float value)
        {
            // Ungültige Werte werden abgewiesen statt still auf den Rand gesetzt;
            // sonst würde unbemerkt eine andere Abbildung gezeigt.
            if (float.IsNaN(value) || value > 2f
                || value < (allowPincushionExtension ? (float)CheckerboardInstrumentMapping.MinimumExtendedSetting : 0f))
                throw new ArgumentOutOfRangeException(nameof(value), allowPincushionExtension
                    ? "Der Reizwert muss zwischen -2 und 2 liegen."
                    : "Merlitz-k muss zwischen 0 und 2 liegen. Negative Werte nur mit der eigenen tanh-Erweiterung.");
            // k wird als Instrumentenbedingung vorgegeben, nicht als Wahrnehmungs-l.
            instrumentDistortionK = value;
            ApplyChange();
        }

        public void SetPincushionExtensionEnabled(bool enabled)
        {
            // Ein negativer Wert wird beim Ausschalten NICHT still zu k = 0. Er bleibt
            // stehen, das Muster wird nicht gezeigt und InstrumentMappingProblem nennt
            // den Grund. Trials und Übungen setzen k direkt danach ohnehin neu.
            allowPincushionExtension = enabled;
            ApplyChange();
        }

        public void SetInstrumentMagnificationM(float value)
        {
            if (float.IsNaN(value) || value < 1f || value > 20f)
                throw new ArgumentOutOfRangeException(nameof(value), "m muss zwischen 1 und 20 liegen.");
            instrumentMagnificationM = value;
            ApplyChange();
        }

        public void SetGridMapping(CheckerboardGridMapping value)
        {
            CheckerboardInstrumentMapping.ValidateGridMapping(value);
            gridMapping = value;
            ApplyChange();
        }

        public void SetCheckerboardInverted(bool value)
        {
            // Schwarze und weiße Felder tauschen. Die Abbildung bleibt gleich.
            invertCheckerboard = value;
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
            string problem = InstrumentMappingProblem;
            if (problem != null)
            {
                Debug.LogWarning("Checkerboard wird nicht gezeichnet (nur graue Fläche): " + problem, this);
            }

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

        // Zeigt die Schwarz-Weiß-Maske in derselben runden Öffnung wie das Muster.
        // Der Seed legt fest, wie die Punkte verteilt sind. Deshalb bekommt
        // jeder Durchgang einen neuen Seed. Danach würfelt LateUpdate das Bild
        // mit der eingestellten Rate immer wieder neu aus.
        public void ShowNoise(int noiseSeed)
        {
            // Das Kreuz bleibt auch während der Maske stehen. So kann die Person
            // an derselben Stelle weiterschauen.
            currentNoiseSeed = noiseSeed;
            noiseFrameSeed = noiseSeed;
            noiseStartSeconds = Time.realtimeSinceStartupAsDouble;
            ShowParts(checkerboard: false, noise: true, fixation: true, prompt: false);
        }

        // Zum Anschauen der Maske: im Inspector auf die drei Punkte (oder
        // Rechtsklick) an dieser Komponente und "Preview Noise Mask" wählen. Die
        // Maske bleibt dann stehen, bis der Experiment Manager etwas anderes zeigt,
        // zum Beispiel auf dem Startbildschirm, solange man keine Taste drückt.
        [ContextMenu("Preview Noise Mask")]
        private void PreviewNoiseMask()
        {
            ShowNoise(Environment.TickCount);
        }

        // Begrüßung, Trainingstexte und Hinweise werden als Text vor dem Kopf
        // eingeblendet. Der Text ist immer auf beiden Augen zu sehen.
        public void ShowResponsePrompt(string promptText)
        {
            SetupTextObject();
            textMesh.text = promptText ?? string.Empty;
            ShowParts(checkerboard: false, noise: false, fixation: false, prompt: true);
        }

        // Pausenhinweis: Text wie bei ShowResponsePrompt, aber das Fixationskreuz
        // bleibt sichtbar (der Text lässt die Mitte frei).
        public void ShowPausePrompt(string promptText)
        {
            SetupTextObject();
            textMesh.text = promptText ?? string.Empty;
            ShowParts(checkerboard: false, noise: false, fixation: true, prompt: true);
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
                instrumentDistortionK = instrumentDistortionK,
                instrumentMagnificationM = instrumentMagnificationM,
                allowPincushionExtension = allowPincushionExtension,
                gridMapping = gridMapping,
                gridSourceScale = GridSourceScale,
                // Kein zusätzlicher Content Zoom; m gehört zur Instrumentengleichung.
                contentZoom = 1f,
                gridLineSpacingDegrees = gridSpacingDegrees,
                gridLineSpacingUv = DegreesToUv(gridSpacingDegrees),
                eyePresentation = eyePresentation,
                checkerboardInverted = invertCheckerboard
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

        /// <summary>Stellt alle vor der Live-Vorschau gespeicherten Einstellungen wieder her.</summary>
        public void RestorePreviewSettings(string settingsJson)
        {
            JsonUtility.FromJsonOverwrite(settingsJson, this);
            ClampInspectorValues();
            UpdateEverything();
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
            propertyBlock.SetFloat("_InstrumentDistortionK", instrumentDistortionK);
            propertyBlock.SetFloat("_InstrumentMagnificationM", instrumentMagnificationM);
            propertyBlock.SetFloat("_AllowPincushionExtension", allowPincushionExtension ? 1f : 0f);
            propertyBlock.SetFloat("_GridLineSpacingUv", DegreesToUv(gridSpacingDegrees));
            // Ungültige Kombination: kein Muster statt einer still veränderten Abbildung.
            bool mappingValid = InstrumentMappingProblem == null;
            propertyBlock.SetFloat("_GridSourceScale", mappingValid ? GridSourceScale : 1f);
            propertyBlock.SetColor("_DarkColor", darkColor);
            propertyBlock.SetColor("_LightColor", lightColor);
            propertyBlock.SetColor("_FixationBackgroundColor", backgroundColor);
            propertyBlock.SetFloat("_CheckerboardEnabled", checkerboardVisible && mappingValid ? 1f : 0f);
            propertyBlock.SetFloat("_CheckerboardInverted", invertCheckerboard ? 1f : 0f);
            propertyBlock.SetFloat("_NoiseEnabled", noiseVisible ? 1f : 0f);
            propertyBlock.SetFloat("_NoiseCellSizeUv", DegreesToUv(NoiseDotSizeDegrees));
            propertyBlock.SetFloat("_NoiseType", (float)noiseType);
            propertyBlock.SetFloat("_NoiseOctaves", NoiseOctaves);
            // Beim Antworttext wird immer auf beiden Augen gezeigt, damit der Text
            // auch im Monokular-Durchgang gut lesbar bleibt.
            CheckerboardEyePresentation eyeMode =
                responsePromptVisible ? CheckerboardEyePresentation.BothEyes : eyePresentation;
            propertyBlock.SetFloat("_EyeMode", (float)eyeMode);
            propertyBlock.SetFloat("_FixationEnabled", showFixationTarget && fixationVisible ? 1f : 0f);
            propertyBlock.SetFloat("_FixationHalfSizeRad", 0.5f * fixationSizeDegrees * Mathf.Deg2Rad);
            propertyBlock.SetColor("_FixationColor", fixationColor);
            AddFrameValues(propertyBlock);
            meshRenderer.SetPropertyBlock(propertyBlock);
            PaintCameraBackground();
        }

        private void PaintCameraBackground()
        {
            // Außerhalb vom Viereck sieht man den Hintergrund der Kamera. Der wird
            // auf dasselbe Grau gestellt, damit rundherum keine schwarze Fläche
            // bleibt. Das passiert nur im Play Mode, damit die Szene selbst nicht
            // verändert wird. Nach dem Play Mode stellt Unity alles zurück.
            if (!Application.isPlaying || observer == null)
            {
                return;
            }

            Camera headCamera = observer.GetComponent<Camera>();
            if (headCamera != null)
            {
                headCamera.clearFlags = CameraClearFlags.SolidColor;
                headCamera.backgroundColor = backgroundColor;
            }
        }

        private void SendFrameValuesToShader()
        {
            // Die Kopfposition und das Rauschbild ändern sich dauernd, der Rest
            // meist nur am Anfang eines Durchgangs. Deshalb gibt es hier eine
            // kurze eigene Methode.
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            AddFrameValues(propertyBlock);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void AddFrameValues(MaterialPropertyBlock block)
        {
            // Aus diesen vier Angaben baut sich der Shader sein eigenes
            // Koordinatensystem, das am Kopf hängt.
            Transform head = observer != null ? observer : transform;
            block.SetVector("_ObserverWorldPosition", head.position);
            block.SetVector("_ObserverWorldRight", head.right);
            block.SetVector("_ObserverWorldUp", head.up);
            block.SetVector("_ObserverWorldForward", head.forward);

            // Als echte ganze Zahl übertragen. Ein Float kann große Seeds wie
            // 20260901 nicht mehr exakt speichern und würde benachbarte Seeds runden.
            block.SetInteger("_NoiseSeed", noiseFrameSeed);
        }

        private bool TryGetDegreesPerPixel(out float degreesPerPixel)
        {
            // Die Pixelgröße hängt an der Kamera im Kopf: an ihrem Blickwinkel
            // und daran, wie viele Pixel hoch ihr Bild ist.
            degreesPerPixel = 0f;
            Camera headCamera = observer != null ? observer.GetComponent<Camera>() : null;
            if (headCamera == null)
            {
                return false;
            }

            // Im Headset bekommt jedes Auge ein eigenes Bild. Beide sind gleich
            // groß, deshalb reicht das linke. Am Bildschirm zählt das Game View.
            bool headset = headCamera.stereoEnabled && XRSettings.eyeTextureHeight > 0;
            Matrix4x4 projection = headset
                ? headCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left)
                : headCamera.projectionMatrix;
            int pixelHeight = headset ? XRSettings.eyeTextureHeight : headCamera.pixelHeight;
            if (pixelHeight <= 0 || projection.m11 <= 0f)
            {
                return false;
            }

            degreesPerPixel = DegreesPerPixel(projection, pixelHeight);
            return true;
        }

        private float DegreesToUv(float degrees)
        {
            // Eingegeben wird in Grad, weil man sich das besser vorstellen kann.
            // Hier wird das vor dem Zeichnen in u/v-Weite umgerechnet, fürs Gitter
            // und genauso für die Noise-Kästchen.
            // Beispiel: Bei 90 Grad FOV werden aus 10 Grad ungefähr 0,1763
            // in u/v-Koordinaten.
            return (float)CheckerboardInstrumentMapping.GridLineSpacingUv(
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
            // Der Text steht kurz vor der Fernebene, auf derselben Tiefe wie das Kreuz
            // (der Shader zeichnet es optisch unendlich weit weg). Er ist so vergrößert,
            // dass er so groß aussieht wie in textDistanceMeters. Der Abstand des
            // Vierecks (der Text hängt daran) wird wieder abgezogen.
            float distance = UnityTools.FarTextDistance(observer);
            textObject.transform.localPosition = Vector3.forward * (distance - QuadDistanceMeters);
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one * (distance / textDistanceMeters);
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
            // Nur der Reglerbereich -2..2. Ein negativer Wert ohne Erweiterung wird
            // nicht still zu 0, sondern als Problem gemeldet (InstrumentMappingProblem).
            instrumentDistortionK = Mathf.Clamp(instrumentDistortionK,
                (float)CheckerboardInstrumentMapping.MinimumExtendedSetting, 2f);
            instrumentMagnificationM = Mathf.Clamp(instrumentMagnificationM, 1f, 20f);
            gridSpacingDegrees = Mathf.Clamp(gridSpacingDegrees, 0.5f, 45f);
            noiseDotSizeDegrees = Mathf.Clamp(noiseDotSizeDegrees, 0.02f, 10f);
            noiseBroadbandFinestDegrees = Mathf.Clamp(noiseBroadbandFinestDegrees, 0.1f, 10f);
            noiseBroadbandCoarsestDegrees = Mathf.Clamp(noiseBroadbandCoarsestDegrees,
                noiseBroadbandFinestDegrees, 20f);
            noiseDotSizePixels = Mathf.Clamp(noiseDotSizePixels, 1f, 10f);
            noiseRefreshRateHz = Mathf.Clamp(noiseRefreshRateHz, 0f, 120f);
            fixationSizeDegrees = Mathf.Clamp(fixationSizeDegrees, 0.05f, 5f);
            textDistanceMeters = Mathf.Max(0.5f, textDistanceMeters);
            textSize = Mathf.Clamp(textSize, 0.005f, 0.1f);
        }
    }

    // Diese beiden kurzen Listen stehen hier mit beim Stimulus. Sie sagen direkt,
    // wie gezeigt wird und wie die Antwort gespeichert wird. Dafür lohnen sich
    // keine eigenen Dateien.
    // Art der Noise-Maske (siehe Tooltip an noiseType).
    public enum NoiseMaskType
    {
        [InspectorName("Fine Pixels (TV Static)")]
        FinePixels = 0,

        [InspectorName("One Spot Size")]
        SingleScaleDegrees = 1,

        [InspectorName("Fine And Coarse Spots")]
        Broadband = 2
    }

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
        public float instrumentDistortionK;
        public float instrumentMagnificationM;
        public bool allowPincushionExtension;
        public CheckerboardGridMapping gridMapping;
        public float gridSourceScale;
        public float contentZoom;
        public float gridLineSpacingDegrees;
        public float gridLineSpacingUv;
        public CheckerboardEyePresentation eyePresentation;
        public bool checkerboardInverted;
    }
}
