# Sprint 0037 — Props IV: melee — canonical v2

## Estado de esta revisión

La revisión v1 queda sustituida. La auditoría adversarial posterior encontró defectos de ownership, timing y rollback que impedían tratarla como candidata canónica:

1. El collider melee se creaba después del snapshot standalone y su integración con layers, materiales e internal collision ignores no estaba demostrada de extremo a extremo.
2. Masa y center of mass dependían indirectamente del tick del additional pin, por lo que podían quedar obsoletos en Kinematic, Disabled o ausencia de autoridad.
3. `BeginAction()` podía armarse durante la preparación, antes del commit físico completo.
4. Drop, switch y fault podían conservar durante varios pasos el collider o la masa de ataque.
5. El baseline de masa/COM y el snapshot melee podían liberarse antes de que la restauración standalone fuera irreversible, rompiendo el rollback a held tras una excepción.
6. El boost de pin se multiplicaba antes de un `Clamp01`; con autoridad completa un multiplicador mayor que uno podía no incrementar la fuerza.
7. Un collider transitorio deshabilitado podía capturar un baseline `Physics.IgnoreCollision=true` que luego no podía restaurarse de forma segura.
8. El owner oculto podía adoptar por nombre un hijo authored no perteneciente al sistema.
9. Activar un collider y aumentar masa sobre un Rigidbody dormido dependía de que otro contacto lo despertara.
10. La v1 solo añadía cinco pruebas deterministas y no cubría lifecycle, PhysX, rollback ni integración con Props II/III.

La v2 corrige estos puntos y mantiene F12, F13 y F14 en **Validación pendiente**, no en Implementado certificado, hasta su ejecución en Unity.

## Alcance

Este sprint cubre exclusivamente:

- **F12:** `PropMelee` con superficie de acción Box o Capsule durante pickup.
- **F13:** boosts transaccionales de collider, additional pin y masa.
- **F14:** offset local absoluto del center of mass del slot mientras el prop está sostenido.

F07 Animated target children permanece pendiente. `BehaviourFall`, eventos/sub-behaviours e IK pertenecen a los sprints siguientes.

## Arquitectura de ownership

### `RagdollProp`

Continúa siendo el owner de la transacción completa de pickup/drop:

- Captura el Rigidbody standalone antes de crear superficies dinámicas.
- Congela masa de pickup, additional pin y configuración melee por sesión.
- Captura el baseline de masa y center of mass del Rigidbody permanente del slot.
- Aplica/restaura layers y `PhysicMaterial` mediante el snapshot de superficie.
- Mantiene la sesión de internal collision ignores y coordina su liberación con el owner central.
- No libera snapshots hasta que la restauración standalone finaliza con éxito.

### `RagdollPropMelee`

Es owner exclusivo de una superficie dinámica dedicada:

- Crea un hijo oculto con identidad específica de la instancia.
- Nunca adopta ni destruye un hijo authored que solo comparta un nombre genérico.
- Mantiene un `BoxCollider` y un `CapsuleCollider`, pero solo la forma congelada de la sesión participa en el conjunto físico.
- No crea un segundo Rigidbody; el collider seleccionado pertenece al Rigidbody del slot mientras el prop está held.
- Deshabilita ambos colliders fuera de acción, en disable, destroy, drop, switch y fault.

### `RagdollPropMuscle`

- Reconciliará masa y COM en su tick tardío aunque el additional pin no pueda ejecutarse.
- Cancela melee antes de capturar el release state.
- Mantiene diagnósticos separados para transición, collision policy, physical overrides y additional pin.

## Orden transaccional de pickup

1. Validar estructura standalone y configuración melee.
2. Capturar jerarquía y Rigidbody standalone exactos.
3. Congelar configuración melee y crear el owner/collider dinámico deshabilitado.
4. Capturar layers y materiales, incluyendo la superficie melee.
5. Congelar masa, additional pin y baseline del Rigidbody del slot.
6. Mover raíz física y Mesh Root a sus slots independientes.
7. Aplicar layers/material de pickup.
8. Destruir el Rigidbody standalone de forma diferida.
9. Reconectar el slot permanente.
10. Crear el overlay de internal collision ignores, incluyendo el collider melee seleccionado.
11. Aplicar masa y COM held.
12. Marcar pickup como comprometido.

`BeginAction()` público solo se acepta después del paso 12 y cuando el `RagdollPropMuscle` está realmente en `Holding`.

## Superficie melee

La geometría base se congela por pickup:

- Forma Box o Capsule.
- Centro local.
- Tamaño Box.
- Radio, altura y dirección Capsule.
- Multiplicador geométrico de acción.

Durante `BeginAction()`:

- Se aplica la geometría amplificada desde el snapshot, nunca desde la geometría del frame anterior.
- Se habilita únicamente el collider seleccionado.
- Se rearman inmediatamente sus pares de internal collision ignores.
- Se aplican inmediatamente masa y COM held.
- Se despierta una vez el Rigidbody dinámico del slot.

Una llamada repetida a `BeginAction()` no incrementa la versión de estado, pero repara geometría/collider/ignores/overrides si otro sistema los alteró.

`EndAction()` deshabilita ambos colliders, restaura geometría base y devuelve la masa al valor de pickup sin esperar otro `FixedUpdate`.

## Boost del additional pin

La autoridad efectiva de acción se calcula separando autoridad muscular y boost:

```text
appliedWeight = configuredWeight
              × clamp01(effectivePositionAuthority)
              × max(0, actionPinWeightMultiplier)
```

