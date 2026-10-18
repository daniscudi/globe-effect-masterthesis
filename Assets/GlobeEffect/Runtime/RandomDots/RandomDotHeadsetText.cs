using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Texte im Headset: Startbildschirm, Anleitungen, Übungen und Pausen. Zwei
    /// Textzeilen hängen am Kopf, eine Überschrift über und eine Fußzeile unter dem
    /// Kreuz, damit das Kreuz frei sichtbar bleibt. Während der Punkte ist alles aus.
    /// </summary>
    public sealed class RandomDotHeadsetText : IDisposable
    {
        // Für diese Entfernung sind Positionen und Schriftgrößen unten gewählt.
        private const float LayoutDistanceMeters = 2f;

        private readonly GameObject root;
        private readonly TextMesh heading;
        private readonly TextMesh footer;
        private readonly Material headingMaterial;
        private readonly Material footerMaterial;

        public RandomDotHeadsetText(Transform observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            // Der Text hängt am Kopf. Das Layout ist für 2 m gebaut; er steht aber
            // kurz vor der Fernebene und ist im selben Verhältnis größer. So sieht er
            // gleich groß aus und liegt auf derselben Tiefe wie das Kreuz (unendlich).
            float distance = UnityTools.FarTextDistance(observer);
            root = new GameObject("Runtime Random Dot Headset Text");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(observer, false);
            root.transform.localPosition = new Vector3(0f, 0f, distance);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * (distance / LayoutDistanceMeters);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            heading = CreateText("Heading", font, 0.30f, 0.010f, out headingMaterial);
            footer = CreateText("Footer", font, -0.43f, 0.007f, out footerMaterial);
            Hide();
        }

        public void ShowWelcome(string startKey, string otherKey, string trainingKey, string previewKey,
            RandomDotMotionMode mode, bool completed = false)
        {
            string practice = mode == RandomDotMotionMode.HeadTracked ? "ACTIVE HEAD MOVEMENT" : "SIMULATED PANNING";
            string other = mode == RandomDotMotionMode.HeadTracked ? "SIMULATED PANNING" : "ACTIVE HEAD MOVEMENT";
            Show(completed ? "SESSION FINISHED AND SAVED" : "RANDOM DOT EXPERIMENT",
                startKey + " = NEW SESSION: " + practice + "\n" +
                otherKey + " = NEW SESSION: " + other + "\n" +
                trainingKey + " = PRACTICE: " + practice + "\n" +
                previewKey + " = PREVIEW (NO DATA)");
        }

        // responseLines sind die beiden Zeilen mit der Tastenbelegung. Der
        // Experiment Manager baut sie aus der eingestellten Zuordnung.
        public void ShowResponseInstructions(string responseLines, string startKeyName)
        {
            Show("RANDOM DOT EXPERIMENT\n\n" +
                "Keep looking at the cross.\nAnswer after the dots have disappeared.",
                responseLines + "\n\n" + startKeyName + " = START");
        }

        public void ShowBlockPause(int finishedBlock, int blockCount)
        {
            // Überschrift über, Fußzeile unter dem Kreuz: das rote Kreuz bleibt frei sichtbar.
            Show("SHORT BREAK\n\nBlock " + finishedBlock + " of " + blockCount + " done.",
                "Rest a moment if you like.\nThe experimenter will continue.");
        }

        // ---- Simulated: Übung ----

        public void ShowSimulatedInstructions(string responseLines, string startKey, bool feedback = false,
            bool examples = false)
        {
            Show("SIMULATED PANNING PRACTICE\n\n" +
                "Keep your head still and look at the cross.\n" +
                "The computer moves the dots.\n" +
                "After they disappear: did the field curve outward or inward?" +
                (examples ? "\nFirst you will see one labelled example of each." : string.Empty) +
                (feedback ? "\nFor clear examples you will see the expected answer." : string.Empty),
                responseLines + "\n\n" + startKey + " = START PRACTICE");
        }

        public void ShowSimulatedExample(string categoryLabel, string note)
        {
            Show("EXAMPLE: " + categoryLabel + "\n\n" + note, string.Empty);
        }

        public void ShowSimulatedPracticeStart(bool feedback)
        {
            Show("NOW PRACTISE\n\n" + (feedback
                    ? "For clear examples you will see the expected answer."
                    : "No hints from now on. Answer what you see."),
                string.Empty);
        }

        public void ShowSimulatedResponse(int trial, int total, string responseLines)
        {
            Show("PRACTICE " + trial + " / " + total + "\n\nOUTWARD OR INWARD?", responseLines);
        }

        public void ShowSimulatedFeedback(bool correct, string expectedLabel)
        {
            Show((correct ? "CORRECT" : "NOT QUITE") + "\n\nThis example was clearly\n" + expectedLabel + ".",
                string.Empty);
        }

        // ---- Active: Anleitung der echten Trials und Übung ----

        public void ShowActiveMotionInstructions(string responseLines, string startKey, HeadVelocityAxis axis,
            bool alternate)
        {
            Show("ACTIVE HEAD MOVEMENT\n\n" + ActiveTrialRules(axis, alternate),
                responseLines + "\n\n" + startKey + " = START TRIALS");
        }

        // Die Regel der aktiven Trials, gleich in der Anleitung und vor Practice 2.
        private static string ActiveTrialRules(HeadVelocityAxis axis, bool alternate)
        {
            string stopRule = "If you stop or turn back too early, the dots vanish.\n" +
                "Answer anyway. The trial comes again later.";
            if (alternate)
            {
                // Ein Schwenk pro Trial, abwechselnd: hin, antworten, zurück, antworten.
                string sides = axis == HeadVelocityAxis.Horizontal ? "to one side" : "up or down";
                string back = axis == HeadVelocityAxis.Horizontal ? "back to the other side" : "back the other way";
                return "Keep looking at the cross. Each trial is one sweep:\n" +
                    "turn your head " + sides + ", at a natural speed.\n" +
                    "The dots appear while you turn. Keep turning until they disappear.\n" +
                    "Then stop and answer. Next trial: sweep " + back + ".\n" + stopRule;
            }

            string direction = axis == HeadVelocityAxis.Horizontal ? "left or right" : "up or down";
            return "Keep looking at the cross and turn your head\n" +
                direction + ", at a speed that feels natural.\n" +
                "The dots appear while you turn. Keep turning until they disappear.\n" +
                "Then answer.\n" + stopRule;
        }

        public void ShowActivePracticeInstructions(string responseLines, string startKey, bool speedExample,
            bool freeMovement)
        {
            string steps = (speedExample ? "- An example of a calm speed. Keep your head still for it.\n" : "") +
                (freeMovement ? "- Practice 1: turn your head freely and get a feel for the dots.\n" : "") +
                "- Practice 2: trials like in the test.";
            Show("ACTIVE HEAD MOVEMENT PRACTICE\n\n" + steps,
                "In practice 2, answer after the dots with:\n" + responseLines + "\n\n" +
                startKey + " = START PRACTICE");
        }

        public void ShowActiveSpeedExample()
        {
            Show("AN EXAMPLE OF A CALM SPEED\n\nKeep your head still and look at the cross.\n" +
                "The computer moves the dots.", string.Empty);
        }

        public void ShowFreeMovementPracticeStart(HeadVelocityAxis axis)
        {
            string direction = axis == HeadVelocityAxis.Horizontal ? "left and right" : "up and down";
            Show("PRACTICE 1: FREE HEAD MOVEMENT\n\n" +
                "Look at the cross and turn your head " + direction + ",\n" +
                "like looking around with binoculars.\n" +
                "The dots stay while your head moves\nand fade out when it stops.\n" +
                "No answers. The experimenter ends this part.", string.Empty);
        }

        public void ShowTrialPracticeStart(string responseLines, HeadVelocityAxis axis, bool alternate)
        {
            Show("PRACTICE 2: TRIALS LIKE IN THE TEST\n\n" + ActiveTrialRules(axis, alternate),
                "Answer with:\n" + responseLines);
        }

        /// <summary>Hängt eine Zeile unten an, zum Beispiel "press trackpad to continue".</summary>
        public void AppendFooter(string line)
        {
            footer.text = string.IsNullOrEmpty(footer.text) ? line : footer.text + "\n\n" + line;
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        public void Dispose()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }

            if (headingMaterial != null) Object.Destroy(headingMaterial);
            if (footerMaterial != null) Object.Destroy(footerMaterial);
        }

        // Macht den Text sichtbar und setzt Überschrift und Fußzeile.
        private void Show(string headingText, string footerText)
        {
            root.SetActive(true);
            heading.text = headingText;
            footer.text = footerText;
        }

        private TextMesh CreateText(string name, Font font, float y, float size, out Material material)
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
            material = null;
            if (font != null)
            {
                // Eigenes Material mit hoher Render-Reihenfolge, damit der Text
                // immer vor allem anderen gezeichnet wird.
                material = new Material(font.material)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = 5000
                };
                textObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return mesh;
        }
    }
}
