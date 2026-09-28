using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Kleines binokulares Trainingsdisplay vor dem Headset. Die zwei Marker
    /// zeigen die Sollbewegung und die gemessene Kopfbewegung auf getrennten
    /// Zeilen; im eigentlichen Versuch ist das Display ausgeblendet.
    /// </summary>
    public sealed class RandomDotHeadSweepTrainingView : IDisposable
    {
        private const float MarkerHalfTravelMeters = 0.18f;
        private readonly GameObject root;
        private readonly GameObject trackGroup;
        private readonly Transform targetMarker;
        private readonly Transform headMarker;
        private readonly TextMesh heading;
        private readonly TextMesh footer;
        private readonly AudioSource audioSource;
        private readonly AudioClip leftTone;
        private readonly AudioClip rightTone;
        private readonly List<Material> materials = new();

        public RandomDotHeadSweepTrainingView(Transform observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            root = new GameObject("Runtime Random Dot Head Training")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetParent(observer, false);
            root.transform.localPosition = new Vector3(0f, 0f, 2f);
            root.transform.localRotation = Quaternion.identity;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = CreateText("Heading", font, 0.30f, 0.010f);
            footer = CreateText("Footer", font, -0.43f, 0.007f);

            trackGroup = new GameObject("Motion Guide");
            trackGroup.transform.SetParent(root.transform, false);
            Material trackMaterial = CreateMaterial(new Color(0.12f, 0.12f, 0.12f));
            Material targetMaterial = CreateMaterial(new Color(0f, 0.55f, 0.9f));
            Material headMaterial = CreateMaterial(new Color(1f, 0.75f, 0f));
            CreatePrimitive("Target Track", PrimitiveType.Cube,
                new Vector3(0f, -0.15f, 0f),
                new Vector3(0.43f, 0.008f, 0.008f), trackMaterial);
            CreatePrimitive("Head Track", PrimitiveType.Cube,
                new Vector3(0f, -0.30f, 0f),
                new Vector3(0.43f, 0.008f, 0.008f), trackMaterial);
            CreatePrimitive("Center Tick", PrimitiveType.Cube,
                new Vector3(0f, -0.225f, -0.005f),
                new Vector3(0.006f, 0.17f, 0.006f), trackMaterial);
            targetMarker = CreatePrimitive("Blue Target", PrimitiveType.Sphere,
                new Vector3(0f, -0.15f, -0.02f),
                Vector3.one * 0.035f, targetMaterial);
            headMarker = CreatePrimitive("Yellow Head", PrimitiveType.Sphere,
                new Vector3(0f, -0.30f, -0.02f),
                Vector3.one * 0.035f, headMaterial);

            audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.18f;
            leftTone = CreateTone("Random Dot Left Turn", 440f);
            rightTone = CreateTone("Random Dot Right Turn", 660f);
            Hide();
        }

        public void ShowInstructions(float amplitudeDegrees)
        {
            root.SetActive(true);
            trackGroup.SetActive(false);
            heading.text = "HEAD MOVEMENT PRACTICE\n\n" +
                "Follow the blue target by turning your head.\n" +
                "Yellow shows your own head position.\n" +
                "Look at the central cross and turn only " +
                amplitudeDegrees.ToString("F1") + " deg to each side.";
            footer.text = "F5 = START PRACTICE     F6 = STOP";
        }

        public void ShowPractice(int goodSweeps, int requiredSweeps,
            RandomDotSweepDirection direction)
        {
            root.SetActive(true);
            trackGroup.SetActive(true);
            heading.text = "EYES ON CROSS - MOVE YOUR HEAD\n" +
                (direction == RandomDotSweepDirection.RightFirst
                    ? "Start toward the right"
                    : "Start toward the left");
            footer.text = "BLUE = TARGET     YELLOW = HEAD\n" +
                "Good sweeps: " + goodSweeps + " / " + requiredSweeps;
        }

        public void ShowCentering(float actualYawDegrees, float amplitudeDegrees)
        {
            root.SetActive(true);
            trackGroup.SetActive(true);
            heading.text = "RETURN YOUR HEAD TO CENTER";
            footer.text = "Align yellow with the center mark";
            UpdateMarkers(0f, actualYawDegrees, amplitudeDegrees);
        }

        public void ShowFeedback(string message, int goodSweeps,
            int requiredSweeps)
        {
            root.SetActive(true);
            trackGroup.SetActive(false);
            heading.text = message;
            footer.text = "Good sweeps: " + goodSweeps + " / " + requiredSweeps;
        }

        public void ShowCompleted()
        {
            root.SetActive(true);
            trackGroup.SetActive(false);
            heading.text = "PRACTICE COMPLETE";
            footer.text = "The random dots will start shortly.";
        }

        public void UpdateMarkers(float targetYawDegrees, float actualYawDegrees,
            float amplitudeDegrees)
        {
            float scale = MarkerHalfTravelMeters / Mathf.Max(0.1f,
                amplitudeDegrees);
            Vector3 targetPosition = targetMarker.localPosition;
            targetPosition.x = Mathf.Clamp(targetYawDegrees * scale, -0.25f, 0.25f);
            targetMarker.localPosition = targetPosition;

            Vector3 headPosition = headMarker.localPosition;
            headPosition.x = Mathf.Clamp(actualYawDegrees * scale, -0.25f, 0.25f);
            headMarker.localPosition = headPosition;
        }

        public void PlayTurnCue(float targetYawDegrees)
        {
            audioSource.PlayOneShot(targetYawDegrees >= 0f ? rightTone : leftTone);
        }

        public void Hide()
        {
            audioSource.Stop();
            root.SetActive(false);
        }

        public void Dispose()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }

            foreach (Material material in materials)
            {
                UnityEngine.Object.Destroy(material);
            }

            UnityEngine.Object.Destroy(leftTone);
            UnityEngine.Object.Destroy(rightTone);
        }

        private TextMesh CreateText(string name, Font font, float y, float size)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, y, -0.08f);
            TextMesh mesh = textObject.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.lineSpacing = 1.1f;
            mesh.color = Color.black;
            mesh.richText = false;
            if (font != null)
            {
                var material = new Material(font.material)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = 5000
                };
                materials.Add(material);
                textObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return mesh;
        }

        private Transform CreatePrimitive(string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(trackGroup.transform, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            return primitive.transform;
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color,
                renderQueue = 4000
            };
            materials.Add(material);
            return material;
        }

        private static AudioClip CreateTone(string name, float frequencyHz)
        {
            const int sampleRate = 44100;
            const int sampleCount = 4410;
            var samples = new float[sampleCount];
            for (int index = 0; index < samples.Length; index++)
            {
                float time = (float)index / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * index / (sampleCount - 1));
                samples[index] = 0.35f * envelope *
                    Mathf.Sin(2f * Mathf.PI * frequencyHz * time);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
