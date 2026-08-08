# API pública — referencia rápida

Referencia orientativa de las entradas públicas principales. La firma exacta y
los genéricos deben consultarse en IntelliSense y en los XML de cada assembly.

| Área | Entradas principales |
|---|---|
| Animator | `TargetAnimator`, `TargetAnimation`, `EffectiveUpdateMode`, `MasterAlpha`, `MasterPinWeight`, `MasterMuscleWeight`, `MasterMuscleDamper` |
| Hooks | `OnPostInitialized`, `OnPostLateUpdate`, modificadores ordenados |
| Matching | `SetAuthorities`, `SetMappingAuthorities`, `SetDriveMultipliers`, `SetAuthorityWeights` |
| Modes | `SetMode`, `SetModeImmediate`, `PrepareManualSimulation`/`OnPreSimulate`, `CompleteManualSimulation`/`OnPostSimulate`, `FlattenHierarchy`, `TreeHierarchy`, `FixMusclePositions`, `FixMusclePositionsAndRotations` |
| Puppet | `Unpin`/`LoseBalance`, `IsProne`, `TryBeginGetUp`, `Reset`/`Respawn`, `SetColliders`/`SetColliderSurfaceState`, `QuadrupedGetUp` |
| Collisions | `CollisionObserved`, `CollisionAccepted`, `CollisionUnpinApplied`, `CollisionReported` |
| Hierarchy | `TrySetMuscles`, `TryReplaceMuscles`, `TryAddMuscle`, `TryReplaceMuscle`, `TryRemoveMuscleRecursive`, `TryDisconnectMuscleRecursive`, `TryReconnectMuscleRecursive` |
| Props | `GetRigidbody`/`CurrentRigidbody`, `CurrentMuscle`, `AddAdditionalPin`, `RemoveAdditionalPin` |
| Melee | `StartAction(float)`, `BeginAction`, `EndAction` |
| Baker | `StartBaking(out error)`, `RagdollBakerResult`, `RagdollBakerCompletionStatus` |
| Authoring | `TryFromHumanoid`, `TryBuild`, `TryValidateDualRig`, `SetFlatHierarchy`, `SetTreeHierarchy` |
| IK | `IRagdollIKSolver`, `RagdollIKSolvePhase` |

Los métodos `Try...` no realizan commit parcial: devuelven `false` y un error
concreto cuando la validación falla. Las operaciones runtime deben ejecutarse en
la frontera de física documentada por cada sistema.

La simulación manual exige que el componente `RagdollAnimator` esté deshabilitado
y que `Physics.simulationMode == SimulationMode.Script`. `OnPreSimulate` evalúa
el Animator controlado fuera del loop automático antes de `OnPostSimulate`.
