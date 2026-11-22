# Roadmap

Regla: al terminar cada fase se ejecutan las pruebas, se resume qué funciona y qué no, y **no se avanza hasta confirmación del usuario**.

## Fases

| Fase | Objetivo | Criterio de salida (resumen) |
|---|---|---|
| **0 — Fundamentos** ✅ *(núcleo; ver estado abajo)* | Docs, estructura, CI, RNG determinista, modelo de datos base, esquema SQLite con migraciones | `dotnet test` verde en CI; migración 0→N probada; spike de SQLite en Android documentado |
| **1 — Corte vertical jugable** | Partido 11v11 3D jugable con controles táctiles (detalle abajo) | Criterios medibles de la sección "Fase 1"; el usuario valida la sensación de juego |
| **2 — Núcleo de gestión** ⚠️ *núcleo hecho, sin interfaz* | Plantilla, contratos, finanzas, calendario, ligas, tablas, simulación rápida, guardado/carga | Temporada completa simulada headless sin errores; economía sin quiebras absurdas |
| **3 — Integración gestión ↔ partido** ⚠️ *núcleo hecho, sin interfaz* | Táctica, forma, moral, fatiga, lesiones y sanciones fluyen en ambos sentidos | Un partido jugado actualiza la carrera; prueba de ida y vuelta |
| **4 — Sistemas de club** ⚠️ *núcleo en buena parte, sin interfaz* | Fichajes con negociación, cantera, ojeadores, estadio e inmediaciones, socios, directiva, prensa, patrocinadores | Cada sistema con pruebas y efecto medible en la economía/partido |
| **5 — Competiciones** | Copas nacionales e internacionales, sorteos, desempates, congestión | Competición internacional completa simulada, sin conflictos de calendario |
| **6 — IA y dificultad** | Roles tácticos, 5 niveles calibrados, adaptativa opcional | `BALANCE.md` con métricas dentro de rango en los 5 niveles |
| **7 — Pulido** | Arte intercambiable, audio, VFX, repeticiones, accesibilidad, localización | Auditoría de accesibilidad y localización es/en completa |
| **8 — Estabilización** | Resistencia (1.000 simulaciones), perfilado en dispositivos reales, documentación de usuario | Sin crashes en 1.000 temporadas; FPS objetivo en dispositivos reales |

---

## Fase 0 — Estado

| Criterio | Estado |
|---|---|
| Estructura del repositorio (`Core`, `Tests`, `Tools`, `Docs`, CI) | ✅ hecho |
| RNG determinista (xoshiro256** + SplitMix64) con flujos derivados (`Fork`) | ✅ hecho; contrastado con una implementación de referencia independiente |
| Bus de eventos sin allocations y reloj de simulación | ✅ hecho; allocations medidas = 0 B |
| Modelo de datos base (jugador, 24 atributos, contrato, club, estadio, partido) | ✅ hecho |
| Esquema SQLite con migraciones versionadas (v1 inicial, v2 contratos) | ✅ hecho; prueba que migra desde cada versión conservando datos, revierte migraciones fallidas y rechaza bases más nuevas |
| `dotnet test Core/Core.sln` | ✅ 50 pruebas pasan (ejecutadas aquí) |
| CI en GitHub Actions (`.github/workflows/ci.yml`) | ⚠️ escrito; su primera ejecución se verá en GitHub tras el push |
| DLL del núcleo para Unity (`Tools/build-core.sh`) | ✅ se genera; **no probada dentro de Unity** |
| Spike de SQLite en Android (Galaxy A13) | ⏳ pendiente: requiere Unity y el dispositivo |

---

## Fase 1 — Plan detallado (corte vertical jugable)

**Meta:** un partido completo 11v11 que se *sienta bien* con el dedo, con placeholders. Se valida la jugabilidad antes de construir la gestión.

### Subhitos

