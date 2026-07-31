# Auditoría de cobertura PuppetMaster → Hairibar.Ragdoll

**Base auditada:** rama `master` actual, incluyendo la etapa 0015.

## Corrección de base

La etapa 0015 está integrada. La rama actual contiene `RagdollPhysicsQualityController`, perfiles de calidad, histéresis, presupuesto compartido y overrides reversibles del solver en `RagdollSettings`.

## Criterio de alcance

La auditoría cubre toda funcionalidad pública localizada en:

- Manual Doxygen distribuido de PuppetMaster: creación, edición, setup, componente, props, IK, rendimiento, behaviours, BehaviourPuppet, BehaviourFall y Baker.
- Referencia pública de APIs y tooltips de `PuppetMaster` y `BehaviourPuppet`, usada solo para inventariar comportamiento observable; no se copiará código propietario.
- Documentación y código actual de Hairibar.Ragdoll.

Los elementos que la propia documentación describe explícitamente como ideas futuras no se consideran capacidades entregadas. Se añadirán a la matriz si una fuente posterior confirma que fueron publicadas.

Estados: **Implementado**, **Parcial**, **Pendiente**, **Validación pendiente** y **En auditoría**.

## Resumen

- Total de capacidades auditadas: **140**
- Implementado: **34**
- Parcial: **23**
- Pendiente: **81**
- Validación pendiente: **1**
- En auditoría: **1**

> “Parcial” significa que existe una base funcional, pero falta al menos una semántica pública documentada. No se contará como paridad completa.

## Matriz completa

### Autoría

| ID | Funcionalidad | Estado |
|---|---|---|
| A01 | Wizard dual rig Target/Puppet | Parcial |
| A02 | Autodetección de referencias bípedas/Humanoid | Pendiente |
| A03 | Generación automática de colliders | Pendiente |
| A04 | Generación automática de joints y opciones biométricas | Pendiente |
| A05 | Flujo manual guiado para colliders y joints | Implementado |
| A06 | Editor Scene View de colliders/joints con simetría y Undo | Pendiente |
| A07 | Conversión/rotación de tipos de collider | Pendiente |
| A08 | Herramientas visuales de ejes y connectedBody | Pendiente |
| A09 | Asignación automática de capas y matriz de colisión | Parcial |
| A10 | Creación completa de ragdoll/puppet en runtime | Pendiente |
| A11 | Conversión de jerarquía flat/tree | Pendiente |
| A12 | Target alternativo y reutilización de Puppet entre rigs | Parcial |

### Núcleo

| ID | Funcionalidad | Estado |
|---|---|---|
| B01 | Dual rig Target/Puppet | Implementado |
| B02 | ConfigurableJoints y ragdolls parciales | Implementado |
| B03 | Lectura Target, animation matching y mapping | Implementado |
| B04 | Modos Active/Kinematic/Disabled | Implementado |
| B05 | Blend seguro entre modos | Implementado |
| B06 | LOD de calidad y presupuesto compartido | Implementado |
| B07 | Estados Alive/Dead/Frozen | Pendiente |
| B08 | Kill blend, dead muscle weight y damper | Pendiente |
| B09 | Freeze por velocidad y freeze permanente | Pendiente |
| B10 | Límites/colisiones internas al morir | Pendiente |
| B11 | Fix Target Transforms para huesos no animados | Pendiente |
| B12 | Pesos maestros mapping/pin/muscle | Parcial |
| B13 | Spring/damping muscular equivalente | Implementado |
| B14 | Curva pinPow | Pendiente |
| B15 | Pin distance falloff | Pendiente |
| B16 | Angular pinning separado | Pendiente |
| B17 | Actualización runtime de joint anchors | Pendiente |
| B18 | Soporte de animación de traslación | Implementado |
| B19 | Toggle/manual control de límites angulares | Parcial |
| B20 | Control global/manual de colisiones internas | Parcial |
| B21 | Propiedades individuales y por grupo | Parcial |
| B22 | Configuración Humanoid compartible | Parcial |
| B23 | Modos de actualización y control del Animator | Parcial |
| B24 | Hooks OnRead/OnWrite/PostLate/FixTransforms | Parcial |
| B25 | Teleport/respawn coherente con behaviours | Pendiente |
| B26 | Añadir/eliminar músculos en runtime | Pendiente |
| B27 | Desconectar/reconectar músculos y mapping | Pendiente |
| B28 | Gestión de joint break | Pendiente |
| B29 | Jerarquía flat runtime | Pendiente |
| B30 | Visualización de Target pose | Parcial |

