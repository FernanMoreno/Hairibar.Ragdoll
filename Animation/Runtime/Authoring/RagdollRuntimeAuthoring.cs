using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation
{
    /// <summary>
    /// Creates a ConfigurableJoint biped ragdoll from explicit semantic references.
    /// Safe ownership records allow clearing only components created by this API.
    /// </summary>
    public static class RagdollRuntimeAuthoring
    {
        internal interface IObjectFactory
        {
            T AddComponent<T>(GameObject owner) where T : Component;
            void Destroy(UnityEngine.Object value);
        }

        internal interface ITransactionBoundaryProbe
        {
            void AfterRegistryCommit();
        }

        sealed class DefaultObjectFactory : IObjectFactory
        {
            internal static readonly DefaultObjectFactory Instance =
                new DefaultObjectFactory();

            public T AddComponent<T>(GameObject owner) where T : Component
            {
                return owner.AddComponent<T>();
            }

            public void Destroy(UnityEngine.Object value)
            {
                DestroyObject(value);
            }
        }

        internal static IObjectFactory DefaultFactory =>
            DefaultObjectFactory.Instance;

        sealed class Node
        {
            public Transform Transform;
            public Transform Endpoint;
            public RagdollAuthoringColliderShape Shape;
            public Rigidbody Body;
            public Collider Collider;
            public ConfigurableJoint Joint;
            public float Volume;
        }

        public static bool TryBuild(
            RagdollBipedReferences references,
            RagdollAuthoringOptions options,
            out RagdollAuthoredRig authoredRig,
            out string error)
        {
            return TryBuild(
                references,
                options,
                DefaultObjectFactory.Instance,
                out authoredRig,
                out error);
        }

        internal static bool TryRebuild(
            RagdollAuthoredRig existing,
            RagdollBipedReferences references,
            RagdollAuthoringOptions options,
            IObjectFactory factory,
            out string error)
        {
            if (!existing)
                throw new ArgumentNullException(nameof(existing));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (references == null)
            {
                error = "Biped references are required.";
                return false;
            }
            if (!references.Validate(out error)) return false;
            options.Normalize();
            List<Node> nodes = BuildNodes(references, options);
            HashSet<UnityEngine.Object> oldOwned = new HashSet<UnityEngine.Object>();
            AddOwned(oldOwned, existing.Rigidbodies);
            AddOwned(oldOwned, existing.Colliders);
            AddOwned(oldOwned, existing.Joints);
            if (!ValidateReplacementComponents(nodes, oldOwned, out error))
                return false;

            List<UnityEngine.Object> created = new List<UnityEngine.Object>();
            List<Action> rollback = new List<Action>();
            Rigidbody[] originalBodies = existing.Rigidbodies;
            Collider[] originalColliders = existing.Colliders;
            ConfigurableJoint[] originalJoints = existing.Joints;
            bool registryCommitted = false;
            try
            {
                for (int index = 0; index < nodes.Count; index++)
                {
                    Node node = nodes[index];
                    node.Body = FindOwnedAt(existing.Rigidbodies, node.Transform);
                    if (!node.Body)
                    {
                        node.Body = factory.AddComponent<Rigidbody>(
                            node.Transform.gameObject);
                        created.Add(node.Body);
                    }
                    else
                    {
                        Rigidbody body = node.Body;
                        float mass = body.mass;
                        rollback.Add(() => { if (body) body.mass = mass; });
                    }

                    Collider compatible = FindCompatibleOwnedCollider(
                        existing.Colliders, node.Transform, node.Shape);
                    if (compatible)
                    {
                        node.Collider = compatible;
                        rollback.Add(CaptureColliderRollback(compatible));
                    }
                    else
                    {
                        node.Collider = AddCollider(node, factory);
                        created.Add(node.Collider);
                    }
                    ConfigureCollider(node, options, node.Collider);
                }

                for (int index = 0; index < nodes.Count; index++)
                {
                    Node node = nodes[index];
                    node.Joint = FindOwnedAt(existing.Joints, node.Transform);
                    if (!node.Joint)
                    {
                        node.Joint = factory.AddComponent<ConfigurableJoint>(
                            node.Transform.gameObject);
                        created.Add(node.Joint);
                    }
                    else rollback.Add(CaptureJointRollback(node.Joint));
                    ConfigureJoint(node, nodes, options);
                }

                DistributeMass(nodes, references, options.totalMass,
                    options.massDistribution);
                Rigidbody[] bodies = Select(nodes, node => node.Body);
                Collider[] colliders = Select(nodes, node => node.Collider);
                ConfigurableJoint[] joints = Select(nodes, node => node.Joint);
                existing.SetOwnedComponents(bodies, colliders, joints);
                registryCommitted = true;
                ITransactionBoundaryProbe probe =
                    factory as ITransactionBoundaryProbe;
                probe?.AfterRegistryCommit();

                HashSet<UnityEngine.Object> retained = new HashSet<UnityEngine.Object>();
                AddOwned(retained, bodies);
                AddOwned(retained, colliders);
                AddOwned(retained, joints);
                foreach (UnityEngine.Object value in oldOwned)
                {
                    if (!value || retained.Contains(value)) continue;
                    try { factory.Destroy(value); }
                    catch (Exception cleanupException)
                    {
                        UnityEngine.Debug.LogException(cleanupException, existing);
                    }
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (registryCommitted)
                {
                    existing.SetOwnedComponents(
                        originalBodies,
                        originalColliders,
                        originalJoints);
                }
                for (int index = rollback.Count - 1; index >= 0; index--)
                {
                    try { rollback[index](); }
                    catch (Exception rollbackException)
                    {
                        UnityEngine.Debug.LogException(rollbackException, existing);
                    }
                }
                for (int index = created.Count - 1; index >= 0; index--)
                    if (created[index]) factory.Destroy(created[index]);
                error = exception.Message;
                return false;
            }
        }

        internal static bool TryBuild(
            RagdollBipedReferences references,
            RagdollAuthoringOptions options,
            IObjectFactory factory,
            out RagdollAuthoredRig authoredRig,
            out string error)
        {
            authoredRig = null;
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (references == null)
            {
                error = "Biped references are required.";
                return false;
            }
            if (!references.Validate(out error)) return false;
            if (references.hips.GetComponent<RagdollAuthoredRig>())
            {
                error = "This hierarchy already owns an authored ragdoll. Clear it first.";
                return false;
            }

            options.Normalize();
            List<Node> nodes = BuildNodes(references, options);
            if (!ValidateComponentsAbsent(nodes, out error)) return false;
            try
            {
                for (int index = 0; index < nodes.Count; index++)
                {
                    Node node = nodes[index];
                    node.Body = RequireAbsent<Rigidbody>(node.Transform, factory);
                    node.Collider = CreateCollider(node, options, factory);
                }

                for (int index = 0; index < nodes.Count; index++)
                {
                    CreateJoint(nodes[index], nodes, options, factory);
                }

                DistributeMass(
                    nodes,
                    references,
                    options.totalMass,
                    options.massDistribution);
                authoredRig = factory.AddComponent<RagdollAuthoredRig>(
                    references.hips.gameObject);
                authoredRig.SetOwnedComponents(
                    Select(nodes, node => node.Body),
                    Select(nodes, node => node.Collider),
                    Select(nodes, node => node.Joint));
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                ClearCreated(nodes, factory);
                factory.Destroy(authoredRig);
                authoredRig = null;
                error = exception.Message;
                return false;
            }
        }

        static bool ValidateComponentsAbsent(List<Node> nodes, out string error)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                Transform bone = nodes[index].Transform;
                if (bone.GetComponent<Rigidbody>())
                {
                    error = bone.name + " already has Rigidbody.";
                    return false;
                }
                if (bone.GetComponent<ConfigurableJoint>())
                {
                    error = bone.name + " already has ConfigurableJoint.";
                    return false;
                }
                if (bone.GetComponent<Collider>())
                {
                    error = bone.name + " already has Collider.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        static bool ValidateReplacementComponents(
            List<Node> nodes,
            HashSet<UnityEngine.Object> owned,
            out string error)
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                Component[] components = nodes[index].Transform
                    .GetComponents<Component>();
                for (int componentIndex = 0;
                     componentIndex < components.Length;
                     componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (!(component is Rigidbody)
                        && !(component is Collider)
                        && !(component is ConfigurableJoint)) continue;
                    if (owned.Contains(component)) continue;
                    error = nodes[index].Transform.name + " contains "
                        + component.GetType().Name
                        + " not owned by this author.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        static void AddOwned<T>(
            HashSet<UnityEngine.Object> destination,
            T[] values) where T : UnityEngine.Object
        {
            if (values == null) return;
            for (int index = 0; index < values.Length; index++)
                if (values[index]) destination.Add(values[index]);
        }

        static T FindOwnedAt<T>(T[] values, Transform owner)
            where T : Component
        {
            if (values == null) return null;
            for (int index = 0; index < values.Length; index++)
                if (values[index] && values[index].transform == owner)
                    return values[index];
            return null;
        }

        static Collider FindCompatibleOwnedCollider(
            Collider[] values,
            Transform owner,
            RagdollAuthoringColliderShape shape)
        {
            if (values == null) return null;
            Type required = shape == RagdollAuthoringColliderShape.Box
                ? typeof(BoxCollider)
                : shape == RagdollAuthoringColliderShape.Sphere
                    ? typeof(SphereCollider)
                    : typeof(CapsuleCollider);
            for (int index = 0; index < values.Length; index++)
                if (values[index] && values[index].transform == owner
                    && values[index].GetType() == required)
                    return values[index];
            return null;
        }

        static Action CaptureColliderRollback(Collider value)
        {
            BoxCollider box = value as BoxCollider;
            if (box)
            {
                Vector3 center = box.center;
                Vector3 size = box.size;
                return () =>
                {
                    if (!box) return;
                    box.center = center;
                    box.size = size;
                };
            }
            SphereCollider sphere = value as SphereCollider;
            if (sphere)
            {
                Vector3 center = sphere.center;
                float radius = sphere.radius;
                return () =>
                {
                    if (!sphere) return;
                    sphere.center = center;
                    sphere.radius = radius;
                };
            }
            CapsuleCollider capsule = (CapsuleCollider)value;
            Vector3 capsuleCenter = capsule.center;
            float capsuleRadius = capsule.radius;
            float capsuleHeight = capsule.height;
            int capsuleDirection = capsule.direction;
            return () =>
            {
                if (!capsule) return;
                capsule.center = capsuleCenter;
                capsule.radius = capsuleRadius;
                capsule.height = capsuleHeight;
                capsule.direction = capsuleDirection;
            };
        }

        static Action CaptureJointRollback(ConfigurableJoint joint)
        {
            Rigidbody connectedBody = joint.connectedBody;
            bool preprocessing = joint.enablePreprocessing;
            JointProjectionMode projection = joint.projectionMode;
            Vector3 axis = joint.axis;
            Vector3 secondary = joint.secondaryAxis;
            ConfigurableJointMotion xMotion = joint.xMotion;
            ConfigurableJointMotion yMotion = joint.yMotion;
            ConfigurableJointMotion zMotion = joint.zMotion;
            ConfigurableJointMotion angularX = joint.angularXMotion;
            ConfigurableJointMotion angularY = joint.angularYMotion;
            ConfigurableJointMotion angularZ = joint.angularZMotion;
            SoftJointLimit lowX = joint.lowAngularXLimit;
            SoftJointLimit highX = joint.highAngularXLimit;
            SoftJointLimit yLimit = joint.angularYLimit;
            SoftJointLimit zLimit = joint.angularZLimit;
            RotationDriveMode driveMode = joint.rotationDriveMode;
            return () =>
            {
                if (!joint) return;
                joint.connectedBody = connectedBody;
                joint.enablePreprocessing = preprocessing;
                joint.projectionMode = projection;
                joint.axis = axis;
                joint.secondaryAxis = secondary;
                joint.xMotion = xMotion;
                joint.yMotion = yMotion;
                joint.zMotion = zMotion;
                joint.angularXMotion = angularX;
                joint.angularYMotion = angularY;
                joint.angularZMotion = angularZ;
                joint.lowAngularXLimit = lowX;
                joint.highAngularXLimit = highX;
                joint.angularYLimit = yLimit;
                joint.angularZLimit = zLimit;
                joint.rotationDriveMode = driveMode;
            };
        }

        public static void Clear(RagdollAuthoredRig authoredRig)
        {
            if (!authoredRig) return;
            DestroyOwned(authoredRig.Joints);
            DestroyOwned(authoredRig.Colliders);
            DestroyOwned(authoredRig.Rigidbodies);
            DestroyObject(authoredRig);
        }

        static List<Node> BuildNodes(
            RagdollBipedReferences r,
            RagdollAuthoringOptions o)
        {
            List<Node> nodes = new List<Node>(16);
            Transform central = o.includeSpine && r.spine
                ? r.spine
                : o.includeChest && r.chest ? r.chest : r.head;
            Add(nodes, r.hips, central, o.torsoColliders);
            if (o.includeSpine) Add(nodes, r.spine, First(r.chest, r.head), o.torsoColliders);
            if (o.includeChest) Add(nodes, r.chest, r.head, o.torsoColliders);
            Add(nodes, r.head, null, o.headCollider);

            AddLimb(nodes, r.leftUpperArm, r.leftLowerArm,
                o.includeHands ? r.leftHand : null,
                o.armColliders, o.handColliders, o.includeHands);
            AddLimb(nodes, r.rightUpperArm, r.rightLowerArm,
                o.includeHands ? r.rightHand : null,
                o.armColliders, o.handColliders, o.includeHands);
            AddLimb(nodes, r.leftUpperLeg, r.leftLowerLeg,
                o.includeFeet ? r.leftFoot : null,
                o.legColliders, o.footColliders, o.includeFeet);
            AddLimb(nodes, r.rightUpperLeg, r.rightLowerLeg,
                o.includeFeet ? r.rightFoot : null,
                o.legColliders, o.footColliders, o.includeFeet);

            nodes.Sort((left, right) => GetDepth(left.Transform)
                .CompareTo(GetDepth(right.Transform)));
            return nodes;
        }

        static void AddLimb(
            List<Node> nodes,
            Transform upper,
            Transform lower,
            Transform extremity,
            RagdollAuthoringColliderShape limbShape,
            RagdollAuthoringColliderShape extremityShape,
            bool includeExtremity)
        {
            Add(nodes, upper, lower, limbShape);
            Add(nodes, lower, extremity, limbShape);
            if (includeExtremity) Add(nodes, extremity, null, extremityShape);
        }

        static void Add(
            List<Node> nodes,
            Transform transform,
            Transform endpoint,
            RagdollAuthoringColliderShape shape)
        {
            if (!transform) return;
            for (int index = 0; index < nodes.Count; index++)
            {
                if (nodes[index].Transform == transform) return;
            }
            nodes.Add(new Node
            {
                Transform = transform,
                Endpoint = endpoint,
                Shape = shape
            });
        }

        static T RequireAbsent<T>(
            Transform transform,
            IObjectFactory factory) where T : Component
        {
            if (transform.GetComponent<T>())
            {
                throw new InvalidOperationException(
                    transform.name + " already has " + typeof(T).Name + ".");
            }
            return factory.AddComponent<T>(transform.gameObject);
        }

        static Collider CreateCollider(
            Node node,
            RagdollAuthoringOptions options,
            IObjectFactory factory)
        {
            Collider collider = AddCollider(node, factory);
            ConfigureCollider(node, options, collider);
            return collider;
        }

        static Collider AddCollider(Node node, IObjectFactory factory)
        {
            if (node.Shape == RagdollAuthoringColliderShape.Box)
                return factory.AddComponent<BoxCollider>(node.Transform.gameObject);
            if (node.Shape == RagdollAuthoringColliderShape.Sphere)
                return factory.AddComponent<SphereCollider>(node.Transform.gameObject);
            return factory.AddComponent<CapsuleCollider>(node.Transform.gameObject);
        }

        static void ConfigureCollider(
            Node node,
            RagdollAuthoringOptions options,
            Collider value)
        {
            Vector3 localVector = ResolveLocalSegment(node);
            float length = Mathf.Max(options.minimumColliderSize, localVector.magnitude);
            float overlapLength = Mathf.Max(
                options.minimumColliderSize,
                length * (1f + options.colliderLengthOverlap));
            float radius = Mathf.Max(
                options.minimumColliderSize,
                length * options.colliderRadiusScale);
            Vector3 direction = localVector.sqrMagnitude > 0f
                ? localVector.normalized
                : Vector3.up;
            int axis = DominantAxis(direction);

            if (node.Shape == RagdollAuthoringColliderShape.Box)
            {
                BoxCollider collider = (BoxCollider)value;
                collider.center = direction * overlapLength * 0.5f;
                Vector3 size = Vector3.one * (radius * 2f);
                size[axis] = overlapLength;
                collider.size = size;
                node.Volume = Mathf.Max(0.000001f, size.x * size.y * size.z);
                return;
            }
            if (node.Shape == RagdollAuthoringColliderShape.Sphere)
            {
                SphereCollider collider = (SphereCollider)value;
                collider.center = direction * length * 0.25f;
                collider.radius = Mathf.Max(radius, overlapLength * 0.35f);
                float r = collider.radius;
                node.Volume = Mathf.Max(0.000001f, 4f / 3f * Mathf.PI * r * r * r);
                return;
            }

            CapsuleCollider capsule = (CapsuleCollider)value;
            capsule.direction = axis;
            capsule.center = direction * overlapLength * 0.5f;
            capsule.radius = radius;
            capsule.height = Mathf.Max(overlapLength, radius * 2f);
            float cylinderHeight = Mathf.Max(0f, capsule.height - radius * 2f);
            node.Volume = Mathf.Max(
                0.000001f,
                Mathf.PI * radius * radius * cylinderHeight
                + 4f / 3f * Mathf.PI * radius * radius * radius);
        }

        static void CreateJoint(
            Node node,
            List<Node> nodes,
            RagdollAuthoringOptions options,
            IObjectFactory factory)
        {
            node.Joint = RequireAbsent<ConfigurableJoint>(node.Transform, factory);
            ConfigureJoint(node, nodes, options);
        }

        static void ConfigureJoint(
            Node node,
            List<Node> nodes,
            RagdollAuthoringOptions options)
        {
            Node parent = FindNearestParent(node, nodes);
            node.Joint.connectedBody = parent != null ? parent.Body : null;
            node.Joint.enablePreprocessing = options.enablePreprocessing;
            node.Joint.projectionMode = options.enableProjection
                ? JointProjectionMode.PositionAndRotation
                : JointProjectionMode.None;

            Vector3 axis = ResolveLocalSegment(node).normalized;
            if (axis.sqrMagnitude < 0.5f) axis = Vector3.right;
            node.Joint.axis = axis;
            node.Joint.secondaryAxis = ResolveSecondaryAxis(axis);

            if (parent == null)
            {
                node.Joint.xMotion = ConfigurableJointMotion.Free;
                node.Joint.yMotion = ConfigurableJointMotion.Free;
                node.Joint.zMotion = ConfigurableJointMotion.Free;
                node.Joint.angularXMotion = ConfigurableJointMotion.Free;
                node.Joint.angularYMotion = ConfigurableJointMotion.Free;
                node.Joint.angularZMotion = ConfigurableJointMotion.Free;
                return;
            }

            node.Joint.xMotion = ConfigurableJointMotion.Locked;
            node.Joint.yMotion = ConfigurableJointMotion.Locked;
            node.Joint.zMotion = ConfigurableJointMotion.Locked;
            node.Joint.angularXMotion = ConfigurableJointMotion.Limited;
            node.Joint.angularYMotion = ConfigurableJointMotion.Limited;
            node.Joint.angularZMotion = ConfigurableJointMotion.Limited;

            float x = options.angularXLimit * options.jointRangeMultiplier;
            float yz = options.angularYZLimit * options.jointRangeMultiplier;
            node.Joint.lowAngularXLimit = Limit(-x);
            node.Joint.highAngularXLimit = Limit(x);
            node.Joint.angularYLimit = Limit(yz);
            node.Joint.angularZLimit = Limit(yz);
            node.Joint.rotationDriveMode = RotationDriveMode.Slerp;
        }

        static SoftJointLimit Limit(float value)
        {
            return new SoftJointLimit { limit = value };
        }

        static void DistributeMass(
            List<Node> nodes,
            RagdollBipedReferences references,
            float totalMass,
            RagdollAuthoringMassDistribution distribution)
        {
            float[] weights = new float[nodes.Count];
            float sum = 0f;
            for (int index = 0; index < nodes.Count; index++)
            {
                weights[index] = distribution
                    == RagdollAuthoringMassDistribution.ColliderVolume
                    ? nodes[index].Volume
                    : ResolveBiometricWeight(nodes[index].Transform, references, nodes);
                sum += Mathf.Max(0f, weights[index]);
            }

            if (sum <= Mathf.Epsilon)
            {
                for (int index = 0; index < weights.Length; index++) weights[index] = 1f;
                sum = weights.Length;
            }

            float assigned = 0f;
            for (int index = 0; index < nodes.Count; index++)
            {
                float mass = index == nodes.Count - 1
                    ? totalMass - assigned
                    : totalMass * Mathf.Max(0f, weights[index]) / sum;
                nodes[index].Body.mass = Mathf.Max(0.0001f, mass);
                assigned += nodes[index].Body.mass;
            }
        }

        static float ResolveBiometricWeight(
            Transform bone,
            RagdollBipedReferences r,
            List<Node> included)
        {
            // Relative category weights are Hairibar-owned authoring policy. Missing
            // optional bones are normalized out, preserving exact requested total mass.
            if (bone == r.hips || bone == r.spine || bone == r.chest)
            {
                return 0.50f / CountIncluded(included, r.hips, r.spine, r.chest);
            }
            if (bone == r.head) return 0.08f;
            if (bone == r.leftUpperArm || bone == r.rightUpperArm
                || bone == r.leftLowerArm || bone == r.rightLowerArm)
            {
                return 0.10f / CountIncluded(
                    included,
                    r.leftUpperArm,
                    r.rightUpperArm,
                    r.leftLowerArm,
                    r.rightLowerArm);
            }
            if (bone == r.leftHand || bone == r.rightHand)
            {
                return 0.02f / CountIncluded(included, r.leftHand, r.rightHand);
            }
            if (bone == r.leftUpperLeg || bone == r.rightUpperLeg)
            {
                return 0.20f / CountIncluded(included, r.leftUpperLeg, r.rightUpperLeg);
            }
            if (bone == r.leftLowerLeg || bone == r.rightLowerLeg)
            {
                return 0.08f / CountIncluded(included, r.leftLowerLeg, r.rightLowerLeg);
            }
            if (bone == r.leftFoot || bone == r.rightFoot)
            {
                return 0.02f / CountIncluded(included, r.leftFoot, r.rightFoot);
            }
            return 0f;
        }

        static int CountIncluded(List<Node> nodes, params Transform[] candidates)
        {
            int count = 0;
            for (int candidate = 0; candidate < candidates.Length; candidate++)
            {
                if (!candidates[candidate]) continue;
                for (int index = 0; index < nodes.Count; index++)
                {
                    if (nodes[index].Transform != candidates[candidate]) continue;
                    count++;
                    break;
                }
            }
            return Mathf.Max(1, count);
        }

        static Vector3 ResolveLocalSegment(Node node)
        {
            if (node.Endpoint)
            {
                return node.Transform.InverseTransformVector(
                    node.Endpoint.position - node.Transform.position);
            }

            Transform parent = node.Transform.parent;
            if (parent)
            {
                Vector3 away = node.Transform.position - parent.position;
                return node.Transform.InverseTransformVector(away * 0.5f);
            }
            return Vector3.up * 0.1f;
        }

        static Node FindNearestParent(Node node, List<Node> nodes)
        {
            for (Transform parent = node.Transform.parent; parent; parent = parent.parent)
            {
                for (int index = 0; index < nodes.Count; index++)
                {
                    if (nodes[index].Transform == parent) return nodes[index];
                }
            }
            return null;
        }

        static Vector3 ResolveSecondaryAxis(Vector3 axis)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.forward;
            return Vector3.Cross(axis, reference).normalized;
        }

        static int DominantAxis(Vector3 direction)
        {
            Vector3 absolute = new Vector3(
                Mathf.Abs(direction.x),
                Mathf.Abs(direction.y),
                Mathf.Abs(direction.z));
            if (absolute.x >= absolute.y && absolute.x >= absolute.z) return 0;
            return absolute.y >= absolute.z ? 1 : 2;
        }

        static int GetDepth(Transform transform)
        {
            int depth = 0;
            for (; transform; transform = transform.parent) depth++;
            return depth;
        }

        static Transform First(Transform preferred, Transform fallback)
        {
            return preferred ? preferred : fallback;
        }

        static T[] Select<T>(List<Node> nodes, Func<Node, T> selector)
        {
            T[] values = new T[nodes.Count];
            for (int index = 0; index < nodes.Count; index++)
            {
                values[index] = selector(nodes[index]);
            }
            return values;
        }

        static void ClearCreated(List<Node> nodes, IObjectFactory factory)
        {
            for (int index = nodes.Count - 1; index >= 0; index--)
            {
                factory.Destroy(nodes[index].Joint);
                factory.Destroy(nodes[index].Collider);
                factory.Destroy(nodes[index].Body);
            }
        }

        static void DestroyOwned<T>(T[] components) where T : UnityEngine.Object
        {
            if (components == null) return;
            for (int index = components.Length - 1; index >= 0; index--)
            {
                DestroyObject(components[index]);
            }
        }

        static void DestroyObject(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
