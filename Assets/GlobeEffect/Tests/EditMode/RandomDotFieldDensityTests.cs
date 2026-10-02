using NUnit.Framework;
using Field = GlobeEffect.VRCheckerboard.RandomDots.RandomDotFieldStimulus;

namespace GlobeEffect.VRCheckerboard.Tests
{
    /// <summary>
    /// Testet, wie groß die Punktwelt wird und wie viele Punkte hineinkommen.
    ///
    /// Die Idee dahinter: In der Bildmitte soll es bei jedem m gleich dicht
    /// aussehen. Doppeltes m heißt also viermal so viele Punkte pro Fläche in
    /// der Außenwelt.
    /// </summary>
    public sealed class RandomDotFieldDensityTests
    {
        [Test]
        public void DotCount_MatchesTheOldSetupAtMagnificationTen()
        {
            // Früher: 24.500 Punkte auf 40,6 Grad bei m = 10.
            Assert.That(Field.DotCountFor(0.19f, 10f, 1f, 40.6f), Is.EqualTo(24500).Within(1).Percent);
        }

        [Test]
        public void DotCount_GrowsWithTheSquareOfMagnification()
        {
            int atSeven = Field.DotCountFor(0.19f, 7f, 1f, 20f);
            int atFourteen = Field.DotCountFor(0.19f, 14f, 1f, 20f);
            Assert.That(atFourteen / (float)atSeven, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void DotCount_TreatsContentZoomLikeExtraMagnification()
        {
            int zoomed = Field.DotCountFor(0.19f, 5f, 2f, 20f);
            Assert.That(zoomed, Is.EqualTo(Field.DotCountFor(0.19f, 10f, 1f, 20f)));
        }

        [Test]
        public void Coverage_CoversVisibleFieldPlusSweepPlusMargin()
        {
            // FOV 90, m = 10, k = 0,5: Am Rand des Kreises landet ein Weltwinkel
            // von 4,744 Grad. Dazu 1 Grad Reserve, und das Ganze mal zwei.
            Assert.That(Field.CoverageNeeded(90f, 1f, 10f, 0.5f, 0f), Is.EqualTo(11.488f).Within(0.01f));

            // Jedes Grad Schwenk zu jeder Seite macht die Welt 2 Grad breiter.
            Assert.That(Field.CoverageNeeded(90f, 1f, 10f, 0.5f, 3f), Is.EqualTo(17.488f).Within(0.01f));
        }

        [Test]
        public void Coverage_IsInfiniteBehindTheTurningPoint()
        {
            // k = 1,4 und 170 Grad FOV: 1,4 * 85 Grad liegt hinter 90 Grad.
            Assert.That(float.IsPositiveInfinity(Field.CoverageNeeded(170f, 1f, 10f, 1.4f, 0f)), Is.True);
        }
    }
}
