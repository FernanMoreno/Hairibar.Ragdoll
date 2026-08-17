# RagdollLab

Laboratorio determinista de telemetría/análisis para ragdolls basados en `Rigidbody`/`ConfigurableJoint`. No depende de Game View ni modifica tuning automáticamente. No asume ningún consumidor concreto: no referencia `Hairibar.Ragdoll.Core` ni `Hairibar.Ragdoll.Animation` en tiempo de compilación, salvo la localización opcional por reflexión de `RagdollPuppetBehaviour`/`RagdollSimulationModeController`/`RagdollAnimator` cuando están presentes en la jerarquía capturada.

## Componentes

- `RagdollTelemetryRecorder`: captura en `FixedUpdate`, cachea Rigidbody/ConfigurableJoint/Collider y escribe artifacts. Cuando están presentes, observa por reflexión los diagnostics de Puppet, BehaviourController y Stagger sin tomar una dependencia de compilación con Animation.
- `RagdollContactRelay`: reenvía enter/stay/exit desde colliders hijos.
- `RagdollLabMath`: RMS, percentile, quaternion angle, COM, energía, zero-crossing, frequency approximation, settling.
- `RagdollLabAnalyzer`: agregados y joint reports.
- `RagdollLabThresholds`: thresholds serializables (`ScriptableObject`).
- `RagdollLabComparison`: comparación A/B scenario-aware y safety-first contra ejecución previa.
- `RagdollLabScenarioProfiles`: perfiles explícitos de `Idle`, `Push`, `GetUp`, `Locomotion`, `Stagger` y `Balancer`; nombres desconocidos quedan `Unavailable`.
- `RagdollLabScenarioSignalCatalog`: contrato ejecutable de `requiredSignals`; cada ID canónico declara fuente, tipo/unidad, disponibilidad mínima, regla de finitud y falsificador.
- `RagdollLabTypes`: esquema serializable (`PhysicsFrame`, `ScenarioReport`, `DiagnosticsReport`, …) y provenance de tuning.
- `RagdollTuningPlanner`: protocolo puro de baseline, evidencia emparejada, registry, promoción y rollback.
- `RagdollTuningExecutor`: orquestación mediante adapters inyectados de store/runner; verifica readback, consume artifacts persistidos cuando se inyecta transporte y restaura o promociona explícitamente.
- `RagdollTuningFileArtifactTransport`: manifiesto versionado `tuning-manifest.json` con binding completo y SHA-256 de `evaluation.json`/`balance-comparison.json`; lectura fail-closed.
- `RagdollSupportGeometry`: soporte finito 0/1/2/3+ puntos proyectado sobre el `supportUp` efectivo (disco, cápsula y hull).

El driver de escenarios (Idle/Push/JointImpulse/Fall) y el batch runner son
específicos de cada proyecto consumidor y deben residir y probarse en ese proyecto;
no forman parte de la certificación de este paquete.

## Artifacts

`evaluation.json`, `frames.json`, `frames.csv`, `comparison.json`, `balance-comparison.json`, `diagnostics.json`, `summary.md`; una corrida de tuning añade `tuning-manifest.json`.

Todos contienen `schemaVersion` donde aplica. El esquema actual es `1.6.0`; artifacts `1.5.0`, `1.4.0`, `1.3.0` y anteriores siguen siendo legibles y exponen los campos nuevos como ausentes, `false`, `0` o `Unavailable`. IDs usan ruta jerárquica estable, no InstanceID.

Feature 007 añade `PhysicsFrame.animatedPairs`, una muestra por cada `RagdollAnimator.AnimatedPair`, con identidad exacta target/physics, velocidad, aceleración y jerk lineal/angular de ambos lados y pesos de mapping authored/effective. Los derivados de target usan el tiempo real de muestreo de Animator; los derivados físicos usan el intervalo de FixedUpdate. Un reset, teleport o timestamp inválido marca la muestra como no disponible, sin convertirla en cero válido. `animatedPairCaptureAttempted` distingue capturas actuales sin fuente de artifacts antiguos, que conservan el fallback legacy. `MAPPING_INTEGRITY` sólo se emite con warnings explícitos de identidad/disponibilidad del recorder.

`diagnostics.json` es un contrato de evidencia, no una orden de tuning. Cada diagnóstico conserva `type`, escenario/perfil, severidad, confianza, métrica observada, frames/tiempos, recomendación y falsificador. Si falta el balance/support source, el informe conserva la razón en `unavailableReasons` y no inventa `Stable`, soporte o recuperación.

Los `requiredSignals` de `ScenarioProfile` ya no son etiquetas de presentación:
`balance.signedSupportMargin`, `balance.capturePoint`, `recovery.time`,
`recovery.fallenFrames`, `recovery.completion`, `tracking.poseError`,
`tracking.velocityError`, `locomotion.taskCompletion`, `foot.slip`,
`contact.penetration`, `stagger.replant`, `stagger.terminalOutcome` y
`prop.lifecycleCompletion` se validan contra el `ScenarioReport` antes de
comparar. La ausencia, no finitud o falsedad de una señal obligatoria produce
`invalid` con `required_signal_missing:<role>:<signal>:<reason>`; no se sustituye
por COM, margen u otra métrica de balance. Por eso GetUp, Locomotion, Stagger y
Props seguirán siendo explícitamente no evaluables hasta que su productor
publique completion/tracking/replant/lifecycle, mientras Idle/Push/Balancer
pueden usar las fuentes que ya existen.

