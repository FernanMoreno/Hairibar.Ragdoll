using System;
using System.Collections.Generic;
using System.Linq;

namespace Hairibar.Ragdoll.Animation.Editor
{
    /// <summary>
    /// Immutable normative contract for one certification capability. Evidence
    /// kinds use AND semantics: every listed kind must be current and valid.
    /// </summary>
    public sealed class RagdollCapabilityContract
    {
        readonly Dictionary<RagdollEvidenceKind, string> exactNUnitEvidenceTests =
            new Dictionary<RagdollEvidenceKind, string>();

        internal RagdollCapabilityContract(
            string id,
            string officialSource,
            string sourceLocator,
            string observableClaim,
            string affectedApis,
            bool isApplicable,
            string exclusionReason,
            params RagdollEvidenceKind[] requiredEvidence)
        {
            Id = id;
            OfficialSource = officialSource;
            SourceLocator = sourceLocator;
            ObservableClaim = observableClaim;
            AffectedApis = affectedApis;
            IsApplicable = isApplicable;
            ExclusionReason = exclusionReason;
            RequiredEvidence = Array.AsReadOnly(
                (RagdollEvidenceKind[])requiredEvidence.Clone());
        }

        public string Id { get; }
        public string OfficialSource { get; }
        public string SourceLocator { get; }
        public string ObservableClaim { get; }
        public string AffectedApis { get; }
        public bool IsApplicable { get; }
        public string ExclusionReason { get; }
        public IReadOnlyList<RagdollEvidenceKind> RequiredEvidence { get; }
        public IReadOnlyDictionary<RagdollEvidenceKind, string>
            ExactNUnitEvidenceTests => exactNUnitEvidenceTests;

        internal void SetExactNUnitEvidenceTest(
            RagdollEvidenceKind kind,
            string fullName)
        {
            if (kind != RagdollEvidenceKind.NUnitEditMode
                && kind != RagdollEvidenceKind.NUnitPlayMode)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException(
                    "An exact NUnit evidence test name is required.",
                    nameof(fullName));
            if (!RequiredEvidence.Contains(kind))
                throw new InvalidOperationException(
                    $"{Id} does not require {kind} evidence.");
            exactNUnitEvidenceTests.Add(kind, fullName);
        }
    }

    /// <summary>
    /// Code-owned inventory of all 140 public-documentation capabilities. It is
    /// intentionally independent from certification Markdown and its status
    /// columns, so documentation can never certify itself.
    /// </summary>
    public static class RagdollCapabilityCatalog
    {
        public const int ExpectedCount = 140;

        const string Creation = "http://www.root-motion.com/puppetmasterdox/html/page1.html";
        const string Editing = "http://www.root-motion.com/puppetmasterdox/html/page2.html";
        const string Setup = "http://www.root-motion.com/puppetmasterdox/html/page4.html";
        const string PuppetMaster = "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_puppet_master.html";
        const string Behaviours = "http://www.root-motion.com/puppetmasterdox/html/page9.html";
        const string BehaviourPuppet = "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html";
        const string BehaviourFall = "http://www.root-motion.com/puppetmasterdox/html/class_root_motion_1_1_dynamics_1_1_behaviour_fall.html";
        const string Props = "http://www.root-motion.com/puppetmasterdox/html/page6.html";
        const string Ik = "http://www.root-motion.com/puppetmasterdox/html/page7.html";
        const string Performance = "http://www.root-motion.com/puppetmasterdox/html/page8.html";
        const string Baker = "http://www.root-motion.com/puppetmasterdox/html/page12.html";
        const string UnityUndo = "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Undo.html";
        const string UnityAnimator = "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.html";
        const string UnityBuild = "https://docs.unity3d.com/6000.0/Documentation/Manual/build-script-build.html";
        const string UnityProfiler = "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html";
        const string UnityTests = "https://docs.unity3d.com/6000.0/Documentation/Manual/testing-editortestsrunner.html";

        static readonly IReadOnlyList<RagdollCapabilityContract> contracts = Build();
        static readonly IReadOnlyDictionary<string, RagdollCapabilityContract> byId =
            contracts.ToDictionary(contract => contract.Id, StringComparer.Ordinal);

        public static IReadOnlyList<RagdollCapabilityContract> Contracts => contracts;

        public static bool TryGet(string id, out RagdollCapabilityContract contract)
        {
            return byId.TryGetValue(id ?? string.Empty, out contract);
        }

        public static RagdollCapabilityContract Get(string id)
        {
            if (!TryGet(id, out RagdollCapabilityContract contract))
                throw new KeyNotFoundException("Unknown ragdoll capability ID '" + id + "'.");
            return contract;
        }

