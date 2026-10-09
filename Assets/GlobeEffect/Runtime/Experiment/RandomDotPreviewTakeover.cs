using System;
using System.Collections.Generic;
using GlobeEffect.VRCheckerboard.RandomDots;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.Experiment
{
    /// <summary>
    /// "Vorschau-Werte ins Experiment übernehmen": kopiert die in der Live-Vorschau
    /// eingestellten Werte vom Random Dot Field in den Experiment Manager.
    ///
    /// Die Feldnamen der kleinen Klassen unten sind genau die serialisierten
    /// Feldnamen von Manager bzw. Stimulus. JsonUtility.FromJsonOverwrite
    /// überschreibt dadurch nur diese Felder und lässt alles andere stehen.
    /// </summary>
    public static class RandomDotPreviewTakeover
    {
        public static void CopyPreviewIntoPlan(RandomDotFieldStimulus stimulus, RandomDotExperimentManager manager)
        {
            var preview = new PreviewValues();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(stimulus), preview);
            var plan = new PlanValues();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(manager), plan);

            // k bleibt unverändert: die k-Liste ist das, was gemessen wird.
            plan.fieldOfViewValues = new List<float> { preview.fieldOfViewDegrees };
            plan.eyePresentations = new List<CheckerboardEyePresentation> { preview.eyePresentation };
            plan.instrumentMagnificationMValues = new List<float> { preview.instrumentMagnificationM };
            plan.contentZoomValues = new List<float> { preview.contentZoom };
            plan.sessionMotionMode = preview.motionMode;
            plan.simulatedSweepAxis = preview.sweepAxis;
            plan.simulatedSpeedReference = preview.previewSpeedReference;
            // Nur der Geschwindigkeitswert der gewählten Definition wird übernommen.
            // Sweep Speed gilt am Manager auch für Kopftraining und geführte Trials.
            if (preview.previewSpeedReference == RandomDotSweepSpeedReference.ImageCenter)
                plan.simulatedImageCenterSpeed = Mathf.Clamp(preview.previewImageCenterSpeed, 0.5f, 120f);
            else
                plan.sweepSpeed = Mathf.Clamp(preview.simulatedYawSpeedDegreesPerSecond, 0.1f, 60f);

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(plan), manager);
        }

        /// <summary>Versuchsplan des Managers plus Punktbild als ein JSON-Text.</summary>
        public static string Capture(RandomDotExperimentManager manager, string stimulusJson)
        {
            var plan = new PlanValues();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(manager), plan);
            return JsonUtility.ToJson(new Snapshot
            {
                plan = JsonUtility.ToJson(plan),
                look = CaptureLook(stimulusJson)
            });
        }

        public static string CaptureLook(string stimulusJson)
        {
            var look = new LookValues();
            JsonUtility.FromJsonOverwrite(stimulusJson, look);
            return JsonUtility.ToJson(look);
        }

        public static void ApplyPlan(string valuesJson, RandomDotExperimentManager manager)
        {
            JsonUtility.FromJsonOverwrite(Read(valuesJson).plan, manager);
        }

        public static string LookOf(string valuesJson) => Read(valuesJson).look;

        private static Snapshot Read(string valuesJson)
        {
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(valuesJson);
            if (snapshot == null || string.IsNullOrEmpty(snapshot.plan) || string.IsNullOrEmpty(snapshot.look))
                throw new ArgumentException("Keine gültigen Random-Dot-Experimentwerte.", nameof(valuesJson));
            return snapshot;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public string plan;
            public string look;
        }

        // Feldnamen = RandomDotExperimentManager.
        [Serializable]
        private sealed class PlanValues
        {
            public List<float> fieldOfViewValues = new();
            public List<CheckerboardEyePresentation> eyePresentations = new();
            public List<float> instrumentMagnificationMValues = new();
            public List<float> contentZoomValues = new();
            public RandomDotMotionMode sessionMotionMode;
            public RandomDotSweepAxis simulatedSweepAxis;
            public RandomDotSweepSpeedReference simulatedSpeedReference;
            public float simulatedImageCenterSpeed;
            public float sweepSpeed;
        }

        // Feldnamen = RandomDotFieldStimulus, Vorschauwerte.
        [Serializable]
        private sealed class PreviewValues
        {
            public float fieldOfViewDegrees;
            public float instrumentMagnificationM;
            public float contentZoom;
            public CheckerboardEyePresentation eyePresentation;
            public RandomDotMotionMode motionMode;
            public RandomDotSweepAxis sweepAxis;
            public RandomDotSweepSpeedReference previewSpeedReference;
            public float previewImageCenterSpeed;
            public float simulatedYawSpeedDegreesPerSecond;
        }

        // Feldnamen = RandomDotFieldStimulus, Punktbild. Diese Werte setzt der
        // Manager in Trials nicht selbst; sie gelten direkt aus dem Stimulus.
        [Serializable]
        private sealed class LookValues
        {
            public float edgeSoftnessDegrees;
            public float dotDensity;
            public float dotSizeDegrees;
            public Color darkColor;
            public Color lightColor;
            public Color fieldBackgroundColor;
            public Color backgroundColor;
            public float lightDotFraction;
        }
    }
}
