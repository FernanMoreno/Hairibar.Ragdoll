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
        public void SignedSupportMargin_UsesExplicitSupportUp()
        {
            float margin = RagdollBipedBalanceMath.SignedSupportMargin(
                point: Vector3.up,
                leftFoot: Vector3.zero,
                rightFoot: Vector3.up,
                supportRadius: 0.25f,
                supportUp: Vector3.right);

            Assert.That(margin, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void SignedSupportMargin_OneContactIgnoresAirborneEndpoint()
        {
            float margin = RagdollBipedBalanceMath.SignedSupportMargin(
                point: Vector3.zero,
                hasLeftFootSupport: true,
                leftFoot: Vector3.zero,
                hasRightFootSupport: false,
                rightFoot: new Vector3(100f, 0f, 0f),
                supportRadius: 0.25f,
                supportUp: Vector3.up);

            Assert.That(margin, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void SignedSupportMargin_ZeroContactReturnsFiniteNonPositiveSentinel()
        {
            float margin = RagdollBipedBalanceMath.SignedSupportMargin(
                point: Vector3.zero,
                hasLeftFootSupport: false,
                leftFoot: Vector3.left,
                hasRightFootSupport: false,
                rightFoot: Vector3.right,
                supportRadius: 0.25f,
                supportUp: Vector3.up);

            Assert.That(float.IsNaN(margin), Is.False);
            Assert.That(float.IsInfinity(margin), Is.False);
            Assert.That(margin, Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void Classify_ZeroContactIsUnrecoverableEvenWithStepBudget()
        {
            RagdollBipedBalanceState state = RagdollBipedBalanceMath.Classify(
                signedSupportMargin: -0.15f,
                supportPointCount: 0,
                stableMargin: StableMargin,
                requiresStepMargin: RequiresStepMargin);

            Assert.That(state, Is.EqualTo(RagdollBipedBalanceState.Unrecoverable));
        }

        [Test]
        public void SignedSupportMargin_TwoContactPointsMatchesLegacyTwoFootGeometry()
        {
            float legacy = RagdollBipedBalanceMath.SignedSupportMargin(
                Vector3.zero, Vector3.left, Vector3.right, 0.25f, Vector3.up);
            float contactBacked = RagdollBipedBalanceMath.SignedSupportMargin(
                point: Vector3.zero,
                hasLeftFootSupport: true,
                leftFoot: Vector3.left,
                hasRightFootSupport: true,
                rightFoot: Vector3.right,
                supportRadius: 0.25f,
                supportUp: Vector3.up);

            Assert.That(contactBacked, Is.EqualTo(legacy).Within(0.0001f));
        }

        [Test]
        public void CapturePoint_CompatibilityOverloadMatchesExplicitWorldUp()
        {
            Vector3 centerOfMass = new Vector3(0.2f, 1.1f, -0.3f);
            Vector3 velocity = new Vector3(0.4f, 3f, -0.7f);

            Assert.That(
                RagdollBipedBalanceMath.CapturePoint(
                    centerOfMass, velocity, 0.9f, 9.81f),
                Is.EqualTo(RagdollBipedBalanceMath.CapturePoint(
                    centerOfMass, velocity, 0.9f, 9.81f, Vector3.up)));
        }

        [Test]
        public void CapturePoint_ProjectsVelocityAgainstArbitrarySupportUp()
        {
            Vector3 point = RagdollBipedBalanceMath.CapturePoint(
                Vector3.zero, Vector3.up, 1f, 9.81f, Vector3.right);

            Assert.That(point.y, Is.GreaterThan(0f));
            Assert.That(point.x, Is.EqualTo(0f).Within(0.0001f));
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