        static IReadOnlyList<RagdollCapabilityContract> Build()
        {
            var result = new List<RagdollCapabilityContract>(ExpectedCount);

            Add(result, "A", Creation, RagdollEvidenceKind.NUnitEditMode, new[]
            {
                S(Setup + "; " + UnityUndo, "PuppetMaster Setup / Creating a Puppet", "A wizard atomically creates separate Target and Puppet hierarchies, controllers, behaviours and bindings, and Undo or failure removes the entire transaction.", "RagdollSetupWizard; RagdollRuntimeSetupService; Undo"),
                S("Biped references / Humanoid", "Automatic reference discovery accepts a valid Humanoid Avatar and resolves every required biped binding without depending on transform names.", "RagdollHumanoidReferences.TryFromHumanoid; Avatar"),
                S("Ragdoll creation / colliders", "Automatic authoring creates a finite, non-degenerate collider on the intended included bones, including small and non-uniformly scaled rigs.", "RagdollRuntimeAuthoring; Collider"),
                S("Ragdoll creation / joints and mass", "Automatic authoring creates an acyclic ConfigurableJoint topology with valid axes and limits, and normalized regional masses sum exactly to totalMass.", "RagdollRuntimeAuthoring; RagdollBiometrics; ConfigurableJoint"),
                S(Creation + "; " + Editing + "; " + UnityUndo, "Manual setup", "Guided manual setup creates the selected colliders and joints and records the complete operation for Undo and Redo.", "RagdollSetupWizard; Undo"),
                S(Editing + "; " + UnityUndo, "Ragdoll editor", "Scene editing changes collider geometry, joint axes and limits symmetrically in one undoable operation.", "RagdollAuthoringEditor; Undo"),
                S(Editing + "; " + UnityUndo, "Collider conversion", "Box, Capsule and Sphere conversion preserves center, material, trigger and layer; collider rotation remains valid and the conversion is undoable.", "RagdollAuthoringEditor; Collider; Undo"),
                S(Editing + "; " + UnityUndo, "Joint editing", "Visual axis and connectedBody editing rejects unsupported or invalid bindings before mutating the authored rig.", "RagdollAuthoringEditor; ConfigurableJoint; Undo"),
                S(Setup, "PuppetMaster Setup / layers", "Setup applies Target and Puppet layers recursively, validates the collision matrix and restores both objects and matrix on rollback.", "RagdollSetupWizard; RagdollRuntimeSetupService; Physics.IgnoreLayerCollision"),
                S(Setup, "Runtime setup", "Runtime setup supports duplicate-as-Target, separate Target/Puppet and direct-Puppet entry points without leaving partial owned objects after failure.", "RagdollRuntimeSetupService"),
                S(PuppetMaster, "Hierarchy", "Flat and tree conversion includes the root and preserves world pose, connected bodies, handles and unrelated external objects.", "RagdollAnimator.FlattenHierarchy; RagdollAnimator.RestoreHierarchy"),
                S(Setup, "PuppetMaster Setup / alternate Target", "One Puppet can bind to an alternate semantically compatible Target whose bone orientations and names differ, while preserving the documented dual-rig contract.", "RagdollRuntimeSetupService; RagdollAnimator"),
            });

            Add(result, "B", PuppetMaster, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("targetRoot and muscles", "Initialization maintains distinct animated Target and physical Puppet hierarchies with complete root and muscle bindings.", "RagdollAnimator.Initialize; TargetRoot; Muscles"),
                S("muscles", "A partial ragdoll with valid ConfigurableJoints initializes and maps only its registered muscles with a valid parent topology.", "RagdollAnimator; RagdollMuscle"),
                S("Update pipeline", "Each simulation step reads Target animation, applies matching before physics, then maps the resulting Puppet pose back in documented order.", "OnRead; matching; mapping; OnWrite"),
                S("Mode", "Active, Kinematic and Disabled modes produce their documented Rigidbody and mapping states.", "RagdollAnimator.Mode; RagdollSimulationModeController"),
                S("SwitchMode", "Mode blending completes without discontinuity, incompatible writes or orphaned intermediate ownership.", "RagdollSimulationModeController; IsBlending; IsSwitchingMode"),
                S("quality and updateMode", "Quality LOD and a shared simulation budget reduce and restore work deterministically after lifecycle overrides.", "RagdollQualityController; RagdollSimulationBudget"),
                S("State", "Alive, Dead and Frozen transitions, including temporary and permanent freeze, preserve a restorable physical snapshot.", "RagdollAnimator.State; Kill; Freeze; Resurrect"),
                S("Kill", "Kill blends to the configured dead muscle weight and damper and Resurrect restores the authored drives.", "RagdollAnimator.Kill; Resurrect; MasterMuscleWeight; MasterMuscleDamper"),
                S("Freeze", "Velocity-gated temporary freeze and permanent freeze enter only under their documented conditions and restore correctly.", "RagdollAnimator.Freeze; Resurrect"),
                S("Internal collisions and joint break", "Death applies the configured angular-limit and internal-collision policy, while a PhysX-broken joint remains irreversibly broken until explicit reconstruction.", "RagdollAnimator.Kill; ConfigurableJoint; OnJointBreak"),
                S("fixTargetTransforms", "Fix Target Transforms restores only unanimated Target transforms at the documented lifecycle hook.", "FixTargetTransforms; OnFixTransforms"),
                S("mappingWeight, pinWeight, muscleWeight", "Global mapping, pin and muscle weights remain independently configurable for all zero/one combinations.", "MasterMappingWeight; MasterPinWeight; MasterMuscleWeight"),
                S("muscleSpring and muscleDamper", "Muscle spring and absolute damper are written to the corresponding ConfigurableJoint angular drive.", "MasterMuscleWeight; MasterMuscleDamper; JointDrive"),
                S("pinPow", "pinPow shapes only interpolation between zero and one and remains finite for sanitized inputs.", "PinPower; RagdollPinMath"),
                S("pinDistanceFalloff", "Pinning force decreases with Target-to-muscle distance according to the configured distance falloff.", "PinDistanceFalloff; RagdollPinMath"),
                S("angularPinning", "Angular pinning can operate independently of muscle drive and linear pinning.", "AngularPinning; MasterPinWeight; MasterMuscleWeight"),
                S("UpdateJointAnchors", "Runtime anchor updates commit on a physics boundary and restore the authored anchor snapshot.", "UpdateJointAnchors; ConfigurableJoint.anchor; connectedAnchor"),
                S("mapping translation", "Animated local translation is mapped with authored offsets without overwriting mapped rotation.", "RagdollMuscle.Mapping; AnimatedTarget"),
                S("angular limits", "Global and per-muscle angular-limit control update physical joints and restore the prior policy.", "SetAngularLimits; ConfigurableJoint"),
                S("internal collisions", "Global and per-muscle internal-collision control updates ignores and restores the authored collision state.", "SetInternalCollisions; Physics.IgnoreCollision"),
                S("muscle properties", "Weights can be assigned independently by muscle, group and recursive branch without affecting unrelated muscles.", "SetMuscleWeights; SetGroupWeights; SetBranchWeights"),
                S("Humanoid config", "A semantic Humanoid profile is shareable by two compatible valid Avatars without name-based binding.", "RagdollHumanoidProfile; Avatar"),
                S("updateMode and Animator control", "Normal, AnimatePhysics, UnscaledTime and manual simulation honor timeScale and never let Animator and Legacy Animation drive Target simultaneously.", "EffectiveUpdateMode; TargetAnimator; TargetAnimation; PrepareManualSimulation"),
                S("callbacks", "OnRead, OnWrite, OnFixTransforms, OnPostLateUpdate and OnPostInitialized execute in pipeline order and isolate each subscriber exception.", "OnRead; OnWrite; OnFixTransforms; OnPostLateUpdate; OnPostInitialized"),
                S("Teleport", "Teleport and Respawn behave coherently from Puppet, Unpinned, GetUp, Dead and Frozen and apply the operation's velocity policy.", "RagdollAnimator.Teleport; RagdollPuppetBehaviour.Respawn"),
                S("runtime muscles", "Atomic collection and branch mutations preserve compatible held props and additional pin, invalidate removed handles and fully roll back physical state on failure.", "TrySetMuscles; TryReplaceMuscles; AddMuscle; RemoveMuscleRecursive"),
                S("disconnect and reconnect", "Disconnect and reconnect preserve an explicit unmapped state and resolve the nearest continuous ancestor on reconnection.", "DisconnectMuscle; ReconnectMuscle"),
                S("joint break", "A real PhysX joint break invalidates the affected physical connection once and emits one hierarchy notification.", "OnJointBreak; RagdollMuscleHandle"),
                S("flat hierarchy", "Runtime flat/tree conversion preserves topology, connected bodies and pose and reports every root-inclusive muscle.", "FlattenHierarchy; RestoreHierarchy; HierarchyIsFlat"),
                S("visualize target pose", "Runtime visualization renders the current Target pose and bindings without changing simulation state.", "RagdollTargetPoseVisualizer"),
            });

