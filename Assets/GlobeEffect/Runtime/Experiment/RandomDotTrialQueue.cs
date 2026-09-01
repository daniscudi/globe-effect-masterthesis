using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Hält die noch ausstehenden Random-Dot-Trials. Eine wegen der Fixation
    /// ungültige Präsentation wird mit erhöhter AttemptNumber hinten angehängt,
    /// damit nicht sofort dieselbe Bedingung wieder erscheint.
    /// </summary>
    public sealed class RandomDotTrialQueue
    {
        private readonly Queue<RandomDotTrial> pending = new();

        public int Count => pending.Count;

        public RandomDotTrialQueue(IReadOnlyList<RandomDotTrial> plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            foreach (RandomDotTrial trial in plan)
            {
                pending.Enqueue(trial);
            }
        }

        public bool TryTakeNext(out RandomDotTrial trial)
        {
            if (pending.Count == 0)
            {
                trial = null;
                return false;
            }

            trial = pending.Dequeue();
            return true;
        }

        public RandomDotTrial AppendRepeatedAttempt(RandomDotTrial invalidTrial)
        {
            if (invalidTrial == null)
            {
                throw new ArgumentNullException(nameof(invalidTrial));
            }

            RandomDotTrial repeat = invalidTrial.CreateRepeatedAttempt();
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
