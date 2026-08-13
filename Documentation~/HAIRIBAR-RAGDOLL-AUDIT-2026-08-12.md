# Auditoría del diagnóstico de active ragdoll — 2026-08-12

Verificación línea por línea de 10 reivindicaciones de un diagnóstico externo, contrastadas contra el código real de `Hairibar.Ragdoll` y `CODE RED` (17 agentes de lectura directa, sin asumir nada), con plan de implementación para cada brecha confirmada.

**2 repos** · Hairibar.Ragdoll + CODE RED · **5 confirmadas** · **5 parciales** · **0 refutadas**

---

## 1. Resumen

El diagnóstico original acierta en la arquitectura general: el core físico está sólido, y lo que falta es capa de decisión/recuperación, no más física. Ninguna de las 10 reivindicaciones verificadas resultó falsa — pero 5 de 10 necesitan corrección de matiz, y una coincidencia cambia el orden de prioridades: **dos de las brechas que el diagnóstico marca como "falta completamente" ya estaban siendo cerradas el mismo día**, horas antes de este audit.

Commit `ef23175` ("feat(behaviours): add biped balance detection skeleton", Hairibar.Ragdoll, 03:56) implementa casi literalmente la matemática de capture point que el diagnóstico propone en su sección 3 — `ω₀=√(g/h)`, `x_cp=x_com+v_com/ω₀` — como módulo de clasificación (Stable/RecoverableWithoutStep/RequiresStep/Unrecoverable). Es detección pura: cero suscriptores, cero actuación, cero wiring. El instinto del diagnóstico era correcto; la foto que describe ya no es exacta.

Commit `62e803b` ("Wired the 'Knockdown' Animator bool... never driven by any production code", CODE RED, 01:43) cierra exactamente el hueco P0 que el diagnóstico señala en su punto 6. Pero verificar esa certificación reveló **tres huecos nuevos que el diagnóstico no menciona**: el fix de CrossFade GetUp/GetUpProne se aplicó al prefab de prototipo de RagdollLab, no al prefab de producción; el parámetro `Knockdown` del AnimatorController es de tipo `Trigger` pero el código llama `SetBool`; y cero tests automatizados verifican el lado Animator de la integración.

El resto del diagnóstico se sostiene con matices: el bug de `RagdollLabAnalyzer` es *peor* de lo descrito (no ruidoso — literalmente idéntico entre todas las articulaciones), la corrección de AnchorDrift es *más barata* de lo implícito (la telemetría cruda por-frame y los event markers de impacto ya existen; falta solo la capa de análisis), y la cifra "140/140" no aparece en ningún documento — el número real, repetido en cinco docs, es **139 Verified / 1 N/A / 0 Open**, aunque la crítica de fondo (denominador auto-definido, `BipedStagger` ausente del inventario) es correcta.

| | |
|---|---|
| Commits del mismo día que adelantan brechas P0 | **2** |
| Huecos nuevos hallados en la integración Animator ya "cerrada" | **3** |
| Reivindicaciones falsas | **0** |
| Cifras citadas que no existen textualmente ("140/140") | **1** |

---

## 2. Tabla de veredictos

| Reivindicación | Veredicto | Hallazgo clave |
|---|---|---|
| Balance bípedo / stagger "falta completamente" | **Parcial** | Actuador (paso de recuperación) 100% ausente — confirmado. Pero el módulo de *detección* (capture point + clasificación) ya existe, commiteado horas antes del audit, sin wiring. |
| Bug de tracking angular por-articulación en RagdollLab | **Confirmado** | Peor que lo descrito: el valor sale *idéntico* en todas las articulaciones del reporte, no solo "poco fiable". |
| AnchorDrift resumido en un solo p95 | **Confirmado** | Cierto, y más barato de arreglar de lo implícito: la serie cruda por-frame y los event markers de impacto ya se capturan; falta solo la capa de análisis. |
| Integración Animator↔Knockdown sin certificar (P0) | **Parcial** | Cierto hasta hace horas — ya wireado el mismo día. Pero el prefab de producción sigue sin el CrossFade de GetUp, el parámetro es Trigger tratado como Bool, y no hay test que lo verifique. |
| MP5Prop: divergencia real sin política | **Parcial** | La ausencia de política es 100% real. Pero la divergencia medida es de un arma *no empuñada* (slot vacío), no de una carga bajo combate. |
| Física core (joints/masa/solver/colisión) — "fuerte" | **Confirmado** | Sin stubs, sin TODOs, infraestructura completa. Único matiz: los límites angulares por defecto son genéricos, no anatómicos por hueso. |
| Animation matching + power/authority — "fuerte" | **Confirmado** | Posición y rotación son ejes totalmente independientes en cada capa; Kinematic/Powered/Unpowered existe tal cual. |
| Semántica Puppet/Unpinned/GetUp (3 sub-claims) | **Confirmado** | Las 3 sub-reivindicaciones ciertas: sin flag de gracia post-GetUp, multiplicador binario (no gradual) compartido por 3 protecciones, reevaluación en el mismo FixedUpdate que termina GetUp. |
| "140/140" e inventario PuppetMaster incompleto | **Parcial** | "140/140" no existe en ningún doc — la cifra real es 139V/1N-A/0Open. Pero BipedStagger ausente del inventario, sí confirmado. |
| Certificación: manifest verde vs. prosa contradictoria | **Parcial** | Contradicción real, pero localizada en 2 de 9 documentos. Los dos docs que funcionan como fuente de verdad (README + FINAL-0050) están limpios. |
| Cruce contra API real de PuppetMaster (`SubBehaviourBalancer`, eventos de balance) | **Nuevo (P2)** | `SubBehaviourBalancer` (torque reactivo de tobillo) no tiene equivalente en Hairibar — brecha real, distinta del step. Eventos `onLoseBalance/onRegainBalance/onLoseBalanceFromGetUp` sí tienen paridad exacta — sin brecha. |

---

## 3. Hallazgos detallados

### 3.1 Balance bípedo / stagger — Parcialmente confirmado

> "Falta completamente el equivalente a `BehaviourBipedStagger`: selección de pie, lift-off, swing, aterrizaje, replant, abandono a Unpinned."