            Add(result, "C", Behaviours, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("Behaviour base class", "A behaviour initializes once and receives its complete activation, simulation and deactivation lifecycle callbacks in order.", "RagdollBehaviour; OnPostInitialized"),
                S("Behaviour switching", "Exactly one behaviour is active and a failed or reentrant switch restores the previous active behaviour.", "RagdollBehaviourController.SwitchBehaviour"),
                S("Behaviour context", "Runtime setup injects the correct Animator, muscles, animated pairs, collision hub and Puppet context without external authored references.", "RagdollBehaviourContext"),
                S("Behaviour events", "Serialized animation, switch and Unity events fire once at the documented phase and remain deterministic.", "RagdollBehaviourEvent"),
                S("Sub-behaviours", "Reusable sub-behaviours activate once, deactivate cleanly and isolate one sub-behaviour exception from the others.", "RagdollSubBehaviour; RagdollCenterOfMassSubBehaviour"),
                S("Collision broadcasting", "One observed collision is centrally dispatched once in stable order under the per-step budget.", "RagdollCollisionHub; CollisionObserved"),
                S("Reactivation and teleport", "Behaviour reactivation and teleport hooks reconcile current state once and isolate all subscribers.", "RagdollBehaviour.OnReactivate; OnTeleport"),
            });

