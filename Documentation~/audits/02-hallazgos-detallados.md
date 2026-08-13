## 3. Hallazgos detallados

### 3.1 Balance bÃ­pedo / stagger â€” Parcialmente confirmado

> "Falta completamente el equivalente a `BehaviourBipedStagger`: selecciÃ³n de pie, lift-off, swing, aterrizaje, replant, abandono a Unpinned."

El actuador estÃ¡, en efecto, 100% ausente â€” cero resultados para `Stagger|SwingPhase|LiftOff|LandingTarget|StepGenerator|Replant` en ambos repos. Pero **horas antes de este audit**, el commit `ef23175` aÃ±adiÃ³ `RagdollBipedBalanceBehaviour.cs` + `RagdollBipedBalanceMath.cs`: matemÃ¡tica de capture point casi verbatim a la propuesta del diagnÃ³stico, con clasificaciÃ³n en 4 estados (no 3) â€” `Stable / RecoverableWithoutStep / RequiresStep / Unrecoverable`. Es un mÃ³dulo muerto: dispara un evento `BalanceStateChanged` sin un solo suscriptor, nunca se auto-activa, nunca llama a `Controller.Activate/Deactivate`. El propio comentario de clase ya avisa: *"Wiring that trigger, and the actual foot movement, is out of scope for this change."*

Evidencia:
- `RagdollBipedBalanceMath.cs:23-33` â€” `CapturePoint()`: `omega = sqrt(max(0.01,gravity)/max(0.05,pendulumLength))`; retorna `centerOfMass + ProjectOnPlane(velocity,up)/omega`
- `RagdollBipedBalanceBehaviour.cs:52,101-108` â€” `BalanceStateChanged` es un `Action` plano, cero suscriptores en ningÃºn repo
- `RagdollBehaviourController.cs:239-301,380-422` â€” mecanismo de activaciÃ³n exclusiva (`Activate<T>`/`ActivateByExactTypeName`) que BipedBalance nunca invoca
- `RagdollPuppetEvent.cs:102-146` â€” el wiring declarativo "switchToBehaviour" que `RagdollFallBehaviour` ya usa para su `onEnd` â€” BipedBalance no lo usa
- git `ef23175` â€” "Detection only: no foot actuation, no auto-activation, not wired to any trigger yet."

### 3.2 RagdollLab: bug de tracking angular por-articulaciÃ³n â€” Confirmado

> "El loop acumula error contra TODOS los target poses en vez del target de esa articulaciÃ³n â€” comparaciones cruzadas no fiables."

Confirmado exactamente, y mÃ¡s severo: la lista de tracking no depende del Ã­ndice de articulaciÃ³n `j` en absoluto, asÃ­ que **el `MetricSummary` sale byte-idÃ©ntico para las 21 articulaciones del reporte** â€” "spine_02 vs upperarm_r" no es solo poco fiable, es matemÃ¡ticamente imposible que difiera. `AnchorError` queda intacto, tal como el diagnÃ³stico intuye â€” es un cÃ¡lculo estructuralmente separado.

Evidencia:
- `RagdollLabAnalyzer.cs:55` â€” `for (p...) tracking.Add(frames[i].targetPoses[p].targetPhysicsAngularError)` â€” nunca referencia `j`
- `RagdollTelemetryRecorder.cs:182-208` â€” `CaptureTargetPoses` solo captura 7 huesos fijos, sin id que los enlace a la articulaciÃ³n (por eso el loop original "mezclÃ³ todo": no habÃ­a clave de join)
- git blame â€” todo atribuido a `51da212` (2026-08-08), sin tocar desde entonces; cero tests ejercitan `Analyze()` hoy

Fix propuesto: aÃ±adir `physicsBodyId` a `TargetPoseTelemetry`, poblarlo en `CaptureTargetPoses` con el mismo `StableId(transform,"Rigidbody")` que ya usa `CaptureJoint`, y filtrar el loop del analizador por `physicsBodyId == sample.bodyId`. Ver plan completo abajo (P1).

### 3.3 AnchorDrift: un solo p95 mezcla impacto grande+recuperaciÃ³n rÃ¡pida con drift persistente â€” Confirmado

