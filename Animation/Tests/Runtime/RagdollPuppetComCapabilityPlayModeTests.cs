using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Physical certification for RootMotion's documented reusable SubBehaviourCOM
    /// contract. These tests intentionally consume the public BehaviourPuppet
    /// snapshot after real fixed steps instead of certifying the math helpers alone.
    /// </summary>
    public sealed class RagdollPuppetComCapabilityPlayModeTests
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        RagdollDefinition definition;
        Vector3 originalGravity;

        [SetUp]
        public void SetUp()
        {
            originalGravity = Physics.gravity;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Physics.gravity = originalGravity;
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index]) UnityEngine.Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator D10_GroundLayersRejectExcludedAndTriggerHitsWithArbitraryGravity()
        {
            Physics.gravity = new Vector3(-9.81f, 0f, 0f);
            GameObject wall = Own(new GameObject("arbitrary-gravity ground"));
            wall.layer = 8;
            wall.transform.position = new Vector3(-1.5f, 2f, 0f);
            BoxCollider wallCollider = wall.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(0.5f, 20f, 20f);
            GameObject trigger = Own(new GameObject("ignored ground trigger"));
            trigger.layer = 8;
            trigger.transform.position = new Vector3(-0.5f, 2f, 0f);
            BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
            triggerCollider.size = new Vector3(0.1f, 20f, 20f);
            triggerCollider.isTrigger = true;

            RagdollSetupResult setup = CreatePhysicalPuppet();
            yield return null;
            setup.Animator.MasterPinWeight = 0f;
            setup.Animator.MasterMuscleWeight = 0f;
            setup.PuppetBehaviour.CenterOfMass.ProbeDistance = 3f;
            setup.PuppetBehaviour.GroundLayers = 1 << 9;

            for (int step = 0; step < 30; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(setup.PuppetBehaviour.Grounding.IsGrounded, Is.False,
                "A collider outside groundLayers must not ground the Puppet.");

            setup.PuppetBehaviour.GroundLayers = 1 << 8;
            RagdollGroundingSnapshot grounded = RagdollGroundingSnapshot.Empty;
            for (int step = 0; step < 90; step++)
            {
                yield return new WaitForFixedUpdate();
                grounded = setup.PuppetBehaviour.Grounding;
                AssertFinite(grounded);
                if (grounded.IsGrounded) break;
            }

            Assert.That(grounded.IsGrounded, Is.True);
            Assert.That(grounded.GroundNormal.x, Is.GreaterThan(0.9f),
                "Effective up must be opposite the arbitrary gravity vector.");
            Assert.That(grounded.GroundPoint.x, Is.LessThan(-1f),
                "The nearer trigger must be ignored by the ground query.");
        }

        [UnityTest]
        public IEnumerator D45_ComReportsPhysicalMassVelocityGroundingAndStableTime()
        {
            RagdollSetupResult setup = CreatePhysicalPuppet();
            yield return null;

            Assert.That(setup.Succeeded, Is.True, setup.Error);
            setup.Animator.MasterPinWeight = 0f;
            setup.Animator.MasterMuscleWeight = 0f;
            setup.PuppetBehaviour.GroundLayers = 1 << 0;

            RagdollGroundingSnapshot grounded = RagdollGroundingSnapshot.Empty;
            for (int step = 0; step < 180; step++)
            {
                yield return new WaitForFixedUpdate();
                RagdollGroundingSnapshot current = setup.PuppetBehaviour.Grounding;
                AssertFinite(current);
                if (current.IsGrounded && current.StableTime > grounded.StableTime)
                    grounded = current;
            }

            Assert.That(grounded.IsGrounded, Is.True,
                "The real Puppet never produced a grounded COM snapshot.");
            Assert.That(grounded.TotalMass, Is.EqualTo(5f).Within(0.001f));
            Assert.That(grounded.StableTime, Is.GreaterThan(0f));
            Assert.That(grounded.GroundNormal.y, Is.GreaterThan(0.99f));
            AssertFinite(grounded);
        }

        [UnityTest]
        public IEnumerator D46_RealMultipleContactsProduceFinitePressureToComGeometry()
        {
            RagdollSetupResult setup = CreatePhysicalPuppet();
            yield return null;

            Assert.That(setup.Succeeded, Is.True, setup.Error);
            setup.Animator.MasterPinWeight = 0f;
            setup.Animator.MasterMuscleWeight = 0f;
            setup.PuppetBehaviour.GroundLayers = 1 << 0;

            RagdollGroundingSnapshot pressure = RagdollGroundingSnapshot.Empty;
            for (int step = 0; step < 180; step++)
            {
                yield return new WaitForFixedUpdate();
                RagdollGroundingSnapshot current = setup.PuppetBehaviour.Grounding;
                AssertFinite(current);
                if (current.HasCenterOfPressure)
                {
                    pressure = current;
                    if (current.IsGrounded) break;
                }
            }

            Assert.That(pressure.HasCenterOfPressure, Is.True,
                "No center of pressure was produced from the real muscle contacts.");
            Assert.That(pressure.CenterOfMassDistance, Is.GreaterThan(0f));
            Assert.That(pressure.CenterOfMassDirection.sqrMagnitude,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(pressure.CenterOfMassAngle,
                Is.InRange(0f, 180f));
            AssertFinite(pressure);
        }

        RagdollSetupResult CreatePhysicalPuppet()
        {
            GameObject floor = Own(new GameObject("COM certification floor"));
            floor.layer = 0;
            floor.transform.position = new Vector3(0.5f, -0.25f, 0f);
            BoxCollider floorCollider = floor.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(20f, 0.5f, 20f);

            GameObject puppet = Own(new GameObject("COM Puppet"));
            puppet.SetActive(false);
            puppet.transform.position = new Vector3(0f, 2f, 0f);
            GameObject puppetChild = Own(new GameObject("Child"));
            puppetChild.transform.SetParent(puppet.transform, false);
            puppetChild.transform.localPosition = Vector3.right;

            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            rootBody.mass = 2f;
            ConfigurableJoint rootJoint = puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();
            Rigidbody childBody = puppetChild.AddComponent<Rigidbody>();
            childBody.mass = 3f;
            ConfigurableJoint childJoint = puppetChild.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            puppetChild.AddComponent<BoxCollider>();

            BoneName rootName = new BoneName("Root");
            BoneName childName = new BoneName("Child");
            definition = Own(ScriptableObject.CreateInstance<RagdollDefinition>());
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

            GameObject target = Own(new GameObject("COM Target"));
            target.transform.position = puppet.transform.position;
            GameObject targetChild = Own(new GameObject("Child"));
            targetChild.transform.SetParent(target.transform, false);
            targetChild.transform.localPosition = Vector3.right;

            RagdollAnimationProfile profile =
                Own(ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            RagdollSetupResult setup =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    target.transform,
                    bindings,
                    profile,
                    30,
                    31);
            Assert.That(setup.Succeeded, Is.True, setup.Error);
            return setup;
        }

        T Own<T>(T value) where T : UnityEngine.Object
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
                "BoneJointBindingsDictionary",
                BindingFlags.NonPublic);
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

        static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        static void AssertFinite(RagdollGroundingSnapshot snapshot)
        {
            AssertFinite(snapshot.CenterOfMass);
            AssertFinite(snapshot.CenterOfMassVelocity);
            AssertFinite(snapshot.CenterOfPressure);
            AssertFinite(snapshot.CenterOfMassVector);
            AssertFinite(snapshot.CenterOfMassDirection);
            Assert.That(float.IsNaN(snapshot.TotalMass)
                || float.IsInfinity(snapshot.TotalMass), Is.False);
            Assert.That(float.IsNaN(snapshot.CenterOfMassDistance)
                || float.IsInfinity(snapshot.CenterOfMassDistance), Is.False);
            Assert.That(float.IsNaN(snapshot.CenterOfMassAngle)
                || float.IsInfinity(snapshot.CenterOfMassAngle), Is.False);
        }

        static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }
    }
}
