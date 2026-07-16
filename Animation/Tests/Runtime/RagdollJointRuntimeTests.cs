using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollJointRuntimeTests
    {
        GameObject jointObject;
        GameObject connectedObject;
        ConfigurableJoint joint;

        [SetUp]
        public void SetUp()
        {
            jointObject = new GameObject("Joint Anchor State");
            connectedObject = new GameObject("Connected Anchor State");
            jointObject.AddComponent<Rigidbody>();
            Rigidbody connectedBody =
                connectedObject.AddComponent<Rigidbody>();
            joint = jointObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(jointObject);
            Object.DestroyImmediate(connectedObject);
        }

        [Test]
        public void DefaultSettings_MatchPublishedJointRuntimeDefaults()
        {
            RagdollJointRuntimeSettings settings =
                RagdollJointRuntimeSettings.Default;

            Assert.That(settings.UpdateJointAnchors, Is.True);
            Assert.That(settings.SupportTranslationAnimation, Is.False);
            Assert.That(settings.AngularLimits, Is.False);
        }

        [Test]
        public void Settings_MigratePreSprint0030DataToPublishedDefaults()
        {
            RagdollJointRuntimeSettings settings =
                JsonUtility.FromJson<RagdollJointRuntimeSettings>("{}");

            settings.Normalize();

            Assert.That(settings.UpdateJointAnchors, Is.True);
            Assert.That(settings.SupportTranslationAnimation, Is.False);
            Assert.That(settings.AngularLimits, Is.False);
        }

        [Test]
        public void Settings_PreserveIntentionalBooleanValues()
        {
            RagdollJointRuntimeSettings settings =
                new RagdollJointRuntimeSettings(false, true, true);

            settings.Normalize();

            Assert.That(settings.UpdateJointAnchors, Is.False);
            Assert.That(settings.SupportTranslationAnimation, Is.True);
            Assert.That(settings.AngularLimits, Is.True);
        }

        [TestCase(false, false, false, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, false, false, true)]
        [TestCase(true, true, true, true)]
        public void AnchorPolicy_MatchesDirectParentTranslationGate(
            bool update,
            bool supportTranslation,
            bool directTargetParent,
            bool expected)
        {
            Assert.That(
                RagdollJointAnchorMath.ShouldUpdateAnchor(
                    update,
                    supportTranslation,
                    directTargetParent),
                Is.EqualTo(expected));
        }

        [Test]
        public void ConnectedAnchor_AlignsNonZeroChildAnchor()
        {
            Vector3 connectedAnchor;
            bool success =
                RagdollJointAnchorMath.TryResolveConnectedAnchor(
                    new Vector3(2f, 0f, 0f),
                    Quaternion.identity,
                    Vector3.one,
                    new Vector3(0.5f, 0f, 0f),
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    out connectedAnchor);

            Assert.That(success, Is.True);
            Assert.That(connectedAnchor.x, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(connectedAnchor.y, Is.Zero);
            Assert.That(connectedAnchor.z, Is.Zero);
        }

        [Test]
        public void ConnectedAnchor_HandlesRotationAndNonUniformScale()
        {
            Vector3 connectedAnchor;
            Quaternion childRotation =
                Quaternion.AngleAxis(90f, Vector3.forward);
            Quaternion parentRotation =
                Quaternion.AngleAxis(90f, Vector3.up);

            bool success =
                RagdollJointAnchorMath.TryResolveConnectedAnchor(
                    new Vector3(4f, 2f, -1f),
                    childRotation,
                    new Vector3(2f, 3f, 4f),
                    new Vector3(1f, 0.5f, -0.25f),
                    new Vector3(-2f, 1f, 3f),
                    parentRotation,
                    new Vector3(2f, 4f, 0.5f),
                    out connectedAnchor);

            Assert.That(success, Is.True);

            Vector3 worldFromChild =
                new Vector3(4f, 2f, -1f)
                + childRotation * Vector3.Scale(
                    new Vector3(1f, 0.5f, -0.25f),
                    new Vector3(2f, 3f, 4f));
            Vector3 worldFromParent =
                new Vector3(-2f, 1f, 3f)
                + parentRotation * Vector3.Scale(
                    connectedAnchor,
                    new Vector3(2f, 4f, 0.5f));

            Assert.That(
                Vector3.Distance(worldFromChild, worldFromParent),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void ConnectedAnchor_RejectsNonInvertibleParentScale()
        {
            Vector3 connectedAnchor;
            bool success =
                RagdollJointAnchorMath.TryResolveConnectedAnchor(
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    Vector3.zero,
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(1f, 0f, 1f),
                    out connectedAnchor);

            Assert.That(success, Is.False);
            Assert.That(connectedAnchor, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AnchorState_TakesAndReleasesRuntimeOwnershipExactly()
        {
            Vector3 authoredAnchor = new Vector3(1f, 2f, 3f);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = authoredAnchor;
            joint.autoConfigureConnectedAnchor = true;
            authoredAnchor = joint.connectedAnchor;

            RagdollJointAnchorState state =
                new RagdollJointAnchorState(joint);

            Assert.That(joint.autoConfigureConnectedAnchor, Is.False);
            Assert.That(state.AuthoredConnectedAnchor,
                Is.EqualTo(authoredAnchor));
            Assert.That(state.AuthoredAutoConfigureConnectedAnchor, Is.True);

            Vector3 runtimeAnchor = new Vector3(-4f, 5f, 6f);
            Assert.That(state.TryApply(runtimeAnchor), Is.True);
            Assert.That(joint.connectedAnchor, Is.EqualTo(runtimeAnchor));
            Assert.That(state.RuntimeAnchorApplied, Is.True);

            state.RestoreAuthoredAnchor();
            Assert.That(joint.connectedAnchor, Is.EqualTo(authoredAnchor));
            Assert.That(joint.autoConfigureConnectedAnchor, Is.False);
            Assert.That(state.RuntimeAnchorApplied, Is.False);

            state.TryApply(runtimeAnchor);
            state.ReleaseRuntimeOwnership();
            Assert.That(joint.connectedAnchor, Is.EqualTo(authoredAnchor));
            Assert.That(joint.autoConfigureConnectedAnchor, Is.True);
            Assert.That(state.RuntimeAnchorApplied, Is.False);
        }

        [Test]
        public void AnchorState_RejectsNonFiniteRuntimeAnchor()
        {
            RagdollJointAnchorState state =
                new RagdollJointAnchorState(joint);

            Assert.That(
                state.TryApply(new Vector3(float.NaN, 0f, 0f)),
                Is.False);
            Assert.That(state.RuntimeAnchorApplied, Is.False);
        }

        [Test]
        public void ConnectedAnchor_RejectsNonFiniteInputs()
        {
            Vector3 connectedAnchor;
            bool success =
                RagdollJointAnchorMath.TryResolveConnectedAnchor(
                    new Vector3(float.NaN, 0f, 0f),
                    Quaternion.identity,
                    Vector3.one,
                    Vector3.zero,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    out connectedAnchor);

            Assert.That(success, Is.False);
            Assert.That(connectedAnchor, Is.EqualTo(Vector3.zero));
        }
    }
}
