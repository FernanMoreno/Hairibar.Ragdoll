# Hairibar.Ragdoll — diseño técnico

## 1. Alcance y autoridad

Este documento describe la implementación propia del paquete, sus invariantes y
los puntos de extensión. No describe código privado de PuppetMaster. Cuando un
contrato no está definido por RootMotion, se trata como diseño propio Hairibar y
se valida contra las reglas públicas de Unity.

## 2. Assemblies y dependencias

| Assembly | Responsabilidad | Dependencias principales |
|---|---|---|
| `Hairibar.Ragdoll.Core` | tipos, definición, huesos, settings, perfiles, colisiones y utilidades | UnityEngine |
| `Hairibar.Ragdoll.Animation` | animator, músculos, matching, behaviours, props, Baker, authoring y simulación | Core, Unity Animation/Physics |
| `Hairibar.Ragdoll.Editor` | inspectors, wizard, authoring y herramientas de escena | Core, UnityEditor |
| `Hairibar.Ragdoll.Animation.Editor` | Baker, certificación y ventanas de Animation | Animation, Editor |
| `*.Tests` | pruebas EditMode/PlayMode | assembly bajo prueba, Unity Test Framework |
| `Hairibar.Ragdoll.Demo` | escenas y runners de regresión | Core, Animation |

Final IK no es dependencia. La integración de IK usa `IRagdollIKSolver`.

## 3. Modelo de datos

### 3.1 Definición authored

`RagdollDefinition` describe la topología authored. `RagdollDefinitionBindings`
relaciona nombres, handles, Rigidbody, Collider, ConfigurableJoint y Transform.
La definición no debe mutarse para representar un cambio temporal de lifecycle.

### 3.2 Registro runtime

`RagdollAnimator` materializa músculos y pares Target/Puppet. Cada músculo posee
un `RagdollBoneHandle`; las generaciones permiten detectar handles retirados.
`RagdollTargetBindings` mantiene offsets capturados entre jerarquías.

### 3.3 Profiles

Los perfiles ScriptableObject separan autoría de runtime:

- `RagdollAnimationProfile`: matching y overrides por hueso.
- `RagdollPowerProfile`: potencia, límites y respuesta.
- `RagdollWeightDistribution`: distribución de masa.
- `RagdollCollisionProfile`: pares ignorados y política de colisión.
- perfiles de calidad: solver, timestep y presupuesto.

Los snapshots runtime preservan valores authored antes de una acción temporal.

## 4. Ciclo de ejecución automático

El orden de alto nivel del `RagdollAnimator` es:

1. `Update`: tareas de estado no físicas y reconciliación compatible con el modo.
2. `FixedUpdate`: transición de calidad/modo, callbacks de behaviours, lectura de
   pose, matching, aplicación de pin/músculo, actualización de COM/grounding,
   props y commits de jerarquía.
3. PhysX integra cuerpos y genera callbacks de colisión.
4. `LateUpdate`: actualización Animator cuando corresponde, mapping Puppet→Target,
   modifiers de pose, escritura de pose y `OnPostLateUpdate`.
5. `OnFixTransforms` se usa para restaurar transforms cuando el flujo de Animator
   lo requiere.

El orden exacto de una ruta de Animator depende de `EffectiveUpdateMode`:
`Normal`, `AnimatePhysics` o `UnscaledTime`. No se debe asumir que `Update` y
`FixedUpdate` tienen una relación uno a uno.

## 5. Hooks y aislamiento de excepciones

Hooks públicos:

- `OnRead`: pose disponible para lectura/modificadores.
- `OnWrite`: pose lista para escritura/modificadores.
- `OnFixTransforms`: restauración de transforms.
- `OnPostLateUpdate`: final del ciclo de render del animator.
- `OnPostInitialized`: inicialización completa.

