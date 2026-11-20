# Juego de fútbol móvil (gestión + partidos)

Videojuego de fútbol para Android: **Modo Carrera** (gestión) y **partidos 3D jugables** con controles táctiles. Uso personal, sin
anuncios ni compras dentro de la aplicación, funciona sin conexión.

## Estado

| Parte | Estado |
|---|---|
| Fase 0 — fundamentos (RNG determinista, modelo de datos, SQLite con migraciones) | ✅ hecha y probada |
| Fase 1 — motor de partido `MatchCore` (física, reglas, IA, 5 estilos, 5 dificultades) | ✅ hecha y probada (146 pruebas; 1.000 partidos sin errores) |
| Fase 1 — capa Unity (pantallas, cámara, controles táctiles, panel de depuración) | ⚠️ escrita, **sin ejecutar todavía en Unity** |
| Fases 2 a 8 — gestión, competiciones, pulido | ⏳ pendientes |

## Para probarlo

Sigue **[Docs/COMO_PROBAR_EN_ANDROID.md](Docs/COMO_PROBAR_EN_ANDROID.md)**.

## Estructura

- `Core/` — núcleo en C# puro (sin Unity): motor de partido, dominio, persistencia.
- `Game/Assets/` — capa de Unity (arranca sola: no hay que montar escenas a mano) y la DLL del núcleo.
- `Tests/` — pruebas automáticas (`dotnet test Core/Core.sln`).
- `Tools/HeadlessSim` — simula partidos sin gráficos para medir y calibrar.
- `Data/balance/match.json` — todos los parámetros del partido.
- `Docs/` — diseño, arquitectura, hoja de ruta, decisiones y calibración.

Para desarrolladores: ver `CLAUDE.md`.
