using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollRuntimeAuthoringTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
        }

        [Test]
        public void Build_CreatesConfigurableRigAndConservesTotalMass()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollAuthoringOptions options = RagdollAuthoringOptions.Default;
            options.includeSpine = false;
            options.includeChest = false;
            options.includeHands = false;
            options.includeFeet = false;
            options.totalMass = 80f;

            RagdollAuthoredRig rig;
            string error;
            bool built = RagdollRuntimeAuthoring.TryBuild(
                references,
                options,
                out rig,
                out error);

            Assert.That(built, Is.True, error);
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.Rigidbodies.Length, Is.EqualTo(10));
            Assert.That(rig.Colliders.Length, Is.EqualTo(10));
            Assert.That(rig.Joints.Length, Is.EqualTo(10));

            float totalMass = 0f;
            for (int index = 0; index < rig.Rigidbodies.Length; index++)
            {
                totalMass += rig.Rigidbodies[index].mass;
            }
            Assert.That(totalMass, Is.EqualTo(80f).Within(0.001f));
            Assert.That(rig.Joints[0].connectedBody, Is.Null);
            Assert.That(rig.Joints[1].connectedBody, Is.Not.Null);
        }

        [Test]
        public void BiometricMass_NormalizesIncludedBonesToExactTotal()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollAuthoringOptions options = RagdollAuthoringOptions.Default;
            options.includeSpine = true;
            options.includeChest = true;
            options.includeHands = true;
            options.includeFeet = true;
            options.totalMass = 73f;
            options.massDistribution = RagdollAuthoringMassDistribution.Biometric;

            RagdollAuthoredRig rig;
            string error;
            Assert.That(RagdollRuntimeAuthoring.TryBuild(
                references,
                options,
                out rig,
                out error), Is.True, error);

            float total = 0f;
            float headMass = 0f;
            float handMass = 0f;
            for (int index = 0; index < rig.Rigidbodies.Length; index++)
            {
                Rigidbody body = rig.Rigidbodies[index];
                total += body.mass;
                if (body.transform == references.head) headMass = body.mass;
                if (body.transform == references.leftHand) handMass = body.mass;
            }
            Assert.That(total, Is.EqualTo(73f).Within(0.0001f));
            Assert.That(headMass, Is.GreaterThan(handMass));
        }

        [Test]
        public void Options_NormalizeNonFiniteGeometryInputs()
        {
            RagdollAuthoringOptions options = RagdollAuthoringOptions.Default;
            options.totalMass = float.NaN;
            options.colliderRadiusScale = float.PositiveInfinity;
            options.colliderLengthOverlap = float.NaN;
            options.minimumColliderSize = 0f;
            options.massDistribution = (RagdollAuthoringMassDistribution)999;
            options.Normalize();

            Assert.That(options.totalMass, Is.EqualTo(70f));
            Assert.That(options.colliderRadiusScale, Is.EqualTo(0.22f));
            Assert.That(options.colliderLengthOverlap, Is.EqualTo(0.1f));
            Assert.That(options.minimumColliderSize, Is.GreaterThan(0f));
            Assert.That(options.massDistribution,
                Is.EqualTo(RagdollAuthoringMassDistribution.Biometric));
        }

        [Test]
        public void Build_RefusesToOverwriteAuthoredComponents()
        {
            RagdollBipedReferences references = CreateReferences();
            references.leftLowerArm.gameObject.AddComponent<Rigidbody>();

            RagdollAuthoredRig rig;
            string error;
            bool built = RagdollRuntimeAuthoring.TryBuild(
                references,
                RagdollAuthoringOptions.Default,
                out rig,
                out error);

            Assert.That(built, Is.False);
            Assert.That(rig, Is.Null);
            StringAssert.Contains("already has Rigidbody", error);
            Assert.That(references.hips.GetComponent<RagdollAuthoredRig>(), Is.Null);
        }

        [Test]
        public void References_RejectDuplicateSemanticBones()
        {
            RagdollBipedReferences references = CreateReferences();
            references.rightLowerArm = references.leftLowerArm;

            string error;
            Assert.That(references.Validate(out error), Is.False);
            StringAssert.Contains("different Transform", error);
        }

        [Test]
        public void AuthoredRig_FlatAndTreePreserveJointTopologyAndWorldPose()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollAuthoredRig rig;
            string error;
            Assert.That(RagdollRuntimeAuthoring.TryBuild(
                references,
                RagdollAuthoringOptions.Default,
                out rig,
                out error), Is.True, error);

            Transform child = rig.Rigidbodies[1].transform;
            Transform originalParent = child.parent;
            Vector3 position = child.position;
            Rigidbody connected = rig.Joints[1].connectedBody;
            rig.SetFlatHierarchy(root.transform);

            Assert.That(rig.IsFlatHierarchy, Is.True);
            Assert.That(child.parent, Is.SameAs(root.transform));
            Assert.That(child.position, Is.EqualTo(position));
            Assert.That(rig.Joints[1].connectedBody, Is.SameAs(connected));

            rig.SetTreeHierarchy();
            Assert.That(rig.IsFlatHierarchy, Is.False);
            Assert.That(child.parent, Is.SameAs(originalParent));
            Assert.That(child.position, Is.EqualTo(position));
        }

        [Test]
        public void SetLayerRecursively_UpdatesEntireHierarchy()
        {
            RagdollBipedReferences references = CreateReferences();
            RagdollSetupUtility.SetLayerRecursively(root.transform, 7);

            foreach (Transform bone in references.EnumerateAll())
            {
                if (bone) Assert.That(bone.gameObject.layer, Is.EqualTo(7));
            }
        }

        RagdollBipedReferences CreateReferences()
        {
            root = new GameObject("Biped");
            Transform hips = Bone("Hips", root.transform, Vector3.zero);
            Transform spine = Bone("Spine", hips, Vector3.up * 0.4f);
            Transform chest = Bone("Chest", spine, Vector3.up * 0.4f);
            Transform head = Bone("Head", chest, Vector3.up * 0.5f);

            Transform lua = Bone("LeftUpperArm", chest, new Vector3(-0.3f, 0.2f, 0f));
            Transform lla = Bone("LeftLowerArm", lua, new Vector3(-0.4f, 0f, 0f));
            Transform lh = Bone("LeftHand", lla, new Vector3(-0.3f, 0f, 0f));
            Transform rua = Bone("RightUpperArm", chest, new Vector3(0.3f, 0.2f, 0f));
            Transform rla = Bone("RightLowerArm", rua, new Vector3(0.4f, 0f, 0f));
            Transform rh = Bone("RightHand", rla, new Vector3(0.3f, 0f, 0f));
            Transform lul = Bone("LeftUpperLeg", hips, new Vector3(-0.2f, -0.4f, 0f));
            Transform lll = Bone("LeftLowerLeg", lul, new Vector3(0f, -0.5f, 0f));
            Transform lf = Bone("LeftFoot", lll, new Vector3(0f, -0.4f, 0.15f));
            Transform rul = Bone("RightUpperLeg", hips, new Vector3(0.2f, -0.4f, 0f));
            Transform rll = Bone("RightLowerLeg", rul, new Vector3(0f, -0.5f, 0f));
            Transform rf = Bone("RightFoot", rll, new Vector3(0f, -0.4f, 0.15f));

            return new RagdollBipedReferences
            {
                hips = hips, spine = spine, chest = chest, head = head,
                leftUpperArm = lua, leftLowerArm = lla, leftHand = lh,
                rightUpperArm = rua, rightLowerArm = rla, rightHand = rh,
                leftUpperLeg = lul, leftLowerLeg = lll, leftFoot = lf,
                rightUpperLeg = rul, rightLowerLeg = rll, rightFoot = rf
            };
        }

        static Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            Transform bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            return bone;
        }
    }
}
