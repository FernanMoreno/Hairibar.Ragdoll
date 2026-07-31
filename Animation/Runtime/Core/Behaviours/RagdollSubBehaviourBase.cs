using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Base for reusable, non-component behaviour modules. An active behaviour may
    /// initialize and run any number of sub-behaviours at the same time.
    /// </summary>
    [Serializable]
    public abstract class RagdollSubBehaviourBase
    {
        [NonSerialized] RagdollBehaviourBase owner;
        [NonSerialized] bool isInitialized;
        [NonSerialized] bool isActive;

        public RagdollBehaviourBase Owner => owner;
        public bool IsInitialized => isInitialized;
        public bool IsActive => isActive;

        protected RagdollBehaviourContext Context
        {
            get
            {
                if (!owner || owner.Controller == null)
                {
                    throw new InvalidOperationException(
                        "The sub-behaviour has no initialized owner.");
                }

                return owner.Controller.Context;
            }
        }

        public void Initialize(RagdollBehaviourBase behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            if (!behaviour.IsInitialized)
            {
                throw new InvalidOperationException(
                    "The owning behaviour must be initialized first.");
            }
            if (isInitialized)
            {
                if (ReferenceEquals(owner, behaviour)) return;
                throw new InvalidOperationException(
                    "A sub-behaviour cannot be shared by multiple owners.");
            }

            owner = behaviour;
            isInitialized = true;
            try
            {
                OnInitialize();
            }
            catch
            {
                owner = null;
                isInitialized = false;
                throw;
            }
        }

        public void SetActive(bool active)
        {
            EnsureInitialized();
            if (isActive == active) return;
            isActive = active;
            if (active) OnActivate();
            else OnDeactivate();
        }

        public void FixedUpdate(float deltaTime)
        {
            EnsureInitialized();
            if (isActive) OnFixedUpdate(SanitizeDeltaTime(deltaTime));
        }

        public void Shutdown()
        {
            if (!isInitialized) return;
            if (isActive) SetActive(false);
            try
            {
                OnShutdown();
            }
            finally
            {
                owner = null;
                isInitialized = false;
            }
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnActivate() { }
        protected virtual void OnDeactivate() { }
        protected virtual void OnFixedUpdate(float deltaTime) { }
        protected virtual void OnShutdown() { }

        void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "The sub-behaviour has not been initialized.");
            }
        }

        static float SanitizeDeltaTime(float deltaTime)
        {
            return float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
                ? 0f
                : Math.Max(0f, deltaTime);
        }
    }
}
