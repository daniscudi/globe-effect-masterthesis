using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Zählt nur echte Wechsel zwischen linker und rechter Winkelschwelle.
    /// Mehrere Frames auf derselben Seite dürfen keinen weiteren Sweep
    /// erzeugen. Die Klasse ist unabhängig von Unity-Transforms testbar.
    /// </summary>
    public sealed class AlternatingHeadSweepCounter
    {
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
        /// Liefert true, wenn dieser Sample einen neuen Seitenwechsel
        /// abgeschlossen hat.
        /// </summary>
        public bool Update(float yawDegrees)
        {
            // Diese drei Werte beschreiben den gesamten bisherigen Trial und werden
            // später zusammen mit dem Ergebnis gespeichert.
            MinimumYawDegrees = Math.Min(MinimumYawDegrees, yawDegrees);
            MaximumYawDegrees = Math.Max(MaximumYawDegrees, yawDegrees);
            MaximumAbsoluteYawDegrees = Math.Max(
                MaximumAbsoluteYawDegrees,
                Math.Abs(yawDegrees));

            int currentExtreme = yawDegrees >= ThresholdDegrees
                ? 1
                : yawDegrees <= -ThresholdDegrees
                    ? -1
                    : 0;
            // 1 = rechter Rand erreicht, -1 = linker Rand, 0 = dazwischen.

            if (currentExtreme == 0 || currentExtreme == lastExtreme)
            {
                return false;
            }

            bool completedAlternation = lastExtreme != 0;
            lastExtreme = currentExtreme;
            if (completedAlternation)
            {
                // Erst links -> rechts oder rechts -> links zählt als Halbschwenk.
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