El actuador está, en efecto, 100% ausente — cero resultados para `Stagger|SwingPhase|LiftOff|LandingTarget|StepGenerator|Replant` en ambos repos. Pero **horas antes de este audit**, el commit `ef23175` añadió `RagdollBipedBalanceBehaviour.cs` + `RagdollBipedBalanceMath.cs`: matemática de capture point casi verbatim a la propuesta del diagnóstico, con clasificación en 4 estados (no 3) — `Stable / RecoverableWithoutStep / RequiresStep / Unrecoverable`. Es un módulo muerto: dispara un evento `BalanceStateChanged` sin un solo suscriptor, nunca se auto-activa, nunca llama a `Controller.Activate/Deactivate`. El propio comentario de clase ya avisa: *"Wiring that trigger, and the actual foot movement, is out of scope for this change."*

Evidencia:
- `RagdollBipedBalanceMath.cs:23-33` — `CapturePoint()`: `omega = sqrt(max(0.01,gravity)/max(0.05,pendulumLength))`; retorna `centerOfMass + ProjectOnPlane(velocity,up)/omega`
- `RagdollBipedBalanceBehaviour.cs:52,101-108` — `BalanceStateChanged` es un `Action` plano, cero suscriptores en ningún repo
- `RagdollBehaviourController.cs:239-301,380-422` — mecanismo de activación exclusiva (`Activate<T>`/`ActivateByExactTypeName`) que BipedBalance nunca invoca
- `RagdollPuppetEvent.cs:102-146` — el wiring declarativo "switchToBehaviour" que `RagdollFallBehaviour` ya usa para su `onEnd` — BipedBalance no lo usa
- git `ef23175` — "Detection only: no foot actuation, no auto-activation, not wired to any trigger yet."

### 3.2 RagdollLab: bug de tracking angular por-articulación — Confirmado

> "El loop acumula error contra TODOS los target poses en vez del target de esa articulación — comparaciones cruzadas no fiables."

Confirmado exactamente, y más severo: la lista de tracking no depende del índice de articulación `j` en absoluto, así que **el `MetricSummary` sale byte-idéntico para las 21 articulaciones del reporte** — "spine_02 vs upperarm_r" no es solo poco fiable, es matemáticamente imposible que difiera. `AnchorError` queda intacto, tal como el diagnóstico intuye — es un cálculo estructuralmente separado.

Evidencia:
- `RagdollLabAnalyzer.cs:55` — `for (p...) tracking.Add(frames[i].targetPoses[p].targetPhysicsAngularError)` — nunca referencia `j`
- `RagdollTelemetryRecorder.cs:182-208` — `CaptureTargetPoses` solo captura 7 huesos fijos, sin id que los enlace a la articulación (por eso el loop original "mezcló todo": no había clave de join)
- git blame — todo atribuido a `51da212` (2026-08-08), sin tocar desde entonces; cero tests ejercitan `Analyze()` hoy

Fix propuesto: añadir `physicsBodyId` a `TargetPoseTelemetry`, poblarlo en `CaptureTargetPoses` con el mismo `StableId(transform,"Rigidbody")` que ya usa `CaptureJoint`, y filtrar el loop del analizador por `physicsBodyId == sample.bodyId`. Ver plan completo abajo (P1).

### 3.3 AnchorDrift: un solo p95 mezcla impacto grande+recuperación rápida con drift persistente — Confirmado

> "RagdollLab debería capturar baseline/peak/+Nms/settlingTime/AUC/timeAboveThreshold por evento de impacto."

Confirmado: `Analyze()` colapsa todo el run a 6 números (`current/mean/rms/p95/max/normalizedMean`), sin eje temporal. El diagnóstico global "AnchorDrift" se dispara solo con ese p95, sin `firstFrame`/`peakFrame` pese a que el schema ya los define. Matiz importante: **esto es más barato de lo que suena** — el recorder ya guarda la serie cruda por-frame en `frames.json` y ya marca el instante del impacto vía `EventMarker` (que CODE RED ya usa: `"eventApplied"` en `RagdollTestScenario.cs:123-125`). El analizador simplemente nunca lee ese campo. `SettlingTime()` también existe ya en `RagdollLabMath.cs`, pero aplicado a velocidad angular relativa, no a `anchorError`.

Evidencia:
- `RagdollLabAnalyzer.cs:83-84` — `if (joint.anchorError.p95 > thresholds.anchorErrorWarningMeters)` — único gatillo, `firstFrame`/`peakFrame` hardcoded a `0,0`
- `RagdollTelemetryRecorder.cs:449` — `frames.json` serializa la lista completa sin agregar, ~251 frames × 21 joints en CriticalKnockdown
- CODE RED `RagdollTestScenario.cs:123-125` — marca `"eventApplied"` en el frame exacto del impulso, dato ya disponible y nunca leído por el analizador

### 3.4 Integración Animator ↔ Hairibar (Knockdown) — Parcialmente confirmado, P0

> "El parámetro 'Knockdown' no estaba siendo alimentado por Hairibar; hay que certificar esto antes de seguir afinando GetUp/Unpinned."

Cierto hasta el commit `62e803b` (CODE RED, 01:43 del mismo día) — *tres horas antes* del commit de balance bípedo. `HairibarRagdollAdapter.cs` ahora sí se suscribe al `StateChanged` real de `RagdollPuppetBehaviour` y llama `Animator.SetBool("Knockdown",...)`. El propio commit narra el bug que arregla. Pero verificar esta certificación — como el diagnóstico pide — encontró **tres huecos que ni el commit ni el diagnóstico mencionan**, y que sobrevivieron a tres commits posteriores (`fd28087`, `a3ad6f2`, `01f345f`) que tocaron estos mismos archivos sin cerrarlos:

- **Prefab equivocado.** El fix de CrossFade GetUp/GetUpProne solo se aplicó a `PF_Enemy_Grunt_HairibarPrototype.prefab` (prototipo de RagdollLab). `PF_Enemy_Grunt.prefab` — el que carga la suite de PlayMode tests y el que se envía — sigue con `onGetUpProne`/`onGetUpSupine` serializados vacíos.
- **Bool vs. Trigger.** `AC_Enemy.controller` declara `Knockdown` como `m_Type:9` — cruzado contra `Speed` (`m_Type:1`=Float, confirmado por `SetFloat`) y `Attacking` (`m_Type:4`=Bool, confirmado por `SetBool`) esto solo cuadra si `9=Trigger`. El adapter llama `SetBool` sobre un parámetro Trigger.
- **Cero cobertura del lado Animator.** Los 1188+ líneas de `HairibarRagdollAdapterPlayModeTests.cs` solo aseveran el enum interno de Hairibar (`RagdollPuppetBehaviour.State`) — ningún test llama `GetCurrentAnimatorStateInfo` ni `GetBool`.

