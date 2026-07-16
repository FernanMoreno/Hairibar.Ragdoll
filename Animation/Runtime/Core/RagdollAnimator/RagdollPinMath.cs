using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>Pure advanced pinning math shared by runtime and tests.</summary>
    internal static class RagdollPinMath
    {
        internal static float ResolveCurvedPinWeight(
            float pinWeight,
            float pinPow)
        {
            float weight = SanitizeUnit(pinWeight, 1f);
            float exponent = SanitizePinPow(pinPow);
            if (weight <= 0f || weight >= 1f) return weight;
            return Mathf.Pow(weight, exponent);
        }

        internal static float ResolveDistanceMultiplier(
            float squaredDistance,
            float pinDistanceFalloff)
        {
            float distance = SanitizeNonNegative(squaredDistance, 0f);
            float falloff = SanitizeFalloff(pinDistanceFalloff);
            if (falloff <= 0f || distance <= 0f) return 1f;

            float denominator = 1f + distance * falloff;
            if (float.IsNaN(denominator) || float.IsInfinity(denominator))
            {
                return 0f;
            }
            return 1f / denominator;
        }

        internal static Vector3 ResolvePositionAcceleration(
            Vector3 baseAcceleration,
            Vector3 positionOffset,
            float pinWeight,
            float pinPow,
            float pinDistanceFalloff)
        {
            if (!IsFinite(baseAcceleration) || !IsFinite(positionOffset))
            {
                return Vector3.zero;
            }

            float multiplier = ResolveCurvedPinWeight(pinWeight, pinPow)
                * ResolveDistanceMultiplier(
                    positionOffset.sqrMagnitude,
                    pinDistanceFalloff);
            return baseAcceleration * multiplier;
        }

        internal static Vector3 ResolveAngularVelocityChange(
            Quaternion currentRotation,
            Quaternion targetRotation,
            Vector3 currentAngularVelocity,
            float pinWeight,
            float pinPow,
            float deltaTime)
        {
            if (!IsFinite(currentAngularVelocity)
                || !IsFinite(currentRotation)
                || !IsFinite(targetRotation))
            {
                return Vector3.zero;
            }

            float dt = SanitizeNonNegative(deltaTime, 0f);
            float weight = ResolveCurvedPinWeight(pinWeight, pinPow);
            if (dt <= Mathf.Epsilon || weight <= 0f) return Vector3.zero;

            Quaternion current = Normalize(currentRotation);
            Quaternion target = Normalize(targetRotation);
            if (Quaternion.Dot(current, target) < 0f)
            {
                target = new Quaternion(-target.x, -target.y, -target.z, -target.w);
            }

            Quaternion delta = target * Quaternion.Inverse(current);
            delta = Normalize(delta);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);

            if (!IsFinite(axis) || !IsFinite(angleDegrees))
            {
                return Vector3.zero;
            }
            if (angleDegrees > 180f) angleDegrees -= 360f;
            if (Mathf.Abs(angleDegrees) <= Mathf.Epsilon
                || axis.sqrMagnitude <= Mathf.Epsilon)
            {
                return -currentAngularVelocity * weight;
            }

            Vector3 desiredAngularVelocity =
                axis.normalized * (angleDegrees * Mathf.Deg2Rad / dt);
            Vector3 result =
                (desiredAngularVelocity - currentAngularVelocity) * weight;
            return IsFinite(result) ? result : Vector3.zero;
        }

        static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w);
            if (!IsFinite(magnitude) || magnitude <= Mathf.Epsilon)
            {
                return Quaternion.identity;
            }
            float inverse = 1f / magnitude;
            return new Quaternion(
                value.x * inverse,
                value.y * inverse,
                value.z * inverse,
                value.w * inverse);
        }

        static float SanitizeUnit(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Clamp01(value);
        }

        static float SanitizePinPow(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 4f;
            return Mathf.Clamp(value, 1f, 8f);
        }

        static float SanitizeFalloff(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = 5f;
            return Mathf.Clamp(value, 0f, 100f);
        }

        static float SanitizeNonNegative(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Mathf.Max(0f, value);
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
