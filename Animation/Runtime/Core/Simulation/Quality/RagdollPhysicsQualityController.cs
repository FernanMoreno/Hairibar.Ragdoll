using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Selects a reusable physics quality tier by observer distance and composes it with an
    /// optional shared Active-ragdoll budget. This component owns the simulation mode while
    /// enabled; gameplay should request quality through this API instead of changing the
    /// RagdollSimulationModeController independently.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [AddComponentMenu("Ragdoll/Ragdoll Physics Quality Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RagdollAnimator))]
    [RequireComponent(typeof(RagdollSimulationModeController))]
    public sealed class RagdollPhysicsQualityController : MonoBehaviour
    {
        [Header("Quality")]
        [SerializeField] RagdollPhysicsQualityProfile profile;
        [SerializeField] bool automaticDistance = true;
        [SerializeField] Transform observer;
        [SerializeField] bool useMainCameraWhenObserverMissing = true;
        [SerializeField, Min(0f)] float evaluationInterval = 0.25f;
        [SerializeField, Min(0f)] float distanceHysteresis = 2f;

        [Header("Shared Budget")]
        [SerializeField] RagdollPhysicsQualityBudget budget;
        [SerializeField] int budgetPriority;
        [SerializeField] int budgetFallbackLevel = -1;

        [Header("Lifetime")]
        [SerializeField] bool restoreInitialModeWhenDisabled = true;

        RagdollAnimator animator;
        RagdollSettings ragdollSettings;
        RagdollSimulationModeController modeController;
        RagdollPhysicsQualityRuntime runtimeProfile;
        RagdollSimulationMode initialMode;
        bool initialized;
        bool quitting;
        bool budgetApproved = true;
        bool budgetRegistered;
        int requestedLevel = -1;
        int appliedLevel = -1;
        int resolvedBudgetFallback = -1;
        float distanceSquared;
        float nextEvaluationTime;

        public bool IsInitialized => initialized;
        public bool AutomaticDistance => automaticDistance;
        public Transform Observer => observer;
        public int RequestedLevel => requestedLevel;
        public int AppliedLevel => appliedLevel;
        public float DistanceSquared => distanceSquared;
        public bool BudgetApproved => budgetApproved;
        public int BudgetPriority => budgetPriority;
        public RagdollPhysicsQualityProfile Profile => profile;

        internal bool RequestsDynamicBudget => initialized
            && requestedLevel >= 0
            && runtimeProfile.GetLevel(requestedLevel).simulationMode
                == RagdollSimulationMode.Active;

        internal bool RetainsDynamicBudget => RequestsDynamicBudget
            && budgetApproved
            && appliedLevel >= 0
            && runtimeProfile.GetLevel(appliedLevel).simulationMode
                == RagdollSimulationMode.Active;

        public event Action<int, int> QualityLevelChanged;

        public void SetManualLevel(int level)
        {
            EnsureInitialized();
            ValidateLevel(level);
            automaticDistance = false;
            SetRequestedLevel(level);
        }

        public void ResumeAutomaticDistance()
        {
            EnsureInitialized();
            automaticDistance = true;
            RefreshNow();
        }

        public void RefreshNow()
        {
            EnsureInitialized();
            if (automaticDistance) EvaluateDistance();
            ApplyEffectiveLevel(true);

            if (budget && budget.isActiveAndEnabled)
            {
                budget.EvaluateNow();
            }
        }

        internal void SetBudgetApproved(bool approved)
        {
            if (budgetApproved == approved) return;
            budgetApproved = approved;
            if (initialized) ApplyEffectiveLevel();
        }

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            if (initialized) return;
            if (!profile)
            {
                throw new InvalidOperationException(
                    "RagdollPhysicsQualityController requires a quality profile.");
            }

            string profileError;
            if (!profile.TryCreateRuntime(out runtimeProfile, out profileError))
            {
                throw new InvalidOperationException(
                    "The assigned physics quality profile is invalid: "
                    + profileError);
            }

            animator = GetComponent<RagdollAnimator>();
            modeController = GetComponent<RagdollSimulationModeController>();
            if (!modeController.IsInitialized)
            {
                throw new InvalidOperationException(
                    "RagdollSimulationModeController must be initialized before physics quality starts.");
            }

            if (!animator.Bindings)
            {
                throw new InvalidOperationException(
                    "RagdollPhysicsQualityController requires initialized ragdoll bindings.");
            }

            ragdollSettings = animator.Bindings.GetComponent<RagdollSettings>();
            if (!ragdollSettings)
            {
                throw new InvalidOperationException(
                    "RagdollPhysicsQualityController requires RagdollSettings on the Puppet root.");
            }

            ResolveObserver();
            resolvedBudgetFallback = ResolveBudgetFallback();
            initialMode = modeController.TargetMode;
            initialized = true;

            requestedLevel = RagdollPhysicsQualitySelector.Evaluate(
                CalculateDistanceSquared(),
                -1,
                runtimeProfile.MinimumDistances,
                0f);

            RegisterBudget();
            ApplyEffectiveLevel(true);
            if (budget && budget.isActiveAndEnabled) budget.EvaluateNow();
            ScheduleNextEvaluation();
        }

        void Update()
        {
            if (!initialized) return;
            if (budget && budget.isActiveAndEnabled && !budgetRegistered)
            {
                RegisterBudget();
                budget.EvaluateNow();
            }

            if (!automaticDistance || Time.unscaledTime < nextEvaluationTime)
            {
                return;
            }

            EvaluateDistance();
            ScheduleNextEvaluation();
        }

        void OnEnable()
        {
            if (!initialized) return;

            quitting = false;
            RegisterBudget();
            ApplyEffectiveLevel(true);
            if (budget && budget.isActiveAndEnabled) budget.EvaluateNow();
            ScheduleNextEvaluation();
        }

        void EvaluateDistance()
        {
            distanceSquared = CalculateDistanceSquared();
            int next = RagdollPhysicsQualitySelector.Evaluate(
                distanceSquared,
                requestedLevel,
                runtimeProfile.MinimumDistances,
                distanceHysteresis);
            SetRequestedLevel(next);
        }

        void SetRequestedLevel(int level)
        {
            ValidateLevel(level);
            if (requestedLevel == level) return;

            requestedLevel = level;
            if (budget && budget.isActiveAndEnabled)
            {
                budget.MarkDirty();
                ApplyEffectiveLevel();
            }
            else
            {
                budgetApproved = true;
                ApplyEffectiveLevel();
            }
        }

        void ApplyEffectiveLevel(bool force = false)
        {
            if (!initialized || requestedLevel < 0) return;

            int effectiveLevel = requestedLevel;
            if (!budgetApproved
                && runtimeProfile.GetLevel(requestedLevel).simulationMode
                    == RagdollSimulationMode.Active)
            {
                effectiveLevel = resolvedBudgetFallback;
            }

            if (effectiveLevel < 0) return;
            if (!force && appliedLevel == effectiveLevel) return;

            RagdollPhysicsQualityLevel level =
                runtimeProfile.GetLevel(effectiveLevel);

            if (level.useAuthoredSolverSettings)
            {
                ragdollSettings.ClearRuntimeSolverOverride();
            }
            else
            {
                ragdollSettings.SetRuntimeSolverOverride(level.solverSettings);
            }

            modeController.SetMode(
                level.simulationMode,
                Mathf.Max(0f, level.modeTransitionDuration));

            if (appliedLevel == effectiveLevel) return;
            int previous = appliedLevel;
            appliedLevel = effectiveLevel;
            QualityLevelChanged?.Invoke(previous, appliedLevel);
        }

        float CalculateDistanceSquared()
        {
            if (!observer) ResolveObserver();
            if (!observer)
            {
                distanceSquared = 0f;
                return 0f;
            }

            Vector3 offset = observer.position - transform.position;
            distanceSquared = offset.sqrMagnitude;
            return distanceSquared;
        }

        void ResolveObserver()
        {
            if (observer || !useMainCameraWhenObserverMissing) return;
            Camera mainCamera = Camera.main;
            if (mainCamera) observer = mainCamera.transform;
        }

        int ResolveBudgetFallback()
        {
            if (!budget) return -1;

            int fallback = budgetFallbackLevel >= 0
                ? budgetFallbackLevel
                : runtimeProfile.FindFirstNonActiveLevel();
            if (fallback < 0 || fallback >= runtimeProfile.LevelCount)
            {
                throw new InvalidOperationException(
                    "A shared physics budget requires a valid non-active fallback level.");
            }

            if (runtimeProfile.GetLevel(fallback).simulationMode
                == RagdollSimulationMode.Active)
            {
                throw new InvalidOperationException(
                    "The budget fallback level must use Kinematic or Disabled mode.");
            }

            return fallback;
        }

        void RegisterBudget()
        {
            budgetApproved = true;
            if (!budget || !budget.isActiveAndEnabled)
            {
                budgetRegistered = false;
                return;
            }

            budget.Register(this);
            budgetRegistered = true;
        }

        void UnregisterBudget()
        {
            if (budget && budgetRegistered) budget.Unregister(this);
            budgetRegistered = false;
            budgetApproved = true;
        }

        void ScheduleNextEvaluation()
        {
            nextEvaluationTime = Time.unscaledTime
                + Mathf.Max(0f, evaluationInterval);
        }

        void ValidateLevel(int level)
        {
            if (level < 0 || level >= runtimeProfile.LevelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        void EnsureInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "RagdollPhysicsQualityController has not initialized yet.");
            }
        }

        void OnDisable()
        {
            UnregisterBudget();
            if (!Application.isPlaying
                || quitting
                || !initialized
                || !gameObject.activeInHierarchy)
            {
                return;
            }

            ragdollSettings.ClearRuntimeSolverOverride();
            if (restoreInitialModeWhenDisabled)
            {
                modeController.SetModeImmediate(initialMode);
            }

            appliedLevel = -1;
        }

        void OnApplicationQuit()
        {
            quitting = true;
        }

        void OnDestroy()
        {
            UnregisterBudget();
        }

        void OnValidate()
        {
            evaluationInterval = Mathf.Max(0f, evaluationInterval);
            distanceHysteresis = Mathf.Max(0f, distanceHysteresis);
        }
    }
}
