# Hairibar.Ragdoll — guía de uso

Esta guía explica el flujo público del paquete 2.0. La implementación es propia.
Las equivalencias conceptuales con PuppetMaster se atribuyen únicamente a su
documentación pública; las decisiones que no define RootMotion se marcan como
**diseño propio Hairibar**.

Para procedimientos exhaustivos con precondiciones, commit, rollback y validación,
consulta [`PROCESSES.md`](PROCESSES.md).

## 1. Instalación y primer ragdoll

1. Instala el paquete en Unity 6 y confirma que no hay errores de compilación.
2. Importa los samples desde Package Manager si quieres una escena de referencia.
3. Para un personaje Humanoid válido, abre `Tools > Hairibar.Ragdoll > Automatic
   Biped Authoring`.
4. Para separar explícitamente Target y Puppet, usa `Tools > Hairibar.Ragdoll >
   Dual Rig Layer Setup`.
5. Valida bindings y perfil antes de activar la simulación.

El flujo manual equivalente es: crear una copia Puppet, añadir `Rigidbody`,
`Collider` y `ConfigurableJoint` a los huesos simulados, crear la definición y
asignar `RagdollTargetBindings`. No es necesario simular todos los huesos; una
rama puede permanecer animada.

## 2. Arquitectura y orden de ejecución

`Target` contiene la animación; `Puppet` contiene la física. Cada paso de física
lee la pose Target, aplica matching y drives a los músculos y deja que PhysX
integre los rigidbodies. En el ciclo de render, el mapping puede copiar la pose
Puppet hacia el Target. Los modificadores se ejecutan por etapas y los solvers IK
se registran mediante `IRagdollIKSolver`.

La configuración debe estar validada antes de inicializar. Cambios de jerarquía,
props y simulación manual se confirman en la frontera de `FixedUpdate`.

## 3. Animator, Animation y modos

`RagdollAnimator.TargetAnimator`, `TargetAnimation` y `EffectiveUpdateMode`
indican qué controlador se usa. Animator y Legacy `Animation` no deben controlar
simultáneamente el mismo Target. El modo efectivo distingue `Normal`,
`AnimatePhysics` y `UnscaledTime`; prueba el comportamiento con el `timeScale`
que use tu juego.

Para un Humanoid, el Avatar debe ser válido y humano. Para Generic/Legacy, asigna
bindings explícitos y verifica que cada curva pertenece al RecordingRoot.

## 4. Matching, pesos y calidad

Los controles maestros son independientes:

- `MasterMappingWeight`: pose Puppet hacia Target.
- `MasterPinWeight`: fuerzas de pinning hacia la pose animada.
- `MasterMuscleWeight`: drives rotacionales musculares.
- `MasterMuscleDamper`: amortiguamiento de esos drives.

`MasterAlpha` se conserva como alias de compatibilidad. Para ajustes locales usa
`SetAuthorities`, `SetMappingAuthorities`, `SetDriveMultipliers` y los setters de
grupo/rama. Los valores no finitos, negativos o fuera de rango se rechazan o
sanean en el punto de aplicación.

`RagdollSimulationModeController` permite Active, Unmapped, Kinematic y Disabled.
Los perfiles de calidad deben cambiarse con `SetMode`, `SetModeImmediate` o el
controlador de calidad; no escribas directamente el estado interno de PhysX.

## 5. BehaviourPuppet, caída y lifecycle

`RagdollPuppetBehaviour` separa observación de colisión, aceptación y aplicación
de unpin mediante `CollisionObserved`, `CollisionAccepted` y
`CollisionUnpinApplied`. `CollisionLayers`, `CollisionThreshold` y los
multiplicadores de resistencia son configurables en runtime.

Operaciones principales:

- `SetNormalMode(...)` cambia Active/Kinematic/Unmapped según la política elegida.
- `TryBeginGetUp()` inicia la recuperación si grounding y orientación son válidos.
- `Respawn(position, rotation)` restaura pose, velocidades, lifecycle, colliders,
  materiales, props y boosts como transacción.
- `SetColliderSurfaceState(unpinned)` cambia la superficie sin destruir el
  snapshot authored.
- `QuadrupedGetUp` activa la clasificación de cuadrúpedos documentada por el
  paquete.

`RagdollFallBehaviour` expone parámetros de estado, transición, raycast, blend,
velocidad, límites y `onEnd`. El evento final se emite una sola vez cuando se
cumplen sus condiciones.

## 6. Props y melee

Un prop sujeto usa `RagdollPropMuscle` y un slot compatible. `RagdollProp`
expone `CurrentRigidbody`, `CurrentMuscle`, `AddAdditionalPin` y
`RemoveAdditionalPin`. El pin adicional se aplica en el siguiente paso físico.

