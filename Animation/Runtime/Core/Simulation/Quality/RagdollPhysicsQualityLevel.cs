using System;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>One distance band in a RagdollPhysicsQualityProfile.</summary>
    [Serializable]
    public struct RagdollPhysicsQualityLevel
    {
        public string name;
        [Min(0f)] public float minimumDistance;
        public RagdollSimulationMode simulationMode;
        [Min(0f)] public float modeTransitionDuration;
        [Tooltip("When enabled, this level clears the runtime solver override and uses RagdollSettings authored values.")]
        public bool useAuthoredSolverSettings;
        public RagdollSolverQualitySettings solverSettings;

        public RagdollPhysicsQualityLevel(
            string name,
            float minimumDistance,
            RagdollSimulationMode simulationMode,
            float modeTransitionDuration,
            bool useAuthoredSolverSettings,
            RagdollSolverQualitySettings solverSettings)
        {
            this.name = name;
            this.minimumDistance = minimumDistance;
            this.simulationMode = simulationMode;
            this.modeTransitionDuration = modeTransitionDuration;
            this.useAuthoredSolverSettings = useAuthoredSolverSettings;
            this.solverSettings = solverSettings;
        }
    }
}