`balance-comparison.json` devuelve exactamente `accept`, `neutral`, `reject` o `invalid`. La comparación exige el setup emparejado y aplica primero las guardas de seguridad; una caída, `Unpinned`, datos no finitos, penetración, slip, energía o torque excesivo no puede ser compensado por una mejora de margen. La eficacia del Balancer sólo se concluye desde una pareja baseline/candidate.

## Métricas implementadas

Rigidbody pose/velocidad/angular velocity/COM/inertia/mass/sleep; joint anchors/error/currentForce/currentTorque/relative angular speed/limit distance; contacto enter/stay/exit/impulse/separation con intervalo start/end y duración; clasificación ground-only por layer y normal; COM ponderado por masa; soporte bipedal y margen firmado relativo al plano; caída relativa a `supportOrigin`/`supportUp`; slip tangencial y duración de stance; energía cinética lineal + angular en ejes principales; energía potencial; target/physics/rendered pose cuando mapping humano funciona; error de velocidad target/physics; RMS/p95/max/mean; oscillation zero-crossing/frequency approximation/settling; diagnostics de anchor drift, torque alto, oscilación, tracking, chatter, penetración, slip, energy spike, COM/support instability, recovery timing y Stagger; por frame, `balance` registra behaviour, estado, capture point, signed margin, transición y torque reactivo; `stagger` registra episodio, phase, swing foot, step count, lift-off y replant; el analyzer agrega episodios cerrados y métricas A/B. `StaggerEpisodeReport.replantContactDuration` representa el intervalo continuo actual de stance observado al replant, no la duración acumulada de stance durante toda la captura.

## Limitaciones declaradas

Penetración usa callbacks más `Physics.ComputePenetration` sobre pares próximos, con límite configurable por paso; no es una enumeración exhaustiva ilimitada. DFT exacta O(N²), apropiada para señales cortas y no para telemetría masiva. La captura de ground support requiere configurar correctamente `groundLayers`; si no existe `supportReference`, el recorder deriva el origen del promedio de los puntos grounded disponibles y falla cerrado cuando no hay referencia. `currentForce/currentTorque` dependen de API y fase del solver. No se hace tuning automático. La comparación A/B exige perfil/scenario compatible, fixed timestep, gravedad, masa, altura, root y fingerprint de condiciones iniciales; las guardas de finitud, caída, Unpinned, step inesperado, penetración, slip, energía y torque tienen prioridad sobre cualquier mejora de margen/velocidad. Las expectativas de COM/energía/tiempo dependen del perfil: por ejemplo, COM speed es neutral en Locomotion y GetUp, no una mejora universal.

## Protocolo tuning

El planner conserva la separación entre decisión y aplicación. Una corrida autónoma
debe enlazar `tuningSessionId`, `experimentId`, `runRole`, `runId`,
`configurationFingerprint`, `baselineConfigurationFingerprint`, parámetro y
valor. Reports antiguos siguen siendo legibles, pero se rechazan como evidencia
autónoma si carecen de ese provenance.

El registry valida bounds, delta seguro, step, escenario y `runtimeWritable`
antes de Unity. El executor recibe adapters del consumidor: verifica el baseline
actual, ejecuta la pareja baseline/candidate con bindings exactos, valida
readback, evalúa las guardas y restaura cualquier candidato no promovido. Sólo
una promoción explícita deja el valor candidato aplicado y actualiza el
baseline del planner. No usa reflexión ni certifica por sí mismo la integración
CODE RED. Para consumir artifacts, el consumidor proporciona `artifactRoot` en
la sesión y un `IRagdollTuningArtifactTransport`; el executor usa el
`EvaluationReport` leído y verificado desde el directorio de cada run, no sólo
el objeto devuelto en memoria por el runner. El recorder publica el manifiesto
al final de `WriteArtifacts`, después de escribir los dos JSON normativos.

CODE RED aporta `tools/run_codered_paired_tuning.py` como runner externo: lanza
dos procesos PlayMode, enlaza cada corrida con su binding, prepara la evaluación
baseline para que Unity produzca el `balance-comparison.json` paired del
candidato y verifica manifest/metadata/SHA-256. Después invoca el entry point
Editor `RagdollLabBatch.RunTuningDecision`, que consume esos artifacts mediante
`RagdollTuningExecutor.EvaluatePersistedPair` y escribe `tuning-decision.json`.
La decisión se limita a `accepted`, `neutral`, `rejected` o `invalid`; no aplica
valores ni promueve candidatos. `--evaluate-existing` permite repetir sólo esa
evaluación sobre un par ya publicado.

La continuidad autónoma usa un estado externo versionado, por defecto
`tuning-session.json` junto al directorio de la sesión. `RunTuningDecision`
lo crea o actualiza atómicamente; `RunTuningPromotion` y
`RunTuningRollback` son operaciones separadas. La promoción vuelve a leer los
dos manifests y hashes antes de convertir el candidato en baseline, y conserva
el historial de experimentos. El siguiente par puede usar
`--use-persisted-baseline` para tomar el valor/fingerprint promovido sin editar
JSON a mano. Una decisión `neutral`, `rejected` o `invalid` queda cerrada y no
se promociona.
