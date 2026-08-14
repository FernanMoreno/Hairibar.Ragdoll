using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollBipedBalancerMathTests
    {
        [Test]
        public void DefaultSettings_MatchDocumentedSubBehaviourBalancerDefaults()
        {
            RagdollBipedBalancerSettings settings = RagdollBipedBalancerSettings.Default;

            Assert.That(settings.DamperForSpring, Is.EqualTo(1f));
            Assert.That(settings.MaxForceMlp, Is.EqualTo(0.05f));
            Assert.That(settings.IMlp, Is.EqualTo(1f));
            Assert.That(settings.VelocityF, Is.EqualTo(0.5f));
            Assert.That(settings.CopOffset, Is.EqualTo(Vector3.zero));
            Assert.That(settings.TorqueMlp, Is.Zero);
            Assert.That(settings.MaxTorqueMag, Is.EqualTo(45f));
        }

        static RagdollBipedBalancerSettings DefaultSettings(float torqueMlp = 1f)
        {
            RagdollBipedBalancerSettings settings = RagdollBipedBalancerSettings.Default;
            settings.TorqueMlp = torqueMlp;
            return settings;
        }

        [Test]
        public void ResolveCenterOfPressureTarget_OffsetsSupportCenterInWorldSpace()
        {
            Vector3 target = RagdollBipedBalancerMath.ResolveCenterOfPressureTarget(
                new Vector3(1f, 0f, 2f), new Vector3(0.1f, 0f, -0.2f));

            Assert.That(target, Is.EqualTo(new Vector3(1.1f, 0f, 1.8f)));
        }

        [Test]
        public void ResolveReactiveTorque_ZeroOffsetAndVelocity_IsZero()
        {
            Vector3 torque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                capturePoint: Vector3.zero,
                captureVelocity: Vector3.zero,
                centerOfPressureTarget: Vector3.zero,
                up: Vector3.up,
                settings: DefaultSettings());

            Assert.That(torque, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ResolveReactiveTorque_TorqueMlpZero_IsAlwaysZero()
        {
            Vector3 torque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                capturePoint: new Vector3(0.5f, 0f, 0f),
                captureVelocity: new Vector3(1f, 0f, 0f),
                centerOfPressureTarget: Vector3.zero,
                up: Vector3.up,
                settings: DefaultSettings(torqueMlp: 0f));

            Assert.That(torque, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ResolveReactiveTorque_ForwardOffset_ProducesHorizontalAxisTorque()
        {
            Vector3 torque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                capturePoint: new Vector3(0f, 0f, 0.5f),
                captureVelocity: Vector3.zero,
                centerOfPressureTarget: Vector3.zero,
                up: Vector3.up,
                settings: DefaultSettings());

            Assert.That(torque.y, Is.EqualTo(0f).Within(0.0001f),
                "A lean-correcting ankle torque rotates about a horizontal axis, not the up axis.");
            Assert.That(torque.magnitude, Is.GreaterThan(0f));
        }

        [Test]
        public void ResolveReactiveTorque_ScalesWithTorqueMlpAndIMlp()
        {
            RagdollBipedBalancerSettings baseline = DefaultSettings(torqueMlp: 1f);
            RagdollBipedBalancerSettings doubled = baseline;
            doubled.IMlp = 2f;

            Vector3 offset = new Vector3(0f, 0f, 0.5f);
            Vector3 baseTorque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                offset, Vector3.zero, Vector3.zero, Vector3.up, baseline);
            Vector3 doubledTorque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                offset, Vector3.zero, Vector3.zero, Vector3.up, doubled);

            Assert.That(doubledTorque.magnitude,
                Is.EqualTo(baseTorque.magnitude * 2f).Within(0.001f));
        }

        [Test]
        public void ResolveReactiveTorque_VelocityPredictsFurtherLean()
        {
            RagdollBipedBalancerSettings settings = DefaultSettings();
            settings.VelocityF = 1f;

            Vector3 withoutVelocity = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.2f), Vector3.zero, Vector3.zero, Vector3.up, settings);
            Vector3 withVelocity = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.2f), new Vector3(0f, 0f, 0.3f), Vector3.zero, Vector3.up, settings);

            Assert.That(withVelocity.magnitude, Is.GreaterThan(withoutVelocity.magnitude));
        }

        [Test]
        public void ResolveReactiveTorque_ClampsToMaxTorqueMag()
        {
            RagdollBipedBalancerSettings settings = DefaultSettings(torqueMlp: 1000f);
            settings.MaxTorqueMag = 5f;

            Vector3 torque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.zero, Vector3.up, settings);

            Assert.That(torque.magnitude, Is.EqualTo(0.25f).Within(0.001f),
                "Effective cap is MaxTorqueMag * MaxForceMlp.");
        }

        [Test]
        public void ResolveReactiveTorque_MaxForceMlpScalesEffectiveCorrectionLimit()
        {
            RagdollBipedBalancerSettings low = DefaultSettings(torqueMlp: 100f);
            low.MaxTorqueMag = 10f;
            low.MaxForceMlp = 0.1f;
            RagdollBipedBalancerSettings high = low;
            high.MaxForceMlp = 0.5f;

            Vector3 lowTorque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.zero, Vector3.up, low);
            Vector3 highTorque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.zero, Vector3.up, high);

            Assert.That(lowTorque.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(highTorque.magnitude, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void ResolveReactiveTorque_DamperForSpringAttenuatesMatchingAngularVelocity()
        {
            RagdollBipedBalancerSettings settings = DefaultSettings(torqueMlp: 10f);
            settings.MaxForceMlp = 1f;
            settings.MaxTorqueMag = 10f;
            settings.DamperForSpring = 1f;
            Vector3 baseline = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.5f), Vector3.zero, Vector3.zero,
                Vector3.up, settings);
            Vector3 damped = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.5f), Vector3.zero, Vector3.zero,
                Vector3.up, baseline.normalized * 2f, settings);

            Assert.That(damped.magnitude,
                Is.EqualTo(baseline.magnitude - 2f).Within(0.001f));
        }

        // Contract evidence for E03. Keep the focused unit tests above: this
        // single stable-ID test proves the public settings together, matching
        // the catalog claim rather than certifying it from damping alone.
        [Test]
        public void E03_PublicSettingsHaveObservableEffects()
        {
            RagdollBipedBalancerSettings lowForce = DefaultSettings(torqueMlp: 100f);
            lowForce.MaxTorqueMag = 10f;
            lowForce.MaxForceMlp = 0.1f;
            RagdollBipedBalancerSettings highForce = lowForce;
            highForce.MaxForceMlp = 0.5f;

            Vector3 lowTorque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.zero,
                Vector3.up, lowForce);
            Vector3 highTorque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.zero,
                Vector3.up, highForce);
            Assert.That(highTorque.magnitude, Is.GreaterThan(lowTorque.magnitude),
                "MaxForceMlp must scale the effective correction limit.");

            RagdollBipedBalancerSettings damping = DefaultSettings(torqueMlp: 10f);
            damping.MaxForceMlp = 1f;
            damping.MaxTorqueMag = 10f;
            damping.DamperForSpring = 1f;
            Vector3 undamped = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.5f), Vector3.zero, Vector3.zero,
                Vector3.up, damping);
            Vector3 damped = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.5f), Vector3.zero, Vector3.zero,
                Vector3.up, undamped.normalized * 2f, damping);
            Assert.That(damped.magnitude, Is.LessThan(undamped.magnitude),
                "DamperForSpring must attenuate matching angular velocity.");
        }

        [Test]
        public void ResolveReactiveTorque_NegativeMaxTorqueMag_IsSanitizedToZero()
        {
            RagdollBipedBalancerSettings settings = DefaultSettings();
            settings.MaxTorqueMag = -10f;

            Vector3 torque = RagdollBipedBalancerMath.ResolveReactiveTorque(
                new Vector3(0f, 0f, 0.5f), Vector3.zero, Vector3.zero, Vector3.up, settings);

            Assert.That(torque, Is.EqualTo(Vector3.zero));
        }
    }
}
