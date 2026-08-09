using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hairibar.Ragdoll.Animation.Tests
{
    /// <summary>
    /// Executable evidence for RootMotion's documented IK-before-physics and
    /// IK-after-mapping boundaries. These cases use an initialized physical Puppet;
    /// they do not call the scheduler or matching helpers directly.
    /// </summary>
    public sealed class RagdollIKClosurePlayModeTests
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        bool ignoredBefore;

        [SetUp]
        public void SetUp()
        {
            ignoredBefore = Physics.GetIgnoreLayerCollision(30, 31);
        }

        [TearDown]
        public void TearDown()
        {
            Physics.IgnoreLayerCollision(30, 31, ignoredBefore);
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index]) UnityEngine.Object.DestroyImmediate(owned[index]);
            owned.Clear();
        }

        [UnityTest]
        public IEnumerator G01_PrePhysicsPoseModifierIsConsumedByMatching()
        {
            RuntimeRig rig = CreateRuntimeWithPoseProbe();
            yield return null;

            RagdollAnimator.AnimatedPair pair =
                rig.Result.Behaviours.Context.Pairs[0];
            Vector3 unmodified = pair.currentPose.worldPosition;
            rig.Modifier.Offset = new Vector3(0.75f, 0.25f, -0.5f);
            rig.Modifier.ModifyCount = 0;
            rig.Probe.ResetProbe();

            SimulateOneManualStep(rig.Result.Animator);

            Assert.That(rig.Modifier.ModifyCount, Is.EqualTo(1));
            Assert.That(rig.Probe.Observed, Is.True,
                "The matching stage did not consume a pose after the modifier ran.");
            Assert.That(Vector3.Distance(
                rig.Probe.PoseSeenByMatching,
                unmodified + rig.Modifier.Offset), Is.LessThan(0.0001f));
            Assert.That(rig.Probe.Sequence, Is.EqualTo(new[]
            {
                "pose-modifier", "matching"
            }));
        }

        [UnityTest]
        public IEnumerator G03_IKSchedulerExecutesBothPhasesInStableOrderAndIsolatesFailure()
        {
            RuntimeRig rig = CreateRuntimeWithPoseProbe();
            yield return null;
            rig.Probe.ResetProbe();

            GameObject beforeObject = Own(new GameObject("Before Physics IK"));
            beforeObject.SetActive(false);
            OrderedSolver beforeA = beforeObject.AddComponent<OrderedSolver>();
            ThrowingSolver beforeFailure = beforeObject.AddComponent<ThrowingSolver>();
            OrderedSolver beforeB = beforeObject.AddComponent<OrderedSolver>();
            beforeA.Configure("before-a", rig.Probe.Sequence);
            beforeFailure.Configure("before-failure", rig.Probe.Sequence);
            beforeB.Configure("before-b", rig.Probe.Sequence);
            RagdollIKScheduler before = beforeObject.AddComponent<RagdollIKScheduler>();
            before.Configure(rig.Result.Animator, RagdollIKSolvePhase.BeforePhysics,
                new MonoBehaviour[] { beforeA, beforeFailure, beforeB });

            GameObject afterObject = Own(new GameObject("After Physics IK"));
            afterObject.SetActive(false);
            OrderedSolver afterA = afterObject.AddComponent<OrderedSolver>();
            OrderedSolver afterB = afterObject.AddComponent<OrderedSolver>();
            afterA.Configure("after-a", rig.Probe.Sequence);
            afterB.Configure("after-b", rig.Probe.Sequence);
            RagdollIKScheduler after = afterObject.AddComponent<RagdollIKScheduler>();
            after.Configure(rig.Result.Animator, RagdollIKSolvePhase.AfterPhysics,
                new MonoBehaviour[] { afterA, afterB });

            LogAssert.Expect(LogType.Exception,
                new Regex("expected scheduler isolation failure"));
            beforeObject.SetActive(true);
            afterObject.SetActive(true);
            SimulateOneManualStep(rig.Result.Animator);

            Assert.That(rig.Probe.Sequence, Is.EqualTo(new[]
            {
                "before-a", "before-failure", "before-b", "pose-modifier",
                "matching",
                "after-a", "after-b"
            }));
            Assert.That(beforeA.AutomaticUpdates, Is.False);
            Assert.That(beforeFailure.AutomaticUpdates, Is.False);
            Assert.That(beforeB.AutomaticUpdates, Is.False);
            Assert.That(afterA.AutomaticUpdates, Is.False);
            Assert.That(afterB.AutomaticUpdates, Is.False);

            beforeObject.SetActive(false);
            afterObject.SetActive(false);
            Assert.That(beforeA.AutomaticUpdates, Is.True);
            Assert.That(beforeFailure.AutomaticUpdates, Is.True);
            Assert.That(beforeB.AutomaticUpdates, Is.True);
            Assert.That(afterA.AutomaticUpdates, Is.True);
            Assert.That(afterB.AutomaticUpdates, Is.True);
        }

        RuntimeRig CreateRuntimeWithPoseProbe()
        {
            BoneName rootName = new BoneName("Root");
            GameObject puppet = Own(new GameObject("IK Physical Puppet"));
            puppet.SetActive(false);
            Rigidbody body = puppet.AddComponent<Rigidbody>();
            body.useGravity = false;
            ConfigurableJoint joint = puppet.AddComponent<ConfigurableJoint>();
            puppet.AddComponent<BoxCollider>();

            RagdollDefinition definition =
                Own(ScriptableObject.CreateInstance<RagdollDefinition>());
            SetField(definition, "_isValid", true);
            SetField(definition, "_root", rootName);
            SetField(definition, "bones", new[] { rootName });
            RagdollDefinitionBindings bindings =
                puppet.AddComponent<RagdollDefinitionBindings>();
            SetField(bindings, "_definition", definition);
            SetField(bindings, "bindings", CreateBindings(rootName, joint));
            ConfigurePhysicalSettings(puppet, definition, rootName);
            puppet.SetActive(true);

            GameObject target = Own(new GameObject("IK Physical Puppet"));
            PoseOffsetModifier modifier = target.AddComponent<PoseOffsetModifier>();
            MatchingPoseProbe probe = target.AddComponent<MatchingPoseProbe>();
            modifier.Trace = probe.Sequence;
            RagdollAnimationProfile profile =
                Own(ScriptableObject.CreateInstance<RagdollAnimationProfile>());
            RagdollSetupResult result =
                RagdollRuntimeSetupService.ConvertHierarchyDirectlyToPuppet(
                    target.transform, bindings, profile, 30, 31);
            Assert.That(result.Succeeded, Is.True, result.Error);
            bindings.Root.PowerSetting = PowerSetting.Powered;
            body.isKinematic = false;
            return new RuntimeRig
            {
                Result = result,
                Modifier = modifier,
                Probe = probe
            };
        }

        void ConfigurePhysicalSettings(
            GameObject puppet,
            RagdollDefinition definition,
            BoneName root)
        {
            RagdollPowerProfile power =
                Own(ScriptableObject.CreateInstance<RagdollPowerProfile>());
            SetField(power, "definition", definition);
            SetField(power, "_isValid", true);
            SetField(power, "settings", CreateProfileDictionary(
                typeof(RagdollPowerProfile), "PowerSettingsDictionary",
                typeof(PowerSetting), root, PowerSetting.Powered));

            RagdollWeightDistribution weights =
                Own(ScriptableObject.CreateInstance<RagdollWeightDistribution>());
            SetField(weights, "definition", definition);
            SetField(weights, "_isValid", true);
            SetField(weights, "factors", CreateProfileDictionary(
                typeof(RagdollWeightDistribution), "WeightDistributionDictionary",
                typeof(float), root, 1f));

            RagdollSettings settings = puppet.AddComponent<RagdollSettings>();
            settings.useGravity = false;
            SetField(settings, "_powerProfile", power);
            SetField(settings, "_weightDistribution", weights);
        }

        static void SimulateOneManualStep(RagdollAnimator animator)
        {
            SimulationMode previous = Physics.simulationMode;
            animator.enabled = false;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                animator.PrepareManualSimulation(Time.fixedDeltaTime);
                Physics.Simulate(Time.fixedDeltaTime);
                animator.CompleteManualSimulation();
            }
            finally
            {
                Physics.simulationMode = previous;
                animator.enabled = true;
            }
        }

        T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        static object CreateBindings(BoneName root, ConfigurableJoint joint)
        {
            Type type = typeof(RagdollDefinitionBindings).GetNestedType(
                "BoneJointBindingsDictionary", BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            type.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(BoneName), typeof(ConfigurableJoint) }, null)
                .Invoke(dictionary, new object[] { root, joint });
            return dictionary;
        }

        static object CreateProfileDictionary(
            Type owner,
            string nestedTypeName,
            Type valueType,
            BoneName key,
            object value)
        {
            Type type = owner.GetNestedType(nestedTypeName, BindingFlags.NonPublic);
            object dictionary = Activator.CreateInstance(type, true);
            type.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(BoneName), valueType }, null)
                .Invoke(dictionary, new[] { (object)key, value });
            return dictionary;
        }

        static void SetField(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance
                    | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        sealed class RuntimeRig
        {
            internal RagdollSetupResult Result;
            internal PoseOffsetModifier Modifier;
            internal MatchingPoseProbe Probe;
        }

        sealed class PoseOffsetModifier : MonoBehaviour, ITargetPoseModifier,
            IOrderedRagdollModifier
        {
            internal Vector3 Offset;
            internal int ModifyCount;
            internal List<string> Trace;
            public RagdollModifierStage Stage =>
                RagdollModifierStage.GameplayOverride;
            public int Priority => 0;
            public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs) { }
            public void ModifyPose(IEnumerable<RagdollAnimator.AnimatedPair> pairs)
            {
                ModifyCount++;
                Trace.Add("pose-modifier");
                foreach (RagdollAnimator.AnimatedPair pair in pairs)
                    pair.currentPose.worldPosition += Offset;
            }
        }

        sealed class MatchingPoseProbe : MonoBehaviour, IBoneProfileModifier,
            IOrderedRagdollModifier
        {
            internal readonly List<string> Sequence = new List<string>();
            internal bool Observed;
            internal Vector3 PoseSeenByMatching;
            public RagdollModifierStage Stage =>
                RagdollModifierStage.GameplayOverride;
            public int Priority => 100;
            public void Initialize(IEnumerable<RagdollAnimator.AnimatedPair> pairs) { }
            public void Modify(ref BoneProfile profile,
                RagdollAnimator.AnimatedPair pair, float deltaTime)
            {
                if (Observed) return;
                Observed = true;
                PoseSeenByMatching = pair.currentPose.worldPosition;
                Sequence.Add("matching");
            }
            internal void ResetProbe()
            {
                Observed = false;
                PoseSeenByMatching = default;
                Sequence.Clear();
            }
        }

        class OrderedSolver : MonoBehaviour, IRagdollIKSolver
        {
            string label;
            List<string> trace;
            public bool IsSolverEnabled => enabled;
            public bool AutomaticUpdates { get; set; } = true;
            internal void Configure(string value, List<string> sequence)
            {
                label = value;
                trace = sequence;
            }
            public virtual void Solve() => trace.Add(label);
        }

        sealed class ThrowingSolver : OrderedSolver
        {
            public override void Solve()
            {
                base.Solve();
                throw new InvalidOperationException(
                    "expected scheduler isolation failure");
            }
        }
    }
}
