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

        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        public static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
