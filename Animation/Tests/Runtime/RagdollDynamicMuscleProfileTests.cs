using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollDynamicMuscleProfileTests
    {
        [Test]
        public void RuntimeBone_UsesResolverWithoutMutatingAuthoredAssignments()
        {
            using (ProfileRig rig = new ProfileRig())
            {
                RagdollBoneHandle ignored;
                string error;
                Assert.That(
                    rig.Bindings.TryAddRuntimeBinding(
                        rig.RuntimeName,
                        rig.RuntimeJoint,
                        out ignored,
                        out error),
                    Is.True,
                    error);

                RagdollMuscleProfileRuntime runtime;
                Assert.That(
                    rig.Profile.TryCreateRuntime(
                        rig.Bindings,
                        bone => bone == rig.RuntimeName
                            ? (RagdollMuscleGroup?)RagdollMuscleGroup.Prop
                            : null,
                        out runtime,
                        out error),
                    Is.True,
                    error);

                Assert.That(runtime.BoneCount, Is.EqualTo(3));
                Assert.That(runtime.GetGroup(2), Is.EqualTo(RagdollMuscleGroup.Prop));
                Assert.That(rig.Profile.BoneGroups.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void RemovedAuthoredBoneRules_AreIgnoredForCurrentGeneration()
        {
            using (ProfileRig rig = new ProfileRig())
            {
                string error;
                Assert.That(
                    rig.Profile.TrySynchronizeInternalCollisionIgnores(out error),
                    Is.True,
                    error);
                RagdollBone[] removed;
                Assert.That(
                    rig.Bindings.TryRemoveRuntimeSubtree(
                        rig.ChildJoint,
                        out removed,
                        out error),
                    Is.True,
                    error);

                RagdollInternalCollisionIgnoreRuntime runtime;
                Assert.That(
                    rig.Profile.TryCreateInternalCollisionRuntime(
                        rig.Bindings,
                        null,
                        out runtime,
                        out error),
                    Is.True,
                    error);

                Assert.That(runtime.BoneCount, Is.EqualTo(1));
                Assert.That(runtime.ForcedBonePairCount, Is.Zero);
                Assert.That(rig.Profile.InternalCollisionIgnores.Count, Is.EqualTo(2));
            }
        }

        sealed class ProfileRig : IDisposable
        {
            public BoneName RootName { get; } = new BoneName("Root");
            public BoneName ChildName { get; } = new BoneName("Child");
            public BoneName RuntimeName { get; } = new BoneName("RuntimeProp");
            public RagdollDefinitionBindings Bindings { get; }
            public RagdollMuscleProfile Profile { get; }
            public ConfigurableJoint ChildJoint { get; }
            public ConfigurableJoint RuntimeJoint { get; }

            readonly GameObject root;
            readonly GameObject child;
            readonly GameObject runtime;
            readonly RagdollDefinition definition;

            public ProfileRig()
            {
                root = new GameObject("Dynamic Profile Root");
                root.SetActive(false);
                child = new GameObject("Dynamic Profile Child");
                child.transform.SetParent(root.transform, false);
                runtime = new GameObject("Dynamic Profile Runtime");
                runtime.transform.SetParent(child.transform, false);

                Rigidbody rootBody = root.AddComponent<Rigidbody>();
                ConfigurableJoint rootJoint = root.AddComponent<ConfigurableJoint>();
                root.AddComponent<BoxCollider>();

                Rigidbody childBody = child.AddComponent<Rigidbody>();
                ChildJoint = child.AddComponent<ConfigurableJoint>();
                ChildJoint.connectedBody = rootBody;
                child.AddComponent<BoxCollider>();

                runtime.AddComponent<Rigidbody>();
                RuntimeJoint = runtime.AddComponent<ConfigurableJoint>();
                RuntimeJoint.connectedBody = childBody;
                runtime.AddComponent<BoxCollider>();

                definition = ScriptableObject.CreateInstance<RagdollDefinition>();
                SetField(definition, "_isValid", true);
                SetField(definition, "_root", RootName);
                SetField(definition, "bones", new[] { RootName, ChildName });

                Bindings = root.AddComponent<RagdollDefinitionBindings>();
                SetField(Bindings, "_definition", definition);
                SetField(
                    Bindings,
                    "bindings",
                    CreateBindingsDictionary(rootJoint, ChildJoint));
                if (Application.isPlaying)
                {
                    root.SetActive(true);
                }
                else
                {
                    InvokeNonPublic(Bindings, "OnValidate");
                }
                if (!Bindings.IsInitialized)
                {
                    throw new InvalidOperationException(
                        "The dynamic profile test registry failed to initialize.");
                }

                Profile = ScriptableObject.CreateInstance<RagdollMuscleProfile>();
                SetField(Profile, "definition", definition);
                string error;
                if (!Profile.TrySynchronizeAssignments(out error))
                {
                    throw new InvalidOperationException(error);
                }
            }

            public void Dispose()
            {
                DestroyObject(Profile);
                DestroyObject(definition);
                DestroyObject(root);
            }

            object CreateBindingsDictionary(
                ConfigurableJoint rootJoint,
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
                add.Invoke(dictionary, new object[] { RootName, rootJoint });
                add.Invoke(dictionary, new object[] { ChildName, childJoint });
                return dictionary;
            }

            static void InvokeNonPublic(object target, string methodName)
            {
                MethodInfo method = target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(target, null);
            }

            static void SetField(object target, string name, object value)
            {
                FieldInfo field = target.GetType().GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(target, value);
            }

            static void DestroyObject(UnityEngine.Object value)
            {
                if (!value) return;
                if (Application.isPlaying) UnityEngine.Object.Destroy(value);
                else UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
