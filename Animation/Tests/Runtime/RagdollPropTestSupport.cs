using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hairibar.Ragdoll.Animation.Tests
{
    internal sealed class FakePropMuscleRuntime : IRagdollPropMuscleRuntime
    {
        public bool IsReady { get; set; } = true;
        public bool IsSimulationDisabled { get; set; }
        public bool HasRegisteredSlot { get; set; }
        public bool GroupValid { get; set; } = true;
        public bool AdditionalPinAvailable { get; set; } = true;
        public float EffectivePositionAuthority { get; set; } = 1f;
        public string AdditionalPinContextError { get; set; }
        public int InternalCollisionPolicyReapplyCount { get; private set; }
        public bool InternalCollisionPolicyReapplySucceeds { get; set; } = true;
        public RagdollMuscleConnectionState ConnectionState { get; set; } =
            RagdollMuscleConnectionState.Connected;
        public bool PendingDisconnect { get; private set; }
        public bool PendingReconnect { get; private set; }
        public bool PendingDeactivate { get; private set; }
        public int ResolveCount { get; private set; }
        public int RegistrationCount { get; private set; }
        public RagdollRuntimeMuscleRegistration LastRegistration { get; private set; }

        public bool TryResolveSlot(
            ConfigurableJoint joint,
            out RagdollBoneHandle handle)
        {
            ResolveCount++;
            handle = default(RagdollBoneHandle);
            return HasRegisteredSlot && joint;
        }

        public bool TryRegisterSlot(
            RagdollRuntimeMuscleRegistration registration,
            out RagdollBoneHandle handle,
            out string error)
        {
            handle = default(RagdollBoneHandle);
            error = null;
            if (!IsReady)
            {
                error = "Runtime is not ready.";
                return false;
            }
            LastRegistration = registration;
            RegistrationCount++;
            HasRegisteredSlot = true;
            ConnectionState = RagdollMuscleConnectionState.Connected;
            return true;
        }

        public bool TryValidatePropGroup(
            RagdollBoneHandle handle,
            out string error)
        {
            error = GroupValid
                ? null
                : "Slot group is not Prop.";
            return GroupValid;
        }

        public RagdollMuscleConnectionState GetConnectionState(
            RagdollBoneHandle handle)
        {
            return ConnectionState;
        }

        public bool IsDisconnecting(RagdollBoneHandle handle)
        {
            return PendingDisconnect;
        }

        public bool IsReconnecting(RagdollBoneHandle handle)
        {
            return PendingReconnect;
        }

        public bool TryDisconnect(
            RagdollBoneHandle handle,
            bool deactivate,
            out string error)
        {
            error = null;
            PendingDisconnect = true;
            PendingReconnect = false;
            PendingDeactivate = deactivate;
            return true;
        }

        public bool TryReconnect(
            RagdollBoneHandle handle,
            out string error)
        {
            error = null;
            PendingReconnect = true;
            PendingDisconnect = false;
            PendingDeactivate = false;
            return true;
        }

        public bool TryGetAdditionalPinAuthority(
            RagdollBoneHandle handle,
            Rigidbody slotBody,
            out float authority,
            out string error)
        {
            authority = AdditionalPinAvailable
                ? Mathf.Clamp01(EffectivePositionAuthority)
                : 0f;
            error = AdditionalPinContextError;
            return AdditionalPinAvailable && string.IsNullOrEmpty(error);
        }

        public bool TryReapplyInternalCollisionPolicy(out string error)
        {
            InternalCollisionPolicyReapplyCount++;
            error = InternalCollisionPolicyReapplySucceeds
                ? null
                : "Synthetic core collision-policy failure.";
            return InternalCollisionPolicyReapplySucceeds;
        }

        public void CommitPending()
        {
            if (PendingReconnect)
            {
                ConnectionState = RagdollMuscleConnectionState.Connected;
                PendingReconnect = false;
                return;
            }
            if (PendingDisconnect)
            {
                ConnectionState = PendingDeactivate
                    ? RagdollMuscleConnectionState.Deactivated
                    : RagdollMuscleConnectionState.Disconnected;
                PendingDisconnect = false;
                PendingDeactivate = false;
            }
        }
    }

    internal sealed class RagdollPropTestRig : IDisposable
    {
        public GameObject StandaloneParent { get; }
        public GameObject PuppetParent { get; }
        public GameObject PhysicalSlot { get; }
        public GameObject TargetParent { get; }
        public GameObject TargetSlot { get; }
        public GameObject MuscleObject { get; }
        public RagdollPropMuscle Muscle { get; }
        public FakePropMuscleRuntime Runtime { get; }

        public RagdollProp PropA { get; }
        public RagdollProp PropB { get; }
        public Transform MeshA { get; }
        public Transform MeshB { get; }

        public Vector3 RootLocalScale { get; } =
            new Vector3(0.75f, 1.25f, 1.5f);
        public Vector3 MeshLocalPosition { get; } =
            new Vector3(0.2f, -0.1f, 0.4f);
        public Quaternion MeshLocalRotation { get; } =
            Quaternion.Euler(5f, 15f, 25f);
        public Vector3 MeshLocalScale { get; } =
            new Vector3(1.1f, 0.9f, 1.2f);

        public float Mass { get; } = 3.25f;
        public float Drag { get; } = 0.15f;
        public float AngularDrag { get; } = 0.35f;
        public float SleepThreshold { get; } = 0.017f;
        public float MaxDepenetrationVelocity { get; } = 7.5f;
        public Vector3 CenterOfMass { get; } =
            new Vector3(0.1f, 0.2f, -0.15f);
        public Vector3 InertiaTensor { get; } =
            new Vector3(1.25f, 2.5f, 3.75f);
        public Quaternion InertiaTensorRotation { get; } =
            Quaternion.Euler(10f, 20f, 30f);

        public RagdollPropTestRig(bool runtimeSlotAlreadyRegistered = false)
        {
            StandaloneParent = new GameObject("Standalone Parent");
            StandaloneParent.transform.localScale = new Vector3(2f, 3f, 4f);

            PuppetParent = new GameObject("Puppet Parent");
            Rigidbody puppetBody = PuppetParent.AddComponent<Rigidbody>();
            puppetBody.isKinematic = true;

            PhysicalSlot = new GameObject("Physical Prop Slot");
            PhysicalSlot.transform.SetParent(PuppetParent.transform, false);
            PhysicalSlot.transform.localScale = new Vector3(1.4f, 0.8f, 1.1f);
            Rigidbody slotBody = PhysicalSlot.AddComponent<Rigidbody>();
            ConfigurableJoint slotJoint =
                PhysicalSlot.AddComponent<ConfigurableJoint>();
            slotJoint.connectedBody = puppetBody;
            PhysicalSlot.AddComponent<BoxCollider>();

            TargetParent = new GameObject("Target Parent");
            TargetSlot = new GameObject("Target Prop Slot");
            TargetSlot.transform.SetParent(TargetParent.transform, false);

            MuscleObject = new GameObject("Prop Muscle Component");
            MuscleObject.transform.SetParent(TargetParent.transform, false);
            Muscle = MuscleObject.AddComponent<RagdollPropMuscle>();
            Runtime = new FakePropMuscleRuntime
            {
                HasRegisteredSlot = runtimeSlotAlreadyRegistered
            };
            Muscle.ConfigureForTesting(
                slotJoint,
                TargetSlot.transform,
                new BoneName("RuntimeProp"));
            Muscle.SetRuntimeForTesting(Runtime);

            PropA = CreateProp("Prop A", out Transform meshA);
            MeshA = meshA;
            PropB = CreateProp("Prop B", out Transform meshB);
            MeshB = meshB;
        }

        public void PrimeEmptySlot()
        {
            Muscle.TickForTesting();
            Assert.That(Runtime.HasRegisteredSlot, Is.True);
            Assert.That(Muscle.State, Is.EqualTo(RagdollPropMuscleState.PrimingEmptySlot));

            Muscle.TickForTesting();
            Assert.That(Runtime.PendingDisconnect, Is.True);
            Runtime.CommitPending();
            Muscle.TickForTesting();
            Assert.That(Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));
        }

        public void PickUp(RagdollProp prop)
        {
            Muscle.SetCurrentProp(prop);
            Muscle.TickForTesting();
            Assert.That(Muscle.State, Is.EqualTo(RagdollPropMuscleState.PreparingPickup));
            prop.CompletePendingBodyDestructionForTesting();

            Muscle.TickForTesting();
            Assert.That(Runtime.PendingReconnect, Is.True);
            Runtime.CommitPending();
            Muscle.TickForTesting();
            Assert.That(Muscle.State, Is.EqualTo(RagdollPropMuscleState.Holding));
            Assert.That(Muscle.CurrentProp, Is.EqualTo(prop));
        }

        public void CompletePendingBodyDestruction(RagdollProp prop)
        {
            prop.CompletePendingBodyDestructionForTesting();
        }

        public void DropCurrent()
        {
            Muscle.Drop();
            Muscle.TickForTesting();
            Assert.That(Muscle.State, Is.EqualTo(RagdollPropMuscleState.Disconnecting));
            Runtime.CommitPending();
            Muscle.TickForTesting();
            Assert.That(
                Muscle.State,
                Is.EqualTo(RagdollPropMuscleState.RestoringStandaloneBody));
            Muscle.TickForTesting();
            Assert.That(Muscle.State, Is.EqualTo(RagdollPropMuscleState.Empty));
        }

        RagdollProp CreateProp(string name, out Transform mesh)
        {
            GameObject propObject = new GameObject(name);
            propObject.transform.SetParent(StandaloneParent.transform, false);
            propObject.transform.localPosition = new Vector3(1f, 2f, 3f);
            propObject.transform.localRotation = Quaternion.Euler(2f, 4f, 6f);
            propObject.transform.localScale = RootLocalScale;
            RagdollProp prop = propObject.AddComponent<RagdollProp>();
            propObject.AddComponent<BoxCollider>();

            GameObject meshObject = new GameObject(name + " Mesh Root");
            meshObject.transform.SetParent(propObject.transform, false);
            meshObject.transform.localPosition = MeshLocalPosition;
            meshObject.transform.localRotation = MeshLocalRotation;
            meshObject.transform.localScale = MeshLocalScale;
            mesh = meshObject.transform;

            Rigidbody body = propObject.AddComponent<Rigidbody>();
            ConfigureBody(body);

            SetField(prop, "meshRoot", meshObject.transform);
            SetField(prop, "standaloneRigidbody", body);
            return prop;
        }

        public void ConfigureBody(Rigidbody body)
        {
            body.mass = Mass;
            body.drag = Drag;
            body.angularDrag = AngularDrag;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationZ;
            body.detectCollisions = true;
            body.centerOfMass = CenterOfMass;
            body.inertiaTensor = InertiaTensor;
            body.inertiaTensorRotation = InertiaTensorRotation;
            body.maxAngularVelocity = 12f;
            body.maxDepenetrationVelocity = MaxDepenetrationVelocity;
            body.sleepThreshold = SleepThreshold;
            body.solverIterations = 9;
            body.solverVelocityIterations = 4;
            body.velocity = new Vector3(1f, 0f, 0f);
            body.angularVelocity = new Vector3(0f, 1f, 0f);
        }

        public void Dispose()
        {
            DestroyObject(MuscleObject);
            DestroyObject(StandaloneParent);
            DestroyObject(PuppetParent);
            DestroyObject(TargetParent);
        }

        public static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, name);
            }
            field.SetValue(target, value);
        }

        public static void DestroyObject(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