### Behaviours

| ID | Funcionalidad | Estado |
|---|---|---|
| C01 | Clase base modular | Implementado |
| C02 | Un único behaviour activo y switching | Implementado |
| C03 | Contexto inyectado sin referencias authored externas | Implementado |
| C04 | Eventos serializados: switch, animación y UnityEvent | Parcial |
| C05 | Framework genérico de sub-behaviours reutilizables | Parcial |
| C06 | Despacho central de colisiones | Implementado |
| C07 | Hooks de reactivación y teleport | Parcial |

### BehaviourPuppet

| ID | Funcionalidad | Estado |
|---|---|---|
| D01 | Estados Puppet/Unpinned/GetUp | Implementado |
| D02 | Knockout por separación Target-Puppet | Implementado |
| D03 | Unpin directo por colisión dentro del behaviour | Parcial |
| D04 | NormalMode Active | Pendiente |
| D05 | NormalMode Unmapped | Pendiente |
| D06 | NormalMode Kinematic con activación por contacto | Pendiente |
| D07 | Mapping blend speed | Pendiente |
| D08 | Activación por colliders estáticos | Pendiente |
| D09 | Activación por impulso mínimo | Pendiente |
| D10 | Ground layers y grounding | Implementado |
| D11 | Collision layers | Parcial |
| D12 | Collision threshold | Parcial |
| D13 | Collision resistance global/grupo | Parcial |
| D14 | Collision resistance curve por velocidad Target | Pendiente |
| D15 | Multiplicadores y threshold por capa | Pendiente |
| D16 | Límite maxCollisions por paso | Pendiente |
| D17 | Regain pin speed por grupo | Implementado |
| D18 | Muscle weight relativo al pin | Pendiente |
| D19 | Boost immunity con falloff | Pendiente |
| D20 | Boost impulse multiplier con falloff | Pendiente |
| D21 | Minimum mapping por grupo | Implementado |
| D22 | Maximum mapping por grupo | Implementado |
| D23 | Minimum pin/position authority por grupo | Implementado |
| D24 | Disable colliders por grupo en Puppet | Pendiente |
| D25 | PhysicMaterial Puppet/GetUp/Unpinned | Pendiente |
| D26 | Max Rigidbody velocity al despinnear | Pendiente |
| D27 | Pin weight threshold de knockout | Implementado |
| D28 | Opción unpinnedMuscleKnockout | Pendiente |
| D29 | Unpinned muscle weight multiplier | Implementado |
| D30 | Drop props al perder equilibrio | Pendiente |
| D31 | GetUp automático | Implementado |
| D32 | GetUp delay | Implementado |
| D33 | Blend to animation time | Implementado |
| D34 | Max GetUp velocity | Implementado |
| D35 | Min GetUp duration independiente | Pendiente |
| D36 | GetUp collision resistance multiplier | Pendiente |
| D37 | GetUp regain pin speed multiplier | Pendiente |
| D38 | GetUp knockout distance multiplier | Implementado |
| D39 | Offsets prone/supine y alineación Target | Implementado |
| D40 | Eventos específicos prone/supine | Parcial |
| D41 | Eventos lose/regain balance por origen | Parcial |
| D42 | canMoveTarget / root sincronizado por red | Pendiente |
| D43 | OnTeleport y conservación de GetUp | Pendiente |
| D44 | Restauración transaccional de colliders/materiales | Pendiente |
| D45 | COM, velocidad y grounded | Implementado |
| D46 | Presión, vector y ángulo de COM | Pendiente |

### Otros behaviours

| ID | Funcionalidad | Estado |
|---|---|---|
| E01 | BehaviourFall completo | Pendiente |

### Props

