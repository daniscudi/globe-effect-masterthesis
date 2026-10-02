using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Zählt während eines Durchgangs zwei Dinge getrennt mit: wie lange die
    /// Person am Stück am Kreuz vorbeigeschaut hat, und wie lange am Stück gar
    /// keine brauchbaren Blickdaten kamen.
    ///
    /// Getrennt deshalb, weil ein Blinzeln etwas anderes ist als wegschauen.
    /// Beide Experiment Manager benutzen genau diese Regeln.
    /// </summary>
    public sealed class LookAwayTimer
    {
        // So lange geht das gerade schon am Stück.
        public float LookAwaySeconds { get; private set; }
        public float NoDataSeconds { get; private set; }

        // Das längste Stück im ganzen Durchgang. Das landet später in der CSV.
        public float LongestLookAwaySeconds { get; private set; }
        public float LongestNoDataSeconds { get; private set; }

        // Jeder Versuch fängt mit frischen Zählern bei null an. Sonst würde man
        // die Aussetzer vom vorigen Durchgang mitschleppen.
        public void Reset()
        {
            LookAwaySeconds = 0f;
            NoDataSeconds = 0f;
            LongestLookAwaySeconds = 0f;
            LongestNoDataSeconds = 0f;
        }

        /// <summary>
        /// Einmal pro Frame aufrufen. sampleUsable heißt: Es kam gerade ein
        /// frischer, brauchbarer Blickwert. onTarget heißt: Der Blick liegt auf
        /// dem Kreuz.
        /// </summary>
        public void Add(float deltaSeconds, bool sampleUsable, bool onTarget)
        {
            if (!sampleUsable)
            {
                NoDataSeconds += deltaSeconds;
                LookAwaySeconds = 0f;
            }
            else if (!onTarget)
            {
                LookAwaySeconds += deltaSeconds;
                NoDataSeconds = 0f;
            }
            else
            {
                LookAwaySeconds = 0f;
                NoDataSeconds = 0f;
            }

            LongestLookAwaySeconds = Mathf.Max(LongestLookAwaySeconds, LookAwaySeconds);
            LongestNoDataSeconds = Mathf.Max(LongestNoDataSeconds, NoDataSeconds);
        }

        /// <summary>
        /// Gibt den Grund zurück, wenn eine der beiden Zeiten am Stück zu lang war,
        /// sonst null. Kurze Aussetzer sind also in Ordnung. Die Texte stehen so
        /// in der CSV und in den Markern.
        /// </summary>
        public string FindProblem(float maxLookAwaySeconds, float maxNoDataSeconds)
        {
            if (LookAwaySeconds > maxLookAwaySeconds)
            {
                return "off_target";
            }

            return NoDataSeconds > maxNoDataSeconds ? "invalid_gaze_data" : null;
        }
    }
}
