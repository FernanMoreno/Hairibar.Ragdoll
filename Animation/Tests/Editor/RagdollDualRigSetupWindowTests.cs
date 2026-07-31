using System;
using System.Reflection;
using Hairibar.Ragdoll;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    public sealed class RagdollDualRigSetupWindowTests
    {
        GameObject target;
        GameObject puppet;
        RagdollDefinition definition;
        RagdollAnimationProfile profile;
        GameObject setupRoot;
        bool ignoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
            Physics.IgnoreLayerCollision(30, 31, false);
            profile = ScriptableObject.CreateInstance<RagdollAnimationProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            if (setupRoot) UnityEngine.Object.DestroyImmediate(setupRoot);
            if (target) UnityEngine.Object.DestroyImmediate(target);
            if (puppet) UnityEngine.Object.DestroyImmediate(puppet);
            if (definition) UnityEngine.Object.DestroyImmediate(definition);
            if (profile) UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void CompleteSetup_UndoRedoRestoresWholeTransaction()
        {
            RagdollDefinitionBindings bindings = CreatePuppet();
            CreateTarget("Child");
            target.layer = 4;
            puppet.layer = 5;

            RagdollSetupResult result =
                RagdollDualRigSetupWindow.ApplyCompleteSetup(
                    target.transform, bindings, profile, 30, 31);

            Assert.That(result.Succeeded, Is.True, result.Error);
            setupRoot = result.Root.gameObject;
            Assert.That(result.Root, Is.Not.Null);
            Assert.That(target.transform.parent, Is.SameAs(result.Root));
            Assert.That(puppet.transform.parent, Is.SameAs(result.Root));
            Assert.That(target.GetComponent<RagdollAnimator>(), Is.Not.Null);
            Assert.That(target.GetComponent<RagdollMuscleController>(), Is.Not.Null);
            Assert.That(target.GetComponent<RagdollBehaviourController>(), Is.Not.Null);
            Assert.That(target.GetComponent<RagdollSimulationModeController>(), Is.Not.Null);
            Assert.That(target.transform.Find("Character Behaviours"), Is.Not.Null);
            Assert.That(puppet.GetComponent<RagdollCollisionHub>(), Is.Not.Null);
            Assert.That(target.layer, Is.EqualTo(30));
            Assert.That(puppet.layer, Is.EqualTo(31));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.True);

            Undo.PerformUndo();
            Assert.That(target.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(target.GetComponent<RagdollTargetBindings>(), Is.Null);
            Assert.That(target.transform.Find("Character Behaviours"), Is.Null);
            Assert.That(puppet.GetComponent<RagdollCollisionHub>(), Is.Null);
            Assert.That(target.layer, Is.EqualTo(4));
            Assert.That(puppet.layer, Is.EqualTo(5));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.False);
            Assert.That(target.transform.parent, Is.Null);
            Assert.That(puppet.transform.parent, Is.Null);

            Undo.PerformRedo();
            Assert.That(target.GetComponent<RagdollAnimator>(), Is.Not.Null);
            Assert.That(target.transform.Find("Character Behaviours"), Is.Not.Null);
            Assert.That(puppet.GetComponent<RagdollCollisionHub>(), Is.Not.Null);
            Assert.That(target.layer, Is.EqualTo(30));
            Assert.That(puppet.layer, Is.EqualTo(31));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.True);
            Assert.That(target.transform.parent, Is.SameAs(puppet.transform.parent));
        }

        [Test]
        public void CompleteSetup_InvalidBindingLeavesNoPartialState()
        {
            RagdollDefinitionBindings bindings = CreatePuppet();
            CreateTarget("WrongName");
            target.layer = 4;
            puppet.layer = 5;

            RagdollSetupResult result =
                RagdollDualRigSetupWindow.ApplyCompleteSetup(
                    target.transform, bindings, profile, 30, 31);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.Not.Empty);
            Assert.That(target.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(target.GetComponent<RagdollTargetBindings>(), Is.Null);
            Assert.That(target.transform.Find("Character Behaviours"), Is.Null);
            Assert.That(puppet.GetComponent<RagdollCollisionHub>(), Is.Null);
            Assert.That(target.layer, Is.EqualTo(4));
            Assert.That(puppet.layer, Is.EqualTo(5));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.False);
        }

        RagdollDefinitionBindings CreatePuppet()
        {
            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");
            puppet = new GameObject("Puppet");
            puppet.SetActive(false);
            GameObject child = new GameObject("Child");
            child.transform.SetParent(puppet.transform, false);

            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            ConfigurableJoint rootJoint = puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();
            child.AddComponent<Rigidbody>();
            ConfigurableJoint childJoint = child.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            child.AddComponent<BoxCollider>();

            definition = ScriptableObject.CreateInstance<RagdollDefinition>();
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName, childName });
            RagdollDefinitionBindings bindings =
                puppet.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(
                rootName, rootJoint, childName, childJoint));
            puppet.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);
            return bindings;
        }

        void CreateTarget(string childName)
        {
            target = new GameObject("Puppet");
            GameObject child = new GameObject(childName);
            child.transform.SetParent(target.transform, false);
            child.transform.localPosition = Vector3.up;
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
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(owner, value);
        }
    }
}