> "RagdollLab deberÃ­a capturar baseline/peak/+Nms/settlingTime/AUC/timeAboveThreshold por evento de impacto."

Confirmado: `Analyze()` colapsa todo el run a 6 nÃºmeros (`current/mean/rms/p95/max/normalizedMean`), sin eje temporal. El diagnÃ³stico global "AnchorDrift" se dispara solo con ese p95, sin `firstFrame`/`peakFrame` pese a que el schema ya los define. Matiz importante: **esto es mÃ¡s barato de lo que suena** â€” el recorder ya guarda la serie cruda por-frame en `frames.json` y ya marca el instante del impacto vÃ­a `EventMarker` (que CODE RED ya usa: `"eventApplied"` en `RagdollTestScenario.cs:123-125`). El analizador simplemente nunca lee ese campo. `SettlingTime()` tambiÃ©n existe ya en `RagdollLabMath.cs`, pero aplicado a velocidad angular relativa, no a `anchorError`.

Evidencia:
- `RagdollLabAnalyzer.cs:83-84` â€” `if (joint.anchorError.p95 > thresholds.anchorErrorWarningMeters)` â€” Ãºnico gatillo, `firstFrame`/`peakFrame` hardcoded a `0,0`
- `RagdollTelemetryRecorder.cs:449` â€” `frames.json` serializa la lista completa sin agregar, ~251 frames Ã— 21 joints en CriticalKnockdown
- CODE RED `RagdollTestScenario.cs:123-125` â€” marca `"eventApplied"` en el frame exacto del impulso, dato ya disponible y nunca leÃ­do por el analizador

### 3.4 IntegraciÃ³n Animator â†” Hairibar (Knockdown) â€” Parcialmente confirmado, P0

> "El parÃ¡metro 'Knockdown' no estaba siendo alimentado por Hairibar; hay que certificar esto antes de seguir afinando GetUp/Unpinned."

Cierto hasta el commit `62e803b` (CODE RED, 01:43 del mismo dÃ­a) â€” *tres horas antes* del commit de balance bÃ­pedo. `HairibarRagdollAdapter.cs` ahora sÃ­ se suscribe al `StateChanged` real de `RagdollPuppetBehaviour` y llama `Animator.SetBool("Knockdown",...)`. El propio commit narra el bug que arregla. Pero verificar esta certificaciÃ³n â€” como el diagnÃ³stico pide â€” encontrÃ³ **tres huecos que ni el commit ni el diagnÃ³stico mencionan**, y que sobrevivieron a tres commits posteriores (`fd28087`, `a3ad6f2`, `01f345f`) que tocaron estos mismos archivos sin cerrarlos:

- **Prefab equivocado.** El fix de CrossFade GetUp/GetUpProne solo se aplicÃ³ a `PF_Enemy_Grunt_HairibarPrototype.prefab` (prototipo de RagdollLab). `PF_Enemy_Grunt.prefab` â€” el que carga la suite de PlayMode tests y el que se envÃ­a â€” sigue con `onGetUpProne`/`onGetUpSupine` serializados vacÃ­os.
- **Bool vs. Trigger.** `AC_Enemy.controller` declara `Knockdown` como `m_Type:9` â€” cruzado contra `Speed` (`m_Type:1`=Float, confirmado por `SetFloat`) y `Attacking` (`m_Type:4`=Bool, confirmado por `SetBool`) esto solo cuadra si `9=Trigger`. El adapter llama `SetBool` sobre un parÃ¡metro Trigger.
- **Cero cobertura del lado Animator.** Los 1188+ lÃ­neas de `HairibarRagdollAdapterPlayModeTests.cs` solo aseveran el enum interno de Hairibar (`RagdollPuppetBehaviour.State`) â€” ningÃºn test llama `GetCurrentAnimatorStateInfo` ni `GetBool`.

Evidencia:
- git `62e803b` (CODE RED) â€” "the 'Knockdown' Animator bool... never driven by any production code"
- `PF_Enemy_Grunt.prefab:653-664` â€” `onGetUpProne`/`onGetUpSupine`: `animations: []`, producciÃ³n, sin wiring
- `AC_Enemy.controller:363-368,375-380` â€” `Attacking m_Type:4`, `Knockdown m_Type:9`
- `HairibarRagdollAdapter.cs:141,145,582` â€” `SetBool(KnockdownHash,...)` contra un parÃ¡metro declarado Trigger

