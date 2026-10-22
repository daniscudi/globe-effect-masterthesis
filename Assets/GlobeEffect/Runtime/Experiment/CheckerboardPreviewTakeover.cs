using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// "Vorschau-Werte ins Experiment übernehmen" für das Checkerboard: kopiert die
    /// in der Live-Vorschau eingestellten Werte vom Checkerboard Stimulus in den
    /// Experiment Manager. Gleiches Prinzip wie RandomDotPreviewTakeover.
    ///
    /// Die Feldnamen der kleinen Klassen unten sind genau die serialisierten
    /// Feldnamen von Manager bzw. Stimulus. JsonUtility.FromJsonOverwrite
    /// überschreibt dadurch nur diese Felder und lässt alles andere stehen.
    /// </summary>
    public static class CheckerboardPreviewTakeover
    {
        public static void CopyPreviewIntoPlan(VrCheckerboardStimulus stimulus, CheckerboardExperimentManager manager)
        {
            var preview = new PreviewValues();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(stimulus), preview);
            var plan = new PlanValues();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(manager), plan);

            // k-Liste bleibt unverändert; FOV, Augenmodus und m übernehmen.
            plan.fieldOfViewValues = new List<float> { preview.fieldOfViewDegrees };
            plan.eyePresentations = new List<CheckerboardEyePresentation> { preview.eyePresentation };
            plan.instrumentMagnificationM = preview.instrumentMagnificationM;
            plan.testFixedSourceGrid = preview.gridMapping == CheckerboardGridMapping.FixedSourceGrid;
            plan.testEdgeNormalizedGrid = preview.gridMapping == CheckerboardGridMapping.EdgeNormalized;

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(plan), manager);
        }

        /// <summary>Versuchsplan des Managers plus Darstellung als ein JSON-Text.</summary>
        public static string Capture(CheckerboardExperimentManager manager, string stimulusJson)
        {
            var plan = new PlanValues();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(manager), plan);
            return PreviewTakeoverSnapshot.Combine(JsonUtility.ToJson(plan), CaptureLook(stimulusJson));
        }

        public static string CaptureLook(string stimulusJson)
        {
            var look = new LookValues();
            JsonUtility.FromJsonOverwrite(stimulusJson, look);
            return JsonUtility.ToJson(look);
        }

        public static void ApplyPlan(string valuesJson, CheckerboardExperimentManager manager)
        {
            JsonUtility.FromJsonOverwrite(PreviewTakeoverSnapshot.PlanOf(valuesJson), manager);
        }

        public static string LookOf(string valuesJson) => PreviewTakeoverSnapshot.LookOf(valuesJson);

        // Feldnamen = CheckerboardExperimentManager.
        [Serializable]
        private sealed class PlanValues
        {
            public List<float> fieldOfViewValues = new();
            public List<CheckerboardEyePresentation> eyePresentations = new();
            public float instrumentMagnificationM = 10f;
            public bool testFixedSourceGrid = true;
            public bool testEdgeNormalizedGrid;
        }

        // Feldnamen = VrCheckerboardStimulus, Vorschauwerte für den Versuchsplan.
        [Serializable]
        private sealed class PreviewValues
        {
            public float fieldOfViewDegrees;
            public CheckerboardEyePresentation eyePresentation;
            public float instrumentMagnificationM = 10f;
            public CheckerboardGridMapping gridMapping;
        }

        // Feldnamen = VrCheckerboardStimulus, Darstellung. Diese Werte setzt der
        // Manager in Trials nicht selbst; sie gelten direkt aus dem Stimulus.
        [Serializable]
        private sealed class LookValues
        {
            // Auch beim Zurücksetzen/Behalten der Vorschau das sichtbare m mitnehmen.
            public float instrumentMagnificationM = 10f;
            public CheckerboardGridMapping gridMapping;
            public float edgeSoftnessDegrees;
            public bool useCircularAperture;
            public float gridSpacingDegrees;
            public Color darkColor;
            public Color lightColor;
            public Color backgroundColor;
            public bool showFixationTarget;
            public float fixationSizeDegrees;
            public Color fixationColor;
            public float noiseBroadbandFinestDegrees;
            public float noiseBroadbandCoarsestDegrees;
            public float noiseRefreshRateHz;
        }
    }
}
