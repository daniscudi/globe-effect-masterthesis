using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Dreht die Kopfkamera in den Ablauftests gleichmäßig um die Hochachse, wie ein
    /// echter Kopf. Der Schritt läuft im PlayerLoop vor allen Update-Methoden, so wie
    /// XR die Kopfpose vor Update setzt: Der Experiment Manager misst dann im selben
    /// Frame genau den Schritt dieses Frames, auch wenn ein Frame länger dauert.
    /// (Eine MonoBehaviour aus der Editor-Testassembly lässt sich nicht anhängen.)
    /// </summary>
    internal static class TestHeadMover
    {
        private static Transform head;
        private static float degreesPerSecond;

        /// <summary>Gleichmäßig drehen (Grad pro Sekunde, positiv = rechts), 0 = anhalten.</summary>
        public static void Turn(Transform target, float speed)
        {
            head = target;
            degreesPerSecond = speed;
            // Nur eingehängt, solange gedreht wird: So bleibt nach einem Test nichts
            // im PlayerLoop zurück.
            SetInstalled(speed != 0f);
        }

        private static void Step()
        {
            if (head == null || degreesPerSecond == 0f) return;
            head.rotation *= Quaternion.Euler(0f, degreesPerSecond * Time.unscaledDeltaTime, 0f);
        }

        private static void SetInstalled(bool install)
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int index = 0; index < loop.subSystemList.Length; index++)
            {
                if (loop.subSystemList[index].type != typeof(PreUpdate)) continue;
                var systems = new List<PlayerLoopSystem>(loop.subSystemList[index].subSystemList);
                systems.RemoveAll(system => system.type == typeof(TestHeadMover));
                if (install)
                    systems.Add(new PlayerLoopSystem { type = typeof(TestHeadMover), updateDelegate = Step });
                loop.subSystemList[index].subSystemList = systems.ToArray();
            }
            PlayerLoop.SetPlayerLoop(loop);
        }
    }
}
