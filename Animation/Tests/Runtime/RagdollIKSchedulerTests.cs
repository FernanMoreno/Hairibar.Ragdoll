using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            if (root) UnityEngine.Object.DestroyImmediate(root);
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

        [Test]
        public void ReadWriteHook_RunsMatchingSolversAndIsolatesFailures()
        {
            root.SetActive(false);
            RagdollAnimator animator = root.AddComponent<RagdollAnimator>();
            GameObject schedulerObject = new GameObject("Read IK Scheduler");
            schedulerObject.SetActive(false);
            try
            {
                ThrowingIKSolver failing = schedulerObject
                    .AddComponent<ThrowingIKSolver>();
                TestIKSolver passing = schedulerObject.AddComponent<TestIKSolver>();
                RagdollIKScheduler scheduler = schedulerObject
                    .AddComponent<RagdollIKScheduler>();
                scheduler.Configure(
                    animator,
                    RagdollIKSolvePhase.BeforePhysics,
                    new MonoBehaviour[] { failing, passing });
                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("expected deterministic IK failure"));

                schedulerObject.SetActive(true);
                InvokeHook(animator, "InvokeReadHooks");

                Assert.That(failing.SolveCount, Is.EqualTo(1));
                Assert.That(passing.SolveCount, Is.EqualTo(1));
                Assert.That(failing.AutomaticUpdates, Is.False);
                Assert.That(passing.AutomaticUpdates, Is.False);
                InvokeHook(animator, "InvokeWriteHooks");
                Assert.That(passing.SolveCount, Is.EqualTo(1),
                    "A BeforePhysics scheduler must not subscribe to OnWrite.");

                schedulerObject.SetActive(false);
                Assert.That(failing.AutomaticUpdates, Is.True);
                Assert.That(passing.AutomaticUpdates, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(schedulerObject);
            }
        }

        static void InvokeHook(RagdollAnimator animator, string name)
        {
            MethodInfo method = typeof(RagdollAnimator).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(animator, null);
        }

        sealed class TestIKSolver : MonoBehaviour, IRagdollIKSolver
        {
            public int SolveCount { get; private set; }
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;
            public void Solve() => SolveCount++;
        }

        sealed class ThrowingIKSolver : MonoBehaviour, IRagdollIKSolver
        {
            public int SolveCount { get; private set; }
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;

            public void Solve()
            {
                SolveCount++;
                throw new InvalidOperationException(
                    "expected deterministic IK failure");
            }
        }
    }
}
