using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public enum RagdollIKSolvePhase
    {
        BeforePhysics,
        AfterPhysics
    }

    /// <summary>
    /// Adapter contract for an external IK solver. Implement this on a component that
    /// wraps the chosen IK package; Hairibar.Ragdoll remains package-independent.
    /// </summary>
    [RagdollCompatibilityApi("Props and IK", "http://www.root-motion.com/puppetmasterdox/html/page7.html")]
    public interface IRagdollIKSolver
    {
        bool IsSolverEnabled { get; }
        bool AutomaticUpdates { get; set; }
        void Solve();
    }

    /// <summary>
    /// Runs external IK in serialized order immediately before Target sampling or
    /// immediately after Puppet-to-Target mapping.
    /// </summary>
    [AddComponentMenu("Ragdoll/Utilities/Ragdoll IK Scheduler")]
    [DisallowMultipleComponent]
    public sealed class RagdollIKScheduler : MonoBehaviour
    {
        struct SolverOwnership
        {
            public IRagdollIKSolver Solver;
            public bool PreviousAutomaticUpdates;
        }

        [SerializeField] RagdollAnimator ragdollAnimator;
        [SerializeField] RagdollIKSolvePhase solvePhase;
        [Tooltip("Components must implement IRagdollIKSolver. Array order is execution order.")]
        [SerializeField] MonoBehaviour[] solverComponents = new MonoBehaviour[0];
        [SerializeField] bool takeControlOfAutomaticUpdates = true;

        readonly List<SolverOwnership> solvers = new List<SolverOwnership>();
        bool subscribed;

        public RagdollAnimator Animator => ragdollAnimator;
        public RagdollIKSolvePhase SolvePhase => solvePhase;
        public bool TakesControlOfAutomaticUpdates => takeControlOfAutomaticUpdates;
        public int SolverCount => solvers.Count;

        public void Configure(
            RagdollAnimator animator,
            RagdollIKSolvePhase phase,
            MonoBehaviour[] components,
            bool takeControl = true)
        {
            if (subscribed)
            {
                throw new InvalidOperationException(
                    "Disable the IK scheduler before changing its configuration.");
            }

            ragdollAnimator = animator;
            solvePhase = phase;
            solverComponents = components ?? new MonoBehaviour[0];
            takeControlOfAutomaticUpdates = takeControl;
        }

        void Reset()
        {
            ragdollAnimator = GetComponent<RagdollAnimator>();
        }

        void OnEnable()
        {
            if (!ragdollAnimator) ragdollAnimator = GetComponent<RagdollAnimator>();
            if (!ragdollAnimator)
            {
                UnityEngine.Debug.LogError(
                    "RagdollIKScheduler requires a RagdollAnimator.",
                    this);
                enabled = false;
                return;
            }

            CaptureSolvers();
            if (solvePhase == RagdollIKSolvePhase.BeforePhysics)
            {
                ragdollAnimator.OnRead += Solve;
            }
            else
            {
                ragdollAnimator.OnWrite += Solve;
            }
            subscribed = true;
        }

        void OnDisable()
        {
            Unsubscribe();
            RestoreSolverOwnership();
        }

        void OnDestroy()
        {
            Unsubscribe();
            RestoreSolverOwnership();
        }

        public void Solve()
        {
            for (int index = 0; index < solvers.Count; index++)
            {
                IRagdollIKSolver solver = solvers[index].Solver;
                if (solver == null || !solver.IsSolverEnabled) continue;

                try
                {
                    solver.Solve();
                }
                catch (Exception exception)
                {
                    UnityEngine.Object context = solver as UnityEngine.Object;
                    UnityEngine.Debug.LogException(exception, context ? context : this);
                }
            }
        }

        void CaptureSolvers()
        {
            solvers.Clear();
            HashSet<IRagdollIKSolver> unique = new HashSet<IRagdollIKSolver>();
            for (int index = 0; index < solverComponents.Length; index++)
            {
                MonoBehaviour component = solverComponents[index];
                if (!component) continue;

                IRagdollIKSolver solver = component as IRagdollIKSolver;
                if (solver == null)
                {
                    UnityEngine.Debug.LogError(
                        component.GetType().FullName
                        + " does not implement IRagdollIKSolver.",
                        component);
                    continue;
                }
                if (!unique.Add(solver))
                {
                    UnityEngine.Debug.LogWarning(
                        "Duplicate IK solver ignored: " + component.name,
                        component);
                    continue;
                }

                SolverOwnership ownership = new SolverOwnership
                {
                    Solver = solver,
                    PreviousAutomaticUpdates = solver.AutomaticUpdates
                };
                solvers.Add(ownership);
                if (takeControlOfAutomaticUpdates)
                {
                    solver.AutomaticUpdates = false;
                }
            }
        }

        void RestoreSolverOwnership()
        {
            if (takeControlOfAutomaticUpdates)
            {
                for (int index = 0; index < solvers.Count; index++)
                {
                    SolverOwnership ownership = solvers[index];
                    if (ownership.Solver != null)
                    {
                        ownership.Solver.AutomaticUpdates =
                            ownership.PreviousAutomaticUpdates;
                    }
                }
            }
            solvers.Clear();
        }

        void Unsubscribe()
        {
            if (!subscribed || !ragdollAnimator) return;
            ragdollAnimator.OnRead -= Solve;
            ragdollAnimator.OnWrite -= Solve;
            subscribed = false;
        }
    }
}
