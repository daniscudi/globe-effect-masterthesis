using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Kleine Kopftempo-Anzeige im Headset für den Pilot in der Vorschau. Eine
    /// Textzeile etwas unter dem Kreuz und ein kurzer Ton, wenn ein Tempo
    /// gespeichert wurde. In Training und Messung nie zu sehen.
    /// </summary>
    public sealed class RandomDotPilotSpeedView : IDisposable
    {
        private readonly GameObject root;
        private readonly TextMesh text;
        private readonly Material material;
        private readonly AudioSource audioSource;
        private readonly AudioClip savedTone;

        public RandomDotPilotSpeedView(Transform observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));

            // Hängt am Kopf, 2 m vor den Augen, etwa 9° unter dem Kreuz.
            root = new GameObject("Runtime Random Dot Pilot Speed");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(observer, false);
            root.transform.localPosition = new Vector3(0f, -0.32f, 2f);
            root.transform.localRotation = Quaternion.identity;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text = root.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.008f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.8f, 0f);
            text.richText = false;
            if (font != null)
            {
                // Vor den Punkten zeichnen, wie beim Trainingsdisplay.
                material = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 5000 };
                root.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.2f;
            savedTone = CreateTone("Random Dot Speed Saved", 880f);
            Hide();
        }

        public void Show(string line)
        {
            root.SetActive(true);
            text.text = line;
        }

        public void Hide()
        {
            if (root.activeSelf) root.SetActive(false);
        }

        public void PlaySavedTone()
        {
            // AudioSource spielt auch, wenn der Text gerade aus ist: deshalb kurz aktivieren.
            root.SetActive(true);
            audioSource.PlayOneShot(savedTone);
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            if (material != null) Object.Destroy(material);
            Object.Destroy(savedTone);
        }

        private static AudioClip CreateTone(string name, float frequencyHz)
        {
            // 0,12 s Sinuston, sanft ein- und ausgeblendet, damit er nicht knackt.
            const int sampleRate = 44100;
            const int sampleCount = 5292;
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