            Add(result, "D", BehaviourPuppet, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("State", "Explicit and automatic transitions select Puppet, Unpinned and GetUp and emit each corresponding callback once.", "RagdollPuppetBehaviour.State; TryBeginGetUp"),
                S("knockOutDistance", "A muscle exceeding its effective Target distance threshold transitions the behaviour to Unpinned.", "KnockOutDistance; LastKnockOutDistance"),
                S("collisionResistance", "An accepted physical collision reduces pin authority inside BehaviourPuppet.", "CollisionAccepted; CollisionUnpinApplied"),
                S("normalMode Active", "NormalMode Active retains dynamically simulated muscles while Puppet state is balanced.", "NormalMode; RagdollSimulationMode.Active"),
                S("normalMode Unmapped", "NormalMode Unmapped suspends mapping only while recent qualifying contact requires it and expires on physics steps.", "NormalMode.Unmapped"),
                S("normalMode Kinematic", "NormalMode Kinematic activates into physics only for a qualifying contact.", "NormalMode.Kinematic"),
                S("mappingBlendSpeed", "Mapping authority approaches its requested value in units per second without overshoot.", "MappingBlendSpeed"),
                S("activateOnStaticCollisions", "Static colliders activate the Puppet only when the authored activation option and layers allow it.", "ActivateOnStaticCollisions"),
                S("minimumImpulse", "A contact impulse activates the Puppet only at or above the finite configured minimum.", "MinimumImpulse"),
                S("groundLayers", "Grounding accepts only ground layers and remains correct on slopes and with arbitrary gravity.", "GroundLayers; GroundingSnapshot"),
                S("collisionLayers", "Collision processing accepts configured collision layers and rejects every excluded layer.", "CollisionLayers"),
                S("collisionThreshold", "Collision impulse below threshold is observed but not accepted or applied as unpin damage.", "CollisionThreshold; CollisionObserved; CollisionAccepted"),
                S("collisionResistance", "Global and muscle-group resistance scale accepted unpin damage independently.", "CollisionResistance; GroupCollisionResistance"),
                S("collisionResistanceByVelocity", "Target velocity evaluates the documented collision-resistance curve before unpin damage.", "CollisionResistanceByVelocity"),
                S("collisionResistanceMultipliers", "Layer multipliers and thresholds alter only contacts on their configured layer.", "CollisionResistanceMultiplier"),
                S("maxCollisions", "At most maxCollisions contacts apply damage in one physics step while all are still observable.", "MaxCollisions"),
                S("regainPinSpeed", "Each muscle group regains pin authority at its configured finite speed.", "RegainPinSpeed"),
                S("muscleWeight", "Unpinned muscle drive remains independently controllable relative to pin authority.", "UnpinnedMuscleWeightMultiplier; MasterPinWeight"),
                S("BoostImmunity", "Immunity boost starts, renews, falls off and expires without changing unrelated groups.", "BoostImmunity"),
                S("BoostImpulseMlp", "Impulse multiplier boost starts, renews, falls off and expires without changing unrelated groups.", "BoostImpulseMultiplier"),
                S("minMappingWeight", "A group minimum mapping weight clamps only that group.", "MinimumMappingWeight"),
                S("maxMappingWeight", "A group maximum mapping weight clamps only that group.", "MaximumMappingWeight"),
                S("minPinWeight", "A group minimum pin or position authority clamps only that group.", "MinimumPinWeight"),
                S("disableColliders", "Only configured Puppet-state muscle-group colliders are disabled and authored-disabled colliders stay disabled.", "DisabledColliderGroups"),
                S("PhysicMaterials", "Puppet, Unpinned and GetUp apply their configured PhysicMaterials and every exit path restores the exact authored material.", "SetColliderSurfaceState; PuppetMaterial; UnpinnedMaterial"),
                S("maxRigidbodyVelocity", "Entering Unpinned clamps finite Rigidbody velocity to the configured maximum without corrupting extreme valid masses.", "MaxRigidbodyVelocity"),
                S("pinWeightThreshold", "Knockout occurs only when effective pin weight is strictly below the documented threshold; non-finite runtime inputs are sanitized.", "PinWeightThreshold"),
                S("unpinnedMuscleKnockout", "Authored zero muscle pin triggers knockout only when unpinnedMuscleKnockout is enabled.", "UnpinnedMuscleKnockout"),
                S("unpinnedMuscleWeightMlp", "Unpinned state applies and later restores its muscle-weight multiplier.", "UnpinnedMuscleWeightMultiplier"),
                S("dropProps", "Losing balance drops held props exactly when the dropProps policy requests it.", "DropProps; RagdollProp"),
                S("canGetUp", "Automatic GetUp begins only when all configured recovery gates permit it.", "CanGetUp; TryBeginGetUp"),
                S("getUpDelay", "GetUp cannot begin before the configured unpinned delay.", "GetUpDelay"),
                S("blendToAnimationTime", "GetUp mapping blend completes over the configured finite duration.", "BlendToAnimationTime"),
                S("maxGetUpVelocity", "GetUp waits until the hip Rigidbody velocity is strictly below the configured maximum.", "MaxGetUpVelocity"),
                S("minGetUpDuration", "GetUp cannot finish before its independent minimum duration.", "MinimumGetUpDuration"),
                S("getUpCollisionResistanceMlp", "GetUp multiplies collision resistance and restores the prior resistance afterward.", "GetUpCollisionResistanceMultiplier"),
                S("getUpRegainPinSpeedMlp", "GetUp multiplies regain-pin speed and restores the prior speed afterward.", "GetUpRegainPinSpeedMultiplier"),
                S("getUpKnockOutDistanceMlp", "GetUp multiplies knockout distance and restores the prior threshold afterward.", "GetUpKnockOutDistanceMultiplier"),
                S("getUpOffsetProne/Supine", "Prone, supine and quadruped classification selects the matching offset and aligns Target against effective up.", "ProneGetUpOffset; SupineGetUpOffset; QuadrupedGetUp"),
                S("onGetUpProne/onGetUpSupine", "Biped or quadruped recovery emits exactly one event matching its classified GetUp orientation.", "OnGetUpProne; OnGetUpSupine"),
                S("onLoseBalance/onRegainBalance", "Balance loss and regain events fire once and retain the origin of their transition.", "OnLoseBalance; OnRegainBalance"),
                S("canMoveTarget", "GetUp preserves external root ownership when canMoveTarget is false and spatially reconciles the local Target when true.", "CanMoveTarget"),
                S("OnTeleport", "Teleport from Update, FixedUpdate, LateUpdate and manual simulation preserves a valid GetUp transaction.", "OnTeleport; RagdollAnimator.Teleport"),
                S("SetColliders", "Disable, exception, death and respawn restore collider enabled states and materials from the authored snapshot.", "SetColliderSurfaceState; Respawn"),
                S("COM", "Each physics step reports finite total mass, center and velocity of mass, grounded and temporal stability.", "RagdollCenterOfMassSubBehaviour; RagdollGroundingSnapshot"),
                S("centerOfPressure", "Multiple valid contacts produce finite pressure center, pressure-to-COM vector, direction, magnitude and angle; empty cases produce finite zero results.", "RagdollGroundingSnapshot.CenterOfPressure; PressureToCenterOfMass"),
            });

            Add(result, "E", BehaviourFall, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("BehaviourFall", "The Target Animator enters the configured Fall state, updates its height/velocity blend, obeys runtime parameters and invokes onEnd once only after both time and velocity gates pass.", "RagdollFallBehaviour; TargetAnimator"),
            }, UnityAnimator);

            Add(result, "F", Props, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("PropMuscle", "A physical prop slot registers against exactly one compatible hand muscle.", "RagdollPropMuscle; RagdollPropSlot"),
                S("Pickup/Drop/Switch", "Pickup, owner switch and drop commit atomically and fully roll back an invalid transfer.", "RagdollProp.PickUp; Drop; SwitchOwner"),
                S("propRoot", "Pickup parents the physical prop root to the PropMuscle and the visual Mesh Root to its animated Target while preserving the authored local mesh alignment; drop restores the original hierarchy.", "RagdollProp.MeshRoot"),
                S("Rigidbody", "CurrentRigidbody reports the body with physical ownership throughout standalone, transition and held states and never returns a destroyed body.", "RagdollProp.CurrentRigidbody"),
                S("mass/layer/material", "Pickup overrides and drop exactly restores Rigidbody mass, collider layer and PhysicMaterial.", "RagdollProp; RagdollPropMuscle"),
                S("internal collision ignores", "Internal ignores are acquired once per ownership and all released on drop or rollback.", "RagdollProp; Physics.IgnoreCollision"),
                S("animatedTargetChildren", "Animated target children remain bound to the held prop across compatible muscle replacement.", "RagdollProp.AnimatedTargetChildren"),
                S("disconnect/reconnect", "A held prop rebinds through compatible disconnect/reconnect and rolls back or drops explicitly when incompatible.", "DisconnectMuscle; ReconnectMuscle; RagdollProp"),
                S("additionalPin", "Additional pin can be added and removed while held on the next physics boundary.", "AddAdditionalPin; RemoveAdditionalPin"),
                S("additionalPin offset/weight/mass", "Runtime additional-pin offset, absolute weight and mass changes commit together and roll back on invalid ownership.", "RagdollAdditionalPin"),
                S("dropProps", "BehaviourPuppet drop completes prop restoration before any hierarchy disconnect.", "RagdollPuppetBehaviour.DropProps; RagdollProp.Drop"),
                S("PropMelee collider", "A held melee prop uses Capsule for its entire held state and restores Box on drop.", "RagdollPropMelee"),
                S("PropMelee action", "A timed melee action temporarily multiplies Capsule radius and Rigidbody mass, sets the additional-pin weight, supports restart, and restores on every cancellation path. RootMotion documents no action-specific PhysicMaterial change.", "RagdollPropMelee.StartAction; BeginAction; EndAction"),
                S("centerOfMass", "Authored prop center of mass applies during physical ownership and restores exactly after action and drop.", "RagdollProp.CenterOfMass"),
            });

            Add(result, "G", Ik, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("pre-physics pose modification", "A registered pose modifier changes the Target pose read by matching before physics.", "IRagdollPoseModifier; OnRead"),
                S("post-mapping callback", "A deterministic external solver callback runs after mapping and observes the written Puppet pose.", "IRagdollIKSolver; OnWrite"),
                S("IK scheduling", "Multiple solvers execute by configured phase and stable order, and one solver exception does not block later solvers.", "RagdollIKScheduler; IRagdollIKSolver"),
                S("public read/write hooks", "Public OnRead and OnWrite hooks execute at their documented boundaries and isolate every subscriber.", "OnRead; OnWrite"),
                S("Final IK integration", "Final IK dependency is deliberately excluded; the generic solver contract remains executable and Animator IK remains covered elsewhere.", "IRagdollIKSolver; Animator IK"),
            }, UnityAnimator);
            MarkNotApplicable(result, "G05",
                "The optional Final IK package dependency is deliberately excluded. " +
                "Generic IRagdollIKSolver and Unity Animator IK remain applicable " +
                "under G01-G04 and I08-I09.");

            Add(result, "H", Performance, RagdollEvidenceKind.NUnitPlayMode, new[]
            {
                S("solverIterationCount", "Configured solver position and velocity iterations apply to every active body and restore afterward.", "RagdollQualityProfile; Rigidbody.solverIterations"),
                S("maxAngularVelocity/maxDepenetrationVelocity/inertiaTensor", "Finite velocity, depenetration and inertia limits apply under valid extremes without non-finite state.", "RagdollQualityProfile; Rigidbody"),
                S("kinematic/disabled/LOD", "Kinematic, Disabled and LOD budget transitions leave no active orphan bodies and restore quality.", "RagdollQualityController; RagdollSimulationModeController"),
                S("collisionThreshold/maxCollisions", "Threshold and per-step collision budget bound accepted work under saturated contacts.", "CollisionThreshold; MaxCollisions"),
                S("flat hierarchy", "Player profiling compares root-inclusive flat and tree layouts made through public APIs using real Puppets.", "FlattenHierarchy; TreeHierarchy; HierarchyIsFlat"),
                S("muscle reduction", "An explicitly authored reduced Puppet uses fewer registered muscles while preserving every retained root-to-active topology path. RootMotion recommends reducing muscle count but does not define a dynamic LOD-removal algorithm; selection of the reduced rig is Hairibar design.", "RagdollDefinitionBindings; RagdollRuntimeSetupService"),
                S("collision broadcasters", "Collision relays with no consumers remain disabled and dispatching an empty hub allocates no managed memory after warm-up.", "RagdollCollisionRelay; RagdollCollisionHub"),
                S("benchmarks", "Development Player profiles 1/10/25/50 real Puppets in Active-tree, Active-flat, Kinematic and Disabled, recording 120 warm-up and 600 measured frames, median/p95 CPU and memory, and critical-path GC.", "HairibarCertification; ProfilerRecorder"),
            }, UnityProfiler);
            RequireAdditional(result, "H05", RagdollEvidenceKind.WindowsPlayerScenario, RagdollEvidenceKind.ProfilerResult);
            RequireAdditional(result, "H08", RagdollEvidenceKind.WindowsPlayerScenario, RagdollEvidenceKind.ProfilerResult);

            Add(result, "I", Baker, RagdollEvidenceKind.NUnitEditMode, new[]
            {
                S("Baker", "Baker validates, opens one segment, records and commits an inspectable clip; cancellation and error do not report success or alter the destination.", "RagdollBaker.StartBaking; CancelBaking; BakingResult"),
                S("Humanoid Baker", "A valid Humanoid Avatar records a usable Humanoid clip with its semantic bindings.", "RagdollBaker; Avatar"),
                S("Generic/Legacy Baker", "Generic and Legacy input/output preserve valid bindings and clip settings and close loop seams.", "RagdollBaker; Animation; Animator"),
                S("Animation Clips batch", "Manual batch sampling writes exactly t=0, each 1/frameRate interval and clip.length with no overshoot.", "RagdollBaker; PlayableGraph"),
                S("Animation States", "AnimationStates records valid base-layer Mecanim states and explicitly rejects absent states and Legacy Animation.", "RagdollBaker; Animator"),
                S("PlayableDirector", "Batch Director uses manual evaluation and restores original time, state and update mode after success, cancellation and exception.", "RagdollBaker; PlayableDirector"),
                S("Realtime", "Realtime records at most one sample per rendered frame using actual elapsed time and accepts missed frames without manufactured poses.", "RagdollBaker; RagdollAnimationRecorder"),
                S("Foot/Hand IK", "Humanoid baking writes inspectable curves for both feet and both hands when IK baking is enabled.", "RagdollBaker; HumanBodyBones"),
                S("animation layers", "A multilayer Humanoid controller records layer blending, Animator events, root motion and retargeting in Normal, AnimatePhysics and UnscaledTime across supported timeScale values.", "RagdollBaker; AnimatorController; AnimatorUpdateMode"),
                S("key reduction", "Muscle and IK key reduction use separate tolerances and preserve required first and final keys.", "RagdollBaker; MuscleKeyReduction; IkKeyReduction"),
            }, UnityAnimator);
            RequireAdditional(result, "I07", RagdollEvidenceKind.NUnitPlayMode, RagdollEvidenceKind.ProfilerResult);
            RequireExactNUnitTest(
                result,
                "I07",
                RagdollEvidenceKind.NUnitPlayMode,
                "Hairibar.Ragdoll.Animation.Tests." +
                "RagdollBakerRealtimeFramePlayModeEvidence." +
                "RealtimeSamplesAtMostOncePerRenderedFrame");
            RequireAdditional(result, "I09", RagdollEvidenceKind.NUnitPlayMode);
            RequireExactNUnitTest(
                result,
                "I09",
                RagdollEvidenceKind.NUnitPlayMode,
                "Hairibar.Ragdoll.Animation.Tests." +
                "RagdollHumanoidCapabilityDirectPlayModeTests." +
                "MultilayerAnimatorModesEventsRootMotionAndRetargeting");

            AddQuality(result);
            Validate(result);
            return result.AsReadOnly();
        }

        static void AddQuality(List<RagdollCapabilityContract> result)
        {
            result.Add(C("J01", UnityBuild, "BuildPipeline.BuildPlayer / BuildReport", "Runtime, Editor, Tests and Samples compile and Windows, Linux, macOS and WebGL Development builds produce successful BuildReports with no Hairibar-owned diagnostics.", "HairibarCertification.RunClosure; BuildPipeline", RagdollEvidenceKind.BuildReport));
            result.Add(C("J02", UnityTests, "EditMode test execution", "The complete EditMode result contains zero failed, skipped or inconclusive cases.", "HairibarCertification.RunClosure", RagdollEvidenceKind.NUnitEditMode));
            result.Add(C("J03", UnityTests, "PlayMode test execution", "The complete PlayMode result contains zero failed, skipped or inconclusive cases.", "HairibarCertification.RunClosure", RagdollEvidenceKind.NUnitPlayMode));
            result.Add(C("J04", UnityBuild, "Development Player regression scenes", "All four imported regression scenes have no missing scripts and execute their deterministic assertions in a Development Player.", "RegressionScenarioRunner; HairibarCertification", RagdollEvidenceKind.SceneArtifact, RagdollEvidenceKind.WindowsPlayerScenario));
            result.Add(C("J05", UnityProfiler, "ProfilerRecorder", "Player profiling records warm-up, sample count, median and p95 CPU/memory and zero post-warm-up allocation for declared critical paths.", "HairibarCertification; ProfilerRecorder", RagdollEvidenceKind.ProfilerResult, RagdollEvidenceKind.WindowsPlayerScenario));
            result.Add(C("J06", UnityTests, "independent manifest validation", "An independent second process validates 140 unique contracts, official sources, required current hashes and the provisional closure before the final manifest is issued.", "RagdollCapabilityCatalog; RagdollCoverageManifest", RagdollEvidenceKind.IndependentValidation));
            result.Add(C("J07", PuppetMaster, "public API reference and Unity serialization contracts", "Technical migration documentation maps every exposed compatibility API and serialized migration to a real symbol and an official source without claiming undocumented PuppetMaster behavior.", "Public API; FormerlySerializedAs; migration guide", RagdollEvidenceKind.DocumentationAudit));
        }

        static void Add(
            List<RagdollCapabilityContract> destination,
            string prefix,
            string source,
            RagdollEvidenceKind evidence,
            Spec[] specs,
            string secondarySource = null)
        {
            for (int index = 0; index < specs.Length; index++)
            {
                string id = prefix + (index + 1).ToString("00");
                Spec spec = specs[index];
                destination.Add(C(
                    id,
                    spec.Source ?? (secondarySource == null
                        ? source
                        : source + "; " + secondarySource),
                    spec.Locator,
                    spec.Claim,
                    spec.Apis,
                    evidence));
            }
        }

        static void RequireAdditional(
            List<RagdollCapabilityContract> contracts,
            string id,
            params RagdollEvidenceKind[] additional)
        {
            int index = contracts.FindIndex(contract => contract.Id == id);
            if (index < 0) throw new InvalidOperationException("Unknown contract " + id + ".");
            RagdollCapabilityContract current = contracts[index];
            RagdollEvidenceKind[] evidence = current.RequiredEvidence
                .Concat(additional)
                .Distinct()
                .ToArray();
            contracts[index] = C(current.Id, current.OfficialSource,
                current.SourceLocator, current.ObservableClaim,
                current.AffectedApis, evidence);
        }

        static void RequireExactNUnitTest(
            List<RagdollCapabilityContract> contracts,
            string id,
            RagdollEvidenceKind kind,
            string fullName)
        {
            int index = contracts.FindIndex(contract => contract.Id == id);
            if (index < 0) throw new InvalidOperationException("Unknown contract " + id + ".");
            contracts[index].SetExactNUnitEvidenceTest(kind, fullName);
        }

        static void MarkNotApplicable(
            List<RagdollCapabilityContract> contracts,
            string id,
            string reason)
        {
            int index = contracts.FindIndex(contract => contract.Id == id);
            if (index < 0) throw new InvalidOperationException("Unknown contract " + id + ".");
            RagdollCapabilityContract current = contracts[index];
            contracts[index] = new RagdollCapabilityContract(
                current.Id,
                current.OfficialSource,
                current.SourceLocator,
                current.ObservableClaim,
                current.AffectedApis,
                false,
                reason,
                Array.Empty<RagdollEvidenceKind>());
        }

        static RagdollCapabilityContract C(
            string id,
            string source,
            string locator,
            string claim,
            string apis,
            params RagdollEvidenceKind[] evidence)
        {
            return new RagdollCapabilityContract(
                id, source, locator, claim, apis, true, string.Empty, evidence);
        }

        static Spec S(string locator, string claim, string apis)
        {
            return new Spec(null, locator, claim, apis);
        }

        static Spec S(string source, string locator, string claim, string apis)
        {
            return new Spec(source, locator, claim, apis);
        }

        static void Validate(List<RagdollCapabilityContract> result)
        {
            if (result.Count != ExpectedCount)
                throw new InvalidOperationException(
                    "Capability catalog must contain exactly 140 contracts; found " +
                    result.Count + ".");

            string[] duplicates = result.GroupBy(contract => contract.Id)
                .Where(group => group.Count() != 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicates.Length != 0)
                throw new InvalidOperationException(
                    "Duplicate capability IDs: " + string.Join(", ", duplicates) + ".");

            foreach (RagdollCapabilityContract contract in result)
            {
                if (string.IsNullOrWhiteSpace(contract.OfficialSource)
                    || string.IsNullOrWhiteSpace(contract.SourceLocator)
                    || string.IsNullOrWhiteSpace(contract.ObservableClaim)
                    || string.IsNullOrWhiteSpace(contract.AffectedApis))
                    throw new InvalidOperationException(
                        "Capability " + contract.Id + " has an incomplete normative contract.");
                if (contract.Id == "G05")
                {
                    if (contract.IsApplicable
                        || contract.RequiredEvidence.Count != 0
                        || string.IsNullOrWhiteSpace(contract.ExclusionReason))
                        throw new InvalidOperationException(
                            "G05 must be the explicit optional Final IK exclusion.");
                }
                else if (!contract.IsApplicable
                    || contract.RequiredEvidence.Count == 0
                    || !string.IsNullOrEmpty(contract.ExclusionReason))
                    throw new InvalidOperationException(
                        "Capability " + contract.Id +
                        " must be applicable and have typed evidence requirements.");

                foreach (KeyValuePair<RagdollEvidenceKind, string> exact
                    in contract.ExactNUnitEvidenceTests)
                {
                    if (!contract.RequiredEvidence.Contains(exact.Key)
                        || string.IsNullOrWhiteSpace(exact.Value))
                        throw new InvalidOperationException(
                            "Capability " + contract.Id +
                            " has an invalid exact NUnit evidence requirement.");
                }
            }
        }

        readonly struct Spec
        {
            public Spec(string source, string locator, string claim, string apis)
            {
                Source = source;
                Locator = locator;
                Claim = claim;
                Apis = apis;
            }

            public string Source { get; }
            public string Locator { get; }
            public string Claim { get; }
            public string Apis { get; }
        }
    }
}
