using GlobeEffect.VRCheckerboard.EyeTracking;
using NUnit.Framework;
using UnityEngine;
using ViveSR.anipal.Eye;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Prüft die Umrechnung der SRanipal-Samples (rechtshändig, Millimeter) in das
    /// GazeData-Format der Toolbox (Unity: linkshändig, Meter). Dafür wird weder
    /// ein Headset noch die SRanipal-Runtime gebraucht.
    /// </summary>
    public sealed class SRanipalGazeConversionTests
    {
        private const ulong OriginValid = 1UL << 0;
        private const ulong DirectionValid = 1UL << 1;
        private const ulong PupilDiameterValid = 1UL << 2;
        private const ulong OpennessValid = 1UL << 3;
        private const ulong AllValid = 0b11111;
        private const float Tolerance = 1e-6f;

        [Test]
        public void ConvertOrigin_MirrorsXAndScalesMillimetersToMeters()
        {
            Vector3 origin = SRanipalGazeConversion.ConvertOrigin(
                new Vector3(32f, -1.5f, -40f));

            Assert.That(origin.x, Is.EqualTo(-0.032f).Within(Tolerance));
            Assert.That(origin.y, Is.EqualTo(-0.0015f).Within(Tolerance));
            Assert.That(origin.z, Is.EqualTo(-0.040f).Within(Tolerance));
        }

        [Test]
        public void ConvertDirection_MirrorsOnlyX()
        {
            Vector3 direction = SRanipalGazeConversion.ConvertDirection(
                new Vector3(0.6f, 0.2f, 0.7f));

            Assert.That(direction.x, Is.EqualTo(-0.6f).Within(Tolerance));
            Assert.That(direction.y, Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(direction.z, Is.EqualTo(0.7f).Within(Tolerance));
        }

        [Test]
        public void Convert_PutsLeftEyeOnNegativeXAndRightEyeOnPositiveX()
        {
            // Im rechtshändigen SRanipal-System liegt das linke Auge bei +x.
            GazeData gaze = SRanipalGazeConversion.Convert(CreateSample());

            Assert.That(gaze.leftRayLocal.origin.x, Is.EqualTo(-0.032f).Within(Tolerance));
            Assert.That(gaze.rightRayLocal.origin.x, Is.EqualTo(0.031f).Within(Tolerance));
            Assert.That(gaze.combinedRayLocal.origin.x, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Convert_GazeTowardsTheUsersRightHasPositiveX()
        {
            EyeData_v2 sample = CreateSample();

            // -x heißt bei SRanipal: aus Sicht der Person nach rechts.
            sample.verbose_data.combined.eye_data.gaze_direction_normalized =
                new Vector3(-0.5f, 0f, 0.8660254f);

            GazeData gaze = SRanipalGazeConversion.Convert(sample);

            Assert.That(gaze.combinedRayLocal.direction.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(gaze.combinedRayLocal.direction.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(gaze.combinedRayLocal.direction.z,
                Is.EqualTo(0.8660254f).Within(1e-5f));
        }

        [Test]
        public void Convert_StraightAheadStaysStraightAhead()
        {
            GazeData gaze = SRanipalGazeConversion.Convert(CreateSample());

            Assert.That(Vector3.Angle(gaze.leftRayLocal.direction, Vector3.forward),
                Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(gaze.rightRayLocal.direction, Vector3.forward),
                Is.LessThan(0.001f));
        }

        [Test]
        public void Convert_CopiesFrameNumberAndConvertsTimestampToNanoseconds()
        {
            EyeData_v2 sample = CreateSample();
            sample.frame_sequence = 4711;
            sample.timestamp = 123456;

            GazeData gaze = SRanipalGazeConversion.Convert(sample);

            Assert.That(gaze.frameNumber, Is.EqualTo(4711L));
            Assert.That(gaze.deviceTimestamp, Is.EqualTo(123_456_000_000L));
        }

        [Test]
        public void Convert_LargeTimestampDoesNotOverflow()
        {
            EyeData_v2 sample = CreateSample();
            sample.timestamp = int.MaxValue;

            GazeData gaze = SRanipalGazeConversion.Convert(sample);

            Assert.That(gaze.deviceTimestamp, Is.EqualTo(int.MaxValue * 1_000_000L));
        }

        [Test]
        public void Convert_ValidityFollowsTheGazeDirectionBit()
        {
            EyeData_v2 sample = CreateSample();
            sample.verbose_data.left.eye_data_validata_bit_mask = DirectionValid;
            sample.verbose_data.right.eye_data_validata_bit_mask = OriginValid;
            sample.verbose_data.combined.eye_data.eye_data_validata_bit_mask = 0UL;

            GazeData gaze = SRanipalGazeConversion.Convert(sample);

            Assert.That(gaze.leftValidity, Is.True);
            Assert.That(gaze.rightValidity, Is.False);
            Assert.That(gaze.combinedValidity, Is.False);
        }

        [Test]
        public void Convert_KeepsPupilDiameterAndOpennessWhenValid()
        {
            GazeData gaze = SRanipalGazeConversion.Convert(CreateSample());

            Assert.That(gaze.leftPupilDiameter, Is.EqualTo(3.5f).Within(Tolerance));
            Assert.That(gaze.rightPupilDiameter, Is.EqualTo(3.7f).Within(Tolerance));
            Assert.That(gaze.leftEyeOpenness, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(gaze.rightEyeOpenness, Is.EqualTo(0.8f).Within(Tolerance));
        }

        [Test]
        public void Convert_WritesNaNForInvalidPupilDiameterAndOpenness()
        {
            EyeData_v2 sample = CreateSample();
            sample.verbose_data.left.eye_data_validata_bit_mask =
                OriginValid | DirectionValid | OpennessValid;
            sample.verbose_data.right.eye_data_validata_bit_mask =
                OriginValid | DirectionValid | PupilDiameterValid;

            GazeData gaze = SRanipalGazeConversion.Convert(sample);

            Assert.That(gaze.leftPupilDiameter, Is.NaN);
            Assert.That(gaze.leftEyeOpenness, Is.EqualTo(0.9f).Within(Tolerance));
            Assert.That(gaze.rightPupilDiameter, Is.EqualTo(3.7f).Within(Tolerance));
            Assert.That(gaze.rightEyeOpenness, Is.NaN);
        }

        [Test]
        public void Convert_ConvergenceDistanceIsMetersWhenValidAndNaNOtherwise()
        {
            EyeData_v2 sample = CreateSample();
            sample.verbose_data.combined.convergence_distance_mm = 1500f;

            sample.verbose_data.combined.convergence_distance_validity = true;
            Assert.That(SRanipalGazeConversion.Convert(sample).gazeDistance,
                Is.EqualTo(1.5f).Within(Tolerance));

            sample.verbose_data.combined.convergence_distance_validity = false;
            Assert.That(SRanipalGazeConversion.Convert(sample).gazeDistance, Is.NaN);
        }

        [Test]
        public void Convert_LeavesInterPupillaryDistanceEmpty()
        {
            GazeData gaze = SRanipalGazeConversion.Convert(CreateSample());

            Assert.That(gaze.interPupillaryDistanceMillimeters, Is.NaN);
        }

        [Test]
        public void Convert_RawStatusKeepsBitMasksAndNoUserFlag()
        {
            EyeData_v2 sample = CreateSample();
            sample.verbose_data.left.eye_data_validata_bit_mask = 0b00011;
            sample.verbose_data.right.eye_data_validata_bit_mask = 0b01111;
            sample.verbose_data.combined.eye_data.eye_data_validata_bit_mask = 0b00010;

            sample.no_user = false;
            GazeData withUser = SRanipalGazeConversion.Convert(sample);
            Assert.That(withUser.leftTrackingStatus, Is.EqualTo(0b00011));
            Assert.That(withUser.rightTrackingStatus, Is.EqualTo(0b01111));
            Assert.That(withUser.trackingStatus, Is.EqualTo(0b00010));

            sample.no_user = true;
            GazeData withoutUser = SRanipalGazeConversion.Convert(sample);
            Assert.That(withoutUser.trackingStatus,
                Is.EqualTo(0b00010 | SRanipalGazeConversion.NoUserStatusBit));
        }

        [Test]
        public void CreateInvalidSample_HasNoValidRaysAndNoMeasurements()
        {
            GazeData gaze = SRanipalGazeConversion.CreateInvalidSample();

            Assert.That(gaze.combinedValidity, Is.False);
            Assert.That(gaze.leftValidity, Is.False);
            Assert.That(gaze.rightValidity, Is.False);
            Assert.That(gaze.frameNumber, Is.EqualTo(0L));
            Assert.That(gaze.leftPupilDiameter, Is.NaN);
            Assert.That(gaze.rightEyeOpenness, Is.NaN);
            Assert.That(gaze.gazeDistance, Is.NaN);
        }

        // Ein vollständig gültiges Sample: beide Augen schauen geradeaus, das linke
        // Auge liegt (rechtshändig) bei +32 mm, das rechte bei -31 mm.
        private static EyeData_v2 CreateSample()
        {
            var sample = new EyeData_v2
            {
                no_user = false,
                frame_sequence = 1,
                timestamp = 1000
            };

            sample.verbose_data.left = CreateEye(
                new Vector3(32f, 1f, -40f), Vector3.forward, AllValid, 3.5f, 0.9f);
            sample.verbose_data.right = CreateEye(
                new Vector3(-31f, 1f, -40f), Vector3.forward, AllValid, 3.7f, 0.8f);
            sample.verbose_data.combined.eye_data = CreateEye(
                new Vector3(0f, 1f, -40f), Vector3.forward, AllValid, 0f, 0f);
            return sample;
        }

        private static SingleEyeData CreateEye(
            Vector3 originMillimeters,
            Vector3 direction,
            ulong validityMask,
            float pupilDiameterMillimeters,
            float openness)
        {
            return new SingleEyeData
            {
                eye_data_validata_bit_mask = validityMask,
                gaze_origin_mm = originMillimeters,
                gaze_direction_normalized = direction,
                pupil_diameter_mm = pupilDiameterMillimeters,
                eye_openness = openness
            };
        }
    }
}
