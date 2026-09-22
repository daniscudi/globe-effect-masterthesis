using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Zählt, wie oft der Kopf wirklich von links nach rechts und wieder zurück
    /// gedreht wurde.
    ///
    /// Bleibt der Kopf mehrere Frames lang auf derselben Seite, wird das nicht
    /// noch einmal gezählt. Es zählt nur ein echter Wechsel auf die andere Seite.
    ///
    /// Hier steht nichts von Unity drin, deshalb kann man es einzeln testen.
    /// </summary>
    public sealed class AlternatingHeadSweepCounter
    {
        // Auf welcher Seite der Kopf zuletzt war: 1 rechts, -1 links, 0 noch nirgends.
        private int lastExtreme;

        public AlternatingHeadSweepCounter(float thresholdDegrees)
        {
            if (thresholdDegrees <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(thresholdDegrees));
            }

            ThresholdDegrees = thresholdDegrees;
            Reset();
        }

        public float ThresholdDegrees { get; }
        public int CompletedHalfSweeps { get; private set; }
        public float MinimumYawDegrees { get; private set; }
        public float MaximumYawDegrees { get; private set; }
        public float MaximumAbsoluteYawDegrees { get; private set; }

        /// <summary>
        /// Wird für jeden neuen Kopfwinkel aufgerufen. Gibt true zurück, wenn
        /// genau jetzt ein Wechsel auf die andere Seite fertig geworden ist.
        /// </summary>
        public bool Update(float yawDegrees)
        {
            // Die drei Werte merken sich, wie weit der Kopf im ganzen Durchgang
            // gedreht wurde. Sie werden später mit dem Ergebnis gespeichert.
            MinimumYawDegrees = Math.Min(MinimumYawDegrees, yawDegrees);
            MaximumYawDegrees = Math.Max(MaximumYawDegrees, yawDegrees);
            MaximumAbsoluteYawDegrees = Math.Max(
                MaximumAbsoluteYawDegrees,
                Math.Abs(yawDegrees));

            // Wo ist der Kopf gerade?
            // 1 heißt rechter Rand erreicht, -1 linker Rand, 0 irgendwo dazwischen.
            int currentExtreme = yawDegrees >= ThresholdDegrees
                ? 1
                : yawDegrees <= -ThresholdDegrees
                    ? -1
                    : 0;

            // Mittendrin oder immer noch auf derselben Seite: nichts passiert.
            if (currentExtreme == 0 || currentExtreme == lastExtreme)
            {
                return false;
            }

            // Nur wenn der Kopf vorher schon mal an einem Rand war, ist das jetzt
            // ein echter Wechsel. Beim allerersten Rand gibt es noch nichts zu zählen.
            bool completedAlternation = lastExtreme != 0;
            lastExtreme = currentExtreme;
            if (completedAlternation)
            {
                CompletedHalfSweeps++;
            }

            return completedAlternation;
        }

        public void Reset()
        {
            lastExtreme = 0;
            CompletedHalfSweeps = 0;
            MinimumYawDegrees = 0f;
            MaximumYawDegrees = 0f;
            MaximumAbsoluteYawDegrees = 0f;
        }
    }
}