| # | Entrega | Verificable aquí (nube) | Verificable solo en tu máquina |
|---|---|---|---|
| 1.1 | Física del balón (gravedad, arrastre, rebote, rodadura, efecto) y tipos de golpeo, en `MatchCore` | Sí (`dotnet test`) | — |
| 1.2 | Movimiento de jugadores: aceleración, giro con inercia, velocidad por atributos y fatiga | Sí | — |
| 1.3 | Reglas esenciales: gol, fuera de juego, saques de banda/esquina/puerta, faltas y tarjetas básicas, penalti, tiempo reglamentario | Sí | — |
| 1.4 | IA básica: formaciones 4-4-2/4-3-3, posicionamiento por zonas, utility AI (pasar/conducir/tirar/presionar/cubrir), portero, y **varios rivales con estilos distintos** definidos en datos (posesión, contraataque, presión alta, defensivo) | Sí | — |
| 1.5 | Adaptador Unity: snapshot→render con placeholders procedurales, cámara de transmisión, árbitro visual | No (requiere Unity) | Compilar y abrir escena |
| 1.6 | Controles táctiles: joystick flotante/fijo, botones contextuales, cambio de jugador, zurdos, háptica, preview de pase | No | Probar en dispositivo |
| 1.7 | HUD: marcador, reloj, fin de partido, perfil gráfico, panel de depuración, escena de benchmark | No | Medir FPS/GC/RAM |

### Estado de la Fase 1

| Subhito | Estado |
|---|---|
| 1.1 Física del balón y tipos de golpeo | ✅ hecho y probado (15 pruebas, error < 1 % frente a la fórmula analítica) |
| 1.2 Movimiento de jugadores | ✅ hecho y probado |
| 1.3 Reglas esenciales | ✅ hecho y probado (gol, fuera de juego, banda, puerta, córner, falta, tarjetas, penalti, descanso y final) |
| 1.4 IA con varios rivales y estilos | ✅ hecho: 5 estilos y 5 dificultades en datos; portero con paradas |
| 1.5 Adaptador Unity (render, cámara) — 1.ª prueba del usuario: arrancaba en bucle sin dibujar el campo; corregido (errores visibles en pantalla), figuras humanas animadas, estadio y cámara más cercana | ⚠️ escrito, compila contra una simulación de la API de Unity; **sin ejecutar en Unity** (`MatchView`, `MatchScreen`) |
| 1.6 Controles táctiles | ⚠️ escrito, **sin probar en el dispositivo** (`TouchControls`: joystick flotante, botones contextuales, zurdos, pase asistido con vista previa, tiro con carga; teclado para probar en PC) |
| 1.6b Menús por pantallas (estilo FC) | ⚠️ escrito, sin ejecutar en Unity: menú principal (amistoso; carrera y torneo «próximamente»), equipos en tarjetas, alineación (formación, titulares, suplentes, no convocados), ajustes del partido y ajustes generales |
| 1.7 HUD, depuración y benchmark | ⚠️ escrito, **sin medir en el dispositivo** (marcador, pausa, panel de depuración, partido IA contra IA con semilla fija) |
| 1.8 Jugabilidad del partido: entradas según posición y orientación, recortes, contención y cobertura de la IA, mantener el balón, táctica en vivo (mentalidad y presión) e IA que se adapta al marcador | ✅ núcleo hecho y probado (`DuelTests`, `TacticsTests`; métricas en `BALANCE.md` §1.1); ⚠️ botones de táctica en la pausa sin ejecutar en Unity |
| 1.10 Presentación del partido: radar (11v11) y fichas de compañeros fuera de cámara, barras de potencia y resistencia, aro pulsante y aro del próximo CAMBIAR, estela del balón, cámara dinámica (zoom y anticipación), sonido generado por código (hinchada dinámica, silbato, golpeo, palo, red), papelitos, repetición del gol en cámara lenta y vibración | ⚠️ escrito, compila contra la simulación de la API de Unity; **sin ejecutar en Unity** |
| 1.13 Pedido del 22-23/11/2026: rendimiento (mallas agrupadas, zona segura, ajustes de vista en JSON), buffer de entrada y segunda marca, `MatchReport` completo con xG, mal control (pase 85,7 %), fatiga y lesiones, roles, doble dibujo, pelota parada, gritos y charla, arqueros por tiempo de reacción (2,63 goles), pausa y watchdog corregidos, selector EA FC con jugador 3D, estadio con público y LED, jugadores redondeados con animaciones, 9 clubes más con clásicos | ✅ núcleo probado (350 pruebas); ⚠️ capa Unity compilada contra la simulación, sin ejecutar en Unity |
| 1.12 Transmisión de TV y pulido: presentación aérea con alineaciones, placas animadas (gol y tarjetas), festejo con cámara sobre el goleador antes de la repetición, red que se infla, temblor en el palo, choques con trastabilleo, inercia más pesada, mandos Bluetooth, perfiles gráficos Baja/Media/Alta y menú con un 3 contra 3 de fondo | ✅ motor probado (choques y festejo); ⚠️ capa Unity sin ejecutar en Unity |
| 1.11 IA: arquero que achica en el mano a mano, desmarques al espacio libre, cambio automático al compañero que llega primero | ✅ núcleo hecho y probado (`AiBehaviourTests`) |
| 1.9 Revisión de calidad con agentes (motor, Unity, datos, pruebas): goles fantasma del arquero, autor del gol, fuera de juego, sacador expulsado, convocatorias guardadas, interfaz en pantallas 20:9, cámara única, toques colgados, pausa al salir de la app | ✅ núcleo corregido y probado (`EngineRegressionTests`); ⚠️ Unity compilado contra la simulación, sin ejecutar. Pendiente: reducir llamadas de dibujo (~300 por fotograma), zona segura de la muesca, ajustes de URP para gama baja, valores de ajuste aún escritos en código |

