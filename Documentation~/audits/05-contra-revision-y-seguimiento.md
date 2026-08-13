## 6. Contra-revisión externa (2026-08-13)

Estado de los nueve bugs de actuación:

1. COM vacío en primera clasificación: corregido con `pendingFirstClassification`.
2. `Unrecoverable`: aborta inmediatamente.
3. Crossfade inválido: `TryCrossFadeStep` falla el ciclo de forma explícita.
4. Contrato de pie↔clip: interfaz `StepSwingFoot` preparada; decisión de controller abierta.
5. `ClampStepLength`: helper no listado como capacidad activa.
6. Histéresis con duración cero: latch por episodio, cubierto por tests.
7. Balancer desacoplado de `canStagger`.
8. `damperForSpring` y `maxForceMlp`: documentados como superficie compatible sin wiring físico.
9. AnchorDrift: diagnóstico temporal `TransientAnchorExcursion`/`PersistentAnchorDrift`, incluyendo eventos persistentes ocultos por p95 bajo.

Seguimiento añadido: `animatorLayer=-1` resuelve el layer que contiene el state y lo usa tanto en validación como crossfade. Fixture `UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet` crea offset antes de joints, por lo que PhysX conserva el escenario.

Verificación Unity headless: RagdollLab PlayMode 6/6, Stagger PlayMode 2/2 y Animator multi-layer EditMode 1/1 GREEN.

No certificar Stagger/Balancer en catálogo todavía: falta benchmark físico end-to-end con pie seleccionado, lift-off, replant, mejora de capture margin y límite de foot-slip.
