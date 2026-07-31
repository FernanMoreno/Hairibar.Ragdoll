using Hairibar.EngineExtensions.Serialization;
using UnityEngine;

#pragma warning disable 649
namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Defines how the total weight of the ragdoll is distributed between its bones.
    /// </summary>
    [CreateAssetMenu(menuName = "Ragdoll/Weight Distribution", fileName = "RAGWGT_New", order = 3)]
    public class RagdollWeightDistribution : RagdollProfile
    {
        [SerializeField] WeightDistributionDictionary factors;


        internal event System.Action OnUpdateValues;

        internal float GetBoneMass(BoneName bone, float totalMass)
        {
            ThrowExceptionIfNotValid();

            if (!IsFinite(totalMass) || totalMass <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(totalMass),
                    totalMass,
                    "Total ragdoll mass must be finite and positive.");
            }

            if (factors.TryGetValue(bone, out float factor))
            {
                ValidateFactor(factor, bone);
                float actualFactor = factor / GetValidatedTotalFactorSum();
                return totalMass * actualFactor;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"Bone \"{bone}\" is not present in {definition.name}.", this);
                return 1;
            }
        }


        float GetValidatedTotalFactorSum()
        {
            float total = 0;

            foreach (System.Collections.Generic.KeyValuePair<BoneName, float> pair
                in factors)
            {
                ValidateFactor(pair.Value, pair.Key);
                total += pair.Value;
            }

            if (!IsFinite(total) || total <= 0f)
            {
                throw new System.InvalidOperationException(
                    "Weight distribution factors must have a finite positive sum.");
            }
            return total;
        }

        static void ValidateFactor(float factor, BoneName bone)
        {
            if (IsFinite(factor) && factor > 0f) return;

            throw new System.InvalidOperationException(
                "Weight factor for bone '" + bone
                + "' must be finite and positive.");
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        void OnValidate()
        {
            if (IsValid) OnUpdateValues?.Invoke();
        }


        [System.Serializable]
        class WeightDistributionDictionary : SerializableDictionary<BoneName, float> { }
    }
}
