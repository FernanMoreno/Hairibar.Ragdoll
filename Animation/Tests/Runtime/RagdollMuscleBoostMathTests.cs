using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollMuscleBoostMathTests
    {
        [Test]
        public void Immunity_ReducesSuppressionToZeroAtFullBoost()
        {
            Assert.That(
                RagdollMuscleBoostMath.ApplyImmunity(0.8f, 0.25f),
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(
                RagdollMuscleBoostMath.ApplyImmunity(0.8f, 1f),
                Is.EqualTo(0f));
        }

        [Test]
        public void ImpulseMultiplier_ScalesDamageImpulseFromNeutralOne()
        {
            Assert.That(
                RagdollMuscleBoostMath.ApplyImpulseMultiplier(3f, 1f),
                Is.EqualTo(3f));
            Assert.That(
                RagdollMuscleBoostMath.ApplyImpulseMultiplier(3f, 2.5f),
                Is.EqualTo(7.5f));
        }

        [Test]
        public void Falloff_ReturnsImmunityAndImpulseToDifferentNeutralValues()
        {
            Assert.That(
                RagdollMuscleBoostMath.StepImmunity(1f, 0.25f, 2f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                RagdollMuscleBoostMath.StepImpulseMultiplier(3f, 0.25f, 2f),
                Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void InvalidDamageInputs_AreSanitizedWithoutProducingNaN()
        {
            Assert.That(
                RagdollMuscleBoostMath.ApplyImmunity(float.NaN, float.NaN),
                Is.EqualTo(0f));
            Assert.That(
                RagdollMuscleBoostMath.ApplyImmunity(
                    float.PositiveInfinity,
                    float.NegativeInfinity),
                Is.EqualTo(1f));
            Assert.That(
                RagdollMuscleBoostMath.ApplyImpulseMultiplier(
                    float.NaN,
                    float.PositiveInfinity),
                Is.EqualTo(0f));
        }

        [Test]
        public void ImpulseScaling_SaturatesInsteadOfOverflowing()
        {
            Assert.That(
                RagdollMuscleBoostMath.ApplyImpulseMultiplier(
                    float.MaxValue,
                    float.MaxValue),
                Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void TinyImpulseRemainder_SnapsToNeutralOne()
        {
            Assert.That(
                RagdollMuscleBoostMath.StepImpulseMultiplier(
                    1.00005f,
                    0.1f,
                    0.1f),
                Is.EqualTo(1f));
        }

        [Test]
        public void DirectionalFalloff_UsesOnlyAncestorsAndDescendants()
        {
            RagdollBoneTopology topology;
            string error;
            Assert.That(
                RagdollBoneTopology.TryCreate(
                    17,
                    3,
                    new[] { -1, 0, 1, 0 },
                    out topology,
                    out error),
                Is.True,
                error);

            RagdollBoneHandle root = new RagdollBoneHandle(17, 3, 0);
            RagdollBoneHandle parent = new RagdollBoneHandle(17, 3, 1);
            RagdollBoneHandle source = new RagdollBoneHandle(17, 3, 2);
            RagdollBoneHandle sibling = new RagdollBoneHandle(17, 3, 3);

            Assert.That(
                RagdollMuscleBoostMath.EvaluateDirectionalFalloff(
                    topology, source, source, 0.5f, 0.25f),
                Is.EqualTo(1f));
            Assert.That(
                RagdollMuscleBoostMath.EvaluateDirectionalFalloff(
                    topology, source, parent, 0.5f, 0.25f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                RagdollMuscleBoostMath.EvaluateDirectionalFalloff(
                    topology, source, root, 0.5f, 0.25f),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(
                RagdollMuscleBoostMath.EvaluateDirectionalFalloff(
                    topology, root, source, 0.5f, 0.25f),
                Is.EqualTo(0.0625f).Within(0.0001f));
            Assert.That(
                RagdollMuscleBoostMath.EvaluateDirectionalFalloff(
                    topology, source, sibling, 0.5f, 0.25f),
                Is.EqualTo(0f));
        }
    }
}
