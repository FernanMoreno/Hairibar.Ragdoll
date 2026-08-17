using NUnit.Framework;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBipedStaggerStateMachineTests
    {
        const float LiftOff = 0.1f;
        const float Swing = 0.2f;
        const float Replant = 0.1f;
        const float Settling = 0.15f;

        [Test]
        public void NewMachine_StartsAtIdleWithZeroSteps()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Idle));
            Assert.That(machine.StepCount, Is.EqualTo(0));
        }

        [Test]
        public void TryBeginStep_FromIdle_EntersLiftOffAndIncrementsStepCount()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            bool began = machine.TryBeginStep(maxSteps: 3);
            Assert.That(began, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.LiftOff));
            Assert.That(machine.StepCount, Is.EqualTo(1));
        }

        [Test]
        public void TryBeginStep_AtMaxSteps_FailsInstead()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 1);
            CompleteOneCycle(machine);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Idle));

            bool began = machine.TryBeginStep(maxSteps: 1);

            Assert.That(began, Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Failed));
        }

        [Test]
        public void Advance_BeforePhaseDurationElapsed_StaysInSamePhase()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);

            bool completed = machine.Advance(LiftOff * 0.5f, LiftOff, Swing, Replant, Settling);

            Assert.That(completed, Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.LiftOff));
        }

        [Test]
        public void Advance_PhaseDurationElapsed_MovesToNextPhase()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);

            bool completed = machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling);

            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Swing));
        }

        [Test]
        public void Advance_LiftOffContactLoss_PreemptsTimeout()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);

            machine.Advance(0.02f, LiftOff, Swing, Replant, Settling,
                GroundedSignals);
            bool completed = machine.Advance(0.02f, LiftOff, Swing, Replant, Settling,
                new RagdollBipedStaggerPhaseSignals(
                    selectedFootGrounded: false,
                    animatorStateAvailable: false,
                    animatorNormalizedTime: 0f,
                    balanceRecovered: false));

            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Swing));
            Assert.That(machine.LiftOffContactObserved, Is.True);
        }

        [Test]
        public void Advance_SwingIgnoresPreLiftGroundedContact()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);

            machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling, GroundedSignals);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Swing));

            bool completed = machine.Advance(0.02f, LiftOff, Swing, Replant, Settling,
                GroundedSignals);

            Assert.That(completed, Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Swing));
        }

        [Test]
        public void Advance_SwingContactReacquisition_EntersReplant()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            machine.Advance(0f, LiftOff, Swing, Replant, Settling,
                AirborneSignals);

            bool completed = machine.Advance(0.02f, LiftOff, Swing, Replant, Settling,
                GroundedSignals);

            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Replant));

            completed = machine.Advance(0.02f, LiftOff, Swing, Replant, Settling,
                GroundedSignals);
            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Settling));
        }

        [Test]
        public void Advance_SwingMatchingAnimatorProgress_EntersReplant()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling,
                AirborneSignals);

            bool completed = machine.Advance(
                0.02f, LiftOff, Swing, Replant, Settling,
                new RagdollBipedStaggerPhaseSignals(
                    selectedFootGrounded: false,
                    animatorStateAvailable: true,
                    animatorNormalizedTime: 0.8f,
                    balanceRecovered: false),
                animatorReplantProgress: 0.75f);

            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Replant));
        }

        [Test]
        public void Advance_MismatchedOrNonFiniteAnimatorProgress_DoesNotCompleteEarly()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling,
                AirborneSignals);

            bool completed = machine.Advance(
                0.02f, LiftOff, Swing, Replant, Settling,
                new RagdollBipedStaggerPhaseSignals(
                    selectedFootGrounded: false,
                    animatorStateAvailable: false,
                    animatorNormalizedTime: 0.99f,
                    balanceRecovered: false),
                animatorReplantProgress: 0.75f);

            Assert.That(completed, Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Swing));

            completed = machine.Advance(
                Swing, LiftOff, Swing, Replant, Settling,
                new RagdollBipedStaggerPhaseSignals(
                    selectedFootGrounded: false,
                    animatorStateAvailable: true,
                    animatorNormalizedTime: float.NaN,
                    balanceRecovered: false),
                animatorReplantProgress: 0.75f);

            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Replant),
                "The configured Swing duration remains the fail-safe timeout.");
        }

        [Test]
        public void Advance_SettlingRequiresContinuousRecoveredWindow()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            CompleteToSettling(machine);

            bool completed = machine.Advance(
                0.05f, LiftOff, Swing, Replant, Settling,
                RecoveredSignals, stableBalanceDuration: 0.1f);
            Assert.That(completed, Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Settling));

            machine.Advance(
                0.02f, LiftOff, Swing, Replant, Settling,
                GroundedSignals, stableBalanceDuration: 0.1f);
            completed = machine.Advance(
                0.1f, LiftOff, Swing, Replant, Settling,
                RecoveredSignals, stableBalanceDuration: 0.1f);

            Assert.That(completed, Is.True);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Idle));
        }

        [Test]
        public void FullCycle_LiftOffSwingReplantSettling_ReturnsToIdle()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);

            CompleteOneCycle(machine);

            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Idle));
        }

        [Test]
        public void TryBeginStep_AfterSettling_BeginsSecondStepAndIncrementsCount()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            CompleteOneCycle(machine);

            bool began = machine.TryBeginStep(maxSteps: 3);

            Assert.That(began, Is.True);
            Assert.That(machine.StepCount, Is.EqualTo(2));
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.LiftOff));
        }

        [Test]
        public void RegisterStepFailed_ForcesFailedRegardlessOfPhase()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling);

            machine.RegisterStepFailed();

            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Failed));
        }

        [Test]
        public void TryBeginStep_WhenFailed_DoesNothing()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.RegisterStepFailed();

            bool began = machine.TryBeginStep(maxSteps: 3);

            Assert.That(began, Is.False);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Failed));
        }

        [Test]
        public void Reset_ReturnsToIdleAndClearsStepCount()
        {
            var machine = new RagdollBipedStaggerStateMachine();
            machine.TryBeginStep(maxSteps: 3);
            CompleteOneCycle(machine);
            machine.TryBeginStep(maxSteps: 3);

            machine.Reset();

            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Idle));
            Assert.That(machine.StepCount, Is.EqualTo(0));
        }

        static void CompleteOneCycle(RagdollBipedStaggerStateMachine machine)
        {
            machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling, NoEvidenceSignals); // LiftOff -> Swing
            machine.Advance(Swing, LiftOff, Swing, Replant, Settling, NoEvidenceSignals);   // Swing -> Replant
            machine.Advance(Replant, LiftOff, Swing, Replant, Settling, NoEvidenceSignals); // Replant -> Settling
            machine.Advance(Settling, LiftOff, Swing, Replant, Settling, NoEvidenceSignals); // Settling -> Idle
        }

        static void CompleteToSettling(RagdollBipedStaggerStateMachine machine)
        {
            machine.Advance(LiftOff, LiftOff, Swing, Replant, Settling, AirborneSignals);
            machine.Advance(Swing, LiftOff, Swing, Replant, Settling, GroundedSignals);
            machine.Advance(Replant, LiftOff, Swing, Replant, Settling, GroundedSignals);
            Assert.That(machine.State, Is.EqualTo(RagdollBipedStaggerState.Settling));
        }

        static readonly RagdollBipedStaggerPhaseSignals NoEvidenceSignals =
            new RagdollBipedStaggerPhaseSignals(false, false, 0f, false);
        static readonly RagdollBipedStaggerPhaseSignals AirborneSignals =
            new RagdollBipedStaggerPhaseSignals(false, false, 0f, false);
        static readonly RagdollBipedStaggerPhaseSignals GroundedSignals =
            new RagdollBipedStaggerPhaseSignals(true, false, 0f, false);
        static readonly RagdollBipedStaggerPhaseSignals RecoveredSignals =
            new RagdollBipedStaggerPhaseSignals(true, false, 0f, true);
    }
}
