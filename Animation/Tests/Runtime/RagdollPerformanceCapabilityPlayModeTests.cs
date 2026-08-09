using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Direct performance-contract evidence for RootMotion's public performance page.
    /// Every case uses initialized dual-rig Puppets. H06 deliberately verifies an
    /// authored Hairibar reduction strategy: RootMotion recommends fewer muscles but
    /// does not publish a runtime LOD-removal algorithm.
    /// </summary>
    public sealed class RagdollPerformanceCapabilityPlayModeTests
    {
        readonly List<UnityEngine.Object> owned =
            new List<UnityEngine.Object>();
        bool ignoredBefore;
        bool projectileIgnoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(26, 27);
            projectileIgnoredBefore = Physics.GetIgnoreLayerCollision(0, 27);
            Physics.IgnoreLayerCollision(0, 27, false);
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(26, 27, ignoredBefore);
            Physics.IgnoreLayerCollision(0, 27, projectileIgnoredBefore);
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index])
                    UnityEngine.Object.DestroyImmediate(owned[index]);
            }
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator H01_SolverIterationsApplyToEveryRegisteredMuscle()
        {
            RuntimeRig rig = CreateRig(3, "H01");
            yield return null;

            rig.Settings.solverIterations = 7;
            rig.Settings.solverVelocityIterations = 3;
            rig.Settings.ApplySettings();

            Assert.That(rig.Bindings.BoneCount, Is.EqualTo(3));
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                Rigidbody body = rig.Bindings.GetBoneAt(index).Rigidbody;
                Assert.That(body.solverIterations, Is.EqualTo(7));
                Assert.That(body.solverVelocityIterations, Is.EqualTo(3));
            }

            rig.Settings.SetRuntimeSolverOverride(
                RagdollSolverQualitySettings.Create(
                    2,
                    1,
                    5f,
                    4f,
                    RigidbodyInterpolation.None,
                    CollisionDetectionMode.Discrete));
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                Rigidbody body = rig.Bindings.GetBoneAt(index).Rigidbody;
                Assert.That(body.solverIterations, Is.EqualTo(2));
                Assert.That(body.solverVelocityIterations, Is.EqualTo(1));
            }

            Assert.That(rig.Settings.ClearRuntimeSolverOverride(), Is.True);
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                Rigidbody body = rig.Bindings.GetBoneAt(index).Rigidbody;
                Assert.That(body.solverIterations, Is.EqualTo(7));
                Assert.That(body.solverVelocityIterations, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator H02_FiniteSolverLimitsRemainFiniteUnderPhysXLoad()
        {
            RuntimeRig rig = CreateRig(3, "H02");
            yield return null;

            rig.Settings.solverIterations = 1;
            rig.Settings.solverVelocityIterations = 1;
            rig.Settings.maxAngularVelocity = 2f;
            rig.Settings.maxDepenetrationVelocity = 1f;
            rig.Settings.ApplySettings();
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                Rigidbody body = rig.Bindings.GetBoneAt(index).Rigidbody;
                body.AddForce(new Vector3(200f, -100f, 75f), ForceMode.Impulse);
                body.AddTorque(new Vector3(80f, 120f, -60f), ForceMode.Impulse);
            }

            for (int step = 0; step < 20; step++)
                yield return new WaitForFixedUpdate();

            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                Rigidbody body = rig.Bindings.GetBoneAt(index).Rigidbody;
                AssertFinite(body.position);
                AssertFinite(body.rotation);
                AssertFinite(body.linearVelocity);
                AssertFinite(body.angularVelocity);
                AssertFinite(body.inertiaTensor);
                Assert.That(body.inertiaTensor.x, Is.GreaterThan(0f));
                Assert.That(body.inertiaTensor.y, Is.GreaterThan(0f));
                Assert.That(body.inertiaTensor.z, Is.GreaterThan(0f));
                Assert.That(body.maxAngularVelocity, Is.EqualTo(2f));
                Assert.That(body.maxDepenetrationVelocity, Is.EqualTo(1f));
                Assert.That(body.solverIterations, Is.GreaterThan(0));
                Assert.That(body.solverVelocityIterations, Is.GreaterThan(0));
            }
        }

        [UnityTest]
        public IEnumerator H03_QualityLevelsSelectActiveKinematicDisabledAndRestore()
        {
            RuntimeRig rig = CreateRig(3, "H03");
            RagdollPhysicsQualityProfile quality =
                ScriptableObject.CreateInstance<RagdollPhysicsQualityProfile>();
            owned.Add(quality);
            RagdollSolverQualitySettings reducedSolver =
                RagdollSolverQualitySettings.Create(
                    2,
                    1,
                    5f,
                    4f,
                    RigidbodyInterpolation.None,
                    CollisionDetectionMode.Discrete);
            SetField(quality, "levels", new[]
            {
                new RagdollPhysicsQualityLevel(
                    "Active",
                    0f,
                    RagdollSimulationMode.Active,
                    0.04f,
                    true,
                    default(RagdollSolverQualitySettings)),
                new RagdollPhysicsQualityLevel(
                    "Reduced Active",
                    10f,
                    RagdollSimulationMode.Active,
                    0.04f,
                    false,
                    reducedSolver),
                new RagdollPhysicsQualityLevel(
                    "Kinematic",
                    20f,
                    RagdollSimulationMode.Kinematic,
                    0.04f,
                    false,
                    reducedSolver),
                new RagdollPhysicsQualityLevel(
                    "Disabled",
                    30f,
                    RagdollSimulationMode.Disabled,
                    0.04f,
                    true,
                    default(RagdollSolverQualitySettings))
            });
            GameObject budgetOwner = new GameObject("H03 Quality Budget");
            owned.Add(budgetOwner);
            RagdollPhysicsQualityBudget budget =
                budgetOwner.AddComponent<RagdollPhysicsQualityBudget>();
            budget.MaximumActiveRagdolls = 1;
            RagdollPhysicsQualityController controller =
                rig.Result.Animator.gameObject.AddComponent<
                    RagdollPhysicsQualityController>();
            SetField(controller, "profile", quality);
            SetField(controller, "automaticDistance", false);
            SetField(controller, "budget", budget);
            SetField(controller, "budgetFallbackLevel", 2);
            yield return null;

            Assert.That(controller.IsInitialized, Is.True);
            controller.SetManualLevel(0);
            controller.RefreshNow();
            budget.EvaluateNow();
            yield return WaitForMode(rig.Result.Simulation,
                RagdollSimulationMode.Active);
            AssertBodies(rig, true, true);

            budget.MaximumActiveRagdolls = 0;
            budget.EvaluateNow();
            yield return WaitForMode(rig.Result.Simulation,
                RagdollSimulationMode.Kinematic);
            Assert.That(controller.BudgetApproved, Is.False);
            AssertBodies(rig, true, false);

            budget.MaximumActiveRagdolls = 1;
            budget.EvaluateNow();
            yield return WaitForMode(rig.Result.Simulation,
                RagdollSimulationMode.Active);
            Assert.That(controller.BudgetApproved, Is.True);
            AssertBodies(rig, true, true);

            controller.SetManualLevel(2);
            controller.RefreshNow();
            yield return WaitForMode(rig.Result.Simulation,
                RagdollSimulationMode.Kinematic);
            AssertBodies(rig, true, false);

            controller.SetManualLevel(3);
            controller.RefreshNow();
            yield return WaitForMode(rig.Result.Simulation,
                RagdollSimulationMode.Disabled);
            Assert.That(rig.Result.Puppet.gameObject.activeSelf, Is.False);

            controller.SetManualLevel(0);
            controller.RefreshNow();
            yield return WaitForMode(rig.Result.Simulation,
                RagdollSimulationMode.Active);
            AssertBodies(rig, true, true);
            Assert.That(rig.Settings.HasRuntimeSolverOverride, Is.False);
        }

        [UnityTest]
        public IEnumerator H04_CollisionThresholdAndBudgetBoundSaturatedDispatch()
        {
            RuntimeRig rig = CreateRig(3, "H04");
            yield return null;
            RagdollPuppetBehaviour puppet = rig.Result.PuppetBehaviour;
            RagdollCollisionHub hub = rig.Result.Collisions;
            RagdollBoneHandle handle = rig.Bindings.GetHandleAt(0);
            int observed = 0;
            int accepted = 0;
            Dictionary<float, int> observedByStep =
                new Dictionary<float, int>();
            Dictionary<float, int> acceptedByStep =
                new Dictionary<float, int>();
            puppet.CollisionLayers = -1;
            puppet.CollisionThreshold = 0f;
            puppet.MaximumCollisionsPerFixedStep = 1;
            hub.MaxEventsPerFixedStep = 32;
            puppet.CollisionObserved += collisionEvent =>
            {
                observed++;
                Increment(observedByStep, collisionEvent.FixedTime);
            };
            puppet.CollisionAccepted += collisionEvent =>
            {
                accepted++;
                Increment(acceptedByStep, collisionEvent.FixedTime);
            };

            Rigidbody rootBody = rig.Bindings.GetBone(handle).Rigidbody;
            rootBody.useGravity = false;
            rootBody.constraints = RigidbodyConstraints.FreezeAll;
            rig.Result.Animator.MasterMappingWeight = 0f;
            rig.Result.Animator.MasterPinWeight = 0f;
            rig.Result.Animator.MasterMuscleWeight = 0f;
            Physics.SyncTransforms();

            // Eight real dynamic bodies strike the root during the same PhysX step.
            // The relays therefore deliver genuine Collision objects and impulses.
            for (int index = 0; index < 8; index++)
            {
                float angle = index * Mathf.PI * 2f / 8f;
                Vector3 direction = new Vector3(
                    Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                CreateProjectile(
                    rootBody.position + direction * 0.65f,
                    -direction * 18f);
            }

            for (int step = 0; step < 20; step++)
                yield return new WaitForFixedUpdate();
            Assert.That(observed, Is.GreaterThanOrEqualTo(2),
                "The saturated PhysX contact fixture produced fewer than two callbacks.");
            Assert.That(accepted, Is.GreaterThanOrEqualTo(1));
            bool saturatedStep = false;
            foreach (KeyValuePair<float, int> step in observedByStep)
            {
                int acceptedInStep;
                acceptedByStep.TryGetValue(step.Key, out acceptedInStep);
                Assert.That(acceptedInStep, Is.LessThanOrEqualTo(1),
                    "More than one contact was accepted at fixedTime " + step.Key);
                if (step.Value > 1 && acceptedInStep == 1)
                    saturatedStep = true;
            }
            Assert.That(saturatedStep, Is.True,
                "No PhysX timestamp saturated the one-contact BehaviourPuppet budget.");

            yield return new WaitForFixedUpdate();
            int observedBeforeThreshold = observed;
            int acceptedBeforeThreshold = accepted;
            puppet.MaximumCollisionsPerFixedStep = 30;
            puppet.CollisionThreshold = 1000000f;
            CreateProjectile(rootBody.position + Vector3.left * 0.65f,
                Vector3.right * 18f);
            for (int step = 0; step < 20 && observed == observedBeforeThreshold;
                step++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(observed, Is.GreaterThan(observedBeforeThreshold));
            Assert.That(accepted, Is.EqualTo(acceptedBeforeThreshold));
            Assert.That(puppet.LastCollisionRejectionReason,
                Is.EqualTo(RagdollPuppetCollisionRejectionReason.BelowThreshold));
        }

        [UnityTest]
        public IEnumerator H05_PublicFlatAndTreeLayoutsIncludeRootAndPreserveTopology()
        {
            RuntimeRig rig = CreateRig(3, "H05");
            yield return null;
            Transform authoredContainer = rig.Bindings.Root.Transform.parent;
            Rigidbody[] connected = new Rigidbody[rig.Bindings.BoneCount];
            Vector3[] positions = new Vector3[rig.Bindings.BoneCount];
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                RagdollBone bone = rig.Bindings.GetBoneAt(index);
                connected[index] = bone.Joint.connectedBody;
                positions[index] = bone.Transform.position;
            }

            Assert.That(rig.Result.Animator.HierarchyIsFlat(), Is.False);
            rig.Result.Animator.FlattenHierarchy();
            Assert.That(rig.Result.Animator.HierarchyIsFlat(), Is.True);
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                RagdollBone bone = rig.Bindings.GetBoneAt(index);
                Assert.That(bone.Transform.parent, Is.SameAs(authoredContainer));
                Assert.That(bone.Joint.connectedBody, Is.SameAs(connected[index]));
                Assert.That(Vector3.Distance(bone.Transform.position, positions[index]),
                    Is.LessThan(0.0001f));
            }

            rig.Result.Animator.TreeHierarchy();
            Assert.That(rig.Result.Animator.HierarchyIsFlat(), Is.False);
            Assert.That(rig.Bindings.Root.Transform.parent,
                Is.SameAs(authoredContainer));
            for (int index = 1; index < rig.Bindings.BoneCount; index++)
            {
                RagdollBone bone = rig.Bindings.GetBoneAt(index);
                Assert.That(bone.Transform.parent,
                    Is.SameAs(rig.Bindings.GetBoneAt(index - 1).Transform));
                Assert.That(bone.Joint.connectedBody, Is.SameAs(connected[index]));
                Assert.That(Vector3.Distance(bone.Transform.position, positions[index]),
                    Is.LessThan(0.0001f));
            }
        }

        [UnityTest]
        public IEnumerator H06_AuthoredMuscleReductionKeepsValidRootToLeafTopology()
        {
            RuntimeRig full = CreateRig(3, "H06 Full");
            RuntimeRig reduced = CreateRig(2, "H06 Reduced");
            yield return null;

            Assert.That(reduced.Bindings.BoneCount,
                Is.LessThan(full.Bindings.BoneCount));
            Assert.That(reduced.Bindings.Root.IsRoot, Is.True);
            for (int index = 0; index < reduced.Bindings.BoneCount; index++)
            {
                RagdollBoneHandle handle = reduced.Bindings.GetHandleAt(index);
                Assert.That(reduced.Bindings.Topology.Contains(handle), Is.True);
                if (index == 0) continue;
                RagdollBoneHandle parent;
                Assert.That(reduced.Bindings.Topology.TryGetParent(handle, out parent),
                    Is.True);
                Assert.That(parent, Is.EqualTo(
                    reduced.Bindings.GetHandleAt(index - 1)));
                Assert.That(reduced.Bindings.GetBone(handle).Joint.connectedBody,
                    Is.SameAs(reduced.Bindings.GetBone(parent).Rigidbody));
            }
            Assert.That(reduced.Result.Animator.Initiated, Is.True);
            Assert.That(reduced.Result.Behaviours.Context.Pairs.Count,
                Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator H08_ProfilerCountersSampleRealPuppetExecution()
        {
            RuntimeRig first = CreateRig(3, "H08 A");
            RuntimeRig second = CreateRig(3, "H08 B");
            using (ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal,
                "CPU Main Thread Frame Time",
                32))
            using (ProfilerRecorder gcAllocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                32))
            using (ProfilerRecorder totalMemory = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "Total Reserved Memory",
                32))
            {
                for (int frame = 0; frame < 12; frame++)
                {
                    first.Target.transform.position += Vector3.right * 0.001f;
                    second.Target.transform.position -= Vector3.right * 0.001f;
                    yield return null;
                }

                Assert.That(mainThread.Valid, Is.True);
                Assert.That(gcAllocated.Valid, Is.True);
                Assert.That(totalMemory.Valid, Is.True);
                Assert.That(mainThread.Count, Is.GreaterThan(0));
                Assert.That(gcAllocated.Count, Is.GreaterThan(0));
                Assert.That(totalMemory.Count, Is.GreaterThan(0));
                Assert.That(mainThread.LastValue, Is.GreaterThanOrEqualTo(0));
                Assert.That(gcAllocated.LastValue, Is.GreaterThanOrEqualTo(0));
                Assert.That(totalMemory.LastValue, Is.GreaterThan(0));
            }
        }

        RuntimeRig CreateRig(int muscleCount, string name)
        {
            GameObject owner = new GameObject(name);
            owned.Add(owner);
            GameObject puppet = new GameObject("Puppet");
            puppet.transform.SetParent(owner.transform, false);
            puppet.SetActive(false);

            BoneName[] names = new BoneName[muscleCount];
            ConfigurableJoint[] joints = new ConfigurableJoint[muscleCount];
            Rigidbody previous = null;
            Transform parent = puppet.transform.parent;
            for (int index = 0; index < muscleCount; index++)
            {
                GameObject boneObject = index == 0
                    ? puppet
                    : new GameObject("Bone" + index);
                if (index > 0)
                {
                    boneObject.transform.SetParent(parent, false);
                    boneObject.transform.localPosition = Vector3.up;
                }
                Rigidbody body = boneObject.AddComponent<Rigidbody>();
                body.mass = 1f;
                ConfigurableJoint joint =
                    boneObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = previous;
                boneObject.AddComponent<BoxCollider>().size =
                    new Vector3(0.3f, 0.7f, 0.3f);
                names[index] = new BoneName(index == 0 ? "Root" : "Bone" + index);
                joints[index] = joint;
                previous = body;
                parent = boneObject.transform;
            }

            RagdollDefinition definition =
                ScriptableObject.CreateInstance<RagdollDefinition>();
            owned.Add(definition);
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", names[0]);
            SetField(definition, "bones", names);
            RagdollDefinitionBindings bindings =
                puppet.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(names, joints));
            RagdollSettings settings = ConfigurePhysicalSettings(
                puppet,
                definition,
                names);
            puppet.SetActive(true);
            Assert.That(bindings.IsInitialized, Is.True);

            GameObject target = new GameObject("Puppet");
            target.transform.SetParent(owner.transform, false);
            Transform targetParent = target.transform;
            for (int index = 1; index < muscleCount; index++)
            {
                GameObject targetBone = new GameObject("Bone" + index);
                targetBone.transform.SetParent(targetParent, false);
                targetBone.transform.localPosition = Vector3.up;
                targetParent = targetBone.transform;
            }
            target.AddComponent<UnityEngine.Animation>().animatePhysics = true;

            RagdollAnimationProfile profile =
                ScriptableObject.CreateInstance<RagdollAnimationProfile>();
            owned.Add(profile);
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConfigureSeparated(
                    target.transform,
                    bindings,
                    profile,
                    26,
                    27);
            Assert.That(result.Succeeded, Is.True, result.Error);
            return new RuntimeRig
            {
                Result = result,
                Target = target,
                Bindings = bindings,
                Settings = settings
            };
        }

        static void Increment(Dictionary<float, int> counts, float key)
        {
            int value;
            counts.TryGetValue(key, out value);
            counts[key] = value + 1;
        }

        Rigidbody CreateProjectile(Vector3 position, Vector3 velocity)
        {
            GameObject projectile = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            projectile.name = "H04 PhysX Projectile";
            projectile.layer = 0;
            projectile.transform.position = position;
            projectile.transform.localScale = Vector3.one * 0.24f;
            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            owned.Add(projectile);
            return body;
        }

        static object CreateBindings(BoneName[] names, ConfigurableJoint[] joints)
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
            for (int index = 0; index < names.Length; index++)
                add.Invoke(dictionary, new object[] { names[index], joints[index] });
            return dictionary;
        }

        RagdollSettings ConfigurePhysicalSettings(
            GameObject puppet,
            RagdollDefinition definition,
            BoneName[] names)
        {
            RagdollPowerProfile power =
                ScriptableObject.CreateInstance<RagdollPowerProfile>();
            owned.Add(power);
            SetField(power, "definition", definition);
            SetField(power, "_isValid", true);
            SetField(power, "settings", CreateProfileDictionary(
                typeof(RagdollPowerProfile),
                "PowerSettingsDictionary",
                typeof(PowerSetting),
                names,
                index => PowerSetting.Powered));

            RagdollWeightDistribution weights =
                ScriptableObject.CreateInstance<RagdollWeightDistribution>();
            owned.Add(weights);
            SetField(weights, "definition", definition);
            SetField(weights, "_isValid", true);
            float uniform = 1f / names.Length;
            SetField(weights, "factors", CreateProfileDictionary(
                typeof(RagdollWeightDistribution),
                "WeightDistributionDictionary",
                typeof(float),
                names,
                index => index == names.Length - 1
                    ? 1f - uniform * (names.Length - 1)
                    : uniform));

            RagdollSettings settings = puppet.AddComponent<RagdollSettings>();
            settings.useGravity = false;
            SetField(settings, "_powerProfile", power);
            SetField(settings, "_weightDistribution", weights);
            return settings;
        }

        static object CreateProfileDictionary(
            Type ownerType,
            string nestedTypeName,
            Type valueType,
            BoneName[] names,
            Func<int, object> valueAt)
        {
            Type type = ownerType.GetNestedType(
                nestedTypeName,
                BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            MethodInfo add = type.GetMethod(
                "Add",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(BoneName), valueType },
                null);
            Assert.That(add, Is.Not.Null, nestedTypeName + ".Add");
            for (int index = 0; index < names.Length; index++)
                add.Invoke(dictionary, new[] { (object)names[index], valueAt(index) });
            return dictionary;
        }

        static IEnumerator WaitForMode(
            RagdollSimulationModeController controller,
            RagdollSimulationMode expected,
            float transitionDuration = 0.04f)
        {
            int maximumSteps = Mathf.CeilToInt(
                Mathf.Max(0f, transitionDuration) / Time.fixedDeltaTime) + 4;
            for (int step = 0; step < maximumSteps &&
                (controller.IsTransitioning || controller.CurrentMode != expected);
                step++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(controller.IsTransitioning, Is.False);
            Assert.That(controller.CurrentMode, Is.EqualTo(expected));
        }

        static void AssertBodies(RuntimeRig rig, bool active, bool dynamic)
        {
            Assert.That(rig.Result.Puppet.gameObject.activeSelf, Is.EqualTo(active));
            for (int index = 0; index < rig.Bindings.BoneCount; index++)
            {
                Rigidbody body = rig.Bindings.GetBoneAt(index).Rigidbody;
                Assert.That(body.isKinematic, Is.EqualTo(!dynamic));
            }
        }

        static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }

        static void AssertFinite(Quaternion value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
            Assert.That(float.IsNaN(value.w) || float.IsInfinity(value.w), Is.False);
        }

        static void SetField(object target, string fieldName, object value)
        {
            Type current = target.GetType();
            FieldInfo field = null;
            while (current != null && field == null)
            {
                field = current.GetField(
                    fieldName,
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.Public
                    | BindingFlags.DeclaredOnly);
                current = current.BaseType;
            }
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        sealed class RuntimeRig
        {
            internal RagdollSetupResult Result;
            internal GameObject Target;
            internal RagdollDefinitionBindings Bindings;
            internal RagdollSettings Settings;
        }
    }
}