| ID | Funcionalidad | Estado |
|---|---|---|
| F01 | PropMuscle / slots físicos | Pendiente |
| F02 | Pickup/drop/switch de props | Pendiente |
| F03 | Separación y reparent de mesh root | Pendiente |
| F04 | Destruir/restaurar Rigidbody del prop | Pendiente |
| F05 | Overrides de masa, capas y material | Pendiente |
| F06 | Internal collision ignores | Pendiente |
| F07 | Animated target children | Pendiente |
| F08 | Integración con disconnect/reconnect | Pendiente |
| F09 | Additional pin | Pendiente |
| F10 | Offset, peso y masa de additional pin | Pendiente |
| F11 | Drop props desde BehaviourPuppet | Pendiente |
| F12 | PropMelee: Box/Capsule en pickup | Pendiente |
| F13 | PropMelee action pin/mass/collider boost | Pendiente |
| F14 | Prop center-of-mass offset | Pendiente |

### IK e integración

| ID | Funcionalidad | Estado |
|---|---|---|
| G01 | Modificación de pose antes de física | Implementado |
| G02 | Callback posterior al mapping/escritura | Pendiente |
| G03 | Scheduling de solvers IK externos | Pendiente |
| G04 | Hooks públicos equivalentes OnRead/OnWrite | Parcial |
| G05 | Integración demostrada con Animator IK/Final IK | Pendiente |

### Rendimiento

| ID | Funcionalidad | Estado |
|---|---|---|
| H01 | Solver iterations configurables | Implementado |
| H02 | Solver velocity/inertia/velocity limits | Implementado |
| H03 | Kinematic/Disabled y LOD/presupuesto | Implementado |
| H04 | Collision threshold y max collisions | Pendiente |
| H05 | Flat hierarchy para reducir transforms | Pendiente |
| H06 | Reducción configurable de músculos | Parcial |
| H07 | Desactivar broadcasters cuando no se usan | Pendiente |
| H08 | Instrumentación y benchmarks PlayMode | Pendiente |

### Baker

| ID | Funcionalidad | Estado |
|---|---|---|
| I01 | Baker base y grabación de clips | Pendiente |
| I02 | Humanoid Baker | Pendiente |
| I03 | Generic/Legacy Baker | Pendiente |
| I04 | Batch AnimationClips | Pendiente |
| I05 | Animation States | Pendiente |
| I06 | PlayableDirector/Timeline | Pendiente |
| I07 | Realtime ragdoll/Puppet recording | Pendiente |
| I08 | Baking de Foot/Hand IK | Pendiente |
| I09 | Combinación de capas y física/animación | Pendiente |
| I10 | Reducción de claves separada para músculo/IK | Pendiente |

### Calidad

| ID | Funcionalidad | Estado |
|---|---|---|
| J01 | Compilación real en Unity de 0008-0015 | Validación pendiente |
| J02 | EditMode tests de matemática/estado | Implementado |
| J03 | PlayMode tests integrales | Pendiente |
| J04 | Escenas de regresión y muestras de features nuevas | Pendiente |
| J05 | Benchmarks CPU/GC/estabilidad | Pendiente |
| J06 | Matriz de cobertura versionada | En auditoría |
| J07 | Documentación de migración/configuración completa | Parcial |

## Conclusión de auditoría

Hairibar ya tiene una base sólida del núcleo active-ragdoll y varias mejoras propias, pero todavía no cubre toda la superficie pública de PuppetMaster. La mayor parte de las brechas se concentra en lifecycle Dead/Frozen, semánticas completas de BehaviourPuppet, jerarquía dinámica, props, herramientas de autoría, BehaviourFall, hooks IK post-mapping y Baker.

No se declarará cobertura completa mientras exista una fila Pendiente, Parcial o Validación pendiente.

## Roadmap propuesto en sprints pequeños