### 3.5 Prop muscle: divergencia MP5Prop sin polÃ­tica â€” Parcialmente confirmado

> "Un prop muscle mostrÃ³ divergencia real; hoy no hay polÃ­tica â€” cualquier drift de prop derriba al puppet entero."

La mitad de polÃ­tica: 100% confirmada. `TryFindKnockOutBone` itera cada `AnimatedPair` â€” cuerpo o prop â€” por el mismo chequeo de `knockOutDistance`, sin branch por `RagdollMuscleGroup`. El Ãºnico mecanismo prop-aware (`DropPropsNow`) actÃºa *despuÃ©s* de que todo el puppet ya entrÃ³ en Unpinned, no antes â€” lo opuesto a una polÃ­tica dirigida.

La mitad de "divergencia real" tiene un matiz importante: en los cuatro artifacts disponibles (CriticalKnockdown, StrongKnockdown, Recovery, GameplayHit), el arma **nunca fue recogida** â€” fuerza/torque del joint leen `0.0` en todo el run, y la telemetrÃ­a `propPeakLiveDivergence` construida especÃ­ficamente para esto lee `0.000` en el log inspeccionado. El propio `experiment-log.md` del proyecto dice: *"not yet established as the cause of the global collapse."* Es una seÃ±al real y ya investigada â€” pero documenta un slot vacÃ­o, no un arma cargada bajo combate.

Evidencia:
- `RagdollPuppetBehaviour.cs:2339-2426` â€” `TryFindKnockOutBone`, sin filtro de `RagdollMuscleGroup`
- `RagdollPuppetBehaviour_Props.cs:60-123` â€” `DropPropsNow`: reactivo global post-Unpinned, no preventivo por-prop
- CODE RED `Artifacts/.../CriticalKnockdown/evaluation.json:105-165` â€” MP5PropMuscle `anchorError p95=1.847m`, force/torque=0.0
- CODE RED `experiment-log.md:36-37` â€” drift 0.6144m, fuerza p95 solo 9.115N, "not yet established as the cause"

### 3.6 FÃ­sica core â€” Confirmado

Sin stubs, TODOs ni `NotImplementedException` en las ocho carpetas escaneadas. Masa/inercia con 3 modos explÃ­citos (`PreserveAuthored/ResetFromColliders/ResetAndStabilize`), solver reconfigurable en runtime vÃ­a `RagdollPhysicsQualityController`, drives PD reales en `RagdollAnimator_AnimationMatching.cs`. Ãšnico matiz: los lÃ­mites angulares por defecto son multiplicadores uniformes por opciÃ³n, no valores anatÃ³micos por articulaciÃ³n â€” exactamente lo que el propio diagnÃ³stico ya reserva para "certificaciÃ³n por personaje".

### 3.7 Animation matching + power/authority â€” Confirmado

PosiciÃ³n y rotaciÃ³n tienen alpha/damping/acceleration cap completamente independientes en cada capa (perfil autorado, master authority, estado runtime por mÃºsculo). `PowerSetting{Kinematic,Powered,Unpowered}` existe tal cual. `MuscleRuntimeState` va mÃ¡s allÃ¡ de un escalar Ãºnico: autoridad, mapping-authority, damping y acceleration-multiplier, todos por eje y por hueso.

### 3.8 SemÃ¡ntica Puppet / Unpinned / GetUp â€” Confirmado

Las tres sub-reivindicaciones, confirmadas de forma independiente:

- **Sin flag de gracia post-GetUp** â€” grep de `sinceGetUp|GetUpGrace|GetUpImmunity|JustGotUp|postGetUp`: 0 resultados en todo el repo.
- **Multiplicador binario, no gradual** â€” `stateDistanceMultiplier = State==GetUp ? mlp : 1f`, y el mismo patrÃ³n (`ResolveGetUpStateMultiplier`) se reutiliza para resistencia a colisiÃ³n y velocidad de regain-pin: es un diseÃ±o sistÃ©mico, no una omisiÃ³n aislada.
- **ReevaluaciÃ³n en el mismo frame** â€” `OnBehaviourFixedUpdate` avanza el state machine y, sin `return`, cae directo al chequeo de knockout que ya lee el estado `Puppet` reciÃ©n actualizado (multiplicador 1Ã—).

