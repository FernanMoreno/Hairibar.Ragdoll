# Perfil local de rendimiento — Windows Development Player

Entorno: Unity 6000.5.2f1, Windows64 Development Player, 2026-08-01. Cada puppet
del fixture tiene dos músculos físicos. Cada combinación ejecutó 120 frames de
warm-up y 600 frames medidos. CPU y memoria muestran mediana y percentil 95; no
son presupuestos universales ni se extrapolan a otras plataformas.

`GC Allocated In Frame` tuvo máximo 0 bytes en todas las combinaciones. Los
escenarios integrales también verificaron cero asignaciones tras warm-up en
mapping, matching, COM, additional pin y Baker Realtime. La inicialización, el
runner y la serialización JSON quedaron fuera de la ventana medida.

| Puppets | Modo | CPU mediana (ms) | CPU p95 (ms) | Memoria mediana (bytes) | Memoria p95 (bytes) | GC máx./frame (bytes) |
|---:|---|---:|---:|---:|---:|---:|
| 1 | Active tree | 0.0297 | 0.0424 | 244,711,424 | 244,711,424 | 0 |
| 1 | Active flat | 0.0297 | 0.0425 | 244,789,248 | 244,789,248 | 0 |
| 1 | Kinematic | 0.0291 | 0.0489 | 244,903,936 | 244,903,936 | 0 |
| 1 | Disabled | 0.0180 | 0.0228 | 245,088,256 | 245,088,256 | 0 |
| 10 | Active tree | 0.0906 | 0.1321 | 245,129,216 | 245,129,216 | 0 |
| 10 | Active flat | 0.0904 | 0.1382 | 245,141,504 | 245,141,504 | 0 |
| 10 | Kinematic | 0.0874 | 0.1445 | 245,174,272 | 245,174,272 | 0 |
| 10 | Disabled | 0.0181 | 0.0192 | 245,174,272 | 245,174,272 | 0 |
| 25 | Active tree | 0.1926 | 0.2976 | 245,186,560 | 245,186,560 | 0 |
| 25 | Active flat | 0.1924 | 0.3292 | 245,260,288 | 245,260,288 | 0 |
| 25 | Kinematic | 0.1839 | 0.2759 | 245,297,152 | 245,297,152 | 0 |
| 25 | Disabled | 0.0183 | 0.0208 | 245,297,152 | 245,297,152 | 0 |
| 50 | Active tree | 0.3693 | 0.5734 | 245,374,976 | 245,374,976 | 0 |
| 50 | Active flat | 0.3678 | 0.5497 | 245,506,048 | 245,506,048 | 0 |
| 50 | Kinematic | 0.3536 | 0.4989 | 245,706,752 | 245,706,752 | 0 |
| 50 | Disabled | 0.0180 | 0.0189 | 245,772,288 | 245,772,288 | 0 |

Evidencia cruda: `%TEMP%/HairibarRagdollCertification-Windows/windows-player-result.json`, generada
fuera del repositorio. Instrumentación: `ProfilerRecorder`, conforme al manual
oficial de Unity:
https://docs.unity3d.com/Manual/profiler-creating-custom-counters.html

Windows fue ejecutado. Linux64, macOS y WebGL tienen `BuildReport` satisfactorio;
Linux64 requiere ejecución posterior en un host Linux real.
