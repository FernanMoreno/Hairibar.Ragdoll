# Procesos detallados de Hairibar.Ragdoll

Este documento describe qué preparar, qué ejecutar, qué comprobar y cómo recuperar
un fallo. Las reglas de Unity se basan en su documentación oficial. Las reglas
específicas de equivalencia con PuppetMaster se limitan a la documentación pública
de RootMotion. Todo comportamiento adicional está marcado como diseño propio.

## Convenciones

- **Target**: jerarquía animada que el jugador ve o que produce la pose.
- **Puppet**: jerarquía física que contiene rigidbodies, colliders y joints.
- **Commit físico**: cambio aplicado en la frontera segura de `FixedUpdate`.
- **Rollback**: restauración del snapshot anterior si falla una validación o una
  operación posterior.
- **Éxito**: la operación devuelve su resultado positivo y las invariantes quedan
  satisfechas; que no haya excepción no basta.

## Proceso A — Crear un ragdoll Humanoid

### Precondiciones

1. El personaje tiene `Animator`.
2. `Animator.avatar` no es nulo, `isValid` es `true` y `isHuman` es `true`.
3. La escala no es cero ni contiene ejes degenerados.
4. El proyecto tiene capas de Target y Puppet disponibles.
5. Se ha guardado la escena o existe una rama de control para Undo.

### Ejecución

1. Selecciona la raíz del personaje.
2. Abre `Tools > Hairibar.Ragdoll > Automatic Biped Authoring`.
3. Ejecuta `RagdollBipedReferences.TryFromHumanoid`.
4. Revisa pelvis, torso, cabeza, brazos, manos, muslos, piernas y pies.
5. Selecciona forma de collider, distribución de masa y opciones de joints.
6. Ejecuta la validación previa. Si devuelve error, no se inicia la reconstrucción.
7. Construye la descripción nueva.
8. El authoring elimina únicamente componentes cuya propiedad es suya.
9. Crea o actualiza `Rigidbody`, `Collider` y `ConfigurableJoint`.
10. Crea la definición, bindings y perfil.
11. Ejecuta `TryValidate` de definición, perfil y bindings.
12. Confirma el commit y guarda la escena.

### Comprobaciones posteriores

- Cada músculo tiene Rigidbody, collider y joint compatibles.
- `connectedBody` apunta al padre físico correcto o es nulo para la raíz.
- No existen colliders degenerados ni referencias duplicadas.
- Las masas suman `totalMass` con tolerancia numérica documentada por el test.
- El Target conserva Animator y el Puppet no compite por controlarlo.
- Undo elimina solo lo creado por la operación.

### Fallo y rollback

Si falla validación, construcción o aplicación, el resultado debe ser `false`,
contener un mensaje concreto y conservar jerarquía, componentes externos, perfiles
y referencias anteriores. No continúes con una escena parcialmente construida.

## Proceso B — Crear un rig Generic o manual

1. Duplica la jerarquía para separar Target y Puppet.
2. Decide qué huesos se simulan y cuáles permanecen animados.
3. Añade colliders no degenerados a cada músculo.
4. Añade Rigidbody y configura masa, gravedad, detección de colisión e inercia.
5. Añade ConfigurableJoint y asigna `connectedBody` del padre.
6. Configura anchors, ejes, límites angulares, projection y preprocessing.
7. Crea `RagdollDefinition` y `RagdollDefinitionBindings`.
8. Ejecuta `TryAutoBindByName` solo si los nombres son inequívocos; si no, usa
   `TryAssignTargets` con referencias explícitas.
9. Ejecuta `TryCaptureOffsets` después de colocar Target y Puppet en la misma pose.
10. Ejecuta `TryValidate`; corrige todos los errores antes de inicializar.
11. Asigna `RagdollSettings`, perfiles y `RagdollAnimator`.
12. Inicializa y observa al menos un paso de física y un paso de render.

## Proceso C — Inicialización y primer frame

1. Comprueba que la definición y bindings están asignados.
2. Comprueba que TargetAnimator o TargetAnimation es el controlador elegido.
3. Comprueba que Animator y Legacy Animation no están activos a la vez.
4. Comprueba layers, collision matrix, Rigidbody y joints.
5. Inicializa el animator.
6. Espera la notificación `OnPostInitialized`.
7. Comprueba que handles, músculos, relays y behaviours existen.
8. Selecciona modo Kinematic o Disabled para inspección inicial si el personaje
   empieza en el suelo.
9. Cambia a Active solo después de verificar la pose inicial.
10. Registra errores de cada subscriber sin detener a los demás subscribers.

## Proceso D — Configurar matching

1. Captura la pose inicial con bindings válidos.
2. Establece `MasterMappingWeight` para la salida Puppet→Target.
3. Establece `MasterPinWeight` para el pin hacia la pose animada.
4. Establece `MasterMuscleWeight` para el drive rotacional.
5. Establece `MasterMuscleDamper` para amortiguamiento.
6. Ajusta por hueso con `SetAuthorities` o `SetMappingAuthorities`.
7. Ajusta por grupo o rama con `SetAuthorityWeights` y su variante recursiva.
8. Avanza varios `FixedUpdate` y observa posición, rotación, velocidad y torque.
9. Comprueba las cuatro combinaciones: pin solo, músculo solo, ambos y ninguno.
10. Si un valor no es finito, negativo o excede el contrato, corrígelo antes de
    aplicar la configuración.

