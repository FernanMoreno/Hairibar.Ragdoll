using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.RagdollLab
{
    /// Pure, deterministic helpers kept independent from scene components.
    public static class RagdollLabMath
    {
        public static float QuaternionAngle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);

        public static Vector3 ConnectedAnchorWorld(ConfigurableJoint joint, Vector3 ownAnchorWorld)
        {
            if (joint == null) return ownAnchorWorld;
            if (joint.connectedBody != null)
                return joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);

            // A joint with no connected body is attached to the world only for the
            // constrained linear axes. Hairibar's root joint deliberately leaves all
            // three linear axes free; its serialized connectedAnchor is therefore not
            // a positional constraint and must not be reported as drift.
            bool constrainsPosition = joint.xMotion != ConfigurableJointMotion.Free
                || joint.yMotion != ConfigurableJointMotion.Free
                || joint.zMotion != ConfigurableJointMotion.Free;
            return constrainsPosition ? joint.connectedAnchor : ownAnchorWorld;
        }

        public static float JointAnchorError(ConfigurableJoint joint)
        {
            if (joint == null) return 0f;
            Vector3 own = joint.transform.TransformPoint(joint.anchor);
            return Vector3.Distance(own, ConnectedAnchorWorld(joint, own));
        }

        public static Vector3 CenterOfMass(IReadOnlyList<Rigidbody> bodies)
        {
            Vector3 weighted = Vector3.zero;
            float mass = 0f;
            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null || !IsFinite(body.mass) || body.mass <= 0f) continue;
                weighted += body.worldCenterOfMass * body.mass;
                mass += body.mass;
            }
            return mass > Mathf.Epsilon ? weighted / mass : Vector3.zero;
        }

        public static float Rms(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            double sum = 0d;
            for (int i = 0; i < values.Count; i++) sum += values[i] * (double)values[i];
            return Mathf.Sqrt((float)(sum / values.Count));
        }

        public static float Percentile(IReadOnlyList<float> values, float percentile)
        {
            if (values == null || values.Count == 0) return 0f;
            var sorted = new List<float>(values);
            sorted.Sort();
            float p = Mathf.Clamp01(percentile) * (sorted.Count - 1);
            int lower = Mathf.FloorToInt(p), upper = Mathf.CeilToInt(p);
            return Mathf.Lerp(sorted[lower], sorted[upper], p - lower);
        }

        public static int ZeroCrossings(IReadOnlyList<float> values, float deadBand = 0f)
        {
            int count = 0; float previous = 0f; bool hasPrevious = false;
            for (int i = 0; i < values.Count; i++)
            {
                float value = values[i];
                if (Mathf.Abs(value) <= deadBand) continue;
                if (hasPrevious && Mathf.Sign(value) != Mathf.Sign(previous)) count++;
                previous = value; hasPrevious = true;
            }
            return count;
        }

        public static float DominantFrequencyByZeroCrossings(IReadOnlyList<float> values, float sampleRate, float deadBand = 0f)
        {
            if (values == null || values.Count < 2 || sampleRate <= 0f) return 0f;
            int crossings = ZeroCrossings(values, deadBand);
            // A sampled sinusoid commonly starts/ends exactly on zero. The sign-change
            // loop intentionally ignores those samples; restore one boundary crossing
            // so a 5 Hz / 100 sample synthetic signal does not become 4.5 Hz.
            if (Mathf.Abs(values[0]) <= deadBand || Mathf.Abs(values[values.Count - 1]) <= deadBand) crossings++;
            return crossings * sampleRate / (2f * values.Count);
        }

        public static float KineticEnergy(Rigidbody body)
        {
            if (body == null || !IsFinite(body.mass)) return 0f;
            float linear = 0.5f * body.mass * body.linearVelocity.sqrMagnitude;
            Vector3 omegaLocal = Quaternion.Inverse(body.rotation * body.inertiaTensorRotation) * body.angularVelocity;
            Vector3 angularPrincipal = new(omegaLocal.x * body.inertiaTensor.x, omegaLocal.y * body.inertiaTensor.y, omegaLocal.z * body.inertiaTensor.z);
            Vector3 angularWorld = body.rotation * body.inertiaTensorRotation * angularPrincipal;
            float angular = 0.5f * Vector3.Dot(body.angularVelocity, angularWorld);
            return Mathf.Max(0f, linear + angular);
        }

        public static float OvershootPercent(IReadOnlyList<float> values, float target = 0f)
        {
            if (values == null || values.Count == 0) return 0f;
            float peak = 0f;
            for (int i = 0; i < values.Count; i++) peak = Mathf.Max(peak, Mathf.Abs(values[i] - target));
            float reference = Mathf.Max(Mathf.Abs(target), 0.0001f);
            return peak / reference * 100f;
        }

        public static float SettlingTime(IReadOnlyList<float> values, float dt, float target, float tolerance)
        {
            if (values == null || values.Count == 0) return 0f;
            for (int i = values.Count - 1; i >= 0; i--)
                if (Mathf.Abs(values[i] - target) > tolerance)
                    return Mathf.Min(values.Count - 1, i + 1) * dt;
            return 0f;
        }

        public static float DominantFrequencyDft(IReadOnlyList<float> values, float sampleRate, int minBin = 1)
        {
            if (values == null || values.Count < 4 || sampleRate <= 0f) return 0f;
            int n = values.Count, bestBin = 0; double bestPower = double.MinValue;
            for (int bin = Mathf.Max(1, minBin); bin <= n / 2; bin++)
            {
                double real = 0d, imaginary = 0d;
                for (int i = 0; i < n; i++)
                {
                    double angle = 2d * Math.PI * bin * i / n;
                    real += values[i] * Math.Cos(angle); imaginary -= values[i] * Math.Sin(angle);
                }
                double power = real * real + imaginary * imaginary;
                if (power > bestPower) { bestPower = power; bestBin = bin; }
            }
            return bestBin * sampleRate / n;
        }

        public static bool IsLikelyFallen(Vector3 com, Quaternion rootRotation, int supportContactCount, float minHeight = 0.35f)
        {
            float upright = Vector3.Dot(rootRotation * Vector3.up, Vector3.up);
            return com.y < minHeight || (upright < 0.35f && supportContactCount == 0);
        }

        public static bool IsLikelyFallen(
            Vector3 com,
            Quaternion rootRotation,
            int supportContactCount,
            Vector3 supportOrigin,
            Vector3 supportUp,
            float minHeight = 0.35f,
            bool supportReferenceAvailable = true)
        {
            Vector3 up = supportUp.sqrMagnitude > 0.000001f && IsFinite(supportUp)
                ? supportUp.normalized
                : Vector3.up;
            float upright = Vector3.Dot(rootRotation * Vector3.up, up);
            bool belowSupport = supportReferenceAvailable && IsFinite(com) && IsFinite(supportOrigin)
                && Vector3.Dot(com - supportOrigin, up) < minHeight;
            return belowSupport || (upright < 0.35f && supportContactCount == 0);
        }

        /// Classifies a contact normal against the effective support up direction.
        /// The recorder supplies the physical ContactPoint.normal after orienting it
        /// toward the tracked body, so this helper remains independent of collider
        /// ordering and can be tested without a Unity collision callback.
        public static bool IsGroundSupportNormal(
            Vector3 normal,
            Vector3 supportUp,
            float maximumGroundAngle,
            out float normalDot)
        {
            normalDot = -1f;
            if (!IsFinite(normal) || !IsFinite(supportUp)
                || normal.sqrMagnitude <= Mathf.Epsilon || supportUp.sqrMagnitude <= Mathf.Epsilon)
                return false;

            Vector3 normalizedNormal = normal.normalized;
            Vector3 normalizedUp = supportUp.normalized;
            normalDot = Vector3.Dot(normalizedNormal, normalizedUp);
            float angle = Mathf.Clamp(IsFinite(maximumGroundAngle) ? maximumGroundAngle : 90f, 0f, 90f);
            return normalDot >= Mathf.Cos(angle * Mathf.Deg2Rad);
        }

        /// Mean of the samples strictly before eventIndex, over up to lookbackFrames.
        /// With no antecedent (eventIndex == 0) the event's own sample is returned.
        public static float Baseline(IReadOnlyList<float> values, int eventIndex, int lookbackFrames)
        {
            if (values == null || values.Count == 0) return 0f;
            int clampedEvent = Mathf.Clamp(eventIndex, 0, values.Count - 1);
            int start = Mathf.Max(0, clampedEvent - Mathf.Max(1, lookbackFrames));
            int end = Mathf.Max(start + 1, clampedEvent);
            double sum = 0d; int count = 0;
            for (int i = start; i < end; i++) { sum += values[i]; count++; }
            return count > 0 ? (float)(sum / count) : values[clampedEvent];
        }

        /// The maximum value (and its index) within [startIndex, endIndexExclusive).
        public static (int index, float value) PeakAfter(IReadOnlyList<float> values, int startIndex, int endIndexExclusive)
        {
            if (values == null || values.Count == 0) return (0, 0f);
            int start = Mathf.Clamp(startIndex, 0, values.Count - 1);
            int end = Mathf.Clamp(endIndexExclusive, start + 1, values.Count);
            int bestIndex = start; float bestValue = values[start];
            for (int i = start; i < end; i++) if (values[i] > bestValue) { bestValue = values[i]; bestIndex = i; }
            return (bestIndex, bestValue);
        }

        /// The sample offsetSeconds after eventIndex, clamped to the available data.
        public static float SampleAtOffset(IReadOnlyList<float> values, float dt, int eventIndex, float offsetSeconds)
        {
            if (values == null || values.Count == 0 || dt <= 0f) return 0f;
            int index = eventIndex + Mathf.RoundToInt(offsetSeconds / dt);
            index = Mathf.Clamp(index, 0, values.Count - 1);
            return values[index];
        }

        /// Trapezoidal integral of values over [startIndex, endIndexExclusive).
        public static float AreaUnderCurve(IReadOnlyList<float> values, float dt, int startIndex, int endIndexExclusive)
        {
            if (values == null || values.Count == 0) return 0f;
            int start = Mathf.Clamp(startIndex, 0, values.Count);
            int end = Mathf.Clamp(endIndexExclusive, start, values.Count);
            double area = 0d;
            for (int i = start; i < end - 1; i++) area += (values[i] + values[i + 1]) * 0.5d * dt;
            return (float)area;
        }

        /// Total time, in seconds, that values exceed threshold within [startIndex, endIndexExclusive).
        public static float TimeAboveThreshold(IReadOnlyList<float> values, float dt, int startIndex, int endIndexExclusive, float threshold)
        {
            if (values == null || values.Count == 0) return 0f;
            int start = Mathf.Clamp(startIndex, 0, values.Count);
            int end = Mathf.Clamp(endIndexExclusive, start, values.Count);
            int count = 0;
            for (int i = start; i < end; i++) if (values[i] > threshold) count++;
            return count * dt;
        }

        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        public static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
