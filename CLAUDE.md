# CLAUDE.md — Juego de fútbol móvil (gestión + partidos)

Videojuego de fútbol para **Android** (objetivo actual: Samsung Galaxy A13, gama baja; iOS queda para cuando haya un Mac): **Modo Carrera** (gestión tipo Football Manager) + **partidos 3D jugables** (tipo FIFA/PES). Uso personal: sin monetización, sin anuncios, offline-first.

Documentos de referencia: `Docs/GDD.md`, `Docs/ARCHITECTURE.md`, `Docs/ROADMAP.md`, `Docs/DECISIONS.md`, `Docs/BALANCE.md`. `ASSETS_LICENSES.md` se crea cuando haya assets que registrar.

## Estado actual

**Fase 0 completada. Fase 1 (corte vertical jugable): núcleo hecho y probado; la capa Unity está escrita pero sin ejecutar en Unity** (D-03 aprobada; el usuario tiene PC con Windows y un Galaxy A13). Falta que el usuario la pruebe y reporte mediciones y sensaciones. **Por pedido explícito del usuario (D-16, D-20), el núcleo de las Fases 2–4 (gestión, `MatchSim`, competiciones, migraciones, orquestador `Career`) ya existe en `Core/` con pruebas, y las pantallas de Modo Carrera y Torneo están escritas en Unity sin probar.** El juego se llama **POTRERO** (D-17). **No se avanza de fase sin confirmación explícita del usuario.** Al terminar cada fase: ejecutar pruebas, resumir qué funciona y qué no, y detenerse.

## Comandos

| Acción | Comando | Disponible desde |
|---|---|---|
| Compilar el núcleo | `dotnet build Core/Core.sln` | Fase 0 ✅ |
| Pruebas del núcleo | `dotnet test Core/Core.sln` | Fase 0 ✅ |
| DLL del núcleo para Unity | `Tools/build-core.sh` (copia a `Game/Assets/Plugins/Core/`) | Fase 0 ✅ |
| Simulación masiva headless | `dotnet run -c Release --project Tools/HeadlessSim -- --matches 1000 --seed 1` (opciones: `--difficulty`, `--away-difficulty`, `--rating`, `--ascii 12`, `--dump-config ruta`, `--config ruta`) | Fase 1 ✅ |
| Simulador rápido (`MatchSim`) con las mismas métricas | `dotnet run -c Release --project Tools/HeadlessSim -- --sim --matches 1000 --seed 1` | Fase 2 ✅ |
| Regenerar archivos de balance | `--dump-config`, `--indoor --dump-config`, `--dump-sim-config`, `--dump-finance-config`, `--dump-management-config`, `--dump-career-config` (cada uno con la ruta en `Data/balance/`) | ✅ |
| Alineaciones y torneo entre equipos de un archivo | `dotnet run -c Release --project Tools/HeadlessSim -- --teams Data/teams/argentina.json --matches 10` | Fase 1 ✅ |
| Malla del jugador sin Unity (triángulos, pesos, costuras) | `dotnet run --project Tools/SkinCheck` | ✅ |
| Pruebas Unity (EditMode/PlayMode) | desde el editor, o `Unity -runTests -batchmode -projectPath Game` | Fase 1 |
| Benchmark reproducible | escena `Game/Benchmark` + script `Tools/benchmark.md` | Fase 1 |

El entorno de nube **no trae Unity ni .NET SDK preinstalados**; el SDK de .NET 8 se instala con `apt-get update && apt-get install -y dotnet-sdk-8.0` (los hosts de Microsoft están bloqueados, el repositorio de Ubuntu no). Unity no se puede instalar aquí. Lo que no se pueda ejecutar aquí (compilar Unity, medir FPS, probar el control táctil) se entrega con los pasos exactos que debe ejecutar el usuario en su máquina. Nunca se declara verificado algo que no se ejecutó.

## Arquitectura (resumen; detalle en `Docs/ARCHITECTURE.md`)