`RagdollPropMelee.StartAction(duration)` valida duración finita y no negativa.
Al expirar o cancelar restaura collider, material, masa, centro de masa, pin e
ignores. `BeginAction`/`EndAction` siguen disponibles para control manual.

Antes de reemplazar músculos, el slot del prop debe existir en la nueva colección.
Si no es compatible, la operación falla antes del commit y conserva el estado.

## 7. Jerarquía dinámica

Usa `TrySetMuscles` para reemplazar la colección completa y `TryReplaceMuscles`
para reemplazos puntuales. También existen `TryAddMuscle`,
`TryReplaceMuscle`, `TryRemoveMuscleRecursive`, `TryDisconnectMuscleRecursive` y
`TryReconnectMuscleRecursive`.

Estas operaciones validan duplicados, topología, joints, connected bodies,
bindings, lifecycle y props antes de modificar PhysX. Los handles retirados quedan
inválidos de forma detectable. Las altas se notifican padre-hijo y las bajas
hijo-padre. Ante error se ejecuta rollback.

## 8. Baker

Configura `RagdollBaker` con `RecordingRoot`, modo, clips/estados y destino dentro
de `Assets`. `StartBaking(out error)` devuelve `false` sin cambiar el estado si la
configuración es inválida.

- Batch usa tiempo manual determinista, desde `t=0` hasta `clip.length`.
- Realtime emite como máximo una muestra por frame y no fabrica frames perdidos.
- AnimationClips puede usar Animator o Legacy `Animation` según el tipo.
- AnimationStates es Mecanim.
- PlayableDirector conserva y restaura su estado temporal original.
- La política de settings del clip puede preservar destino, heredar fuente o usar
  defaults.

El resultado indica éxito, cancelación o error. Graph y recorder se destruyen ante
excepción, cancelación, disable o destrucción.

## 9. Simulación manual e IK

El orden obligatorio es:

```csharp
animator.PrepareManualSimulation(deltaTime);
Physics.Simulate(deltaTime);
animator.CompleteManualSimulation();
```

No llames dos veces a una fase ni inviertas el orden. El controlador restaura el
Animator al finalizar. El solver IK externo implementa `IRagdollIKSolver` y se
registra en la fase definida por `RagdollIKSolvePhase`; Final IK no es dependencia.

## 10. Authoring y perfiles

`RagdollAuthoredRig` conserva la autoría editable. `RagdollRuntimeAuthoring` y
`RagdollRuntimeSetupService` construyen Target/Puppet de forma transaccional y
solo eliminan componentes que poseen. La autoría Humanoid usa
`RagdollBipedReferences.TryFromHumanoid`; Generic requiere referencias explícitas.

La distribución de masa biométrica es una opción de authoring, no una constante
universal de PuppetMaster. Valida escala, colliders degenerados, connectedBody y
proyección de joints antes de confirmar.

## 11. Colisiones, grounding y diagnóstico

`RagdollCollisionHub` publica fases Enter/Stay/Exit y eventos agregados. Usa
`RagdollCollisionIgnorer`/perfiles para pares internos. Grounding excluye triggers,
colliders propios y capas no válidas; admite gravedad arbitraria y resultados
vacíos sin NaN.

Activa `RagdollColliderVisualizer` para inspección local. No confundas un evento
observado con uno aceptado: filtros de capas, threshold y presupuesto se aplican
después de la observación.

## 12. Rendimiento y builds

Mide siempre en Development Player. Calienta antes de medir; separa inicialización,
runner y serialización de los frames críticos. `ProfilerRecorder` permite registrar
GC, CPU y memoria. Los límites observados son dependientes de escena, plataforma,
masa, escala y fixed timestep; no son presupuestos universales.

## 13. Troubleshooting rápido

| Síntoma | Comprobación |
|---|---|
| No inicializa | Validar definición, bindings, joints, colliders y Target. |
| Target no sigue | Revisar `MasterMappingWeight`, modo y bindings. |
| Prop no se sujeta | Verificar slot, Rigidbody, layers e ignores. |
| GetUp no termina | Revisar grounding, raycast layers, orientación y velocidades. |
| Baker falla | Revisar RecordingRoot, clips/estados, frameRate y carpeta `Assets`. |
| GC durante runtime | Medir Development Player después del warm-up; evitar cambios de jerarquía por frame. |
| Joint inestable | Revisar masas, anchors, límites, projection y solver quality. |

Para la matriz normativa, evidencia, migración y límites de certificación consulta
[`Documentation~/Certification`](Certification/README.md).
