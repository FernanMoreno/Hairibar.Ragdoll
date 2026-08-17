# Migration guide for the PuppetMaster-documented closure

This package contains original Hairibar code. RootMotion's public PuppetMaster
documentation defines only the observable capability targets; Unity 6 documentation
defines Animator, Humanoid, PhysX, build and profiling mechanics. Any behavior not
specified by those sources is identified here as **diseño propio Hairibar**.

Normative public references:

- RootMotion PuppetMaster pages and class reference:
  https://root-motion.com/puppetmasterdox/html/pages.html
- Unity 6 scripting and engine contracts: https://docs.unity3d.com/6000.0/

## Master authority

Use `MasterMappingWeight`, `MasterPinWeight`, `MasterMuscleWeight` and
`MasterMuscleDamper` as independent controls. The obsolete `MasterAlpha` alias writes
pin and muscle together so old serialized scenes retain their previous behavior.
Invalid numeric values are resolved at the application boundary.
`MasterMuscleDamper` is the absolute documented joint-drive damper;
`MasterMuscleDamperMultiplier` is the Hairibar compatibility multiplier. Use
`SetMuscleWeights` for the PuppetMaster-compatible argument order. The older
`SetAuthorityWeights` and `SetAuthorityWeightsRecursive` overloads remain Hairibar
compatibility APIs.
The complete overload family is `SetMuscleWeights` and
`SetMuscleWeightsRecursive`; overloads are inventoried by their complete reflected
signature, not merely by method name. `MasterDampingRatio` is the obsolete Hairibar
alias retained for source compatibility.
Renamed serialized fields retain their previous scene and prefab data through
Unity's `FormerlySerializedAs` migration metadata; split-semantics fields use an
explicit serialized version before the compatibility value is applied.

## Lifecycle and animation

`OnPostInitialized` runs after initialization and, like `OnRead`, `OnWrite`,
`OnFixTransforms` and `OnPostLateUpdate`, isolates each subscriber exception.
`TargetAnimator`, `TargetAnimation` and `EffectiveUpdateMode` expose the active target
driver. Animator and Legacy `Animation` are mutually exclusive.

For scripted physics call `PrepareManualSimulation(deltaTime)`, then
`Physics.Simulate(deltaTime)`, then `CompleteManualSimulation()`. Invalid or duplicate
ordering throws. `Respawn(position, rotation)` restores lifecycle, physics, surfaces,
props and temporary boosts transactionally.
`OnPreSimulate(deltaTime)` and `OnPostSimulate()` are compatibility names for the
same pre/post manual-simulation boundary.

`RagdollPuppetBehaviour.State` can explicitly select Puppet, Unpinned or GetUp.
`RagdollAnimator.Mode`, `IsActive`, `IsBlending`,
`IsSwitchingMode` and `Initiated` are the compatibility facade over Hairibar's
simulation controller. `SetColliderSurfaceState` applies the authored pinned or
unpinned collider surface. `OnCollision` aliases observed muscle collisions and
`OnCollisionImpulse` aliases effective pin-loss impulses.

## Runtime hierarchy

`TrySetMuscles` replaces the complete active registry and `TryReplaceMuscles` applies
explicit replacements. Both commit only during `FixedUpdate`, validate topology and
held prop slots before mutation, invalidate retired handles, preserve owned state and
roll back on failure. Existing leaf/branch APIs remain supported.

## Props and IK

`StartAction(float)` starts or restarts a timed melee action; zero ends on the next
safe physics boundary. `CurrentRigidbody` returns the body that physically owns the
prop. Additional pin can be changed using `AddAdditionalPin` and
`RemoveAdditionalPin` while held.
`BeginAction()` and `EndAction()` remain the manual compatibility alternative to the
timed action.

Final IK is intentionally not a dependency. External solvers implement
`IRagdollIKSolver` and are scheduled through the generic Hairibar hook contract.

## Serialization migrations

Unity's `FormerlySerializedAs` contract preserves the exact historical serialized
name. J07 inventories every occurrence from every compiled package Runtime assembly;
the current mapping is `canGetUp <- automaticGetUp` on
`RagdollPuppetBehaviour`. Both the current field and `automaticGetUp` are part of the
audited identity, so adding, removing or renaming a migration invalidates existing
evidence until the documentation audit is regenerated.

## Baker compatibility

`inheritClipSettings` is the obsolete source-compatible alias for
`clipSettingsPolicy`. New code selects `PreserveDestination`, `InheritSource` or
`UseDefaults` explicitly; the alias is retained only so existing callers compile.

## Runtime setup compatibility

The `ConfigureSeparated` overload without a Humanoid binding profile is retained for
legacy name-based Target migration. New Humanoid integrations should use the semantic
binding-profile overload, which does not assume matching Transform names or axes.

## Certification

`HairibarCertification.RunAll` imports the demo sample, validates a real Humanoid
Avatar, generates the AnimatorController and builds Development players for Windows,
Linux, macOS and WebGL. Windows executes the four deterministic regression scenes.
Android is excluded when its Unity module is absent. CPU and memory values are
platform-local; Hairibar does not attribute universal performance thresholds to
RootMotion.
