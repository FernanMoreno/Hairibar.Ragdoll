using Hairibar.NaughtyExtensions;
using NaughtyAttributes;
using UnityEngine;

namespace Hairibar.Ragdoll
{
    /// <summary>
    /// Manages the settings for all ragdoll bones from a central place.
    /// Field changes must be flushed with ApplySettings().
    /// </summary>
    [AddComponentMenu("Ragdoll/Ragdoll Settings")]
    [RequireComponent(typeof(RagdollDefinitionBindings)), DisallowMultipleComponent]
    [SelectionBase, ExecuteAlways]
    public class RagdollSettings : MonoBehaviour
    {
        #region Public Fields
        public float limitBounciness = 0.3f;
        public float limitContactDistanceFactor = 0.2f;
        public float limitSpring = 1000f;
        public float limitSpringDamping = 80f;

        public bool enableJointPreProcessing = false;
        public bool enableJointProjection = true;
        public float minJointProjectionDistance = 0.5f;
        public float minJointProjectionAngle = 10;

        public bool useGravity = true;
        public float totalMass = 7;
        public float drag = 0;
        public float angularDrag = 0.05f;
        public PhysicMaterial material = null;
        public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
        public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.Discrete;
        public int solverIterations = 20;

        [Header("Advanced Solver")]
        [Min(1)] public int solverVelocityIterations = 1;
        [Min(0f)] public float maxAngularVelocity = 7f;
        [Min(0f)] public float maxDepenetrationVelocity = 10f;
        public bool overrideSleepThreshold = false;
        [Min(0f)] public float sleepThreshold = 0.005f;
        public RagdollInertiaTensorMode inertiaTensorMode =
            RagdollInertiaTensorMode.PreserveAuthored;
        [Min(1f)] public float maximumInertiaTensorRatio = 10f;
        public RagdollAngularDriveInertiaMode angularDriveInertiaMode =
            RagdollAngularDriveInertiaMode.RigidbodyMass;
        #endregion

        [SerializeField, UsePropertySetter] RagdollPowerProfile _powerProfile;
        [SerializeField, UsePropertySetter] RagdollWeightDistribution _weightDistribution;

        #region Public Properties
        public RagdollPowerProfile PowerProfile
        {
            get => _powerProfile;
            set
            {
                RagdollProfile.ValidateAsArgument(value, bindings.Definition, true, "Tried to set a null PowerProfile at RagdollSettings.");

                if (_powerProfile)
                {
                    _powerProfile.OnUpdateValues -= ApplyPowerProfile;
                }

                _powerProfile = value;

                if (_powerProfile)
                {
                    _powerProfile.OnUpdateValues += ApplyPowerProfile;
                    ApplyPowerProfile();
                }
            }
        }

        public RagdollWeightDistribution WeightDistribution
        {
            get => _weightDistribution;
            set
            {
                RagdollProfile.ValidateAsArgument(value, bindings.Definition, true, "Tried to set a null WeightDistribution at RagdolSettings.");

                if (_weightDistribution)
                {
                    _weightDistribution.OnUpdateValues -= ApplyWeightDistribution;
                }

                _weightDistribution = value;

                if (_weightDistribution)
                {
                    _weightDistribution.OnUpdateValues += ApplyWeightDistribution;
                    ApplyWeightDistribution();
                }
            }
        }
        #endregion

        #region References
        RagdollDefinitionBindings bindings;
        bool hasRuntimeSolverOverride;
        RagdollSolverQualitySettings runtimeSolverOverride;
        #endregion

        /// <summary>True while a runtime quality controller owns solver values.</summary>
        public bool HasRuntimeSolverOverride => hasRuntimeSolverOverride;

        /// <summary>
        /// Applies a reversible runtime override for solver iterations, velocity limits,
        /// interpolation and collision detection. Authored values remain unchanged.
        /// </summary>
        public void SetRuntimeSolverOverride(
            RagdollSolverQualitySettings solverSettings)
        {
            runtimeSolverOverride = solverSettings.Sanitized();
            hasRuntimeSolverOverride = true;
            ReapplyRigidbodySettings();
        }

        /// <summary>Restores the solver values authored on this component.</summary>
        public bool ClearRuntimeSolverOverride()
        {
            if (!hasRuntimeSolverOverride) return false;

            hasRuntimeSolverOverride = false;
            ReapplyRigidbodySettings();
            return true;
        }

        /// <summary>
        /// Reapplies only Rigidbody settings. This avoids rebuilding limits or resetting
        /// inertia tensors when simulation modes and quality tiers change at runtime.
        /// </summary>
        public void ReapplyRigidbodySettings()
        {
            if (!bindings || !bindings.IsInitialized) return;
            ApplyRigidbodySettings(false);
        }

