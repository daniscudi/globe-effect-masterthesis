using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// "Vorschau-Werte ins Experiment übernehmen" und "Originalwerte wiederherstellen".
    /// Beide Experiment Manager (Random Dots und Checkerboard) bieten das gleich an;
    /// der Editor-Teil arbeitet nur mit dieser Schnittstelle.
    /// </summary>
    public interface IPreviewTakeoverManager
    {
        bool IsPreviewActive { get; }
        bool HasValuesBeforePreviewTakeover { get; }
        string ValuesBeforePreviewTakeover { get; set; }

        // Der Stimulus, dessen Darstellungswerte mit übernommen werden (für Undo).
        UnityEngine.Object PreviewStimulus { get; }

        bool TakeOverPreviewValues();
        bool RestoreValuesBeforePreviewTakeover();
        string CaptureExperimentValues();
        void ApplyExperimentValues(string valuesJson);
    }

    /// <summary>
    /// Versuchsplan und Darstellung als ein JSON-Text: {"plan": "...", "look": "..."}.
    /// Gemeinsam für beide Manager.
    /// </summary>
    public static class PreviewTakeoverSnapshot
    {
        public static string Combine(string planJson, string lookJson) =>
            JsonUtility.ToJson(new Snapshot { plan = planJson, look = lookJson });

        public static string PlanOf(string valuesJson) => Read(valuesJson).plan;

        public static string LookOf(string valuesJson) => Read(valuesJson).look;

        private static Snapshot Read(string valuesJson)
        {
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(valuesJson);
            if (snapshot == null || string.IsNullOrEmpty(snapshot.plan) || string.IsNullOrEmpty(snapshot.look))
                throw new ArgumentException("Keine gültigen gespeicherten Experimentwerte.", nameof(valuesJson));
            return snapshot;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public string plan;
            public string look;
        }
    }
}
