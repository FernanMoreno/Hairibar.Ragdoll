# Reauditoría explícita de las 140 capacidades

Fecha: 2026-07-31. Esta reauditoría no es una certificación de paridad. Conserva
los textos normativos de `PUPPETMASTER_COVERAGE_AUDIT.md`, inventariados desde el
manual y la referencia pública enlazados por RootMotion. La página oficial histórica
de Doxygen actualmente responde 404; por ello no se amplía ningún contrato a partir
de memoria, nombres de símbolos o código propietario.

Estados:

- `V`: comportamiento observable cubierto por pruebas ejecutadas en este pase.
- `N/A`: exclusión deliberada que no forma parte de la dependencia del paquete.

La lista contiene cada ID histórico exactamente una vez (12 A + 30 B + 7 C +
46 D + 1 E + 14 F + 5 G + 8 H + 10 I + 7 J = 140).

## Autoría (A01-A12)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| A01 | V | Setup Editor crea controladores y behaviours en una transacción; commit, Undo/Redo y rollback negativo probados. |
| A02 | V | Avatar Mixamo real importado: `isValid`, `isHuman` y referencias bípedas completas. |
| A03 | V | `RagdollRuntimeAuthoringTests`: colliders automáticos y geometría saneada. |
| A04 | V | Generación de joints y masa biométrica exacta probadas. |
| A05 | V | Setup guiado completo cubierto con transacción Undo/Redo. |
| A06 | V | Edición de colliders conserva propiedades y revierte mediante Undo. |
| A07 | V | Conversión Box/Capsule/Sphere, propiedades comunes y Undo/Redo verificados. |
| A08 | V | Entradas no soportadas y bindings inválidos se rechazan antes de mutar. |
| A09 | V | Setup runtime prueba layers, matriz y rollback exacto. |
| A10 | V | Tres entradas runtime ejercitadas: separated rollback, direct y duplicate. |
| A11 | V | Flat/tree conserva pose y connectedBody en prueba. |
| A12 | V | Dos instancias Humanoid reutilizan el mismo contrato semántico sin depender de nombres. |

## Núcleo (B01-B30)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| B01 | V | Setup dual Target/Puppet inicializa el runtime real. |
| B02 | V | Bindings y topología parcial tienen regresiones runtime. |
| B03 | V | Sampling, matching y mapping cubiertos por suites existentes. |
| B04 | V | Modos Active/Kinematic/Disabled cubiertos por pruebas de política/runtime. |
| B05 | V | Transiciones de simulación y reconciliación verificadas. |
| B06 | V | Calidad y presupuesto compartido tienen suites dedicadas. |
| B07 | V | Alive/Dead/Frozen y respawn inmediato se ejercitan con runtime físico y Animation Legacy. |
| B08 | V | Player integral ejecuta kill/resurrect con músculos físicos y valida drives y estado. |
| B09 | V | Player integral ejecuta freeze temporal/permanente, resurrect y restauración física/props. |
| B10 | V | Player integral cubre lifecycle, contactos saturados y rotura irreversible de joint por PhysX. |
| B11 | V | Default pose/fix transforms tiene regresiones. |
| B12 | V | Matriz independiente mapping/pin/muscle/damper verificada. |
| B13 | V | Drive spring/damping cubierto por perfiles y solver tests. |
| B14 | V | `RagdollPinMathTests` verifica curva pinPow y entradas no finitas. |
| B15 | V | Falloff por distancia probado. |
| B16 | V | Pin angular independiente probado. |
| B17 | V | Joint anchors runtime y restauración authored probados. |
| B18 | V | Translation binding y offsets cubiertos. |
| B19 | V | Active/Kinematic/Disabled y ownership lifecycle se ejercitan juntos. |
| B20 | V | Política interna restaura el baseline aun con cambios durante lifecycle. |
| B21 | V | Autoridad de rama y colección mutable se ejercitan en runtime inicializado. |
| B22 | V | Perfil semántico compartido probado con Avatar Humanoid real. |
| B23 | V | Simulación manual, Legacy y Humanoid real cubren Normal, AnimatePhysics y UnscaledTime con `timeScale` 0/0.5/1/2. |
| B24 | V | Todos los hooks aíslan suscriptores y conservan el orden del pipeline. |
| B25 | V | Respawn inmediato verificado desde Unpinned, GetUp, Dead y Frozen. |
| B26 | V | La colección se reemplaza con prop sujeto y additional pin; el Player verifica commit y rollback físico. |
| B27 | V | Reconnect resuelve ancestro continuo y mapping desconectado explícito. |
| B28 | V | Un `ConfigurableJoint` real rompe por PhysX y el Player verifica la pérdida irreversible. |
| B29 | V | Conversión flat/tree conserva topología, connected bodies y pose. |
| B30 | V | Cuatro escenas importables incluyen runner determinista y visualización runtime. |