`MasterAlpha` es compatibilidad: no lo combines con setters nuevos sin decidir qué
autoridades debe modificar.

## Proceso E — Cambiar modo de simulación

1. Comprueba el modo actual con `CurrentMode`.
2. Para transición gradual usa `SetMode(mode, duration)`.
3. Para una frontera inmediata usa `SetModeImmediate(mode)`.
4. Espera el final de la transición antes de reemplazar jerarquía o props.
5. Comprueba Rigidbody `isKinematic`, fuerzas, colliders y Animator.
6. Comprueba que el modo objetivo no se deshace en el siguiente frame por una
   reconciliación de calidad o lifecycle.
7. Si falla la aplicación, conserva el modo anterior y registra el error.

## Proceso F — Colisiones, grounding y COM

1. Define capas de suelo y excluye la propia jerarquía.
2. Excluye triggers y colliders internos no válidos.
3. Configura threshold y presupuesto de eventos.
4. Usa `CollisionObserved` para instrumentación de toda colisión muscular.
5. Usa `CollisionAccepted` para impactos que superan filtros.
6. Usa `CollisionUnpinApplied` para la reducción real de pinning.
7. Comprueba `RagdollGroundingSnapshot` en un suelo plano y una pendiente.
8. Comprueba escenario sin contactos y escenario sin gravedad.
9. Comprueba masa total, centro de masa, velocidad, centro de presión, vector,
   dirección, magnitud, ángulo y estabilidad temporal.
10. Verifica que ausencia de contactos o masa no produce NaN.

## Proceso G — Kill, freeze, resurrect y respawn

1. Registra modo, lifecycle, pose, velocidades, colliders, materiales, props,
   ignores, boosts y autoridades.
2. Ejecuta kill o freeze desde el API público del behaviour.
3. Comprueba que cada subscriber recibe el evento aunque otro lance excepción.
4. Comprueba qué colliders quedan activos y qué cuerpos son cinemáticos.
5. Ejecuta resurrect o `Respawn(position, rotation)`.
6. El respawn reposiciona Target y Puppet y limpia velocidades.
7. Cancela Unpinned, GetUp, acciones de props y boosts temporales.
8. Restaura mapping, pin, muscle, damper, simulación, colliders y materiales.
9. Limpia contactos, grounding, ignores pendientes y handles transitorios.
10. Comprueba que el prop conserva o recupera su slot compatible.
11. Si un paso falla, intenta los demás pasos de restauración y devuelve el error
    agregado; no abandones la transacción en el primer fallo.

## Proceso H — GetUp y teleport

1. Asegura grounding válido y una superficie con normal utilizable.
2. Clasifica bípeda prone/supine o activa `QuadrupedGetUp`.
3. Llama `TryBeginGetUp` y comprueba estado, blend y evento.
4. No confundas estado GetUp con estado Unpinned.
5. Para teleport, guarda primero posición, rotación y velocidades.
6. Ejecuta teleport desde Update, FixedUpdate, LateUpdate o simulación manual según
   el caso que se quiera certificar.
7. Recalcula anchors, grounding y contactos en la siguiente frontera física.
8. Comprueba que no se restauran velocidades antiguas sobre cuerpos cinemáticos.

## Proceso I — Props y acción melee

1. Valida el prop standalone: Rigidbody, collider, material, masa y centro de masa.
2. Registra snapshot authored antes de cambiar superficie o masa.
3. Registra el slot Target/Puppet compatible.
4. Ejecuta pickup y comprueba `CurrentRigidbody`.
5. Llama `AddAdditionalPin` si el prop lo necesita.
6. Cambia peso, offset o masa y comprueba aplicación en el siguiente FixedUpdate.
7. Llama `StartAction(duration)` con duración finita y no negativa.
8. Comprueba collider melee, radio, material, masa, COM, pin e ignores durante la
   acción.
9. Deja expirar o llama `EndAction`.
10. Comprueba restauración exacta del snapshot.
11. Prueba cancelación por drop, owner switch, muerte, freeze, disable y destroy.
12. Para rollback, fuerza un target/slot inválido y confirma que no queda masa,
    layer, material o ignore modificado.

## Proceso J — Reemplazar músculos y ramas

1. Congela el cambio hasta una frontera segura de FixedUpdate.
2. Construye la colección o lista de reemplazos sin tocar el registro activo.
3. Valida duplicados, nombres, handles, topología, joints, connected bodies,
   bindings, behaviours y lifecycle.
4. Si existe prop sujeto, exige que el nuevo slot sea compatible.
5. Ejecuta `TrySetMuscles` o `TryReplaceMuscles`.
6. Reconstruye mappings, animated children, relays, ignores y additional pin.
7. Emite un único `HierarchyChanged` en el orden documentado.
8. Comprueba handles añadidos, retirados, reemplazados y generación del registro.
9. Comprueba props, materiales, masas y ownership.
10. Si falla cualquier etapa, restaura colección, física, handles, prop y perfiles.

