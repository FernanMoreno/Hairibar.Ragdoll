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
        [Test] public void B08_KillBlendAndDeadDrive()
        {
            var tests = new RagdollLifecycleMathTests();
            tests.KillWeight_BlendsLinearlyToDeadWeight();
            tests.DeadDrive_ReleasesPositionAndScalesRotation();
        }

        [Test] public void B09_TemporaryAndPermanentFreeze()
        {
            var tests = new RagdollLifecycleMathTests();
            tests.FreezeVelocity_UsesInclusiveSquaredThreshold(0f, 0f, true);
            tests.FreezeVelocity_NonFiniteValuesNeverFreeze();
        }

        [Test] public void B10_LifecycleLimitsAndCollisionRollback()
        {
            RunFixture(
                new RagdollLifecyclePhysicsPolicyTests(),
                test => test.KillPolicy_AppliesAuthoredLimitsAndRestoresExactPreKillState());
        }

        [Test] public void B19_SimulationModesRespectLifecycleOwnership()
        {
            var tests = new RagdollSimulationModePolicyTests();
            tests.ActiveKeepsHierarchyAndAuthoredPower();
            tests.LifecycleOwnership_BlocksUserModeChangesOutsideAlive();
            tests.DisabledDeactivatesHierarchyAndHasNoDriveWeight();
        }

        [Test] public void B20_InternalCollisionsRestoreAcrossLifecycle()
        {
            RunFixture(
                new RagdollInternalCollisionTests(),
                test => test.LifecycleEnd_UsesGlobalValueChangedWhileOverrideWasActive());
        }

        [UnityTest] public IEnumerator B21_BranchAuthoritySurvivesCollectionMutation()
        {
            return RunSetupServiceIntegration(test =>
                test.OfficialStateWeightsAndModeFacadesAffectLiveRuntime());
        }

        [UnityTest] public IEnumerator B23_ManualAndLegacyUpdateLifecycle()
        {
            return new RagdollManualSimulationPlayModeTests()
                .ManualStep_EnforcesOrderAndRestoresLegacyAnimation();
        }

        [UnityTest] public IEnumerator B24_AllCoreHooksAreOrderedAndIsolated()
        {
            return RunSetupServiceIntegration(test =>
                test.CoreHooksPreserveOrderAndIsolateEverySubscriber());
        }

        [UnityTest] public IEnumerator B26_CompleteCollectionCommitsAndRollsBackAtomically()
        {
            return RunSetupServiceIntegration(test =>
                test.CollectionValidationFailuresLeaveRegistryAndPhysicsUntouched());
        }

        [Test] public void B27_DisconnectReconnectPreservesMappingContract()
        {
            var tests = new RagdollMuscleConnectionPolicyTests();
            tests.Reconnect_UsesHighestContiguousDisconnectedAncestor();
            tests.DisconnectedMapping_SuppressesNormalPassAndHonoursToggle();
        }

        [Test] public void B28_DisconnectedJointIsExcludedFromLifecycleWrites()
        {
            RunFixture(
                new RagdollLifecyclePhysicsPolicyTests(),
                test => test.DisconnectedJoint_IsExcludedFromGlobalAndLifecycleWrites());
        }

        [Test] public void B29_FlatTreeConversionPreservesTopologyAndPose()
        {
            var tests = new RagdollRuntimeAuthoringTests();
            try { tests.AuthoredRig_FlatAndTreePreserveJointTopologyAndWorldPose(); }
            finally { tests.TearDown(); }
        }

        [Test] public void C04_SerializedPuppetEventsHaveDeterministicPhases()
        {
            RunFixture(
                new RagdollPuppetBehaviourEventIntegrationTests(),
                test => test.CollisionObserved_PrecedesFiltersAndIncludesAllPhases());
        }

        [Test] public void C07_ReactivationTeleportAndSubscriberExceptions()
        {
            var collisions = new RagdollInternalCollisionTests();
            collisions.SetUp();
            try
            {
                collisions.ReapplyCurrentPolicy_RestoresAutomaticStateLostByReactivation();
            }
            finally { collisions.TearDown(); }

            new AnimatedPoseSamplerTests()
                .Teleport_TransformsWorldPoseAndClearsCachedVelocities();
            RunFixture(
                new RagdollPuppetBehaviourEventIntegrationTests(),
                test => test.CollisionSubscribers_AreIsolatedAndOfficialAliasSharesStream());
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

        [Test] public void D18_MasterPinAndMuscleAreIndependent()
        {
            var tests = new RagdollMasterAuthorityTests();
            tests.PinAndMuscleWeightsAreIndependent(0f, 1f, 0f, 8f);
            tests.PinAndMuscleWeightsAreIndependent(1f, 0f, 1f, 0f);
        }

        [Test] public void D26_UnpinnedVelocityLimitHandlesExtremeValues()
        {
            var tests = new RagdollPuppetBehaviourMathTests();
            tests.VelocityLimit_ClampsMagnitudeAndPreservesDirection();
            tests.VelocityLimit_InfinityLeavesVelocityUntouched();
        }

        [Test] public void D28_AuthoredZeroPinKnockoutIsConfigurable()
        {
            var tests = new RagdollPuppetBehaviourMathTests();
            tests.ZeroConfiguredPin_IsIgnoredWhenUnpinnedMuscleKnockoutIsDisabled();
            tests.ZeroConfiguredPin_CanKnockOutWhenOptionIsEnabled();
        }

        [Test] public void TeleportPreservesGetUpPipeline_LegacyCoverage()
        {
            var tests = new RagdollPuppetBehaviourMathTests();
            tests.TeleportMoveToTarget_CompletesOnlyTheGetUpBlend();
            tests.TeleportRotation_TransformsAndNormalizesCachedGroundDirection();
        }

        [Test] public void F07_AnimatedTargetChildrenRemainExplicitAndNullSafe()
        {
            var tests = new RagdollTargetBindingTests();
            try { tests.AnimatedTargetChildren_AreExplicitAndNullSafe(); }
            finally { tests.TearDown(); }
        }

        [Test] public void F08_PropHandleRebindsAfterRegistryGenerationChange()
        {
            new RagdollPropMuscleStateMachineTests()
                .HandleIsResolvedEveryTick_ForRegistryGenerationChanges();
        }

        [Test] public void F13_TimedMeleeActionRestoresAtSafeBoundary()
        {
            var tests = new RagdollPropMeleeTests();
            tests.TimedAction_RestartsAndExpiresAtSafeBoundary();
            tests.DropRequest_CancelsActionBeforeDisconnectCompletes();
        }

        [Test] public void G02_DeterministicExternalSolverImplementsGenericContract()
        {
            RunFixture(
                new RagdollIKSchedulerTests(),
                test => test.InterfaceSolver_ExposesIndependentAutomaticAndEnabledState());
        }

        [Test] public void G04_SolverHooksAreIsolatedAroundReadWrite()
        {
            RunFixture(
                new RagdollIKSchedulerTests(),
                test => test.ReadWriteHook_RunsMatchingSolversAndIsolatesFailures());
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

        static void RunFixture(
            RagdollPuppetBehaviourEventIntegrationTests fixture,
            Action<RagdollPuppetBehaviourEventIntegrationTests> body)
        {
            fixture.SetUp();
            try { body(fixture); }
            finally { fixture.TearDown(); }
        }

        static void RunFixture(
            RagdollIKSchedulerTests fixture,
            Action<RagdollIKSchedulerTests> body)
        {
            fixture.SetUp();
            try { body(fixture); }
            finally { fixture.TearDown(); }
        }
    }
}