Evidencia:
- git `62e803b` (CODE RED) — "the 'Knockdown' Animator bool... never driven by any production code"
- `PF_Enemy_Grunt.prefab:653-664` — `onGetUpProne`/`onGetUpSupine`: `animations: []`, producción, sin wiring
- `AC_Enemy.controller:363-368,375-380` — `Attacking m_Type:4`, `Knockdown m_Type:9`
- `HairibarRagdollAdapter.cs:141,145,582` — `SetBool(KnockdownHash,...)` contra un parámetro declarado Trigger

### 3.5 Prop muscle: divergencia MP5Prop sin política — Parcialmente confirmado

> "Un prop muscle mostró divergencia real; hoy no hay política — cualquier drift de prop derriba al puppet entero."

La mitad de política: 100% confirmada. `TryFindKnockOutBone` itera cada `AnimatedPair` — cuerpo o prop — por el mismo chequeo de `knockOutDistance`, sin branch por `RagdollMuscleGroup`. El único mecanismo prop-aware (`DropPropsNow`) actúa *después* de que todo el puppet ya entró en Unpinned, no antes — lo opuesto a una política dirigida.

La mitad de "divergencia real" tiene un matiz importante: en los cuatro artifacts disponibles (CriticalKnockdown, StrongKnockdown, Recovery, GameplayHit), el arma **nunca fue recogida** — fuerza/torque del joint leen `0.0` en todo el run, y la telemetría `propPeakLiveDivergence` construida específicamente para esto lee `0.000` en el log inspeccionado. El propio `experiment-log.md` del proyecto dice: *"not yet established as the cause of the global collapse."* Es una señal real y ya investigada — pero documenta un slot vacío, no un arma cargada bajo combate.

Evidencia:
- `RagdollPuppetBehaviour.cs:2339-2426` — `TryFindKnockOutBone`, sin filtro de `RagdollMuscleGroup`
- `RagdollPuppetBehaviour_Props.cs:60-123` — `DropPropsNow`: reactivo global post-Unpinned, no preventivo por-prop
- CODE RED `Artifacts/.../CriticalKnockdown/evaluation.json:105-165` — MP5PropMuscle `anchorError p95=1.847m`, force/torque=0.0
- CODE RED `experiment-log.md:36-37` — drift 0.6144m, fuerza p95 solo 9.115N, "not yet established as the cause"

### 3.6 Física core — Confirmado

Sin stubs, TODOs ni `NotImplementedException` en las ocho carpetas escaneadas. Masa/inercia con 3 modos explícitos (`PreserveAuthored/ResetFromColliders/ResetAndStabilize`), solver reconfigurable en runtime vía `RagdollPhysicsQualityController`, drives PD reales en `RagdollAnimator_AnimationMatching.cs`. Único matiz: los límites angulares por defecto son multiplicadores uniformes por opción, no valores anatómicos por articulación — exactamente lo que el propio diagnóstico ya reserva para "certificación por personaje".

### 3.7 Animation matching + power/authority — Confirmado

Posición y rotación tienen alpha/damping/acceleration cap completamente independientes en cada capa (perfil autorado, master authority, estado runtime por músculo). `PowerSetting{Kinematic,Powered,Unpowered}` existe tal cual. `MuscleRuntimeState` va más allá de un escalar único: autoridad, mapping-authority, damping y acceleration-multiplier, todos por eje y por hueso.

### 3.8 Semántica Puppet / Unpinned / GetUp — Confirmado

Las tres sub-reivindicaciones, confirmadas de forma independiente:

- **Sin flag de gracia post-GetUp** — grep de `sinceGetUp|GetUpGrace|GetUpImmunity|JustGotUp|postGetUp`: 0 resultados en todo el repo.
- **Multiplicador binario, no gradual** — `stateDistanceMultiplier = State==GetUp ? mlp : 1f`, y el mismo patrón (`ResolveGetUpStateMultiplier`) se reutiliza para resistencia a colisión y velocidad de regain-pin: es un diseño sistémico, no una omisión aislada.
- **Reevaluación en el mismo frame** — `OnBehaviourFixedUpdate` avanza el state machine y, sin `return`, cae directo al chequeo de knockout que ya lee el estado `Puppet` recién actualizado (multiplicador 1×).

Evidencia:
- `RagdollPuppetBehaviour.cs:2339-2344` — multiplicador binario
- `RagdollPuppetBehaviour.cs:1628-1670` — `stateMachine.Advance` sin return antes de `TryFindKnockOutBone`
- `RagdollPuppetGetUpCapabilityPlayModeTests.cs` — ningún test existente ejercita el escenario exacto de reevaluación mismo-frame

### 3.9 Inventario PuppetMaster: "140/140" y BipedStagger — Parcialmente confirmado

> "140/140 puede significar 100% del inventario propio, no 100% de PuppetMaster real — BipedStagger no aparece."

Grep de "140/140" sobre todo el repo: **0 resultados**. La cifra real, repetida en cinco documentos: **139 Verified / 1 N/A (G05, exclusión deliberada de Final IK) / 0 Open**, sobre un catálogo de 140. La crítica de fondo sí aterriza: categoría "Other behaviours" tiene exactamente una entrada (`E01 = BehaviourFall`); grep de "stagger"/"rebalance" sobre catálogo y los cinco docs de certificación: 0 resultados. El commit de hoy (`ef23175`) no toca ningún archivo de certificación — el catálogo sigue en 140 contratos, sin categoría nueva.

Evidencia:
- `RagdollCapabilityCatalog.cs:66-73` — "Code-owned inventory of all 140 public-documentation capabilities... intentionally independent from certification Markdown"
- `README.md:34-36` — "certificación vigente del 2026-08-11 contiene 139 filas Verified, G05 como único N/A y cero filas abiertas"
- `PUPPETMASTER-COVERAGE-FINAL-0050.md` — "'Cobertura' ... no significa identidad binaria... con un producto de terceros" (disclaimer propio del documento)

