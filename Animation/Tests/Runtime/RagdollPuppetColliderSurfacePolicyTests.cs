using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    public sealed class RagdollPuppetColliderSurfacePolicyTests
    {
        [Test]
        public void PuppetCanDisableColliderAndUsePuppetMaterial()
        {
            PhysicMaterial baseline = new PhysicMaterial("baseline");
            PhysicMaterial puppet = new PhysicMaterial("puppet");
            try
            {
                RagdollPuppetColliderSurfacePlan plan =
                    RagdollPuppetColliderSurfacePolicy.Resolve(
                        RagdollPuppetColliderSurfaceState.Puppet,
                        true,
                        true,
                        baseline,
                        puppet,
                        null);

                Assert.That(plan.Enabled, Is.False);
                Assert.That(plan.DisabledByBehaviour, Is.True);
                Assert.That(plan.Material, Is.SameAs(puppet));
                Assert.That(plan.MaterialOverridden, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(puppet);
            }
        }

        [Test]
        public void PuppetNeverEnablesColliderThatWasDisabledAtBaseline()
        {
            RagdollPuppetColliderSurfacePlan plan =
                RagdollPuppetColliderSurfacePolicy.Resolve(
                    RagdollPuppetColliderSurfaceState.Puppet,
                    false,
                    false,
                    null,
                    null,
                    null);

            Assert.That(plan.Enabled, Is.False);
            Assert.That(plan.DisabledByBehaviour, Is.False);
        }

        [Test]
        public void GetUpUsesPuppetMaterialButIgnoresDisableCollidersFlag()
        {
            PhysicMaterial puppet = new PhysicMaterial("puppet");
            try
            {
                RagdollPuppetColliderSurfacePlan plan =
                    RagdollPuppetColliderSurfacePolicy.Resolve(
                        RagdollPuppetColliderSurfaceState.GetUp,
                        true,
                        true,
                        null,
                        puppet,
                        null);

                Assert.That(plan.Enabled, Is.True);
                Assert.That(plan.DisabledByBehaviour, Is.False);
                Assert.That(plan.Material, Is.SameAs(puppet));
            }
            finally
            {
                Object.DestroyImmediate(puppet);
            }
        }

        [Test]
        public void UnpinnedUsesUnpinnedMaterialAndRestoresColliderEnablement()
        {
            PhysicMaterial unpinned = new PhysicMaterial("unpinned");
            try
            {
                RagdollPuppetColliderSurfacePlan plan =
                    RagdollPuppetColliderSurfacePolicy.Resolve(
                        RagdollPuppetColliderSurfaceState.Unpinned,
                        true,
                        true,
                        null,
                        null,
                        unpinned);

                Assert.That(plan.Enabled, Is.True);
                Assert.That(plan.DisabledByBehaviour, Is.False);
                Assert.That(plan.Material, Is.SameAs(unpinned));
            }
            finally
            {
                Object.DestroyImmediate(unpinned);
            }
        }

        [Test]
        public void MissingStateMaterialPreservesCapturedBaselineMaterial()
        {
            PhysicMaterial baseline = new PhysicMaterial("baseline");
            try
            {
                RagdollPuppetColliderSurfacePlan plan =
                    RagdollPuppetColliderSurfacePolicy.Resolve(
                        RagdollPuppetColliderSurfaceState.Unpinned,
                        true,
                        false,
                        baseline,
                        null,
                        null);

                Assert.That(plan.Material, Is.SameAs(baseline));
                Assert.That(plan.MaterialOverridden, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(baseline);
            }
        }

        [Test]
        public void StateMappingMatchesBehaviourStates()
        {
            Assert.That(
                RagdollPuppetColliderSurfacePolicy.ResolveState(
                    RagdollPuppetState.Puppet),
                Is.EqualTo(RagdollPuppetColliderSurfaceState.Puppet));
            Assert.That(
                RagdollPuppetColliderSurfacePolicy.ResolveState(
                    RagdollPuppetState.Unpinned),
                Is.EqualTo(RagdollPuppetColliderSurfaceState.Unpinned));
            Assert.That(
                RagdollPuppetColliderSurfacePolicy.ResolveState(
                    RagdollPuppetState.GetUp),
                Is.EqualTo(RagdollPuppetColliderSurfaceState.GetUp));
        }

        [Test]
        public void BindingRestoresExactCapturedEnabledAndMaterialBaseline()
        {
            GameObject gameObject = new GameObject("surface-binding-test");
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            PhysicMaterial baseline = new PhysicMaterial("baseline");
            PhysicMaterial overrideMaterial = new PhysicMaterial("override");
            try
            {
                collider.enabled = true;
                collider.sharedMaterial = baseline;
                RagdollPuppetColliderSurfaceBinding binding =
                    new RagdollPuppetColliderSurfaceBinding(
                        collider,
                        default(RagdollBoneHandle));
                binding.CaptureBaseline();
                binding.Apply(
                    new RagdollPuppetColliderSurfacePlan(
                        false,
                        overrideMaterial,
                        true,
                        true));

                Assert.That(collider.enabled, Is.False);
                Assert.That(collider.sharedMaterial, Is.SameAs(overrideMaterial));

                binding.RestoreBaseline();
                Assert.That(collider.enabled, Is.True);
                Assert.That(collider.sharedMaterial, Is.SameAs(baseline));
                Assert.That(binding.HasBaseline, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(baseline);
                Object.DestroyImmediate(overrideMaterial);
            }
        }

        [Test]
        public void BindingCapturesASecondIndependentBaselineAfterRestore()
        {
            GameObject gameObject = new GameObject("surface-recapture-test");
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            PhysicMaterial first = new PhysicMaterial("first");
            PhysicMaterial second = new PhysicMaterial("second");
            try
            {
                RagdollPuppetColliderSurfaceBinding binding =
                    new RagdollPuppetColliderSurfaceBinding(
                        collider,
                        default(RagdollBoneHandle));

                collider.enabled = true;
                collider.sharedMaterial = first;
                binding.CaptureBaseline();
                binding.RestoreBaseline();

                collider.enabled = false;
                collider.sharedMaterial = second;
                binding.CaptureBaseline();
                binding.Apply(
                    new RagdollPuppetColliderSurfacePlan(
                        true,
                        first,
                        false,
                        true));
                binding.RestoreBaseline();

                Assert.That(collider.enabled, Is.False);
                Assert.That(collider.sharedMaterial, Is.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }
    }
}
