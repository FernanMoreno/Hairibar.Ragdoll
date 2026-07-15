using System;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Validated immutable copy of a physics quality profile.</summary>
    internal sealed class RagdollPhysicsQualityRuntime
    {
        readonly RagdollPhysicsQualityLevel[] levels;
        readonly float[] minimumDistances;

        internal int LevelCount => levels.Length;
        internal float[] MinimumDistances => minimumDistances;

        RagdollPhysicsQualityRuntime(RagdollPhysicsQualityLevel[] levels)
        {
            this.levels = levels;
            minimumDistances = new float[levels.Length];
            for (int index = 0; index < levels.Length; index++)
            {
                minimumDistances[index] = levels[index].minimumDistance;
            }
        }

        internal RagdollPhysicsQualityLevel GetLevel(int index)
        {
            if (index < 0 || index >= levels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return levels[index];
        }

        internal int FindFirstNonActiveLevel()
        {
            for (int index = 0; index < levels.Length; index++)
            {
                if (levels[index].simulationMode != RagdollSimulationMode.Active)
                {
                    return index;
                }
            }

            return -1;
        }

        internal static bool TryCreate(
            RagdollPhysicsQualityLevel[] source,
            out RagdollPhysicsQualityRuntime runtime,
            out string error)
        {
            runtime = null;
            if (source == null || source.Length == 0)
            {
                error = "At least one physics quality level is required.";
                return false;
            }

            RagdollPhysicsQualityLevel[] copy =
                new RagdollPhysicsQualityLevel[source.Length];
            float previousDistance = -1f;
            RagdollSimulationMode previousMode = RagdollSimulationMode.Active;

            for (int index = 0; index < source.Length; index++)
            {
                RagdollPhysicsQualityLevel level = source[index];
                if (string.IsNullOrEmpty(level.name))
                {
                    error = "Quality level " + index + " requires a name.";
                    return false;
                }

                if (float.IsNaN(level.minimumDistance)
                    || float.IsInfinity(level.minimumDistance)
                    || level.minimumDistance < 0f)
                {
                    error = "Quality level " + index
                        + " has an invalid minimum distance.";
                    return false;
                }

                if (index == 0 && level.minimumDistance > 0.0001f)
                {
                    error = "The first quality level must begin at distance zero.";
                    return false;
                }

                if (index > 0 && level.minimumDistance <= previousDistance)
                {
                    error = "Quality distances must be strictly increasing.";
                    return false;
                }

                if (!IsValidMode(level.simulationMode))
                {
                    error = "Quality level " + index
                        + " has an invalid simulation mode.";
                    return false;
                }

                if (index > 0
                    && GetModeCost(level.simulationMode)
                        < GetModeCost(previousMode))
                {
                    error = "Farther quality levels cannot return to a more expensive simulation mode.";
                    return false;
                }

                if (float.IsNaN(level.modeTransitionDuration)
                    || float.IsInfinity(level.modeTransitionDuration)
                    || level.modeTransitionDuration < 0f)
                {
                    error = "Quality level " + index
                        + " has an invalid transition duration.";
                    return false;
                }

                if (!level.useAuthoredSolverSettings)
                {
                    string solverError;
                    if (!level.solverSettings.TryValidate(out solverError))
                    {
                        error = "Quality level " + index + ": " + solverError;
                        return false;
                    }

                    level.solverSettings = level.solverSettings.Sanitized();
                }

                copy[index] = level;
                previousDistance = level.minimumDistance;
                previousMode = level.simulationMode;
            }

            runtime = new RagdollPhysicsQualityRuntime(copy);
            error = null;
            return true;
        }


        static int GetModeCost(RagdollSimulationMode mode)
        {
            switch (mode)
            {
                case RagdollSimulationMode.Active:
                    return 2;
                case RagdollSimulationMode.Kinematic:
                    return 1;
                case RagdollSimulationMode.Disabled:
                    return 0;
                default:
                    return -1;
            }
        }

        static bool IsValidMode(RagdollSimulationMode mode)
        {
            return mode == RagdollSimulationMode.Active
                || mode == RagdollSimulationMode.Kinematic
                || mode == RagdollSimulationMode.Disabled;
        }
    }
}