### Criterios de aceptación medibles

**Núcleo (lo verifico yo con pruebas automáticas)**
- A1. ✅ Determinismo: misma semilla e idéntica secuencia de inputs ⇒ marcador y hash final del estado idénticos en 100 de 100 ejecuciones (`SameSeed_GivesTheSameMatch_100Times`).
- A2. ✅ Física del balón: caída libre desde 10 m coherente con la fórmula analítica (error < 1 %); coeficiente de rebote y distancia de rodadura dentro de tolerancias definidas en datos; un tiro con efecto se curva en el sentido correcto.
- A3. ✅ Reglas: cada regla (gol, fuera de juego, banda, esquina, saque de puerta, falta, tarjeta, penalti) tiene ≥ 3 casos de prueba positivos y ≥ 2 negativos.
- A4. ✅ Simulación headless de 1.000 partidos IA vs IA (estilos y formaciones al azar): 0 excepciones, 0 sin terminar, 0 estados inválidos, **2,59 goles por partido** (rango [2,0 ; 3,5]).
- A5. ✅ 0 bytes asignados por paso de `MatchCore` en régimen estable (6.000 pasos medidos con `GC.GetAllocatedBytesForCurrentThread`).
- A6. ✅ `MatchCore` simula un partido de 90' en **50 ms** de media (máximo 257 ms) en headless con 1 hilo, en Release.

**Dispositivo (lo mides tú con mis herramientas; yo entrego los pasos y scripts)**
- B1. Latencia toque→acción del jugador (instrumentada en el HUD de depuración) < 100 ms en el percentil 95.
- B2. FPS en el **Galaxy A13 (dispositivo de referencia, gama baja)**: 30 FPS estables, con p99 de frame time ≤ 40 ms durante 5 min de partido, en perfil gráfico Bajo. El objetivo de 60 FPS queda para gama media-alta y no se puede medir con el dispositivo actual.
- B3. Sin picos de GC en partido (perfilador: 0 B/frame de GC.Alloc en régimen estable).
- B4. RAM ≤ 1,5 GB en el Galaxy A13; arranque a partido en < 15 s.
- B5. Un partido completo jugable de principio a fin sin crashes, con marcador, fin de partido y vuelta al menú.

