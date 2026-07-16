using System;

namespace Hairibar.Ragdoll.Animation
{
    public partial class RagdollAnimator
    {
        /// <summary>Called immediately before an animated Target pose is sampled.</summary>
        public event Action OnRead;

        /// <summary>Called immediately after the Puppet pose has been mapped to the Target.</summary>
        public event Action OnWrite;

        /// <summary>Called after every initialized RagdollAnimator LateUpdate.</summary>
        public event Action OnPostLateUpdate;

        /// <summary>Called when it is time to restore unanimated Target transforms.</summary>
        public event Action OnFixTransforms;

        void InvokeReadHooks()
        {
            OnRead?.Invoke();
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
            OnWrite?.Invoke();
        }

        void InvokePostLateUpdateHook()
        {
            OnPostLateUpdate?.Invoke();
        }

        void FixTargetTransformsAtUpdateBoundary()
        {
            if (!LifecycleAllowsAnimationSampling()) return;

            OnFixTransforms?.Invoke();
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
    }
}
