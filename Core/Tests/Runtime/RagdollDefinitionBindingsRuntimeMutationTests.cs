using System;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Tests
{
    public sealed class RagdollDefinitionBindingsRuntimeMutationTests
    {
        [Test]
        public void Add_AppendsDeterministicallyAndInvalidatesOldHandles()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            using (RuntimeBone runtime = new RuntimeBone(rig.ChildBody))
            {
                RagdollBoneHandle oldChild;
                Assert.That(
                    rig.Bindings.TryGetBoneHandle(rig.ChildName, out oldChild),
                    Is.True);
                int oldGeneration = rig.Bindings.RegistryGeneration;

                RagdollBoneHandle added;
                string error;
                Assert.That(
                    rig.Bindings.TryAddRuntimeBinding(
                        runtime.Name,
                        runtime.Joint,
                        out added,
                        out error),
                    Is.True,
                    error);

                Assert.That(rig.Bindings.BoneCount, Is.EqualTo(3));
                Assert.That(rig.Bindings.RegistryGeneration, Is.Not.EqualTo(oldGeneration));
                Assert.That(added.Index, Is.EqualTo(2));
                Assert.That(rig.Bindings.GetBoneAt(2).Name, Is.EqualTo(runtime.Name));
                Assert.That(rig.Bindings.TryGetBone(oldChild, out _), Is.False);

                RagdollBoneHandle parent;
                Assert.That(
                    rig.Bindings.Topology.TryGetParent(added, out parent),
                    Is.True);
                Assert.That(rig.Bindings.GetBone(parent).Name, Is.EqualTo(rig.ChildName));
            }
        }

        [Test]
        public void RemoveSubtree_RemovesAuthoredAndRuntimeDescendants()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            using (RuntimeBone runtime = new RuntimeBone(rig.ChildBody))
            {
                RagdollBoneHandle ignored;
                string error;
                Assert.That(
                    rig.Bindings.TryAddRuntimeBinding(
                        runtime.Name,
                        runtime.Joint,
                        out ignored,
                        out error),
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

                Assert.That(removed.Length, Is.EqualTo(2));
                Assert.That(removed[0].Name, Is.EqualTo(rig.ChildName));
                Assert.That(removed[1].Name, Is.EqualTo(runtime.Name));
                Assert.That(rig.Bindings.BoneCount, Is.EqualTo(1));
                Assert.That(rig.Bindings.Root.Name, Is.EqualTo(rig.RootName));
            }
        }

        [Test]
        public void RestoreSnapshot_ReinstatesExactGenerationAndMembership()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            using (RuntimeBone runtime = new RuntimeBone(rig.ChildBody))
            {
                RagdollDefinitionBindings.RuntimeRegistrySnapshot snapshot =
                    rig.Bindings.CaptureRuntimeRegistry();
                int generation = rig.Bindings.RegistryGeneration;
                RagdollBoneHandle childHandle;
                rig.Bindings.TryGetBoneHandle(rig.ChildName, out childHandle);

                RagdollBoneHandle ignored;
                string error;
                Assert.That(
                    rig.Bindings.TryAddRuntimeBinding(
                        runtime.Name,
                        runtime.Joint,
                        out ignored,
                        out error),
                    Is.True,
                    error);

                rig.Bindings.RestoreRuntimeRegistry(snapshot);

                Assert.That(rig.Bindings.RegistryGeneration, Is.EqualTo(generation));
                Assert.That(rig.Bindings.BoneCount, Is.EqualTo(2));
                Assert.That(rig.Bindings.TryGetBone(runtime.Name, out _), Is.False);
                Assert.That(rig.Bindings.TryGetBone(childHandle, out RagdollBone child), Is.True);
                Assert.That(child.Name, Is.EqualTo(rig.ChildName));
            }
        }

        [Test]
        public void RuntimeNotification_DoesNotReuseAuthoredCreationEvent()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            {
                int authoredNotifications = 0;
                int runtimeNotifications = 0;
                Action authored = () => authoredNotifications++;
                Action runtime = () => runtimeNotifications++;

                rig.Bindings.SubscribeToOnBonesCreated(authored);
                authoredNotifications = 0;
                rig.Bindings.SubscribeToRuntimeHierarchyChanged(runtime);

                rig.Bindings.NotifyRuntimeHierarchyChanged();

                Assert.That(authoredNotifications, Is.Zero);
                Assert.That(runtimeNotifications, Is.EqualTo(1));
                rig.Bindings.UnsubscribeFromOnBonesCreated(authored);
                rig.Bindings.UnsubscribeFromRuntimeHierarchyChanged(runtime);
            }
        }

        [Test]
        public void DuplicateNameFailure_DoesNotMutateRegistry()
        {
            using (RagdollBindingsTestRig rig = new RagdollBindingsTestRig())
            using (RuntimeBone runtime = new RuntimeBone(rig.ChildBody))
            {
                int generation = rig.Bindings.RegistryGeneration;
                RagdollBoneHandle handle;
                string error;

                Assert.That(
                    rig.Bindings.TryAddRuntimeBinding(
                        rig.ChildName,
                        runtime.Joint,
                        out handle,
                        out error),
                    Is.False);

                Assert.That(error, Does.Contain("already exists"));
                Assert.That(handle, Is.EqualTo(RagdollBoneHandle.Invalid));
                Assert.That(rig.Bindings.RegistryGeneration, Is.EqualTo(generation));
                Assert.That(rig.Bindings.BoneCount, Is.EqualTo(2));
            }
        }

        sealed class RuntimeBone : IDisposable
        {
            public BoneName Name { get; } = new BoneName("RuntimeGrandchild");
            public ConfigurableJoint Joint { get; }

            readonly GameObject gameObject;

            public RuntimeBone(Rigidbody parent)
            {
                gameObject = new GameObject("Runtime Registry Grandchild");
                gameObject.transform.SetParent(parent.transform, false);
                gameObject.AddComponent<Rigidbody>();
                Joint = gameObject.AddComponent<ConfigurableJoint>();
                Joint.connectedBody = parent;
                gameObject.AddComponent<BoxCollider>();
            }

            public void Dispose()
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