## Behaviours (C01-C07)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| C01 | V | Base modular y lifecycle unitario probados. |
| C02 | V | Colección garantiza un activo y rollback de switch. |
| C03 | V | Contexto inyectado se inicializa en setup real. |
| C04 | V | Eventos serializados y fases Observed/Accepted/Unpin tienen orden determinista. |
| C05 | V | Sub-behaviour COM reutilizable integrado una vez. |
| C06 | V | Hub y presupuesto determinista tienen regresiones. |
| C07 | V | Reactivación, teleport y excepciones de suscriptores se ejercitan en setup real. |

## BehaviourPuppet (D01-D46)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| D01 | V | State machine Puppet/Unpinned/GetUp y transición real probadas. |
| D02 | V | Knockout por separación cubierto por math/state tests. |
| D03 | V | Observed/Accepted/Unpin distinguidos y filtrados. |
| D04 | V | Active se ejecuta con puppets físicos reales durante warm-up y 600 frames medidos. |
| D05 | V | Unmapped exige contacto reciente y expira por pasos físicos. |
| D06 | V | Contactos PhysX reales y saturados ejercitan activación y grounding. |
| D07 | V | Blend avanza en unidades por segundo sin overshoot. |
| D08 | V | Suelo y 40 colliders estáticos generan contactos reales en Player. |
| D09 | V | Cuerpos físicos reciben fuerzas e impactos reales, incluido el caso de joint break. |
| D10 | V | Ground layers, pendientes y gravedad arbitraria probados. |
| D11 | V | Layers de colisión aceptan/rechazan determinísticamente. |
| D12 | V | Threshold y telemetría verificados. |
| D13 | V | Resistencia global/grupo cubierta por response tests. |
| D14 | V | Curva por velocidad Target cubierta por matemática de respuesta. |
| D15 | V | Multiplicadores/threshold por capa probados. |
| D16 | V | Presupuesto máximo por FixedUpdate probado. |
| D17 | V | Regain pin por grupo cubierto por recovery tests. |
| D18 | V | Matriz independiente pin/muscle 0/1 probada en ambos sentidos. |
| D19 | V | Boost immunity y falloff tienen math tests. |
| D20 | V | Boost impulse/falloff tienen math tests. |
| D21 | V | Minimum mapping por grupo verificado. |
| D22 | V | Maximum mapping por grupo verificado. |
| D23 | V | Minimum pin por grupo verificado. |
| D24 | V | Disabled colliders authored se conservan en surface tests. |
| D25 | V | Materiales por estado y restauración exacta probados. |
| D26 | V | El Player mantiene estados finitos con masas extremas de 0.001 y 1000. |
| D27 | V | Threshold de knockout cubierto. |
| D28 | V | Pin authored cero se prueba con knockout activado y desactivado. |
| D29 | V | Multiplicador Unpinned probado. |
| D30 | V | Drop props por pérdida de equilibrio tiene policy tests. |
| D31 | V | GetUp automático cubierto por state/math tests. |
| D32 | V | Delay cubierto. |
| D33 | V | Blend temporal cubierto. |
| D34 | V | Velocidad máxima cubierta. |
| D35 | V | Duración mínima independiente cubierta. |
| D36 | V | Multiplicador de resistencia GetUp cubierto. |
| D37 | V | Multiplicador regain GetUp cubierto. |
| D38 | V | Multiplicador knockout GetUp cubierto. |
| D39 | V | Offsets/alineación prone-supine y cuadrúpedo probados. |
| D40 | V | GetUp cuadrúpedo real selecciona prone/supine y emite el evento correspondiente una vez. |
| D41 | V | Eventos de origen de pérdida/recuperación tienen integración. |
| D42 | V | Ownership externo preserva root; ownership local alinea Target en GetUp. |
| D43 | V | Teleport se ejecuta desde Update, FixedUpdate, LateUpdate y simulación manual. |
| D44 | V | Snapshot de colliders/materiales se restaura exactamente. |
| D45 | V | Masa, COM, velocidad y grounded probados. |
| D46 | V | Centro de presión, vector, dirección, magnitud y ángulo probados. |

