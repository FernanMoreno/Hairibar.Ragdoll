# Auditoría 0050 — certificación funcional cerrada

> La reapertura R01-R34 queda cerrada por el manifiesto ejecutable schema 3 del
> 2026-08-11. Las cifras anteriores se conservan debajo como historial y no se
> usan para certificar el árbol actual.

## Estado actual: certificación vigente

El cierre se ejecutó desde un proyecto Unity limpio que referencia únicamente el
paquete local y Unity Test Framework. El catálogo canónico contiene 140 contratos:
139 quedaron `Verified` y G05 quedó `N/A` por la exclusión deliberada de Final IK;
Animator IK y el contrato genérico `IRagdollIKSolver` permanecen cubiertos.

La validación final exige evidencia NUnit por ID y, donde corresponde, artefactos
Player, escenas, profiler, BuildReport y auditoría documental unidos por el mismo
`runId`, revisión y SHA-256 del árbol. El provisional, el auditor independiente y
el finalizador se ejecutaron en tres procesos distintos.

Fecha de la evidencia vigente: 2026-08-11
Paquete: `com.hairibar.ragdoll` 2.0.0  
Unity usada para validación: 6000.5.2f1

## Alcance y fuentes

La comparación se hizo contra el manual y la referencia pública oficial enlazados
desde RootMotion Support: creación/edición, setup, componente, props, IK,
performance, behaviours, BehaviourPuppet, BehaviourFall y Baker. Las decisiones de
API Unity se verificaron contra Manual/Scripting API oficiales de Unity 6.

La implementación es propia. “Cobertura” significa que existe una capacidad pública
equivalente al concepto documentado; no significa identidad binaria, de arquitectura,
constantes internas ni resultado numérico con un producto de terceros.

## Cierre por sprint

| Sprint | Resultado |
|---|---|
| 0016–0017 | Presupuesto de colisiones, layers, thresholds, multiplicadores y resistencia por velocidad. |
| 0018–0019 | NormalMode Active/Unmapped/Kinematic y activación física segura. |
| 0020–0025 | Recuperación, superficies, seguridad Unpinned, GetUp, teleport y boosts. |
| 0026–0031 | Alive/Dead/Frozen, hooks, pins, joints y colisiones internas runtime. |
| 0032–0033 | Jerarquía dinámica, disconnect/reconnect, joint break y animated target children. |
| 0034–0037 | Props transaccionales, additional pin y melee. |
| 0038 | `RagdollFallBehaviour`: altura, velocidad vertical, blend, crossfade y finalización. |
| 0039 | `RagdollPuppetEvent`, `RagdollAnimatorEvent` y `RagdollSubBehaviourBase`. |
| 0040 | Hooks pre/post mapping y `RagdollIKScheduler`. |
| 0041–0042 | Referencias Humanoid/Generic, masa, colliders, joints y creación runtime. |
| 0043–0044 | Handles, simetría, conversión, ejes, connectedBody, preprocessing y Undo. |
| 0045–0046 | Capas/matriz, flat/tree, targets alternativos y perfil Humanoid portable. |
| 0047–0049 | Baker Generic/Legacy/Humanoid, batch, states, Director, realtime e IK/reducción separada. |
| 0050 | Compilación real, tests acumulativos, auditoría del diff y documentación. |

## Evidencia automática vigente

- El cierre se ejecuta con `Tools~/Run-HairibarClosure.ps1` sobre un proyecto
  temporal limpio y una carpeta de salida externa al repositorio.
- EditMode del paquete: **107/107**.
- PlayMode del paquete: **651/651**.
- Total limpio: **758/758**, cero fallos, omitidos o inconclusos.
- Development builds: Windows64, Linux64, macOS y WebGL correctos; cero
  diagnósticos propios. Windows Player fue ejecutado; los otros tres targets se
  validaron mediante su `BuildReport` en este host Windows.
- Escenarios ejecutados: CoreLifecycle, HumanoidBakerFall, HierarchyProps y
  CollisionsPerformance, con 18 contratos semánticos identificados y medidos.
- Profiler: 120 frames de warm-up y 600 muestras crudas. Matching, mapping,
  collision relay, COM, additional pin y Baker Realtime registraron **0 B** de
  asignación administrada en sus ámbitos medidos.
- Matriz final: **139 Verified / 1 N/A / 0 Open**.
- El manifiesto final guarda los SHA-256 concretos del provisional, validación,
  XML NUnit, BuildReport, Player, escenas, profiler y auditoría documental. No se
  duplican aquí para evitar una referencia circular entre documentación fuente y
  artefactos ligados al hash de esa misma fuente.

## Evidencia automática histórica (sustituida)

- EditMode: **202/202**.
- PlayMode: **533/533**.
- Total: **735/735**, cero fallos, cero omitidos y cero inconclusos.
- Compilación runtime y Editor correcta en Unity 6000.5.2f1.
- Validación realizada en un proyecto nuevo, sin dependencias de paquetes de terceros.
- Samples importados y compilados: cero errores y cero warnings C#.
- Development builds Windows64, Linux64, macOS y WebGL correctos.
- Windows Player ejecuta 109 aserciones en cuatro escenas integrales: lifecycle y
  PhysX, Humanoid/Baker/Fall, jerarquía/props y colisiones/rendimiento.
- Tras 120 frames de warm-up y 600 de medición, `GC Allocated In Frame` es cero en
  mapping, matching, COM, additional pin y Baker Realtime.
- CPU y memoria se registran como mediana/p95 para 1/10/25/50 puppets en Active
  tree/flat, Kinematic y Disabled.
- `git diff --check`: sin errores de whitespace.

Archivos XML generados por el proyecto de validación externo:

- Unity Test Framework PlayMode: `c63ce629cc434b548903fe798b69ae03`.
- Unity Test Framework EditMode: `565f966a7172477081b3dcde3251e71f`.
- `%TEMP%/HairibarRagdollCertification-Windows/windows-build-manifest.json`.
- `%TEMP%/HairibarRagdollCertification-Windows/windows-player-result.json`.

## Límites explícitos de certificación

- El paquete no redistribuye ni inspecciona código de PuppetMaster.
- Final IK no es dependencia: se integra mediante `IRagdollIKSolver`; Animator IK se
  demuestra con los samples existentes.
- Los perfiles físicos siguen requiriendo tuning por personaje, masa, escala,
  timestep y gameplay. Ninguna documentación oficial define constantes universales.
- CPU/GC y estabilidad física dependen de escena/plataforma; el sistema incluye LOD,
  presupuesto y límites, pero deben perfilarse en el juego consumidor.
- Linux64 se compiló desde Windows; su ejecución queda como gate portátil para un
  host Linux real y no se declara ejecutada en este host.
