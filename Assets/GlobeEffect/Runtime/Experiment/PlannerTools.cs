using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Kleine Werkzeuge, die beide Trialplaner gleich brauchen.
    /// </summary>
    public static class PlannerTools
    {
        /// <summary>
        /// Mischt die Liste mit dem Verfahren Fisher-Yates: Man geht von hinten
        /// durch und tauscht jeden Eintrag mit einem zufälligen Eintrag weiter
        /// vorne. Der Seed sorgt dafür, dass dabei immer dasselbe Mischergebnis
        /// herauskommt.
        /// </summary>
        public static void Shuffle<T>(List<T> items, int seed)
        {
            var random = new Random(seed);
            for (int index = items.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
            }
        }

        /// <summary>
        /// In jeder Liste muss mindestens ein Wert stehen, sonst gibt es gar keine
        /// Kombinationen und der Plan wäre leer.
        /// </summary>
        public static void RequireNonEmpty<T>(IReadOnlyCollection<T> values, string name)
        {
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("Mindestens ein Wert ist erforderlich.", name);
            }
        }

        /// <summary>
        /// Wie oft der Reizwert an Position index gezeigt wird. Steht für ihn eine
        /// eigene Zahl in perValue, gilt diese (z. B. Randwerte 8-mal, Mitte 25-mal),
        /// sonst die gemeinsame Zahl fallback.
        /// </summary>
        public static int RepetitionsFor(IReadOnlyList<int> perValue, int index, int fallback)
        {
            int repetitions = perValue != null && index < perValue.Count ? perValue[index] : fallback;
            if (repetitions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(perValue),
                    $"Jeder Reizwert braucht mindestens 1 Wiederholung (Position {index + 1}).");
            }

            return repetitions;
        }
    }
}
