# Arquitectura

## 1. Principio rector

Separar el **núcleo de simulación en C# puro** de la **presentación en Unity**. Todo lo que decide qué pasa (gestión, economía, calendarios, reglas, física del balón, IA) vive en `Core` y se prueba con `dotnet test` sin editor. Unity solo dibuja, reproduce sonido y traduce el toque en comandos.

```
┌────────────────────────── Game (Unity) ──────────────────────────┐
│  UI (UI Toolkit)   HUD de partido   Input táctil   Presentation  │
│        │                 │               │            (arte)    │
└────────┼─────────────────┼───────────────┼───────────────────────┘
         │ lee estado      │ lee snapshot  │ comandos
┌────────▼─────────────────▼───────────────▼───────────────────────┐
│                         Core (C# puro)                           │
│  Management  Competitions  MatchSim  MatchCore  Persistence      │
│                        Domain   Common                            │
└───────────────────────────────────────────────────────────────────┘
```

Regla de dependencias: `Game → Core`; dentro de `Core`: `Management`, `Competitions`, `MatchSim`, `MatchCore`, `Persistence` → `Domain` → `Common`. Nunca al revés ni entre hermanos (se comunican por eventos/interfaces).

## 2. Estructura del repositorio

```
/Core                    C# puro (netstandard2.1)
  /Common                RNG con semilla, bus de eventos, tiempo, utilidades
  /Domain                Club, Player, Staff, Stadium, Finance, Fixture, Tactic…
  /Management            economía, fichajes, cantera, contratos, socios, directiva
  /Competitions          calendarios, tablas, copas, sorteos, desempates
  /MatchSim              motor estadístico rápido (modo Simular)
  /MatchCore             motor de partido en tiempo real SIN gráficos (propuesta, ver DECISIONS D-03)
  /Persistence           SQLite, repositorios, migraciones versionadas
/Game                    proyecto Unity
  /Match                 adaptador Unity de MatchCore: render, cámara, árbitro visual
  /UI                    pantallas de gestión y HUD
  /Presentation          arte, audio, VFX (intercambiable)
/Tests                   NUnit (núcleo) · /Game/Tests (Unity Test Framework)
/Tools                   HeadlessSim, importador/editor de base de datos, benchmark
/Data                    JSON/CSV: base de datos ficticia, balance, localización
/Docs                    GDD, ARCHITECTURE, ROADMAP, BALANCE, DECISIONS
```

En Unity, `Core` se consume como **assembly definitions** (`asmdef`) o como DLL compilada desde `Core/`; la decisión de empaquetado se fija en la Fase 0 (D-02).

## 3. Núcleo de simulación

### 3.1 Determinismo
- Un único `IRandom` con semilla inyectable (xoshiro/PCG). Cada subsistema recibe su propio flujo derivado (`rng.Fork("transfers")`) para que añadir azar en un sitio no altere otros.
- El tiempo es una entrada (`SimClock`), nunca `DateTime.Now`.
- En el partido, el paso de simulación es **fijo (60 Hz)**; el render interpola. Misma semilla + mismos inputs ⇒ mismo resultado bit a bit en el mismo build.
- Coma flotante: se usa `float` con orden de operaciones fijo; la reproducibilidad entre plataformas distintas **no** se promete (D-05).

### 3.2 Eventos
Bus de eventos tipado y sin allocations (`EventBus<T>` con structs). Ejemplos: `GoalScored`, `PlayerInjured`, `MatchFinished`, `TransferCompleted`, `MonthClosed`. Los sistemas se suscriben; no se llaman entre sí.

### 3.3 Modelo de datos único
`Player` y `Team` son las **mismas entidades** para gestión, Simular y partido 3D. El partido consume una vista inmutable (`PlayerMatchView`) con atributos efectivos ya modificados por forma, moral, fatiga y lesión; el resultado vuelve como `MatchReport` (eventos, minutos, estadísticas) y la gestión lo aplica.

### 3.4 Datos y balance
Todo parámetro de balance está en `Data/balance/*.json` con esquema y validación. Sin números mágicos en código; las pruebas leen los mismos archivos.

## 4. Motor de partido

Dos motores calibrados entre sí:

| Motor | Uso | Coste |
|---|---|---|
| `MatchSim` (estadístico) | Modo *Simular*, temporadas masivas, calibración económica | Microsegundos por partido |
| `MatchCore` (tiempo real) | Modos *Jugar* y *Ver*; también headless para calibrar | 60 Hz, 22 agentes + balón |

