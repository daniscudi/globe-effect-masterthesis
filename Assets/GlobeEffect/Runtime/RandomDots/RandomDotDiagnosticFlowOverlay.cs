using System.Collections.Generic;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Eigenes, kopffestes Diagnoseblatt mit Geschwindigkeitsfarben und Pfeilen.
    /// Die Pfeile sind Messsymbole am gemeinsamen (u,v)-Gitter, genau wie im
    /// zweiten Python-Plot. Sie verändern NICHT die Bahnen der echten Marker.
    /// </summary>
    public sealed class RandomDotDiagnosticFlowOverlay : MonoBehaviour
    {
        private const int Resolution = 192;
        private Mesh mesh;
        private Texture2D texture;
        private Material material;
        private MeshRenderer surface;
        private Mesh arrowMesh;
        private Material arrowMaterial;
        private MeshRenderer arrowSurface;
        public double HorizontalEdgeRatio { get; private set; }
        public double VerticalEdgeRatio { get; private set; }
        public double MinimumRatio { get; private set; }
        public double MaximumRatio { get; private set; }

        public void Configure(RandomDotTrajectoryDiagnostic settings)
        {
            EnsureSurface();
            if (surface == null) return;
            float radius = Mathf.Tan(0.5f * settings.fieldOfViewDegrees * Mathf.Deg2Rad);
            double m = settings.magnification;
            double k = settings.distortionK;
            var axis = settings.sweepAxis;
            var coordinates = settings.flowCoordinates;
            HorizontalEdgeRatio = RandomDotDiagnosticFlowMath.RelativeSpeed(radius, 0d, m, k,
                axis, coordinates);
            VerticalEdgeRatio = RandomDotDiagnosticFlowMath.RelativeSpeed(0d, radius, m, k,
                axis, coordinates);
            MinimumRatio = double.PositiveInfinity;
            MaximumRatio = double.NegativeInfinity;
            var pixels = new Color[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                double u = (2d * x / (Resolution - 1) - 1d) * radius;
                double v = (2d * y / (Resolution - 1) - 1d) * radius;
                if (u * u + v * v > radius * radius) continue;
                double ratio = RandomDotDiagnosticFlowMath.RelativeSpeed(u, v, m, k, axis, coordinates);
                MinimumRatio = System.Math.Min(MinimumRatio, ratio);
                MaximumRatio = System.Math.Max(MaximumRatio, ratio);
                Color color = SpeedColor((float)ratio, settings.flowColorMinimum, settings.flowColorMaximum);
                color.a = settings.showSpeedHeatmap ? settings.heatmapOpacity : 0f;
                pixels[y * Resolution + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false);
            ConfigureMaterial(material, radius, settings);
            ConfigureMaterial(arrowMaterial, radius, settings);
            surface.enabled = settings.showSpeedHeatmap;
            arrowSurface.enabled = settings.showVelocityArrows;
            if (settings.showVelocityArrows) BuildArrows(radius, settings);
        }

        private static void ConfigureMaterial(Material target, float radius,
            RandomDotTrajectoryDiagnostic settings)
        {
            target.SetFloat("_TanHalfField", radius);
            target.SetFloat("_HalfFieldRad", 0.5f * settings.fieldOfViewDegrees * Mathf.Deg2Rad);
            target.SetFloat("_SoftnessRad", settings.edgeSoftnessDegrees * Mathf.Deg2Rad);
        }

        public static Color SpeedColor(float ratio, float minimum, float maximum)
        {
            float t = Mathf.Clamp01((ratio - minimum) / (maximum - minimum));
            // Blau: langsamer; grün: mittlerer Skalenwert; gelb: schneller.
            return t < 0.5f ? Color.Lerp(new Color(0.15f, 0.2f, 0.7f), Color.green, t * 2f)
                : Color.Lerp(Color.green, Color.yellow, (t - 0.5f) * 2f);
        }

        public static int OddGridSize(int count) => Mathf.Clamp(count, 5, 17) | 1;

        public static Vector2 ArrowVector(double x, double y, double magnification,
            float size, RandomDotSweepDirection direction)
        {
            float sign = direction == RandomDotSweepDirection.LeftFirst ? -1f : 1f;
            // Normierte Blattkoordinaten -1..1: 1-fache Mittengeschwindigkeit
            // ergibt 6 % Felddurchmesser. Keine automatische Skalierung pro Modus.
            return new Vector2((float)(x / magnification), (float)(y / magnification)) *
                (sign * 0.12f * size);
        }

        private void BuildArrows(float radius, RandomDotTrajectoryDiagnostic settings)
        {
            int count = OddGridSize(settings.arrowGridSize);
            var vertices = new List<Vector3>();
            var localPositions = new List<Vector2>();
            var dimensions = new List<Vector2>();
            var triangles = new List<int>();
            float width = 0.003f * settings.arrowThickness;
            float headHalfWidth = Mathf.Max(width * 2.2f, 0.012f * settings.arrowSize);
            const float padding = 0.008f;
            for (int row = 0; row < count; row++)
            for (int column = 0; column < count; column++)
            {
                var start = new Vector2(2f * column / (count - 1) - 1f,
                    2f * row / (count - 1) - 1f) * 0.9f;
                if (start.sqrMagnitude > 0.9f * 0.9f + 1e-6f) continue;
                var velocity = RandomDotDiagnosticFlowMath.Velocity(start.x * radius, start.y * radius,
                    settings.magnification,
                    settings.distortionK, settings.sweepAxis, settings.flowCoordinates);
                Vector2 delta = ArrowVector(velocity.x, velocity.y, settings.magnification,
                    settings.arrowSize, settings.sweepDirection);
                float length = delta.magnitude;
                if (length < 1e-6f) continue;
                Vector2 forward = delta / length;
                Vector2 sideways = new Vector2(-forward.y, forward.x);
                float headLength = Mathf.Min(0.036f * settings.arrowSize, length * 0.4f);
                int first = vertices.Count;
                // Eigene Vierecke statt Texturpixel: Der Shader zeichnet daraus
                // dünne Schäfte und gefüllte Spitzen, geglättet in Bildschirmauflösung.
                AddCorner(-padding, -headHalfWidth - padding);
                AddCorner(length + padding, -headHalfWidth - padding);
                AddCorner(length + padding, headHalfWidth + padding);
                AddCorner(-padding, headHalfWidth + padding);
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });

                void AddCorner(float x, float y)
                {
                    Vector2 point = start + forward * x + sideways * y;
                    vertices.Add(new Vector3(point.x, point.y, 0f));
                    localPositions.Add(new Vector2(x, y));
                    dimensions.Add(new Vector2(length, headLength));
                }
            }
            arrowMesh.Clear();
            arrowMesh.SetVertices(vertices);
            arrowMesh.SetUVs(0, localPositions);
            arrowMesh.SetUVs(1, dimensions);
            arrowMesh.SetTriangles(triangles, 0);
            arrowMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            arrowMaterial.SetFloat("_ArrowWidth", width);
            arrowMaterial.SetFloat("_HeadHalfWidth", headHalfWidth);
            arrowMaterial.SetColor("_ArrowColor", settings.arrowColor);
        }

        private void EnsureSurface()
        {
            if (surface != null) return;
            material = UnityTools.CreateShaderMaterial("GlobeEffectDiagnosticFlowOverlay",
                "GlobeEffect/Diagnostic Flow Overlay", "Diagnostic Flow Overlay", this);
            if (material == null) return;
            texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            material.SetTexture("_FlowMap", texture);
            mesh = new Mesh
            {
                hideFlags = HideFlags.DontSave,
                vertices = new[] { new Vector3(-1, -1), new Vector3(1, -1),
                    new Vector3(1, 1), new Vector3(-1, 1) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                bounds = new Bounds(Vector3.zero, Vector3.one * 10000f)
            };
            arrowMesh = new Mesh { name = "Diagnostic Flow Arrows", hideFlags = HideFlags.DontSave };
            arrowMaterial = new Material(material) { hideFlags = HideFlags.DontSave };
            arrowMaterial.SetFloat("_Arrows", 1f);
            // Zwei eigene Ebenen: hinten die Farbfläche, davor die Pfeile.
            surface = UnityTools.CreateMeshRenderer(transform, "Speed heatmap", mesh, material, -2);
            arrowSurface = UnityTools.CreateMeshRenderer(transform, "Sharp velocity arrows", arrowMesh,
                arrowMaterial, -1);
        }

        private void OnDestroy()
        {
            UnityTools.DeleteObject(mesh);
            UnityTools.DeleteObject(texture);
            UnityTools.DeleteObject(material);
            UnityTools.DeleteObject(arrowMesh);
            UnityTools.DeleteObject(arrowMaterial);
        }
    }
}