Los subscribers se cachean y se invocan individualmente. Una excepción se registra
y no impide notificar a los siguientes subscribers. El orden de suscripción y el
orden documentado por la clase son parte del contrato; no uses un subscriber para
forzar una fase distinta.

## 6. Matching y aplicación física

El matching convierte diferencias Target/Puppet en objetivos de posición y rotación.
La autoridad efectiva se calcula separando mapping, pin, muscle y damper. El punto
de aplicación sanea entradas no finitas y evita enviar masa, fuerza o drive inválido
a PhysX.

La matriz de autoridad que debe probarse es:

| Mapping | Pin | Muscle | Interpretación |
|---:|---:|---:|---|
| 0 | 0 | 0 | sin autoridad animada |
| 1 | 0 | 0 | mapping sin pin/drive |
| 0 | 1 | 0 | pin independiente |
| 0 | 0 | 1 | drive muscular independiente |
| 1 | 1 | 1 | autoridad completa |

`MasterAlpha` solo adapta compatibilidad hacia las autoridades nuevas; código nuevo
debe usar propiedades independientes.

## 7. States y lifecycle

El lifecycle del puppet es distinto del modo de simulación y del estado GetUp.
Un cambio de estado debe conservar o restaurar:

- estado y transición del behaviour;
- Rigidbody, velocidades y kinematic;
- colliders enabled, layers y materiales;
- joint limits y roturas;
- grounding, contactos e ignores;
- mapping, pin, muscle, damper y boosts;
- props y additional pin;
- ownership de calidad y hooks.

`Respawn` es una transacción: captura/limpia/restaura cada dominio y agrega errores
si más de un dominio falla. No debe detenerse en la primera excepción.

## 8. Colisiones y COM

La tubería de colisión es:

1. PhysX produce el callback.
2. `CollisionObserved` notifica la observación sin filtros de aceptación.
3. Se filtran layers, triggers, colliders propios, threshold y presupuesto.
4. `CollisionAccepted` notifica impactos aceptados.
5. Si corresponde, se aplica reducción de pin y se emite
   `CollisionUnpinApplied`.

El módulo COM procesa por FixedUpdate masa total, centro/velocidad de masa,
contactos válidos, centro de presión, vector presión→COM, dirección, magnitud,
ángulo, grounded y estabilidad temporal. Sin masa o contactos devuelve un snapshot
finito y vacío, nunca NaN.

## 9. Props y ownership

El prop standalone conserva un snapshot authored. Al sujetarse, el slot muscular
se convierte en el owner físico. `CurrentRigidbody` debe reflejar el cuerpo que
posee físicamente el prop en ese instante; nunca un componente pendiente de
destrucción.

Las acciones melee y additional pin son overrides temporales. El commit se difiere
a FixedUpdate. Drop, owner switch, freeze, muerte, disable y destroy cancelan la
acción y restauran el snapshot. La política interna de ignores tiene un único owner
para evitar restauraciones cruzadas.

## 10. Jerarquía dinámica y transacciones

`TrySetMuscles` representa el registro completo; `TryReplaceMuscles` representa
reemplazos concretos. El algoritmo es:

1. Capturar registro, handles, props, relays, mappings, perfiles y física.
2. Validar la colección sin modificar el estado activo.
3. Validar slots sujetos, connected bodies, ciclos, duplicados y lifecycle.
4. Preparar nuevos componentes/handles fuera del registro publicado.
5. Aplicar bajas hijo→padre.
6. Aplicar altas padre→hijo.
7. Reconstruir mapping, animated children, relays, ignores y props.
8. Publicar nueva generación y emitir un solo `HierarchyChanged`.
9. Ante fallo, deshacer en orden inverso y publicar el registro anterior.

Los handles antiguos deben fallar de manera detectable. Los objetos externos no se
destruyen; solo se restauran componentes propiedad del sistema.

## 11. Manual simulation

El contrato es una máquina de estados de dos fases:

```text
Idle -> Prepared -> Idle
```