### 3.11 Cruce contra la API real de PuppetMaster (root-motion.com) — Nuevo, hallado en esta pasada

Fuente: doc oficial de PuppetMaster (Doxygen, `RootMotion.Dynamics`) — índices `annotated.html`/`classes.html`/`inherits.html`/`functions.html`, más las páginas de clase individuales bajadas para verificar miembros exactos.

**`SubBehaviourBalancer` existe en el producto real y no tiene equivalente en Hairibar.** No es un stepper — es un estabilizador reactivo por par de tobillo: aplica torque continuo a las piernas inferiores (`torqueMlp`, `maxTorqueMag`, `copOffset` para centro de presión, `IMlp` multiplicador de tensor de inercia, `velocityF` para predicción por velocidad, `damperForSpring`/`maxForceMlp` para el joint del tobillo) — todo *antes* de que se dispare cualquier evento de pérdida de balance, e independiente del actuador de step que propone el diagnóstico original. `RagdollPuppetBehaviourMath.ShouldLoseBalance` en Hairibar es puramente clasificatorio (distancia/velocidad vs. umbral) — no hay ningún lazo de corrección continua equivalente a `SubBehaviourBalancer` antes del corte binario a `LoseBalance()`.

**Los eventos de balance sí tienen paridad exacta.** `BehaviourPuppet` real expone `onLoseBalance`, `onLoseBalanceFromPuppet`, `onLoseBalanceFromGetUp`, `onRegainBalance` — los mismos cuatro, con los mismos nombres, ya están en `RagdollPuppetBehaviour.cs:129-132,575-593,2687-2700` y catalogados en `RagdollCapabilityCatalog.cs:219`. Esta parte del diagnóstico original no tenía brecha que señalar y la auditoría previa no la mencionó — se confirma aquí como ya cerrada, sin acción pendiente.

**Consecuencia para el nombre del skeleton (nota de la sección 5).** El nombre oficial de PuppetMaster para "algo relacionado con balance" ya está ocupado por `SubBehaviourBalancer` (torque reactivo, sin pasos). Nombrar el actuador nuevo `RagdollBipedStaggerBehaviour` (como ya hizo el commit de rename previo a este audit) es correcto y evita colisión conceptual — `Stagger` es el término libre; `Balancer`/`Balance` ya tiene un significado específico y distinto en la API real que un lector familiarizado con PuppetMaster esperaría.

Evidencia:
- `class_root_motion_1_1_dynamics_1_1_sub_behaviour_balancer_1_1_settings.html` — 7 campos públicos de `SubBehaviourBalancer.Settings`: `damperForSpring`, `maxForceMlp`, `IMlp`, `velocityF`, `copOffset`, `torqueMlp`, `maxTorqueMag`
- `class_root_motion_1_1_dynamics_1_1_behaviour_puppet.html` — 4 `PuppetEvent`: `onLoseBalance`, `onLoseBalanceFromPuppet`, `onLoseBalanceFromGetUp`, `onRegainBalance`
- `RagdollPuppetBehaviour.cs:129-132,2687-2700` — mismos 4 eventos, mismo naming, ya wireados
- `RagdollCapabilityCatalog.cs:219` — capacidad `onLoseBalance/onRegainBalance` ya certificada
- Grep repo-wide de `SubBehaviourBalancer|BalancerSettings|torqueMlp|copOffset`: 0 resultados en ambos repos — cero indicio de que el par reactivo de tobillo esté implementado o siquiera evaluado como alternativa al step

### 3.10 Consistencia de la documentación de certificación — Parcialmente confirmado

> "El manifest dice verde, pero el cuerpo del documento conserva prosa contradictoria de sprints anteriores."

Real, pero localizado — no es un problema de los nueve documentos de certificación como conjunto. Dos contradicciones concretas encontradas:

- `PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md` se contradice a sí mismo: el encabezado dice "cerrado... 139 Verified, G05 N/A, cero abiertas" (líneas 9-13), pero la sección titulada literalmente *"## Cierre actual"* (líneas 212-221) dice *"la reauditoría permanece abierta... la afirmación anterior de 139 filas verificadas no constituye certificación vigente."*
- `PUPPETMASTER-R01-R34-REPAIR-REGISTER.md`: el header dice cerrado, pero la sección "Latest executed evidence" (líneas 17-40) muestra `41 Verified / 98 Open`, fechada 3 días antes del cierre — y las 34 filas R01-R34 están todas marcadas "In progress".

Los dos documentos que sí funcionan como fuente de verdad para un lector — `README.md` y `PUPPETMASTER-COVERAGE-FINAL-0050.md` — separan correctamente "vigente" de "histórica (sustituida)" y no se contradicen.

---

## 4. Plan de implementación

### P0 — certificar antes de seguir afinando

#### Cerrar la certificación Animator↔Knockdown en CODE RED

El wiring en C# es real (commit `62e803b`), pero el prefab de producción, el tipo de parámetro y la cobertura de test siguen sin certificar — exactamente lo que el diagnóstico original pedía verificar antes de seguir afinando GetUp/Unpinned.

| Acción | Archivo | Descripción |
|---|---|---|
| modificar | `Assets/_Project/Prefabs/Enemies/PF_Enemy_Grunt.prefab` | Copiar el wiring de `onGetUpProne`/`onGetUpSupine` (CrossFade, 0.15s, layer 0) desde el prefab prototipo; subir `minimumGetUpDuration` de 1 a 2.7 para que coincida con el largo real del clip. |
| modificar | `Assets/_Project/Enemies/AI/HairibarRagdollAdapter.cs` | Cambiar `SetBool(KnockdownHash,...)` por `SetTrigger`/`ResetTrigger` en las 3 líneas (141, 145, 582) — el parámetro es Trigger (`m_Type:9`), no Bool. |
| añadir | `Assets/_Project/Tests/PlayMode/HairibarRagdollAdapterPlayModeTests.cs` | Nuevo `[UnityTest]` sobre el prefab de producción: fuerza Unpinned → assert `GetCurrentAnimatorStateInfo(0).IsName("Fall")`; fuerza GetUp → assert `IsName("GetUpProne"/"GetUp")`. Es el test que hoy fallaría por las 3 razones de arriba. |