        /// <summary>
        /// Returns the scalar used to convert requested angular acceleration into the
        /// SLERP drive spring, damping and maximum torque. Legacy mode uses Rigidbody.mass;
        /// advanced modes use a rotation-invariant approximation of the inertia tensor.
        /// </summary>
        public float GetRotationDriveEffectiveMass(Rigidbody rigidbody)
        {
            if (!rigidbody)
            {
                throw new System.ArgumentNullException(nameof(rigidbody));
            }

            return RagdollSolverMath.ResolveAngularDriveMass(
                rigidbody.mass,
                rigidbody.inertiaTensor,
                angularDriveInertiaMode);
        }

        #region Value Appliance
        /// <summary>
        /// Applies the current settings to the ragdoll (not needed for ScriptableObject-based properties).
        /// </summary>
        public void ApplySettings()
        {
            if (!bindings || !bindings.IsInitialized) return;

            ApplyLimitSettings();
            ApplyJointSettings();
            ApplyRigidbodySettings(true);
        }

        void ApplyLimitSettings()
        {
            foreach (RagdollBone bone in bindings.Bones)
            {
                ConfigurableJoint joint = bone.Joint;
                Rigidbody rb = bone.Rigidbody;

                //Limit springs
                SoftJointLimitSpring newLimitSpring = new SoftJointLimitSpring()
                {
                    spring = limitSpring * rb.mass,
                    damper = limitSpringDamping * rb.mass
                };
                joint.angularXLimitSpring = newLimitSpring;
                joint.angularYZLimitSpring = newLimitSpring;


                SoftJointLimit limit;
                float xDistance = joint.highAngularXLimit.limit - joint.lowAngularXLimit.limit;

                limit = joint.highAngularXLimit;
                limit.bounciness = limitBounciness;
                limit.contactDistance = xDistance * limitContactDistanceFactor;
                joint.highAngularXLimit = limit;

                limit = joint.lowAngularXLimit;
                limit.bounciness = limitBounciness;
                limit.contactDistance = xDistance * limitContactDistanceFactor;
                joint.lowAngularXLimit = limit;

                limit = joint.angularYLimit;
                limit.bounciness = limitBounciness;
                limit.contactDistance = limit.limit * limitContactDistanceFactor;
                joint.angularYLimit = limit;

                limit = joint.angularZLimit;
                limit.bounciness = limitBounciness;
                limit.contactDistance = limit.limit * limitContactDistanceFactor;
                joint.angularZLimit = limit;
            }
        }

        void ApplyJointSettings()
        {
            foreach (RagdollBone bone in bindings.Bones)
            {
                ConfigurableJoint joint = bone.Joint;

                joint.enableCollision = false;
                joint.enablePreprocessing = enableJointPreProcessing;

                if (enableJointProjection)
                {
                    joint.projectionMode = JointProjectionMode.PositionAndRotation;
                    joint.projectionDistance = minJointProjectionDistance;
                    joint.projectionAngle = minJointProjectionAngle;
                }
                else
                {
                    joint.projectionMode = JointProjectionMode.None;
                }
            }
        }

        void ApplyRigidbodySettings(bool applyInertiaTensor)
        {
            RagdollSolverQualitySettings solverSettings =
                GetEffectiveSolverQualitySettings();

            foreach (RagdollBone bone in bindings.Bones)
            {
                Rigidbody rb = bone.Rigidbody;

                rb.useGravity = useGravity;
                rb.drag = drag;
                rb.angularDrag = angularDrag;
                rb.interpolation = solverSettings.interpolation;

                rb.solverIterations = solverSettings.solverIterations;
                ApplyAdvancedRigidbodySettings(
                    rb,
                    solverSettings,
                    applyInertiaTensor);

                SetCollisionDetectionMode(
                    rb,
                    solverSettings.collisionDetectionMode,
                    rb.isKinematic);

                foreach (Collider collider in bone.Colliders)
                {
                    collider.sharedMaterial = material;
                }
            }
        }

        void ApplyAdvancedRigidbodySettings(
            Rigidbody rb,
            RagdollSolverQualitySettings solverSettings,
            bool applyInertiaTensor)
        {
            rb.solverVelocityIterations = solverSettings.solverVelocityIterations;
            rb.maxAngularVelocity = solverSettings.maxAngularVelocity;
            rb.maxDepenetrationVelocity = solverSettings.maxDepenetrationVelocity;

            if (overrideSleepThreshold)
            {
                rb.sleepThreshold = Mathf.Max(0f, sleepThreshold);
            }

            if (!applyInertiaTensor) return;

            switch (inertiaTensorMode)
            {
                case RagdollInertiaTensorMode.PreserveAuthored:
                    break;
                case RagdollInertiaTensorMode.ResetFromColliders:
                    rb.ResetInertiaTensor();
                    break;
                case RagdollInertiaTensorMode.ResetAndStabilize:
                    rb.ResetInertiaTensor();
                    rb.inertiaTensor = RagdollSolverMath.StabilizeInertiaTensor(
                        rb.inertiaTensor,
                        maximumInertiaTensorRatio);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(inertiaTensorMode),
                        inertiaTensorMode,
                        "Unsupported ragdoll inertia tensor mode.");
            }
        }