## BehaviourFall (E01)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| E01 | V | AnimatorController real contiene Fall, parámetro y estados GetUp Humanoid. |

## Props (F01-F14)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| F01 | V | Slot/PropMuscle state machine probado. |
| F02 | V | Pickup/drop/switch/rollback cubiertos. |
| F03 | V | Reparent y restauración de mesh root cubiertos. |
| F04 | V | Rigidbody standalone/held y transición cubiertos. |
| F05 | V | Masa, layers y material tienen snapshots/rollback tests. |
| F06 | V | Ignores internos y ownership probados. |
| F07 | V | El reemplazo preserva el prop sujeto y su Target animado. |
| F08 | V | Player cubre rebind, disconnect, drop y rollback con prop físico activo. |
| F09 | V | Additional pin dinámico add/remove probado. |
| F10 | V | Offset, peso, masa y rollback dinámicos probados. |
| F11 | V | Drop desde BehaviourPuppet probado. |
| F12 | V | Box/Capsule melee en pickup cubiertos. |
| F13 | V | Reentrada reinicia tiempo y drop cancela/restaura antes del disconnect. |
| F14 | V | Offset COM y restauración cubiertos. |

## IK e integración (G01-G05)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| G01 | V | Modificadores de pose pre-física tienen ordering tests. |
| G02 | V | Solver externo determinista implementa `IRagdollIKSolver` sin Final IK. |
| G03 | V | Scheduler y orden de adaptadores probados. |
| G04 | V | OnRead/OnWrite y hooks adyacentes aíslan cada suscriptor en setup real. |
| G05 | N/A | Final IK se excluye deliberadamente; `IRagdollIKSolver` cubre el contrato genérico oficial. |

## Rendimiento (H01-H08)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| H01 | V | Iteraciones saneadas y aplicadas por quality tests. |
| H02 | V | Velocidad/inercia/límites probados. |
| H03 | V | Modos y presupuesto cubiertos. |
| H04 | V | Threshold y max collisions cubiertos. |
| H05 | V | Player mide 1/10/25/50 puppets reales en flat/tree y registra mediana/p95. |
| H06 | V | Reducción/LOD configurable cubierta por quality tests. |
| H07 | V | 10.000 despachos sin consumidores asignan cero bytes gestionados. |
| H08 | V | Cuatro BuildReports pasan y Windows Development ejecuta 109 aserciones integrales. |

## Baker (I01-I10)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| I01 | V | Inicio/segmento/resultado/error/cancelación cubiertos. |
| I02 | V | Humanoid Baker se instancia con Avatar Humanoid real en proyecto limpio. |
| I03 | V | Generic y Legacy de entrada/salida probados. |
| I04 | V | Batch exacto t=0, intervalos y final probado. |
| I05 | V | Estados Mecanim y rechazo Legacy probados. |
| I06 | V | Director manual/restauración cubiertos. |
| I07 | V | Realtime máximo una muestra/frame y delta real cubiertos. |
| I08 | V | Recorder Humanoid real escribe curvas de ambos pies y manos. |
| I09 | V | Controller Humanoid multicapa valida modos, `timeScale`, evento, root motion y retargeting. |
| I10 | V | Reducción separada tiene EditMode tests. |

## Calidad (J01-J07)

| ID | Estado | Evidencia o brecha concreta |
|---|---:|---|
| J01 | V | Compilación limpia en Unity 6000.5.2f1. |
| J02 | V | EditMode 33/33. |
| J03 | V | PlayMode 518/518. |
| J04 | V | Las cuatro escenas ejecutan 109 aserciones integrales deterministas en Windows Development Player. |
| J05 | V | Player confirma cero GC en caminos críticos y registra mediana/p95 de CPU y memoria. |
| J06 | V | Reauditoría cerrada: 139 filas verificadas y G05 como único N/A justificado. |
| J07 | V | `MIGRATION-PUPPETMASTER-CLOSURE.md` documenta APIs, compatibilidad y diseño propio. |

## Cierre actual

Reauditoría cerrada con 139 filas verificadas y G05 como única exclusión deliberada.
La evidencia comprende 551 pruebas NUnit, 109 aserciones integrales en Windows
Development Player, cuatro BuildReports válidos y perfil de 120 frames de warm-up
más 600 frames medidos. Linux64 está compilado; su ejecución queda explícitamente
reservada para un host Linux real.
