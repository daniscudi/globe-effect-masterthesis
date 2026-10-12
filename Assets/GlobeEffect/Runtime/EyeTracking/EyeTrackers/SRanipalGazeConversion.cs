using UnityEngine;
using ViveSR.anipal.Eye;

namespace GlobeEffect.VRCheckerboard.EyeTracking
{
    /// <summary>
    /// Reine Umrechnung eines SRanipal-Samples (EyeData_v2) in das GazeData-Format
    /// der Lab-Toolbox. Hier wird keine Unity-Szene und keine Hardware gebraucht;
    /// deshalb lässt sich die Umrechnung im EditMode testen und darf auch aus dem
    /// Callback-Thread der SRanipal-DLL aufgerufen werden.
    /// </summary>
    public static class SRanipalGazeConversion
    {
        public const float MillimetersToMeters = 0.001f;
        public const long MillisecondsToNanoseconds = 1_000_000L;

        // SRanipal meldet zusätzlich, ob überhaupt jemand das Headset trägt. Damit
        // diese Information nicht verloren geht, steht sie als eigenes Bit oberhalb
        // der Validitätsmaske im kombinierten Rohstatus.
        public const int NoUserStatusBit = 1 << 16;

        private const ulong ValidityMask = 0xFFFF;

        /// <summary>
        /// Ursprung eines Blickstrahls: SRanipal liefert Millimeter in einem
        /// rechtshändigen System (+X zeigt aus Sicht der Person nach links, +Y nach
        /// oben, +Z nach vorn). Unity rechnet linkshändig in Metern mit +X nach
        /// rechts. Deshalb wird x gespiegelt und mit 0,001 skaliert.
        ///
        /// Achtung: Der SDK-Helfer SRanipal_Eye_v2.GetGazeRay skaliert den Ursprung
        /// nur und spiegelt ihn nicht. Dort fällt das nicht auf, weil die Samples
        /// den Ursprung gar nicht verwenden. Ohne die Spiegelung läge das linke Auge
        /// im Kamerasystem rechts.
        /// </summary>
        public static Vector3 ConvertOrigin(Vector3 originMillimetersRightHanded)
        {
            return new Vector3(
                -originMillimetersRightHanded.x * MillimetersToMeters,
                originMillimetersRightHanded.y * MillimetersToMeters,
                originMillimetersRightHanded.z * MillimetersToMeters);
        }

        /// <summary>
        /// Blickrichtung: dieselbe Spiegelung der x-Achse wie im SDK
        /// (SRanipal_Eye_v2.GetGazeRay: direction.x *= -1). Eine Skalierung ist nicht
        /// nötig, weil die Richtung bereits normiert ist.
        /// </summary>
        public static Vector3 ConvertDirection(Vector3 directionRightHanded)
        {
            return new Vector3(
                -directionRightHanded.x,
                directionRightHanded.y,
                directionRightHanded.z);
        }

        public static Ray ConvertRay(SingleEyeData eye)
        {
            return new Ray(
                ConvertOrigin(eye.gaze_origin_mm),
                ConvertDirection(eye.gaze_direction_normalized));
        }

        public static GazeData Convert(in EyeData_v2 sample)
        {
            SingleEyeData left = sample.verbose_data.left;
            SingleEyeData right = sample.verbose_data.right;
            CombinedEyeData combined = sample.verbose_data.combined;

            return new GazeData
            {
                frameNumber = sample.frame_sequence,

                // SRanipal liefert Millisekunden. Die Spalte eye_timestamp enthält
                // bei Varjo und beim Dummy Nanosekunden; damit die Einheit in den
                // CSV-Dateien gleich bleibt, wird hier verlustfrei umgerechnet.
                deviceTimestamp = sample.timestamp * MillisecondsToNanoseconds,

                leftRayLocal = ConvertRay(left),
                rightRayLocal = ConvertRay(right),
                combinedRayLocal = ConvertRay(combined.eye_data),

                // Die Konvergenzdistanz ist nur verwendbar, wenn SRanipal sie als
                // gültig markiert. Sonst bleibt die Spalte NaN.
                gazeDistance = combined.convergence_distance_validity
                    ? combined.convergence_distance_mm * MillimetersToMeters
                    : float.NaN,

                // SRanipal liefert keinen Augenabstand. Er lässt sich bei Bedarf
                // später aus den beiden Ursprüngen berechnen.
                interPupillaryDistanceMillimeters = float.NaN,

                leftPupilDiameter = ValueOrNaN(
                    left,
                    SingleEyeDataValidity.SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY,
                    left.pupil_diameter_mm),
                rightPupilDiameter = ValueOrNaN(
                    right,
                    SingleEyeDataValidity.SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY,
                    right.pupil_diameter_mm),
                leftEyeOpenness = ValueOrNaN(
                    left,
                    SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY,
                    left.eye_openness),
                rightEyeOpenness = ValueOrNaN(
                    right,
                    SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY,
                    right.eye_openness),

                // Wie im SDK gilt ein Strahl als gültig, wenn SRanipal die
                // Blickrichtung als gültig markiert. Die vollständigen Masken
                // bleiben in den Rohstatuswerten erhalten.
                combinedValidity = IsGazeDirectionValid(combined.eye_data),
                leftValidity = IsGazeDirectionValid(left),
                rightValidity = IsGazeDirectionValid(right),

                trackingStatus = ToRawStatus(combined.eye_data) |
                    (sample.no_user ? NoUserStatusBit : 0),
                leftTrackingStatus = ToRawStatus(left),
                rightTrackingStatus = ToRawStatus(right)
            };
        }

        /// <summary>
        /// Platzhalter, solange noch kein Sample vorliegt oder SRanipal nicht läuft.
        /// </summary>
        public static GazeData CreateInvalidSample()
        {
            var forward = new Ray(Vector3.zero, Vector3.forward);
            return new GazeData
            {
                leftRayLocal = forward,
                rightRayLocal = forward,
                combinedRayLocal = forward,
                gazeDistance = float.NaN,
                interPupillaryDistanceMillimeters = float.NaN,
                leftPupilDiameter = float.NaN,
                rightPupilDiameter = float.NaN,
                leftEyeOpenness = float.NaN,
                rightEyeOpenness = float.NaN
            };
        }

        public static bool IsGazeDirectionValid(SingleEyeData eye)
        {
            return eye.GetValidity(
                SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY);
        }

        // Bit 0 = Ursprung, 1 = Richtung, 2 = Pupillendurchmesser, 3 = Lidöffnung,
        // 4 = Pupillenposition im Sensorbild (siehe SingleEyeDataValidity im SDK).
        private static int ToRawStatus(SingleEyeData eye)
        {
            return (int)(eye.eye_data_validata_bit_mask & ValidityMask);
        }

        private static float ValueOrNaN(
            SingleEyeData eye,
            SingleEyeDataValidity validity,
            float value)
        {
            return eye.GetValidity(validity) ? value : float.NaN;
        }
    }
}
