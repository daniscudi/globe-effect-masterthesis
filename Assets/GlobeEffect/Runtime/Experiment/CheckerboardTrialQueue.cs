using System;
using System.Collections.Generic;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// Kleine Warteschlange für den Versuchsablauf. Ein ungültiger Trial wird
    /// nicht sofort wiederholt, sondern mit erhöhter Versuchsnummer hinten
    /// angehängt. So erscheint nicht direkt noch einmal dieselbe Bedingung.
    /// </summary>
    public sealed class CheckerboardTrialQueue
    {
        private readonly Queue<CheckerboardTrial> pending = new();

        public int Count => pending.Count;

        public CheckerboardTrialQueue(IReadOnlyList<CheckerboardTrial> plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            foreach (CheckerboardTrial trial in plan)
            {
                pending.Enqueue(trial);
            }
        }

        public bool TryTakeNext(out CheckerboardTrial trial)
        {
            if (pending.Count == 0)
            {
                trial = null;
                return false;
            }

            trial = pending.Dequeue();
            return true;
        }

        public CheckerboardTrial AppendRepeatedAttempt(CheckerboardTrial invalidTrial)
        {
            if (invalidTrial == null)
            {
                throw new ArgumentNullException(nameof(invalidTrial));
            }

            CheckerboardTrial repeat = invalidTrial.CreateRepeatedAttempt();
            pending.Enqueue(repeat);
            return repeat;
        }
    }
}