| Sprint | Objetivo | Entrega cerrada |
|---|---|---|
| 0016 | Colisiones de BehaviourPuppet I | collision layers, threshold, límite maxCollisions y contabilidad determinista por paso físico. |
| 0017 | Colisiones de BehaviourPuppet II | multiplicadores por capa, threshold override y resistencia evaluada por velocidad del Target. |
| 0018 | NormalMode Unmapped | desmapear fuera de contacto, mappingBlendSpeed y restauración sin saltos. |
| 0019 | NormalMode Kinematic | activación por contacto estático/rigidbody e impulso mínimo; retorno seguro a Kinematic. |
| 0020 | Recuperación Puppet | regainPinSpeed global, muscleRelativeToPinWeight y acoplamiento correcto con overrides por grupo. |
| 0021 | Superficies físicas por estado | puppet/unpinned materials, disableColliders por grupo y restauración transaccional. |
| 0022 | Seguridad Unpinned | maxRigidbodyVelocity, unpinnedMuscleKnockout y casos de pin authored igual a cero. |
| 0023 | GetUp completo | minGetUpDuration, collisionResistance/regain multipliers y eventos específicos de transición. |
| 0024 | Target ownership y teleport del behaviour | canMoveTarget, OnTeleport, GetUp pendiente y reactivación segura. |
| 0025 | Boosts de combate | immunity, impulse multiplier, falloff y API por hueso/grupo. |
| 0026 | Lifecycle Alive/Dead | kill blend, dead muscle weight/damper y políticas de Animator. |
| 0027 | Lifecycle Frozen | umbral de velocidad, freeze permanente, límites y colisiones internas al morir. |
| 0028 | Teleport y hooks del núcleo | Teleport/respawn transaccional, OnRead/OnWrite/PostLate/FixTransforms y fixTargetTransforms. |
| 0029 | Pins avanzados | pinPow, pinDistanceFalloff y angular pinning opcional. |
| 0030 | Joints runtime | updateJointAnchors, supportTranslationAnimation explícito y control manual de límites. |
| 0031 | Colisiones internas runtime | toggle global, ignores authored y API manual coherente con props. |
| 0032 | Jerarquía dinámica I | add/remove músculos, reconstrucción de handles/topología y eventos. |
| 0033 | Jerarquía dinámica II | disconnect/reconnect, mapDisconnectedMuscles, animated children y joint break. |
| 0034 | Props I | PropMuscle, pickup/drop/switch, mesh root y restauración de Rigidbody. |
| 0035 | Props II | masa, capas, materiales, collision ignores y dropProps desde BehaviourPuppet. |
| 0036 | Props III | additional pin, offset/peso/masa y sincronización con animation matching. |
| 0037 | Props IV: melee | Box/Capsule, action collider radius, action pin/mass boost y COM offset. |
| 0038 | BehaviourFall | raycast de altura, velocidad vertical, blend parameter, crossfade y condiciones de finalización. |
| 0039 | Eventos y sub-behaviours | PuppetEvent serializable, switching, crossfade, UnityEvent y módulos reutilizables. |
| 0040 | IK antes/después de física | callback post-mapping, scheduling de solvers externos y muestras Animator IK/Final IK. |
| 0041 | Autoría automática I | detección Humanoid/generic, referencias bípedas y generación de masa/estructura. |
| 0042 | Autoría automática II | generación de colliders/joints, opciones de forma, overlap y creación runtime. |
| 0043 | Ragdoll Editor I | handles de colliders, simetría, conversión y rotación de collider. |
| 0044 | Ragdoll Editor II | handles de joints, connectedBody, ejes, inversión, preprocessing y Undo. |
| 0045 | Setup avanzado | capas/matriz, flat/tree, Target alternativo, sharing/swap de rigs y validadores. |
| 0046 | Configuración Humanoid portable | perfil por avatar, remapeo semántico y migración entre personajes. |
| 0047 | Baker I | base de grabación, Generic/Legacy, AnimationClips y AnimationStates. |
| 0048 | Baker II | modo Realtime, PlayableDirector/Timeline y combinación de capas/física. |
| 0049 | Baker III | Humanoid, Foot/Hand IK y reducción de claves diferenciada. |
| 0050 | Certificación final | Unity compile, EditMode/PlayMode, escenas, CPU/GC, estabilidad y cierre de toda la matriz. |

## Regla de ejecución para cada sprint

1. Verificar commit base y estado del repositorio.
2. Releer la documentación específica del sprint.
3. Crear una mini-matriz de requisitos con IDs de esta auditoría.
4. Implementar solo esos IDs.
5. Añadir tests EditMode y, cuando proceda, PlayMode.
6. Validar `git apply`, reversión, LF/CRLF, GUIDs y documentación.
7. Actualizar esta matriz; ninguna fila puede desaparecer sin quedar Implementada, validada o justificada como no aplicable.
