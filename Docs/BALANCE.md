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

### 1.1 Jugabilidad del partido (21/11/2026): duelos, coberturas, ritmo y táctica en vivo

Cambios de comportamiento (detalle en la sección 4) y su efecto, medido con 1.000 partidos (semilla 1) antes y después:

| Métrica (por equipo y partido) | Antes | Después | Lectura |
|---|---|---|---|
| Goles por partido (total) | 2,42 | **2,12** | dentro del rango [2,0 ; 3,5] |
| Duración media de una posesión (tiempo de simulación) | 6,8 s | **9,0 s** | menos «ping-pong»: el balón cambia de dueño un 30 % menos |
| Pases por posesión | 2,31 | **3,27** | más circulación antes de perderla o terminar en tiro |
| Posesiones | 22,9 | 18,1 | |
| Tiros | 8,5 | 5,2 | menos tiros, mejor elegidos (la cobertura tapa los disparos fáciles) |
| Pases completados | 91,3 % | 93,4 % | algo alto frente al 80–88 % real (pendiente) |
| Entradas intentadas / ganadas | 19,9 / 4,4 (21,9 %) | 10,2 / 2,6 (25,4 %) | la IA ya no entra «a ciegas»: entra menos y elige mejor |
| Faltas | 4,4 | 3,0 | la IA ya no entra por detrás; `FoulBase` sube a 0,30 para compensar |

Efecto medible de la táctica en vivo (`TacticsTests.Mentality_HasAMeasurableEffectOnTheBlockAndThePress`, 12 partidos por caso):
con mentalidad **muy ofensiva + presión alta** el bloque juega en el **48,0 %** del campo y hace **20,1 entradas** por partido; con
**muy defensiva + presión baja**, en el **38,6 %** (unos 10 m más atrás) y **3,6 entradas**.

Brecha de dificultad (300 partidos, semilla 3): **Leyenda vs Principiante 85 % / 12 % / 4 %** (antes 94 / 5 / 1);
Mundial vs Profesional 48 % / 30 % / 22 %.

Estilos con los equipos reales (20 vueltas, 600 partidos): Racing (presión alta) 314 pts, **River (posesión) 305**, Estudiantes 275,
Independiente 266, Boca 265, San Lorenzo 198. La posesión dejó de ser el estilo débil; la presión alta sigue algo por encima.

### 1.2 Revisión de código del motor (21/11/2026)

Una revisión encontró que **unos dos tercios de los goles venían de errores**, no de buenas jugadas: el tiro que el arquero no
atajaba lo «atravesaba» (y el gol se le acreditaba a él), el arquero llevaba el balón detrás de su propia línea, y el manotazo por
encima del larguero podía terminar dentro del arco. Al corregirlos los goles cayeron a **0,8 por partido**. Se modeló así:
el arquero que falla la atajada de un tiro ya no lo toca (gol del que pateó); un bloqueo de un jugador de campo rebota según el
punto de contacto; el arquero con el balón en las manos no lo pierde ni cruza la línea; el manotazo solo va por encima del
larguero si la trayectoria es posible. Medición tras los arreglos (1.000 partidos, semilla 1): **2,15 goles por partido**,
2,36 tiros a puerta y 1,37 paradas por equipo, posesión 8,9 s, 3,25 pases por posesión, 0 estados inválidos.

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
| **Entradas con contexto** (`TackleFacingWeight` 0,12, `TackleFrontWeight` 0,08) | — | Entrar de frente y mirando al poseedor ayuda; perseguirlo por detrás, no |
| **Recorte** (`CutEvadeBonus` 0,25, `CutWindow` 0,4 s, `CutAngleDeg` 40°) | — | Un giro brusco a velocidad justo antes de la entrada deja atrás al defensor (más cuanto mejor regateador) |
| Entrada de pie por detrás (`StandingBehindFoulMultiplier`) | — → 1,3 | Más falta por la espalda |
| **Contención** de la IA (`ContainDistance` 2 m, `TackleEngageDistance` 2,6 m, `TackleChanceThreshold` 0,30) | — | El que presiona se coloca entre el poseedor y su portería y solo entra de frente o de costado con buenas opciones |
| **Cobertura** (`CoverDepth` 7 m, `CoverRadius` 18 m) | — | Un compañero cubre la espalda del que presiona; goles 2,4 → 1,3 hasta recalibrar |
| Bloqueo del carril de pase (`BlockReach` 0,8 m) | — | La IA no ve un defensor plantado en el carril como «sin tiempo de reacción» |
| **Mantener el balón** (`HoldBias` 0,002, `HoldStyleWeight` 0,012, `HoldMaxTime` 1,5 s) | — | Proteger el balón al paso para esperar un desmarque; los equipos de posesión lo valoran más |
| `ThreatBase` (valor de tener el balón en cualquier lugar) | 0,005 → 0,06 | Perder el balón cuesta también en el medio: posesión 6,8 → 8,9 s |
| `TackleWinBase` | 0,32 → 0,28 | Con el contexto, las entradas ya buenas ganaban demasiado |
| `ShotAngleErrorDeg` / `KeeperSaveBase` | 26 → 12 / 0,72 → 0,58 | Recuperar goles tras la mejora defensiva (1,3 → 2,1) |
| **Táctica en vivo** (`MentalityLineStep` 0,12, `MentalityRiskStep` 0,1, `MentalityDirectnessStep` 0,1, `PressLevelStep` 0,3) | — | Mentalidad y presión desde la pausa |
| **IA que se adapta al marcador** (`AdaptStartMinute` 60', `AdaptLateMinute` 80') | — | Si pierde se vuelca al ataque con presión alta; si gana al final, se cierra |

## 5. Equipos reales: medias y estilos (noviembre de 2026)

Medición con `dotnet run -c Release --project Tools/HeadlessSim -- --teams Data/teams/argentina.json --matches 10 --seed 3`
(10 vueltas de ida y vuelta = 50 partidos por equipo).

| Prueba | Resultado |
|---|---|
| Todos con el mismo estilo y formación | El orden sigue a las medias: Boca (74,2) 159 pts, River (75,4) 154, Racing (71,8) 144, Independiente (70,1) 130, Estudiantes (71,3) 112, San Lorenzo (68,6) 110. **Las medias sí diferencian a los equipos.** |
| Solo Boca cambia a «Presión alta» | Boca pasa de 159 a 179 pts: **el estilo de presión alta da demasiada ventaja** |
| Archivo real (estilos variados) | Racing (presión alta, media 71,8) termina primero y River (posesión, 75,2) tercero |

Conclusión: los estilos aún no están equilibrados (presión alta fuerte, posesión débil). Se ajustará en la Fase 6 con más
partidos por medición, porque con 50 partidos por equipo el ruido es de unos ±9 puntos.

## 6. Pendientes de calibración

- Fueras de juego casi inexistentes (Fase 6).
- Ventaja de local (Fase 3, junto con estadio y afición).
- Brecha de dificultad: mejoró (85/12/4) pero sigue amplia (Fase 6).
- Equilibrio entre estilos: «Presión alta» todavía algo por encima (ver 1.1).
- Acierto de pase alto (93 %) frente al real (80–88 %).
- La sensación del regate y de la contención con el dedo solo se puede validar en el teléfono.
- Consistencia *Jugar* / *Ver* / *Simular*: el motor estadístico `MatchSim` aún no existe (Fase 2); se calibrará contra `MatchCore`.
- Economía de la carrera (Fase 2 en adelante).
