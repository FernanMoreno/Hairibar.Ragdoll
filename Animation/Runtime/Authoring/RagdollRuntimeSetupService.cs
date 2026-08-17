using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    public sealed class RagdollSetupResult
    {
        public bool Succeeded { get; internal set; }
        public string Error { get; internal set; }
        public Transform Root { get; internal set; }
        public Transform Target { get; internal set; }
        public Transform Puppet { get; internal set; }
        public RagdollAnimator Animator { get; internal set; }
        public RagdollMuscleController Muscles { get; internal set; }
        public RagdollBehaviourController Behaviours { get; internal set; }
        public RagdollSimulationModeController Simulation { get; internal set; }
        public RagdollCollisionHub Collisions { get; internal set; }
        public RagdollPuppetBehaviour PuppetBehaviour { get; internal set; }
    }

    /// <summary>
    /// Transactional runtime wiring for an already-authored physical Puppet and a
    /// separate Target hierarchy. Components created by a failed transaction are the
    /// only objects removed during rollback.
    /// </summary>
    public static class RagdollRuntimeSetupService
    {
        internal interface IObjectFactory
        {
            T AddComponent<T>(GameObject owner) where T : Component;
            GameObject CreateGameObject(string name);
            void Destroy(UnityEngine.Object value);
        }

        sealed class DefaultObjectFactory : IObjectFactory
        {
            internal static readonly DefaultObjectFactory Instance =
                new DefaultObjectFactory();

            public T AddComponent<T>(GameObject owner) where T : Component
            {
                return owner.AddComponent<T>();
            }

            public GameObject CreateGameObject(string name)
            {
                return new GameObject(name);
            }

            public void Destroy(UnityEngine.Object value)
            {
                DestroyOwnedObject(value);
            }
        }

        /// <summary>
        /// Duplicates an authored ragdoll as the Puppet, converts the original hierarchy
        /// into the animation Target by removing its physics, then wires the full runtime.
        /// The original is not mutated until the duplicate has validated successfully.
        /// </summary>
        public static RagdollSetupResult DuplicateAndConvertOriginalToTarget(
            RagdollDefinitionBindings original,
            RagdollAnimationProfile profile,
            int targetLayer,
            int puppetLayer)
        {
            if (!original || !profile)
            {
                return new RagdollSetupResult
                {
                    Error = "An initialized authored ragdoll and AnimationProfile are required."
                };
            }
            if (!original.IsInitialized)
            {
                return new RagdollSetupResult
                {
                    Target = original.transform,
                    Puppet = null,
                    Error = "The source ragdoll bindings must be initialized before duplication."
                };
            }
            if (original.GetComponent<RagdollAnimator>())
            {
                return new RagdollSetupResult
                {
                    Target = original.transform,
                    Puppet = null,
                    Error = "The source already has a RagdollAnimator; use ConfigureSeparated instead."
                };
            }

            GameObject originalObject = original.gameObject;
            Transform originalTransform = original.transform;
            bool originalActive = originalObject.activeSelf;
            string originalName = originalObject.name;
            Transform originalParent = originalTransform.parent;
            int originalSiblingIndex = originalTransform.GetSiblingIndex();
            GameObject puppetObject = null;
            GameObject createdContainer = null;
            try
            {
                if (!originalParent)
                {
                    createdContainer = new GameObject(originalName + " Ragdoll");
                    originalTransform.SetParent(
                        createdContainer.transform,
                        true);
                }
                originalObject.SetActive(false);
                puppetObject = UnityEngine.Object.Instantiate(
                    originalObject,
                    originalTransform.parent);
                // Instantiate appends "(Clone)". Keep the authored root name
                // through legacy name migration, then apply the public Puppet
                // name only after direct Transform bindings have been captured.
                puppetObject.name = originalName;
                puppetObject.SetActive(true);
                RagdollDefinitionBindings puppet =
                    puppetObject.GetComponent<RagdollDefinitionBindings>();
                if (!puppet || !puppet.IsInitialized)
                {
                    throw new InvalidOperationException(
                        "The duplicated Puppet bindings could not initialize.");
                }

                RagdollSetupResult result = ConfigureSeparated(
                    originalTransform,
                    puppet,
                    profile,
                    targetLayer,
                    puppetLayer);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(result.Error);
                }

                // Name-based binding must complete while the Target root still has the
                // same authored name as the Puppet root. Renaming afterwards is safe
                // because RagdollTargetBindings now holds direct Transform references.
                puppetObject.name = "Puppet";
                originalObject.name = "Target";
                RemoveTargetPhysics(originalTransform, original);
                originalObject.SetActive(originalActive);
                result.Root = ResolveCommonRoot(originalTransform, puppet.transform);
                return result;
            }
            catch (Exception exception)
            {
                if (puppetObject) DestroyOwnedObject(puppetObject);
                if (createdContainer)
                {
                    originalTransform.SetParent(null, true);
                    DestroyOwnedObject(createdContainer);
                }
                else if (originalTransform.parent != originalParent)
                {
                    originalTransform.SetParent(originalParent, true);
                    originalTransform.SetSiblingIndex(originalSiblingIndex);
                }
                originalObject.name = originalName;
                originalObject.SetActive(originalActive);
                return new RagdollSetupResult
                {
                    Root = originalTransform.root,
                    Target = originalTransform,
                    Puppet = null,
                    Error = exception.Message
                };
            }
        }

        /// <summary>
        /// Converts the supplied authored physical hierarchy directly into the Puppet for
        /// an already separate Target. No hierarchy is duplicated or stripped.
        /// </summary>
        public static RagdollSetupResult ConvertHierarchyDirectlyToPuppet(
            Transform target,
            RagdollDefinitionBindings hierarchy,
            RagdollAnimationProfile profile,
            int targetLayer,
            int puppetLayer)
        {
            return ConfigureSeparated(
                target,
                hierarchy,
                profile,
                targetLayer,
                puppetLayer);
        }

        [RagdollCompatibilityApi("Runtime setup compatibility", "https://root-motion.com/puppetmasterdox/html/pages.html")]
        public static RagdollSetupResult ConfigureSeparated(
            Transform target,
            RagdollDefinitionBindings puppet,
            RagdollAnimationProfile profile,
            int targetLayer,
            int puppetLayer)
        {
            return ConfigureSeparated(
                target,
                puppet,
                profile,
                targetLayer,
                puppetLayer,
                null,
                DefaultObjectFactory.Instance);
        }

        /// <summary>
        /// Configures a separated Humanoid Target through semantic Avatar bindings.
        /// Unlike the compatibility overload, this path never uses Transform names
        /// or assumes matching local bone axes.
        /// </summary>
        public static RagdollSetupResult ConfigureSeparated(
            Transform target,
            RagdollDefinitionBindings puppet,
            RagdollAnimationProfile profile,
            RagdollHumanoidBindingProfile humanoidBindings,
            int targetLayer,
            int puppetLayer)
        {
            if (!humanoidBindings)
            {
                return new RagdollSetupResult
                {
                    Target = target,
                    Puppet = puppet ? puppet.transform : null,
                    Root = ResolveCommonRoot(
                        target,
                        puppet ? puppet.transform : null),
                    Error = "A Humanoid binding profile is required."
                };
            }
            return ConfigureSeparated(
                target,
                puppet,
                profile,
                targetLayer,
                puppetLayer,
                humanoidBindings,
                DefaultObjectFactory.Instance);
        }

        internal static RagdollSetupResult ConfigureSeparated(
            Transform target,
            RagdollDefinitionBindings puppet,
            RagdollAnimationProfile profile,
            int targetLayer,
            int puppetLayer,
            IObjectFactory factory)
        {
            return ConfigureSeparated(
                target,
                puppet,
                profile,
                targetLayer,
                puppetLayer,
                null,
                factory);
        }

        static RagdollSetupResult ConfigureSeparated(
            Transform target,
            RagdollDefinitionBindings puppet,
            RagdollAnimationProfile profile,
            int targetLayer,
            int puppetLayer,
            RagdollHumanoidBindingProfile humanoidBindings,
            IObjectFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            RagdollSetupResult result = new RagdollSetupResult
            {
                Target = target,
                Puppet = puppet ? puppet.transform : null,
                Root = ResolveCommonRoot(target, puppet ? puppet.transform : null),
                Error = string.Empty
            };
            if (!target || !puppet || !profile)
            {
                result.Error = "Target, initialized Puppet bindings and AnimationProfile are required.";
                return result;
            }
            if (target == puppet.transform || target.IsChildOf(puppet.transform)
                || puppet.transform.IsChildOf(target))
            {
                result.Error = "Target and Puppet must be separate hierarchies.";
                return result;
            }
            if (!puppet.IsInitialized)
            {
                result.Error = "Puppet bindings must be initialized before runtime setup.";
                return result;
            }
            if (targetLayer < 0 || targetLayer > 31 || puppetLayer < 0
                || puppetLayer > 31 || targetLayer == puppetLayer)
            {
                result.Error = "Target and Puppet require distinct layer indices from 0 to 31.";
                return result;
            }
            if (target.GetComponent<RagdollAnimator>())
            {
                result.Error = "Target already has a RagdollAnimator.";
                return result;
            }

            bool targetWasActive = target.gameObject.activeSelf;
            bool ignoredBefore = Physics.GetIgnoreLayerCollision(targetLayer, puppetLayer);
            List<LayerSnapshot> layerSnapshots = new List<LayerSnapshot>();
            CaptureLayers(target, layerSnapshots);
            CaptureLayers(puppet.transform, layerSnapshots);
            List<UnityEngine.Object> created = new List<UnityEngine.Object>();
            try
            {
                target.gameObject.SetActive(false);
                RagdollSetupUtility.SetLayerRecursively(target, targetLayer);
                RagdollSetupUtility.SetLayerRecursively(puppet.transform, puppetLayer);
                Physics.IgnoreLayerCollision(targetLayer, puppetLayer, true);

                RagdollTargetBindings targetBindings =
                    factory.AddComponent<RagdollTargetBindings>(target.gameObject);
                created.Add(targetBindings);
                targetBindings.SetRagdollBindings(puppet);
                string bindingError;
                bool assigned = humanoidBindings
                    ? humanoidBindings.TryApply(
                        target.GetComponentInChildren<Animator>(true),
                        targetBindings,
                        out bindingError)
                    : targetBindings.TryAutoBindByName(out bindingError);
                if (!assigned
                    || !targetBindings.TryCaptureOffsets(out bindingError))
                {
                    throw new InvalidOperationException(bindingError);
                }

                result.Animator = factory.AddComponent<RagdollAnimator>(target.gameObject);
                created.Add(result.Animator);
                result.Animator.ConfigureBeforeInitialization(
                    puppet,
                    targetBindings,
                    profile);
                result.Muscles = factory.AddComponent<RagdollMuscleController>(
                    target.gameObject);
                created.Add(result.Muscles);
                result.Simulation =
                    factory.AddComponent<RagdollSimulationModeController>(
                        target.gameObject);
                created.Add(result.Simulation);
                result.Behaviours =
                    factory.AddComponent<RagdollBehaviourController>(
                        target.gameObject);
                created.Add(result.Behaviours);

                GameObject behaviourRoot =
                    factory.CreateGameObject("Character Behaviours");
                created.Add(behaviourRoot);
                // The setup contract applies the character-controller layer to
                // the complete Target hierarchy. This object is created after the
                // recursive layer pass, so it must inherit that layer explicitly.
                behaviourRoot.layer = targetLayer;
                behaviourRoot.transform.SetParent(target, false);
                result.PuppetBehaviour =
                    factory.AddComponent<RagdollPuppetBehaviour>(behaviourRoot);
                result.Behaviours.SetBehaviourRoot(behaviourRoot.transform);

                result.Collisions = puppet.GetComponent<RagdollCollisionHub>();
                if (!result.Collisions)
                {
                    result.Collisions =
                        factory.AddComponent<RagdollCollisionHub>(
                            puppet.gameObject);
                    created.Add(result.Collisions);
                }

                target.gameObject.SetActive(targetWasActive);
                result.Succeeded = true;
                return result;
            }
            catch (Exception exception)
            {
                target.gameObject.SetActive(false);
                for (int index = created.Count - 1; index >= 0; index--)
                {
                    if (created[index]) factory.Destroy(created[index]);
                }
                Physics.IgnoreLayerCollision(
                    targetLayer,
                    puppetLayer,
                    ignoredBefore);
                RestoreLayers(layerSnapshots);
                target.gameObject.SetActive(targetWasActive);
                result.Error = exception.Message;
                result.Animator = null;
                result.Muscles = null;
                result.Behaviours = null;
                result.Simulation = null;
                result.Collisions = null;
                result.PuppetBehaviour = null;
                return result;
            }
        }

        static Transform ResolveCommonRoot(Transform first, Transform second)
        {
            if (!first || !second) return first ? first.root : second ? second.root : null;
            HashSet<Transform> ancestors = new HashSet<Transform>();
            for (Transform value = first; value; value = value.parent) ancestors.Add(value);
            for (Transform value = second; value; value = value.parent)
            {
                if (ancestors.Contains(value)) return value;
            }
            return null;
        }

        static void RemoveTargetPhysics(
            Transform target,
            RagdollDefinitionBindings ownedBindings)
        {
            ConfigurableJoint[] joints =
                target.GetComponentsInChildren<ConfigurableJoint>(true);
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
            for (int index = 0; index < joints.Length; index++)
            {
                if (!joints[index]) continue;
                joints[index].connectedBody = null;
                DestroyOwnedObject(joints[index]);
            }
            for (int index = 0; index < colliders.Length; index++)
            {
                if (!colliders[index]) continue;
                colliders[index].enabled = false;
                DestroyOwnedObject(colliders[index]);
            }
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (!body) continue;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.detectCollisions = false;
                body.isKinematic = true;
                DestroyOwnedObject(body);
            }

            RagdollCollisionHub hub = ownedBindings
                ? ownedBindings.GetComponent<RagdollCollisionHub>()
                : null;
            RagdollSettings settings = ownedBindings
                ? ownedBindings.GetComponent<RagdollSettings>()
                : null;
            RagdollAuthoredRig authored = ownedBindings
                ? ownedBindings.GetComponent<RagdollAuthoredRig>()
                : null;
            DestroyOwnedObject(hub);
            DestroyOwnedObject(settings);
            DestroyOwnedObject(authored);
            DestroyOwnedObject(ownedBindings);
        }

        static void DestroyOwnedObject(UnityEngine.Object value)
        {
            if (!value) return;
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(value);
                return;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject) gameObject.SetActive(false);
            Behaviour behaviour = value as Behaviour;
            if (behaviour) behaviour.enabled = false;
            Collider collider = value as Collider;
            if (collider) collider.enabled = false;
            UnityEngine.Object.Destroy(value);
        }

        static void CaptureLayers(Transform root, List<LayerSnapshot> snapshots)
        {
            snapshots.Add(new LayerSnapshot(root.gameObject, root.gameObject.layer));
            for (int index = 0; index < root.childCount; index++)
            {
                CaptureLayers(root.GetChild(index), snapshots);
            }
        }

        static void RestoreLayers(List<LayerSnapshot> snapshots)
        {
            for (int index = 0; index < snapshots.Count; index++)
            {
                if (snapshots[index].Object)
                {
                    snapshots[index].Object.layer = snapshots[index].Layer;
                }
            }
        }

        readonly struct LayerSnapshot
        {
            internal readonly GameObject Object;
            internal readonly int Layer;

            internal LayerSnapshot(GameObject value, int layer)
            {
                Object = value;
                Layer = layer;
            }
        }
    }
}
