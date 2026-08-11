# Certification documentation

## Estado vigente

La certificación descrita aquí pertenece exclusivamente al paquete
`com.hairibar.ragdoll`. No importa ni ejecuta código, escenas, prefabs, AI, NavMesh,
pooling o armas de un juego consumidor. Los antiguos R24-R33 de CODE RED se conservan
solo como [historial de integración consumer-only](../Integration/CODE-RED-CONSUMER-HISTORY.md)
y no son gates de la matriz de 140 capacidades.

- `PUPPETMASTER-COVERAGE-FINAL-0050.md`: certificación vigente del paquete.
- `PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md`: matriz normativa de 140 filas
  cerrada por el manifiesto schema 3; conserva sus marcas históricas.
- `PUPPETMASTER-R01-R34-REPAIR-REGISTER.md`: registro histórico de la reapertura.
- `PUPPETMASTER-IMPLEMENTATION-EVIDENCE.md`: evidencia por capacidad.
- `PUPPETMASTER-PERFORMANCE-WINDOWS-EDITOR-2026-07-31.md`: mediciones del Windows Development Player.
- `MIGRATION-PUPPETMASTER-CLOSURE.md`: guía de API y migración.

## Historial

El menÃº `Tools/Hairibar/Certification/Generate Coverage Manifest` genera
`Library/HairibarCertification/coverage-manifest.json`. Descubre el test exacto
de cada ID y solo escribe `Verified` cuando ese test figura como `Passed` en el
XML NUnit indicado por `HAIRIBAR_TEST_RESULTS`; un estado escrito en Markdown
nunca cuenta como evidencia. `HAIRIBAR_COVERAGE_MANIFEST` permite cambiar la
salida en CI.

- `PUPPETMASTER_COVERAGE_AUDIT.md`
- `PUPPETMASTER_COVERAGE_AUDIT-AFTER-0037-V2.md`
- `PUPPETMASTER-CODE-AUDIT-POST-0050.md`
- `PUPPETMASTER-REMEDIATION-REGISTER.md`
- `README-SPRINT-0037-PROPS-IV-MELEE-CANONICAL-V2.md`

El historial se conserva como trazabilidad. La certificación vigente del
2026-08-11 contiene 139 filas `Verified`, G05 como único `N/A` y cero filas
abiertas. El estado de integraciones consumer-only no altera ese resultado.