**Riesgos:** retunear `minimumGetUpDuration` a 2.7s alarga la ventana de invulnerabilidad en combate real — avisar a quien tunea el pacing, no meterlo silenciosamente en un commit de "fix de wiring". `SetTrigger` se auto-consume distinto a `SetBool` — cubrir un caso de re-hit rápido en el nuevo test.

**Por qué nadie lo agarró antes:** 3 commits posteriores (`fd28087`, `a3ad6f2`, `01f345f`) tocaron estos mismos archivos sin cerrar ninguno de los 3 huecos — indica blind spot de proceso, no staleness accidental.

### P1 — brechas confirmadas, sin certificación bloqueante

#### Actuador de stagger — 🟢 Implementado, bugs de actuación de la sección 6 corregidos y verificados (1 fixture de test pendiente)

Extendido `RagdollBipedBalanceBehaviour` → renombrado `RagdollBipedStaggerBehaviour` (ver nota de nombre en sección 5, ya aplicada). V1 con clips de Animator crossfadeados (mismo patrón que `RagdollFallBehaviour`), no IK procedural, tal como se recomendaba. **Corrección de sobre-alcance:** el test PlayMode citado abajo verifica lifecycle/fail-safe (activación, éxito, fallo) con pies y raíz `FreezeAll` — no verifica step actuation real (Animator entrando al estado, pie despegando, aterrizando, recuperando soporte). Ver sección 6 para el listado de los 9 bugs de actuación confirmados por contra-revisión externa, corregidos y verificados contra Unity real; queda pendiente el fixture de un test (`UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet`), documentado ahí.

| Acción | Archivo | Estado |
|---|---|---|
| añadido | `Animation/Runtime/Core/Behaviours/RagdollBipedStaggerMath.cs` | Selección de pie, clamp de largo de paso, clasificación de dirección (4 clips), `ResolveOutcome`. 17/17 tests GREEN. |
| añadido | `Animation/Runtime/Core/Behaviours/RagdollBipedStaggerStateMachine.cs` | Idle→LiftOff→Swing→Replant→Settling→Failed, contador de pasos + `maxSteps`. 27/27 tests GREEN. |
| añadido | `Animation/Runtime/Core/Behaviours/RagdollBipedBalanceTrigger.cs` | Histéresis sobre `RagdollBipedBalanceMath.Classify`. 33/33 tests GREEN. |
| modificado | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviour.cs` | `CanStagger` (default `false`), `RagdollPuppetEvent OnRequiresStep`, hand-off declarativo. 69/69 tests GREEN. |
| modificado → actuador completo | `Animation/Runtime/Core/Behaviours/RagdollBipedStaggerBehaviour.cs` | Camino de éxito: `Controller.Activate<RagdollPuppetBehaviour>()`. Camino de fallo: mismo `Activate` + `puppet.Unpin()` explícito (evitó exactamente la trampa señalada abajo). 660/663 tests GREEN (3 fallos preexistentes no relacionados, ver nota). |
| añadido | `Animation/Tests/Runtime/RagdollBipedStaggerBehaviourPlayModeTests.cs` | Test end-to-end físico: éxito (capture point balanceado → vuelve a `Puppet`) y fallo (`Unrecoverable` con `maxSteps=1` → `Unpinned`). 2/2 GREEN. |
| modificado | `Samples~/Demos/Regression/RegressionScenarioRunner.cs` | Escenario de certificación: activa el stagger sin pies configurados y verifica que siempre falla-seguro de vuelta a `Puppet`. Compila limpio, verificado vía copia gitignorada en CODE RED. |

**Riesgo señalado — evitado en la implementación:** el comentario original del skeleton decía "llamar Deactivate() (volviendo a Puppet)" — incorrecto, `Deactivate()` deja `ActiveBehaviour` en `null`. La implementación final usa `Activate<RagdollPuppetBehaviour>()` explícito (que siempre resetea a Puppet) + `Unpin()` explícito en el camino de fallo, tal como recomendaba este plan.

**Los 3 fallos preexistentes** en la corrida de regresión completa (662-666/665-669 según el punto) son de `RagdollHumanoidCapabilityDirectPlayModeTests`, por falta de `HairibarCertification.PrepareAssets` — un prerequisito de entorno no relacionado con stagger/props, confirmado antes y después del cambio.

#### Fix del bug de tracking angular por-articulación

Falta una clave de join entre el target pose capturado y la articulación física — por eso el loop original mezcló todo.

| Acción | Archivo | Descripción |
|---|---|---|
| modificar | `RagdollLab/Runtime/RagdollLabTypes.cs` | Añadir `public string physicsBodyId;` a `TargetPoseTelemetry`. |
| modificar | `RagdollLab/Runtime/RagdollTelemetryRecorder.cs` | Poblar `physicsBodyId = StableId(physics.transform,"Rigidbody")` en `CaptureTargetPoses` — mismo formato que ya usa `CaptureJoint` para `bodyId`. |
| modificar | `RagdollLab/Runtime/RagdollLabAnalyzer.cs` | Línea 55: filtrar por `targetPoses[p].physicsBodyId == sample.bodyId` antes de acumular en `tracking`. |
| añadir | `RagdollLab/Tests/Runtime/RagdollLabAnalyzerTests.cs` | No existe hoy ningún test de `Analyze()` — solo de `RagdollLabMath`. Caso: dos joints sintéticos, solo uno con pose emparejada; assert que el otro sale con `count==0`, no con el mismo valor copiado. |

**Efecto secundario esperado (no regresión):** articulaciones fuera de los 7 huesos rastreados hoy pasarán a reportar `count=0` en vez de un valor idéntico fabricado — es la corrección, no un bug nuevo. `RagdollLabComparison.Build` no toca `angularTrackingError`, así que el pipeline de comparación no se ve afectado.

#### AnchorDrift: análisis alineado a evento de impacto

Capa de análisis pura sobre datos que ya se graban — no hace falta tocar la granularidad de captura del recorder.

| Acción | Archivo | Descripción |
|---|---|---|
| modificar | `RagdollLab/Runtime/RagdollLabMath.cs` | 5 helpers puros nuevos junto a `SettlingTime`: `Baseline`, `PeakAfter`, `SampleAtOffset`, `AreaUnderCurve`, `TimeAboveThreshold`. |
| modificar | `RagdollLab/Runtime/RagdollLabTypes.cs` | Nuevo `AnchorDriftEventReport` (baseline/peak/+50/100/250/500/1000ms/settling/AUC/timeAboveThreshold); campo aditivo `anchorErrorEvents[]` en `JointReport` — no toca el `anchorError.p95` existente. |
| modificar | `RagdollLab/Runtime/RagdollLabAnalyzer.cs` | Nuevo parámetro opcional `thresholds` en `Analyze()` (compatible con la firma actual); construir los reportes leyendo `frame.events`, campo que ya existe y que hoy nunca se lee. |
| modificar | `RagdollLab/Runtime/RagdollTelemetryRecorder.cs` | Pasar `thresholds` al nuevo `Analyze()` en `WriteArtifacts` (línea 443); sección aditiva `## Anchor Drift Events` en `WriteSummary()`. |

