using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Kleines binokulares Trainingsdisplay vor dem Headset. Die zwei Marker
    /// zeigen die Sollbewegung und die gemessene Kopfbewegung auf getrennten
    /// Zeilen; im eigentlichen Versuch ist das Display ausgeblendet. Vor dem
    /// ersten Durchgang zeigt es außerdem, welche Taste welche Antwort gibt.
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

            // Das Display hängt am Kopf und steht immer 2 m vor den Augen.
            root = new GameObject("Runtime Random Dot Head Training");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(observer, false);
            root.transform.localPosition = new Vector3(0f, 0f, 2f);
            root.transform.localRotation = Quaternion.identity;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = CreateText("Heading", font, 0.30f, 0.010f);
            footer = CreateText("Footer", font, -0.43f, 0.007f);

            // Zwei graue Schienen: oben läuft das blaue Ziel, unten der gelbe Kopf.
            // Der senkrechte Strich in der Mitte zeigt die Geradeaus-Stellung.
            trackGroup = new GameObject("Motion Guide");
            trackGroup.transform.SetParent(root.transform, false);
            Material trackMaterial = CreateMaterial(new Color(0.12f, 0.12f, 0.12f));
            var trackSize = new Vector3(0.43f, 0.008f, 0.008f);
            Vector3 markerSize = Vector3.one * 0.035f;
            CreatePrimitive("Target Track", PrimitiveType.Cube,
                new Vector3(0f, -0.15f, 0f), trackSize, trackMaterial);
            CreatePrimitive("Head Track", PrimitiveType.Cube,
                new Vector3(0f, -0.30f, 0f), trackSize, trackMaterial);
            CreatePrimitive("Center Tick", PrimitiveType.Cube,
                new Vector3(0f, -0.225f, -0.005f), new Vector3(0.006f, 0.17f, 0.006f), trackMaterial);
            targetMarker = CreatePrimitive("Blue Target", PrimitiveType.Sphere,
                new Vector3(0f, -0.15f, -0.02f), markerSize, CreateMaterial(new Color(0f, 0.55f, 0.9f)));
            headMarker = CreatePrimitive("Yellow Head", PrimitiveType.Sphere,
                new Vector3(0f, -0.30f, -0.02f), markerSize, CreateMaterial(new Color(1f, 0.75f, 0f)));

            // Ein kurzer Ton bei jeder Wende: tief für links, hoch für rechts.
            audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.18f;
            leftTone = CreateTone("Random Dot Left Turn", 440f);
            rightTone = CreateTone("Random Dot Right Turn", 660f);
            Hide();
        }

        // responseLines sind die beiden Zeilen mit der Tastenbelegung. Der
        // Experiment Manager baut sie aus der eingestellten Zuordnung.
        public void ShowWelcome(string startKey, string otherKey, string trainingKey, string previewKey,
            RandomDotMotionMode mode, bool completed = false)
        {
            string practice = mode == RandomDotMotionMode.HeadTracked ? "ACTIVE HEAD MOVEMENT" : "SIMULATED PANNING";
            string other = mode == RandomDotMotionMode.HeadTracked ? "SIMULATED PANNING" : "ACTIVE HEAD MOVEMENT";
            Show(false, completed ? "SESSION FINISHED AND SAVED" : "RANDOM DOT EXPERIMENT",
                startKey + " = NEW SESSION: " + practice + "\n" +
                otherKey + " = NEW SESSION: " + other + "\n" +
                trainingKey + " = PRACTICE: " + practice + "\n" +
                previewKey + " = PREVIEW (NO DATA)");
        }

        public void ShowSimulatedInstructions(string responseLines, string startKey, bool feedback = false,
            bool examples = false)
        {
            Show(false, "SIMULATED PANNING PRACTICE\n\n" +
                "Keep your head still and look at the cross.\n" +
                "The computer moves the dots.\n" +
                "After they disappear: did the field curve outward or inward?" +
                (examples ? "\nFirst you will see one labelled example of each." : string.Empty) +
                (feedback ? "\nFor clear examples you will see the expected answer." : string.Empty),
                responseLines + "\n\n" + startKey + " = START PRACTICE");
        }

        public void ShowSimulatedResponse(int trial, int total, string responseLines)
        {
            Show(false, "PRACTICE " + trial + " / " + total + "\n\nOUTWARD OR INWARD?", responseLines);
        }

        public void ShowBlockPause(int finishedBlock, int blockCount)
        {
            // Überschrift über, Fußzeile unter dem Kreuz: das rote Kreuz bleibt frei sichtbar.
            Show(false, "SHORT BREAK\n\nBlock " + finishedBlock + " of " + blockCount + " done.",
                "Rest a moment if you like.\nThe experimenter will continue.");
        }

        public void ShowSimulatedExample(string categoryLabel, string note)
        {
            Show(false, "EXAMPLE: " + categoryLabel + "\n\n" + note, string.Empty);
        }

        public void ShowSimulatedPracticeStart(bool feedback)
        {
            Show(false, "NOW PRACTISE\n\n" + (feedback
                    ? "For clear examples you will see the expected answer."
                    : "No hints from now on. Answer what you see."),
                string.Empty);
        }

        public void ShowSimulatedFeedback(bool correct, string expectedLabel)
        {
            Show(false, (correct ? "CORRECT" : "NOT QUITE") + "\n\nThis example was clearly\n" + expectedLabel + ".",
                string.Empty);
        }

        public void ShowActiveMotionInstructions(bool free, bool openEnded, string responseLines, string startKey)
        {
            if (free && openEnded)
            {
                // Offene aktive Trials: kein Zeitlimit, die Antwort beendet den Trial.
                Show(false, "ACTIVE HEAD MOVEMENT\n\n" +
                    "Look around freely and move your head in any direction,\n" +
                    "as long as you like.\n" +
                    "Answer whenever you are ready. Then the next trial starts.",
                    responseLines + "\n\n" + startKey + " = START TRIALS");
                return;
            }

            string movement = free
                ? "Move your head freely: left/right or up/down.\nNo target path or speed is required."
                : "Use the left/right rhythm you practised.";
            Show(false, "ACTIVE HEAD MOVEMENT\n\n" + movement + "\n" +
                "Keep looking at the cross.\nAnswer after the dots disappear.",
                responseLines + "\n\n" + startKey + " = START TRIALS");
        }

        public void ShowResponseInstructions(string responseLines, string startKeyName, bool openEnded = false)
        {
            Show(false,
                "RANDOM DOT EXPERIMENT\n\n" +
                (openEnded
                    ? "Look around freely, as long as you like.\nAnswer whenever you are ready."
                    : "Keep looking at the cross.\nAnswer after the dots have disappeared."),
                responseLines + "\n\n" + startKeyName + " = START");
        }

        public void ShowInstructions(float amplitudeDegrees, string responseLines,
            string startKey = "F5", string stopKey = "F6")
        {
            Show(false,
                "HEAD MOVEMENT PRACTICE\n\n" +
                "Follow the blue target by turning your head.\n" +
                "Yellow shows your own head position.\n" +
                "Look at the central cross and turn only " +
                amplitudeDegrees.ToString("F1") + " deg to each side.",
                "After each trial answer with:\n" + responseLines + "\n\n" +
                startKey + " = START PRACTICE     " + stopKey + " = STOP");
        }

        public void ShowPractice(int goodSweeps, int requiredSweeps, RandomDotSweepDirection direction)
        {
            string side = direction == RandomDotSweepDirection.RightFirst ? "right" : "left";
            Show(true,
                "EYES ON CROSS - MOVE YOUR HEAD\nStart toward the " + side,
                "BLUE = TARGET     YELLOW = HEAD\n" + GoodSweepsText(goodSweeps, requiredSweeps));
        }

        public void ShowCentering(float actualYawDegrees, float amplitudeDegrees)
        {
            Show(true, "RETURN YOUR HEAD TO CENTER", "Align yellow with the center mark");
            UpdateMarkers(0f, actualYawDegrees, amplitudeDegrees);
        }

        public void ShowFeedback(string message, int goodSweeps, int requiredSweeps)
        {
            Show(false, message, GoodSweepsText(goodSweeps, requiredSweeps));
        }

        public void ShowCompleted()
        {
            Show(false, "PRACTICE COMPLETE", "The random dots will start shortly.");
        }

        public void UpdateMarkers(float targetYawDegrees, float actualYawDegrees, float amplitudeDegrees)
        {
            // Der volle Ausschlag zur Seite entspricht 18 cm auf der Schiene.
            float scale = MarkerHalfTravelMeters / Mathf.Max(0.1f, amplitudeDegrees);
            MoveMarker(targetMarker, targetYawDegrees * scale);
            MoveMarker(headMarker, actualYawDegrees * scale);
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
                Object.Destroy(root);
            }

            foreach (Material material in materials)
            {
                Object.Destroy(material);
            }

            Object.Destroy(leftTone);
            Object.Destroy(rightTone);
        }

        // Macht das Display sichtbar und setzt beide Texte.
        // showTrack sagt, ob die Schienen mit den Kugeln zu sehen sind.
        private void Show(bool showTrack, string headingText, string footerText)
        {
            root.SetActive(true);
            trackGroup.SetActive(showTrack);
            heading.text = headingText;
            footer.text = footerText;
        }

        private static string GoodSweepsText(int goodSweeps, int requiredSweeps)
        {
            return "Good sweeps: " + goodSweeps + " / " + requiredSweeps;
        }

        // Schiebt eine Kugel nach links oder rechts, aber nie über die Schiene hinaus.
        private static void MoveMarker(Transform marker, float x)
        {
            Vector3 position = marker.localPosition;
            position.x = Mathf.Clamp(x, -0.25f, 0.25f);
            marker.localPosition = position;
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
                // Eigenes Material mit hoher Render-Reihenfolge, damit der Text
                // immer vor allem anderen gezeichnet wird.
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

        private Transform CreatePrimitive(
            string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(trackGroup.transform, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;

            // Die Formen sind nur zum Anschauen. Ein Collider wird nicht gebraucht.
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            return primitive.transform;
        }

        private Material CreateMaterial(Color color)
        {
            var material = new Material(Shader.Find("Unlit/Color"))
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
            // 0,1 s Sinuston, der sanft ein- und ausgeblendet wird, damit er nicht knackt.
            const int sampleRate = 44100;
            const int sampleCount = 4410;
            var samples = new float[sampleCount];
            for (int index = 0; index < samples.Length; index++)
            {
                float time = (float)index / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * index / (sampleCount - 1));
                samples[index] = 0.35f * envelope * Mathf.Sin(2f * Mathf.PI * frequencyHz * time);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
