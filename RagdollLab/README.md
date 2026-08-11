# RagdollLab

Laboratorio determinista de telemetría/análisis para ragdolls basados en `Rigidbody`/`ConfigurableJoint`. No depende de Game View ni modifica tuning automáticamente. No asume ningún consumidor concreto: no referencia `Hairibar.Ragdoll.Core` ni `Hairibar.Ragdoll.Animation` en tiempo de compilación, salvo la localización opcional por reflexión de `RagdollPuppetBehaviour`/`RagdollSimulationModeController`/`RagdollAnimator` cuando están presentes en la jerarquía capturada.

## Componentes

- `RagdollTelemetryRecorder`: captura en `FixedUpdate`, cachea Rigidbody/ConfigurableJoint/Collider y escribe artifacts.
- `RagdollContactRelay`: reenvía enter/stay/exit desde colliders hijos.
- `RagdollLabMath`: RMS, percentile, quaternion angle, COM, energía, zero-crossing, frequency approximation, settling.
- `RagdollLabAnalyzer`: agregados y joint reports.
- `RagdollLabThresholds`: thresholds serializables (`ScriptableObject`).
- `RagdollLabComparison`: delta contra ejecución previa.
- `RagdollLabTypes`: esquema serializable (`PhysicsFrame`, `ScenarioReport`, `DiagnosticsReport`, …).
- `RagdollSupportGeometry`: convex hull / point-containment 2D XZ para el polígono de soporte.

El driver de escenarios (Idle/Push/JointImpulse/Fall) y el batch runner son
específicos de cada proyecto consumidor y deben residir y probarse en ese proyecto;
no forman parte de la certificación de este paquete.

## Artifacts

`evaluation.json`, `frames.json`, `frames.csv`, `comparison.json`, `diagnostics.json`, `summary.md`.

Todos contienen `schemaVersion` donde aplica. IDs usan ruta jerárquica estable, no InstanceID.

## Métricas implementadas

Rigidbody pose/velocidad/angular velocity/COM/inertia/mass/sleep; joint anchors/error/currentForce/currentTorque/relative angular speed/limit distance; contacto enter/stay/exit/impulse/separation; COM ponderado por masa; energía cinética lineal + angular en ejes principales; energía potencial; target/physics/rendered pose cuando mapping humano funciona; RMS/p95/max/mean; oscillation zero-crossing/frequency approximation/settling; diagnostics de anchor drift, torque alto, oscilación y tracking.

## Limitaciones declaradas

Penetración usa callbacks más `Physics.ComputePenetration` sobre pares próximos, con límite configurable por paso; no es una enumeración exhaustiva ilimitada. FFT usa DFT exacta O(N²), apropiada para señales cortas y no para telemetría masiva. Support polygon completo no está inferido: `supportContactCount` y caída son señales configurables, no verdad absoluta. `currentForce/currentTorque` dependen de API y fase del solver. No se hace tuning automático.

## Protocolo tuning

Ejecutar baseline → leer evaluation/comparison/diagnostics → formular hipótesis con evidencia → cambiar un parámetro estrechamente relacionado → repetir mismos escenarios → revisar regresiones → aceptar/rechazar con registro.
