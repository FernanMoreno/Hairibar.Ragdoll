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

## Diferencia intencional de referencias bípedas

`RagdollBipedReferences` no es un alias nominal de
`RootMotion.Dynamics.BipedRagdollReferences`. El tipo oficial incluye `root` como
contenedor padre de todos los huesos. Hairibar recibe esa raíz mediante la selección
del wizard y los argumentos/resultados de setup; el objeto
`RagdollBipedReferences` enumera solo huesos que pueden convertirse en músculos
físicos (`hips`, torso, cabeza y extremidades). Consulta la sección 13.1 del diseño
técnico antes de migrar una herramienta de autoría basada en nombres de campos.

No agregues el contenedor lógico a `EnumerateAll` ni lo trates como Rigidbody raíz.
Al migrar, pasa el antiguo `root` al wizard/servicio de setup y conserva `hips` como
primera referencia física. Fuente oficial de la diferencia:
[BipedRagdollReferences](https://root-motion.com/puppetmasterdox/html/struct_root_motion_1_1_dynamics_1_1_biped_ragdoll_references.html).

La simulación manual exige que el componente `RagdollAnimator` esté deshabilitado
y que `Physics.simulationMode == SimulationMode.Script`. `OnPreSimulate` evalúa
el Animator controlado fuera del loop automático antes de `OnPostSimulate`.
