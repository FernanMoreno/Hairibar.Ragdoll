# Auditoría hostil de código posterior al sprint 0050

Fecha: 2026-07-22  
Paquete: `com.hairibar.ragdoll` 2.0.0  
Unity validada: 6000.5.2f1

## Método

Se revisaron las rutas de estado, eventos, física, calidad, colisiones, authoring y
compatibilidad contra el manual y la referencia pública oficial de PuppetMaster y
contra los contratos de Unity 6 usados por el paquete. La comparación es conceptual:
la implementación de Hairibar es propia y no usa ni inspecciona código de terceros.

## Defectos confirmados y corregidos

| Severidad | Defecto | Corrección |
|---|---|---|
| Crítica | La conversión world-space de `ConfigurableJoint.targetRotation` multiplicaba los cuaterniones en orden incorrecto. | Orden corregido y base del joint ortonormalizada; dos regresiones matemáticas. |
| Alta | Un tier de calidad podía quedar marcado como aplicado después de que el lifecycle liberase su control, sin restaurar modo o solver. | Reconciliación continua de modo, transición y ownership del override. |
| Alta | Los seis eventos documentados de `BehaviourPuppet` estaban serializados conceptualmente pero no conectados a las transiciones reales. | Integración exacta de pérdida/recuperación y orientación prone/supine, excluyendo muerte de lifecycle. |
| Alta | `BehaviourFall.onEnd` era un `UnityEvent`, aunque la referencia oficial lo define como `PuppetEvent`. | Sustituido por `RagdollPuppetEvent`; conserva acceso compatible al `UnityEvent` y ejecución única. |
| Alta | Un fallo durante `RagdollBehaviourController.Activate` dejaba selección, flags y componentes parcialmente cambiados. | Cambio transaccional con rollback y `AggregateException` si el rollback también falla. |
| Alta | Una excepción de un behaviour podía cortar Frozen/Unfrozen, muerte, resurrección, teleport o reactivación para los restantes módulos. | Fan-out aislado por behaviour con registro de cada excepción. |
| Alta | `RagdollSettings` podía enviar `NaN`, infinitos, masa cero o valores negativos a PhysX; una distribución corrupta podía dividir por cero. | Saneamiento en el punto de aplicación y validación estricta de masa/factores. |
| Media | `RagdollSettings` seguía reaccionando a reconstrucciones estando deshabilitado. | Suscripción y desuscripción simétricas en `OnEnable`/`OnDisable`; stack original preservado al relanzar. |
| Media | Un `RagdollCollisionHub` añadido después de inicializar bindings no creaba relés hasta otro rebuild y retenía referencias obsoletas. | Rebuild inmediato en `OnEnable`, limpieza de la colección y guardia defensiva de dispatch. |
| Media | `BehaviourFall` trataba un `RaycastNonAlloc` saturado como completo, aunque Unity no garantiza que contenga el hit más cercano. | Búfer reutilizable expansible; fallo cerrado al límite defensivo. La sonda de GetUp usa la misma política. |
| Media | Duración `NaN` y capas menores que `-1` podían llegar a `Animator.CrossFade`. | Duración finita no negativa y capa limitada al contrato admitido. |
| Media | Varios tiempos, multiplicadores y velocidad máxima de `RagdollPuppetBehaviour` aceptaban valores no finitos. | Setters y `OnValidate` comparten saneamiento explícito; `+Infinity` sólo se conserva donde significa “sin límite”. |

## Corrección del inventario documental

La conclusión anterior sobre la fila histórica `D46` era incorrecta. La página oficial
de Behaviours describe `SubBehaviourCOM` como responsable del centro de presión, la
dirección y el ángulo del vector COM, además del estado grounded. Se implementó en
`RagdollCenterOfMassSubBehaviour` y quedó cubierto por pruebas unitarias y por el
escenario integral Player; por ello `D46` está verificado.

`H08` y `J05` son actividades de certificación (Profiler, CPU/GC, estrés y estabilidad),
no una API de PuppetMaster. Las pruebas automáticas del paquete no sustituyen perfiles
en dispositivos, escalas, masas, fixed timestep y escenas del juego consumidor.

## Evidencia final actualizada (2026-07-31)

- EditMode: **33/33**.
- PlayMode: **518/518**.
- Total: **551/551**, sin fallos, pruebas omitidas ni inconclusas.
- Compilación runtime/Editor: sin errores ni warnings C#.
- Development builds: Windows64, Linux64, macOS y WebGL correctos.
- Windows Player: 109 aserciones integrales; mapping, matching, COM, additional pin
  y Baker Realtime certificados sin `GC.Alloc` tras warm-up.
- CPU y memoria: mediana/p95 para 1/10/25/50 puppets y cuatro modos.
- `git diff --check`: sin errores de whitespace (sólo avisos de normalización LF/CRLF).
- Proyecto limpio de validación externo, sin dependencias de PuppetMaster o Final IK.

## Límite honesto

Esta auditoría demuestra consistencia de código y cobertura automática del paquete;
no demuestra paridad numérica con un binario licenciado de PuppetMaster ni estabilidad
universal de PhysX. Esa afirmación requeriría una batería comparativa autorizada y
perfiles representativos del juego final.
