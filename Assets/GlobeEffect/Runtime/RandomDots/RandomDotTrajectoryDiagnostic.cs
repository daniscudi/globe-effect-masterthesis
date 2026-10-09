using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Technische Demonstration, kein Experiment. Ein Raster aus bekannten
    /// Richtungen (voreingestellt 3 x 3) wird mit dem ORIGINAL-Random-Dot-Shader
    /// gezeichnet. Die Spuren sind dieselben Richtungen bei früheren
    /// Schwenkwinkeln, keine vorgegebenen horizontalen Bahnen.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class RandomDotTrajectoryDiagnostic : MonoBehaviour
    {
        [Header("Instrument – live einstellbar; Änderungen starten neu")]
        [Range(0f, 2f)] public float distortionK = 1f;
        [Range(1f, 20f)] public float magnification = 10f;
        [Range(20f, 100f)] public float fieldOfViewDegrees = 60f;
        [Range(0f, 10f)] public float edgeSoftnessDegrees = 1f;
        public RandomDotSweepAxis sweepAxis = RandomDotSweepAxis.Horizontal;
        public RandomDotSweepDirection sweepDirection = RandomDotSweepDirection.LeftFirst;

        [Header("Einseitiger Schwenk – gleiche Zeitfunktion wie im Experiment")]
        [Range(0.5f, 60f)] public float imageCenterSpeed = 12f;
        [Range(0.2f, 8f)] public float durationSeconds = 2f;
        [Range(0.05f, 2f)] public float playbackSpeed = 1f;
        public bool loop;

        [Header("Punkte und Referenzraster")]
        [Tooltip("Wie viele Spalten Punkte nebeneinander. Jede Spalte hat ihre eigene Farbe, von rot über grün bis blau.")]
        [Range(1, 15)] public int markerColumns = 3;
        [Tooltip("Wie viele Punkte übereinander in jeder Spalte. Nach oben werden sie heller.")]
        [Range(1, 15)] public int markerRows = 3;
        [Tooltip("Abstand der Spalten/Zeilen in der unveränderten mittleren Blickstellung. Punkte, die damit über 80 Grad hinaus liegen würden, fallen weg.")]
        [Range(2f, 25f)] public float markerSpacingDegrees = 10f;
        [Range(0.1f, 2f)] public float markerSizeDegrees = 0.45f;
        [Range(10, 160)] public int trailSamples = 80;
        public bool showTrails = true;
        public bool showReferenceGrid = true;
        [Range(1f, 15f)] public float gridSpacingDegrees = 5f;
        public bool showDesktopInstructions = true;

        [Header("Optic-Flow-Diagnose – nur Messfarben, keine neue Verzeichnung")]
        public bool showSpeedHeatmap = true;
        public bool showVelocityArrows = true;
        [Tooltip("Berechnete Koordinatenraten für den laufenden Schwenk. Keine gemessene Wahrnehmung; " +
            "S/M-Pfeile sind Messsymbole, nicht Bildschirm-Bahnrichtungen. Farben bleiben bei Pause stehen.")]
        public RandomDotDiagnosticCoordinates flowCoordinates = RandomDotDiagnosticCoordinates.LinearImage;
        [Tooltip("Gemeinsame feste Farbskala. Werte über 1 NICHT abschneiden wie im alten Plot.")]
        [Range(0f, 0.99f)] public float flowColorMinimum = 0.7f;
        [Range(1.01f, 3f)] public float flowColorMaximum = 1.1f;
        [Range(0.1f, 0.8f)] public float heatmapOpacity = 0.45f;
        [Tooltip("Ungerade Rastergröße, damit ein Referenzpfeil genau in der Mitte liegt.")]
        [Range(5, 17)] public int arrowGridSize = 9;
        [Tooltip("Gemeinsame Pfeillänge für A/S/M; Länge bleibt proportional zur relativen Geschwindigkeit.")]
        [Range(0.3f, 2f)] public float arrowSize = 1f;
        [Range(0.5f, 3f)] public float arrowThickness = 1f;
        public Color arrowColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);

        private RandomDotFieldStimulus field;
        private MeshRenderer background;
        private MeshRenderer liveMarkers;
        private MeshRenderer[] trails;
        private Mesh markerMesh;
        private Mesh trailMesh;
        private Transform observer;
        private bool ownsCamera;
        private bool dirty = true;
        private bool paused;
        private double elapsed;
        private float amplitude;
        private float objectSpeed;
        private MaterialPropertyBlock values;
        private RandomDotDiagnosticFlowOverlay flowOverlay;
        private (float k, float m, float speed, float duration, float spacing, int columns, int rows,
            RandomDotSweepAxis axis, RandomDotSweepDirection direction) motionSettings;

        private void OnEnable()
        {
            dirty = true;
            Application.onBeforeRender += RefreshBeforeRender;
        }
        private void OnDisable()
        {
            Application.onBeforeRender -= RefreshBeforeRender;
            if (field != null) field.Hide();
            if (liveMarkers != null) liveMarkers.enabled = false;
            if (flowOverlay != null) flowOverlay.gameObject.SetActive(false);
            if (trails != null)
                foreach (var trail in trails)
                    if (trail != null) trail.enabled = false;
        }

        private void Awake()
        {
            // Unity-Objekte erst im Hauptthread anlegen, nicht im Feldinitialisierer.
            values = new MaterialPropertyBlock();
            // Eigenständige Szene: Desktop-Kamera; in XR zusätzlich Kopfpose lesen.
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Diagnostic Camera").AddComponent<Camera>();
                camera.gameObject.tag = "MainCamera";
                camera.transform.SetParent(transform, false);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.fieldOfView = 90f;
                camera.nearClipPlane = 0.01f;
                ownsCamera = true;
            }
            observer = camera.transform;
            var surface = new GameObject("Original Random-Dot Background");
            surface.transform.SetParent(transform, false);
            field = surface.AddComponent<RandomDotFieldStimulus>();
            field.Observer = observer;
            background = surface.GetComponent<MeshRenderer>();
            background.sortingOrder = -3;
            var overlayObject = new GameObject("Optic Flow – measurement overlay")
                { hideFlags = HideFlags.DontSave };
            overlayObject.transform.SetParent(field.transform, false);
            flowOverlay = overlayObject.AddComponent<RandomDotDiagnosticFlowOverlay>();
        }

        private void OnValidate()
        {
            // Die Grenzen der Regler hält Unity über [Range] selbst ein. Hier muss
            // nur noch das Pfeilraster ungerade werden, damit ein Pfeil genau in
            // der Mitte steht.
            arrowGridSize = RandomDotDiagnosticFlowOverlay.OddGridSize(arrowGridSize);
            dirty = true;
        }

        private void Update()
        {
            if (ownsCamera && XRSettings.isDeviceActive)
            {
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position))
                    observer.localPosition = position;
                if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation))
                    observer.localRotation = rotation;
            }
            if (dirty) Rebuild();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
            if (!paused)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                if (loop && elapsed > durationSeconds) elapsed %= durationSeconds;
                else elapsed = System.Math.Min(elapsed, durationSeconds);
            }
        }

        public void Restart()
        {
            elapsed = 0d;
            paused = false;
        }

        public void TogglePause() => paused = !paused;

        private void Rebuild()
        {
            var nextMotion = (distortionK, magnification, imageCenterSpeed, durationSeconds,
                markerSpacingDegrees, markerColumns, markerRows, sweepAxis, sweepDirection);
            bool restartMotion = nextMotion != motionSettings;
            motionSettings = nextMotion;
            ClearMarkers();
            objectSpeed = RandomDotSimulatedSweep.ObjectSpeed(RandomDotSweepSpeedReference.ImageCenter,
                1f, imageCenterSpeed, magnification, 1f);
            amplitude = objectSpeed * durationSeconds * 0.5f;
            field.SetInstrumentDistortionK(distortionK);
            field.SetInstrumentMagnification(magnification);
            field.SetContentZoom(1f);
            field.SetAngularDiameter(fieldOfViewDegrees);
            field.SetApertureEdgeSoftness(edgeSoftnessDegrees);
            field.SetMotionMode(RandomDotMotionMode.SimulatedYaw);
            field.SetSweepAxis(sweepAxis);
            field.SetSweepDirection(sweepDirection);
            // Hintergrund und Kreuz wiederverwenden, zufällige Punkte ausblenden.
            field.ConfigurePointField(1, 1f);
            field.ShowFixationOnly();
            markerMesh = CreateMarkerMesh(1f, 1f);
            trailMesh = CreateMarkerMesh(0.28f, 0.55f);
            liveMarkers = UnityTools.CreateMeshRenderer(field.transform, "Live markers", markerMesh,
                background.sharedMaterial, 1);
            trails = new MeshRenderer[trailSamples + 1];
            for (int i = 0; i < trails.Length; i++)
                trails[i] = UnityTools.CreateMeshRenderer(field.transform, "Trail sample " + i, trailMesh,
                    background.sharedMaterial, 0);
            flowOverlay.gameObject.SetActive(true);
            flowOverlay.Configure(this);
            dirty = false;
            // Der Wechsel des Messlineals lässt Punkte und Pausenzustand unverändert.
            if (restartMotion) Restart();
        }

        private Mesh CreateMarkerMesh(float size, float alpha)
        {
            // Erst alle Punkte sammeln, die sich sinnvoll zeichnen lassen. Jede
            // Spalte bekommt eine eigene Farbe von rot über grün bis blau, nach
            // oben werden die Punkte heller. Bei 3 x 3 ist das genau das alte Bild.
            var points = new List<(Vector3 direction, Color color)>();
            for (int column = 0; column < markerColumns; column++)
            for (int row = 0; row < markerRows; row++)
            {
                // Das Raster liegt symmetrisch um die Mitte. Bei einer geraden
                // Anzahl gibt es deshalb keine Spalte genau in der Mitte.
                Vector3 direction = MarkerObjectDirection(column - 0.5f * (markerColumns - 1),
                    row - 0.5f * (markerRows - 1), markerSpacingDegrees, magnification, distortionK);
                if (direction == Vector3.zero) continue;
                Color color = Color.HSVToRGB(Fraction(column, markerColumns) * 0.62f, 0.85f, 1f)
                    * (0.65f + 0.35f * Fraction(row, markerRows));
                color.a = alpha;
                points.Add((direction, color));
            }

            // Jeder Punkt wird genauso gebaut wie die Zufallspunkte im Versuch.
            var vertices = new Vector3[points.Count * 4];
            var uv = new Vector2[points.Count * 4];
            var sizes = new Vector2[points.Count * 4];
            var colors = new Color32[points.Count * 4];
            var indices = new int[points.Count * 6];
            for (int i = 0; i < points.Count; i++)
                RandomDotFieldStimulus.AddOneDot(i, points[i].direction * field.FieldRadiusMeters, size,
                    RandomDotFieldStimulus.DotElement, points[i].color, vertices, uv, sizes, colors, indices);
            return new Mesh
            {
                name = "Diagnostic direction markers", hideFlags = HideFlags.DontSave,
                vertices = vertices, uv = uv, uv2 = sizes, colors32 = colors, triangles = indices,
                bounds = new Bounds(Vector3.zero, Vector3.one * field.FieldRadiusMeters * 4f)
            };
        }

        // Wo im Raster man steht, von 0 (erste Spalte/Zeile) bis 1 (letzte).
        private static float Fraction(int index, int count) => count > 1 ? index / (count - 1f) : 0.5f;

        /// <summary>
        /// Nur die START-Anordnung zurückrechnen: senkrechte Spalten bei
        /// Schwenkwinkel null. Alle späteren Positionen berechnet der Originalshader.
        /// column und row zählen von der Mitte aus, in Rasterabständen.
        /// Liegt ein Punkt so weit außen, dass sich die Formel nicht mehr
        /// zurückrechnen lässt (über 80 Grad oder hinter dem Umkehrpunkt von k),
        /// kommt Vector3.zero zurück und der Punkt wird weggelassen.
        /// </summary>
        public static Vector3 MarkerObjectDirection(float column, float row, float spacing,
            float m, float k)
        {
            if (Mathf.Abs(column * spacing) >= 80f || Mathf.Abs(row * spacing) >= 80f)
                return Vector3.zero;
            var displayed = new Vector2(Mathf.Tan(column * spacing * Mathf.Deg2Rad),
                Mathf.Tan(row * spacing * Mathf.Deg2Rad));
            float radius = displayed.magnitude;
            if (radius < 1e-6f) return Vector3.forward;
            float apparent = Mathf.Atan(radius);
            if (apparent >= 80f * Mathf.Deg2Rad || k * apparent >= 89f * Mathf.Deg2Rad)
                return Vector3.zero;
            float angle = (float)MerlitzBinocularReferenceMath.ObjectAngleFromApparent(apparent, m, k);
            Vector2 source = displayed * (Mathf.Tan(angle) / radius);
            return new Vector3(source.x, source.y, 1f).normalized;
        }

        private void LateUpdate()
        {
            if (liveMarkers == null || background == null) return;
            // Auch nach einem Editor-Script-Reload muss der native Block existieren.
            values ??= new MaterialPropertyBlock();
            field.ShowFixationOnly();
            liveMarkers.enabled = true;
            // Nach dem Stimulus-Update: dieselben Kopf-/Optikwerte für alle Renderer.
            background.GetPropertyBlock(values);
            values.SetFloat("_ReferenceGridEnabled", showReferenceGrid ? 1f : 0f);
            values.SetFloat("_ReferenceGridSpacingUv", Mathf.Tan(gridSpacingDegrees * Mathf.Deg2Rad));
            background.SetPropertyBlock(values);
            values.SetFloat("_DotsEnabled", 1f);
            values.SetFloat("_DotHalfSizeRad", markerSizeDegrees * 0.5f * Mathf.Deg2Rad);
            DrawAtTime(liveMarkers, elapsed);
            for (int i = 0; i < trails.Length; i++)
            {
                double sampleTime = durationSeconds * (double)i / trailSamples;
                trails[i].enabled = showTrails && sampleTime <= elapsed;
                if (trails[i].enabled) DrawAtTime(trails[i], sampleTime);
            }
        }

        // Nach der späten Kopfaktualisierung des Stimulus auch die Spur-Renderer
        // auffrischen, damit sie in XR nicht mit alten Kopfwerten gezeichnet werden.
        [BeforeRenderOrder(100)]
        private void RefreshBeforeRender()
        {
            if (isActiveAndEnabled) LateUpdate();
        }

        private void DrawAtTime(MeshRenderer renderer, double seconds)
        {
            float sweep = RandomDotSimulatedSweep.EvaluateOneWayDegrees(
                seconds, amplitude, objectSpeed, sweepDirection);
            values.SetFloat("_SimulatedSweepRad", sweep * Mathf.Deg2Rad);
            renderer.SetPropertyBlock(values);
        }

        private void OnGUI()
        {
            if (!showDesktopInstructions) return;
            GUI.Box(new Rect(10, 10, 650, 195), "Random-Dot-Diagnose – keine Messung");
            GUI.Label(new Rect(20, 35, 580, 25),
                "Leertaste: Pause | R: Neustart | Einstellungen live im Inspector");
            GUI.Label(new Rect(20, 60, 580, 25),
                $"k={distortionK:F2}, m={magnification:F1} | t={elapsed:F2}/{durationSeconds:F2} s");
            GUI.Label(new Rect(20, 85, 580, 25),
                $"{markerColumns} x {markerRows} Punkte, Spalten von rot bis blau. Raster bleibt fest. " +
                "Spuren zeigen frühere Positionen.");
            string coordinateLabel = flowCoordinates == RandomDotDiagnosticCoordinates.LinearImage
                ? "A: flacher Bildraum" : flowCoordinates == RandomDotDiagnosticCoordinates.SeparateAngles
                    ? "S: getrennte Winkelumrechnung" : "M: radiale Winkelumrechnung (l = 0)";
            GUI.Label(new Rect(20, 110, 620, 25), "Messlineal: " + coordinateLabel +
                " | Betrag relativ zur Mitte = 1");
            if (flowOverlay != null)
                GUI.Label(new Rect(20, 135, 620, 25),
                    $"Rand links/rechts: {flowOverlay.HorizontalEdgeRatio:F4} | " +
                    $"Rand oben/unten: {flowOverlay.VerticalEdgeRatio:F4} | " +
                    $"Feld: {flowOverlay.MinimumRatio:F3}–{flowOverlay.MaximumRatio:F3}");
            Color previousColor = GUI.color;
            for (int i = 0; i < 100; i++)
            {
                float ratio = Mathf.Lerp(flowColorMinimum, flowColorMaximum, i / 99f);
                GUI.color = RandomDotDiagnosticFlowOverlay.SpeedColor(ratio,
                    flowColorMinimum, flowColorMaximum);
                GUI.DrawTexture(new Rect(20 + 3 * i, 166, 3, 8), Texture2D.whiteTexture);
            }
            GUI.color = previousColor;
            GUI.Label(new Rect(20, 178, 620, 22),
                $"Skala {flowColorMinimum:F2} bis {flowColorMaximum:F2}. " +
                "Pfeile = Koordinatenraten; kein Beweis einer wahrgenommenen Form.");
        }

        private void ClearMarkers()
        {
            if (liveMarkers != null) Destroy(liveMarkers.gameObject);
            if (trails != null)
                foreach (var trail in trails)
                    if (trail != null) Destroy(trail.gameObject);
            if (markerMesh != null) Destroy(markerMesh);
            if (trailMesh != null) Destroy(trailMesh);
        }

        private void OnDestroy() => ClearMarkers();
    }
}
