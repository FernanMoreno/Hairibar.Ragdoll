# Certification documentation

## Estado vigente

- `PUPPETMASTER-R01-R34-REPAIR-REGISTER.md`: estado vigente de reparaciones.
- `PUPPETMASTER-COVERAGE-FINAL-0050.md`: cierre histórico actualmente reabierto.
- `PUPPETMASTER-COVERAGE-REAUDIT-2026-07-31.md`: matriz normativa de 140 filas.
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

El historial se conserva como trazabilidad. No hay certificación completa vigente
mientras alguna fila R01-R34 siga abierta.
