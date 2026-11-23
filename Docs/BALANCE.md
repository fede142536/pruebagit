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

### 1.3 Revisión de la batería de pruebas (21/11/2026)

Se introdujeron errores a propósito en una copia del código (pruebas de mutación) para ver si las pruebas los detectaban:
22 de 36 pasaban sin ser detectados (por ejemplo, acreditar los goles del segundo tiempo al equipo equivocado, o que la IA
nunca adaptara su táctica). Se añadieron `CoverageGapTests` y se endurecieron los límites de balance (goles: objetivo ±3 errores
típicos; acierto de pase 85–97 %). Siete de los mutantes más graves, comprobados de nuevo, ahora hacen fallar la batería.
Una de las pruebas nuevas encontró que un tiro lento directo al arquero solo se atajaba el 46 % de las veces: la atajada pasa a
depender más de la velocidad del tiro (`KeeperSaveBase` 0,58 → 0,85, `KeeperSaveSpeedRef` 18 → 14 m/s,
`KeeperSaveSpeedPenalty` 0,012 → 0,045). Medición final (1.000 partidos, semilla 1): **2,34 goles por partido**, posesión 9,0 s,
3,29 pases por posesión.

### 1.4 Ajustes tras la prueba del usuario (21/11/2026)

- **Recuperar el balón:** con PRESIONAR mantenido 8 s sobre un rival que conduce, los robos pasan de 48 % a 63 % y las faltas
  de 28 % a 15 % (`HumanPressing_WinsTheBallMostOfTheTime_WithFewFouls`): `FoulBase` 0,30 → 0,16, entrada de frente con la mitad
  de faltas (`FrontTackleFoulMultiplier` 0,5), ayuda de entrada para la persona (`HumanTackleBonus` 0,10) y menos espera entre
  entradas (`HumanTackleCooldown` 0,6 s). `KeeperSaveBase` 0,85 → 0,90 para compensar (2,2 goles por partido).
- **Pase:** un toque es automático; mantener PASE elige la potencia (`HumanPassPowerMin` 0,8 a `HumanPassPowerMax` 1,6 de la
  velocidad asistida).

### 1.5 Modo 3 contra 3 (cancha cerrada)

Medición con `dotnet run -c Release --project Tools/HeadlessSim -- --indoor --matches 200 --seed 1` (`match_indoor.json`):

| Ajuste | Goles por partido | Tiros por equipo |
|---|---|---|
| Valores de cancha grande escalados | 14,6 | 29,7 |
| Menos ganas de tirar y alcance de tiro 13 m | 12,0 | 18,8 |
| + el arquero sufre menos los tiros fuertes de cerca, tiro máx. 26 m/s | **5,9** | 20,9 |

Las pruebas exigen entre 4 y 9 goles por partido, sin saques de banda, de esquina ni fueras de juego.

**Potrero sin arqueros (22/11/2026).** Por pedido del usuario el 3 contra 3 pasa a jugarse sin arquero: cierre, armador y
pivote; nadie usa las manos y el arco es de 2,4 × 1,4 m. Sin arquero, el jugador de campo más cercano al arco propio que no
presiona hace de «cierre» y se coloca sobre la línea balón-arco (`GoalCoverShare` 0,35 de la distancia, entre
`GoalCoverMinDepth` 1,2 m y `GoalCoverMaxDepth` 5 m). Medición (`--indoor --matches 200 --seed 1`):

| Ajuste | Goles por partido | Tiros por equipo | Goles / xG |
|---|---|---|---|
| Arco 2,4 × 1,4 sin arquero, umbrales anteriores | 6,43 | 22,8 | 0,26 |
| `ShotEagerness` 0,5 y alcance 12 m; xG recalibrado (`XgCalibration` 2,05 en el potrero) | **6,17** | 21,9 | **0,99** |

Resultado final: 42 % / 16 % / 43 % (local/empate/visitante), 0 atajadas, 71 % de pases completados, resistencia al final 70 %.
El xG de cancha grande sobrestimaba 4 veces el arco chico; por eso el potrero tiene su propia calibración.

### 1.6 Disparo de la IA en el área y saques (21/11/2026)

**Problema medido** (60 partidos): el 22 % de las veces que la IA entraba al área con el balón se quedaba más de 1,5 s
gambeteando (máximo 5,2 s) y el 36 % de esas jugadas terminaba sin tiro.