        RagdollSolverQualitySettings GetEffectiveSolverQualitySettings()
        {
            return hasRuntimeSolverOverride
                ? runtimeSolverOverride
                : RagdollSolverQualitySettings.FromAuthored(this);
        }

        void SetCollisionDetectionMode(Rigidbody rb, CollisionDetectionMode mode, bool isKinematic)
        {
            //Kinematic bodies only work with continuous collision detection if it's speculative.
            if (isKinematic && (mode == CollisionDetectionMode.Continuous || mode == CollisionDetectionMode.ContinuousDynamic))
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
            else
            {
                rb.collisionDetectionMode = mode;
            }
        }

        void OnBindingsInitialized()
        {
            ApplySettings();
            ApplyPowerProfile();
            ApplyWeightDistribution();
        }

        void ApplyPowerProfile()
        {
            if (!bindings || !bindings.IsInitialized) return;
            if (!PowerProfile || !PowerProfile.IsValid) return;

            CollisionDetectionMode effectiveCollisionMode =
                GetEffectiveSolverQualitySettings().collisionDetectionMode;

            foreach (RagdollBone bone in bindings.Bones)
            {
                PowerSetting powerSetting = _powerProfile.GetBoneSetting(bone.Name);

                switch (powerSetting)
                {
                    case PowerSetting.Kinematic:
                        SetCollisionDetectionMode(
                            bone.Rigidbody,
                            effectiveCollisionMode,
                            true);
                        bone.Rigidbody.isKinematic = true;
                        break;
                    case PowerSetting.Powered:
                    case PowerSetting.Unpowered:
                        bone.Rigidbody.isKinematic = false;
                        SetCollisionDetectionMode(
                            bone.Rigidbody,
                            effectiveCollisionMode,
                            false);
                        break;
                }

                bone.PowerSetting = powerSetting;
            }
        }

        void ApplyWeightDistribution()
        {
            if (!bindings || !bindings.IsInitialized) return;
            if (!WeightDistribution || !WeightDistribution.IsValid) return;

            foreach (RagdollBone bone in bindings.Bones)
            {
                bone.Rigidbody.mass = _weightDistribution.GetBoneMass(bone.Name, totalMass);
            }

            //As we change RBs' mass, the springs have to be scaled. ApplySettings takes care of that.
            ApplySettings();
        }
        #endregion

        #region Auto Value Updating
        void OnValidate()
        {
            solverIterations = Mathf.Max(1, solverIterations);
            solverVelocityIterations = Mathf.Max(1, solverVelocityIterations);
            maxAngularVelocity = Mathf.Max(0f, maxAngularVelocity);
            maxDepenetrationVelocity = Mathf.Max(0f, maxDepenetrationVelocity);
            sleepThreshold = Mathf.Max(0f, sleepThreshold);
            maximumInertiaTensorRatio = Mathf.Max(1f, maximumInertiaTensorRatio);

            //There is some redundant applying here, but it doesn't really matter. OnValidate() is not used outside the editor anyway; and the redundancy makes the profiles work with Undo.
            ApplyWeightDistribution();
            ApplySettings();
            ApplyPowerProfile();
        }
        #endregion

        #region Lifetime
        void Start()
        {
            bindings.SubscribeToOnBonesCreated(OnBindingsInitialized);
        }

        void OnEnable()
        {
            //This would be in Awake, but fast nter play mode and [ExecuteAlways] makes Awake not be called.
            bindings = GetComponent<RagdollDefinitionBindings>();

            if (Application.isPlaying) ValidateInspectorReferences();

            ApplySettings();
            ApplyPowerProfile();
            ApplyWeightDistribution();

            if (_powerProfile) _powerProfile.OnUpdateValues += ApplyPowerProfile;
            if (_weightDistribution) _weightDistribution.OnUpdateValues += ApplyWeightDistribution;
        }

        void ValidateInspectorReferences()
        {
            try
            {
                RagdollProfile.ValidateAsInspectorField(_powerProfile, bindings.Definition, true, "A RagdollPowerProfile must be assigned in RagdollSettings.");
                RagdollProfile.ValidateAsInspectorField(_weightDistribution, bindings.Definition, true, "A RagdollWeightDistribution must be assigned in RagdollSettings.");
            }
            catch (System.Exception e)
            {
                enabled = false;
                throw e;
            }
        }

        void OnDisable()
        {
            //Unsubscribe from OnUpdateValues events.
            if (_powerProfile) _powerProfile.OnUpdateValues -= ApplyPowerProfile;
            if (_weightDistribution) _weightDistribution.OnUpdateValues -= ApplyWeightDistribution;
        }

        void OnDestroy()
        {
            bindings.UnsubscribeFromOnBonesCreated(OnBindingsInitialized);
        }
        #endregion
    }
}