- `Core/` — C# puro (netstandard2.1), **sin dependencias de Unity**. Dominio (incluido el contrato del partido en `Domain/Match`), `Management`, `Competitions`, `Career`, `MatchSim`, `MatchCore`, `Persistence`, `Common`. Se prueba con `dotnet test`.
- `Game/Assets/Football/` — capa Unity (`Runtime/` presentación, UI y entrada; `Editor/` scripts del editor). Solo **lee** el estado del núcleo (`MatchSnapshot`) y le envía comandos (`MatchInput`). El proyecto de Unity en sí lo crea el usuario con Unity Hub y se le copian `Assets/Football` y `Assets/Plugins` (ver `Docs/COMO_PROBAR_EN_ANDROID.md`).
- `Game/Assets/Plugins/Core/Football.Core.dll` — DLL del núcleo **versionada**: regenerarla con `Tools/build-core.sh` y confirmarla en cada cambio de `Core/`.
- `Tests/` — NUnit para el núcleo.
- `Docs/` — documentación viva.

Reglas:
1. Dependencias en un solo sentido: `Game → Core`. Nunca al revés.
2. Comunicación entre sistemas por **eventos/mensajes e interfaces**, no por referencias directas.
3. **Todo el azar** pasa por el RNG con semilla inyectable (`Core/Common`). Prohibido `System.Random` o `UnityEngine.Random` en lógica de juego.
4. Un solo modelo de datos de jugador/equipo compartido por gestión, simulación rápida y partido 3D.
5. Balance y configuración en datos (JSON/ScriptableObject), **sin números mágicos**.
6. Sin lógica de negocio en `MonoBehaviour`; sin `Find*` ni búsquedas costosas en `Update`.
7. Cero allocations por frame durante el partido (pooling, structs, buffers reutilizados).

## Convenciones de código

- Legibilidad primero: nombres claros, funciones cortas, una responsabilidad por clase.
- Comentarios XML (`/// <summary>`) en toda clase y método público: propósito, parámetros, retorno.
- Textos de interfaz siempre desde archivos de recursos (es/en); nunca incrustados en código.
- Toda lógica del núcleo lleva pruebas unitarias. Las pruebas de balance se expresan como datos medibles, no opiniones.

## Contenido y licencias

- **Escudos reales: solo si los pone el usuario** (D-66): el repositorio no incluye ninguno; `CrestLibrary` usa los PNG de `Assets/MisEscudos/Resources/Crests` de su proyecto (ver `Docs/COMO_AGREGAR_ESCUDOS.md`) y, si no hay imagen, el escudo procedural. **Sin logotipos ni diseños oficiales en el repositorio.** Por pedido explícito del usuario (D-12), `Data/teams/argentina.json` tiene nombres reales de jugadores de los 30 clubes de Primera con medias **estimadas por nosotros**; es para uso personal. El juego debe seguir funcionando sin ese archivo (equipos ficticios de reserva). Tras editarlo, `Tools/build-core.sh` lo copia a `Game/Assets/Football/Resources/teams.json` (una prueba verifica que coinciden).
- Solo assets con licencia clara (CC0/MIT o equivalente), registrados en `ASSETS_LICENSES.md` (fuente, autor, licencia, URL, fecha).
- La capa de arte es intercambiable: la lógica no depende de modelos, texturas ni audio concretos.

## Entrega al usuario

- El usuario actualiza su proyecto de Unity descargando el ZIP de la rama. **Copiar carpetas encima de las viejas le mezcló versiones**
  (errores CS0111 por archivos duplicados). En cada entrega, indicarle que use `ACTUALIZAR_JUEGO.bat` (en la raíz del repositorio:
  sincroniza `Assets/Football` y `Assets/Plugins/Core` con `robocopy /MIR`, borrando archivos que ya no existen). Si se renombra o
  elimina un archivo de `Game/Assets`, mencionarlo explícitamente.
- Su proyecto está en `C:\Users\Charly\Juego de futbol` (Unity 6.6, plantilla Universal 3D con Input System).

## Flujo de trabajo y git

- Commits pequeños y descriptivos, pruebas verdes antes de cada commit.
- Una rama por hito cuando el entorno lo permita; en sesiones de nube se desarrolla en la rama que asigne la sesión y cada hito se marca con un tag (`fase-0`, `fase-1`, …).
- Decisiones relevantes → `Docs/DECISIONS.md`. Máximo 5 preguntas agrupadas al usuario cuando el coste de error sea alto; en lo demás, decidir con criterio y documentar.
- Idioma de documentación y comunicación: español. Código e identificadores: inglés.
