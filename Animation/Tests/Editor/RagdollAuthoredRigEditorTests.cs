using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollAuthoredRigEditorTests
    {
        GameObject owner;
        PhysicsMaterial material;
        RagdollAuthoredRig rig;
        RagdollAuthoredRigEditor inspector;
        float expectedContactOffset;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Authored collider conversion");
            Rigidbody body = owner.AddComponent<Rigidbody>();
            BoxCollider collider = owner.AddComponent<BoxCollider>();
            collider.center = new Vector3(1f, 2f, 3f);
            collider.size = new Vector3(2f, 6f, 4f);
            collider.isTrigger = true;
            collider.enabled = false;
            collider.contactOffset = 0.02f;
            expectedContactOffset = collider.contactOffset;
            collider.providesContacts = true;
            collider.layerOverridePriority = 7;
            collider.includeLayers = 1 << 8;
            collider.excludeLayers = 1 << 9;
            material = new PhysicsMaterial("conversion material");
            collider.sharedMaterial = material;
            ConfigurableJoint joint = owner.AddComponent<ConfigurableJoint>();
            rig = owner.AddComponent<RagdollAuthoredRig>();
            rig.SetOwnedComponents(
                new[] { body },
                new Collider[] { collider },
                new[] { joint });
            inspector = (RagdollAuthoredRigEditor)UnityEditor.Editor.CreateEditor(
                rig,
                typeof(RagdollAuthoredRigEditor));
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (inspector) Object.DestroyImmediate(inspector);
            if (owner) Object.DestroyImmediate(owner);
            if (material) Object.DestroyImmediate(material);
        }

        [Test]
        public void BoxCapsuleSphereBox_PreservesCommonSettingsAndUndo()
        {
            Assert.That(
                inspector.ConvertSelectedCollider(typeof(CapsuleCollider)),
                Is.True);
            CapsuleCollider capsule = rig.Colliders[0] as CapsuleCollider;
            AssertCommon(capsule);
            Assert.That(capsule.direction, Is.EqualTo(1));
            Assert.That(capsule.height, Is.GreaterThanOrEqualTo(capsule.radius * 2f));

            Assert.That(
                inspector.ConvertSelectedCollider(typeof(SphereCollider)),
                Is.True);
            AssertCommon(rig.Colliders[0] as SphereCollider);

            Assert.That(
                inspector.ConvertSelectedCollider(typeof(BoxCollider)),
                Is.True);
            BoxCollider box = rig.Colliders[0] as BoxCollider;
            AssertCommon(box);
            Assert.That(box.size, Is.EqualTo(Vector3.one * 6f));

            Undo.PerformUndo();
            Assert.That(rig.Colliders[0], Is.TypeOf<SphereCollider>());
            Undo.PerformRedo();
            Assert.That(rig.Colliders[0], Is.TypeOf<BoxCollider>());
            AssertCommon(rig.Colliders[0]);
        }

        [Test]
        public void UnsupportedType_IsRejectedBeforeMutation()
        {
            Collider original = rig.Colliders[0];
            Assert.Throws<System.ArgumentException>(() =>
                inspector.ConvertSelectedCollider(typeof(MeshCollider)));
            Assert.That(rig.Colliders[0], Is.SameAs(original));
        }

        [Test]
        public void MirrorJoint_CopiesLimitsWithinToleranceAndIsUndoable()
        {
            Object.DestroyImmediate(inspector);
            Object.DestroyImmediate(owner);
            owner = new GameObject("Symmetric authored rig");
            GameObject leftObject = new GameObject("Left");
            GameObject rightObject = new GameObject("Right");
            leftObject.transform.SetParent(owner.transform, false);
            rightObject.transform.SetParent(owner.transform, false);
            leftObject.transform.localPosition = Vector3.left;
            rightObject.transform.localPosition = Vector3.right;
            Rigidbody leftBody = leftObject.AddComponent<Rigidbody>();
            Rigidbody rightBody = rightObject.AddComponent<Rigidbody>();
            ConfigurableJoint left = leftObject.AddComponent<ConfigurableJoint>();
            ConfigurableJoint right = rightObject.AddComponent<ConfigurableJoint>();
            SoftJointLimit limit = left.angularYLimit;
            limit.limit = 73f;
            left.angularYLimit = limit;
            rig = owner.AddComponent<RagdollAuthoredRig>();
            rig.SetOwnedComponents(
                new[] { leftBody, rightBody },
                new Collider[0],
                new[] { left, right });
            inspector = (RagdollAuthoredRigEditor)UnityEditor.Editor.CreateEditor(
                rig, typeof(RagdollAuthoredRigEditor));
            inspector.SymmetryDistance = 0.1f;

            inspector.MirrorSelectedJoint();

            Assert.That(right.angularYLimit.limit, Is.EqualTo(73f));
            Undo.PerformUndo();
            Assert.That(right.angularYLimit.limit, Is.Not.EqualTo(73f));
        }

        [Test]
        public void MirrorJoint_RejectsRemoteCandidateOutsideTolerance()
        {
            Object.DestroyImmediate(inspector);
            ConfigurableJoint source = rig.Joints[0];
            GameObject remote = new GameObject("Remote");
            remote.transform.SetParent(owner.transform, false);
            remote.transform.localPosition = Vector3.right * 100f;
            Rigidbody remoteBody = remote.AddComponent<Rigidbody>();
            ConfigurableJoint remoteJoint = remote.AddComponent<ConfigurableJoint>();
            SoftJointLimit remoteBefore = remoteJoint.angularYLimit;
            rig.SetOwnedComponents(
                new[] { rig.Rigidbodies[0], remoteBody },
                rig.Colliders,
                new[] { source, remoteJoint });
            inspector = (RagdollAuthoredRigEditor)UnityEditor.Editor.CreateEditor(
                rig, typeof(RagdollAuthoredRigEditor));
            inspector.SymmetryDistance = 0.25f;

            inspector.MirrorSelectedJoint();

            Assert.That(remoteJoint.angularYLimit.limit,
                Is.EqualTo(remoteBefore.limit));
        }

        void AssertCommon(Collider value)
        {
            Assert.That(value, Is.Not.Null);
            Assert.That(value.enabled, Is.False);
            Assert.That(value.isTrigger, Is.True);
            Assert.That(value.sharedMaterial, Is.SameAs(material));
            Assert.That(
                value.contactOffset,
                Is.EqualTo(expectedContactOffset).Within(0.0001f));
            Assert.That(value.providesContacts, Is.True);
            Assert.That(value.layerOverridePriority, Is.EqualTo(7));
            Assert.That(value.includeLayers.value, Is.EqualTo(1 << 8));
            Assert.That(value.excludeLayers.value, Is.EqualTo(1 << 9));
        }
    }
}
