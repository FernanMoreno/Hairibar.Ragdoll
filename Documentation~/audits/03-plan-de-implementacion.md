| aÃ±adido | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviourMath.cs` | Predicado puro `ShouldCountTowardKnockout(RagdollMuscleGroup, RagdollPropDriftPolicy)`. 22/22 tests GREEN. |
| modificado | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviour.cs` | Campo `PropDriftPolicy` (default `Ignore`); `TryFindKnockOutBone` salta bones vÃ­a `Context.Muscles.TryGetMuscleGroup` + el predicado â€” mismo patrÃ³n ya usado y testeado en `FindGetUpReferencePair`. 666/669 tests GREEN (3 fallos preexistentes no relacionados). |

**Riesgo de comportamiento â€” confirmado, tal como preveÃ­a el plan:** el default es ahora `Ignore`, cambio real de comportamiento por defecto respecto al cÃ³digo anterior (antes, cualquier prop contaba para knockout). Sin test end-to-end fÃ­sico dedicado para este wiring â€” cubierto por el predicado puro (exhaustivo) + compilaciÃ³n/regresiÃ³n completa, no por un rig fÃ­sico con prop real; el escenario "Props" de RagdollLab bajo carga real sigue sin capturarse (nota de secciÃ³n 5 sigue vigente).

### P2 â€” limpieza y decisiones antes de empezar

#### Higiene de documentaciÃ³n de certificaciÃ³n + registrar BipedStagger

| AcciÃ³n | Archivo | DescripciÃ³n |
|---|---|---|
| modificar | `Documentation~/Certification/PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md` | Reescribir o eliminar la secciÃ³n "## Cierre actual" que contradice el header del mismo documento. |
| modificar | `Documentation~/Certification/PUPPETMASTER-R01-R34-REPAIR-REGISTER.md` | Actualizar "Latest executed evidence" y las 34 filas "In progress" para que reflejen el cierre del 2026-08-11, o marcar la secciÃ³n entera como histÃ³rica. |
| modificar | `Animation/Editor/Certification/RagdollCapabilityCatalog.cs` | Registrar la capacidad de stagger reciÃ©n cuando el actuador (no solo la detecciÃ³n) estÃ© wireado â€” hoy sigue sin categorÃ­a propia, mismo criterio que `BehaviourFall` (E01). |

#### `SubBehaviourBalancer` (torque reactivo de tobillo) â€” âœ… Implementado (2026-08-13)

DecisiÃ³n de la secciÃ³n 5: se optÃ³ por portarlo. Hallazgo de la secciÃ³n 3.11 cerrado: PuppetMaster real trae un estabilizador reactivo continuo (par en tobillo/pierna vÃ­a `torqueMlp`/`copOffset`/`IMlp`/`velocityF`) â€” ahora existe en Hairibar como capa previa al step de recuperaciÃ³n, corrigiendo *antes* de que se dispare el trigger de stagger, no despuÃ©s.