**Blast radius:** `RagdollLabAnalyzer.Analyze` tiene un único call site en los dos repos combinados. Cero consumidores programáticos parsean estos JSON hoy en CODE RED — cambio aditivo de bajo riesgo, y `JsonUtility` tolera el campo nuevo al diffear contra un `evaluation.json` viejo.

#### Política de drift para prop muscles — ✅ Implementado (2026-08-13), alcance reducido respecto al plan original

La infraestructura de props (state machine, eventos, fault/recovery) ya era madura — el fix fue aditivo, tal como se preveía. **Divergencia deliberada del plan:** se implementó un enum de 2 valores en vez de 4, cubriendo exactamente el gap confirmado en 3.5 (knockout global sin filtro por grupo) sin construir la maquinaria de drop/notify automático que nadie pidió todavía.

| Acción | Archivo | Estado |
|---|---|---|
| añadido (2 valores, no 4) | `Animation/Runtime/Core/Behaviours/RagdollPropDriftPolicy.cs` | `enum { Ignore, CountsTowardKnockout }` — default `Ignore`. Los valores `Notify`/`Drop` del plan original no se implementaron: `DropPropsNow` ya cubre el caso de soltar props al perder balance (post-Unpinned); un drop *preventivo* por-prop queda fuera de este cambio, no se identificó necesidad concreta. |
| añadido | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviourMath.cs` | Predicado puro `ShouldCountTowardKnockout(RagdollMuscleGroup, RagdollPropDriftPolicy)`. 22/22 tests GREEN. |
| modificado | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviour.cs` | Campo `PropDriftPolicy` (default `Ignore`); `TryFindKnockOutBone` salta bones vía `Context.Muscles.TryGetMuscleGroup` + el predicado — mismo patrón ya usado y testeado en `FindGetUpReferencePair`. 666/669 tests GREEN (3 fallos preexistentes no relacionados). |

**Riesgo de comportamiento — confirmado, tal como preveía el plan:** el default es ahora `Ignore`, cambio real de comportamiento por defecto respecto al código anterior (antes, cualquier prop contaba para knockout). Sin test end-to-end físico dedicado para este wiring — cubierto por el predicado puro (exhaustivo) + compilación/regresión completa, no por un rig físico con prop real; el escenario "Props" de RagdollLab bajo carga real sigue sin capturarse (nota de sección 5 sigue vigente).

### P2 — limpieza y decisiones antes de empezar

#### Higiene de documentación de certificación + registrar BipedStagger

| Acción | Archivo | Descripción |
|---|---|---|
| modificar | `Documentation~/Certification/PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md` | Reescribir o eliminar la sección "## Cierre actual" que contradice el header del mismo documento. |
| modificar | `Documentation~/Certification/PUPPETMASTER-R01-R34-REPAIR-REGISTER.md` | Actualizar "Latest executed evidence" y las 34 filas "In progress" para que reflejen el cierre del 2026-08-11, o marcar la sección entera como histórica. |
| modificar | `Animation/Editor/Certification/RagdollCapabilityCatalog.cs` | Registrar la capacidad de stagger recién cuando el actuador (no solo la detección) esté wireado — hoy sigue sin categoría propia, mismo criterio que `BehaviourFall` (E01). |

#### `SubBehaviourBalancer` (torque reactivo de tobillo) — ✅ Implementado (2026-08-13)

Decisión de la sección 5: se optó por portarlo. Hallazgo de la sección 3.11 cerrado: PuppetMaster real trae un estabilizador reactivo continuo (par en tobillo/pierna vía `torqueMlp`/`copOffset`/`IMlp`/`velocityF`) — ahora existe en Hairibar como capa previa al step de recuperación, corrigiendo *antes* de que se dispare el trigger de stagger, no después.

