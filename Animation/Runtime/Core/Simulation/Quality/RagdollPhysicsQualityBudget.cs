using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Optional scene-level budget that grants Active simulation to the highest priority
    /// nearby ragdolls. Other registered controllers use their non-active fallback tier.
    /// </summary>
    [DefaultExecutionOrder(950)]
    [AddComponentMenu("Ragdoll/Ragdoll Physics Quality Budget")]
    [DisallowMultipleComponent]
    public sealed class RagdollPhysicsQualityBudget : MonoBehaviour
    {
        [SerializeField, Min(0)] int maximumActiveRagdolls = 8;
        [SerializeField, Min(0f)] float evaluationInterval = 0.25f;
        [SerializeField, Min(0f)] float retainedRagdollDistanceBonus = 2f;

        readonly List<RagdollPhysicsQualityController> registered =
            new List<RagdollPhysicsQualityController>();
        readonly List<RagdollPhysicsQualityController> candidates =
            new List<RagdollPhysicsQualityController>();

        Comparison<RagdollPhysicsQualityController> comparison;
        float nextEvaluationTime;
        bool dirty = true;
        int activeGrantCount;

        public int MaximumActiveRagdolls
        {
            get => maximumActiveRagdolls;
            set
            {
                maximumActiveRagdolls = Mathf.Max(0, value);
                MarkDirty();
            }
        }

        public int RegisteredCount => registered.Count;
        public int ActiveGrantCount => activeGrantCount;

        internal void Register(RagdollPhysicsQualityController controller)
        {
            if (!controller || registered.Contains(controller)) return;
            registered.Add(controller);
            MarkDirty();
        }

        internal void Unregister(RagdollPhysicsQualityController controller)
        {
            if (!controller) return;
            if (registered.Remove(controller)) MarkDirty();
        }

        internal void MarkDirty()
        {
            dirty = true;
        }

        public void EvaluateNow()
        {
            EnsureComparison();
            RemoveInvalidRegistrations();
            candidates.Clear();

            for (int index = 0; index < registered.Count; index++)
            {
                RagdollPhysicsQualityController controller = registered[index];
                if (controller.RequestsDynamicBudget)
                {
                    candidates.Add(controller);
                }
                else
                {
                    controller.SetBudgetApproved(true);
                }
            }

            candidates.Sort(comparison);
            int granted = Mathf.Min(
                Mathf.Max(0, maximumActiveRagdolls),
                candidates.Count);

            for (int index = 0; index < candidates.Count; index++)
            {
                candidates[index].SetBudgetApproved(index < granted);
            }

            activeGrantCount = granted;
            dirty = false;
            nextEvaluationTime = Time.unscaledTime
                + Mathf.Max(0f, evaluationInterval);
        }

        void Update()
        {
            if (!dirty && Time.unscaledTime < nextEvaluationTime) return;
            EvaluateNow();
        }

        void OnEnable()
        {
            EnsureComparison();
            MarkDirty();
        }

        void OnDisable()
        {
            for (int index = 0; index < registered.Count; index++)
            {
                if (registered[index])
                {
                    registered[index].SetBudgetApproved(true);
                }
            }

            activeGrantCount = 0;
        }

        void OnValidate()
        {
            maximumActiveRagdolls = Mathf.Max(0, maximumActiveRagdolls);
            evaluationInterval = Mathf.Max(0f, evaluationInterval);
            retainedRagdollDistanceBonus =
                Mathf.Max(0f, retainedRagdollDistanceBonus);
            MarkDirty();
        }

        void EnsureComparison()
        {
            if (comparison == null) comparison = CompareControllers;
        }

        int CompareControllers(
            RagdollPhysicsQualityController first,
            RagdollPhysicsQualityController second)
        {
            return RagdollPhysicsBudgetPolicy.Compare(
                first.BudgetPriority,
                first.DistanceSquared,
                first.RetainsDynamicBudget,
                first.GetInstanceID(),
                second.BudgetPriority,
                second.DistanceSquared,
                second.RetainsDynamicBudget,
                second.GetInstanceID(),
                retainedRagdollDistanceBonus);
        }

        void RemoveInvalidRegistrations()
        {
            for (int index = registered.Count - 1; index >= 0; index--)
            {
                if (!registered[index]) registered.RemoveAt(index);
            }
        }
    }
}
