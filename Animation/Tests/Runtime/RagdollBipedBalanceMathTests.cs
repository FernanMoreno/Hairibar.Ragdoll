using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBipedBalanceMathTests
    {
        const float StableMargin = 0.05f;
        const float RequiresStepMargin = 0.25f;

        [Test]
        public void Classify_MarginAboveStableThreshold_IsStable()
        {
            var state = RagdollBipedBalanceMath.Classify(0.10f, StableMargin, RequiresStepMargin);
            Assert.That(state, Is.EqualTo(RagdollBipedBalanceState.Stable));
        }

        [Test]
        public void Classify_PositiveMarginBelowStableThreshold_IsRecoverableWithoutStep()
        {
            var state = RagdollBipedBalanceMath.Classify(0.02f, StableMargin, RequiresStepMargin);
            Assert.That(state, Is.EqualTo(RagdollBipedBalanceState.RecoverableWithoutStep));
        }

        [Test]
        public void Classify_NegativeMarginWithinStepRange_RequiresStep()
        {
            var state = RagdollBipedBalanceMath.Classify(-0.10f, StableMargin, RequiresStepMargin);
            Assert.That(state, Is.EqualTo(RagdollBipedBalanceState.RequiresStep));
        }

        [Test]
        public void Classify_MarginBeyondStepRange_IsUnrecoverable()
        {
            var state = RagdollBipedBalanceMath.Classify(-0.30f, StableMargin, RequiresStepMargin);
            Assert.That(state, Is.EqualTo(RagdollBipedBalanceState.Unrecoverable));
        }

        [Test]
        public void SignedSupportMargin_Centof_BetweenFeet_EqualsRadius()
        {
            float margin = RagdollBipedBalanceMath.SignedSupportMargin(
                Vector3.zero, Vector3.left, Vector3.right, 0.5f);
            Assert.That(margin, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SignedSupportMargin_PointOutsideSegment_ReducesMargin()
        {
            float margin = RagdollBipedBalanceMath.SignedSupportMargin(
                new Vector3(2f, 0f, 0f), Vector3.left, Vector3.right, 0.5f);
            // Nearest point on the [-1,0,0]-[1,0,0] segment is (1,0,0); distance 1.
            Assert.That(margin, Is.EqualTo(0.5f - 1f).Within(0.0001f));
        }

        [Test]
        public void SignedCaptureMargin_StationaryComCenteredOnFeet_MatchesSupportMargin()
        {
            float margin = RagdollBipedBalanceMath.SignedCaptureMargin(
                centerOfMass: Vector3.zero,
                centerOfMassVelocity: Vector3.zero,
                leftFoot: Vector3.left,
                rightFoot: Vector3.right,
                pendulumLength: 0.9f,
                gravity: 9.81f,
                supportRadius: 0.5f);
            Assert.That(margin, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void CapturePoint_ForwardVelocity_ProjectsInFrontOfCom()
        {
            Vector3 point = RagdollBipedBalanceMath.CapturePoint(
                Vector3.zero, Vector3.forward, 1f, 9.81f);
            Assert.That(point.z, Is.GreaterThan(0f));
            Assert.That(point.y, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
