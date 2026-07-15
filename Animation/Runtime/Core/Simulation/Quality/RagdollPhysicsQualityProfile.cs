using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Reusable distance bands for runtime PhysX quality and global simulation modes.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Ragdoll Physics Quality Profile",
        menuName = "Ragdoll/Physics Quality Profile",
        order = 105)]
    public sealed class RagdollPhysicsQualityProfile : ScriptableObject
    {
        [SerializeField] RagdollPhysicsQualityLevel[] levels =
            CreateRecommendedLevels();

        public int LevelCount => levels != null ? levels.Length : 0;

        public RagdollPhysicsQualityLevel GetLevel(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }

            return levels[index];
        }

        public bool TryValidate(out string error)
        {
            RagdollPhysicsQualityRuntime ignored;
            return RagdollPhysicsQualityRuntime.TryCreate(
                levels,
                out ignored,
                out error);
        }

        public void ResetToRecommendedLevels()
        {
            levels = CreateRecommendedLevels();
        }

        internal bool TryCreateRuntime(
            out RagdollPhysicsQualityRuntime runtime,
            out string error)
        {
            return RagdollPhysicsQualityRuntime.TryCreate(
                levels,
                out runtime,
                out error);
        }

        static RagdollPhysicsQualityLevel[] CreateRecommendedLevels()
        {
            return new[]
            {
                new RagdollPhysicsQualityLevel(
                    "Near Active",
                    0f,
                    RagdollSimulationMode.Active,
                    0.2f,
                    true,
                    default(RagdollSolverQualitySettings)),
                new RagdollPhysicsQualityLevel(
                    "Medium Active",
                    12f,
                    RagdollSimulationMode.Active,
                    0.2f,
                    false,
                    RagdollSolverQualitySettings.Create(
                        12,
                        4,
                        15f,
                        5f,
                        RigidbodyInterpolation.None,
                        CollisionDetectionMode.Discrete)),
                new RagdollPhysicsQualityLevel(
                    "Kinematic",
                    25f,
                    RagdollSimulationMode.Kinematic,
                    0.25f,
                    false,
                    RagdollSolverQualitySettings.Create(
                        6,
                        1,
                        7f,
                        5f,
                        RigidbodyInterpolation.None,
                        CollisionDetectionMode.Discrete)),
                new RagdollPhysicsQualityLevel(
                    "Disabled",
                    50f,
                    RagdollSimulationMode.Disabled,
                    0f,
                    true,
                    default(RagdollSolverQualitySettings))
            };
        }
    }
}
