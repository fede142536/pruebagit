# Decisiones (ADR ligeras)

Formato: contexto → decisión → consecuencias. Estado: **Propuesta** hasta que el usuario apruebe el plan.

## D-01 · Motor y lenguaje — Propuesta
**Decisión:** Unity (LTS vigente; propongo Unity 6 LTS), C#, URP.
**Por qué:** un solo código para Android e iOS, ecosistema móvil maduro (Addressables, UI Toolkit, Test Framework), herramientas de perfilado y C# compartible con el núcleo. Alternativas descartadas: Unreal (más pesado en móvil y memoria, C++ aumenta el coste de iterar en solitario), Godot (menos tooling 3D móvil y de perfilado, ecosistema más pequeño para este alcance).
**Consecuencias:** el editor de Unity no está en el entorno de nube; la capa Unity solo se compila y prueba en la máquina del usuario.

## D-02 · Núcleo en C# puro y empaquetado — Propuesta
**Decisión:** `Core` como proyecto .NET `netstandard2.1` (compatible con Unity), probado con NUnit/`dotnet test` en CI. En Unity se consume como asmdef o DLL; se decide en Fase 0 según fricción real.
**Por qué:** verificabilidad en CI sin editor y límites de dependencia que el compilador hace cumplir.
**Consecuencias:** sin APIs de Unity en `Core` (vectores propios `Vec2/Vec3` en structs).

## D-03 · Motor de partido en tiempo real sin PhysX (`MatchCore`) — Propuesta, requiere tu OK
**Contexto:** el prompt original coloca el motor 3D jugable dentro de `/Game/Match` (Unity) y deja el estadístico en `Core/MatchSim`.
**Decisión:** añadir `Core/MatchCore`: física del balón, movimiento, reglas, árbitro e IA en C# puro con paso fijo de 60 Hz. `Game/Match` solo dibuja un snapshot por tick.
**Por qué:**
- *Determinismo y pruebas:* PhysX no es determinista ni se ejecuta sin Unity.
- *Calibración:* permite simular miles de partidos headless para alinear *Jugar/Ver* con *Simular* (requisito de la sección 10 del prompt).
- *Rendimiento móvil:* control total de allocations y de coste por tick.
**Costo:** escribir física del balón y colisiones simples propias. Es asumible: el fútbol necesita pocas colisiones (balón-suelo, balón-poste/larguero, jugador-jugador simplificado).
**Alternativa:** física de Unity con ajustes — más rápido al inicio, pero pierde determinismo, pruebas y calibración.

## D-04 · Persistencia — Propuesta
**Decisión:** SQLite con migraciones numeradas y repositorios tras interfaces. Librería concreta (p. ej. `sqlite-net` con SQLitePCLRaw) a validar con un spike en Android e iOS en la Fase 0.
**Riesgo:** binarios nativos y compilación AOT en iOS; si falla, plan B: JSON versionado + SQLite solo para consultas pesadas.

## D-05 · Determinismo — Propuesta
**Decisión:** semilla única, flujos derivados por subsistema, paso fijo. Se garantiza reproducibilidad **dentro de la misma plataforma y build**; no entre arquitecturas de CPU distintas (coma flotante).
**Consecuencia:** los guardados no dependen de reproducir una simulación; guardan estado, no semillas de replay.

## D-06 · Base de datos ficticia e importador — Propuesta
**Decisión:** datos por defecto 100 % inventados en `Data/` (JSON), con esquema validado. `Tools/DbImporter` importa CSV/JSON del usuario. No se incluyen nombres, escudos ni equipaciones con licencia.

## D-07 · Ramas e hitos en la nube — Aceptada
Las sesiones de nube desarrollan en la rama asignada por la sesión; los hitos se marcan con tags (`fase-N`) en lugar de una rama por hito.

---

## Stack final propuesto

| Capa | Tecnología | Justificación |
|---|---|---|
| Motor | Unity LTS + URP | Ver D-01 |
| Núcleo | C# netstandard2.1 + NUnit | Ver D-02 |
| Partido | `MatchCore` propio, paso fijo 60 Hz | Ver D-03 |
| UI gestión | UI Toolkit | Tablas/listas densas y estilos reutilizables |
| HUD partido | uGUI/overlay ligero | Coste bajo por frame |
| Datos | SQLite + JSON | Ver D-04, D-06 |
| Contenido pesado | Addressables | Instalación inicial ligera |
| CI | GitHub Actions (`dotnet test`) | Verificación automática del núcleo |

## Riesgos principales

| Tipo | Riesgo | Probabilidad / impacto | Mitigación |
|---|---|---|---|
| Técnico | **Alcance enorme** (gestión profunda + partido 3D + competiciones): es un proyecto de años para un equipo | Alta / Alto | Fases con puertas de validación; Fase 1 primero; priorizar profundidad en pocos sistemas antes que amplitud |
| Técnico | **La sensación de control táctil** no se puede verificar en la nube | Alta / Alto | Playtest temprano en tu dispositivo; instrumentación de latencia; parámetros de control en datos |
| Técnico | 22 agentes con IA en móvil de gama baja | Media / Alto | IA escalonada, medición en Fase 1, perfiles gráficos y de IA |
| Técnico | SQLite nativo en iOS/Android | Media / Medio | Spike en Fase 0; plan B en D-04 |
| Técnico | No hay Unity ni .NET aquí: parte del código no se compila en mi entorno | Cierta / Medio | Núcleo en C# puro verificable; capa Unity con pasos exactos para ti; no se declara verificado lo no ejecutado |
| Arte | Calidad "AAA" visual exige modelos, animaciones (motion capture), estadios y audio que no se generan con código | Cierta / Alto | Capa de arte intercambiable; placeholders ahora; assets definitivos CC0 o propios/encargados después |
| Licencias | Clubes, jugadores, escudos y equipaciones reales están protegidos | Cierta / Alto | Base ficticia + importador que ejecutas tú bajo tu responsabilidad; registro en `ASSETS_LICENSES.md`; nunca se distribuyen datos con licencia en el repo |
| Licencias | Assets "gratis" con licencia ambigua | Media / Medio | Solo CC0/MIT/equivalente con URL y fecha registradas |
| Distribución | Compilar para iOS exige Mac + cuenta de desarrollador; instalar en tu propio dispositivo caduca (7 días con cuenta gratuita) | Media / Medio | Empezar por Android; iOS cuando haya Mac |
