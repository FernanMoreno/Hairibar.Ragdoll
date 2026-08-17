using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public class RagdollMappingWeightsTests
    {
        GameObject parentObject;
        GameObject childObject;
        GameObject targetParentObject;
        GameObject targetChildObject;
        RagdollDefinition definition;
        RagdollDefinitionBindings bindings;

        [TearDown]
        public void TearDown()
        {
            if (targetChildObject) Object.DestroyImmediate(targetChildObject);
            if (targetParentObject) Object.DestroyImmediate(targetParentObject);
            if (childObject) Object.DestroyImmediate(childObject);
            if (parentObject) Object.DestroyImmediate(parentObject);
            if (definition) Object.DestroyImmediate(definition);
        }

        [Test]
        public void FullMapping_UsesSimulatedPositionAndRotation()
        {
            parentObject = new GameObject("Target");
            parentObject.transform.SetPositionAndRotation(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(10f, 20f, 30f));

            Vector3 simulatedPosition = new Vector3(8f, 9f, 10f);
            Quaternion simulatedRotation = Quaternion.Euler(40f, 50f, 60f);

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                simulatedPosition,
                simulatedRotation,
                RagdollMappingWeights.Full);

            Assert.That(parentObject.transform.position, Is.EqualTo(simulatedPosition));
            Assert.That(Quaternion.Angle(parentObject.transform.rotation, simulatedRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void ZeroMapping_PreservesAnimatedTransform()
        {
            parentObject = new GameObject("Target");
            Vector3 animatedPosition = new Vector3(1f, 2f, 3f);
            Quaternion animatedRotation = Quaternion.Euler(10f, 20f, 30f);
            parentObject.transform.SetPositionAndRotation(animatedPosition, animatedRotation);

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                new Vector3(8f, 9f, 10f),
                Quaternion.Euler(40f, 50f, 60f),
                RagdollMappingWeights.None);

            Assert.That(parentObject.transform.position, Is.EqualTo(animatedPosition));
            Assert.That(Quaternion.Angle(parentObject.transform.rotation, animatedRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void PositionAndRotationMapping_AreIndependent()
        {
            parentObject = new GameObject("Target");
            Vector3 animatedPosition = new Vector3(1f, 2f, 3f);
            Quaternion animatedRotation = Quaternion.Euler(10f, 20f, 30f);
            Quaternion simulatedRotation = Quaternion.Euler(40f, 50f, 60f);
            parentObject.transform.SetPositionAndRotation(animatedPosition, animatedRotation);

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                new Vector3(8f, 9f, 10f),
                simulatedRotation,
                new RagdollMappingWeights(0f, 1f));

            Assert.That(parentObject.transform.position, Is.EqualTo(animatedPosition));
            Assert.That(Quaternion.Angle(parentObject.transform.rotation, simulatedRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void ChildWithZeroPositionWeight_FollowsMappedParentWithoutWorldSpaceCorrection()
        {
            parentObject = new GameObject("Parent");
            childObject = new GameObject("Child");
            childObject.transform.SetParent(parentObject.transform, false);
            childObject.transform.localPosition = Vector3.right;

            RagdollToTargetMapper.MapTransform(
                parentObject.transform,
                new Vector3(10f, 0f, 0f),
                Quaternion.identity,
                RagdollMappingWeights.Full);

            RagdollToTargetMapper.MapTransform(
                childObject.transform,
                new Vector3(20f, 0f, 0f),
                Quaternion.identity,
                RagdollMappingWeights.None);

            Assert.That(childObject.transform.localPosition, Is.EqualTo(Vector3.right));
            Assert.That(childObject.transform.position, Is.EqualTo(new Vector3(11f, 0f, 0f)));
        }

        [Test]
        public void Multipliers_ComposeAndClamp()
        {
            RagdollMappingWeights weights = new RagdollMappingWeights(0.8f, 0.5f);

            weights.Multiply(0.5f, 4f);

            Assert.That(weights.PositionWeight, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(weights.RotationWeight, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void EffectiveMappingSnapshot_IncludesMasterAndModifierWeights()
        {
            CreateTwoPairMappingRig();
            var targetBindings = new List<RagdollTargetBinding>
            {
                new RagdollTargetBinding(
                    bindings.GetBoneAt(0).Name,
                    targetParentObject.transform,
                    parentObject.transform),
                new RagdollTargetBinding(
                    bindings.GetBoneAt(1).Name,
                    targetChildObject.transform,
                    childObject.transform)
            };
            var mapper = new RagdollToTargetMapper(bindings, targetBindings);
            var pairs = new[]
            {
                new RagdollAnimator.AnimatedPair(
                    new RagdollBoneTargetBonePair(
                        bindings.GetBoneAt(0), targetBindings[0]),
                    bindings.GetHandleAt(0),
                    new RagdollMappingWeights(0.8f, 0.6f)),
                new RagdollAnimator.AnimatedPair(
                    new RagdollBoneTargetBonePair(
                        bindings.GetBoneAt(1), targetBindings[1]),
                    bindings.GetHandleAt(1),
                    new RagdollMappingWeights(0.4f, 0.2f))
            };
            var modifier = parentObject.AddComponent<SnapshotModifier>();

            mapper.MapRagdollToTarget(
                pairs,
                0.5f,
                new IRagdollMappingModifier[] { modifier });

            Assert.That(pairs[0].EffectiveMappingAvailable, Is.True);
            Assert.That(pairs[0].EffectiveMappingWeights.PositionWeight,
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(pairs[0].EffectiveMappingWeights.RotationWeight,
                Is.EqualTo(0.225f).Within(0.0001f));
            Assert.That(pairs[1].EffectiveMappingAvailable, Is.True);
            Assert.That(pairs[1].EffectiveMappingWeights.PositionWeight,
                Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(pairs[1].EffectiveMappingWeights.RotationWeight,
                Is.EqualTo(0.075f).Within(0.0001f));
        }

        void CreateTwoPairMappingRig()
        {
            parentObject = new GameObject("Mapping Puppet Root");
            childObject = new GameObject("Mapping Puppet Child");
            parentObject.SetActive(false);
            targetParentObject = new GameObject("Mapping Target Root");
            targetChildObject = new GameObject("Mapping Target Child");
            targetChildObject.transform.SetParent(targetParentObject.transform, false);

            Rigidbody rootBody = parentObject.AddComponent<Rigidbody>();
            Rigidbody childBody = childObject.AddComponent<Rigidbody>();
            rootBody.isKinematic = true;
            childBody.isKinematic = true;
            ConfigurableJoint rootJoint = parentObject.AddComponent<ConfigurableJoint>();
            ConfigurableJoint childJoint = childObject.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            parentObject.AddComponent<BoxCollider>();
            childObject.AddComponent<BoxCollider>();

            BoneName rootName = new BoneName("MappingRoot");
            BoneName childName = new BoneName("MappingChild");
            definition = ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            bindings = parentObject.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            parentObject.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);
        }

        static object CreateBindings(
            BoneName root,
            ConfigurableJoint rootJoint,
            BoneName child,
            ConfigurableJoint childJoint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary", BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) },
                null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { child, childJoint });
            return dictionary;
        }

        static void SetField(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + name);
            field.SetValue(owner, value);
        }

        sealed class SnapshotModifier : MonoBehaviour, IRagdollMappingModifier
        {
            public void ModifyMapping(
                ref RagdollMappingWeights mappingWeights,
                RagdollAnimator.AnimatedPair pair)
            {
                mappingWeights = new RagdollMappingWeights(
                    mappingWeights.PositionWeight * 0.25f,
                    mappingWeights.RotationWeight * 0.75f);
            }
        }
    }
}
