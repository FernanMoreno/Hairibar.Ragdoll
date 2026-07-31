using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollIKSchedulerTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("IK Scheduler Tests");
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
        }

        [Test]
        public void InterfaceSolver_ExposesIndependentAutomaticAndEnabledState()
        {
            TestIKSolver solver = root.AddComponent<TestIKSolver>();
            IRagdollIKSolver contract = solver;

            Assert.That(contract.IsSolverEnabled, Is.True);
            Assert.That(contract.AutomaticUpdates, Is.True);

            contract.AutomaticUpdates = false;
            contract.Solve();

            Assert.That(solver.SolveCount, Is.EqualTo(1));
            Assert.That(contract.AutomaticUpdates, Is.False);
        }

        [Test]
        public void Configure_NormalizesNullSolverArray()
        {
            GameObject schedulerObject = new GameObject("Scheduler");
            schedulerObject.SetActive(false);
            schedulerObject.transform.SetParent(root.transform);
            RagdollIKScheduler scheduler =
                schedulerObject.AddComponent<RagdollIKScheduler>();

            Assert.DoesNotThrow(
                () => scheduler.Configure(
                    null,
                    RagdollIKSolvePhase.AfterPhysics,
                    null));
            Assert.That(scheduler.SolvePhase, Is.EqualTo(
                RagdollIKSolvePhase.AfterPhysics));
        }

        sealed class TestIKSolver : MonoBehaviour, IRagdollIKSolver
        {
            public int SolveCount { get; private set; }
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;
            public void Solve() => SolveCount++;
        }
    }
}