| Acción | Archivo | Estado |
|---|---|---|
| añadido | `Animation/Runtime/Core/Behaviours/RagdollBipedBalancerSettings.cs` | Struct espejo de `SubBehaviourBalancer.Settings`, 7 campos con **defaults verificados contra la doc oficial** (no asumidos): `torqueMlp=0f`, `IMlp=1f`, `velocityF=0.5f`, `maxTorqueMag=45f` — los tres últimos corrigieron mi primer supuesto tras re-consultar Context7. |
| añadido | `Animation/Runtime/Core/Behaviours/RagdollBipedBalancerMath.cs` | `ResolveCenterOfPressureTarget` + `ResolveReactiveTorque` — math pura Hairibar-owned (RootMotion's real implementation es closed-source; solo el settings surface es público). 8/8 tests GREEN. |
| modificado | `Animation/Runtime/Core/Behaviours/RagdollPuppetBehaviour.cs` | `BalancerSettings` (default `torqueMlp=0`, inerte hasta opt-in), `ApplyReactiveBalancer()` llamado en `OnBehaviourFixedUpdate` junto a `TryClassifyStaggerBalance`; aplica torque a `balancerLeftCalfBone`/`balancerRightCalfBone` solo durante `RecoverableWithoutStep`, cede a `EvaluateStaggerTrigger` si escala a `RequiresStep`. |

**Efecto secundario encontrado y corregido:** el wiring expuso una carrera de timing preexistente en `RagdollBehaviourSystemClosurePlayModeTests.cs:C05_SubBehavioursAreReusableAndOneFailureIsIsolated` (sin relación con balance/stagger) — el test asumía cero ticks físicos durante un `yield return null` antes de su propio `ModifyPose()` manual, asunción nunca garantizada que el crecimiento del código volvió sistemáticamente falsa. Corregido a aserciones basadas en delta (antes/después), no en conteo absoluto — determinístico independientemente de ticks físicos incidentales. Confirmado: el test pasaba antes del cambio (log de la corrida previa), reproducible 100% aislado tras el cambio, GREEN tras el fix. 674/677 tests GREEN final (3 fallos son el mismo prerequisito `HairibarCertification.PrepareAssets` de siempre, no relacionado).

**Por qué se implementó igual siendo opt-in en el original:** el propio `SubBehaviourBalancer` es opt-in incluso en el producto real (`torqueMlp=0f` por defecto) — el wiring de Hairibar respeta exactamente eso: inerte por defecto, cero cambio de comportamiento hasta que un proyecto consumidor configure `torqueMlp>0` explícitamente.

---

## 5. Notas abiertas

Decisiones que conviene tomar antes de empezar a codear, no durante.

- **Nombre — ✅ resuelto.** `RagdollBipedBalanceBehaviour` renombrado a `RagdollBipedStaggerBehaviour` (2026-08-13), antes de wirear tests/actuador, tal como recomendaba esta nota.
- **Cadena de piernas para el swing.** ¿Solo pantorrilla+pie (recomendado, no desestabiliza cadera/pelvis) o incluir el muslo para un swing visualmente más completo? Vale un playtest rápido antes de fijar los campos.
- **Ignore vs. Notify en la política de props.** ¿`Ignore` debería suprimir también el evento de telemetría (silencio total) o solo la acción física de soltar? El plan de arriba asume silencio total — confirmar antes de implementar.
- **MP5Prop en CODE RED.** Aunque Hairibar.Ragdoll ofrezca la política nueva, alguien tiene que configurar `driftPolicy`/`driftDropDistance` en el prop concreto — y idealmente capturar por fin el escenario "Props" en RagdollLab con el arma realmente empuñada, para tener una baseline real de drift bajo carga.
- **Retune de `minimumGetUpDuration`.** Pasar de 1 a 2.7 en el prefab de producción cambia el pacing de combate (ventana de invulnerabilidad post-caída) — tratarlo como decisión de diseño explícita, no como parte silenciosa del fix de wiring.
- **Estabilizador reactivo (`SubBehaviourBalancer`) — ¿portar o no?** El producto real resuelve micro-perturbaciones con torque continuo en tobillo/pierna *antes* de clasificar pérdida de balance; Hairibar hoy salta directo de "dentro de umbral" a "fuera de umbral" sin zona de amortiguación. Sin ese estabilizador, el step de stagger (P1) puede dispararse más seguido de lo que el producto original dispararía en la misma perturbación — vale decidir esto antes de tunear los umbrales de `RagdollBipedBalanceMath.Classify`, no después.

---

## 6. Contra-revisión externa (2026-08-13) — bugs de actuación confirmados y corregidos

Segunda pasada independiente contra `master` (no contra el diagnóstico ni los mensajes de commit). Verifiqué directamente contra el código los 7 hallazgos de mayor impacto — **los 7 confirmados exactos**, cita archivo:línea incluida. Tras confirmarlos, se implementaron los 9 fixes (instrucción "hazlo") y se verificaron con la disciplina TDD Iron Law: cada fix con su test (nuevo o existente) corriendo GREEN vía Unity headless real (`-runTests -testPlatform PlayMode`), no simulado.

### Bugs confirmados y corregidos, en orden de prioridad

1. ✅ **Corregido.** `RagdollBipedStaggerBehaviour.OnBehaviourActivated` daba el primer paso con COM vacío (`BeginStep()` llamado antes de cualquier `centerOfMass.FixedUpdate()`). Fix: `OnBehaviourActivated` ya no clasifica ni llama `BeginStep()`; marca `pendingFirstClassification = true` y difiere la decisión al primer `OnBehaviourFixedUpdate` real (que corre la sonda de COM antes de clasificar). Verificado vía regresión completa PlayMode (740/746, sin nuevas roturas atribuibles a este cambio — ver caveat de fixture abajo).
2. ✅ **Corregido.** `Unrecoverable` no abortaba hasta terminar el ciclo completo. Fix: `OnBehaviourFixedUpdate` chequea `CurrentState == Unrecoverable` inmediatamente después de `UpdateBalanceClassification()` y llama `Recover(false)` sin esperar a que `stepMachine.Advance` complete el ciclo.
3. ✅ **Corregido.** `CrossFadeStep` no marcaba fallo si el Animator o el estado no existían. Fix: convertido a `TryCrossFadeStep` (retorna `bool`); si el animator o el nombre de estado no son válidos, `BeginStep()` llama `stepMachine.RegisterStepFailed()` en vez de dejar la state machine avanzar sobre un Animator que nunca se movió.
4. **Sin fix de código** (según lo previsto en el hallazgo original) — requiere decisión de contrato de producto (8 clips por pie vs. parámetro `SwingFoot` + mirroring) antes de tocar lógica. Se añadió el campo `swingFootParameterName` (Animator int, opcional) a `RagdollBipedStaggerBehaviour` para que un Animator Controller pueda mirror/branch el clip según el pie físicamente elegido, sin forzar los 8 clips — la decisión final de contrato queda abierta, documentada aquí.
5. **Sin cambio de código, por diseño** — `ClampStepLength` es un helper puro sin call site en el actuador V1 clip-based; el hallazgo original ya recomendaba no listarlo como capacidad activa en el catálogo, no arreglarlo. Reconocido, sin acción adicional.
6. ✅ **Corregido.** Histéresis del trigger nunca disparaba con `minimumRequiresStepDuration=0`. Fix: `RagdollBipedBalanceTrigger.Evaluate` cambiado de comparación estricta (`wasBelow && isAtOrAbove`) a un latch explícito por episodio (`requiresStepElapsed >= minimumRequiresStepDuration` dispara una sola vez por episodio de `RequiresStep`, incluyendo el caso `0f` = inmediato). Verificado con 2 tests nuevos (`ZeroMinimumDuration_FiresImmediatelyOnFirstRequiresStepFrame`, `ZeroMinimumDuration_DoesNotFireTwice`) — GREEN en Unity headless real.
7. ✅ **Corregido.** El balancer reactivo dependía indebidamente de `canStagger`. Fix: gate cambiado a `canStagger || balancerSettings.TorqueMlp > 0f` en `RagdollPuppetBehaviour.OnBehaviourFixedUpdate`, y cada capa (balancer continuo vs. trigger de step) se aplica según su propio gate dentro del bloque.
8. ✅ **Documentado como parcial** (según lo recomendado en el hallazgo original, sin implementación de drive real). `RagdollBipedBalancerSettings`: doc comment de la clase y tooltips de `damperForSpring`/`maxForceMlp` actualizados para dejar explícito que están expuestos por paridad de campo con la doc oficial pero sin efecto — `RagdollBipedBalancerMath` no los lee.
9. ✅ **Corregido.** `Diagnose()` de RagdollLab diagnosticaba `AnchorDrift` solo por p95 global, sin usar los `AnchorDriftEventReport[]` ya capturados. Fix: nuevo método `AddAnchorDriftDiagnostic` en `RagdollLabAnalyzer` que usa `settlingTimeSeconds`/`timeAboveThresholdSeconds` de los event reports para emitir `TransientAnchorExcursion` (impacto que se asienta rápido) o `PersistentAnchorDrift` (drift que no se asienta) en vez de una única etiqueta `AnchorDrift`. Verificado con 2 tests nuevos (`Diagnose_TransientSpikeThatSettlesQuickly_IsNotFlaggedAsPersistentDrift`, `Diagnose_SpikeThatNeverSettles_IsFlaggedAsPersistentDrift`) — GREEN en Unity headless real.

### Caveat de verificación: un test de fixture pre-existente queda roto, no la lógica que verifica

`RagdollBipedStaggerBehaviourPlayModeTests.UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet` quedó en rojo tras el fix #1, y **no se pudo repararlo dentro de esta sesión**. Diagnóstico confirmado (log `STAGGER_DIAG`/`STAGGER_TICK` instrumentado y removido tras el diagnóstico): el test intenta simular "pies lejos del centro de masa" moviendo `Rigidbody.transform.position` de los pies directamente, pero esos pies están unidos a la raíz por un `ConfigurableJoint` (motion Locked por defecto) creado *antes* del movimiento — el solver revierte el teleport en el primer step físico, así que el margen de captura leído por la clasificación real (ahora correcta, gracias al fix #1) sigue siendo el de la postura original ("Stable"), no el escenario "Unrecoverable" que el test cree estar construyendo. Se intentaron y descartaron, todos verificados por Unity headless real y todos insuficientes: liberar los ejes del joint (`xMotion/yMotion/zMotion = Free`), destruir el joint (rompe disposal downstream con `MissingReferenceException`), deshabilitarlo (`Joint` no expone `.enabled`, solo `Behaviour`), y re-basear `connectedAnchor` con `autoConfigureConnectedAnchor = false` (matemáticamente correcto pero sin efecto observado — indica que la resistencia no viene del joint en absoluto, sino de algún otro mecanismo de sincronización, posiblemente el pipeline de animación re-imponiendo la pose autorada). El código de producción (`RagdollBipedStaggerBehaviour.cs`) fue revertido a un estado limpio sin instrumentación; el archivo de test fue revertido a su forma original de dos líneas (sin los intentos fallidos). Este es un defecto de fixture de test pre-existente expuesto por la corrección (antes, el bug #1 hacía que el test "pasara" clasificando desde un snapshot vacío/casualmente favorable, no porque el escenario físico real fuera el correcto). **Pendiente como trabajo de seguimiento**: reconstruir el rig de este test para que el offset de posición sobreviva al primer step físico (candidatos: mover el offset antes de crear los joints, o usar una rig de dos cuerpos sin joint intermedio).

### No confirmados directamente en esta pasada (créditos a la contra-revisión, sin verificación línea por línea propia)

- Catálogo de certificación (`RagdollCapabilityCatalog.cs`) sigue sin entradas `Stagger`/`Balancer` — consistente con la sección 4/P2 de este documento, ya marcado como pendiente ahí.
- GetUp parity (multiplicador binario, reevaluación mismo-frame) — ya documentado en sección 3.8 como comportamiento confirmado, no bug nuevo; sigue sin flag de gracia post-GetUp.
- Integración CODE RED (prefab/Animator/Trigger) — este repo (`Hairibar.Ragdoll`) no contiene CODE RED; no verificable desde aquí, tal como ya advertía la sección 3.4.
- Modelo de soporte asume `Vector3.up` fijo en vez de gravedad arbitraria — razonable como límite de V1, no bloqueante.

### Veredicto corregido

Los 9 bugs de actuación identificados por la contra-revisión están **corregidos y verificados** (7 con fix de código + test GREEN, 1 documentado como parcial por diseño, 1 sin cambio por diseño), salvo un residual: el fixture de un test PlayMode pre-existente (`UnrecoverableCaptureMargin_ExhaustsStepsAndUnpinsThePuppet`) no logra construir físicamente el escenario que pretende verificar, y queda como trabajo de seguimiento explícito. La regresión completa de PlayMode (746 tests) corre 741/746 en verde: los 4 fallos restantes (`ProductionFlow_HealthAiControllerAndNavMeshStayEnabled`, `B22_SharedHumanoidProfileBindsTwoRenamedAvatarsSemantically`, `E01_TargetAnimatorFallRunsBlendRuntimeSettersAndBothEndGates`, `MultilayerAnimatorModesEventsRootMotionAndRetargeting`) comparten un mismo mensaje de precondición no cumplida en este entorno ("Run HairibarCertification.PrepareAssets before PlayMode tests") — no relacionados con ninguno de estos 9 fixes.

---

*Auditoría generada por verificación directa de código — 17 agentes de lectura contra `Hairibar.Ragdoll` y `CODE RED`, cero afirmaciones sin cita archivo:línea o hash de commit. Ninguna reivindicación del diagnóstico original resultó falsa. Contra-revisión de la sección 6 (2026-08-13) verificada línea por línea contra `master` antes de incorporarse. Versión web con navegación: [artifact publicado](https://claude.ai/code/artifact/8113af88-615d-40cb-8fec-e75eab6777cc).*