Evidencia:
- `RagdollPuppetBehaviour.cs:2339-2344` â€” multiplicador binario
- `RagdollPuppetBehaviour.cs:1628-1670` â€” `stateMachine.Advance` sin return antes de `TryFindKnockOutBone`
- `RagdollPuppetGetUpCapabilityPlayModeTests.cs` â€” ningÃºn test existente ejercita el escenario exacto de reevaluaciÃ³n mismo-frame

### 3.9 Inventario PuppetMaster: "140/140" y BipedStagger â€” Parcialmente confirmado

> "140/140 puede significar 100% del inventario propio, no 100% de PuppetMaster real â€” BipedStagger no aparece."

Grep de "140/140" sobre todo el repo: **0 resultados**. La cifra real, repetida en cinco documentos: **139 Verified / 1 N/A (G05, exclusiÃ³n deliberada de Final IK) / 0 Open**, sobre un catÃ¡logo de 140. La crÃ­tica de fondo sÃ­ aterriza: categorÃ­a "Other behaviours" tiene exactamente una entrada (`E01 = BehaviourFall`); grep de "stagger"/"rebalance" sobre catÃ¡logo y los cinco docs de certificaciÃ³n: 0 resultados. El commit de hoy (`ef23175`) no toca ningÃºn archivo de certificaciÃ³n â€” el catÃ¡logo sigue en 140 contratos, sin categorÃ­a nueva.

Evidencia:
- `RagdollCapabilityCatalog.cs:66-73` â€” "Code-owned inventory of all 140 public-documentation capabilities... intentionally independent from certification Markdown"
- `README.md:34-36` â€” "certificaciÃ³n vigente del 2026-08-11 contiene 139 filas Verified, G05 como Ãºnico N/A y cero filas abiertas"
- `PUPPETMASTER-COVERAGE-FINAL-0050.md` â€” "'Cobertura' ... no significa identidad binaria... con un producto de terceros" (disclaimer propio del documento)

### 3.11 Cruce contra la API real de PuppetMaster (root-motion.com) â€” Nuevo, hallado en esta pasada

Fuente: doc oficial de PuppetMaster (Doxygen, `RootMotion.Dynamics`) â€” Ã­ndices `annotated.html`/`classes.html`/`inherits.html`/`functions.html`, mÃ¡s las pÃ¡ginas de clase individuales bajadas para verificar miembros exactos.

**`SubBehaviourBalancer` existe en el producto real y no tiene equivalente en Hairibar.** No es un stepper â€” es un estabilizador reactivo por par de tobillo: aplica torque continuo a las piernas inferiores (`torqueMlp`, `maxTorqueMag`, `copOffset` para centro de presiÃ³n, `IMlp` multiplicador de tensor de inercia, `velocityF` para predicciÃ³n por velocidad, `damperForSpring`/`maxForceMlp` para el joint del tobillo) â€” todo *antes* de que se dispare cualquier evento de pÃ©rdida de balance, e independiente del actuador de step que propone el diagnÃ³stico original. `RagdollPuppetBehaviourMath.ShouldLoseBalance` en Hairibar es puramente clasificatorio (distancia/velocidad vs. umbral) â€” no hay ningÃºn lazo de correcciÃ³n continua equivalente a `SubBehaviourBalancer` antes del corte binario a `LoseBalance()`.

**Los eventos de balance sÃ­ tienen paridad exacta.** `BehaviourPuppet` real expone `onLoseBalance`, `onLoseBalanceFromPuppet`, `onLoseBalanceFromGetUp`, `onRegainBalance` â€” los mismos cuatro, con los mismos nombres, ya estÃ¡n en `RagdollPuppetBehaviour.cs:129-132,575-593,2687-2700` y catalogados en `RagdollCapabilityCatalog.cs:219`. Esta parte del diagnÃ³stico original no tenÃ­a brecha que seÃ±alar y la auditorÃ­a previa no la mencionÃ³ â€” se confirma aquÃ­ como ya cerrada, sin acciÃ³n pendiente.

