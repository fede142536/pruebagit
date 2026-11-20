# CLAUDE.md — Juego de fútbol móvil (gestión + partidos)

Videojuego de fútbol para Android e iOS: **Modo Carrera** (gestión tipo Football Manager) + **partidos 3D jugables** (tipo FIFA/PES). Uso personal: sin monetización, sin anuncios, offline-first.

Documentos de referencia: `Docs/GDD.md`, `Docs/ARCHITECTURE.md`, `Docs/ROADMAP.md`, `Docs/DECISIONS.md`. Los de la Fase 0 (`BALANCE.md`) y `ASSETS_LICENSES.md` se crean cuando haya contenido que registrar.

## Estado actual

Fase 0 pendiente de aprobación del plan. **No se avanza de fase sin confirmación explícita del usuario.** Al terminar cada fase: ejecutar pruebas, resumir qué funciona y qué no, y detenerse.

## Comandos

| Acción | Comando | Disponible desde |
|---|---|---|
| Compilar el núcleo | `dotnet build Core/Core.sln` | Fase 0 |
| Pruebas del núcleo | `dotnet test Core/Core.sln` | Fase 0 |
| Simulación masiva headless | `dotnet run --project Tools/HeadlessSim -- --matches 1000 --seed 1` | Fase 1 |
| Pruebas Unity (EditMode/PlayMode) | desde el editor, o `Unity -runTests -batchmode -projectPath Game` | Fase 1 |
| Benchmark reproducible | escena `Game/Benchmark` + script `Tools/benchmark.md` | Fase 1 |

El entorno de nube **no tiene Unity ni .NET SDK preinstalados**. Lo que no se pueda ejecutar aquí (compilar Unity, medir FPS, probar el control táctil) se entrega con los pasos exactos que debe ejecutar el usuario en su máquina. Nunca se declara verificado algo que no se ejecutó.

## Arquitectura (resumen; detalle en `Docs/ARCHITECTURE.md`)

- `Core/` — C# puro (netstandard2.1), **sin dependencias de Unity**. Dominio, gestión, competiciones, simulación, persistencia, utilidades. Se prueba con `dotnet test`.
- `Game/` — proyecto Unity: presentación, UI, entrada táctil. Solo **lee** el estado del núcleo y le envía comandos.
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

- **No usar nombres, escudos, equipaciones ni jugadores con licencia.** La base de datos por defecto es ficticia; existirá un importador/editor JSON/CSV para que el usuario cargue datos reales por su cuenta.
- Solo assets con licencia clara (CC0/MIT o equivalente), registrados en `ASSETS_LICENSES.md` (fuente, autor, licencia, URL, fecha).
- La capa de arte es intercambiable: la lógica no depende de modelos, texturas ni audio concretos.

## Flujo de trabajo y git

- Commits pequeños y descriptivos, pruebas verdes antes de cada commit.
- Una rama por hito cuando el entorno lo permita; en sesiones de nube se desarrolla en la rama que asigne la sesión y cada hito se marca con un tag (`fase-0`, `fase-1`, …).
- Decisiones relevantes → `Docs/DECISIONS.md`. Máximo 5 preguntas agrupadas al usuario cuando el coste de error sea alto; en lo demás, decidir con criterio y documentar.
- Idioma de documentación y comunicación: español. Código e identificadores: inglés.