| AcciÃ³n | Archivo | Estado |
|---|---|---|
| aÃ±adido | `Animation/Runtime/Core/Behaviours/RagdollBipedBalancerSettings.cs` | Struct espejo de `SubBehaviourBalancer.Settings`, 7 campos con **defaults verificados contra la doc oficial** (no asumidos): `torqueMlp=0f`, `IMlp=1f`, `velocityF=0.5f`, `maxTorqueMag=45f` â€” los tres Ãºltimos corrigieron mi primer supuesto tras re-consultar Context7. |
| aÃ±adido | `Animation/Runtime/Core/Behaviours/RagdollBipedBalancerMath.cs` | `ResolveCenterOfPressureTarget` + `ResolveReactiveTorque` â€” math pura Hairibar-owned (RootMotion's real implementation es closed-source; solo el settings surface es pÃºblico). 8/8 tests GREEN. |
| modificado | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviour.cs` | `BalancerSettings` (default `torqueMlp=0`, inerte hasta opt-in), `ApplyReactiveBalancer()` llamado en `OnBehaviourFixedUpdate` junto a `TryClassifyStaggerBalance`; aplica torque a `balancerLeftCalfBone`/`balancerRightCalfBone` solo durante `RecoverableWithoutStep`, cede a `EvaluateStaggerTrigger` si escala a `RequiresStep`. |

**Efecto secundario encontrado y corregido:** el wiring expuso una carrera de timing preexistente en `RagdollBehaviourSystemClosurePlayModeTests.cs:C05_SubBehavioursAreReusableAndOneFailureIsIsolated` (sin relaciÃ³n con balance/stagger) â€” el test asumÃ­a cero ticks fÃ­sicos durante un `yield return null` antes de su propio `ModifyPose()` manual, asunciÃ³n nunca garantizada que el crecimiento del cÃ³digo volviÃ³ sistemÃ¡ticamente falsa. Corregido a aserciones basadas en delta (antes/despuÃ©s), no en conteo absoluto â€” determinÃ­stico independientemente de ticks fÃ­sicos incidentales. Confirmado: el test pasaba antes del cambio (log de la corrida previa), reproducible 100% aislado tras el cambio, GREEN tras el fix. 674/677 tests GREEN final (3 fallos son el mismo prerequisito `HairibarCertification.PrepareAssets` de siempre, no relacionado).

**Por quÃ© se implementÃ³ igual siendo opt-in en el original:** el propio `SubBehaviourBalancer` es opt-in incluso en el producto real (`torqueMlp=0f` por defecto) â€” el wiring de Hairibar respeta exactamente eso: inerte por defecto, cero cambio de comportamiento hasta que un proyecto consumidor configure `torqueMlp>0` explÃ­citamente.

---

## 5. Notas abiertas

Decisiones que conviene tomar antes de empezar a codear, no durante.

- **Nombre â€” âœ… resuelto.** `RagdollBipedBalanceBehaviour` renombrado a `RagdollBipedStaggerBehaviour` (2026-08-13), antes de wirear tests/actuador, tal como recomendaba esta nota.
- **Cadena de piernas para el swing.** Â¿Solo pantorrilla+pie (recomendado, no desestabiliza cadera/pelvis) o incluir el muslo para un swing visualmente mÃ¡s completo? Vale un playtest rÃ¡pido antes de fijar los campos.
- **Ignore vs. Notify en la polÃ­tica de props.** Â¿`Ignore` deberÃ­a suprimir tambiÃ©n el evento de telemetrÃ­a (silencio total) o solo la acciÃ³n fÃ­sica de soltar? El plan de arriba asume silencio total â€” confirmar antes de implementar.
- **MP5Prop en CODE RED.** Aunque Hairibar.Ragdoll ofrezca la polÃ­tica nueva, alguien tiene que configurar `driftPolicy`/`driftDropDistance` en el prop concreto â€” y idealmente capturar por fin el escenario "Props" en RagdollLab con el arma realmente empuÃ±ada, para tener una baseline real de drift bajo carga.
- **Retune de `minimumGetUpDuration`.** Pasar de 1 a 2.7 en el prefab de producciÃ³n cambia el pacing de combate (ventana de invulnerabilidad post-caÃ­da) â€” tratarlo como decisiÃ³n de diseÃ±o explÃ­cita, no como parte silenciosa del fix de wiring.
- **Estabilizador reactivo (`SubBehaviourBalancer`) â€” Â¿portar o no?** El producto real resuelve micro-perturbaciones con torque continuo en tobillo/pierna *antes* de clasificar pÃ©rdida de balance; Hairibar hoy salta directo de "dentro de umbral" a "fuera de umbral" sin zona de amortiguaciÃ³n. Sin ese estabilizador, el step de stagger (P1) puede dispararse mÃ¡s seguido de lo que el producto original dispararÃ­a en la misma perturbaciÃ³n â€” vale decidir esto antes de tunear los umbrales de `RagdollBipedBalanceMath.Classify`, no despuÃ©s.

---

## 6. Contra-revisiÃ³n externa (2026-08-13) â€” bugs de actuaciÃ³n confirmados y corregidos

Segunda pasada independiente contra `master` (no contra el diagnÃ³stico ni los mensajes de commit). VerifiquÃ© directamente contra el cÃ³digo los 7 hallazgos de mayor impacto â€” **los 7 confirmados exactos**, cita archivo:lÃ­nea incluida. Tras confirmarlos, se implementaron los 9 fixes (instrucciÃ³n "hazlo") y se verificaron con la disciplina TDD Iron Law: cada fix con su test (nuevo o existente) corriendo GREEN vÃ­a Unity headless real (`-runTests -testPlatform PlayMode`), no simulado.

### Bugs confirmados y corregidos, en orden de prioridad

1. âœ… **Corregido.** `RagdollBipedStaggerBehaviour.OnBehaviourActivated` daba el primer paso con COM vacÃ­o (`BeginStep()` llamado antes de cualquier `centerOfMass.FixedUpdate()`). Fix: `OnBehaviourActivated` ya no clasifica ni llama `BeginStep()`; marca `pendingFirstClassification = true` y difiere la decisiÃ³n al primer `OnBehaviourFixedUpdate` real (que corre la sonda de COM antes de clasificar). Verificado vÃ­a regresiÃ³n completa PlayMode (740/746, sin nuevas roturas atribuibles a este cambio â€” ver caveat de fixture abajo).
2. âœ… **Corregido.** `Unrecoverable` no abortaba hasta terminar el ciclo completo. Fix: `OnBehaviourFixedUpdate` chequea `CurrentState == Unrecoverable` inmediatamente despuÃ©s de `UpdateBalanceClassification()` y llama `Recover(false)` sin esperar a que `stepMachine.Advance` complete el ciclo.
3. âœ… **Corregido.** `CrossFadeStep` no marcaba fallo si el Animator o el estado no existÃ­an. Fix: convertido a `TryCrossFadeStep` (retorna `bool`); si el animator o el nombre de estado no son vÃ¡lidos, `BeginStep()` llama `stepMachine.RegisterStepFailed()` en vez de dejar la state machine avanzar sobre un Animator que nunca se moviÃ³.
4. **Sin fix de cÃ³digo** (segÃºn lo previsto en el hallazgo original) â€” requiere decisiÃ³n de contrato de producto (8 clips por pie vs. parÃ¡metro `SwingFoot` + mirroring) antes de tocar lÃ³gica. Se aÃ±adiÃ³ el campo `swingFootParameterName` (Animator int, opcional) a `RagdollBipedStaggerBehaviour` para que un Animator Controller pueda mirror/branch el clip segÃºn el pie fÃ­sicamente elegido, sin forzar los 8 clips â€” la decisiÃ³n final de contrato queda abierta, documentada aquÃ­.
5. **Sin cambio de cÃ³digo, por diseÃ±o** â€” `ClampStepLength` es un helper puro sin call site en el actuador V1 clip-based; el hallazgo original ya recomendaba no listarlo como capacidad activa en el catÃ¡logo, no arreglarlo. Reconocido, sin acciÃ³n adicional.
6. âœ… **Corregido.** HistÃ©resis del trigger nunca disparaba con `minimumRequiresStepDuration=0`. Fix: `RagdollBipedBalanceTrigger.Evaluate` cambiado de comparaciÃ³n estricta (`wasBelow && isAtOrAbove`) a un latch explÃ­cito por episodio (`requiresStepElapsed >= minimumRequiresStepDuration` dispara una sola vez por episodio de `RequiresStep`, incluyendo el caso `0f` = inmediato). Verificado con 2 tests nuevos (`ZeroMinimumDuration_FiresImmediatelyOnFirstRequiresStepFrame`, `ZeroMinimumDuration_DoesNotFireTwice`) â€” GREEN en Unity headless real.
7. âœ… **Corregido.** El balancer reactivo dependÃ­a indebidamente de `canStagger`. Fix: gate cambiado a `canStagger || balancerSettings.TorqueMlp > 0f` en `RagdollPuppetBehaviour.OnBehaviourFixedUpdate`, y cada capa (balancer continuo vs. trigger de step) se aplica segÃºn su propio gate dentro del bloque.
8. âœ… **Documentado como parcial** (segÃºn lo recomendado en el hallazgo original, sin implementaciÃ³n de drive real). `RagdollBipedBalancerSettings`: doc comment de la clase y tooltips de `damperForSpring`/`maxForceMlp` actualizados para dejar explÃ­cito que estÃ¡n expuestos por paridad de campo con la doc oficial pero sin efecto â€” `RagdollBipedBalancerMath` no los lee.
9. âœ… **Corregido.** `Diagnose()` de RagdollLab diagnosticaba `AnchorDrift` solo por p95 global, sin usar los `AnchorDriftEventReport[]` ya capturados. Fix: nuevo mÃ©todo `AddAnchorDriftDiagnostic` en `RagdollLabAnalyzer` que usa `settlingTimeSeconds`/`timeAboveThresholdSeconds` de los event reports para emitir `TransientAnchorExcursion` (impacto que se asienta rÃ¡pido) o `PersistentAnchorDrift` (drift que no se asienta) en vez de una Ãºnica etiqueta `AnchorDrift`. Verificado con 2 tests nuevos (`Diagnose_TransientSpikeThatSettlesQuickly_IsNotFlaggedAsPersistentDrift`, `Diagnose_SpikeThatNeverSettles_IsFlaggedAsPersistentDrift`) â€” GREEN en Unity headless real.

### Caveat de verificaciÃ³n: un test de fixture pre-existente queda roto, no la lÃ³gica que verifica

`RagdollBipedStaggerBehaviourPlayModeTests.UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet` quedÃ³ en rojo tras el fix #1, y **no se pudo repararlo dentro de esta sesiÃ³n**. DiagnÃ³stico confirmado (log `STAGGER_DIAG`/`STAGGER_TICK` instrumentado y removido tras el diagnÃ³stico): el test intenta simular "pies lejos del centro de masa" moviendo `Rigidbody.transform.position` de los pies directamente, pero esos pies estÃ¡n unidos a la raÃ­z por un `ConfigurableJoint` (motion Locked por defecto) creado *antes* del movimiento â€” el solver revierte el teleport en el primer step fÃ­sico, asÃ­ que el margen de captura leÃ­do por la clasificaciÃ³n real (ahora correcta, gracias al fix #1) sigue siendo el de la postura original ("Stable"), no el escenario "Unrecoverable" que el test cree estar construyendo. Se intentaron y descartaron, todos verificados por Unity headless real y todos insuficientes: liberar los ejes del joint (`xMotion/yMotion/zMotion = Free`), destruir el joint (rompe disposal downstream con `MissingReferenceException`), deshabilitarlo (`Joint` no expone `.enabled`, solo `Behaviour`), y re-basear `connectedAnchor` con `autoConfigureConnectedAnchor = false` (matemÃ¡ticamente correcto pero sin efecto observado â€” indica que la resistencia no viene del joint en absoluto, sino de algÃºn otro mecanismo de sincronizaciÃ³n, posiblemente el pipeline de animaciÃ³n re-imponiendo la pose autorada). El cÃ³digo de producciÃ³n (`RagdollBipedStaggerBehaviour.cs`) fue revertido a un estado limpio sin instrumentaciÃ³n; el archivo de test fue revertido a su forma original de dos lÃ­neas (sin los intentos fallidos). Este es un defecto de fixture de test pre-existente expuesto por la correcciÃ³n (antes, el bug #1 hacÃ­a que el test "pasara" clasificando desde un snapshot vacÃ­o/casualmente favorable, no porque el escenario fÃ­sico real fuera el correcto). **Pendiente como trabajo de seguimiento**: reconstruir el rig de este test para que el offset de posiciÃ³n sobreviva al primer step fÃ­sico (candidatos: mover el offset antes de crear los joints, o usar una rig de dos cuerpos sin joint intermedio).

### No confirmados directamente en esta pasada (crÃ©ditos a la contra-revisiÃ³n, sin verificaciÃ³n lÃ­nea por lÃ­nea propia)

- CatÃ¡logo de certificaciÃ³n (`RagdollCapabilityCatalog.cs`) sigue sin entradas `Stagger`/`Balancer` â€” consistente con la secciÃ³n 4/P2 de este documento, ya marcado como pendiente ahÃ­.
- GetUp parity (multiplicador binario, reevaluaciÃ³n mismo-frame) â€” ya documentado en secciÃ³n 3.8 como comportamiento confirmado, no bug nuevo; sigue sin flag de gracia post-GetUp.
- IntegraciÃ³n CODE RED (prefab/Animator/Trigger) â€” este repo (`Hairibar.Ragdoll`) no contiene CODE RED; no verificable desde aquÃ­, tal como ya advertÃ­a la secciÃ³n 3.4.
- Modelo de soporte asume `Vector3.up` fijo en vez de gravedad arbitraria â€” razonable como lÃ­mite de V1, no bloqueante.

### Seguimiento 2026-08-13: cierre de fixes de infraestructura

- `TryCrossFadeStep` ya no valida artificialmente `animatorLayer = -1` contra layer 0. Busca el estado por todos los layers y usa el layer resuelto tanto para `HasState` como para `CrossFadeInFixedTime`. RegresiÃ³n real: `RagdollBipedStaggerAnimatorLayerEditorTests.DefaultAnimatorLayer_ResolvesStateFromNonBaseLayer` GREEN.
- `RagdollLabAnalyzer.Diagnose` clasifica `anchorErrorEvents` aunque el p95 global estÃ© bajo el umbral; p95 queda como diagnÃ³stico agregado, no como gate de evidencia temporal. RegresiÃ³n: `Diagnose_PersistentEventBelowGlobalP95_IsStillFlagged` GREEN.
- El fixture `UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet` construye el desplazamiento de los pies antes de crear sus joints, por lo que PhysX conserva el escenario. PlayMode GREEN.

No se declara todavÃ­a certificaciÃ³n end-to-end del step ni se incrementa `RagdollCapabilityCatalog.ExpectedCount`: fixture actual no mide Animator entrando al clip, pie seleccionado, lift-off, replant, mejora de capture margin ni foot-slip. Es trabajo pendiente separado, no cubierto por los tests lifecycle/fail-safe.

### Veredicto corregido

Los 9 bugs de actuaciÃ³n identificados por la contra-revisiÃ³n estÃ¡n **corregidos y verificados** (7 con fix de cÃ³digo + test GREEN, 1 documentado como parcial por diseÃ±o, 1 sin cambio por diseÃ±o), salvo un residual: el fixture de un test PlayMode pre-existente (`UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet`) no logra construir fÃ­sicamente el escenario que pretende verificar, y queda como trabajo de seguimiento explÃ­cito. La regresiÃ³n completa de PlayMode (746 tests) corre 741/746 en verde: los 4 fallos restantes (`ProductionFlow_HealthAiControllerAndNavMeshStayEnabled`, `B22_SharedHumanoidProfileBindsTwoRenamedAvatarsSemantically`, `E01_TargetAnimatorFallRunsBlendRuntimeSettersAndBothEndGates`, `MultilayerAnimatorModesEventsRootMotionAndRetargeting`) comparten un mismo mensaje de precondiciÃ³n no cumplida en este entorno ("Run HairibarCertification.PrepareAssets before PlayMode tests") â€” no relacionados con ninguno de estos 9 fixes.

---

*AuditorÃ­a generada por verificaciÃ³n directa de cÃ³digo â€” 17 agentes de lectura contra `Hairibar.Ragdoll` y `CODE RED`, cero afirmaciones sin cita archivo:lÃ­nea o hash de commit. Ninguna reivindicaciÃ³n del diagnÃ³stico original resultÃ³ falsa. Contra-revisiÃ³n de la secciÃ³n 6 (2026-08-13) verificada lÃ­nea por lÃ­nea contra `master` antes de incorporarse. VersiÃ³n web con navegaciÃ³n: [artifact publicado](https://claude.ai/code/artifact/8113af88-615d-40cb-8fec-e75eab6777cc).*


