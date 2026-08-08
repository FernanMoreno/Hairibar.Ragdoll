using System;
using System.Collections.Generic;
using System.Reflection;
using Hairibar.Ragdoll;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Editor.Tests
{
    /// <summary>
    /// Direct executable evidence for the RootMotion creation/editing/setup
    /// authoring contracts. Helpers only create fixtures; no test invokes another
    /// test or treats source text as evidence.
    /// </summary>
    public sealed class RagdollAuthoringCapabilityEditorTests
    {
        readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        bool ignored2829;
        bool ignored3031;

        [SetUp]
        public void SetUp()
        {
            ignored2829 = Physics.GetIgnoreLayerCollision(28, 29);
            ignored3031 = Physics.GetIgnoreLayerCollision(30, 31);
            Physics.IgnoreLayerCollision(28, 29, false);
            Physics.IgnoreLayerCollision(30, 31, false);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            Physics.IgnoreLayerCollision(28, 29, ignored2829);
            Physics.IgnoreLayerCollision(30, 31, ignored3031);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index])
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void A01_WizardCreatesCompleteDualRigAsOneUndoTransaction()
        {
            SetupFixture fixture = CreateSetupFixture("A01", true);
            RagdollSetupResult result =
                RagdollDualRigSetupWindow.ApplyCompleteSetup(
                    fixture.Target,
                    fixture.Bindings,
                    fixture.Profile,
                    30,
                    31);

            Assert.That(result.Succeeded, Is.True, result.Error);
            cleanup.Add(result.Root.gameObject);
            Assert.That(result.Root, Is.Not.Null);
            Assert.That(result.Target, Is.SameAs(fixture.Target));
            Assert.That(result.Puppet, Is.SameAs(fixture.Bindings.transform));
            Assert.That(result.Animator, Is.Not.Null);
            Assert.That(result.Muscles, Is.Not.Null);
            Assert.That(result.Simulation, Is.Not.Null);
            Assert.That(result.Behaviours, Is.Not.Null);
            Assert.That(result.Collisions, Is.Not.Null);
            Assert.That(result.PuppetBehaviour, Is.Not.Null);
            Assert.That(result.PuppetBehaviour.transform.parent,
                Is.SameAs(fixture.Target));
            Assert.That(fixture.Target.GetComponent<RagdollTargetBindings>(),
                Is.Not.Null);

            Undo.PerformUndo();
            Assert.That(fixture.Target.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(fixture.Target.GetComponent<RagdollTargetBindings>(),
                Is.Null);
            Assert.That(fixture.Target.Find("Character Behaviours"), Is.Null);
            Assert.That(fixture.Bindings.GetComponent<RagdollCollisionHub>(),
                Is.Null);
            Assert.That(fixture.Target.parent, Is.Null);
            Assert.That(fixture.Bindings.transform.parent, Is.Null);

            Undo.PerformRedo();
            Assert.That(fixture.Target.GetComponent<RagdollAnimator>(), Is.Not.Null);
            Assert.That(fixture.Target.Find("Character Behaviours"), Is.Not.Null);
            Assert.That(fixture.Target.parent,
                Is.SameAs(fixture.Bindings.transform.parent));
        }

        [Test]
        public void A03_AutomaticAuthoringCreatesFiniteNonDegenerateBoneColliders()
        {
            RagdollBipedReferences references = CreateBiped(
                "A03",
                new Vector3(0.015f, 2.5f, 0.4f));
            RagdollAuthoringOptions options = RagdollAuthoringOptions.Default;
            options.minimumColliderSize = 0.002f;
            options.colliderRadiusScale = 0.18f;

            RagdollAuthoredRig rig;
            string error;
            Assert.That(RagdollRuntimeAuthoring.TryBuild(
                references, options, out rig, out error), Is.True, error);
            Assert.That(rig.Colliders.Length, Is.EqualTo(16));
            var intended = new HashSet<Transform>(references.EnumerateAll());
            foreach (Collider collider in rig.Colliders)
            {
                Assert.That(collider, Is.Not.Null);
                Assert.That(intended.Contains(collider.transform), Is.True,
                    collider ? collider.name : "missing collider");
                AssertFinitePositive(ColliderDimensions(collider), collider.name);
                AssertFinite(ColliderCenter(collider), collider.name + " center");
            }

            // The negative path validates before mutation and cannot leave a
            // half-authored owner or components on otherwise valid bones.
            RagdollBipedReferences invalid = CreateBiped("A03-invalid", Vector3.one);
            invalid.rightLowerArm = invalid.leftLowerArm;
            Transform invalidHips = invalid.hips;
            Assert.That(RagdollRuntimeAuthoring.TryBuild(
                invalid, options, out rig, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(invalidHips.GetComponent<RagdollAuthoredRig>(), Is.Null);
            Assert.That(invalidHips.GetComponent<Rigidbody>(), Is.Null);
        }

        [Test]
        public void A04_JointsAndBiometricMassFormExactValidTopology()
        {
            RagdollBipedReferences references = CreateBiped("A04", Vector3.one);
            RagdollAuthoringOptions options = RagdollAuthoringOptions.Default;
            options.totalMass = 73f;
            options.angularXLimit = 37f;
            options.angularYZLimit = 24f;
            options.enableProjection = true;
            options.massDistribution = RagdollAuthoringMassDistribution.Biometric;

            RagdollAuthoredRig rig;
            string error;
            Assert.That(RagdollRuntimeAuthoring.TryBuild(
                references, options, out rig, out error), Is.True, error);

            var bodies = new HashSet<Rigidbody>(rig.Rigidbodies);
            float mass = 0f;
            int roots = 0;
            foreach (ConfigurableJoint joint in rig.Joints)
            {
                Rigidbody body = joint.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null);
                mass += body.mass;
                AssertFinite(joint.axis, joint.name + " axis");
                AssertFinite(joint.secondaryAxis, joint.name + " secondary axis");
                Assert.That(joint.axis.sqrMagnitude, Is.GreaterThan(0.5f));
                Assert.That(joint.secondaryAxis.sqrMagnitude, Is.GreaterThan(0.5f));
                Assert.That(Mathf.Abs(Vector3.Dot(
                    joint.axis.normalized,
                    joint.secondaryAxis.normalized)), Is.LessThan(0.001f));
                if (!joint.connectedBody)
                {
                    roots++;
                    continue;
                }
                Assert.That(bodies.Contains(joint.connectedBody), Is.True);
                Assert.That(joint.transform.IsChildOf(
                    joint.connectedBody.transform), Is.True);
                Assert.That(joint.lowAngularXLimit.limit,
                    Is.EqualTo(-37f).Within(0.001f));
                Assert.That(joint.highAngularXLimit.limit,
                    Is.EqualTo(37f).Within(0.001f));
                Assert.That(joint.angularYLimit.limit,
                    Is.EqualTo(24f).Within(0.001f));
                Assert.That(joint.angularZLimit.limit,
                    Is.EqualTo(24f).Within(0.001f));
                Assert.That(joint.projectionMode,
                    Is.EqualTo(JointProjectionMode.PositionAndRotation));
            }
            Assert.That(roots, Is.EqualTo(1));
            Assert.That(mass, Is.EqualTo(73f).Within(0.0001f));
        }

        [Test]
        public void A07_ColliderConversionPreservesAuthoredStateAndUndoRedo()
        {
            GameObject owner = Track(new GameObject("A07 collider"));
            owner.layer = 17;
            Rigidbody body = owner.AddComponent<Rigidbody>();
            BoxCollider box = owner.AddComponent<BoxCollider>();
            box.center = new Vector3(1f, 2f, 3f);
            box.size = new Vector3(2f, 6f, 4f);
            box.enabled = false;
            box.isTrigger = true;
            PhysicsMaterial material = Track(new PhysicsMaterial("A07 material"));
            box.sharedMaterial = material;
            ConfigurableJoint joint = owner.AddComponent<ConfigurableJoint>();
            RagdollAuthoredRig rig = owner.AddComponent<RagdollAuthoredRig>();
            rig.SetOwnedComponents(new[] { body }, new Collider[] { box },
                new[] { joint });
            RagdollAuthoredRigEditor inspector =
                (RagdollAuthoredRigEditor)UnityEditor.Editor.CreateEditor(
                    rig, typeof(RagdollAuthoredRigEditor));
            cleanup.Add(inspector);

            Assert.That(inspector.ConvertSelectedCollider(
                typeof(CapsuleCollider)), Is.True);
            AssertColliderCommon(rig.Colliders[0], material, 17);
            Assert.That(inspector.ConvertSelectedCollider(
                typeof(SphereCollider)), Is.True);
            AssertColliderCommon(rig.Colliders[0], material, 17);
            Assert.That(inspector.ConvertSelectedCollider(
                typeof(BoxCollider)), Is.True);
            AssertColliderCommon(rig.Colliders[0], material, 17);
            Assert.That(((BoxCollider)rig.Colliders[0]).center,
                Is.EqualTo(new Vector3(1f, 2f, 3f)));

            Undo.PerformUndo();
            Assert.That(rig.Colliders[0], Is.TypeOf<SphereCollider>());
            Undo.PerformRedo();
            Assert.That(rig.Colliders[0], Is.TypeOf<BoxCollider>());

            Collider before = rig.Colliders[0];
            Assert.Throws<ArgumentException>(() =>
                inspector.ConvertSelectedCollider(typeof(MeshCollider)));
            Assert.That(rig.Colliders[0], Is.SameAs(before));
        }

        [Test]
        public void A09_SetupLayersAndCollisionMatrixRestoreOnUndoAndFailure()
        {
            SetupFixture success = CreateSetupFixture("A09-success", true);
            success.Target.gameObject.layer = 4;
            success.Bindings.gameObject.layer = 5;
            success.Target.GetChild(0).gameObject.layer = 6;
            success.Bindings.transform.GetChild(0).gameObject.layer = 7;
            RagdollSetupResult result =
                RagdollDualRigSetupWindow.ApplyCompleteSetup(
                    success.Target, success.Bindings, success.Profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            cleanup.Add(result.Root.gameObject);
            AssertHierarchyLayer(success.Target, 30);
            AssertHierarchyLayer(success.Bindings.transform, 31);
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.True);

            Undo.PerformUndo();
            Assert.That(success.Target.gameObject.layer, Is.EqualTo(4));
            Assert.That(success.Target.GetChild(0).gameObject.layer, Is.EqualTo(6));
            Assert.That(success.Bindings.gameObject.layer, Is.EqualTo(5));
            Assert.That(success.Bindings.transform.GetChild(0).gameObject.layer,
                Is.EqualTo(7));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.False);

            SetupFixture failure = CreateSetupFixture("A09-failure", false);
            failure.Target.gameObject.layer = 8;
            failure.Bindings.gameObject.layer = 9;
            result = RagdollDualRigSetupWindow.ApplyCompleteSetup(
                failure.Target, failure.Bindings, failure.Profile, 28, 29);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(failure.Target.gameObject.layer, Is.EqualTo(8));
            Assert.That(failure.Bindings.gameObject.layer, Is.EqualTo(9));
            Assert.That(Physics.GetIgnoreLayerCollision(28, 29), Is.False);
            Assert.That(failure.Target.GetComponent<RagdollAnimator>(), Is.Null);
        }

        [Test]
        public void A10_RuntimeSetupSupportsAllEntriesAndTransactionalRollback()
        {
            SetupFixture separated = CreateSetupFixture("A10-separated", true);
            RagdollSetupResult configured =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    separated.Target,
                    separated.Bindings,
                    separated.Profile,
                    28,
                    29);
            AssertCompleteSetup(configured);
            if (configured.Root) cleanup.Add(configured.Root.gameObject);

            SetupFixture direct = CreateSetupFixture("A10-direct", true);
            RagdollSetupResult converted =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    direct.Target,
                    direct.Bindings,
                    direct.Profile,
                    30,
                    31);
            AssertCompleteSetup(converted);
            if (converted.Root) cleanup.Add(converted.Root.gameObject);

            SetupFixture duplicate = CreateSetupFixture("A10-duplicate", true);
            UnityEngine.Object.DestroyImmediate(duplicate.Target.gameObject);
            RagdollSetupResult duplicated =
                RagdollRuntimeSetupService.DuplicateAndConvertOriginalToTarget(
                    duplicate.Bindings,
                    duplicate.Profile,
                    28,
                    29);
            AssertCompleteSetup(duplicated);
            if (duplicated.Root) cleanup.Add(duplicated.Root.gameObject);
            Assert.That(duplicated.Target.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(duplicated.Puppet.GetComponent<Rigidbody>(), Is.Not.Null);

            SetupFixture rollback = CreateSetupFixture("A10-rollback", true);
            AudioSource external = rollback.Target.gameObject.AddComponent<AudioSource>();
            int targetLayer = rollback.Target.gameObject.layer;
            int puppetLayer = rollback.Bindings.gameObject.layer;
            bool ignored = Physics.GetIgnoreLayerCollision(30, 31);
            RagdollSetupResult failed =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    rollback.Target,
                    rollback.Bindings,
                    rollback.Profile,
                    30,
                    31,
                    new ThrowingSetupFactory());
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Error, Does.Contain("injected"));
            Assert.That(external, Is.Not.Null);
            Assert.That(rollback.Target.gameObject.layer, Is.EqualTo(targetLayer));
            Assert.That(rollback.Bindings.gameObject.layer, Is.EqualTo(puppetLayer));
            Assert.That(Physics.GetIgnoreLayerCollision(30, 31), Is.EqualTo(ignored));
            Assert.That(rollback.Target.GetComponent<RagdollTargetBindings>(), Is.Null);
            Assert.That(rollback.Target.GetComponent<RagdollAnimator>(), Is.Null);
            Assert.That(rollback.Target.Find("Character Behaviours"), Is.Null);
        }

        [Test]
        public void A11_FlatTreeIncludesRootAndPreservesPoseTopologyAndExternalObjects()
        {
            RagdollBipedReferences references = CreateBiped("A11", Vector3.one);
            RagdollAuthoredRig rig;
            string error;
            Assert.That(RagdollRuntimeAuthoring.TryBuild(
                references,
                RagdollAuthoringOptions.Default,
                out rig,
                out error), Is.True, error);
            GameObject container = Track(new GameObject("A11 flat container"));
            GameObject unrelated = Track(new GameObject("A11 unrelated"));
            unrelated.transform.SetParent(container.transform, false);
            Transform unrelatedParent = unrelated.transform.parent;

            Transform[] parents = new Transform[rig.Rigidbodies.Length];
            Vector3[] positions = new Vector3[rig.Rigidbodies.Length];
            Quaternion[] rotations = new Quaternion[rig.Rigidbodies.Length];
            Rigidbody[] connected = new Rigidbody[rig.Joints.Length];
            Rigidbody[] identities = (Rigidbody[])rig.Rigidbodies.Clone();
            for (int index = 0; index < rig.Rigidbodies.Length; index++)
            {
                parents[index] = rig.Rigidbodies[index].transform.parent;
                positions[index] = rig.Rigidbodies[index].position;
                rotations[index] = rig.Rigidbodies[index].rotation;
                connected[index] = rig.Joints[index].connectedBody;
            }

            rig.SetFlatHierarchy(container.transform);
            Assert.That(rig.IsFlatHierarchy, Is.True);
            for (int index = 0; index < identities.Length; index++)
            {
                Assert.That(rig.Rigidbodies[index], Is.SameAs(identities[index]));
                Assert.That(rig.Rigidbodies[index].transform.parent,
                    Is.SameAs(container.transform));
                AssertPose(rig.Rigidbodies[index], positions[index], rotations[index]);
                Assert.That(rig.Joints[index].connectedBody,
                    Is.SameAs(connected[index]));
            }
            Assert.That(unrelated.transform.parent, Is.SameAs(unrelatedParent));

            rig.SetTreeHierarchy();
            Assert.That(rig.IsFlatHierarchy, Is.False);
            for (int index = 0; index < identities.Length; index++)
            {
                Assert.That(rig.Rigidbodies[index], Is.SameAs(identities[index]));
                Assert.That(rig.Rigidbodies[index].transform.parent,
                    Is.SameAs(parents[index]));
                AssertPose(rig.Rigidbodies[index], positions[index], rotations[index]);
                Assert.That(rig.Joints[index].connectedBody,
                    Is.SameAs(connected[index]));
            }
            Assert.That(unrelated.transform.parent, Is.SameAs(unrelatedParent));
        }

        SetupFixture CreateSetupFixture(string prefix, bool matchingTarget)
        {
            GameObject puppet = Track(new GameObject(prefix + " Puppet"));
            puppet.SetActive(false);
            GameObject puppetChild = new GameObject("Child");
            puppetChild.transform.SetParent(puppet.transform, false);
            Rigidbody rootBody = puppet.AddComponent<Rigidbody>();
            ConfigurableJoint rootJoint = puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();
            puppetChild.AddComponent<Rigidbody>();
            ConfigurableJoint childJoint = puppetChild.AddComponent<ConfigurableJoint>();
            childJoint.connectedBody = rootBody;
            puppetChild.AddComponent<BoxCollider>();

            BoneName rootName = new BoneName(puppet.name);
            BoneName childName = new BoneName("Child");
            RagdollDefinition definition = Track(
                ScriptableObject.CreateInstance<RagdollDefinition>());
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

            GameObject target = Track(new GameObject(puppet.name));
            GameObject targetChild = new GameObject(
                matchingTarget ? "Child" : "DifferentChild");
            targetChild.transform.SetParent(target.transform, false);
            targetChild.transform.localPosition = Vector3.up;
            RagdollAnimationProfile profile = Track(
                ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            return new SetupFixture
            {
                Target = target.transform,
                Bindings = bindings,
                Profile = profile
            };
        }

        RagdollBipedReferences CreateBiped(string prefix, Vector3 scale)
        {
            GameObject root = Track(new GameObject(prefix + " Biped"));
            root.transform.localScale = scale;
            Transform hips = Bone("Hips", root.transform, Vector3.zero);
            Transform spine = Bone("Spine", hips, Vector3.up * 0.4f);
            Transform chest = Bone("Chest", spine, Vector3.up * 0.4f);
            Transform head = Bone("Head", chest, Vector3.up * 0.5f);
            Transform lua = Bone("LeftUpperArm", chest,
                new Vector3(-0.3f, 0.2f, 0f));
            Transform lla = Bone("LeftLowerArm", lua,
                new Vector3(-0.4f, 0f, 0f));
            Transform lh = Bone("LeftHand", lla, new Vector3(-0.3f, 0f, 0f));
            Transform rua = Bone("RightUpperArm", chest,
                new Vector3(0.3f, 0.2f, 0f));
            Transform rla = Bone("RightLowerArm", rua,
                new Vector3(0.4f, 0f, 0f));
            Transform rh = Bone("RightHand", rla, new Vector3(0.3f, 0f, 0f));
            Transform lul = Bone("LeftUpperLeg", hips,
                new Vector3(-0.2f, -0.4f, 0f));
            Transform lll = Bone("LeftLowerLeg", lul,
                new Vector3(0f, -0.5f, 0f));
            Transform lf = Bone("LeftFoot", lll,
                new Vector3(0f, -0.4f, 0.15f));
            Transform rul = Bone("RightUpperLeg", hips,
                new Vector3(0.2f, -0.4f, 0f));
            Transform rll = Bone("RightLowerLeg", rul,
                new Vector3(0f, -0.5f, 0f));
            Transform rf = Bone("RightFoot", rll,
                new Vector3(0f, -0.4f, 0.15f));
            return new RagdollBipedReferences
            {
                hips = hips, spine = spine, chest = chest, head = head,
                leftUpperArm = lua, leftLowerArm = lla, leftHand = lh,
                rightUpperArm = rua, rightLowerArm = rla, rightHand = rh,
                leftUpperLeg = lul, leftLowerLeg = lll, leftFoot = lf,
                rightUpperLeg = rul, rightLowerLeg = rll, rightFoot = rf
            };
        }

        T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        static Transform Bone(string name, Transform parent, Vector3 position)
        {
            Transform value = new GameObject(name).transform;
            value.SetParent(parent, false);
            value.localPosition = position;
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
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod("Add", BindingFlags.Instance
                | BindingFlags.Public, null,
                new[] { typeof(BoneName), typeof(ConfigurableJoint) }, null);
            add.Invoke(dictionary, new object[] { root, rootJoint });
            add.Invoke(dictionary, new object[] { child, childJoint });
            return dictionary;
        }

        static void SetField(object owner, string name, object value)
        {
            owner.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        }

        static Vector3 ColliderDimensions(Collider collider)
        {
            BoxCollider box = collider as BoxCollider;
            if (box) return box.size;
            CapsuleCollider capsule = collider as CapsuleCollider;
            if (capsule)
            {
                Vector3 size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = capsule.height;
                return size;
            }
            SphereCollider sphere = (SphereCollider)collider;
            return Vector3.one * sphere.radius * 2f;
        }

        static Vector3 ColliderCenter(Collider collider)
        {
            BoxCollider box = collider as BoxCollider;
            if (box) return box.center;
            CapsuleCollider capsule = collider as CapsuleCollider;
            if (capsule) return capsule.center;
            return ((SphereCollider)collider).center;
        }

        static void AssertFinitePositive(Vector3 value, string label)
        {
            AssertFinite(value, label);
            Assert.That(value.x, Is.GreaterThan(0f), label);
            Assert.That(value.y, Is.GreaterThan(0f), label);
            Assert.That(value.z, Is.GreaterThan(0f), label);
        }

        static void AssertFinite(Vector3 value, string label)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x),
                Is.False, label);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y),
                Is.False, label);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z),
                Is.False, label);
        }

        static void AssertColliderCommon(
            Collider collider,
            PhysicsMaterial material,
            int layer)
        {
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.enabled, Is.False);
            Assert.That(collider.isTrigger, Is.True);
            Assert.That(collider.sharedMaterial, Is.SameAs(material));
            Assert.That(collider.gameObject.layer, Is.EqualTo(layer));
        }

        static void AssertHierarchyLayer(Transform root, int layer)
        {
            Assert.That(root.gameObject.layer, Is.EqualTo(layer));
            for (int index = 0; index < root.childCount; index++)
                AssertHierarchyLayer(root.GetChild(index), layer);
        }

        static void AssertCompleteSetup(RagdollSetupResult result)
        {
            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Animator, Is.Not.Null);
            Assert.That(result.Muscles, Is.Not.Null);
            Assert.That(result.Simulation, Is.Not.Null);
            Assert.That(result.Behaviours, Is.Not.Null);
            Assert.That(result.Collisions, Is.Not.Null);
            Assert.That(result.PuppetBehaviour, Is.Not.Null);
        }

        static void AssertPose(
            Rigidbody body,
            Vector3 position,
            Quaternion rotation)
        {
            Assert.That(Vector3.Distance(body.position, position),
                Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(body.rotation, rotation),
                Is.LessThan(0.001f));
        }

        sealed class SetupFixture
        {
            public Transform Target;
            public RagdollDefinitionBindings Bindings;
            public RagdollAnimationProfile Profile;
        }

        sealed class ThrowingSetupFactory :
            RagdollRuntimeSetupService.IObjectFactory
        {
            public T AddComponent<T>(GameObject owner) where T : Component
            {
                if (typeof(T) == typeof(RagdollMuscleController))
                    throw new InvalidOperationException("injected setup failure");
                return owner.AddComponent<T>();
            }

            public GameObject CreateGameObject(string name)
            {
                return new GameObject(name);
            }

            public void Destroy(UnityEngine.Object value)
            {
                if (value) UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
