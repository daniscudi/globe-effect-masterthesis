using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Zeigt ein Feld aus schwarzen und weißen Punkten.
    ///
    /// Die Punkte werden als Richtungen gezeichnet, nicht als Sachen in einer
    /// bestimmten Entfernung. Dadurch sehen beide Augen dasselbe und müssen nicht
    /// auf eine nahe Fläche schielen.
    ///
    /// Im Hauptversuch hängt die runde Öffnung am Kopf, und Unity schiebt das
    /// Punktfeld dahinter nach links und rechts. Die Instrumentenwerte m und k
    /// bleiben während einer Darbietung fest.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RandomDotFieldStimulus : MonoBehaviour
    {
        // Das Skript hat im Grunde fünf Aufgaben:
        // 1. Für jeden Punkt ein kleines Viereck bauen (CreateDotMesh).
        // 2. Die Punkte gleichmäßig über eine gewölbte Fläche verteilen.
        // 3. Instrumenten-k, Instrumenten-m, Zusatzzoom, FOV und Bewegung an den
        //    Shader weitergeben.
        // 4. Das Feld beim simulierten Schwenken vor dem Kopf halten.
        // 5. Show und Hide für den Experiment Manager anbieten.
        //
        // Die Verzerrung selbst passiert nicht hier, sondern im Shader
        // GlobeEffectVisualSpaceRandomDots.shader, und zwar für jeden Punkt einzeln.
        private const string ShaderResourceName = "GlobeEffectVisualSpaceRandomDots";
        private const string ShaderFallbackName = "GlobeEffect/Visual Space Random Dots";
        private const float MinimumRadiusMeters = 0.25f;

        // Wie wenige und wie viele Punkte erlaubt sind. Die beiden Zahlen stehen
        // absichtlich nur hier, damit der Regler im Inspector und die Prüfungen
        // weiter unten nie auseinanderlaufen.
        public const int MinimumDotCount = 100;
        public const int MaximumDotCount = 60000;

        // Wie klein und wie groß der Zoom sein darf. Die beiden Zahlen stehen
        // nur hier, damit der Regler im Inspector, die Set-Methode und die
        // Prüfung vor der Sitzung nie auseinanderlaufen.
        public const float MinimumContentZoom = 0.25f;
        public const float MaximumContentZoom = 10f;
        public const float MinimumInstrumentMagnification = 1f;
        public const float MaximumInstrumentMagnification = 20f;

        [Header("Observer And Dot Field")]
        [SerializeField]
        [Tooltip("Der Kopf im XR-Rig, also normalerweise die Main Camera.")]
        private Transform observer;

        [FormerlySerializedAs("angularDiameterDegrees")]
        [SerializeField, Range(5f, 170f)]
        [Tooltip("Scheinbarer Winkeldurchmesser des sichtbaren Fernglasfelds im HMD, in Grad.")]
        private float fieldOfViewDegrees = 90f;

        [FormerlySerializedAs("apertureEdgeSoftnessDegrees")]
        [SerializeField, Range(0f, 10f)]
        [Tooltip("Wie weich der Rand des Kreises nach innen ausläuft. 0 gibt eine harte Kante.")]
        private float edgeSoftnessDegrees = 1f;

        [SerializeField, Min(MinimumRadiusMeters)]
        [Tooltip("Nur die technische Größe des Meshes. Der Shader zeichnet Richtungen, deshalb sieht man diesen Abstand nicht als Entfernung.")]
        private float fieldRadiusMeters = 5f;

        [FormerlySerializedAs("worldCoverageDiameterDegrees")]
        [SerializeField, Range(20f, 170f)]
        [Tooltip("Winkeldurchmesser der erzeugten Außenwelt vor der Instrumentenabbildung. Der Experiment Manager erweitert ihn bei Bedarf automatisch für FOV, m, k und Schwenkweite.")]
        private float worldCoverageDegrees = 40.6f;

        [SerializeField, Range(MinimumDotCount, MaximumDotCount)]
        [Tooltip("Wie viele Punkte erzeugt werden.")]
        private int dotCount = 24500;

        [FormerlySerializedAs("dotAngularDiameterDegrees")]
        [SerializeField, Range(0.02f, 2f)]
        [Tooltip("Wie groß ein einzelner Punkt ist, in Grad.")]
        private float dotSizeDegrees = 0.22f;

        [SerializeField]
        [Tooltip("Gleicher Seed heißt: dieselben Punkte an denselben Stellen in denselben Farben.")]
        private int randomSeed = 20260828;

        [SerializeField]
        private Color darkColor = Color.black;

        [SerializeField]
        private Color lightColor = Color.white;

        [SerializeField]
        [Tooltip("Neutrales Grau innerhalb der kreisförmigen Fernglasöffnung. Außerhalb bleibt der Kamerahintergrund schwarz.")]
        private Color fieldBackgroundColor = Color.gray;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Wie viele Punkte hell sind. 0,5 heißt halb schwarz und halb weiß.")]
        private float lightDotFraction = 0.5f;

        [Header("Merlitz Instrument Optics")]
        [FormerlySerializedAs("merlitzK")]
        [FormerlySerializedAs("visualSpaceL")]
        [SerializeField, Range(0f, 1.4f)]
        [Tooltip("Verzeichnung k des simulierten Instruments. k = 1 ist die Tangentenbedingung, k = 0,5 der Helmholtz-/Kreispunkt. Werte über 1 setzen die Familie in die tonnenförmige Richtung fort.")]
        private float instrumentDistortionK = 0.5f;

        [SerializeField, Range(
            MinimumInstrumentMagnification,
            MaximumInstrumentMagnification)]
        [Tooltip("Paraxiale Fernglasvergrößerung m. 10 entspricht einem typischen 10x-Fernglas.")]
        private float instrumentMagnificationM = 10f;

        [SerializeField, Range(MinimumContentZoom, MaximumContentZoom)]
        [Tooltip("Optionaler zusätzlicher Zoom nach der Instrumentenabbildung. Für den Fernglasversuch normalerweise 1 lassen.")]
        private float contentZoom = 1f;

        [SerializeField]
        private CheckerboardEyePresentation eyePresentation =
            CheckerboardEyePresentation.BothEyes;

        [Header("Fixation Target")]
        [SerializeField]
        private bool showFixationTarget = true;

        [FormerlySerializedAs("fixationTargetSizeDegrees")]
        [SerializeField, Range(0.05f, 3f)]
        private float fixationSizeDegrees = 0.5f;

        [SerializeField]
        private Color fixationColor = Color.red;

        [Header("Motion (Preview And Runtime Status)")]
        [SerializeField]
        private RandomDotMotionMode motionMode = RandomDotMotionMode.SimulatedYaw;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Wie weit die Bewegung zu jeder Seite geht. Beim Start einer Sitzung überschreibt der Experiment Manager diesen Wert.")]
        private float simulatedYawAmplitudeDegrees = 2f;

        [SerializeField, Range(0.1f, 60f)]
        [Tooltip("Wie schnell die Bewegung läuft, in Grad pro Sekunde. Beim Start einer Sitzung überschreibt der Experiment Manager diesen Wert.")]
        private float simulatedYawSpeedDegreesPerSecond = 1.2f;

        [SerializeField]
        [Tooltip("Zu welcher Seite die Bewegung zuerst losgeht.")]
        private RandomDotSweepDirection sweepDirection =
            RandomDotSweepDirection.RightFirst;

        [Header("Display And Advanced")]
        [SerializeField]
        private bool visibleAtStart = true;

        [SerializeField]
        [Tooltip("Optional ein eigenes Material mit dem Random-Dot-Shader. Normalerweise bleibt das leer.")]
        private Material materialOverride;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh dotMesh;
        private Material shaderMaterial;
        private MaterialPropertyBlock propertyBlock;
        private bool isVisible = true;
        private bool pointsVisible = true;
        private bool meshRebuildPending;

        // Ab wann die Bewegung läuft. Daraus wird ausgerechnet, wo sie gerade steht.
        private double motionStartSeconds;

        // Hier können andere Skripte mithören, wenn etwas gezeigt, versteckt oder
        // verändert wurde. Der Experiment Manager schreibt damit genau den Zustand
        // mit, der in dem Moment wirklich zu sehen war.
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

        // Diese Werte kann man von außen nur lesen. Geändert werden sie im
        // Inspector oder über die Set-Methoden weiter unten.
        public float AngularDiameterDegrees => fieldOfViewDegrees;
        public float ApertureEdgeSoftnessDegrees => edgeSoftnessDegrees;
        public float FieldRadiusMeters => fieldRadiusMeters;
        public float WorldCoverageDiameterDegrees => worldCoverageDegrees;
        public int DotCount => dotCount;
        public int RandomSeed => randomSeed;
        public float InstrumentDistortionK => instrumentDistortionK;
        public float InstrumentMagnificationM => instrumentMagnificationM;
        // Kompatibilitätsname für bestehende Editor-Erweiterungen und Szenen.
        public float VisualSpaceL => instrumentDistortionK;
        public float ContentZoom => contentZoom;
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public RandomDotMotionMode MotionMode => motionMode;
        public RandomDotSweepDirection SweepDirection => sweepDirection;
        public float SweepAmplitudeDegrees => simulatedYawAmplitudeDegrees;
        public float SweepSpeedDegreesPerSecond =>
            simulatedYawSpeedDegreesPerSecond;
        public bool IsVisible => isVisible;
        public bool ArePointsVisible => isVisible && pointsVisible;

        /// <summary>
        /// Wo die simulierte Bewegung gerade steht, in Grad.
        ///
        /// Dreht die Person den Kopf selbst, steht hier null. Dann rechnet der
        /// Sweep-Monitor die echte Kopfdrehung aus.
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
                    motionStartSeconds;
                return RandomDotSimulatedSweep.EvaluateYawDegrees(
                    elapsed,
                    simulatedYawAmplitudeDegrees,
                    simulatedYawSpeedDegreesPerSecond,
                    sweepDirection);
            }
        }

        public Vector3 FixationWorldPosition => observer != null
            ? observer.position + observer.forward * fieldRadiusMeters
            : transform.position + transform.forward * fieldRadiusMeters;

        /// <summary>
        /// Das Fixationskreuz ist im Shader eine eigene Ebene und wird nicht
        /// verzerrt. Es bleibt einfach in der Mitte stehen, während sich nur die
        /// Punkte bewegen.
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

            renderedDirectionWorld = observer.forward.normalized;
            return true;
        }

        private void Reset()
        {
            // Wenn man das Skript neu ans Objekt hängt, wird gleich die Main
            // Camera eingetragen.
            Camera mainCamera = Camera.main;
            observer = mainCamera != null ? mainCamera.transform : null;
        }

        private void OnEnable()
        {
            // OnEnable läuft beim Laden und beim Anschalten des Objekts.
            // Hier werden Mesh, Material und alle Startwerte vorbereitet.
            Application.onBeforeRender -= UpdateBeforeRendering;
            Application.onBeforeRender += UpdateBeforeRendering;
            ClampInspectorValues();
            meshRebuildPending = false;
            SetupMeshAndMaterial(rebuildMesh: true);
            isVisible = Application.isPlaying ? visibleAtStart : true;
            pointsVisible = true;
            motionStartSeconds = Time.realtimeSinceStartupAsDouble;
            if (observer != null)
            {
                PlaceAroundObserver();
            }

            SendValuesToShader();
            ShowOrHideRenderer();
        }

        private void OnValidate()
        {
            // OnValidate läuft im Editor, sobald man im Inspector etwas ändert.
            // Unity verbietet dort aber das sofortige Löschen des bisherigen
            // Meshes. Deshalb wird der Neuaufbau für den nächsten normalen
            // Editor-/Game-Loop vorgemerkt.
            ClampInspectorValues();
            if (!isActiveAndEnabled)
            {
                return;
            }

            meshRebuildPending = true;
        }

        private void LateUpdate()
        {
            RebuildMeshIfPending();

            // Kopfposition und Schwenkwinkel ändern sich dauernd. Beide werden
            // deshalb in jedem Frame neu weitergegeben.
            FollowHeadDuringSimulatedSweep();
            SendFrameValuesToShader();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateBeforeRendering;
        }

        private void OnDestroy()
        {
            // Mesh und Material hat dieses Skript selbst gebaut. Also räumt es sie
            // beim Löschen auch selbst wieder weg.
            Application.onBeforeRender -= UpdateBeforeRendering;
            DeleteObject(dotMesh);
            DeleteObject(shaderMaterial);
            dotMesh = null;
            shaderMaterial = null;
        }

        private void UpdateBeforeRendering()
        {
            // Kurz vor dem Zeichnen kann noch eine neuere Kopfposition kommen.
            // Dann wird auch wirklich mit dieser neuen Position gezeichnet.
            FollowHeadDuringSimulatedSweep();
            SendFrameValuesToShader();
        }

        private void RebuildMeshIfPending()
        {
            if (!meshRebuildPending || !isActiveAndEnabled)
            {
                return;
            }

            meshRebuildPending = false;
            SetupMeshAndMaterial(rebuildMesh: true);
            SendValuesToShader();
            ShowOrHideRenderer();
        }

        public void SetInstrumentDistortionK(float value)
        {
            // k wird hier nur auf den erlaubten Bereich gebracht und weitergereicht.
            // Die Merlitz-Instrumentenformel wird im Shader ausgewertet.
            instrumentDistortionK = Mathf.Clamp(value, 0f, 1.4f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetVisualSpaceL(float value)
        {
            // Alte Aufrufer dürfen weiterarbeiten. Im Random-Dot-Instrumentenmodus
            // ist dieser Trialwert jetzt ausdrücklich das dargestellte k.
            SetInstrumentDistortionK(value);
        }

        public void SetInstrumentMagnification(float value)
        {
            instrumentMagnificationM = Mathf.Clamp(
                value,
                MinimumInstrumentMagnification,
                MaximumInstrumentMagnification);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetContentZoom(float value)
        {
            // Dieser Zoom kommt nach der Instrumentenabbildung. Er ändert weder
            // k noch m und bleibt im Hauptversuch normalerweise auf 1.
            contentZoom = Mathf.Clamp(value, MinimumContentZoom, MaximumContentZoom);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetAngularDiameter(float value)
        {
            // Wie groß der sichtbare runde Ausschnitt ist, in Grad.
            fieldOfViewDegrees = Mathf.Clamp(value, 5f, 170f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetApertureEdgeSoftness(float value)
        {
            edgeSoftnessDegrees = Mathf.Clamp(value, 0f, 10f);
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetMotionMode(RandomDotMotionMode value)
        {
            // SimulatedYaw heißt: Der Computer bewegt das Muster.
            // HeadTracked heißt: Die Person dreht den Kopf selbst.
            motionMode = value;
            RestartMotionPhase();
            SendValuesToShader();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetSimulatedSweep(
            float amplitudeDegrees,
            float speedDegreesPerSecond)
        {
            // Amplitude ist, wie weit es zu jeder Seite geht.
            // Speed ist, wie schnell, also Grad pro Sekunde.
            simulatedYawAmplitudeDegrees = Mathf.Clamp(amplitudeDegrees, 0.1f, 30f);
            simulatedYawSpeedDegreesPerSecond = Mathf.Clamp(
                speedDegreesPerSecond,
                0.1f,
                60f);
            RestartMotionPhase();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void SetSweepDirection(RandomDotSweepDirection value)
        {
            sweepDirection = value;
            RestartMotionPhase();
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        public void ConfigurePointField(
            int newDotCount,
            int newRandomSeed,
            float newCoverageDiameterDegrees)
        {
            // Andere Punktzahl, anderer Seed oder anderer Bereich heißt: Die Punkte
            // liegen jetzt woanders. Deshalb muss das Mesh komplett neu gebaut werden.
            dotCount = Mathf.Clamp(newDotCount, MinimumDotCount, MaximumDotCount);
            randomSeed = newRandomSeed;
            worldCoverageDegrees = Mathf.Clamp(
                newCoverageDiameterDegrees,
                20f,
                170f);
            SetupMeshAndMaterial(rebuildMesh: true);
            ParametersChanged?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Setzt das Feld dorthin, wo der Kopf gerade ist und hinschaut.
        ///
        /// Bei SimulatedYaw wird das danach in jedem Frame nachgezogen. Bei
        /// HeadTracked bleibt es stehen, damit die Kopfdrehung überhaupt auffällt.
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
            SendFrameValuesToShader();
        }

        private void FollowHeadDuringSimulatedSweep()
        {
            // Wenn der Computer die Bewegung macht, soll die Person nicht einfach
            // aus der Öffnung herausschauen können. Deshalb zieht das ganze Feld
            // mit dem Kopf mit.
            if (observer == null || motionMode != RandomDotMotionMode.SimulatedYaw)
            {
                return;
            }

            transform.SetPositionAndRotation(
                observer.position,
                Quaternion.LookRotation(observer.forward, observer.up));
        }

        public void RestartMotionPhase()
        {
            // Die Bewegung wird aus der Zeit seit dem Start ausgerechnet. Hier wird
            // diese Uhr wieder auf null gestellt.
            motionStartSeconds = Time.realtimeSinceStartupAsDouble;
        }

        public void Show()
        {
            // Öffnung, Punkte und Kreuz kommen zusammen.
            isVisible = true;
            pointsVisible = true;
            SendValuesToShader();
            ShowOrHideRenderer();
            StimulusPresented?.Invoke(CaptureSnapshot());
        }

        /// <summary>
        /// Zeigt vor dem Durchgang nur das Kreuz in der Mitte. Die Punkte bleiben
        /// weg, damit man den k-Wert noch nicht sieht.
        /// </summary>
        public void ShowFixationOnly()
        {
            isVisible = true;
            pointsVisible = false;
            SendValuesToShader();
            ShowOrHideRenderer();
        }

        public void Hide()
        {
            isVisible = false;
            ShowOrHideRenderer();
            StimulusHidden?.Invoke(CaptureSnapshot());
        }

        public RandomDotStimulusSnapshot CaptureSnapshot()
        {
            // Ein Foto von genau jetzt. Ohne das könnten später aus Versehen schon
            // die Werte vom nächsten Durchgang im Protokoll stehen.
            return new RandomDotStimulusSnapshot
            {
                timestampSeconds = Time.realtimeSinceStartupAsDouble,
                visible = isVisible,
                pointsVisible = pointsVisible,
                angularDiameterDegrees = fieldOfViewDegrees,
                apertureEdgeSoftnessDegrees = edgeSoftnessDegrees,
                fieldRadiusMeters = fieldRadiusMeters,
                worldCoverageDiameterDegrees = worldCoverageDegrees,
                dotCount = dotCount,
                randomSeed = randomSeed,
                instrumentDistortionK = instrumentDistortionK,
                instrumentMagnificationM = instrumentMagnificationM,
                contentZoom = contentZoom,
                eyePresentation = eyePresentation,
                motionMode = motionMode,
                sweepDirection = sweepDirection,
                sweepAmplitudeDegrees = simulatedYawAmplitudeDegrees,
                sweepSpeedDegreesPerSecond = simulatedYawSpeedDegreesPerSecond,
                simulatedYawDegrees = CurrentSimulatedYawDegrees
            };
        }

        private void SetupMeshAndMaterial(bool rebuildMesh)
        {
            // Der MeshFilter hält die Punkte, der MeshRenderer zeichnet sie.
            // Fehlen die noch, werden sie hier vom eigenen Objekt geholt.
            meshFilter ??= GetComponent<MeshFilter>();
            meshRenderer ??= GetComponent<MeshRenderer>();

            if (rebuildMesh || dotMesh == null)
            {
                // Bei geänderter Punktzahl oder neuem Seed passt das alte Mesh nicht
                // mehr. Also weg damit und ein neues bauen.
                DeleteObject(dotMesh);
                dotMesh = CreateDotMesh();
                meshFilter.sharedMesh = dotMesh;
            }
            else if (meshFilter.sharedMesh != dotMesh)
            {
                meshFilter.sharedMesh = dotMesh;
            }

            Material materialToUse = materialOverride;
            if (materialToUse == null)
            {
                // Ist im Inspector kein Material eingetragen, baut sich das Skript
                // selbst eins mit dem Shader aus dem Resources-Ordner.
                if (shaderMaterial == null)
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

                    shaderMaterial = new Material(shader)
                    {
                        name = "Runtime Visual Space Random Dot Material",
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

        private Mesh CreateDotMesh()
        {
            // Jeder Punkt braucht vier Ecken und zwei Dreiecke, also sechs Indizes.
            // Das Fixationskreuz läuft technisch einfach als ein Punkt mehr mit.
            // Das erste Viereck ist die graue Kreisfläche hinter den Punkten.
            // Danach folgen die eigentlichen Zufallspunkte und zuletzt optional
            // das Fixationskreuz. Diese Reihenfolge sorgt dafür, dass Punkte und
            // Kreuz vor dem Hintergrund gezeichnet werden.
            int renderedElementCount = 1 + dotCount +
                (showFixationTarget ? 1 : 0);
            var vertices = new Vector3[renderedElementCount * 4];
            var uv = new Vector2[renderedElementCount * 4];
            var uv2 = new Vector2[renderedElementCount * 4];
            var colors = new Color32[renderedElementCount * 4];
            var triangles = new int[renderedElementCount * 6];
            var random = new System.Random(randomSeed);

            AddOneDot(
                0,
                Vector3.forward,
                1f,
                2f,
                fieldBackgroundColor,
                vertices,
                uv,
                uv2,
                colors,
                triangles);

            float halfCoverage = 0.5f * worldCoverageDegrees * Mathf.Deg2Rad;
            float minimumCosine = Mathf.Cos(halfCoverage);
            for (int dotIndex = 0; dotIndex < dotCount; dotIndex++)
            {
                // Hier ist ein kleiner Trick drin: Würde man den Winkel einfach
                // gleichmäßig auslosen, lägen in der Mitte viel mehr Punkte als
                // außen. Deshalb wird stattdessen der Kosinus des Winkels
                // gleichmäßig ausgelost. Dann ist die Dichte überall gleich.
                float cosine = 1f - (1f - minimumCosine) * (float)random.NextDouble();
                float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
                float azimuth = 2f * Mathf.PI * (float)random.NextDouble();
                Vector3 direction = new Vector3(
                    sine * Mathf.Cos(azimuth),
                    sine * Mathf.Sin(azimuth),
                    cosine);

                // Wichtig ist nur die Richtung. Der Abstand kommt erst durch
                // direction * fieldRadiusMeters dazu und ist reine Technik.
                Color color = random.NextDouble() < lightDotFraction
                    ? lightColor
                    : darkColor;
                AddOneDot(
                    dotIndex + 1,
                    direction,
                    1f,
                    0f,
                    color,
                    vertices,
                    uv,
                    uv2,
                    colors,
                    triangles);
            }

            // Das Kreuz kommt ganz zum Schluss dazu, genau geradeaus in der Mitte.
            if (showFixationTarget)
            {
                AddOneDot(
                    dotCount + 1,
                    Vector3.forward,
                    fixationSizeDegrees / dotSizeDegrees,
                    1f,
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
                // Normalerweise kann ein Mesh nur 65.535 Ecken haben. Bei 4000
                // Punkten mit je vier Ecken sind wir schon darüber. Mit UInt32
                // sind deutlich mehr möglich.
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

        private void AddOneDot(
            int dotIndex,
            Vector3 direction,
            float sizeMultiplier,
            float elementKind,
            Color color,
            Vector3[] vertices,
            Vector2[] uv,
            Vector2[] uv2,
            Color32[] colors,
            int[] triangles)
        {
            Vector3 center = direction * fieldRadiusMeters;
            int vertexIndex = dotIndex * 4;

            // Alle vier Ecken liegen erst mal genau übereinander im Mittelpunkt.
            // Auseinandergezogen werden sie erst im Shader, und zwar nachdem dort
            // die Instrumentenabbildung gerechnet wurde. Dadurch verschieben k
            // und m den Punktmittelpunkt, während die Punkte selbst als kleine,
            // sternähnliche Richtungsmarker eine feste Winkelgröße behalten.
            vertices[vertexIndex] = center;
            vertices[vertexIndex + 1] = center;
            vertices[vertexIndex + 2] = center;
            vertices[vertexIndex + 3] = center;

            // Über die uv-Werte weiß der Shader, welche Ecke er in welche Richtung
            // ziehen muss: links unten, rechts unten, rechts oben, links oben.
            uv[vertexIndex] = new Vector2(-1f, -1f);
            uv[vertexIndex + 1] = new Vector2(1f, -1f);
            uv[vertexIndex + 2] = new Vector2(1f, 1f);
            uv[vertexIndex + 3] = new Vector2(-1f, 1f);

            // In uv2 steht die Größe und die Art des Elements:
            // 0 = Punkt, 1 = Fixationskreuz, 2 = grauer Kreis-Hintergrund.
            Vector2 sizeData = new Vector2(
                sizeMultiplier,
                elementKind);
            uv2[vertexIndex] = sizeData;
            uv2[vertexIndex + 1] = sizeData;
            uv2[vertexIndex + 2] = sizeData;
            uv2[vertexIndex + 3] = sizeData;

            // Alle vier Ecken bekommen dieselbe Farbe, sonst würde der Punkt
            // einen Farbverlauf bekommen.
            Color32 packedColor = color;
            colors[vertexIndex] = packedColor;
            colors[vertexIndex + 1] = packedColor;
            colors[vertexIndex + 2] = packedColor;
            colors[vertexIndex + 3] = packedColor;

            // Zwei Dreiecke ergeben zusammen das Viereck.
            int triangleIndex = dotIndex * 6;
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 2;
        }

        private void SendValuesToShader()
        {
            // Der MaterialPropertyBlock schiebt die Werte nur zu diesem einen
            // Renderer. Das Material muss dafür nicht kopiert werden.
            if (meshRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat("_ApertureHalfAngleRad",
                0.5f * fieldOfViewDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_ApertureEdgeSoftnessRad",
                edgeSoftnessDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat(
                "_InstrumentDistortionK",
                instrumentDistortionK);
            propertyBlock.SetFloat(
                "_InstrumentMagnificationM",
                instrumentMagnificationM);
            propertyBlock.SetFloat("_ContentZoom", contentZoom);
            propertyBlock.SetFloat("_EyeMode", (float)eyePresentation);
            propertyBlock.SetFloat("_DotsEnabled", pointsVisible ? 1f : 0f);
            propertyBlock.SetFloat("_DotHalfSizeRad",
                0.5f * dotSizeDegrees * Mathf.Deg2Rad);
            meshRenderer.SetPropertyBlock(propertyBlock);
            SendFrameValuesToShader();
        }

        private void SendFrameValuesToShader()
        {
            // Diese Werte ändern sich in jedem Frame: wo die Bewegung gerade steht
            // und wo der Kopf ist.
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

        private void ShowOrHideRenderer()
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = isVisible;
            }
        }

        private void ClampInspectorValues()
        {
            // Fängt Werte ab, die jemand von Hand eingetippt hat, und alte Werte aus
            // gespeicherten Szenen. So kommen keine negativen Größen oder unmöglichen
            // Winkel in die Mesh-Erzeugung oder in den Shader.
            fieldOfViewDegrees = Mathf.Clamp(fieldOfViewDegrees, 5f, 170f);
            edgeSoftnessDegrees = Mathf.Clamp(
                edgeSoftnessDegrees,
                0f,
                10f);
            fieldRadiusMeters = Mathf.Max(MinimumRadiusMeters, fieldRadiusMeters);
            worldCoverageDegrees = Mathf.Clamp(
                worldCoverageDegrees,
                20f,
                170f);
            dotCount = Mathf.Clamp(dotCount, MinimumDotCount, MaximumDotCount);
            dotSizeDegrees = Mathf.Clamp(dotSizeDegrees, 0.02f, 2f);
            lightDotFraction = Mathf.Clamp01(lightDotFraction);
            instrumentDistortionK = Mathf.Clamp(
                instrumentDistortionK,
                0f,
                1.4f);
            instrumentMagnificationM = Mathf.Clamp(
                instrumentMagnificationM,
                MinimumInstrumentMagnification,
                MaximumInstrumentMagnification);
            contentZoom = Mathf.Clamp(
                contentZoom,
                MinimumContentZoom,
                MaximumContentZoom);
            fixationSizeDegrees = Mathf.Clamp(fixationSizeDegrees, 0.05f, 3f);
            simulatedYawAmplitudeDegrees = Mathf.Clamp(
                simulatedYawAmplitudeDegrees,
                0.1f,
                30f);
            simulatedYawSpeedDegreesPerSecond = Mathf.Clamp(
                simulatedYawSpeedDegreesPerSecond,
                0.1f,
                60f);
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

    public enum RandomDotMotionMode
    {
        // Die Person dreht den Kopf selbst.
        HeadTracked = 0,

        // Der Computer macht die Bewegung. Das ist der normale Fall im Versuch.
        SimulatedYaw = 1
    }

    public enum RandomDotSweepDirection
    {
        LeftFirst = 0,
        RightFirst = 1
    }

    // Ein Foto von den Werten, die gerade wirklich gezeigt werden.
    // Das wandert ins Protokoll und in die Eye-Tracking-Marker.
    [Serializable]
    public struct RandomDotStimulusSnapshot
    {
        public double timestampSeconds;
        public bool visible;
        public bool pointsVisible;
        public float angularDiameterDegrees;
        public float apertureEdgeSoftnessDegrees;
        public float fieldRadiusMeters;
        public float worldCoverageDiameterDegrees;
        public int dotCount;
        public int randomSeed;
        public float instrumentDistortionK;
        public float instrumentMagnificationM;
        public float contentZoom;
        public CheckerboardEyePresentation eyePresentation;
        public RandomDotMotionMode motionMode;
        public RandomDotSweepDirection sweepDirection;
        public float sweepAmplitudeDegrees;
        public float sweepSpeedDegreesPerSecond;
        public float simulatedYawDegrees;
    }
}
