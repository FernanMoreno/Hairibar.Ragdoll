using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Animation parameters to be used by RagdollAnimator.
    /// </summary>
    [System.Serializable]
    public struct BoneProfile
    {
        public float positionAlpha;
        public float positionDampingRatio;
        public float maxLinearAcceleration;

        public float rotationAlpha;
        public float rotationDampingRatio;
        public float maxAngularAcceleration;

        // Runtime-only authority channel. Authored positionAlpha remains the spring
        // stiffness; pin curves and falloff are applied after the base acceleration is
        // limited so temporary unpinning remains effective under saturated springs.
        [System.NonSerialized] float positionPinWeight;
        [System.NonSerialized] bool positionPinWeightInitialized;

        internal float PositionPinWeight =>
            positionPinWeightInitialized ? Mathf.Clamp01(positionPinWeight) : 1f;

        internal void SetPositionPinWeight(float value)
        {
            positionPinWeight = Mathf.Clamp01(value);
            positionPinWeightInitialized = true;
        }

        internal void MultiplyPositionPinWeight(float multiplier)
        {
            SetPositionPinWeight(PositionPinWeight * Mathf.Clamp01(multiplier));
        }

        public static BoneProfile Blend(BoneProfile a, BoneProfile b, float t)
        {
            BoneProfile result = new BoneProfile
            {
                positionAlpha = BlendAlpha(a.positionAlpha, b.positionAlpha, t),
                positionDampingRatio = Mathf.Lerp(a.positionDampingRatio, b.positionDampingRatio, t),
                maxLinearAcceleration = BlendMaxAcceleration(a.maxLinearAcceleration, b.maxLinearAcceleration, t),

                rotationAlpha = BlendAlpha(a.rotationAlpha, b.rotationAlpha, t),
                rotationDampingRatio = Mathf.Lerp(a.rotationDampingRatio, b.rotationDampingRatio, t),
                maxAngularAcceleration = BlendMaxAcceleration(a.maxAngularAcceleration, b.maxAngularAcceleration, t)
            };
            result.SetPositionPinWeight(
                Mathf.Lerp(a.PositionPinWeight, b.PositionPinWeight, t));
            return result;
        }


        /// <summary>
        /// Alpha works best on a squared scale instead of linearly. 
        /// Use this method instead of a lerp for alpha values.
        /// </summary>
        static float BlendAlpha(float a, float b, float t)
        {
            float linearScaleA = Mathf.Sqrt(a);
            float linearScaleB = Mathf.Sqrt(b);

            float blendedLinearScaleValue = Mathf.Lerp(linearScaleA, linearScaleB, t);

            return blendedLinearScaleValue * blendedLinearScaleValue;
        }

        static float BlendMaxAcceleration(float a, float b, float t)
        {
            if (float.IsInfinity(a) || float.IsInfinity(b))
            {
                return Mathf.Infinity;
            }
            else
            {
                return Mathf.Lerp(a, b, t);
            }
        }
    }
}
