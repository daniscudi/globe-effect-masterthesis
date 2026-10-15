using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Misst während der Punkte die Kopfbewegung neben der eigentlichen Schwenkachse:
    /// bei horizontalem Schwenk das Nicken (auf/ab), bei vertikalem das Drehen
    /// (links/rechts), und bei beiden das seitliche Kippen (Roll). Bei Simulated
    /// laufen die Punkte genau entlang der Achse; so lässt sich prüfen, ob die aktiven
    /// Trials vergleichbar waren. Nur Messung, kein Abbruch.
    /// Kein MonoBehaviour: der Manager füttert die Klasse jeden Frame.
    /// </summary>
    public sealed class RandomDotHeadOffAxisMeter
    {
        private HeadVelocityAxis axis;
        private bool measuring;
        private float lastOffAxisDegrees;
        private float startOffAxisDegrees;
        private float startRollDegrees;
        private float seconds;
        private float offAxisTravelDegrees;

        /// <summary>Mittleres Tempo neben der Schwenkachse während der Punkte (°/s), NaN ohne Messung.</summary>
        public float MeanOffAxisSpeed => seconds > 0f ? offAxisTravelDegrees / seconds : float.NaN;

        /// <summary>Größte Abweichung neben der Schwenkachse vom Einblendezeitpunkt (°), NaN ohne Messung.</summary>
        public float MaxOffAxisDeviationDegrees { get; private set; } = float.NaN;

        /// <summary>Größtes seitliches Kippen gegenüber dem Einblendezeitpunkt (°), NaN ohne Messung.</summary>
        public float MaxRollDeviationDegrees { get; private set; } = float.NaN;

        public void Reset()
        {
            measuring = false;
            seconds = 0f;
            offAxisTravelDegrees = 0f;
            MaxOffAxisDeviationDegrees = float.NaN;
            MaxRollDeviationDegrees = float.NaN;
        }

        /// <summary>Beim Einblenden der Punkte: ab hier wird gemessen.</summary>
        public void Begin(Vector3 headForward, Vector3 headUp, HeadVelocityAxis swingAxis)
        {
            Reset();
            axis = swingAxis;
            measuring = true;
            startOffAxisDegrees = OffAxisDegrees(headForward, axis);
            lastOffAxisDegrees = startOffAxisDegrees;
            startRollDegrees = RollDegrees(headForward, headUp);
            MaxOffAxisDeviationDegrees = 0f;
            MaxRollDeviationDegrees = 0f;
        }

        public void Add(Vector3 headForward, Vector3 headUp, float deltaSeconds)
        {
            if (!measuring || deltaSeconds <= 0f) return;
            float offAxis = OffAxisDegrees(headForward, axis);
            offAxisTravelDegrees += Mathf.Abs(Mathf.DeltaAngle(lastOffAxisDegrees, offAxis));
            lastOffAxisDegrees = offAxis;
            seconds += deltaSeconds;
            MaxOffAxisDeviationDegrees = Mathf.Max(MaxOffAxisDeviationDegrees,
                Mathf.Abs(Mathf.DeltaAngle(startOffAxisDegrees, offAxis)));
            MaxRollDeviationDegrees = Mathf.Max(MaxRollDeviationDegrees,
                Mathf.Abs(Mathf.DeltaAngle(startRollDegrees, RollDegrees(headForward, headUp))));
        }

        /// <summary>
        /// Winkel neben der Schwenkachse: bei horizontalem Schwenk die Höhe über dem
        /// Horizont, bei vertikalem die Richtung von oben gesehen.
        /// </summary>
        public static float OffAxisDegrees(Vector3 forward, HeadVelocityAxis swingAxis)
        {
            return swingAxis == HeadVelocityAxis.Horizontal
                ? Mathf.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg
                : Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>Seitliches Kippen des Kopfes: Winkel zwischen Kopf-Oben und Welt-Oben um die Blickachse.</summary>
        public static float RollDegrees(Vector3 forward, Vector3 up)
        {
            Vector3 worldUp = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (worldUp.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(worldUp, up, forward);
        }

        /// <summary>Schwenkrichtung aus dem Tempo mit Vorzeichen: positiv = rechts bzw. oben.</summary>
        public static string Direction(float signedSpeed, HeadVelocityAxis swingAxis)
        {
            if (swingAxis == HeadVelocityAxis.Horizontal) return signedSpeed >= 0f ? "Right" : "Left";
            return signedSpeed >= 0f ? "Up" : "Down";
        }
    }
}
