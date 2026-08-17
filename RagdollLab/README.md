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
- `RagdollLabTypes`: esquema serializable (`PhysicsFrame`, `ScenarioReport`, `DiagnosticsReport`, …).
- `RagdollSupportGeometry`: soporte finito 0/1/2/3+ puntos proyectado sobre el `supportUp` efectivo (disco, cápsula y hull).

El driver de escenarios (Idle/Push/JointImpulse/Fall) y el batch runner son
específicos de cada proyecto consumidor y deben residir y probarse en ese proyecto;
no forman parte de la certificación de este paquete.

## Artifacts

`evaluation.json`, `frames.json`, `frames.csv`, `comparison.json`, `balance-comparison.json`, `diagnostics.json`, `summary.md`.

Todos contienen `schemaVersion` donde aplica. El esquema actual es `1.5.0`; artifacts `1.4.0`, `1.3.0` y anteriores siguen siendo legibles y exponen los campos nuevos como ausentes, `false`, `0` o `Unavailable`. IDs usan ruta jerárquica estable, no InstanceID.

Feature 007 añade `PhysicsFrame.animatedPairs`, una muestra por cada `RagdollAnimator.AnimatedPair`, con identidad exacta target/physics, velocidad, aceleración y jerk lineal/angular de ambos lados y pesos de mapping authored/effective. Los derivados de target usan el tiempo real de muestreo de Animator; los derivados físicos usan el intervalo de FixedUpdate. Un reset, teleport o timestamp inválido marca la muestra como no disponible, sin convertirla en cero válido. `animatedPairCaptureAttempted` distingue capturas actuales sin fuente de artifacts antiguos, que conservan el fallback legacy. `MAPPING_INTEGRITY` sólo se emite con warnings explícitos de identidad/disponibilidad del recorder.

`diagnostics.json` es un contrato de evidencia, no una orden de tuning. Cada diagnóstico conserva `type`, escenario/perfil, severidad, confianza, métrica observada, frames/tiempos, recomendación y falsificador. Si falta el balance/support source, el informe conserva la razón en `unavailableReasons` y no inventa `Stable`, soporte o recuperación.

`balance-comparison.json` devuelve exactamente `accept`, `neutral`, `reject` o `invalid`. La comparación exige el setup emparejado y aplica primero las guardas de seguridad; una caída, `Unpinned`, datos no finitos, penetración, slip, energía o torque excesivo no puede ser compensado por una mejora de margen. La eficacia del Balancer sólo se concluye desde una pareja baseline/candidate.

## Métricas implementadas

Rigidbody pose/velocidad/angular velocity/COM/inertia/mass/sleep; joint anchors/error/currentForce/currentTorque/relative angular speed/limit distance; contacto enter/stay/exit/impulse/separation con intervalo start/end y duración; clasificación ground-only por layer y normal; COM ponderado por masa; soporte bipedal y margen firmado relativo al plano; caída relativa a `supportOrigin`/`supportUp`; slip tangencial y duración de stance; energía cinética lineal + angular en ejes principales; energía potencial; target/physics/rendered pose cuando mapping humano funciona; RMS/p95/max/mean; oscillation zero-crossing/frequency approximation/settling; diagnostics de anchor drift, torque alto, oscilación, tracking, chatter, penetración, slip, energy spike, COM/support instability, recovery timing y Stagger; por frame, `balance` registra behaviour, estado, capture point, signed margin, transición y torque reactivo; `stagger` registra episodio, phase, swing foot, step count, lift-off y replant; el analyzer agrega episodios cerrados y métricas A/B.

## Limitaciones declaradas

Penetración usa callbacks más `Physics.ComputePenetration` sobre pares próximos, con límite configurable por paso; no es una enumeración exhaustiva ilimitada. DFT exacta O(N²), apropiada para señales cortas y no para telemetría masiva. La captura de ground support requiere configurar correctamente `groundLayers`; si no existe `supportReference`, el recorder deriva el origen del promedio de los puntos grounded disponibles y falla cerrado cuando no hay referencia. `currentForce/currentTorque` dependen de API y fase del solver. No se hace tuning automático. La comparación A/B exige perfil/scenario compatible, fixed timestep, gravedad, masa, altura, root y fingerprint de condiciones iniciales; las guardas de finitud, caída, Unpinned, step inesperado, penetración, slip, energía y torque tienen prioridad sobre cualquier mejora de margen/velocidad. Las expectativas de COM/energía/tiempo dependen del perfil: por ejemplo, COM speed es neutral en Locomotion y GetUp, no una mejora universal.

## Protocolo tuning

Ejecutar baseline → leer `evaluation.json`, `comparison.json`, `balance-comparison.json` y `diagnostics.json` → formular hipótesis con evidencia → cambiar un parámetro estrechamente relacionado → repetir mismos escenarios → revisar regresiones y falsificadores → aceptar/rechazar con registro. RagdollLab no muta parámetros ni certifica la integración CODE RED.