## Proceso K — Baker Batch, Realtime y Director

1. Asigna `RecordingRoot` y comprueba que está dentro del proyecto.
2. Selecciona modo y fuente: Animator, Legacy Animation o estados Mecanim.
3. Comprueba clips no nulos, duración finita, frameRate y destino bajo `Assets`.
4. Elige política de `AnimationClipSettings`.
5. Ejecuta `StartBaking(out error)` y no marques baking activo si devuelve false.
6. En Batch, evalúa manualmente `t=0`, intervalos `1/frameRate` y `clip.length`.
7. En Realtime, emite como máximo una muestra por frame real.
8. En PlayableDirector batch, usa evaluación manual y guarda update mode, tiempo y
   estado originales.
9. Comprueba que ignoreList solo excluye rotación y que bakePositionList conserva
   posición.
10. Comprueba bindings, paths y root motion sin curvas fuera de RecordingRoot.
11. Comprueba nombre, asset, loop/wrap y settings del clip destino.
12. Comprueba `RagdollBakerResult` y `RagdollBakerCompletionStatus`.
13. Ante cancelación, disable, destroy o excepción, comprueba graph y recorder
    destruidos y estado temporal restaurado.

## Proceso L — Simulación manual

1. Comprueba que `Physics.simulationMode` permite control manual.
2. Deshabilita el componente `RagdollAnimator`; el Target y su Animator permanecen
   accesibles para evaluación manual.
3. Llama `PrepareManualSimulation(deltaTime)` o `OnPreSimulate(deltaTime)` una sola vez.
4. Ejecuta las fases de lectura, modifiers, matching, mapping, hooks y PhysX en el
   orden del controlador.
5. Llama `Physics.Simulate(deltaTime)`.
6. Llama `CompleteManualSimulation()` o `OnPostSimulate()` una sola vez.
7. Comprueba que Animator, update mode y lifecycle fueron restaurados.
8. Rechaza llamada invertida, duplicada o con `deltaTime` inválido.

## Proceso M — Certificación y diagnóstico

1. Ejecuta EditMode y PlayMode en un proyecto limpio.
2. Ejecuta las cuatro escenas de Regression con runner determinista.
3. Genera Development builds mediante `HairibarCertification.RunAll`.
4. En Player, calienta 120 frames y mide 600.
5. Registra GC, CPU y memoria con `ProfilerRecorder`.
6. Separa inicialización, runner y JSON de la ventana medida.
7. Ejecuta 1/10/25/50 puppets y Active tree/flat, Kinematic y Disabled.
8. Guarda JSON y BuildReport fuera del repositorio.
9. Declara plataforma ejecutada solo si el Player se inició realmente en ella.
10. Mantén Linux compilado pero no ejecutado como pendiente cuando no exista host
    Linux disponible.

Para una ejecucion durable usa `Tools~/Run-HairibarClosure.ps1`. El coordinador
lanza procesos Unity separados para preparacion, builds/Player, EditMode, PlayMode,
manifiesto provisional, validacion independiente y manifiesto final. Al comenzar
elimina exclusivamente los artefactos conocidos de una ejecucion anterior; por eso
un fallo no puede dejar un `coverage-manifest-final.json` obsoleto como resultado
aparentemente vigente.

El Player Windows escribe observaciones separadas:

- `windows-player-result.json`: las cuatro escenas y todas sus aserciones;
- `scene-results.json`: schema 3 con IDs semánticos exactos y métricas
  (`actual`, `expected`, comparación y tolerancia) revalidadas por J04;
- `profiler-results.json`: schema 3, 120 frames de calentamiento, 600 muestras
  crudas revalidadas contra mediana/p95 y agregados GC,
  mediana/p95 y GC de los seis caminos criticos.

`GC Allocated In Frame` se conserva como diagnostico ambiental de cada celda, no
como atribucion a un subsistema. El gate de cero usa
`GC.GetAllocatedBytesForCurrentThread` alrededor de cada llamada productiva exacta:
matching, mapping, dispatch del relay, actualizacion COM, additional pin y muestreo
Realtime del Baker. Cada camino conserva total, maximo por llamada, numero de
muestras y el miembro productivo medido; una misma observacion no se copia entre
caminos.

El Editor acepta esos archivos solo despues de validarlos. `build-manifest.json`
se deriva de `BuildReport.summary`, `BuildReport.steps/messages`, opciones
Development/AllowDebugging y existencia real de cada salida. Su schema 3 conserva
el recorrido ordenado de steps/messages, sus conteos y un SHA-256 que el validador
recalcula antes de clasificar cada diagnóstico. Provisional, validación y finalización
registran PIDs distintos y el script rechaza reutilizar un mismo proceso. El audit documental
incluye el hash de la guia, comprueba los simbolos publicos por reflexion y conserva
la fuente oficial. Ningun productor escribe `succeeded=true` cuando falta una
medicion, un contador, un output, una asercion o un simbolo.