`PrepareManualSimulation(deltaTime)` solo es válido desde Idle y valida modo,
deltaTime y ownership del Animator. Mientras está Prepared, el consumidor ejecuta
la simulación física. `CompleteManualSimulation()` ejecuta hooks/mapping finales y
restaura Animator/update mode. Preparar dos veces, completar desde Idle o invertir
el orden es error.

## 12. Baker

El Baker separa validación, adquisición de fuente, muestreo, escritura y cleanup.
Batch usa `PlayableGraph` manual y evalúa timestamps deterministas. Realtime no
fabrica muestras para frames perdidos. El recorder se destruye en éxito, cancelación,
disable, destroy y excepción.

Bindings fuera de `RecordingRoot`, clips nulos, duración no finita, estados
inexistentes, frameRate inválido o carpeta fuera de `Assets` deben fallar antes de
activar `IsBaking`. La política de settings destino es explícita y no borra ajustes
manuales accidentalmente.

### 12.1 Estados Humanoid multicapa

La página oficial de Baker describe `AnimationStates` como el modo para grabar
estados Mecanim de la capa base cuando la pose final depende de una configuración
más compleja con capas y `AvatarMask`. Hairibar conserva ese contrato mediante un
`AnimatorControllerPlayable` evaluado manualmente:

1. Inicializa el `Animator` con `Update(0)` sin avanzar su reloj.
2. Captura el peso, `fullPathHash`/`shortNameHash` y tiempo normalizado efectivo de
   cada capa.
3. Selecciona en la capa 0 el estado solicitado al Baker.
4. Restaura explícitamente en el playable los estados y pesos de las capas
   superiores válidas.
5. Ejecuta `Evaluate(delta)` y registra la pose Humanoid resultante.

La copia explícita de estados y pesos es diseño propio Hairibar: no se atribuye a
PuppetMaster una implementación interna. Es necesaria porque un playable recién
creado es una instancia de evaluación independiente y no representa por sí solo el
estado runtime que ya tenía otro `Animator`.

El fixture de certificación usa una segunda capa `Override` con un `AvatarMask` de
brazos y dedos. Su clip es Humanoid normal y no declara pose de referencia aditiva;
por eso no se trata como delta `Additive`. La prueba exige primero que Unity mueva
el brazo y cambie el músculo Humanoid con ese controller real, y después exige la
misma variación en el clip comprometido por Baker. La mera existencia de un binding
no certifica combinación de capas.

En las pruebas deterministas el Animator se configura como `AlwaysAnimate`, se
ejecuta `Rebind` y se inicializa con `Update(0)`. `Normal` y `UnscaledTime` se miden
en frames renderizados; `Fixed` se mide en fronteras `WaitForFixedUpdate`. Con
`timeScale == 0`, `Fixed` se verifica mediante frames renderizados porque Unity no
produce una nueva frontera física escalada que se pueda esperar.