**Sensación de juego (lo valida el usuario)**
- C1. Lista de playtest con 10 preguntas (¿el pase va donde apuntas?, ¿el cambio de jugador elige al correcto?, ¿el esprint se siente pesado o ligero?, etc.); la fase solo cierra si el usuario da el visto bueno o define ajustes.

### Decisiones del usuario (registradas)

| Tema | Respuesta |
|---|---|
| Dispositivo de referencia | Samsung Galaxy A13 (SM-A135M), Android 14, One UI 6.1 — **gama baja**; se optimiza para 30 FPS estables |
| iOS | Sin Mac por ahora: se desarrolla solo para Android |
| Cámara | Transmisión lateral alta (estilo FIFA) por defecto |
| Controles | Usuario diestro: joystick a la izquierda, botones a la derecha; auto-apuntado de pase activado por defecto y configurable; opción de zurdos disponible |
| Alcance Fase 1 | **Varios rivales con estilos de juego distintos** (no uno solo); 4-4-2 y 4-3-3; 3 min por tiempo por defecto |

### Pendiente antes de empezar

1. **Aprobación del plan y de D-03** (`MatchCore` sin PhysX).
2. **Computadora:** Unity es un programa gratuito (para uso personal) que se instala en una PC o Mac y con el que se arma la aplicación para el teléfono. Hace falta una computadora con Windows o Mac, preferiblemente con 16 GB de RAM y ~30 GB libres. Con ella instalaríamos Unity y generaríamos el archivo `.apk` que se instala en tu A13.

---

## Fases 2 a 4 — Estado del núcleo (23/11/2026, a pedido del usuario)

El usuario pidió explícitamente avanzar con el núcleo de gestión antes de cerrar la Fase 1 en el teléfono. Hecho en `Core/`,
con pruebas, **sin interfaz todavía**:

| Pieza | Estado |
|---|---|
| `MatchSim` (simulador rápido, ~60 µs por partido, mismo `MatchReport`) calibrado contra `MatchCore` | ✅ prueba de 500 partidos por diferencia de calidad |
| Finanzas (presupuestos separados con comisión, flujo semanal y mensual, taquilla) e infraestructura por niveles | ✅ |
| Calendario sin conflictos, tablas con desempates, promedios, zonas, playoffs y eliminatorias | ✅ |
| Ciclo gestión ↔ partido (forma, moral, físico, lesiones, sanciones, ventaja de local) | ✅ ida y vuelta probada |
| Cuerpo técnico, scouting progresivo, mercado con cláusulas y perfiles de rivales, cantera, personalidad, química, vestuario, promesas, buzón, historia viva | ✅ como sistemas sueltos con pruebas |
| Migraciones 3 a 7 y guardado atómico | ✅ |
| Orquestador de la temporada (`CareerSession`), repositorios SQL de todas las tablas, archivo de partida para el teléfono, Modo Torneo | ✅ 22/11/2026, con pruebas (temporada completa, determinismo al cargar) |
| Pantallas de Modo Carrera y Torneo en Unity (inicio, plantel y táctica, mercado y ojeadores, club y finanzas) | ✅ escritas; **sin probar en Unity** |
| Saque IFAB, presentación táctica, selector con escudos, ficha del jugador, controles en arco, estadio y proporciones (D-21) | ✅ núcleo probado; ⚠️ Unity sin ejecutar |
| Modo Carrera completo (D-22): DT, datos de clubes, directiva y popularidad, público por partido, entradas con rango ideal, socios, remodelación del estadio por semanas, medidor de gestión, Copa Argentina, migración 10 | ✅ núcleo probado; ⚠️ pantallas Unity escritas sin ejecutar |
| Falta | Copas continentales con su calendario real con su calendario real, segunda división (hoy el descenso solo baja reputación), IA de fichajes entre rivales, renovaciones de contrato |