**Consecuencia para el nombre del skeleton (nota de la secciÃ³n 5).** El nombre oficial de PuppetMaster para "algo relacionado con balance" ya estÃ¡ ocupado por `SubBehaviourBalancer` (torque reactivo, sin pasos). Nombrar el actuador nuevo `RagdollBipedStaggerBehaviour` (como ya hizo el commit de rename previo a este audit) es correcto y evita colisiÃ³n conceptual â€” `Stagger` es el tÃ©rmino libre; `Balancer`/`Balance` ya tiene un significado especÃ­fico y distinto en la …2166 tokens truncated…n):** articulaciones fuera de los 7 huesos rastreados hoy pasarÃ¡n a reportar `count=0` en vez de un valor idÃ©ntico fabricado â€” es la correcciÃ³n, no un bug nuevo. `RagdollLabComparison.Build` no toca `angularTrackingError`, asÃ­ que el pipeline de comparaciÃ³n no se ve afectado.

#### AnchorDrift: anÃ¡lisis alineado a evento de impacto

Capa de anÃ¡lisis pura sobre datos que ya se graban â€” no hace falta tocar la granularidad de captura del recorder.

| AcciÃ³n | Archivo | DescripciÃ³n |
|---|---|---|
| modificar | `RagdollLab/Runtime/RagdollLabMath.cs` | 5 helpers puros nuevos junto a `SettlingTime`: `Baseline`, `PeakAfter`, `SampleAtOffset`, `AreaUnderCurve`, `TimeAboveThreshold`. |
| modificar | `RagdollLab/Runtime/RagdollLabTypes.cs` | Nuevo `AnchorDriftEventReport` (baseline/peak/+50/100/250/500/1000ms/settling/AUC/timeAboveThreshold); campo aditivo `anchorErrorEvents[]` en `JointReport` â€” no toca el `anchorError.p95` existente. |
| modificar | `RagdollLab/Runtime/RagdollLabAnalyzer.cs` | Nuevo parÃ¡metro opcional `thresholds` en `Analyze()` (compatible con la firma actual); construir los reportes leyendo `frame.events`, campo que ya existe y que hoy nunca se lee. |
| modificar | `RagdollLab/Runtime/RagdollTelemetryRecorder.cs` | Pasar `thresholds` al nuevo `Analyze()` en `WriteArtifacts` (lÃ­nea 443); secciÃ³n aditiva `## Anchor Drift Events` en `WriteSummary()`. |

**Blast radius:** `RagdollLabAnalyzer.Analyze` tiene un Ãºnico call site en los dos repos combinados. Cero consumidores programÃ¡ticos parsean estos JSON hoy en CODE RED â€” cambio aditivo de bajo riesgo, y `JsonUtility` tolera el campo nuevo al diffear contra un `evaluation.json` viejo.

#### PolÃ­tica de drift para prop muscles â€” âœ… Implementado (2026-08-13), alcance reducido respecto al plan original

La infraestructura de props (state machine, eventos, fault/recovery) ya era madura â€” el fix fue aditivo, tal como se preveÃ­a. **Divergencia deliberada del plan:** se implementÃ³ un enum de 2 valores en vez de 4, cubriendo exactamente el gap confirmado en 3.5 (knockout global sin filtro por grupo) sin construir la maquinaria de drop/notify automÃ¡tico que nadie pidiÃ³ todavÃ­a.

| AcciÃ³n | Archivo | Estado |
|---|---|---|
| aÃ±adido (2 valores, no 4) | `Animation/Runtime/Core/Behaviours/RagdollPropDriftPolicy.cs` | `enum { Ignore, CountsTowardKnockout }` â€” default `Ignore`. Los valores `Notify`/`Drop` del plan original no se implementaron: `DropPropsNow` ya cubre el caso de soltar props al perder balance (post-Unpinned); un drop *preventivo* por-prop queda fuera de este cambio, no se identificÃ³ necesidad concreta. |