Fuentes: [Baker oficial de RootMotion](http://www.root-motion.com/puppetmasterdox/html/page12.html),
[Animator.Update](https://docs.unity3d.com/ScriptReference/Animator.Update.html),
[Animator.updateMode](https://docs.unity3d.com/ScriptReference/Animator-updateMode.html),
[AnimatorControllerPlayable](https://docs.unity3d.com/ScriptReference/Animations.AnimatorControllerPlayable.html)
y [AvatarMask](https://docs.unity3d.com/ScriptReference/AvatarMask.html).

## 13. Authoring y setup runtime

El authoring usa validate-before-rebuild. La descripción nueva se construye aparte;
solo después del commit se retiran componentes propiedad del authoring. Undo/Redo
opera sobre la misma frontera. El setup runtime devuelve root, Target, Puppet,
controladores y error; cualquier excepción elimina solo objetos creados por el
servicio y deja intactos objetos ajenos.

### 13.1 Raíz lógica y hueso físico raíz

La estructura oficial `BipedRagdollReferences` de RootMotion expone un campo
`root`: el padre de todos los huesos, situado normalmente a nivel del suelo. Eso no
se debe confundir con el Rigidbody raíz del ragdoll.

Hairibar representa deliberadamente ambos conceptos por canales distintos:

- la selección del wizard o los argumentos/resultados del servicio de setup
  transportan la raíz lógica del personaje y del dual rig;
- `RagdollBipedReferences` contiene y enumera únicamente los huesos candidatos a
  física, comenzando por `hips`;
- `RagdollDefinitionBindings` identifica cuál de esos músculos físicos es raíz.

Por tanto, Hairibar no declara paridad nominal con el struct oficial y no añade la
raíz lógica a `EnumerateAll`: hacerlo crearía indebidamente Rigidbody/Collider en
el contenedor del personaje y cambiaría ownership, biometría y topología. La
equivalencia es funcional en el flujo de autoría, no identidad de campos.

Fuente: [BipedRagdollReferences oficial](http://www.root-motion.com/puppetmasterdox/html/struct_root_motion_1_1_dynamics_1_1_biped_ragdoll_references.html).

## 14. Extensibilidad

### Behaviour

Deriva de `RagdollBehaviourBase`, usa el contexto entregado y separa callbacks de
estado, FixedUpdate y lifecycle. No cambies Rigidbody directamente fuera de la
frontera documentada.

### Sub-behaviour

Deriva de `RagdollSubBehaviourBase`, activa/desactiva con `SetActive` y coloca el
cálculo físico en `OnFixedUpdate`. El delta se sanea antes de llegar al sub-behaviour.

### Modifiers

Implementa `IBoneProfileModifier`, `ITargetPoseModifier`, `IRagdollMappingModifier`
o `IOrderedRagdollModifier` según la fase necesaria. Evita asignaciones por frame y
no retengas handles tras `HierarchyChanged`.

### IK

Implementa `IRagdollIKSolver`, registra una fase explícita y desregistra en disable
o destroy. El solver debe ser determinista para una misma pose y no controlar
simultáneamente Target mediante otro Animator.

## 15. Serialización y migración

Los aliases antiguos se mantienen durante la migración mayor. `MasterAlpha` adapta
escenas existentes a pin/muscle; valores authored no deben cambiar al abrir una
escena antigua. Los renombrados requieren `FormerlySerializedAs` o migración
explícita y una prueba que compare estado antes/después de reimportar.

No serialices handles runtime como identidad permanente: la generación del registro
puede invalidarlos después de una operación de jerarquía.

## 16. Rendimiento

Los caminos por frame deben evitar allocations en matching, mapping, colisiones,
COM, additional pin y Baker Realtime. Cachea subscribers y buffers reutilizables.
Mide en Development Player con 120 frames de warm-up y 600 medidos. Reporta mediana
y p95 de CPU/memoria para 1, 10, 25 y 50 puppets en Active tree/flat, Kinematic y
Disabled. No conviertas una medición local en umbral universal.

## 17. Pruebas y criterios de regresión

Cada cambio de runtime debe cubrir:

- positivo y negativo;
- rollback;
- enable/disable/destroy;
- lifecycle y excepción de subscriber;
- serialized scene y configuración runtime;
- Editor y Player cuando afecte Unity runtime.

Las escenas de `Samples~/Demos/Regression` producen JSON determinista. La
certificación final debe conservar pruebas unitarias, PlayMode, BuildReport y
ejecución real de la plataforma que se declara certificada.

## 18. Fuentes y límites

RootMotion: FAQ y recursos públicos enlazados desde su soporte oficial.
Unity: Manual/Scripting API de Animator, Humanoid Avatar, Playables, Physics,
BuildPipeline y ProfilerRecorder. No se atribuyen a PuppetMaster constantes,
algoritmos internos ni resultados numéricos que no estén publicados.
