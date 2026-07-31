# Migration guide for the PuppetMaster-documented closure

This package contains original Hairibar code. RootMotion's public PuppetMaster
documentation defines only the observable capability targets; Unity 6 documentation
defines Animator, Humanoid, PhysX, build and profiling mechanics. Any behavior not
specified by those sources is identified here as **diseño propio Hairibar**.

## Master authority

Use `MasterMappingWeight`, `MasterPinWeight`, `MasterMuscleWeight` and
`MasterMuscleDamper` as independent controls. The obsolete `MasterAlpha` alias writes
pin and muscle together so old serialized scenes retain their previous behavior.
Invalid numeric values are resolved at the application boundary.

## Lifecycle and animation

`OnPostInitialized` runs after initialization and, like `OnRead`, `OnWrite`,
`OnFixTransforms` and `OnPostLateUpdate`, isolates each subscriber exception.
`TargetAnimator`, `TargetAnimation` and `EffectiveUpdateMode` expose the active target
driver. Animator and Legacy `Animation` are mutually exclusive.

For scripted physics call `PrepareManualSimulation(deltaTime)`, then
`Physics.Simulate(deltaTime)`, then `CompleteManualSimulation()`. Invalid or duplicate
ordering throws. `Respawn(position, rotation)` restores lifecycle, physics, surfaces,
props and temporary boosts transactionally.

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

Final IK is intentionally not a dependency. External solvers implement
`IRagdollIKSolver` and are scheduled through the generic Hairibar hook contract.

## Certification

`HairibarCertification.RunAll` imports the demo sample, validates a real Humanoid
Avatar, generates the AnimatorController and builds Development players for Windows,
Linux, macOS and WebGL. Windows executes the four deterministic regression scenes.
Android is excluded when its Unity module is absent. CPU and memory values are
platform-local; Hairibar does not attribute universal performance thresholds to
RootMotion.
