# Auditoría de cobertura posterior al Sprint 0037 canonical v2

**Base:** master `11792ff8d00e722608e0288c752fd65f9615cb55` más Sprint 0034 canonical v2, Sprint 0035 canonical v1.1 patchfix, Sprint 0036 canonical v1 y Sprint 0037 canonical v2.

## Corrección respecto a v1

La v1 se declara sustituida. La v2 corrige ownership del collider dinámico, integración de surface snapshots e internal ignores, separación del boost de pin respecto al clamp de autoridad, reconciliación independiente de masa/COM, cancelación temprana en drop/fault, rollback de dos fases, wake-up del Rigidbody y cobertura lifecycle/PhysX.

## Cambio de cobertura

- **F12** pasa de Pendiente a **Validación pendiente**: collider Box/Capsule con owner dedicado, forma congelada, activación post-commit y cleanup completo.
- **F13** pasa de Pendiente a **Validación pendiente**: boosts transaccionales de geometría, pin y masa, sin clamp accidental ni acumulación.
- **F14** pasa de Pendiente a **Validación pendiente**: offset absoluto de COM, baseline pre-attach y rollback exacto numérico.
- **F07** permanece Pendiente.

## Matriz Props

| ID | Funcionalidad | Estado |
|---|---|---|
| F01 | PropMuscle / slots físicos | Validación pendiente |
| F02 | Pickup/drop/switch | Validación pendiente |
| F03 | Separación y reparent de Mesh Root | Validación pendiente |
| F04 | Rigidbody standalone | Validación pendiente |
| F05 | Masa, layers y materiales | Validación pendiente |
| F06 | Internal collision ignores | Validación pendiente |
| F07 | Animated target children | Pendiente |
| F08 | Disconnect/reconnect | Validación pendiente |
| F09 | Additional pin | Validación pendiente |
| F10 | Offset, peso y masa additional pin | Validación pendiente |
| F11 | Drop desde BehaviourPuppet | Validación pendiente |
| F12 | PropMelee Box/Capsule | Validación pendiente |
| F13 | Action collider/pin/mass boost | Validación pendiente |
| F14 | Prop center-of-mass offset | Validación pendiente |

## Evidencia preparada

- Owner dinámico con identidad por instancia y sin Rigidbody adicional.
- Superficie creada antes del snapshot de layers/materiales.
- Collider seleccionado incluido en ignores y rearmado al activarse.
- Baseline transitorio de ignore forzado a false para evitar restauraciones imposibles.
- API pública bloqueada hasta commit y estado Holding.
- Drop/switch/fault cancelan acción antes del release state.
- Boost de pin aplicado después del clamp de autoridad.
- Masa absoluta desde baseline congelado e independiente del additional pin.
- COM baseline + offset, sin reclamar ownership cuando el offset es cero.
- Snapshots conservados hasta commit final; rollback held tras fallo de standalone.
- Wake-up explícito al comenzar la acción.
- 115 tests acumulativos declarados: 105 Test y 10 UnityTest; 43 específicos de melee.
- Patch full y upgrade reproducibles, reversibles y comparados byte a byte.

## Riesgos que requieren Unity

- Center-of-mass automático frente a custom no es introspectable mediante la API numérica usada.
- Recalculo de inertia tensor por cambios de colliders/masa debe observarse con PhysX real.
- Activación de collider dentro de penetraciones puede producir impulsos de depenetración grandes.
- Diferencias extremas de masa pueden degradar estabilidad de joints.
- Script Execution Order configurado por proyecto puede alterar el tick tardío.
- Comparación con PuppetMaster es de capacidad observable; la implementación histórica usa una arquitectura concreta que no puede asumirse idéntica sin el paquete licenciado.

## Estado de certificación

F12–F14 no se marcan Implementado certificado. Permanecen en Validación pendiente hasta importación, compilación, ejecución de EditMode/PlayMode, PhysX, profiler y comparación licenciada.

## Roadmap

0038 BehaviourFall; 0039 eventos/sub-behaviours; 0040 IK; 0041–0046 autoría/setup; 0047–0049 Baker; 0050 certificación integral.
