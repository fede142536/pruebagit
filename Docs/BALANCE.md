# Balance y calibración

Documento vivo: cada cambio de parámetros de `Data/balance/match.json` se anota aquí con la medición que lo justifica.
Las mediciones se obtienen con `dotnet run -c Release --project Tools/HeadlessSim -- --matches 1000 --seed 1`.

## 1. Estado de la calibración del motor de partido (Fase 1)

Medición: 1.000 partidos, semilla 1, dificultad Profesional en ambos equipos, calidad media 65, estilos y formaciones al azar.

| Métrica (por equipo y partido) | Medido | Referencia real (90 min) | Estado |
|---|---|---|---|
| Goles por partido (total) | **2,59** | 2,5 – 2,9 | ✅ en rango |
| Distribución de goles totales 0..9+ | 79 · 171 · 279 · 221 · 129 · 71 · 31 · 14 · 5 · 0 | Poisson ≈ 2,7 | ✅ plausible |
| Victoria local / empate / visitante | 38 % / 25 % / 37 % | ≈ 45 / 27 / 28 (con ventaja local) | ⚠️ no hay ventaja de local todavía (Fase 3: afición y estadio) |
| Tiros | 8,6 | ≈ 12 | ⚠️ algo bajo |
| Paradas del portero | 2,5 | ≈ 3 | ✅ |
| Pases / completados | 58 / 89,6 % | ≈ 80 – 85 % de acierto | ⚠️ acierto algo alto |
| Pases interceptados | 5,5 | — | informativo |
| Faltas | 4,6 | ≈ 12 | ⚠️ ver nota de compresión |
| Amarillas / rojas | 0,83 / 0,09 | ≈ 1,7 / 0,04 | ✅ / ⚠️ |
| Córners | 1,4 | ≈ 5 | ⚠️ ver nota de compresión |
| Fueras de juego | **0,03** | ≈ 2 | ❌ la IA los evita casi siempre; ajustar en Fase 6 |

**Nota de compresión.** El partido dura 3 min por parte (6 min reales para 90' simulados). Los futbolistas corren a velocidad real,
así que hay unas 15 veces menos jugadas que en un partido real. Los goles se calibran al total real (se trata de un juego); los
eventos proporcionales al tiempo (faltas, córners, fueras de juego) salen más bajos y se afinarán en la Fase 6 con objetivos
propios para un partido comprimido.

**Estadística conocida con sesgo.** `ShotsOnTarget` solo cuenta un tiro si sigue "vivo" cuando se para o entra (un desvío de un
defensor lo apaga). No usar como métrica de calibración hasta corregirlo.

## 2. Dificultad

Cada nivel solo cambia **decisión y ejecución** de la IA y la ayuda al jugador; **nunca** velocidad, fuerza ni resistencia.
Valores actuales (`MatchConfig.Difficulties`):

| Nivel | Reacción × | Error de pase × | Error de tiro × | Presión × | Ruido de decisión × | Error del humano × | Asistencia de pase |
|---|---|---|---|---|---|---|---|
| Principiante | 1,80 | 1,60 | 1,60 | 0,60 | 2,0 | 0,50 | 0,90 |
| Amateur | 1,35 | 1,30 | 1,30 | 0,80 | 1,5 | 0,70 | 0,75 |
| Profesional | 1,00 | 1,00 | 1,00 | 1,00 | 1,0 | 0,85 | 0,60 |
| Mundial | 0,80 | 0,80 | 0,80 | 1,15 | 0,6 | 1,00 | 0,40 |
| Leyenda | 0,65 | 0,65 | 0,65 | 1,30 | 0,3 | 1,00 | 0,20 |

Medición del efecto (equipos de igual calidad, 500 partidos): **Leyenda vs Principiante → 94 % / 5 % / 1 %**;
con los lados invertidos, 1 % / 7 % / 92 %. La dificultad funciona, pero **la brecha es demasiado grande**: un Principiante
debería poder ganar de vez en cuando. Pendiente de calibrar en la Fase 6 (objetivo orientativo: Leyenda ≈ 70 % de victorias
contra Profesional con equipos iguales).

La **dificultad adaptativa** (opcional y desactivable) y el *rubber banding* **no existen** y no se añadirán de forma oculta.

## 3. Estilos de juego

Cinco estilos en `MatchConfig.Styles`: Equilibrado, Posesión, Contraataque, Presión alta y Defensivo. Cada uno ajusta altura de la
línea, intensidad de la presión, amplitud, juego directo, riesgo y compactación. Comprobado por prueba automática: los equipos de
presión alta juegan en promedio más adelantados que los defensivos (`HighPressTeams_PlayHigherThanDefensiveTeams`).

## 4. Historial de ajustes

| Cambio | Antes → después | Motivo / efecto |
|---|---|---|
| Elección de pase por tiempo de intercepción en lugar de distancia al carril | pases interceptados 26 → 5,5 por equipo; acierto 64 % → 90 % | La IA elegía pases que un defensor llegaba a cortar |
| `ShotAngleErrorDeg` | 9 → 13 → 26 | Demasiados tiros a puerta (65 %); goles 4,0 → 2,6 |
| `ShotEagerness` | — → 2,2 | Más tiros desde media distancia |
| `KeeperSaveBase` | 0,62 → 0,72 | Portero demasiado débil |
| Anchura del equipo (`BallShiftY`, anchos base) | 0,30 → 0,15 y anchos mayores | Los extremos no llegaban a las bandas |
| Paradas: 40 % se desvían por encima del larguero | — | Sin córners por paradas |
| `FoulBase` | 0,09 → 0,22 | Casi no había faltas |

## 5. Pendientes de calibración

- Fueras de juego casi inexistentes (Fase 6).
- Ventaja de local (Fase 3, junto con estadio y afición).
- Brecha de dificultad excesiva (Fase 6).
- Consistencia *Jugar* / *Ver* / *Simular*: el motor estadístico `MatchSim` aún no existe (Fase 2); se calibrará contra `MatchCore`.
- Economía de la carrera (Fase 2 en adelante).