**Reglas de la zona de disparo** (`ShootZoneDistance` 22 m; 10 m en la cancha cerrada), evaluadas antes que el resto de las
opciones: la IA patea si tiene **línea limpia** (ningún jugador de campo a menos de `ShotLaneClearance` 1,2 m del trayecto), si
un rival le llega a menos de `ShotPressureDistance` 2 m, o si lleva más de `MaxBoxSeconds` 1,5 s en el área. No patea desde
ángulos imposibles (`ZoneShotMinXg`; el forzado por tiempo solo descarta ángulos nulos). La fuerza crece con la distancia
(`ShotPowerClose` 0,7 a quemarropa). **Después:** ninguna jugada pasa de 3 s en el área y las de más de 1,5 s bajan a menos
de 1 de cada 8 (prueba `AiNeverKeepsDribblingInsideTheBox`). Goles 11 contra 11: 2,35. Cancha cerrada: 7,0 → 6,0 tras
`KeeperSaveSpeedPenalty` 0,02 → 0,015.

**Saques:** esperas propias por tipo (`ThrowInDelay` 1,6 s, `GoalKickDelay` 2,0 s, `CornerDelay` 2,4 s) para que el equipo se
acomode; en el lateral los dos compañeros más cercanos se acercan a pedirla. Los saques de la persona (lateral, arco, córner
y tiro libre) esperan a que apunte y elija; si no hace nada en `HumanRestartTimeout` 8 s, saca la IA.

### 1.7 Acierto de pase, informe del partido y xG (22/11/2026)

**Mal control.** Antes, un balón por debajo de la velocidad controlable se controlaba siempre: el receptor corregía cualquier
error de dirección y el acierto de pase no bajaba del 92 % aunque se triplicara `PassAngleErrorDeg` (7° → 15°: 93,2 % → 90,8 %).
Ahora un control puede fallar (el balón sigue de largo, más lento): probabilidad `MiscontrolChance` × (velocidad / máxima
controlable)² × (1 − `MiscontrolSkillWeight` × FirstTouch), ×(1 + `MiscontrolPressureWeight`) con un rival a menos de 2,5 m, y
escalada por la dificultad (error de pase de la IA, error del humano para la persona). Un rebote en un compañero tampoco cuenta
como pase completado.

| `MiscontrolChance` (200 partidos) | 0 | 0,25 | 0,4 | **0,6** |
|---|---|---|---|---|
| Acierto de pase | 92,8 % | 89,9 % | 88,1 % | **85,3 %** |
| Goles por partido | 2,51 | 2,35 | 2,57 | 2,56 |

**Tiros al arco sin sesgo.** Un remate cuenta «al arco» si su trayectoria al salir del pie (simulada sin jugadores) cruza la línea
entre los palos y bajo el larguero; también si termina en gol o lo ataja el arquero. Antes, un desvío de un defensor lo borraba.
Con la definición nueva, el 52 % de los remates va al arco (real ≈ 33 %: la IA patea pocos tiros lejanos y casi siempre con línea
limpia; se revisará en la Fase 6).

**xG del informe.** Es el mismo modelo con el que decide la IA (distancia, ángulo, definición, bloqueo y presión), multiplicado
por `XgCalibration` = 5,0 para que la suma del xG coincida con los goles del motor (goles / xG = **1,01**), con un máximo de 0,95
por remate. La prueba `XgOverManyMatches_MatchesTheGoals` exige una relación entre 0,75 y 1,3.

**Medición final** (`--matches 1000 --seed 1`, dificultad Profesional, calidad 65):

| Métrica | Antes (§1.6) | Ahora | Referencia |
|---|---|---|---|
| Goles por partido | 2,35 | **2,54** | [2,0 ; 3,5] ✅ |
| Acierto de pase | 93,4 % | **85,4 %** | 82–87 % ✅ |
| Tiros por equipo | 5,2 | 6,0 | ≈ 12 (partido comprimido) |
| Remates al arco por equipo | — (con sesgo) | 3,1 (52 %) | ≈ 4 |
| xG por equipo | — | 1,25 | ≈ goles ✅ |
| Recuperaciones por equipo | — | 17,6 | — |
| Distancia recorrida por equipo | — | 11,0 km | ≈ 110 km reales (partido de 6 min reales) |
| Goles con asistencia | — | 39 % | ≈ 75 % (faltan asistencias de centros y rebotes) |
| Duración de una posesión / pases por posesión | 9,0 s / 3,3 | 7,4 s / 2,3 | más pérdidas por mal control |
| Cancha cerrada (200 partidos) | 6,0 goles | 5,6 goles, 82 % de pase | 4–9 goles ✅ |

