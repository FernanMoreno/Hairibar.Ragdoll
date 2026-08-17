using System;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        sealed class CachedActionSubscribers
        {
            Action combined;
            Delegate[] snapshot = Array.Empty<Delegate>();

            internal Delegate[] Snapshot => snapshot;

            internal void Add(Action value)
            {
                if (value == null) return;
                combined = (Action)Delegate.Combine(combined, value);
                snapshot = combined.GetInvocationList();
            }

            internal void Remove(Action value)
            {
                if (value == null) return;
                combined = (Action)Delegate.Remove(combined, value);
                snapshot = combined == null
                    ? Array.Empty<Delegate>()
                    : combined.GetInvocationList();
            }
        }

        readonly CachedActionSubscribers onRead = new CachedActionSubscribers();
        readonly CachedActionSubscribers onWrite = new CachedActionSubscribers();
        readonly CachedActionSubscribers onPostLateUpdate =
            new CachedActionSubscribers();
        readonly CachedActionSubscribers onFixTransforms =
            new CachedActionSubscribers();
        readonly CachedActionSubscribers onPostInitialized =
            new CachedActionSubscribers();

        /// <summary>Called immediately before an animated Target pose is sampled.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public event Action OnRead
        {
            add => onRead.Add(value);
            remove => onRead.Remove(value);
        }

        /// <summary>Called immediately after the Puppet pose has been mapped to the Target.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public event Action OnWrite
        {
            add => onWrite.Add(value);
            remove => onWrite.Remove(value);
        }

        /// <summary>Called after every initialized RagdollAnimator LateUpdate.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public event Action OnPostLateUpdate
        {
            add => onPostLateUpdate.Add(value);
            remove => onPostLateUpdate.Remove(value);
        }

        /// <summary>Called when it is time to restore unanimated Target transforms.</summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public event Action OnFixTransforms
        {
            add => onFixTransforms.Add(value);
            remove => onFixTransforms.Remove(value);
        }

        /// <summary>
        /// Called once after mappings, modifiers, lifecycle, behaviours, collision and
        /// joint runtime services have initialized and the Puppet has snapped to Target.
        /// </summary>
        [RagdollCompatibilityApi("Lifecycle and animation", "https://root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html")]
        public event Action OnPostInitialized
        {
            add => onPostInitialized.Add(value);
            remove => onPostInitialized.Remove(value);
        }

        void InvokeReadHooks()
        {
            InvokeActionSafely(onRead.Snapshot);
            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyRead();
            }
        }

        void InvokeWriteHooks()
        {
            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyWrite();
            }
            InvokeActionSafely(onWrite.Snapshot);
        }

        void InvokePostLateUpdateHook()
        {
            InvokeActionSafely(onPostLateUpdate.Snapshot);
        }

        void InvokePostInitializedHook()
        {
            InvokeActionSafely(onPostInitialized.Snapshot);
        }

        void FixTargetTransformsAtUpdateBoundary()
        {
            if (!LifecycleAllowsAnimationSampling()) return;

            InvokeActionSafely(onFixTransforms.Snapshot);
            if (lifecycleBehaviours && lifecycleBehaviours.IsInitialized)
            {
                lifecycleBehaviours.NotifyFixTransforms();
            }

            if (!fixTargetTransforms || !SimulationAllowsTargetFix()) return;

            for (int index = 0; index < animatedPairs.Length; index++)
            {
                animatedPairs[index].FixTargetTransform();
            }
        }

        bool SimulationAllowsTargetFix()
        {
            if (!lifecycleSimulationMode
                || !lifecycleSimulationMode.IsInitialized)
            {
                return true;
            }

            return lifecycleSimulationMode.CurrentMode
                    == RagdollSimulationMode.Active
                || lifecycleSimulationMode.IsTransitioning;
        }

        void InvokeActionSafely(Delegate[] subscribers)
        {
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action)subscribers[index])();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception, this);
                }
            }
        }
    }
}
