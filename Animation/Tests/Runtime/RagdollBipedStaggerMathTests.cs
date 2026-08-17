using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBipedStaggerMathTests
    {
        [Test]
        public void SelectStepFoot_PicksTheFootFartherFromTheCapturePoint()
        {
            // Capture point sits directly above the left foot; the right foot is
            // trailing and should swing to widen support under the capture point.
            RagdollBipedStepFoot foot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint: Vector3.left,
                leftFoot: Vector3.left,
                rightFoot: Vector3.right);
            Assert.That(foot, Is.EqualTo(RagdollBipedStepFoot.Right));
        }

        [Test]
        public void SelectStepFoot_MirroredCase_PicksTheOtherFoot()
        {
            RagdollBipedStepFoot foot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint: Vector3.right,
                leftFoot: Vector3.left,
                rightFoot: Vector3.right);
            Assert.That(foot, Is.EqualTo(RagdollBipedStepFoot.Left));
        }

        [Test]
        public void SelectStepFoot_UsesExplicitSupportUp()
        {
            RagdollBipedStepFoot foot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint: Vector3.up,
                leftFoot: Vector3.zero,
                rightFoot: Vector3.forward,
                supportUp: Vector3.right);

            Assert.That(foot, Is.EqualTo(RagdollBipedStepFoot.Right));
        }

        [Test]
        public void SelectStepFoot_CompatibilityOverloadMatchesExplicitWorldUp()
        {
            Vector3 capturePoint = new Vector3(0.2f, 4f, -0.7f);
            Vector3 leftFoot = new Vector3(-0.4f, 0.3f, 0.1f);
            Vector3 rightFoot = new Vector3(0.5f, -0.2f, -0.2f);

            Assert.That(
                RagdollBipedStaggerMath.SelectStepFoot(capturePoint, leftFoot, rightFoot),
                Is.EqualTo(RagdollBipedStaggerMath.SelectStepFoot(
                    capturePoint, leftFoot, rightFoot, Vector3.up)));
        }

        [Test]
        public void SelectStepFoot_OneSupportedFootAlwaysSelectsTheAirborneFoot()
        {
            RagdollBipedStepFoot foot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint: new Vector3(20f, 0f, 0f),
                hasLeftFootSupport: true,
                leftFoot: Vector3.zero,
                hasRightFootSupport: false,
                rightFoot: new Vector3(100f, 0f, 0f),
                supportUp: Vector3.up);

            Assert.That(foot, Is.EqualTo(RagdollBipedStepFoot.Right));
        }

        [Test]
        public void SelectStepFoot_RightSupportedFootAlwaysSelectsTheAirborneFoot()
        {
            RagdollBipedStepFoot foot = RagdollBipedStaggerMath.SelectStepFoot(
                capturePoint: new Vector3(-20f, 0f, 0f),
                hasLeftFootSupport: false,
                leftFoot: new Vector3(-100f, 0f, 0f),
                hasRightFootSupport: true,
                rightFoot: Vector3.zero,
                supportUp: Vector3.up);

            Assert.That(foot, Is.EqualTo(RagdollBipedStepFoot.Left));
        }

        [Test]
        public void ClampStepLength_WithinRange_ReturnsTargetUnchanged()
        {
            Vector3 clamped = RagdollBipedStaggerMath.ClampStepLength(
                landingTarget: new Vector3(0.3f, 0f, 0f),
                stanceFootPosition: Vector3.zero,
                maxStepLength: 0.6f);
            Assert.That(clamped, Is.EqualTo(new Vector3(0.3f, 0f, 0f)));
        }

        [Test]
        public void ClampStepLength_BeyondRange_ClampsToMaxDistanceAlongSameDirection()
        {
            Vector3 clamped = RagdollBipedStaggerMath.ClampStepLength(
                landingTarget: new Vector3(2f, 0f, 0f),
                stanceFootPosition: Vector3.zero,
                maxStepLength: 0.6f);
            Assert.That(clamped.x, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(clamped.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ClassifyStepDirection_ForwardOffset_IsForward()
        {
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset: Vector3.forward, bodyFrontAxis: Vector3.forward, bodyUpAxis: Vector3.up);
            Assert.That(direction, Is.EqualTo(RagdollBipedStepDirection.Forward));
        }

        [Test]
        public void ClassifyStepDirection_BackwardOffset_IsBackward()
        {
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset: Vector3.back, bodyFrontAxis: Vector3.forward, bodyUpAxis: Vector3.up);
            Assert.That(direction, Is.EqualTo(RagdollBipedStepDirection.Backward));
        }

        [Test]
        public void ClassifyStepDirection_RightOffset_IsRight()
        {
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset: Vector3.right, bodyFrontAxis: Vector3.forward, bodyUpAxis: Vector3.up);
            Assert.That(direction, Is.EqualTo(RagdollBipedStepDirection.Right));
        }

        [Test]
        public void ClassifyStepDirection_LeftOffset_IsLeft()
        {
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset: Vector3.left, bodyFrontAxis: Vector3.forward, bodyUpAxis: Vector3.up);
            Assert.That(direction, Is.EqualTo(RagdollBipedStepDirection.Left));
        }

        [Test]
        public void ClassifyStepDirection_RotatedBodyAxes_ClassifiesRelativeToBody()
        {
            // Body faces world +X; a world +Z offset is to the body's left.
            RagdollBipedStepDirection direction = RagdollBipedStaggerMath.ClassifyStepDirection(
                offset: Vector3.forward, bodyFrontAxis: Vector3.right, bodyUpAxis: Vector3.up);
            Assert.That(direction, Is.EqualTo(RagdollBipedStepDirection.Left));
        }

        [TestCase(RagdollBipedBalanceState.Stable)]
        [TestCase(RagdollBipedBalanceState.RecoverableWithoutStep)]
        public void ResolveOutcome_AlreadyBalanced_Succeeds(RagdollBipedBalanceState classification)
        {
            RagdollBipedStaggerOutcome outcome = RagdollBipedStaggerMath.ResolveOutcome(
                classification, stepCount: 1, maxSteps: 3);
            Assert.That(outcome, Is.EqualTo(RagdollBipedStaggerOutcome.Succeeded));
        }

        [Test]
        public void ResolveOutcome_RequiresStepWithBudgetRemaining_Continues()
        {
            RagdollBipedStaggerOutcome outcome = RagdollBipedStaggerMath.ResolveOutcome(
                RagdollBipedBalanceState.RequiresStep, stepCount: 1, maxSteps: 3);
            Assert.That(outcome, Is.EqualTo(RagdollBipedStaggerOutcome.Continue));
        }

        [Test]
        public void ResolveOutcome_RequiresStepAtMaxSteps_Fails()
        {
            RagdollBipedStaggerOutcome outcome = RagdollBipedStaggerMath.ResolveOutcome(
                RagdollBipedBalanceState.RequiresStep, stepCount: 3, maxSteps: 3);
            Assert.That(outcome, Is.EqualTo(RagdollBipedStaggerOutcome.Failed));
        }

        [Test]
        public void ResolveOutcome_Unrecoverable_FailsEvenWithBudgetRemaining()
        {
            RagdollBipedStaggerOutcome outcome = RagdollBipedStaggerMath.ResolveOutcome(
                RagdollBipedBalanceState.Unrecoverable, stepCount: 0, maxSteps: 3);
            Assert.That(outcome, Is.EqualTo(RagdollBipedStaggerOutcome.Failed));
        }
    }
}
