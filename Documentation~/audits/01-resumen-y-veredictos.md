# AuditorÃ­a del diagnÃ³stico de active ragdoll â€” 2026-08-12

VerificaciÃ³n lÃ­nea por lÃ­nea de 10 reivindicaciones de un diagnÃ³stico externo, contrastadas contra el cÃ³digo real de `Hairibar.Ragdoll` y `CODE RED` (17 agentes de lectura directa, sin asumir nada), con plan de implementaciÃ³n para cada brecha confirmada.

**2 repos** Â· Hairibar.Ragdoll + CODE RED Â· **5 confirmadas** Â· **5 parciales** Â· **0 refutadas**

---

## 1. Resumen

El diagnÃ³stico original acierta en la arquitectura general: el core fÃ­sico estÃ¡ sÃ³lido, y lo que falta es capa de decisiÃ³n/recuperaciÃ³n, no mÃ¡s fÃ­sica. Ninguna de las 10 reivindicaciones verificadas resultÃ³ falsa â€” pero 5 de 10 necesitan correcciÃ³n de matiz, y una coincidencia cambia el orden de prioridades: **dos de las brechas que el diagnÃ³stico marca como "falta completamente" ya estaban siendo cerradas el mismo dÃ­a**, horas antes de este audit.

Commit `ef23175` ("feat(behaviours): add biped balance detection skeleton", Hairibar.Ragdoll, 03:56) implementa casi literalmente la matemÃ¡tica de capture point que el diagnÃ³stico propone en su secciÃ³n 3 â€” `Ï‰â‚€=âˆš(g/h)`, `x_cp=x_com+v_com/Ï‰â‚€` â€” como mÃ³dulo de clasificaciÃ³n (Stable/RecoverableWithoutStep/RequiresStep/Unrecoverable). Es detecciÃ³n pura: cero suscriptores, cero actuaciÃ³n, cero wiring. El instinto del diagnÃ³stico era correcto; la foto que describe ya no es exacta.

Commit `62e803b` ("Wired the 'Knockdown' Animator bool... never driven by any production code", CODE RED, 01:43) cierra exactamente el hueco P0 que el diagnÃ³stico seÃ±ala en su punto 6. Pero verificar esa certificaciÃ³n revelÃ³ **tres huecos nuevos que el diagnÃ³stico no menciona**: el fix de CrossFade GetUp/GetUpProne se aplicÃ³ al prefab de prototipo de RagdollLab, no al prefab de producciÃ³n; el parÃ¡metro `Knockdown` del AnimatorController es de tipo `Trigger` pero el cÃ³digo llama `SetBool`; y cero tests automatizados verifican el lado Animator de la integraciÃ³n.

El resto del diagnÃ³stico se sostiene con matices: el bug de `RagdollLabAnalyzer` es *peor* de lo descrito (no ruidoso â€” literalmente idÃ©ntico entre todas las articulaciones), la correcciÃ³n de AnchorDrift es *mÃ¡s barata* de lo implÃ­cito (la telemetrÃ­a cruda por-frame y los event markers de impacto ya existen; falta solo la capa de anÃ¡lisis), y la cifra "140/140" no aparece en ningÃºn documento â€” el nÃºmero real, repetido en cinco docs, es **139 Verified / 1 N/A / 0 Open**, aunque la crÃ­tica de fondo (denominador auto-definido, `BipedStagger` ausente del inventario) es correcta.

| | |
|---|---|
| Commits del mismo dÃ­a que adelantan brechas P0 | **2** |
| Huecos nuevos hallados en la integraciÃ³n Animator ya "cerrada" | **3** |
| Reivindicaciones falsas | **0** |
| Cifras citadas que no existen textualmente ("140/140") | **1** |

---

## 2. Tabla de veredictos

| ReivindicaciÃ³n | Veredicto | Hallazgo clave |
|---|---|---|
| Balance bÃ­pedo / stagger "falta completamente" | **Parcial** | Actuador (paso de recuperaciÃ³n) 100% ausente â€” confirmado. Pero el mÃ³dulo de *detecciÃ³n* (capture point + clasificaciÃ³n) ya existe, commiteado horas antes del audit, sin wiring. |
| Bug de tracking angular por-articulaciÃ³n en RagdollLab | **Confirmado** | Peor que lo descrito: el valor sale *idÃ©ntico* en todas las articulaciones del reporte, no solo "poco fiable". |
| AnchorDrift resumido en un solo p95 | **Confirmado** | Cierto, y mÃ¡s barato de arreglar de lo implÃ­cito: la serie cruda por-frame y los event markers de impacto ya se capturan; falta solo la capa de anÃ¡lisis. |
| IntegraciÃ³n Animatorâ†”Knockdown sin certificar (P0) | **Parcial** | Cierto hasta hace horas â€” ya wireado el mismo dÃ­a. Pero el prefab de producciÃ³n sigue sin el CrossFade de GetUp, el parÃ¡metro es Trigger tratado como Bool, y no hay test que lo verifique. |
| MP5Prop: divergencia real sin polÃ­tica | **Parcial** | La ausencia de polÃ­tica es 100% real. Pero la divergencia medida es de un arma *no empuÃ±ada* (slot vacÃ­o), no de una carga bajo combate. |
| FÃ­sica core (joints/masa/solver/colisiÃ³n) â€” "fuerte" | **Confirmado** | Sin stubs, sin TODOs, infraestructura completa. Ãšnico matiz: los lÃ­mites angulares por defecto son genÃ©ricos, no anatÃ³micos por hueso. |
| Animation matching + power/authority â€” "fuerte" | **Confirmado** | PosiciÃ³n y rotaciÃ³n son ejes totalmente independientes en cada capa; Kinematic/Powered/Unpowered existe tal cual. |
| SemÃ¡ntica Puppet/Unpinned/GetUp (3 sub-claims) | **Confirmado** | Las 3 sub-reivindicaciones ciertas: sin flag de gracia post-GetUp, multiplicador binario (no gradual) compartido por 3 protecciones, reevaluaciÃ³n en el mismo FixedUpdate que termina GetUp. |
| "140/140" e inventario PuppetMaster incompleto | **Parcial** | "140/140" no existe en ningÃºn doc â€” la cifra real es 139V/1N-A/0Open. Pero BipedStagger ausente del inventario, sÃ­ confirmado. |
| CertificaciÃ³n: manifest verde vs. prosa contradictoria | **Parcial** | ContradicciÃ³n real, pero localizada en 2 de 9 documentos. Los dos docs que funcionan como fuente de verdad (README + FINAL-0050) estÃ¡n limpios. |
| Cruce contra API real de PuppetMaster (`SubBehaviourBalancer`, eventos de balance) | **Nuevo (P2)** | `SubBehaviourBalancer` (torque reactivo de tobillo) no tiene equivalente en Hairibar â€” brecha real, distinta del step. Eventos `onLoseBalance/onRegainBalance/onLoseBalanceFromGetUp` sÃ­ tienen paridad exacta â€” sin brecha. |

---

