# Decisiones (ADR ligeras)

Formato: contexto → decisión → consecuencias. Estado: **Propuesta** hasta que el usuario apruebe el plan.

## D-01 · Motor y lenguaje — Propuesta
**Decisión:** Unity (LTS vigente; propongo Unity 6 LTS), C#, URP.
**Por qué:** un solo código para Android e iOS, ecosistema móvil maduro (Addressables, UI Toolkit, Test Framework), herramientas de perfilado y C# compartible con el núcleo. Alternativas descartadas: Unreal (más pesado en móvil y memoria, C++ aumenta el coste de iterar en solitario), Godot (menos tooling 3D móvil y de perfilado, ecosistema más pequeño para este alcance).
**Consecuencias:** el editor de Unity no está en el entorno de nube; la capa Unity solo se compila y prueba en la máquina del usuario.

## D-02 · Núcleo en C# puro y empaquetado — Propuesta
**Decisión (cerrada en Fase 0):** `Core` como proyecto .NET `netstandard2.1` (C# 9, compatible con Unity 6), sin paquetes NuGet, probado con NUnit/`dotnet test` en CI. En Unity se consume como **DLL precompilada**: `Tools/build-core.sh` la genera y copia a `Game/Assets/Plugins/Core/`. Se descartó asmdef porque Unity compilaría las carpetas `obj/` y `bin/` y no admite recursos embebidos; por eso las migraciones SQL son código C#, no archivos `.sql`.
**Convención reforzada por el compilador:** todo miembro público requiere comentario XML (CS1591 es error).
**Por qué:** verificabilidad en CI sin editor y límites de dependencia que el compilador hace cumplir.
**Consecuencias:** sin APIs de Unity en `Core` (vectores propios `Vec2/Vec3` en structs).

## D-03 · Motor de partido en tiempo real sin PhysX (`MatchCore`) — Aceptada por el usuario
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
**Avance (Fase 0):** el núcleo no depende de ningún proveedor: repositorios y migrador usan `System.Data.Common` (`DbConnection`). Las pruebas usan `Microsoft.Data.Sqlite`; en Unity/Android habrá que proporcionar una `DbConnection` equivalente.
**Pendiente (requiere Unity y el Galaxy A13):** spike que demuestre abrir, migrar y leer la base en el dispositivo. Candidatos: Mono.Data.Sqlite o `Microsoft.Data.Sqlite` con SQLitePCLRaw y binarios nativos Android (arm64-v8a/armeabi-v7a). Plan B: JSON versionado + SQLite solo para consultas pesadas.

## D-05 · Determinismo — Propuesta
**Decisión:** semilla única, flujos derivados por subsistema, paso fijo. Se garantiza reproducibilidad **dentro de la misma plataforma y build**; no entre arquitecturas de CPU distintas (coma flotante).
**Consecuencia:** los guardados no dependen de reproducir una simulación; guardan estado, no semillas de replay.

## D-06 · Base de datos ficticia e importador — Propuesta
**Decisión:** datos por defecto 100 % inventados en `Data/` (JSON), con esquema validado. `Tools/DbImporter` importa CSV/JSON del usuario. No se incluyen nombres, escudos ni equipaciones con licencia.

## D-08 · Dispositivo de referencia y plataforma inicial — Aceptada
**Decisión:** Samsung Galaxy A13 (SM-A135M, Android 14) como dispositivo de referencia; Android primero. Objetivo principal: **30 FPS estables en perfil Bajo**. 60 FPS queda como objetivo para gama media-alta, sin dispositivo para medirlo por ahora.
**Consecuencias:** el presupuesto de CPU/GPU se diseña para gama baja desde el inicio (IA escalonada, pocas sombras, público con impostors, resolución dinámica). Si funciona en el A13, funcionará en gamas superiores.

## D-09 · DLL del núcleo versionada en el repositorio — Aceptada
**Contexto:** el usuario no tiene .NET instalado en su PC y no conoce Unity. **Decisión:** `Game/Assets/Plugins/Core/Football.Core.dll`
(Release, netstandard2.1, ~85 KB) se guarda en el repositorio; `Tools/build-core.sh` la regenera. **Riesgo:** puede quedar desfasada
respecto a `Core/`. **Mitigación:** regenerarla en cada cambio del núcleo (anotado en `CLAUDE.md`); más adelante, un paso de CI que la
compare.

## D-10 · La capa Unity se construye por código — Aceptada
Sin escenas ni prefabs que montar a mano: `Bootstrap` (`RuntimeInitializeOnLoadMethod`) crea el juego al cargar cualquier escena;
la interfaz, el campo y los jugadores se generan por código con formas simples. Ventajas: reproducible, revisable en git y sin pasos
manuales para el usuario. El único activo "de archivo" es un material base que crea un script del editor (para que su shader entre en
la compilación). El arte definitivo se enchufará después sin tocar la lógica.

## D-11 · Entrada táctil independiente del sistema de entrada de Unity — Aceptada
`PointerInput` usa `#if ENABLE_INPUT_SYSTEM` para soportar el sistema nuevo (`EnhancedTouch`) o el antiguo (`UnityEngine.Input`) según
cómo esté configurado el proyecto, y la interfaz hace su propia detección de toques (sin `EventSystem`). Evita depender de la
plantilla de proyecto que elija el usuario.

## D-12 · Plantillas reales de seis clubes argentinos — Aceptada (a pedido del usuario)
**Contexto:** el prompt original pedía una base ficticia y que el usuario cargara los datos reales por su cuenta. El usuario pidió
después, de forma explícita, los planteles reales de Boca, River, Racing, Estudiantes, San Lorenzo e Independiente con medias.
**Decisión:** `Data/teams/argentina.json` (formato editable, con copia en `Game/Assets/Football/Resources/teams.json`) contiene esos
planteles armados con búsquedas web del 20-21/11/2026. Transfermarkt bloquea el acceso automático y sus condiciones prohíben
extraer datos, y Wikipedia no fue accesible desde la sesión; por eso las listas salen de medios y sitios de clubes, con fechas
distintas, y pueden tener errores. **Las medias son estimaciones propias** (no de EA FC ni de Transfermarkt). Solo nombres y colores:
sin escudos, logotipos ni diseños oficiales de camisetas.
**Uso:** personal. Si el repositorio es público, conviene hacerlo privado o quitar el archivo antes de compartirlo.
El juego sigue funcionando sin el archivo (usa dos equipos ficticios).
**Ampliación (23/11/2026, a pedido del usuario):** nueve clubes más (Vélez, Defensa y Justicia, Banfield, Lanús, Gimnasia, Talleres,
Belgrano, Rosario Central y Newell's), camisetas titular y alternativa (patrones nuevos: V y mitades) y rivalidades
(`Rivalries`, nivel `RivalryLevel`). Las páginas de plantel no se pudieron abrir desde la sesión (dominios bloqueados): solo
se cargaron los nombres que aparecieron en búsquedas web, y cada plantel se completó hasta 22 con jugadores «Juvenil ficticio
XXX-n» para que se pueda jugar. Están marcados para que el usuario los reemplace.

## D-13 · Defensa por posición y táctica en vivo — Aceptada (pedido del usuario: «enfocate en el gameplay del partido»)
**Decisión:** la probabilidad de una entrada depende de la geometría (orientación del defensor, si está delante o detrás del
poseedor y si este acaba de recortar), no solo de los atributos. La IA contiene (se planta entre el poseedor y su portería) y solo
entra con buenas opciones; un compañero cubre su espalda. La mentalidad y la presión se pueden cambiar en vivo y la IA las adapta al
marcador, siempre de forma visible (aviso en pantalla), nunca como ayuda oculta.
**Por qué:** que defender exija posicionarse y que el regate premie el momento justo, con efectos medibles (ver `BALANCE.md` §1.1).
**Consecuencias:** el estilo de cada equipo pasa a ser una copia propia dentro del partido (`TeamState.Style`) para poder ajustarlo
sin tocar la configuración compartida.

## D-14 · Modo 3 contra 3 en cancha cerrada — Aceptada (pedido del usuario)
**Decisión:** el mismo motor (`MatchCore`) juega con equipos de cualquier tamaño; el modo cerrado es solo datos
(`MatchConfig.CreateIndoor()`, guardado en `Data/balance/match_indoor.json`): campo de 40 × 20 m con paredes que devuelven el
balón, porterías de 3 × 2 m, sin fuera de juego y distancias de la IA escaladas. Formación `3v3`: arquero, un jugador de
apoyo y un delantero; se eligen automáticamente los mejores del plantel.
**Por qué:** reutiliza física, reglas, IA y controles ya probados, y el modo se calibra con la misma herramienta.
**Consecuencias:** no hay saques de banda, de esquina ni de puerta en ese modo; las faltas y los penales sí existen.

## D-15 · Visual de jugadores, cambios y ajustes construidos por código — Aceptada (pedido del usuario)
**Decisión:** el juego no tiene prefabs, rigs ni texturas; todo se construye por código. Por eso se adaptan los pedidos:
- Shader propio `Football/Player` (URP, sin texturas): luz de borde y suciedad en la parte baja que crece con los minutos. Un
  solo material por color, con valores globales (`_FootballRim`, `_FootballDirt`) en lugar de `MaterialPropertyBlock`, que
  rompería el agrupado SRP. Si el shader no es compatible, se usa el material plano de siempre. En calidad BAJA se apaga el borde.
- Inclinación en curvas (física: atan(v·ω/g), a la mitad), sombra que se achica al despegarse, contextura determinista
  (arqueros, más altos) y dorsal en la etiqueta del nombre (no hay atlas de números porque las figuras no tienen texturas).
- Cambios y formaciones viven en el núcleo (`MatchEngine.Squad.cs`, `Formations`) como datos, no como ScriptableObjects, para
  que los pruebe `dotnet test`. La pizarra para arrastrar jugadores se reemplaza por tocar dos jugadores para intercambiarlos.
- Ajustes: catálogo único (`SettingsCatalog`) que marca cuáles se cambian en vivo; la pausa muestra solo esos.
**Consecuencias:** no hay volumen de «relatos» (no hay relator) ni calidad de sombras (no hay sombras reales).
**Ampliación (23/11/2026):** estadio con público (figuras simples agrupadas por tribuna con colores por vértice; densidad según
el perfil gráfico) que salta en los goles, carteles LED (una malla con un material propio cuyo color se anima), postes y
travesaño cilíndricos, zonas de césped gastado y luz direccional suave en el shader de jugadores (medio Lambert, apagada en
calidad BAJA). **No hay sombras proyectadas en tiempo real**: los materiales son sin iluminación (decisión de rendimiento
para el A13) y una sombra real exigiría materiales iluminados de URP y otra pasada de dibujo; se mantienen las sombras de
contacto (discos) que se achican con la altura. Se puede reconsiderar para el perfil ALTO cuando haya mediciones en el teléfono.

## D-16 · Núcleo de gestión antes de cerrar la Fase 1 — Aceptada (pedido explícito del usuario, 23/11/2026)
**Contexto:** el roadmap dice no avanzar de fase sin confirmación; el usuario pidió de forma explícita los módulos de las
Fases 2 a 4. **Decisión:** se construyeron en `Core/` con pruebas, sin interfaz, mientras la Fase 1 sigue pendiente de
validarse en el teléfono. **Arquitectura:** el contrato del partido pasó a `Domain/Match` para respetar la regla de
dependencias (`MatchCore` y `MatchSim` son hermanos). `MatchSim` no imita a `MatchCore` por código: se calibra con datos
medidos (goles según diferencia de fuerza y efecto de cada estilo) y una prueba de 500 partidos lo verifica.
**Consecuencias:** si cambia el balance de `MatchCore`, hay que volver a medir y actualizar `matchsim.json`
(procedimiento en `BALANCE.md` §7). Los planteles de los 9 clubes nuevos están incompletos (ver D-12).

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
| Técnico | El A13 es gama baja (GPU modesta, poca RAM): el rendimiento es el principal riesgo de la Fase 1 | Alta / Alto | Medición temprana en el dispositivo; perfil Bajo por defecto; 30 FPS como objetivo |
| Distribución | Compilar para iOS exige Mac + cuenta de desarrollador; instalar en tu propio dispositivo caduca (7 días con cuenta gratuita) | Media / Medio | Empezar por Android; iOS cuando haya Mac |