`MatchCore` contiene la física del balón, el movimiento de jugadores, las reglas, el árbitro y la IA, todo en C# puro y **sin PhysX**: cinemática propia de paso fijo. Motivos: determinismo, pruebas sin editor, rendimiento predecible en móvil, simulación headless de miles de partidos. Unity recibe un **snapshot por tick** (posiciones, orientaciones, estado de animación, eventos) y lo dibuja.

```
Input táctil ──► IntentBuffer ──► MatchCore.Tick(dt)
                                      │  física balón → movimiento → reglas → IA
                                      ▼
                                MatchSnapshot (buffers reutilizados)
                                      │
                    Unity: interpolación → animación/cámara/HUD/audio
```

### IA
Tres capas por jugador: **posicionamiento** (zona dinámica según formación, rol y balón), **decisión** (utility AI con considerandos ponderados por atributos y táctica), **ejecución** (error en pase/tiro según atributos y dificultad). Los parámetros de dificultad se inyectan desde datos; no hay rama de código "trampa".

## 5. Persistencia

- **SQLite** para carrera y guardados; el esquema tiene `schema_version` y migraciones incrementales numeradas (`0001_init.sql`, …) con prueba que migra desde cada versión anterior.
- Repositorios detrás de interfaces (`IClubRepository`…); el dominio no conoce SQL.
- Guardado atómico (transacción + archivo temporal) y copia de seguridad rotativa.

## 6. Capa Unity

- **UI de gestión:** UI Toolkit (UXML/USS), vertical, navegación inferior. Vistas sin lógica: leen *view models* del núcleo.
- **HUD de partido:** overlay ligero y horizontal.
- **Presentación:** modelos, materiales, audio y VFX detrás de interfaces (`IPlayerAvatarFactory`, `IStadiumView`, `IAudioBank`); los placeholders son procedurales y se reemplazan sin tocar lógica.
- **Contenido pesado:** Addressables.
- **Escenas y prefabs:** se crean preferentemente por código/Editor scripts para que sean reproducibles y revisables en git.

## 7. Rendimiento móvil

- Cero allocations por frame en partido: `struct`, `Span<T>`, buffers preasignados, pooling de efectos y UI.
- Render: LODs, GPU instancing, atlas, ASTC, público con impostors, sombras escalables.
- Perfiles Bajo/Medio/Alto/Auto con reducción dinámica ante *thermal throttling*; selector 30/60 FPS y modo ahorro de batería.
- Panel de depuración (FPS, memoria, draw calls, temperatura si se puede) y escena de benchmark reproducible (misma semilla, mismo recorrido).

## 8. Estrategia de pruebas

| Nivel | Herramienta | Qué cubre |
|---|---|---|
| Unitarias del núcleo | NUnit + `dotnet test` | Dominio, economía, calendarios, reglas, física, migraciones |
| Propiedades/invariantes | NUnit (semillas múltiples) | El dinero se conserva, calendarios sin conflictos, tablas coherentes |
| Determinismo | NUnit | Misma semilla ⇒ mismo resultado; snapshot de hashes |
| Simulación masiva | `Tools/HeadlessSim` | 1.000 partidos/temporadas: sin crashes, métricas en rango |
| Capa Unity | Unity Test Framework | Adaptadores, UI, escenas cargan |
| Rendimiento | Escena de benchmark | FPS, GC, memoria en dispositivo de referencia |

**CI** (GitHub Actions): `dotnet build` + `dotnet test` en cada push. La compilación de Unity en CI requiere licencia y queda fuera del alcance inicial; se verifica en la máquina del usuario.

## 9. Riesgos de arquitectura

| Riesgo | Mitigación |
|---|---|
| Divergencia entre `MatchSim` y `MatchCore` | Calibración continua con simulación headless; prueba de distribución en CI |
| IA de 22 agentes costosa en gama baja | Decisiones a frecuencia reducida por agente (p. ej. 10 Hz) escalonadas; medición temprana en Fase 1 |
| SQLite nativo en iOS/Android | Spike en Fase 0 con la librería elegida, en ambas plataformas |
| Float no determinista entre CPUs | No se promete reproducibilidad entre plataformas; sí dentro de un build/plataforma |
| Escenas Unity difíciles de revisar en git | Construcción por código, serialización en texto forzada |