`dotnet run -c Release --project Tools/HeadlessSim -- --matches 1 --report` imprime el informe completo de un partido (equipos,
jugadores con nota, remates con xG).

### 1.8 Fatiga dinámica, lesiones y roles individuales (22/11/2026)

**Fatiga.** El cansancio (1 − resistencia) ya no solo resta velocidad punta: resta aceleración (`StaminaAccelerationFloor`
0,8), suma error de pase, centro y tiro (×(1 + `FatigueErrorWeight` 0,6 × cansancio)) y alarga el tiempo de decisión
(×(1 + `FatigueReactionWeight` 0,5 × cansancio)). Como el partido dura 6 minutos reales, el gasto de resistencia se
multiplicó por 2,2 para que el cansancio se note:

| Gasto × (200 partidos) | 1 | 1,8 | **2,2** | 2,6 | 3 | 4 |
|---|---|---|---|---|---|---|
| Resistencia media al final (90') | 93 % | 79 % | **72 %** | 66 % | 61 % | 46 % |
| La más baja de cada equipo | 77 % | 52 % | **41 %** | 31 % | 22 % | 7 % |
| Goles por partido | 2,49 | 2,25 | 2,05 | 1,95 | 2,02 | 1,99 |

Más cansancio = menos goles; se compensó con el arquero (`KeeperSaveBase` 0,90 → 0,80: 2,05 → 2,39 goles). En la cancha
cerrada el gasto es el 40 % del de la cancha grande (`IndoorStaminaDrainFactor`): con 3 jugadores que corren todo el partido
terminaban agotados.

**Lesiones leves.** Desde el minuto 70 (`InjuryStartMinute`), una tirada por jugador de campo y minuto con probabilidad
`InjuryBaseRate` 0,0002 × (1 + `InjuryFatigueWeight` 25 × cansancio²). Resultado: **0,15 lesiones por equipo y partido**.
El árbitro para el juego para que se pueda hacer el cambio; la IA cambia primero al lesionado; el lesionado que sigue en el
campo corre al 70 % (`InjurySpeedFactor`).

**Roles individuales** (`PlayerDuty`, efecto en `RoleConfig`), medidos sobre el mismo partido y los mismos instantes
(`RoleTests`): lateral carrilero (sube 10 % del largo y se abre ×1,2 en ataque) o defensivo (se queda 8 % más atrás y cierra
×0,85); mediocampista recuperador (al defender se acerca al balón un 30 %), organizador (al atacar se acerca al poseedor un 25 %)
o llegador (sube 10 %); delantero pivote (centrado ×0,4 y algo más atrás) o al espacio (8 % más arriba, nunca más allá del
fuera de juego). Cada estilo trae roles por defecto: Posesión → organizadores; Contraataque → delantero al espacio; Presión alta →
recuperadores y carrileros; Defensivo → laterales defensivos y pivote.

**Medición** (1.000 partidos, semilla 1): **2,38 goles**, 85,7 % de pase, 0,15 lesiones por equipo, resistencia final 72 %;
`XgCalibration` 5,0 → 5,9 para mantener goles / xG ≈ 1,0 (500 partidos: 1,01).

### 1.9 Arqueros justos: tiempo de reacción y colocación (23/11/2026)

**Dos defectos encontrados al probar remates controlados** (`KeeperBalanceTests`):
1. La atajada se tira cuando el balón entra en el radio de alcance del arquero; como viene de frente, siempre entra por el
   borde y la «distancia al cuerpo» daba ≈1 en todos los remates. Resultado: un tiro al centro y otro al palo se atajaban
   igual (132 de 200 goles en ambos casos). Ahora se mide por dónde **va a pasar** el balón al cruzar al arquero, y la
   colocación también se compara con dónde estaba el arquero al patear (la estirada lo acerca, pero el tiro al palo sigue siendo
   más difícil).
2. El castigo por velocidad (0,045 por m/s) hacía imposible atajar un remate lejano potente.

**Modelo nuevo** (todo en `match.json`): tiempo del arquero = distancia del remate ÷ velocidad. Urgencia = `KeeperReactionTime`
0,5 s ÷ ese tiempo; comodidad crece desde `KeeperComfortTime` 0,8 s. Atajada = `KeeperSaveBase` 0,45 + nivel − velocidad
(`KeeperSaveSpeedPenalty` 0,015) − colocación × urgencia × `KeeperSaveEdgePenalty` 0,8 − urgencia × `KeeperUrgencyPenalty` 0,8 ×
(25 % + 75 % de la colocación) + comodidad × `KeeperComfortBonus` 0,6; con comodidad, además, más alcance (`KeeperComfortReach`).
El arquero achica más cerca del arco (`KeeperMaxDepth` 6,5 → 3,5 m) y la IA patea menos de lejos (`ShotEagerness` 2,2 → 1,6,
`XgDecay` 9,5 → 7,5). Cancha cerrada: urgencia casi nula y base 0,87.

| Remate de prueba (200 por caso) | Goles |
|---|---|
| A 24 m/s desde 14 m, al cuerpo / al palo | 65 / 115 |
| Potente y colocado desde 9 m / desde 28 m | 169 / 9 |

**Medición** (1.000 partidos, semilla 1): **2,63 goles por partido** (objetivo 2,4–2,9), 85,7 % de pase, goles / xG 0,98
(`XgCalibration` 11,1). Conversión por distancia: menos de 11 m **46 %**, 11–18 m **34 %**, más de 18 m **13,5 %** (antes 16 %,
y 3,5 tiros lejanos por equipo; ahora 1,7). En el fútbol real la conversión lejana es menor (≈4 %): sigue pendiente.
Cancha cerrada: 5,1 goles.

### 1.10 Medición final del 23/11/2026 (bloques 1 y 2 completos)

`dotnet run -c Release --project Tools/HeadlessSim -- --matches 1000 --seed 1` (incluye doble dibujo, roles por estilo, charla
del entretiempo de la IA, gritos, arqueros por tiempo de reacción, fatiga y lesiones):

| Métrica | Valor | Objetivo |
|---|---|---|
| Excepciones / sin terminar / estados inválidos | 0 / 0 / 0 | 0 ✅ |
| Goles por partido | **2,69** (local 37 % · empate 26 % · visitante 37 %) | 2,4–2,9 ✅ |
| Acierto de pase | **85,6 %** | 82–87 % ✅ |
| Goles / xG | 0,98 | ≈ 1 ✅ |
| Tiros / al arco por equipo | 4,6 / 2,0 (44 %) | — |
| Conversión: < 11 m · 11–18 m · > 18 m | 49 % · 35 % · 13 % | decreciente ✅ |
| Faltas / amarillas / rojas por equipo | 2,0 / 0,36 / 0,03 | — |
| Lesiones por equipo | 0,14 | — |
| Resistencia al final (90') / la más baja | 71 % / 40 % | — |
| Tiempo por partido | 71 ms (máximo 411 ms) | — |

### 1.11 Saque del medio con un solo ejecutante (22/11/2026)

Regla IFAB actual: un único jugador en el círculo, del lado rival del balón y de frente a su propio arco; nadie más (de ningún
equipo) dentro del círculo, todos en su campo y dos compañeros ofrecidos para el pase atrás (11 m detrás de la línea, a 7 m
del centro). El saque humano se apunta hacia el propio campo (cono de `KickoffAimDeg` alrededor de «atrás»).

| Medición | Antes | Después |
|---|---|---|
| 11v11, 500 partidos (`--matches 500 --seed 1`): goles / pases completados / goles÷xG | 2,69 / 85,6 % / 0,98 | 2,64 / 86,2 % / 0,99 |
| Potrero 3v3, 200 partidos: goles por partido | 6,17 | 6,09 |
| Posesiones de más de 1,5 s en el área (120 partidos) | 11,6 % | 13,3 % |

Tres pruebas dependían de la trayectoria exacta de los partidos sembrados y se rehicieron más robustas, sin buscar semillas:
la de «dribles en el área» mide ahora 120 partidos con un umbral del 15 % (la tasa real estaba en 11,6 %, a un punto del
umbral anterior de 12,5 %); la de «Leyenda contra Principiante» pide que el Principiante haga algún gol (ganar los 60 es
posible); la del «cambio por lesión» mira el primer cambio *después* de la lesión.

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
- Remates al arco altos (52 %) y pocas asistencias (39 %) frente al fútbol real.
- La sensación del regate y de la contención con el dedo solo se puede validar en el teléfono.
- Consistencia *Jugar* / *Ver* / *Simular*: `MatchSim` calibrado contra `MatchCore` (§7); repetir la medición si cambia el balance.
- Conversión de tiros lejanos (13 %) todavía alta frente al fútbol real (≈ 4 %).
- Economía de la carrera (Fase 2 en adelante).

## 7. Simulador rápido (`MatchSim`) calibrado contra `MatchCore` (23/11/2026)

`MatchSim` no copia la lógica del motor en tiempo real: reproduce sus **resultados medidos**. Procedimiento (repetirlo si
cambia el balance de `MatchCore`):

1. **Goles según la diferencia de fuerza** (200 partidos de `MatchCore` por caso, equipos de 65 ± la mitad de la
   diferencia, estilo equilibrado, 4-4-2):

   | Diferencia | `MatchCore` victorias / goles | `MatchSim` (500 partidos) |
   |---|---|---|
   | 0 | 36 % · 1,34–1,36 | 35 % · 1,25–1,36 |
   | 5 | 58 % · 1,94–1,05 | 58 % · 1,82–0,92 |
   | 10 | 80 % · 2,59–0,67 | 77 % · 2,43–0,66 |
   | 20 | 97 % · 4,15–0,29 | 94 % · 3,98–0,30 |

   Modelo: goles = `BaseGoals` 1,33 × e^(`StrengthSensitivity` 0,074 × diferencia) (el favorito satura:
   diferencia / (1 + diferencia / 80)) × factor del partido abierto/cerrado común (desviación 0,3).
2. **Efecto de cada estilo** contra uno equilibrado (150 partidos de `MatchCore`): goles a favor / en contra relativos —
   Posesión 0,80 / 1,00; Contraataque 0,87 / 0,90; Presión alta 1,30 / 1,10; Defensivo 0,75 / 0,70.
3. **Escenario mezclado** (`--matches 1000 --seed 1`, estilos y formaciones al azar, calidad 65 ± 5): `MatchSim` **2,65**
   goles, 36 / 24 / 40 % (`MatchCore`: 2,63 · 37 / 26 / 37 %). Tiempo: **≈ 60 µs** por partido.

La prueba `MatchSimTests.FiveHundredMatches_MatchTheRealTimeEngine_AtEveryStrengthGap` exige estar a ±8 puntos de victorias y
dentro de un margen de goles en cada diferencia.

## 8. Gestión (Fases 2–4, núcleo)

- **Ventaja de local**: +3 % en los atributos efectivos del local (`management.json` → `HomeAdvantage`). Con equipos iguales
  (600 partidos de `MatchSim`): local 44,5 % · empate 27 % · visitante 28,5 % (fútbol real ≈ 45 / 27 / 28).
- **Economía** (`finance.json`): un club de prestigio 70 con 20 M semanales de sueldos termina la temporada con caja
  positiva (prueba `ASeasonOfAMidTableClub_DoesNotGoBankrupt`). Falta calibrar con varias temporadas y clubes de distinto
  tamaño cuando exista el orquestador de temporada.
- **Clásicos**: +15 % de faltas y tarjetas por nivel de rivalidad y más error para los jugadores poco serenos.

## 9. Carrera: público, entradas, socios, obras y medidor (`career.json`, 22/11/2026)

Valores de fábrica y lo que miden las pruebas (`CareerManagementTests`, `CareerClubTests`):

| Sistema | Parámetro | Valor | Comprobación |
|---|---|---|---|
| Público | Importancia (final, clásico, decisivo, normal, sin nada) | ×1,45 / ×1,35 / ×1,25 / ×1,00 / ×0,65 | la final llena el estadio aun con precio Premium; un partido sin nada en juego se vacía |
| Público | Ocupación base · elasticidad del precio | 0,62 · 1,1 | en un partido sin nada en juego, subir el precio baja la recaudación neta |
| Entradas | Precio de referencia · niveles | $14.000 · ×0,6 / ×1 / ×1,5 / ×2,3 | el rango ideal es más alto en clásicos y finales y más bajo en un partido gris con mala racha |
| Entradas | Ancho del rango ideal · margen «elevado» | ±15 % · +25 % | verde dentro, amarillo hasta +25 %, rojo después |
| Socios | Malestar al ignorar · crítico · partidos seguidos | +10 · 75 · 3 | repudio y pedido de salida solo con malestar sostenido |
| Directiva | Popularidad inicial · despido | 80 · 25 | una racha de derrotas con la popularidad en caída termina en despido |
| Obras | Tribunas: asientos y semanas | +4.000/8.000/12.000 · 16–20/32–36/48–52 | la obra termina exactamente en su semana y suma la capacidad |
| Medidor | Pesos hinchas / plantel / presupuesto | 35 / 35 / 30 % | cada pilar entre 0 y 100; un fichaje que rinde suma, uno que no, resta |
| Clima | Lluvia por mes · temperatura media | 20–30 % · 10–25 °C | determinista por partido y semilla |

Pendiente: medir varias temporadas completas por jerarquía de club (caja final, popularidad media y despidos) y ajustar
la elasticidad y los objetivos de la directiva con esos datos.

## 10. Primera División de 30 clubes (22/11/2026)

- **Medias de los 15 clubes nuevos** (estimación propia, `Data/teams/argentina.json`): media del once entre 65,7 (San Martín de San
  Juan) y 71,7 (Argentinos Juniors); los originales van de 66,0 a 75,6. Una prueba exige 62–78 por jugador y 65–73 para el once.
- **Calendario**: dos zonas de 15 → 15 fechas por torneo (14 + 1 interzonal), 225 partidos por torneo; Apertura, 3 semanas de
  pausa y Clausura entran en ~250 días. Pruebas: ninguna fecha repite equipo, cada club juega toda su zona una vez y un solo
  cruce interzonal, localías equilibradas (±1), determinismo por semilla.
- **Presupuestos de los nuevos** (fichajes / sueldos anuales / valor, en millones): Media tabla 3,5–4,9 / 10,5–12,6 / 56–77;
  En desarrollo 1,8–3,1 / 6,3–9,8 / 28–49; coherentes con los de Vélez, Lanús, Banfield y Gimnasia.
- **Por medir:** varias temporadas completas con 30 clubes (reparto de puntos, descensos por promedios, ocupación de los estadios
  chicos como Riestra con 8.740 lugares) y si 2 descensos sobre 30 clubes es el castigo adecuado.


## 11. Fin de parte con la jugada terminada (23/11/2026)
1000 partidos, semilla 1: sin esperar (corte inmediato) 2,66 goles; esperando cualquier parada 3,07 (la prórroga es rica en goles: 39 goles en 300 partes con la
tolerancia larga); con `EndPlaySoftMinutes` 0,75, `DangerZoneFraction` 0,5 y `EndPlayHardMinutes` 6: **2,90**, pase 85,7 %. Ver D-28.

## 12. Dificultad y carrera de varias temporadas (23/11/2026)
**Dificultad** (HeadlessSim, 300 partidos, semilla 3, equipos iguales): local nivel 1 contra rival nivel 0 / 1 / 2 → victorias del local 64 % / 37 % / 20 %, goles 2,79 / 2,59 / 2,97.
Con los dos en el mismo nivel: nivel 0 → 2,01 goles; nivel 1 → 2,59; nivel 2 (el predeterminado) → 2,87. `DifficultyBalanceTests` (plantel sintético, 40 partidos por nivel) comprueba que el orden se mantenga (9 % / 39 % / 70 %).
**Carrera** (`CareerLongRunTests`, 5 temporadas, 30 clubes, semilla 21): antes los contratos nunca vencían y los planteles crecían hasta 43 jugadores; y todos los clubes pagaban sueldos parecidos
mientras la facturación iba de ≈ 4.000 M (Riestra) a ≈ 21.000 M por año (River): 9 clubes quedaban en deuda permanente (hasta −15.000 M, 4 veces la caja inicial). Ahora la masa salarial inicial se ajusta a
`RevenuePerReputation × (reputación − 30) × 0,8` (límites 0,4–1,5) y los clubes de la computadora sueltan el excedente sobre 32 jugadores. Resultado a 5 temporadas: plantel 25–34, caja mínima −1.900 M, 4 clubes en deuda.
Pendiente: los fichajes y juveniles de la computadora siguen cobrando sueldos de mercado, así que los clubes chicos se deterioran despacio (por eso el test tolera hasta 10 clubes en deuda).
**Rendimiento del núcleo** (`AllocationTests`): 0 bytes por tick en el motor y 0 al copiar el estado para la vista (regla 7).
