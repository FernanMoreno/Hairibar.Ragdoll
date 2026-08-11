using System;
using System.Collections.Generic;
using System.Reflection;
using Hairibar.Ragdoll;
using Hairibar.Ragdoll.Animation.Debug;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    /// <summary>
    /// Direct Editor evidence for PuppetMaster's documented
    /// visualizeTargetPose option. The official feature is Editor-only; the
    /// Hairibar component therefore observes its inputs without owning them.
    /// </summary>
    public sealed class RagdollTargetPoseVisualizerEditorTests
    {
        readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index]) Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        [Test]
        public void B30_EditorVisualizerDrawsBoundTargetPoseWithoutMutation()
        {
            GameObject physicalRoot = Own(new GameObject("B30 physical root"));
            physicalRoot.SetActive(false);
            GameObject physicalChild = Own(new GameObject("B30 physical child"));
            // Deliberately flatten Transform parenting. The visualizer must use
            // ConfigurableJoint.connectedBody, which is the physical topology.
            physicalChild.transform.position = new Vector3(3f, 4f, 5f);
            Rigidbody rootBody = physicalRoot.AddComponent<Rigidbody>();
            Rigidbody childBody = physicalChild.AddComponent<Rigidbody>();
            ConfigurableJoint rootJoint =
                physicalRoot.AddComponent<ConfigurableJoint>();
            ConfigurableJoint childJoint =
                physicalChild.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            rootBody.mass = 3f;
            childBody.mass = 5f;
            rootBody.isKinematic = true;
            childBody.isKinematic = true;

            GameObject targetRoot = Own(new GameObject("B30 target root"));
            GameObject targetChild = Own(new GameObject("B30 target child"));
            targetChild.transform.SetParent(targetRoot.transform, false);
            targetRoot.transform.SetPositionAndRotation(
                new Vector3(-2f, 1f, 4f),
                Quaternion.Euler(5f, 15f, 25f));
            targetChild.transform.localPosition = new Vector3(0.2f, 0.8f, -0.1f);
            targetChild.transform.localRotation = Quaternion.Euler(20f, 10f, 0f);

            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");
            RagdollDefinition definition =
                Own(ScriptableObject.CreateInstance<RagdollDefinition>());
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            RagdollDefinitionBindings bindings =
                physicalRoot.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            physicalRoot.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);
            RagdollBone rootBone = bindings.GetBoneAt(0);
            RagdollBone childBone = bindings.GetBoneAt(1);
            var rootPair = new RagdollAnimator.AnimatedPair(
                new RagdollBoneTargetBonePair(
                    rootBone,
                    new RagdollTargetBinding(
                        rootBone.Name, targetRoot.transform, physicalRoot.transform)),
                bindings.GetHandleAt(0),
                RagdollMappingWeights.Full);
            var childPair = new RagdollAnimator.AnimatedPair(
                new RagdollBoneTargetBonePair(
                    childBone,
                    new RagdollTargetBinding(
                        childBone.Name, targetChild.transform, physicalChild.transform)),
                bindings.GetHandleAt(1),
                RagdollMappingWeights.Full);
            rootPair.currentPose = new RagdollAnimator.AnimatedPose
            {
                worldPosition = new Vector3(10f, 11f, 12f),
                worldRotation = Quaternion.Euler(1f, 2f, 3f),
                localRotation = Quaternion.Euler(4f, 5f, 6f)
            };
            childPair.currentPose = new RagdollAnimator.AnimatedPose
            {
                worldPosition = new Vector3(13f, 14f, 15f),
                worldRotation = Quaternion.Euler(7f, 8f, 9f),
                localRotation = Quaternion.Euler(10f, 11f, 12f)
            };
            var pairs = new[] { rootPair, childPair };

            PoseState before = PoseState.Capture(
                physicalRoot.transform, physicalChild.transform,
                targetRoot.transform, targetChild.transform,
                rootBody, childBody, rootJoint, childJoint,
                rootPair, childPair);

            TargetPoseVisualizer visualizer =
                targetRoot.AddComponent<TargetPoseVisualizer>();
            Assert.That(visualizer.boneColor, Is.EqualTo(Color.green),
                "The documented Target-pose lines are green by default.");
            visualizer.Initialize(pairs);
            visualizer.ModifyPose(pairs);

            Assert.That(visualizer.IsInitialized, Is.True);
            Assert.That(visualizer.BindingCount, Is.EqualTo(2));
            Assert.That(visualizer.LastDrawnSegmentCount, Is.EqualTo(1),
                "Exactly one physical parent-to-child line must be rendered.");
            AssertSnapshot(visualizer, physicalRoot.transform,
                targetRoot.transform, rootPair.currentPose, false);
            AssertSnapshot(visualizer, physicalChild.transform,
                targetChild.transform, childPair.currentPose, true);
            before.AssertUnchanged(
                physicalRoot.transform, physicalChild.transform,
                targetRoot.transform, targetChild.transform,
                rootBody, childBody, rootJoint, childJoint,
                rootPair, childPair);

            RagdollCapabilityContract contract =
                RagdollCapabilityCatalog.Get("B30");
            Assert.That(contract.RequiredEvidence,
                Is.EqualTo(new[] { RagdollEvidenceKind.NUnitEditMode }));
            Assert.That(contract.OfficialSource,
                Does.Contain("page5.html").And.Contain("page8.html"));
            Assert.That(contract.ObservableClaim,
                Does.Contain("Editor only").IgnoreCase
                    .And.Contain("no-op").IgnoreCase);
            Assert.That(contract.AffectedApis,
                Does.Contain(typeof(TargetPoseVisualizer).FullName));
            Assert.That(contract.AffectedApis,
                Does.Not.Contain("RagdollTargetPoseVisualizer"));
        }

        T Own<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }

        static object CreateBindings(
            BoneName root,
            ConfigurableJoint rootJoint,
            BoneName child,
            ConfigurableJoint childJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary", BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                null);
            Assert.That(add, Is.Not.Null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { child, childJoint });
            return dictionary;
        }

        static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name);
            field.SetValue(owner, value);
        }

        static void AssertSnapshot(
            TargetPoseVisualizer visualizer,
            Transform physical,
            Transform expectedTarget,
            RagdollAnimator.AnimatedPose expectedPose,
            bool expectedLeaf)
        {
            Transform target;
            Vector3 position;
            Quaternion rotation;
            bool leaf;
            Assert.That(visualizer.TryGetSnapshot(
                physical, out target, out position, out rotation, out leaf),
                Is.True);
            Assert.That(target, Is.SameAs(expectedTarget));
            Assert.That(position, Is.EqualTo(expectedPose.worldPosition));
            Assert.That(Quaternion.Angle(rotation, expectedPose.worldRotation),
                Is.LessThan(0.0001f));
            Assert.That(leaf, Is.EqualTo(expectedLeaf));
        }

        readonly struct PoseState
        {
            readonly Vector3[] positions;
            readonly Quaternion[] rotations;
            readonly float[] masses;
            readonly bool[] kinematic;
            readonly Rigidbody[] connectedBodies;
            readonly RagdollAnimator.AnimatedPose[] poses;

            PoseState(
                Transform[] transforms,
                Rigidbody[] bodies,
                ConfigurableJoint[] joints,
                RagdollAnimator.AnimatedPair[] pairs)
            {
                positions = new Vector3[transforms.Length];
                rotations = new Quaternion[transforms.Length];
                for (int index = 0; index < transforms.Length; index++)
                {
                    positions[index] = transforms[index].position;
                    rotations[index] = transforms[index].rotation;
                }
                masses = new float[bodies.Length];
                kinematic = new bool[bodies.Length];
                for (int index = 0; index < bodies.Length; index++)
                {
                    masses[index] = bodies[index].mass;
                    kinematic[index] = bodies[index].isKinematic;
                }
                connectedBodies = new Rigidbody[joints.Length];
                for (int index = 0; index < joints.Length; index++)
                    connectedBodies[index] = joints[index].connectedBody;
                poses = new RagdollAnimator.AnimatedPose[pairs.Length];
                for (int index = 0; index < pairs.Length; index++)
                    poses[index] = pairs[index].currentPose;
            }

            public static PoseState Capture(
                Transform physicalRoot,
                Transform physicalChild,
                Transform targetRoot,
                Transform targetChild,
                Rigidbody rootBody,
                Rigidbody childBody,
                ConfigurableJoint rootJoint,
                ConfigurableJoint childJoint,
                RagdollAnimator.AnimatedPair rootPair,
                RagdollAnimator.AnimatedPair childPair)
            {
                return new PoseState(
                    new[] { physicalRoot, physicalChild, targetRoot, targetChild },
                    new[] { rootBody, childBody },
                    new[] { rootJoint, childJoint },
                    new[] { rootPair, childPair });
            }

            public void AssertUnchanged(
                Transform physicalRoot,
                Transform physicalChild,
                Transform targetRoot,
                Transform targetChild,
                Rigidbody rootBody,
                Rigidbody childBody,
                ConfigurableJoint rootJoint,
                ConfigurableJoint childJoint,
                RagdollAnimator.AnimatedPair rootPair,
                RagdollAnimator.AnimatedPair childPair)
            {
                Transform[] transforms =
                    { physicalRoot, physicalChild, targetRoot, targetChild };
                Rigidbody[] bodies = { rootBody, childBody };
                ConfigurableJoint[] joints = { rootJoint, childJoint };
                RagdollAnimator.AnimatedPair[] pairs = { rootPair, childPair };
                for (int index = 0; index < transforms.Length; index++)
                {
                    Assert.That(transforms[index].position,
                        Is.EqualTo(positions[index]));
                    Assert.That(Quaternion.Angle(
                        transforms[index].rotation, rotations[index]),
                        Is.LessThan(0.0001f));
                }
                for (int index = 0; index < bodies.Length; index++)
                {
                    Assert.That(bodies[index].mass, Is.EqualTo(masses[index]));
                    Assert.That(bodies[index].isKinematic,
                        Is.EqualTo(kinematic[index]));
                }
                for (int index = 0; index < joints.Length; index++)
                    Assert.That(joints[index].connectedBody,
                        Is.SameAs(connectedBodies[index]));
                for (int index = 0; index < pairs.Length; index++)
                {
                    Assert.That(pairs[index].currentPose.worldPosition,
                        Is.EqualTo(poses[index].worldPosition));
                    Assert.That(Quaternion.Angle(
                        pairs[index].currentPose.worldRotation,
                        poses[index].worldRotation), Is.LessThan(0.0001f));
                    Assert.That(Quaternion.Angle(
                        pairs[index].currentPose.localRotation,
                        poses[index].localRotation), Is.LessThan(0.0001f));
                }
            }
        }
    }
}
