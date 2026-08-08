# Auditoría 0050 — certificación funcional reabierta

> El cierre anterior queda suspendido por la auditoría R01-R34. Las ejecuciones
> históricas se conservan como evidencia, no como certificación vigente.

## Estado actual: certificacion reabierta

La auditoria R01-R34 del 2026-08-01 encontro contratos funcionales y pruebas de
cierre que no ejercitaban la capacidad declarada. Este documento conserva la
evidencia historica, pero no certifica cobertura completa mientras alguna
reparacion R01-R34 o fila del manifiesto ejecutable permanezca abierta.

Fecha de la ultima evidencia historica: 2026-08-01
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

## Evidencia automática histórica

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
