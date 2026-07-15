using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollPuppetCollisionResponseMathTests
    {
        [Test]
        public void ConstantResistanceIsUsedWhenCurveIsDisabled()
        {
            float resistance =
                RagdollPuppetCollisionResponseMath.EvaluateGlobalResistance(
                    4f,
                    false,
                    AnimationCurve.Linear(0f, 1f, 10f, 10f),
                    8f);

            Assert.That(resistance, Is.EqualTo(4f));
        }

        [Test]
        public void TargetSpeedCurveReturnsAbsoluteResistance()
        {
            float resistance =
                RagdollPuppetCollisionResponseMath.EvaluateGlobalResistance(
                    3f,
                    true,
                    AnimationCurve.Linear(0f, 2f, 10f, 6f),
                    5f);

            Assert.That(resistance, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void NonPositiveCurveValueFallsBackToConstant()
        {
            AnimationCurve curve = new AnimationCurve(
                new Keyframe(0f, -1f));

            float resistance =
                RagdollPuppetCollisionResponseMath.EvaluateGlobalResistance(
                    5f,
                    true,
                    curve,
                    0f);

            Assert.That(resistance, Is.EqualTo(5f));
        }

        [Test]
        public void LayerResolutionUsesFirstMatchingRule()
        {
            RagdollPuppetCollisionLayerRule[] rules =
            {
                new RagdollPuppetCollisionLayerRule
                {
                    layers = 1 << 4,
                    resistanceMultiplier = 2f
                },
                new RagdollPuppetCollisionLayerRule
                {
                    layers = 1 << 4,
                    resistanceMultiplier = 8f
                }
            };

            RagdollPuppetCollisionResponseMath.LayerResolution result =
                RagdollPuppetCollisionResponseMath.ResolveLayer(
                    rules,
                    4,
                    1f);

            Assert.That(result.RuleIndex, Is.EqualTo(0));
            Assert.That(result.ResistanceMultiplier, Is.EqualTo(2f));
        }

        [Test]
        public void LayerRuleCanOverrideSquaredImpulseThreshold()
        {
            RagdollPuppetCollisionLayerRule[] rules =
            {
                new RagdollPuppetCollisionLayerRule
                {
                    layers = 1 << 7,
                    resistanceMultiplier = 1f,
                    overrideCollisionThreshold = true,
                    collisionThreshold = 9f
                }
            };

            RagdollPuppetCollisionResponseMath.LayerResolution result =
                RagdollPuppetCollisionResponseMath.ResolveLayer(
                    rules,
                    7,
                    2f);

            Assert.That(result.CollisionThreshold, Is.EqualTo(9f));
        }

        [Test]
        public void UnmatchedLayerKeepsDefaultPolicy()
        {
            RagdollPuppetCollisionResponseMath.LayerResolution result =
                RagdollPuppetCollisionResponseMath.ResolveLayer(
                    new RagdollPuppetCollisionLayerRule[0],
                    3,
                    2.5f);

            Assert.That(result.RuleIndex, Is.EqualTo(-1));
            Assert.That(result.ResistanceMultiplier, Is.EqualTo(1f));
            Assert.That(result.CollisionThreshold, Is.EqualTo(2.5f));
        }

        [Test]
        public void SuppressionComposesGlobalLayerAndMuscleResistance()
        {
            float suppression =
                RagdollPuppetCollisionResponseMath.EvaluatePositionSuppression(
                    6f,
                    3f,
                    2f,
                    0.5f);

            Assert.That(suppression, Is.EqualTo(1f));
        }

        [Test]
        public void SuppressionIsClampedAndRejectsInvalidImpulse()
        {
            Assert.That(
                RagdollPuppetCollisionResponseMath.EvaluatePositionSuppression(
                    100f,
                    2f,
                    1f,
                    1f),
                Is.EqualTo(1f));

            Assert.That(
                RagdollPuppetCollisionResponseMath.EvaluatePositionSuppression(
                    float.NaN,
                    2f,
                    1f,
                    1f),
                Is.EqualTo(0f));
        }

        [Test]
        public void EffectiveResistanceSaturatesInsteadOfOverflowing()
        {
            float resistance =
                RagdollPuppetCollisionResponseMath.EvaluateEffectiveResistance(
                    float.MaxValue,
                    float.MaxValue,
                    float.MaxValue);

            Assert.That(resistance, Is.EqualTo(float.MaxValue));
        }
    }
}
