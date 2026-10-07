using GlobeEffect.VRCheckerboard.EyeTracking;
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
    /// Im Hauptversuch hängt die runde Öffnung am Kopf, und Unity schwenkt das
    /// Punktfeld dahinter einmal in eine Richtung. Die Instrumentenwerte m und k
    /// bleiben während einer Darbietung fest.
    ///
    /// Wie viele Punkte es gibt, rechnet das Skript selbst aus. Eingestellt wird
    /// nur, wie dicht sie in der Bildmitte liegen sollen. So sieht das Feld bei
    /// jedem m gleich dicht aus.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RandomDotFieldStimulus : MonoBehaviour, IFixationStimulus
    {
        // Das Skript hat fünf Aufgaben:
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

        // Was für ein Element ein Viereck im Mesh ist. Der Shader liest das aus uv2.
        private const float DotElement = 0f;
        private const float FixationElement = 1f;
        private const float BackgroundElement = 2f;

        // Wie wenige und wie viele Punkte erlaubt sind. Die beiden Zahlen stehen
        // absichtlich nur hier, damit der Regler im Inspector und die Prüfungen
        // weiter unten nie auseinanderlaufen. Die Obergrenze reicht für m = 20 im
        // HeadTracked-Block (rund 78.000 Punkte bei 70 Grad FOV, Dichte 0,19 und
        // 15 Grad Sicherheitsbereich).
        public const int MinimumDotCount = 100;
        public const int MaximumDotCount = 100000;

        // Wie klein und wie groß der Zoom sein darf. Die beiden Zahlen stehen
        // nur hier, damit der Regler im Inspector, die Set-Methode und die
        // Prüfung vor der Sitzung nie auseinanderlaufen.
        public const float MinimumContentZoom = 0.25f;
        public const float MaximumContentZoom = 10f;
        public const float MinimumInstrumentMagnification = 1f;
        public const float MaximumInstrumentMagnification = 20f;

        // Der erlaubte Bereich für den simulierten Schwenk. Der Experiment Manager
        // prüft vor einer Sitzung gegen dieselben Zahlen, damit hier nie
        // stillschweigend ein Wert abgeschnitten wird.
        public const float MinimumSweepAmplitudeDegrees = 0.01f;
        public const float MaximumSweepAmplitudeDegrees = 30f;
        public const float MinimumSweepSpeed = 0.01f;
        public const float MaximumSweepSpeed = 60f;

        // Größer als 170 Grad darf die Punktwelt nicht werden. Dazu kommt rundherum
        // noch 1 Grad Reserve, damit am Rand des Kreises nie eine Lücke entsteht.
        public const float MaximumCoverageDegrees = 170f;
        private const float CoverageMarginDegrees = 1f;

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
        [SerializeField, Range(1f, MaximumCoverageDegrees)]
        [Tooltip("Wie groß die erzeugte Außenwelt ist, in Grad, vor der Instrumentenabbildung. Gilt nur für die Vorschau. Im Versuch rechnet der Experiment Manager die passende Größe für jeden Trial selbst aus.")]
        private float worldCoverageDegrees = 40.6f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Wie dicht die Punkte in der Bildmitte liegen, in Punkten pro Quadratgrad. Die Punktzahl rechnet das Skript daraus selbst aus, damit es bei jedem m gleich dicht aussieht. 0,19 heißt ungefähr ein Punkt alle 2,3 Grad und entspricht dem alten Stand mit 24.500 Punkten bei m = 10.")]
        private float dotDensity = 0.19f;

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

        [SerializeField, Range(MinimumInstrumentMagnification, MaximumInstrumentMagnification)]
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

        [SerializeField]
        [Tooltip("Achse des simulierten Schwenks. Aktive Kopfbewegung funktioniert auch oben/unten.")]
        private RandomDotSweepAxis sweepAxis = RandomDotSweepAxis.Horizontal;

        [SerializeField, Range(MinimumSweepAmplitudeDegrees, MaximumSweepAmplitudeDegrees)]
        [Tooltip("Wie weit der simulierte Schwenk zu jeder Seite der Mitte reicht. Er läuft einmal von der einen Seite zur anderen, insgesamt also die doppelte Strecke. Beim Start einer Sitzung überschreibt der Experiment Manager diesen Wert.")]
        private float simulatedYawAmplitudeDegrees = 2f;

        [SerializeField]
        [Tooltip("Nur Vorschau: Bildmitte hält die sichtbare Geschwindigkeit bei jedem m gleich. " +
            "Objektwinkel hält stattdessen den virtuellen Schwenk gleich schnell. " +
            "Für Trainings-/Mess-Trials gelten die Einstellungen am Experiment Manager.")]
        private RandomDotSweepSpeedReference previewSpeedReference = RandomDotSweepSpeedReference.ImageCenter;

        [SerializeField, Range(0.5f, 120f)]
        [Tooltip("Nur Vorschau: gewünschte Punktgeschwindigkeit nahe der Bildmitte in Grad pro Sekunde. " +
            "Der virtuelle Schwenk wird durch m und Content Zoom geteilt.")]
        private float previewImageCenterSpeed = 12f;

        [SerializeField, Range(MinimumSweepSpeed, MaximumSweepSpeed)]
        [Tooltip("Wie schnell das Instrument im simulierten Schwenk über die Außenwelt schwenkt, in Grad Objektwinkel pro Sekunde. Im Bild laufen die Punkte in der Mitte m-mal so schnell. Die Geschwindigkeit bleibt die ganze Zeit gleich. Beim Start einer Sitzung überschreibt der Experiment Manager diesen Wert.")]
        private float simulatedYawSpeedDegreesPerSecond = 1.2f;

        [SerializeField]
        [Tooltip("RightFirst heißt: Das Fernglas schwenkt nach rechts, bei vertikaler Achse nach oben. LeftFirst schwenkt nach links bzw. unten. Die Punkte wandern dabei jeweils zur anderen Seite. Der Experiment Manager setzt die Richtung pro Trial.")]
        private RandomDotSweepDirection sweepDirection =
            RandomDotSweepDirection.RightFirst;

        [SerializeField]
        [Tooltip("Solange keine Sitzung läuft, schwenkt das Feld immer weiter, damit man sich die Bewegung in Ruhe anschauen kann. Der Schwenk läuft dabei über die ganze Punktwelt und fängt am Ende wieder von vorne an. Je größer World Coverage Degrees, desto länger dauert ein Durchlauf. Im Versuch läuft der Schwenk immer nur einmal und so lang wie am Experiment Manager eingestellt.")]
        private bool loopSweepInPreview = true;

        [Header("Preview Reference Grid (Not An Experimental Stimulus)")]
        [SerializeField]
        [Tooltip("Gerades, kopffestes Karopapier zum Vergleichen der Punktbahnen. Keine Verzeichnung, " +
            "kein Mitschwenken. Während Training und Messung immer aus.")]
        private bool showReferenceGrid;

        [SerializeField, Range(0.5f, 20f)]
        [Tooltip("Linienabstand in der flachen u,v-Bildebene, als Winkelabstand nahe der Mitte angegeben.")]
        private float referenceGridSpacingDegrees = 5f;

        [SerializeField, Range(0.5f, 4f)]
        private float referenceGridWidthPixels = 1f;

        [SerializeField]
        private Color referenceGridColor = new Color(0.15f, 0.15f, 0.15f, 0.7f);

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
        private RandomDotMotionMode lastPreviewMotionMode;

        // Wird bei jedem Neubau aus der Dichte ausgerechnet, nicht eingestellt.
        private int dotCount;

        // Ab wann die Bewegung läuft. Daraus wird ausgerechnet, wo sie gerade steht.
        private double motionStartSeconds;

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
        public float ApertureEdgeSoftnessDegrees => edgeSoftnessDegrees;
        public float FieldRadiusMeters => fieldRadiusMeters;
        public float WorldCoverageDiameterDegrees => worldCoverageDegrees;
        public int DotCount => dotCount;
        public float DotDensity => dotDensity;
        public int RandomSeed => randomSeed;
        public float InstrumentDistortionK => instrumentDistortionK;
        public float InstrumentMagnificationM => instrumentMagnificationM;
        public float ContentZoom => contentZoom;
        public CheckerboardEyePresentation EyePresentation => eyePresentation;
        public RandomDotMotionMode MotionMode => motionMode;
        public float SweepAmplitudeDegrees => simulatedYawAmplitudeDegrees;
        public float SweepSpeedDegreesPerSecond => SessionRunning
            ? simulatedYawSpeedDegreesPerSecond
            : RandomDotSimulatedSweep.ObjectSpeed(previewSpeedReference, simulatedYawSpeedDegreesPerSecond,
                previewImageCenterSpeed, instrumentMagnificationM, contentZoom);
        public bool IsVisible => isVisible;

        // Setzt der Experiment Manager, solange eine Sitzung läuft. Dann gibt es
        // keine Dauerschleife, und jeder Schwenk läuft genau einmal.
        public bool SessionRunning { get; set; }

        /// <summary>
        /// Wo die simulierte Bewegung gerade steht, in Grad.
        ///
        /// Dreht die Person den Kopf selbst, steht hier null. Dann rechnet der
        /// Sweep-Monitor die echte Kopfdrehung aus.
        /// </summary>
        public float CurrentSimulatedSweepDegrees
        {
            get
            {
                if (!Application.isPlaying || motionMode != RandomDotMotionMode.SimulatedYaw)
                {
                    return 0f;
                }

                double elapsed = Time.realtimeSinceStartupAsDouble - motionStartSeconds;
                float amplitude = simulatedYawAmplitudeDegrees;
                float objectSpeed = SweepSpeedDegreesPerSecond;
                if (loopSweepInPreview && !SessionRunning)
                {
                    // Vorschau: Der Schwenk läuft so weit, wie neben dem sichtbaren Kreis
                    // noch Punkte da sind, also über die ganze Punktwelt. Am Ende fängt
                    // er wieder von vorne an. Das % ist der Rest beim Teilen und setzt
                    // die Zeit nach jedem Durchlauf wieder auf null.
                    amplitude = Mathf.Max(amplitude, FreeWorldDegrees());
                    elapsed %= 2d * amplitude / objectSpeed;
                }

                return RandomDotSimulatedSweep.EvaluateOneWayDegrees(
                    elapsed, amplitude, objectSpeed, sweepDirection);
            }
        }

        // Wie viel Punktwelt zu jeder Seite übrig ist, wenn man abzieht, was man
        // durch den Kreis ohnehin schon sieht. So weit kann das Feld schwenken,
        // ohne dass am Rand Lücken auftauchen.
        private float FreeWorldDegrees()
        {
            float visiblePart = CoverageNeeded(fieldOfViewDegrees, contentZoom,
                instrumentMagnificationM, instrumentDistortionK, sweepReachDegrees: 0f);
            return 0.5f * (worldCoverageDegrees - visiblePart);
        }

        /// <summary>
        /// Das Fixationskreuz ist im Shader eine eigene Ebene und wird nicht
        /// verzerrt. Es bleibt einfach in der Mitte stehen, während sich nur die
        /// Punkte bewegen.
        /// </summary>
        public bool TryGetFixationDirection(out Vector3 directionWorld)
        {
            directionWorld = Vector3.forward;
            if (observer == null)
            {
                return false;
            }

            directionWorld = observer.forward.normalized;
            return true;
        }

        private void Reset()
        {
            observer = UnityTools.MainCameraTransform();
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
            lastPreviewMotionMode = motionMode;
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
            if (isActiveAndEnabled)
            {
                meshRebuildPending = true;
            }
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
            UnityTools.DeleteObject(dotMesh);
            UnityTools.DeleteObject(shaderMaterial);
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
            // Beim Live-Wechsel auf Active die Punktwelt am jetzigen Kopf
            // ausrichten und danach stehen lassen. Keine Trial-Uhr zurücksetzen.
            if (!SessionRunning && lastPreviewMotionMode != motionMode)
                PlaceAroundObserver();
            lastPreviewMotionMode = motionMode;
            SendValuesToShader();
            ShowOrHideRenderer();
        }

        public void SetInstrumentDistortionK(float value)
        {
            // k wird hier nur auf den erlaubten Bereich gebracht und weitergereicht.
            // Die Merlitz-Instrumentenformel wird im Shader ausgewertet.
            instrumentDistortionK = value;
            ApplyChange();
        }

        public void SetInstrumentMagnification(float value)
        {
            instrumentMagnificationM = value;
            ApplyChange();
        }

        public void SetContentZoom(float value)
        {
            // Dieser Zoom kommt nach der Instrumentenabbildung. Er ändert weder
            // k noch m und bleibt im Hauptversuch normalerweise auf 1.
            contentZoom = value;
            ApplyChange();
        }

        public void SetAngularDiameter(float value)
        {
            // Wie groß der sichtbare runde Ausschnitt ist, in Grad.
            fieldOfViewDegrees = value;
            ApplyChange();
        }

        public void SetApertureEdgeSoftness(float value)
        {
            edgeSoftnessDegrees = value;
            ApplyChange();
        }

        public void SetEyePresentation(CheckerboardEyePresentation value)
        {
            eyePresentation = value;
            ApplyChange();
        }

        public void SetMotionMode(RandomDotMotionMode value)
        {
            // SimulatedYaw heißt: Der Computer bewegt das Muster.
            // HeadTracked heißt: Die Person dreht den Kopf selbst.
            motionMode = value;
            RestartMotionPhase();
            ApplyChange();
        }

        public void SetSimulatedSweep(float amplitudeDegrees, float speedDegreesPerSecond)
        {
            // Amplitude ist, wie weit es zu jeder Seite der Mitte geht.
            // Speed ist die feste Geschwindigkeit des einseitigen Schwenks.
            simulatedYawAmplitudeDegrees = amplitudeDegrees;
            simulatedYawSpeedDegreesPerSecond = speedDegreesPerSecond;
            ClampInspectorValues();
            RestartMotionPhase();
        }

        public void SetSweepDirection(RandomDotSweepDirection value)
        {
            sweepDirection = value;
            RestartMotionPhase();
        }

        public void SetSweepAxis(RandomDotSweepAxis value)
        {
            sweepAxis = value;
            RestartMotionPhase();
            SendFrameValuesToShader();
        }

        public void ConfigurePointField(int newRandomSeed, float newCoverageDegrees)
        {
            // Anderer Seed oder anderer Bereich heißt: Die Punkte liegen jetzt
            // woanders. Deshalb muss das Mesh komplett neu gebaut werden. Die
            // Punktzahl ergibt sich dabei von selbst aus Dichte, m, Zoom und Bereich.
            randomSeed = newRandomSeed;
            worldCoverageDegrees = newCoverageDegrees;
            ClampInspectorValues();
            SetupMeshAndMaterial(rebuildMesh: true);
        }

        /// <summary>
        /// Wie groß die Punktwelt sein muss, in Grad. Sie muss alles abdecken, was
        /// man durch den Kreis sieht, und zusätzlich so weit reichen, wie sich das
        /// Feld beim Schwenken verschiebt. Sonst sieht man am Rand Lücken.
        /// Unendlich heißt: Diese Kombination lässt sich gar nicht abbilden.
        /// </summary>
        public static float CoverageNeeded(float fieldOfViewDegrees, float zoom,
            float magnificationM, float distortionK, float sweepReachDegrees)
        {
            // Rückwärts gerechnet: Welcher Winkel draußen in der Welt landet genau
            // am Rand des Kreises? Erst wird der Zusatzzoom herausgerechnet, dann
            // die Merlitz-Formel umgedreht.
            float edge = Mathf.Atan(Mathf.Tan(0.5f * fieldOfViewDegrees * Mathf.Deg2Rad) / zoom);
            if (distortionK * edge >= 0.5f * Mathf.PI - 1e-5f)
            {
                // Hinter dem Umkehrpunkt des Tangens gibt es keine sinnvolle Abbildung.
                return float.PositiveInfinity;
            }

            float worldEdge = (float)MerlitzBinocularReferenceMath.ObjectAngleFromApparent(
                edge, magnificationM, distortionK);
            return 2f * (worldEdge * Mathf.Rad2Deg + sweepReachDegrees + CoverageMarginDegrees);
        }

        /// <summary>
        /// Wie viele Punkte die Punktwelt braucht, damit sie im Bild in der Mitte
        /// so dicht aussieht wie eingestellt.
        /// </summary>
        public static int DotCountFor(float densityPerSquareDegree, float magnificationM,
            float zoom, float coverageDegrees)
        {
            // In der Bildmitte vergrößert das Instrument bei jedem k genau um m, der
            // Zusatzzoom noch einmal um seinen Wert. Ein kleines Stück Außenwelt wird
            // im Bild also (m * Zoom)² mal so groß. Damit es im Bild gleich dicht
            // aussieht, braucht die Außenwelt (m * Zoom)² mal so viele Punkte pro Fläche.
            // Am Rand staucht oder streckt das Instrument je nach k. Das ist gewollt,
            // denn genau so macht es auch ein echtes Fernglas.
            float scale = magnificationM * zoom;
            float worldDensity = densityPerSquareDegree * scale * scale;

            // Die Punktwelt ist eine Kugelkappe. Ihre Fläche ist 2π(1 - cos(halber
            // Winkel)) in Steradiant. Mal (180/π)² wird daraus Quadratgrad.
            float halfAngle = 0.5f * coverageDegrees * Mathf.Deg2Rad;
            float capArea = 2f * Mathf.PI * (1f - Mathf.Cos(halfAngle)) * Mathf.Rad2Deg * Mathf.Rad2Deg;
            return Mathf.CeilToInt(worldDensity * capArea);
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

            MoveToHead();
            RestartMotionPhase();
            SendFrameValuesToShader();
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
        }

        private void ApplyChange()
        {
            // Nach jeder Änderung von außen: Grenzen prüfen und an den Shader geben.
            ClampInspectorValues();
            SendValuesToShader();

            // Bei anderem m oder Zoom passt die Punktzahl nicht mehr zur Dichte.
            // Dann wird das Mesh im nächsten Frame neu gebaut. Ruft der Experiment
            // Manager gleich danach ConfigurePointField auf, passiert das sofort,
            // und die Vormerkung fällt wieder weg.
            if (DotCountForCurrentValues() != dotCount)
            {
                meshRebuildPending = true;
            }
        }

        private int DotCountForCurrentValues()
        {
            int count = DotCountFor(dotDensity, instrumentMagnificationM, contentZoom, worldCoverageDegrees);
            return Mathf.Clamp(count, MinimumDotCount, MaximumDotCount);
        }

        /// <summary>Vorschauänderungen verwerfen und Mesh/Shader wieder mit den Originalwerten bauen.</summary>
        public void RestorePreviewSettings(string settingsJson)
        {
            JsonUtility.FromJsonOverwrite(settingsJson, this);
            ClampInspectorValues();
            SetupMeshAndMaterial(rebuildMesh: true);
            lastPreviewMotionMode = motionMode;
            PlaceAroundObserver();
            SendValuesToShader();
            ShowOrHideRenderer();
        }

        private void FollowHeadDuringSimulatedSweep()
        {
            // Wenn der Computer die Bewegung macht, soll die Person nicht einfach
            // aus der Öffnung herausschauen können. Deshalb zieht das ganze Feld
            // mit dem Kopf mit.
            if (observer != null && motionMode == RandomDotMotionMode.SimulatedYaw)
            {
                MoveToHead();
            }
        }

        private void MoveToHead()
        {
            transform.SetPositionAndRotation(
                observer.position, Quaternion.LookRotation(observer.forward, observer.up));
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
                meshRebuildPending = false;
                UnityTools.DeleteObject(dotMesh);
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
                    shaderMaterial = UnityTools.CreateShaderMaterial(ShaderResourceName,
                        ShaderFallbackName, "Runtime Visual Space Random Dot Material", this);
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

        private Mesh CreateDotMesh()
        {
            // Jeder Punkt braucht vier Ecken und zwei Dreiecke, also sechs Indizes.
            // Das Fixationskreuz läuft technisch einfach als ein Punkt mehr mit.
            // Das erste Viereck ist die graue Kreisfläche hinter den Punkten.
            // Danach folgen die eigentlichen Zufallspunkte und zuletzt optional
            // das Fixationskreuz. Diese Reihenfolge sorgt dafür, dass Punkte und
            // Kreuz vor dem Hintergrund gezeichnet werden.
            dotCount = DotCountForCurrentValues();
            int elementCount = 1 + dotCount + (showFixationTarget ? 1 : 0);
            var vertices = new Vector3[elementCount * 4];
            var uv = new Vector2[elementCount * 4];
            var uv2 = new Vector2[elementCount * 4];
            var colors = new Color32[elementCount * 4];
            var triangles = new int[elementCount * 6];
            var random = new System.Random(randomSeed);

            AddOneDot(0, Vector3.forward, 1f, BackgroundElement, fieldBackgroundColor,
                vertices, uv, uv2, colors, triangles);

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
                var direction = new Vector3(sine * Mathf.Cos(azimuth), sine * Mathf.Sin(azimuth), cosine);

                // Wichtig ist nur die Richtung. Der Abstand kommt erst durch
                // direction * fieldRadiusMeters dazu und ist reine Technik.
                Color color = random.NextDouble() < lightDotFraction ? lightColor : darkColor;
                AddOneDot(dotIndex + 1, direction, 1f, DotElement, color,
                    vertices, uv, uv2, colors, triangles);
            }

            // Das Kreuz kommt ganz zum Schluss dazu, genau geradeaus in der Mitte.
            if (showFixationTarget)
            {
                AddOneDot(dotCount + 1, Vector3.forward, fixationSizeDegrees / dotSizeDegrees,
                    FixationElement, fixationColor, vertices, uv, uv2, colors, triangles);
            }

            return new Mesh
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
                    Vector3.forward * fieldRadiusMeters * 0.5f, Vector3.one * fieldRadiusMeters * 2.2f)
            };
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
            //
            // In uv2 steht die Größe und die Art des Elements:
            // 0 = Punkt, 1 = Fixationskreuz, 2 = grauer Kreis-Hintergrund.
            //
            // Alle vier Ecken bekommen dieselbe Farbe, sonst würde der Punkt
            // einen Farbverlauf bekommen.
            var sizeData = new Vector2(sizeMultiplier, elementKind);
            Color32 packedColor = color;
            for (int corner = 0; corner < 4; corner++)
            {
                vertices[vertexIndex + corner] = center;
                uv2[vertexIndex + corner] = sizeData;
                colors[vertexIndex + corner] = packedColor;
            }

            // Über die uv-Werte weiß der Shader, welche Ecke er in welche Richtung
            // ziehen muss: links unten, rechts unten, rechts oben, links oben.
            uv[vertexIndex] = new Vector2(-1f, -1f);
            uv[vertexIndex + 1] = new Vector2(1f, -1f);
            uv[vertexIndex + 2] = new Vector2(1f, 1f);
            uv[vertexIndex + 3] = new Vector2(-1f, 1f);

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
            propertyBlock.SetFloat("_ApertureHalfAngleRad", 0.5f * fieldOfViewDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_ApertureEdgeSoftnessRad", edgeSoftnessDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_InstrumentDistortionK", instrumentDistortionK);
            propertyBlock.SetFloat("_InstrumentMagnificationM", instrumentMagnificationM);
            propertyBlock.SetFloat("_ContentZoom", contentZoom);
            propertyBlock.SetFloat("_EyeMode", (float)eyePresentation);
            propertyBlock.SetFloat("_DotsEnabled", pointsVisible ? 1f : 0f);
            propertyBlock.SetFloat("_DotHalfSizeRad", 0.5f * dotSizeDegrees * Mathf.Deg2Rad);
            // Nur eine unverzerrte Messlatte vor dem Kopf, nie Teil eines Trials.
            propertyBlock.SetFloat("_ReferenceGridEnabled", showReferenceGrid && !SessionRunning ? 1f : 0f);
            propertyBlock.SetFloat("_ReferenceGridSpacingUv",
                Mathf.Tan(referenceGridSpacingDegrees * Mathf.Deg2Rad));
            propertyBlock.SetFloat("_ReferenceGridWidthPixels", referenceGridWidthPixels);
            propertyBlock.SetColor("_ReferenceGridColor", referenceGridColor);
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
            propertyBlock.SetFloat("_SimulatedSweepRad", CurrentSimulatedSweepDegrees * Mathf.Deg2Rad);
            propertyBlock.SetFloat("_SimulatedSweepAxis", (float)sweepAxis);

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
            // Winkel in die Mesh-Erzeugung oder in den Shader. Die Set-Methoden oben
            // nutzen das ebenfalls, damit jede Grenze nur an dieser einen Stelle steht.
            fieldOfViewDegrees = Mathf.Clamp(fieldOfViewDegrees, 5f, 170f);
            edgeSoftnessDegrees = Mathf.Clamp(edgeSoftnessDegrees, 0f, 10f);
            fieldRadiusMeters = Mathf.Max(MinimumRadiusMeters, fieldRadiusMeters);
            worldCoverageDegrees = Mathf.Clamp(worldCoverageDegrees, 1f, MaximumCoverageDegrees);
            dotDensity = Mathf.Clamp(dotDensity, 0.01f, 1f);
            dotSizeDegrees = Mathf.Clamp(dotSizeDegrees, 0.02f, 2f);
            referenceGridSpacingDegrees = Mathf.Clamp(referenceGridSpacingDegrees, 0.5f, 20f);
            referenceGridWidthPixels = Mathf.Clamp(referenceGridWidthPixels, 0.5f, 4f);
            previewImageCenterSpeed = Mathf.Clamp(previewImageCenterSpeed, 0.5f, 120f);
            lightDotFraction = Mathf.Clamp01(lightDotFraction);
            instrumentDistortionK = Mathf.Clamp(instrumentDistortionK, 0f, 1.4f);
            instrumentMagnificationM = Mathf.Clamp(instrumentMagnificationM,
                MinimumInstrumentMagnification, MaximumInstrumentMagnification);
            contentZoom = Mathf.Clamp(contentZoom, MinimumContentZoom, MaximumContentZoom);
            fixationSizeDegrees = Mathf.Clamp(fixationSizeDegrees, 0.05f, 3f);
            simulatedYawAmplitudeDegrees = Mathf.Clamp(simulatedYawAmplitudeDegrees,
                MinimumSweepAmplitudeDegrees, MaximumSweepAmplitudeDegrees);
            simulatedYawSpeedDegreesPerSecond =
                Mathf.Clamp(simulatedYawSpeedDegreesPerSecond, MinimumSweepSpeed, MaximumSweepSpeed);
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

    public enum RandomDotSweepAxis
    {
        Horizontal = 0,
        Vertical = 1
    }

    // Worauf sich die Geschwindigkeit des simulierten Schwenks bezieht.
    public enum RandomDotSweepSpeedReference
    {
        [InspectorName("Objektwinkel (bisher): das Instrument schwenkt mit Sweep Speed")]
        ObjectAngle = 0,

        [InspectorName("Bildmitte: gleiche sichtbare Geschwindigkeit bei jedem m")]
        ImageCenter = 1
    }
}
