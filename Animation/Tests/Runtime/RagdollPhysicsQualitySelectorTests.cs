using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPhysicsQualitySelectorTests
    {
        static readonly float[] Distances = { 0f, 10f, 20f, 40f };

        [Test]
        public void InvalidCurrentLevel_SelectsRawDistanceBand()
        {
            int result = RagdollPhysicsQualitySelector.Evaluate(
                15f * 15f,
                -1,
                Distances,
                2f);

            Assert.That(result, Is.EqualTo(1));
        }

        [Test]
        public void DegradingQuality_RequiresUpperHysteresisMargin()
        {
            Assert.That(
                RagdollPhysicsQualitySelector.Evaluate(
                    11f * 11f,
                    0,
                    Distances,
                    2f),
                Is.EqualTo(0));
            Assert.That(
                RagdollPhysicsQualitySelector.Evaluate(
                    12f * 12f,
                    0,
                    Distances,
                    2f),
                Is.EqualTo(1));
        }

        [Test]
        public void ImprovingQuality_RequiresLowerHysteresisMargin()
        {
            Assert.That(
                RagdollPhysicsQualitySelector.Evaluate(
                    9f * 9f,
                    1,
                    Distances,
                    2f),
                Is.EqualTo(1));
            Assert.That(
                RagdollPhysicsQualitySelector.Evaluate(
                    7.9f * 7.9f,
                    1,
                    Distances,
                    2f),
                Is.EqualTo(0));
        }

        [Test]
        public void LargeDistanceChange_CanSkipMultipleLevels()
        {
            int result = RagdollPhysicsQualitySelector.Evaluate(
                100f * 100f,
                0,
                Distances,
                1f);

            Assert.That(result, Is.EqualTo(3));
        }
    }
}
