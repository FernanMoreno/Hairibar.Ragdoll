using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// ID-stable closure tests. Each test executes the existing integration contract
    /// behind the matching coverage row so the matrix can link to a durable NUnit ID.
    /// </summary>
    public sealed class RagdollClosureManifestPlayModeTests
    {
        [Test] public void LegacyKillBlendAndDeadDrive()
        {
            var tests = new RagdollLifecycleMathTests();
            tests.KillWeight_BlendsLinearlyToDeadWeight();
            tests.DeadDrive_ReleasesPositionAndScalesRotation();
        }

        [Test] public void LegacyTemporaryAndPermanentFreeze()
        {
            var tests = new RagdollLifecycleMathTests();
            tests.FreezeVelocity_UsesInclusiveSquaredThreshold(0f, 0f, true);
            tests.FreezeVelocity_NonFiniteValuesNeverFreeze();
        }

        [Test] public void LegacyLifecycleLimitsAndCollisionRollback()
        {
            RunFixture(
                new RagdollLifecyclePhysicsPolicyTests(),
                test => test.KillPolicy_AppliesAuthoredLimitsAndRestoresExactPreKillState());
        }

        [Test] public void LegacySimulationModesRespectLifecycleOwnership()
        {
            var tests = new RagdollSimulationModePolicyTests();
            tests.ActiveKeepsHierarchyAndAuthoredPower();
            tests.LifecycleOwnership_BlocksUserModeChangesOutsideAlive();
            tests.DisabledDeactivatesHierarchyAndHasNoDriveWeight();
        }

        [Test] public void LegacyInternalCollisionsRestoreAcrossLifecycle()
        {
            RunFixture(
                new RagdollInternalCollisionTests(),
                test => test.LifecycleEnd_UsesGlobalValueChangedWhileOverrideWasActive());
        }

        [UnityTest] public IEnumerator LegacyBranchAuthoritySurvivesCollectionMutation()
        {
            return RunSetupServiceIntegration(test =>
                test.OfficialStateWeightsAndModeFacadesAffectLiveRuntime());
        }

        [UnityTest] public IEnumerator LegacyManualAndLegacyUpdateLifecycle()
        {
            return new RagdollManualSimulationPlayModeTests()
                .ManualStep_EnforcesOrderAndRestoresLegacyAnimation();
        }

        [UnityTest] public IEnumerator LegacyAllCoreHooksAreOrderedAndIsolated()
        {
            return RunSetupServiceIntegration(test =>
                test.CoreHooksPreserveOrderAndIsolateEverySubscriber());
        }

        [UnityTest] public IEnumerator LegacyCompleteCollectionCommitsAndRollsBackAtomically()
        {
            return RunSetupServiceIntegration(test =>
                test.CollectionValidationFailuresLeaveRegistryAndPhysicsUntouched());
        }

        [Test] public void LegacyDisconnectReconnectPreservesMappingContract()
        {
            var tests = new RagdollMuscleConnectionPolicyTests();
            tests.Reconnect_UsesHighestContiguousDisconnectedAncestor();
            tests.DisconnectedMapping_SuppressesNormalPassAndHonoursToggle();
        }

        [Test] public void LegacyDisconnectedJointIsExcludedFromLifecycleWrites()
        {
            RunFixture(
                new RagdollLifecyclePhysicsPolicyTests(),
                test => test.DisconnectedJoint_IsExcludedFromGlobalAndLifecycleWrites());
        }

        [Test] public void LegacyFlatTreeConversionPreservesTopologyAndPose()
        {
            var tests = new RagdollRuntimeAuthoringTests();
            try { tests.AuthoredRig_FlatAndTreePreserveJointTopologyAndWorldPose(); }
            finally { tests.TearDown(); }
        }

        [Test] public void LegacyActiveModeMathUsesFullPuppetMapping()
        {
            new RagdollPuppetNormalModeMathTests()
                .ActiveModeAlwaysRequestsFullMappingInPuppet();
        }

        [Test] public void LegacyUnmappedModeMathRequiresRecentContact()
        {
            var mode = new RagdollPuppetNormalModeMathTests();
            mode.UnmappedModeRequiresRecentContactInPuppet();
            new RagdollPuppetUnmappedContactTrackerTests()
                .ContactRemainsRecentAcrossOnePhysicsStep();
        }

        [Test] public void LegacyKinematicModeMathRequiresAcceptedContact()
        {
            var tests = new RagdollPuppetKinematicActivationPolicyTests();
            tests.SourceClassificationSeparatesStaticKinematicAndDynamic();
            tests.OnlyKinematicPuppetStateCanQueue(
                RagdollPuppetNormalMode.Kinematic,
                RagdollPuppetState.Unpinned);
        }

        [Test] public void LegacyMappingBlendMathIsRateLimitedWithoutOvershoot()
        {
            new RagdollPuppetNormalModeMathTests()
                .StepMappingWeightUsesUnitsPerSecondWithoutOvershoot();
        }

        [Test] public void LegacyStaticAndKinematicContactPolicyIsExplicit()
        {
            new RagdollPuppetKinematicActivationPolicyTests()
                .StaticAndKinematicSourcesUseStaticActivationFlag();
        }

        [Test] public void LegacyMinimumActivationImpulseIsInclusiveAndFinite()
        {
            var tests = new RagdollPuppetKinematicActivationPolicyTests();
            tests.MinimumImpulseIsInclusive();
            tests.InvalidImpulseFailsClosed(float.NaN);
        }

        [Test] public void TeleportPreservesGetUpPipeline_LegacyCoverage()
        {
            var tests = new RagdollPuppetBehaviourMathTests();
            tests.TeleportMoveToTarget_CompletesOnlyTheGetUpBlend();
            tests.TeleportRotation_TransformsAndNormalizesCachedGroundDirection();
        }

        static IEnumerator RunSetupServiceIntegration(
            Func<RagdollRuntimeSetupServiceTests, IEnumerator> select)
        {
            var tests = new RagdollRuntimeSetupServiceTests();
            tests.SetUp();
            try
            {
                IEnumerator routine = select(tests);
                while (routine.MoveNext()) yield return routine.Current;
            }
            finally
            {
                tests.TearDown();
            }
        }

        static void RunFixture(
            RagdollLifecyclePhysicsPolicyTests fixture,
            Action<RagdollLifecyclePhysicsPolicyTests> body)
        {
            fixture.SetUp();
            try { body(fixture); }
            finally { fixture.TearDown(); }
        }

        static void RunFixture(
            RagdollInternalCollisionTests fixture,
            Action<RagdollInternalCollisionTests> body)
        {
            fixture.SetUp();
            try { body(fixture); }
            finally { fixture.TearDown(); }
        }

    }
}
