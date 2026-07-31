# Perfil local de rendimiento — Windows Development Player

Entorno: Unity 6000.5.2f1, Windows64 Development Player, 2026-07-31. Cada puppet
del fixture tiene dos músculos físicos. Cada combinación ejecutó 120 frames de
warm-up y 600 frames medidos. CPU y memoria muestran mediana y percentil 95; no
son presupuestos universales ni se extrapolan a otras plataformas.

`GC Allocated In Frame` tuvo máximo 0 bytes en todas las combinaciones. Los
escenarios integrales también verificaron cero asignaciones tras warm-up en
mapping, matching, COM, additional pin y Baker Realtime. La inicialización, el
runner y la serialización JSON quedaron fuera de la ventana medida.

| Puppets | Modo | CPU mediana (ms) | CPU p95 (ms) | Memoria mediana (bytes) | Memoria p95 (bytes) | GC máx./frame (bytes) |
|---:|---|---:|---:|---:|---:|---:|
| 1 | Active tree | 0.0316 | 0.0594 | 237,686,784 | 237,686,784 | 0 |
| 1 | Active flat | 0.0309 | 0.0822 | 237,686,784 | 237,686,784 | 0 |
| 1 | Kinematic | 0.0306 | 0.0486 | 237,686,784 | 237,686,784 | 0 |
| 1 | Disabled | 0.0203 | 0.0352 | 237,686,784 | 237,686,784 | 0 |
| 10 | Active tree | 0.0949 | 0.1995 | 237,686,784 | 237,686,784 | 0 |
| 10 | Active flat | 0.0942 | 0.1811 | 237,686,784 | 237,686,784 | 0 |
| 10 | Kinematic | 0.0919 | 0.2131 | 237,686,784 | 237,686,784 | 0 |
| 10 | Disabled | 0.0198 | 0.0364 | 237,686,784 | 237,686,784 | 0 |
| 25 | Active tree | 0.2016 | 0.3376 | 237,686,784 | 237,686,784 | 0 |
| 25 | Active flat | 0.2018 | 0.3351 | 237,686,784 | 237,686,784 | 0 |
| 25 | Kinematic | 0.1935 | 0.3382 | 237,686,784 | 237,686,784 | 0 |
| 25 | Disabled | 0.0198 | 0.0326 | 237,686,784 | 237,686,784 | 0 |
| 50 | Active tree | 0.3806 | 0.5716 | 238,321,664 | 238,321,664 | 0 |
| 50 | Active flat | 0.3823 | 0.5887 | 238,321,664 | 238,321,664 | 0 |
| 50 | Kinematic | 0.3668 | 0.6012 | 238,321,664 | 238,321,664 | 0 |
| 50 | Disabled | 0.0203 | 0.0290 | 238,321,664 | 238,321,664 | 0 |

Evidencia cruda: `HairibarIntegral-All-13/windows-player-result.json`, generada
fuera del repositorio. Instrumentación: `ProfilerRecorder`, conforme al manual
oficial de Unity:
https://docs.unity3d.com/Manual/profiler-creating-custom-counters.html

Windows fue ejecutado. Linux64, macOS y WebGL tienen `BuildReport` satisfactorio;
Linux64 requiere ejecución posterior en un host Linux real.