El multiplicador ya no se introduce antes de un `Clamp01`. Por tanto, un peso configurado de 0.5, autoridad 1 y multiplicador 3 produce 1.5 de peso aplicado, en vez de quedar neutralizado en 1.

El additional pin continúa usando un punto virtual local absoluto, `GetPointVelocity` y `AddForceAtPosition(ForceMode.Impulse)`. El boost melee no modifica el snapshot original del pin ni su masa virtual.

## Masa de acción

```text
heldMass = frozenPickedUpMass × effectiveActionMassMultiplier
```

- `frozenPickedUpMass` se captura al iniciar pickup.
- El multiplicador es 1 fuera de acción.
- No existe multiplicación acumulativa frame a frame.
- La reconciliación es independiente de la disponibilidad del additional pin.
- Begin/End action, disable y destroy restauran inmediatamente el valor correspondiente.

## Center of mass

Cuando el offset congelado es distinto de cero:

```text
heldCenterOfMass = capturedSlotCenterOfMass + frozenLocalOffset
```

- El baseline se captura antes de adjuntar la jerarquía física del prop.
- Se escribe de forma absoluta, no aditiva sobre el valor actual.
- Se restaura al baseline durante drop, cancelación, fault, disable o destroy.
- Un offset cero no reclama ownership del COM: cambios externos permanecen intactos mientras no se haya aplicado un offset melee.

Limitación de Unity: la API expone el valor numérico del center of mass pero no permite consultar si el Rigidbody estaba en modo automático o custom. La v2 restaura el valor exacto; la recuperación de ese modo interno requiere validación específica en Unity.

## Internal collision ignores

El collider seleccionado se incluye en la sesión held incluso cuando está deshabilitado. Como es una superficie transitoria creada y poseída por `RagdollPropMelee`, su baseline externo se define como no ignorado (`false`) y no se infiere desde un estado deshabilitado potencialmente stale.

Al comenzar una acción, los ignores se rearman inmediatamente después de habilitar el collider. Al soltar:

1. Se solicita liberar el overlay.
2. Se restauran los baselines authored de los colliders persistentes.
3. Se reaplica la política central vigente.
4. Un nuevo pickup permanece bloqueado mientras esa reconciliación no termine.

## Rollback

La restauración standalone funciona como commit de dos fases:

- Primero escribe masa/COM baseline, libera ignores, reconstruye jerarquía/Rigidbody y restaura superficie.
- Solo después del éxito final libera `heldSlotBody`, masa baseline, COM baseline y snapshot melee.

Si la reconstrucción falla y el slot aún existe, el sistema vuelve a la jerarquía held, reaplica layers/materiales, masa/COM y overlay de ignores usando los snapshots congelados. La acción no se reactiva automáticamente.

## Lifecycle

| Evento | Collider | Boost masa/pin | Sesión congelada |
|---|---|---|---|
| BeginAction | habilita forma seleccionada | aplica | conserva |
| EndAction | deshabilita | restaura base | conserva |
| Drop/switch | cancela antes del release state | restaura base | libera tras commit |
| Fault | deshabilita | restaura base | disponible para recovery |
| Component disable | deshabilita | restaura base | relinquish |
| Hierarchy deactivate | deshabilita | restaura base | conserva |
| Destroy melee | deshabilita y destruye owner | restaura base | libera |
| OnValidate en Play Mode | no rompe acción activa | snapshot no cambia | conserva |

## API pública

```csharp
RagdollProp.Melee
RagdollProp.IsHeldCenterOfMassOverridden

RagdollPropMelee.Settings
RagdollPropMelee.ActionCollider
RagdollPropMelee.IsHeldSession
RagdollPropMelee.IsActionActive
RagdollPropMelee.HeldSessionVersion
RagdollPropMelee.ActionVersion
RagdollPropMelee.LastActionError
RagdollPropMelee.EffectivePinWeightMultiplier
RagdollPropMelee.EffectiveMassMultiplier
RagdollPropMelee.HeldCenterOfMassOffset

bool RagdollPropMelee.BeginAction()
bool RagdollPropMelee.EndAction()
```

## Pruebas preparadas

La distribución declara 115 casos acumulativos:

- 105 `[Test]`.
- 10 `[UnityTest]`.
- 43 casos específicos de melee: 39 EditMode y 4 PlayMode.

La matriz melee cubre configuración, Box/Capsule, ownership del collider, materiales/layers, internal ignores, masa, pin, COM, idempotencia, 100 acciones, múltiples pickups, rollback fallido, fault, disable, destroy, OnValidate, PhysX contact y wake-up.

Estos casos están preparados, no ejecutados en este entorno.

## Validación pendiente

Antes de certificar F12–F14 deben ejecutarse en la versión Unity real del proyecto:

- Importación y domain reload.
- Compilación runtime/editor/tests.
- EditMode y PlayMode.
- Physics Debugger para collider, contacts e ignores.
- Profiler para GC/CPU de 100 acciones y 100 pickups.
- Active/Kinematic/Disabled, Alive/Dead/Frozen y teleports.
- Props con escalas no uniformes, masas extremas y colliders penetrantes.
- Verificación de center-of-mass automático frente a custom.
- Comparación observable con PuppetMaster licenciado.

## Migración

- No aplicar ni conservar la v1 como versión final.
- Desde Sprint 0036, aplicar el patch canonical v2 completo.
- Si la v1 ya está aplicada exactamente, usar el patch de upgrade v1→v2.
- No usar `--reject`, `--3way` ni `--ignore-whitespace` para forzar hunks.
