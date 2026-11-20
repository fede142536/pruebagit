# Roadmap

Regla: al terminar cada fase se ejecutan las pruebas, se resume qué funciona y qué no, y **no se avanza hasta confirmación del usuario**.

## Fases

| Fase | Objetivo | Criterio de salida (resumen) |
|---|---|---|
| **0 — Fundamentos** | Docs, estructura, CI, RNG determinista, modelo de datos base, esquema SQLite con migraciones | `dotnet test` verde en CI; migración 0→N probada; spike de SQLite en Android/iOS documentado |
| **1 — Corte vertical jugable** | Partido 11v11 3D jugable con controles táctiles (detalle abajo) | Criterios medibles de la sección "Fase 1"; el usuario valida la sensación de juego |
| **2 — Núcleo de gestión** | Plantilla, contratos, finanzas, calendario, ligas, tablas, simulación rápida, guardado/carga | Temporada completa simulada headless sin errores; economía sin quiebras absurdas |
| **3 — Integración gestión ↔ partido** | Táctica, forma, moral, fatiga, lesiones y sanciones fluyen en ambos sentidos | Un partido jugado actualiza la carrera; prueba de ida y vuelta |
| **4 — Sistemas de club** | Fichajes con negociación, cantera, ojeadores, estadio e inmediaciones, socios, directiva, prensa, patrocinadores | Cada sistema con pruebas y efecto medible en la economía/partido |
| **5 — Competiciones** | Copas nacionales e internacionales, sorteos, desempates, congestión | Competición internacional completa simulada, sin conflictos de calendario |
| **6 — IA y dificultad** | Roles tácticos, 5 niveles calibrados, adaptativa opcional | `BALANCE.md` con métricas dentro de rango en los 5 niveles |
| **7 — Pulido** | Arte intercambiable, audio, VFX, repeticiones, accesibilidad, localización | Auditoría de accesibilidad y localización es/en completa |
| **8 — Estabilización** | Resistencia (1.000 simulaciones), perfilado en dispositivos reales, documentación de usuario | Sin crashes en 1.000 temporadas; FPS objetivo en dispositivos reales |

---

## Fase 1 — Plan detallado (corte vertical jugable)

**Meta:** un partido completo 11v11 que se *sienta bien* con el dedo, con placeholders. Se valida la jugabilidad antes de construir la gestión.

### Subhitos

| # | Entrega | Verificable aquí (nube) | Verificable solo en tu máquina |
|---|---|---|---|
| 1.1 | Física del balón (gravedad, arrastre, rebote, rodadura, efecto) y tipos de golpeo, en `MatchCore` | Sí (`dotnet test`) | — |
| 1.2 | Movimiento de jugadores: aceleración, giro con inercia, velocidad por atributos y fatiga | Sí | — |
| 1.3 | Reglas esenciales: gol, fuera de juego, saques de banda/esquina/puerta, faltas y tarjetas básicas, penalti, tiempo reglamentario | Sí | — |
| 1.4 | IA básica: formación 4-4-2/4-3-3, posicionamiento por zonas, utility AI (pasar/conducir/tirar/presionar/cubrir), portero | Sí | — |
| 1.5 | Adaptador Unity: snapshot→render con placeholders procedurales, cámara de transmisión, árbitro visual | No (requiere Unity) | Compilar y abrir escena |
| 1.6 | Controles táctiles: joystick flotante/fijo, botones contextuales, cambio de jugador, zurdos, háptica, preview de pase | No | Probar en dispositivo |
| 1.7 | HUD: marcador, reloj, fin de partido, perfil gráfico, panel de depuración, escena de benchmark | No | Medir FPS/GC/RAM |

### Criterios de aceptación medibles

**Núcleo (lo verifico yo con pruebas automáticas)**
- A1. Determinismo: misma semilla e idéntica secuencia de inputs ⇒ marcador y hash final del estado idénticos en 100 de 100 ejecuciones.
- A2. Física del balón: caída libre desde 10 m coherente con la fórmula analítica (error < 1 %); coeficiente de rebote y distancia de rodadura dentro de tolerancias definidas en datos; un tiro con efecto se curva en el sentido correcto.
- A3. Reglas: cada regla (gol, fuera de juego, banda, esquina, saque de puerta, falta, tarjeta, penalti) tiene ≥ 3 casos de prueba positivos y ≥ 2 negativos.
- A4. Simulación headless de 1.000 partidos IA vs IA: 0 excepciones, 0 partidos que no terminan, media de goles por partido en **[2,0 ; 3,5]** (afinado a 2,5–2,9 en Fase 6), ningún jugador fuera del campo salvo estados permitidos.
- A5. 0 bytes asignados por tick de `MatchCore` en régimen estable (prueba con `GC.GetAllocatedBytesForCurrentThread`).
- A6. `MatchCore` simula un partido de 90' en < 2 s en headless con 1 hilo (indicador de coste de CPU).

**Dispositivo (lo mides tú con mis herramientas; yo entrego los pasos y scripts)**
- B1. Latencia toque→acción del jugador (instrumentada en el HUD de depuración) < 100 ms en el percentil 95.
- B2. FPS en dispositivo de referencia de gama media-alta: ≥ 58 FPS de media y p99 de frame time ≤ 20 ms durante 5 min de partido; en gama baja, 30 FPS estables.
- B3. Sin picos de GC en partido (perfilador: 0 B/frame de GC.Alloc en régimen estable).
- B4. RAM ≤ 1,5 GB en gama baja; arranque a partido en < 15 s.
- B5. Un partido completo jugable de principio a fin sin crashes, con marcador, fin de partido y vuelta al menú.

**Sensación de juego (lo valida el usuario)**
- C1. Lista de playtest con 10 preguntas (¿el pase va donde apuntas?, ¿el cambio de jugador elige al correcto?, ¿el esprint se siente pesado o ligero?, etc.); la fase solo cierra si el usuario da el visto bueno o define ajustes.

### Qué necesito de ti para empezar la Fase 1

1. **Dispositivo(s) de referencia:** modelo y versión de Android/iOS de tu móvil principal y, si tienes, uno de gama baja.
2. **Unity:** versión instalada (propongo Unity 6 LTS) y módulos de Android y/o iOS. Para compilar a iOS hace falta un Mac con Xcode; dime si lo tienes.
3. **Cámara:** ¿transmisión lateral alta (estilo FIFA) por defecto, o prefieres otra?
4. **Controles:** joystick izquierdo + botones a la derecha (estándar), ¿mano dominante y tamaño de pantalla típico? ¿Aceptas que el pase tenga auto-apuntado asistido por defecto?
5. **Alcance de la Fase 1:** ¿el partido es contra la IA de un solo rival fijo y duración de 3 min por tiempo? ¿Con 4-4-2 y 4-3-3 como únicas formaciones?
