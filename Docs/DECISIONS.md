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

**Actualización (22/11/2026, pedido del usuario): planteles completos de los nueve clubes nuevos.** Se quitaron todos los
«Juvenil ficticio». Vélez, Defensa y Justicia, Banfield, Lanús, Gimnasia, Talleres, Belgrano, Rosario Central y Newell's
tienen 20–22 jugadores reales con dorsal, tomados de la sección «Plantel 2026» de cada club en Wikipedia en español (leída el
22/11/2026; los cedidos y los que figuran sin dorsal no se incluyen). Los jugadores que ya estaban conservan su media y su
puesto; los nuevos llevan una media **estimada por nosotros** (algo por debajo de la del plantel, más alta para figuras
conocidas) y el puesto general de la fuente (arquero, defensor central, mediocampista central, delantero) salvo casos
conocidos. Se descartaron los que ya no figuran en esos planteles (por ejemplo, Guido Herrera en Talleres). Una prueba exige
20–22 jugadores con dorsal y ningún ficticio en esos nueve clubes.

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
**Actualización (22/11/2026, pedido del usuario): potrero sin arqueros.** La formación `3v3` pasa a ser cierre, armador y
pivote (ningún puesto de arquero) y el arco mide 2,4 × 1,4 m. El motor detecta el equipo sin arquero (`TeamState.HasKeeper`)
y entonces: no hay atajadas ni balón en las manos, el saque de puerta lo hace el más cercano, el remate apunta lejos del
defensor más cercano al arco y el «cierre» tapa el arco. La alineación del 3v3 es 3 titulares + 3 suplentes sin puesto de
arquero obligatorio; las alineaciones 3v3 guardadas antes (con arquero) se descartan.

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

## D-17 · Identidad POTRERO, portada en un túnel y textos es/en — Aceptada (pedido del usuario)
**Decisión:** el juego se llama **POTRERO**. El menú ya no tiene un partido de fondo: una portada estilo consola (`TitleStage`)
muestra el túnel de un estadio de noche con la salida al campo iluminada y tres jugadores de cuerpo entero; el del medio viste
la camiseta del club favorito (el equipo local elegido) y los de los costados van rotando de club cada 6 s. Arriba a la derecha,
tres tarjetas con la camiseta dibujada y el nombre de esos clubes (sin escudos oficiales). Los textos del menú y de las cámaras
salen de `Data/locale/ui.es.json` / `ui.en.json` (lector propio `StringTable` en el núcleo, con pruebas); el idioma es el del
teléfono (inglés o, si no, español). Los textos antiguos de otras pantallas se irán pasando a esos archivos.
**Por qué:** la portada es más liviana que simular un partido de fondo y da identidad; los textos dejan de estar en el código.
**Consecuencias:** se elimina `MenuBackdrop.cs`; `build-core.sh` copia `ui_es.json` y `ui_en.json` a `Resources`.

## D-18 · Cuatro cámaras y repetición desde el césped — Aceptada (pedido del usuario)
**Decisión:** modos TV (zoom dinámico y anticipación), táctica panorámica, lateral baja y PRO (detrás del jugador controlado),
elegibles en Ajustes y en los ajustes rápidos de la pausa; el cambio de modo es un paso suave (`TransitionSpeed`). Todas las
cámaras usan `targetDisplay = 0` y solo hay una activa dibujando la pantalla. La repetición del gol se ve dos veces: plano de
transmisión y luego a ras del césped detrás del arco. Valores en `view_tuning.json`.

## D-19 · Cuerpos esculpidos continuos y shader de jugador mejorado — Aceptada (pedido del usuario)
**Decisión:** los jugadores se arman con mallas continuas generadas por código (`AnatomyKit`): cada pieza es una sucesión de
secciones ovaladas con distinta profundidad delante y detrás (`Ring`), con vértices compartidos para que las normales sean
suaves. Tronco en V con trapecios y pectorales, pelvis con glúteos, pantalón que cae sobre el muslo, muslo con cuádriceps y
rótula, pierna con gemelo y tobillo fino, botín con puntera, caña, lengüeta y suela, brazo con deltoides y bíceps, codo
articulado (nuevo pivote animado), antebrazo, mano con pulgar, cuello, cráneo con pómulos, mandíbula y mentón, nariz, orejas y
ojos. Peinados deterministas: corto, degradé, melena con vincha, rulos y rapado, con barba y bigote opcionales. El diseño de la
camiseta (rayas, franja, banda, V, mitades) es una «piel» que sigue la forma del tronco, ya no cajas que asomaban.
El shader `Football/Player` suma difuso envolvente, borde sutil, pliegues en la tela y brillo especular en botines y pelo; el
tipo de material va en el alfa del color del vértice, así sigue habiendo un único material por jugador (SRP Batcher, sin
`MaterialPropertyBlock`, sin objetos por fotograma). En calidad BAJA se apagan sombreado, pliegues y brillos.
**Por qué:** las primitivas sueltas se veían como un muñeco de piezas; sin rig ni modelos importados, esta es la forma más
liviana de lograr volumen anatómico en gama baja.
**Consecuencias:** se eliminan `MeshKit.Limb` y `MeshKit.Torso`; geometría verificada por cálculo (mallas cerradas y orientadas
hacia fuera), pero el aspecto final solo se puede juzgar en Unity.

## D-20 · Orquestador de carrera, Torneo y guardado en el teléfono — Aceptada (pedido del usuario)
**Decisión:** `CareerSession` une todos los sistemas de gestión día a día. Temporada argentina simplificada: Apertura y
Clausura a una rueda (localía invertida), tabla del torneo, tabla anual y promedios de 3 temporadas; al final descienden 2
por promedios (sin segunda división todavía: bajan reputación y lo anuncia el buzón) y se clasifican 4 por la tabla anual.
El Torneo usa el mismo orquestador con una sola rueda y campeón. Edad, potencial y atributos detallados de los jugadores del
archivo se generan con la semilla a partir de su media (el archivo no los trae). El perfil de mercado de cada club es fijo
por club; los rivales ofrecen solo por jugadores que pueden pagar. La partida se guarda en SQLite (`SqlCareerRepository`,
migración 8 con el estado de la partida) y, en Unity, en un archivo binario propio (`CareerFile`) porque Unity en Android no
trae SQLite; los dos guardan lo mismo y hay prueba de que una partida cargada continúa idéntica. El usuario juega siempre
como lado 0 del motor 3D; si es visitante, el informe se da vuelta antes de cargarlo. Abandonar un partido de carrera lo
simula. Los seguimientos de ojeadores en curso y las ofertas recibidas sin responder no se guardan (se pierden al cerrar).
**Por qué:** cerrar el ciclo jugable de la carrera sin dependencias nativas nuevas.
**Consecuencias:** `career.json` nuevo en `Data/balance`; `build-core.sh` copia a `Resources` también `career`, `finance`,
`management`, `matchsim` y las noticias.

## D-21 · Saque IFAB, ficha del jugador, selector con escudos y controles en arco — Aceptada (pedido del usuario)
**Decisión:**
- **Saque del medio:** un solo ejecutante en el círculo, de frente a su arco, y dos receptores atrás (ver `BALANCE.md` §1.11).
- **Atributos fijos por jugador:** la variación individual de los atributos de los jugadores del archivo se genera con su
  nombre como semilla (`PlayerCards.RandomFor`), no con la del partido, para que la ficha muestre lo mismo que se juega.
  Pierna hábil, altura y edad (que `teams.json` no trae) se estiman igual, de forma determinista (D-12).
- **Ficha:** carta (OVR, puesto, pierna, altura, edad y RIT/TIR/PAS/REG/DEF/FÍS), atributos destacados, mapa de puestos
  compatibles, físico y moral, y estadísticas (temporada en la carrera; el partido en curso en la pausa; ninguna en el
  amistoso). Se abre manteniendo presionado un jugador en la alineación y en la pausa, y tocándolo en el Plantel de la
  carrera. Las estadísticas de temporada se guardan (migración 9 y archivo de partida versión 2; la versión 1 se sigue leyendo).
- **Escudo procedural** (`ClubCrest`): silueta de escudo, diseño de la camiseta titular, colores y siglas; ningún escudo real.
- **Presentación previa** (solo 11v11): toma panorámica de cada mitad, pizarra táctica sobre el césped con las líneas del
  dibujo y fichas flotando sobre los titulares (silueta, OVR, puesto, flecha de forma y apellido); placa con escudo; SALTAR.
- **Controles:** arco de 4 botones abajo a la derecha (PASE, FILTRADO, TIRO/DESPEJE, ESPRINT); sin balón, las mismas casillas
  son PRESIÓN, 2ª MARCA y BARRER. CAMBIAR, CENTRO y ARQUERO quedan como botones chicos sobre el arco. El FILTRADO ya no es
  opcional en Ajustes (siempre está en el arco). DESPEJE reemplaza a TIRO en el campo propio.
- **Estadio:** pista perimetral azul, césped más vivo, LED continuos, tribunas más cerca, bancos de suplentes y fotógrafos
  en el lateral de la cámara. **Figura:** cabeza al 85 % y cuello más largo (≈7,5 cabezas), cejas, boca y franja en las medias.
**Consecuencias:** los partidos amistosos con equipos del archivo ya no varían los atributos de un partido a otro.

## D-22 · Modo Carrera completo: DT, directiva, entradas, socios, obras del estadio y medidor de gestión — Aceptada (pedido del usuario)
**Decisión:**
- **DT propio** (`ManagerProfile`): nombre, apellido, nacionalidad (Argentina de fábrica), uno de 6 avatares y traje o buzo.
  Popularidad inicial 80; la directiva lo despide por debajo de 25 (`career.json` → `Board.FireThreshold`). El despido
  termina la carrera: se borra la partida y se vuelve a crear un DT.
- **Datos institucionales** de los 15 clubes en `argentina.json` (bloque `Club`): fundación, estadio y capacidad reales;
  presupuesto de fichajes, masa salarial y valor **estimados por nosotros** (1 USD = 1.400 ARS), y jerarquía Grande /
  Media tabla / En desarrollo. Sin escudos reales (D-12).
- **Directiva**: cinco objetivos (nacional, continental, juvenil, finanzas y exposición de marca) con prioridad
  1 CRÍTICA a 4 BAJA según la jerarquía; la popularidad cambia con resultados, clásicos, caja y el balance de fin de temporada.
- **Público por partido** (`MatchAttendanceCalculator`): importancia ×1,45 final de copa, ×1,35 clásico, ×1,25 decisivo,
  ×1,00 normal y ×0,65 sin nada en juego; racha, posición respecto de lo esperado, ánimo y malestar de los socios, y
  elasticidad del precio. La taquilla y los consumos van al libro contable. El estadio 3D muestra esa ocupación
  (`StadiumLook`): cada asiento se llena o no según un hash fijo.
- **Entradas**: cuatro niveles (Popular, Normal, Alta, Premium) y ajuste fino para el próximo partido; `TicketPricingAdvisor`
  da un rango ideal por partido (verde ideal, amarillo elevado, rojo excesivo).
- **Socios** (`SupporterRequestSystem`): si el precio supera el rango, piden una rebaja por el buzón; aceptar mejora la
  relación, ignorar sube el malestar. Con malestar sostenido sobre 75 hay repudio, menos público y piden la salida del DT.
- **Estadio** (`StadiumRemodelingSystem`): cuatro categorías × tres niveles con costo y semanas (tribunas +4.000/+8.000/
  +12.000 asientos en 16–20/32–36/48–52 semanas; parking +1.000/+2.500/+5.000 plazas en 10/20/32; equipamiento 6/14/24;
  servicios 8/18/30). Una obra por categoría; cuenta regresiva semanal; al terminar se aplica y avisa. Durante una
  ampliación de tribunas se cierra una parte del estadio y se ven andamios en el 3D y en la vista isométrica.
- **Medidor de gestión** (0–100): hinchas 35 %, plantel y fichajes 35 %, presupuesto 30 %, con cada factor y sus puntos
  a la vista («Refuerzo X: +4 pts»).
- **Copa Argentina** por eliminación directa con todos los clubes (la tabla de liga no la mezcla). Clima del partido
  determinista (`ForecastOf`, por mes y semilla).
- **Persistencia**: migración 10 (DT, datos de clubes, estadios, obras, socios, historial de precios, fichajes e historial
  del medidor) y archivo de partida versión 3 (lee las versiones 1 y 2).
- **Unity**: creación del DT (con figura 3D en traje o buzo), elección de club con expectativas, Central con calendario
  de 5 días, Oficina y objetivos, Finanzas (anillo de ingresos y precio en vivo) y Remodelación del estadio; barra de
  5 botones abajo. Plantilla y Mercado son las de antes, achicadas para dejar lugar a la barra.
**Consecuencias:** las partidas guardadas antes de esta versión se leen con datos de fábrica para lo nuevo (DT sin nombre,
estadio inicial del club). Todos los números de estos sistemas están en `Data/balance/career.json`.

## D-23 · Jugador con una sola malla y 20 huesos (`SkinnedMeshRenderer`) — Aceptada (pedido del usuario)
**Decisión:** el jugador deja de ser un conjunto de mallas por articulación (`AnatomyKit`, D-19, eliminado) y pasa a ser
**una sola malla** dibujada por un `SkinnedMeshRenderer` con 20 huesos (raíz, pelvis, columna, pecho, cuello, cabeza,
hombro/codo/muñeca y cadera/rodilla/tobillo/puntera de cada lado). La arma `SkinnedBodyGeometry` sin Unity:
- Piezas esculpidas por secciones que se solapan en las articulaciones; cada vértice lleva hasta dos huesos con pesos
  suaves según su distancia a la articulación (codos, rodillas, hombros, cuello e ingle), así las costuras no se abren al
  doblar. Calidad de piel `Bone2`.
- Ropa con bordes: ruedo de la camiseta sobre el pantalón, botamanga abierta, borde de la manga, puño enrollado de la media
  y franja; pliegues y trama en el shader. Botines con suela, lengüeta y cuatro tapones.
- Cara con arco superciliar, nariz, pómulos, mentón y orejas aparte; seis peinados (corto, degradé, melena con vincha, rulos,
  rapado y jopo) y barba o candado elegidos por hash. Proporción de 7,6 cabezas (cabeza al 80 %).
- Shader (sigue compatible con el agrupado SRP, sin `MaterialPropertyBlock`): calidez subdérmica en la penumbra de la piel,
  brillo de sudor que crece con los minutos, tela mate con trama fina y pliegues según una máscara por zona (UV0) y pegados a
  la tela con la posición de reposo (UV1). Si el shader no está, la misma malla se dibuja con un material plano por color.
- Animación procedural sin basura por fotograma: la de antes más giro de pelvis y contragiro del pecho, inclinación en las
  curvas repartida entre raíz, pelvis y columna, apoyo del pie (suela paralela al césped, puntera que se dobla al despegar y
  pelvis que baja para que el pie de apoyo toque el suelo; no es IK completa ni bloquea el deslizamiento) y mirada al balón
  repartida entre cuello y cabeza con topes anatómicos (también hacia arriba y abajo).
**Mediciones sin Unity** (`dotnet run --project Tools/SkinCheck`): 2.394–2.582 triángulos con camiseta lisa según peinado
y vello (hasta 2.570 con rayas; 2.326 el DT de traje); 0 pesos inválidos; al doblar 100° el hueco en codo y rodilla queda en
0,5–1,5 cm con pesos contra 4,5–6,1 cm con piel rígida; hombro a 80°: 2,6 cm contra 3,7 cm.
**Consecuencias:** 22 mallas con huesos por partido (antes ~9 objetos por jugador). Falta medir en el Galaxy A13 el costo de
la piel por GPU/CPU y confirmar 0 B de basura por fotograma con el perfilador.

## D-24 · Primera División completa: 30 clubes, torneos por zonas y selectores paginados — Aceptada (pedido del usuario, 22/11/2026)
**Decisión:**
- **Datos** (`Data/teams/argentina.json`, copia en `Game/Assets/Football/Resources/teams.json`): se agregan 15 clubes
  (Huracán, Argentinos Juniors, Godoy Cruz, Tigre, Platense, Unión de Santa Fe, Instituto, Atlético Tucumán, Central Córdoba de
  Santiago del Estero, Barracas Central, Sarmiento de Junín, Independiente Rivadavia, Deportivo Riestra, Aldosivi y San Martín de
  San Juan) y los 15 anteriores quedan intactos. Cada uno trae sigla de 3 letras, camisetas titular y alternativa, estilo y
  formación, ciudad, fundación, estadio y capacidad (de Wikipedia), estacionamiento y estado inicial del césped, jerarquía
  (Media tabla o En desarrollo), presupuestos y valor del club (estimaciones propias en la escala de los demás) y rivalidades.
- **Planteles reales** de 22 jugadores por club, sin ficticios: nombres, dorsales y fecha de nacimiento (edad real en
  `PlayerDefinition.Age`) salen de las páginas de plantel de es.wikipedia.org (Instituto, de en.wikipedia.org porque la versión
  española trae solo 13). **Limitaciones, a corregir editando el archivo:** esas páginas solo distinguen arquero, defensor,
  mediocampista y delantero, así que las posiciones finas (lateral, volante central o por afuera, extremo) se infirieron del
  dorsal; Godoy Cruz y San Martín de San Juan no traen dorsales (se asignaron por la costumbre del puesto); no traen pierna hábil
  ni altura (se siguen estimando de forma fija, D-21); y la selección de los 22 entre los 30–50 de cada página se hizo con
  un criterio propio (numeración de primer equipo y edad), por lo que puede faltar algún titular. **Las medias son estimaciones
  propias** (once inicial entre 65,7 y 71,7 según el club; mínimo 62; el máximo individual es 75).
- **Torneos por zonas** (`FixtureGenerator.TwoZones`, `career.json` → `ZoneSplitFrom` = 20): con 30 clubes un todos contra todos
  son 29 fechas por torneo y dos torneos no entran en el año. Desde 20 clubes cada torneo se juega en dos zonas de 15 (serpentina
  por reputación): 14 partidos de zona y 1 interzonal, en 15 fechas. En zonas impares, cada fecha los dos clubes libres se
  cruzan entre sí; en zonas pares se agrega una fecha final entre los de igual cabeza de serie. Con menos de 20 clubes, todo igual
  que antes. La tabla del torneo es general (todos juegan 15), la anual suma Apertura y Clausura (30 partidos) y los promedios
  y descensos usan los mismos puntos por partido. **Pendiente:** los playoffs entre zonas y que los cruces interzonales sean los
  clásicos (hoy los fija el sorteo de los libres).
- **Copa Argentina**: con 30 clubes son 5 rondas; se corrigió que el club que pasa directo en una ronda impar quedara fuera en las
  siguientes (antes solo se recuperaba en la primera).
- **UI**: el selector del amistoso pagina la grilla de escudos (16 por página) y filtra por jerarquía; el de la carrera tiene contador
  y filtro y muestra la ciudad; la tabla de Central pagina de a 15 (abre en la página del club del usuario).
**Consecuencias:** `career.json` gana `ZoneSplitFrom`; `ClubInfo` gana `City`, `ParkingSpaces` y `Pitch` (opcionales: sin ellos
valen los de la jerarquía); las partidas guardadas con 15 clubes siguen cargando porque el fixture ya está guardado.


## D-25 · Jugador: extremidades continuas, AO pintado, decales por atlas, sombra proyectada, anclaje de pies y LOD — Aceptada (pedido del usuario)
**Decisión (amplía D-23 y reemplaza la parte de D-15 que decía «sin texturas ni atlas de números»):**
- **Extremidades** de una sola superficie (cadera–tobillo, hombro–muñeca) con tres anillos de soporte en cada flexión y pesos de dos huesos por cadena; el botín mezcla con el hueso de la puntera. Oclusión ambiental pintada en el color de los vértices (14–25 % bajo el mentón, axilas, entrepierna, mangas, ruedos y medias).
- **Atlas R8 de 256×256 (64 KB)** generado por código (`PlayerAtlasData`/`PlayerAtlas`) y publicado como textura global `_FootballAtlas`; la camiseta trae proyección plana en UV2–UV4 (dorsal de 1 o 2 dígitos en la espalda y escudo en el pecho). Un solo material, sin `MaterialPropertyBlock`.
- **Cara** con cuencas, arco superciliar, pómulos, labios y mentón en la geometría (columnas cada 7° al frente) e iris, esclerótica y labios en el color de los vértices; **pelo y barba** de mechones.
- **Sombra proyectada**: segunda pasada `PlanarShadow` (`SRPDefaultUnlit`, stencil) en `FootballPlayer.shader`, solo en calidad MEDIA y ALTA; el público usa `Football/Crowd` (`UsePass` del cuerpo). Los jugadores se dibujan en la cola 2100 para quedar después de la cancha.
- **Animación**: cadencia acompasada con la velocidad del suelo, amortiguación `1 − e^(−λ·dt)` de la pose pedida respecto de la zancada base y anclaje de pies con IK de dos huesos (el pie de apoyo también al patear).
- **LOD** de dos niveles sobre el mismo esqueleto: ~2.520 triángulos de cerca y ~840 desde 24 m de la cámara (histéresis de 2 m).
**Sin ejecutar en Unity:** el `UsePass`, el stencil (requiere buffer de profundidad con stencil), `SetShaderPassEnabled` y la IK son lo que más probablemente necesite ajustes en el dispositivo. Medido sin Unity (`Tools/SkinCheck`): triángulos, pesos, una sola superficie por extremidad, AO y proyección de decales.

## D-26 · 45 minutos por parte con tiempo agregado, repetición en cancha cerrada y cámara más baja — Aceptada (pedido del usuario, 23/11/2026)
**Decisión:**
- **Reloj**: el 3 contra 3 también muestra 45 minutos por parte (antes 20); la duración real sigue siendo la que se elige (2, 3 o 5 min por parte).
- **Tiempo agregado** (`RulesConfig.Stoppage*`, `MinAddedMinutes` = 1, `MaxAddedMinutes` = 7): al terminar la hora reglamentaria de cada parte el motor anuncia
  los minutos que sumaron los hechos de esa parte (gol 0,75; cambio 0,5; lesión 1,5; expulsión 1; amarilla 0,25; penal 1), redondeados hacia arriba. El reloj
  sigue contando (45:00, 46:10…) y un cartel «+N» y un aviso lo anuncian; el partido termina al cumplirse. `Minute` es el reloj de pantalla (la segunda parte vuelve a
  empezar en 45) y `PlayedMinute` el minuto jugado de verdad (para minutos de jugadores y cambios). Los hechos guardan su parte (`MatchEvent.Half`) y la placa
  escribe «45+2'».
- **Repetición**: en la cancha cerrada la segunda pasada (a ras del césped detrás del arco) quedaba del otro lado de la pared y solo mostraba la pared; ahora esa
  cancha repite una sola vez desde la cámara de transmisión, y al terminar la cámara vuelve de golpe a su sitio y se reinicia la interpolación del partido.
- **Césped**: se quitan las manchas de «desgaste» (óvalos oscuros en las áreas y el círculo central).
- **Cámara de transmisión**: altura 13 m y distancia 34 m (≈ 21° sobre el horizonte; antes 21 m y 30 m, ≈ 35°). Se ajusta en `view_tuning.json`.
**Medición** (1000 partidos, semilla 1): 2,79 goles por partido (antes 2,69; el tiempo agregado suma ≈ 4 % de juego) dentro del objetivo 2,4–2,9;
sin excepciones ni partidos sin terminar. Pase 85,9 %.

## D-27 · Reloj que arranca con el puntapié, cansancio por minutos de partido, marcas de equipo y DT editable — Aceptada (pedido del usuario, 23/11/2026)
- **Reloj**: no corre mientras se espera el saque del medio (inicio de cada parte y después de cada gol); arranca con el puntapié. Antes la espera de la persona (hasta 8 s) gastaba minutos.
- **Desgaste**: `RulesConfig.FatigueReferenceHalf` (180 s) y `FatigueFactor` (1,25). El gasto de resistencia se mide en minutos de partido: con partes de 2 minutos reales
  se acelera (180/120) para que a los 75' el cansancio sea el mismo con 2 que con 5. Medición (500 partidos): resistencia al final 66,7 % (antes 71 %), la más baja 30 %;
  2,63 goles por partido, pase 85,6 %.
- **11 contra 11**: los jugadores se agrandan un 22 % y llevan una marca en el piso (celeste el equipo de la persona, rojo el rival).
- **DT**: nombre y apellido se escriben con el teclado (la computadora lee texto con `PointerInput.Typed`; el celular usa el teclado en pantalla). Retroceso borra y Enter termina.
- **Buzón**: los récords mostraban la clave de texto y trataban la marca como si fuera un jugador; ahora se lee «¡Récord! Mayor goleada (diferencia de goles): 5 (Club)».

## D-28 · El final de cada parte espera a que termine la jugada — Aceptada (pedido del usuario, 23/11/2026)
- Al cumplirse el tiempo agregado el árbitro no corta una jugada de gol: la parte termina en la primera pelota parada que cierra la jugada
  (lateral, saque de arco o saque del medio después de un gol). Un córner o un tiro libre se dejan ejecutar; un **penal** nunca se corta
  (`PenaltyPending`): termina como gol, fuera o atajada, y la parte acaba en la pelota parada siguiente.
- Márgenes en `RulesConfig` (`match.json`): `EndPlaySoftMinutes` 0,75 (pasado ese margen, si el balón está lejos de las dos áreas —`DangerZoneFraction` 0,5— y no hay
  un tiro en el aire, se corta: `HalfEndReason.PlayOver`) y `EndPlayHardMinutes` 6 (seguro: nunca se espera más, salvo un penal). `MatchEngine.LastHalfEndReason` dice por qué terminó.
- **Medición** (1000 partidos, semilla 1): 2,90 goles por partido (esperar sin límite daba 3,07; cortar al instante, 2,66), pase 85,7 %. El resto del tiempo agregado es el mismo.
- Pruebas: `AiNeverKeepsDribblingInsideTheBox` admite 270 ticks (la secuencia de azar cambió y el pase de 233 ticks en la prórroga es legítimo);
  `Ai_SubstitutesAnInjuredPlayerFirst` admite que un cambio ya pedido salga antes. **No hay una prueba que fuerce un penal justo al final**: la regla se apoya en `PenaltyPending`, ya probado.

## D-29 · Audio real de hinchada y música, cancha táctica y jugador más humano — Aceptada (pedido del usuario, 23/11/2026)
- **Audio**: la hinchada de fondo, el grito de gol y los cánticos de Boca son archivos de `Resources/Audio` (registrados en `ASSETS_LICENSES.md`); se conserva el murmullo
  sintético solo como respaldo si falta el archivo. Los cánticos de Boca suenan a tramos de 18–34 s desde un punto al azar del archivo, con 12–40 s de silencio, solo si el
  equipo local es «Boca Juniors». La música de los menús (nuevo ajuste «MÚSICA DE LOS MENÚS») suena en todo menos en el partido, **pausa incluida**, con fundido. El azar del audio es
  de presentación (semilla por reloj), no de juego.
- **Táctica**: «PLANTEL Y TÁCTICA» dibuja una cancha con cada jugador en su puesto de la formación (sigue la formación, los intercambios y los cambios). Tocar un punto equivale a tocar su fila.
- **Jugador**: hombros y brazos más juntos al torso (huesos de hombro a ±0,235 m, antes ±0,26), manga con hombro redondeado, muslos y pantorrillas más gruesos, tronco con pendiente de trapecio,
  cuello de la camiseta y 14 lados en el tronco. LOD0 2.540–2.870 triángulos (antes 2.450–2.790).
- **Aviso «EL PARTIDO DEJÓ DE AVANZAR»**: se quedaba pegado aunque el partido siguiera; ahora se retira apenas deja de cumplirse la condición.
- `Tools/SkinCheck --ply ruta [lod]` vuelca la malla con colores para mirarla fuera de Unity.

## D-30 · Primer toque y cabezazo, informe de benchmark y lista de verificación — Aceptada (pedido del usuario, 23/11/2026)
- **Animación**: nuevas acciones `Control` (primer toque) y `Header` (cabezazo). El motor las publica al ganar la posesión un jugador de campo (`HeaderHeight` 1,25 m,
  `TouchDisplaySeconds` 0,4 s en `match.json`); son solo presentación y no cambian el juego (mismas métricas). En 4 minutos hubo 517 controles y **ningún cabezazo**: el balón casi siempre viaja por el piso, así que el cabezazo queda implementado pero rara vez se verá.
  En Unity `PlayerModel` anima el control (muslo arriba, pierna que cede, brazos abiertos) y el cabezazo (arco hacia atrás, salto, cabeceo).
- **Modelo real (CC0)**: no se pudo incorporar. El entorno de nube no llega a los sitios de descarga (Quaternius, Kenney, Poly Pizza) ni tiene Blender. Queda como tarea del usuario: descargar un pack humanoide con licencia clara y registrarlo en `ASSETS_LICENSES.md`.
  El rig actual de 20 huesos admite retargeting, pero **no se escribió el importador sin poder probarlo**.
- **Benchmark**: el informe final suma asignaciones por segundo, ajustes gráficos y GPU, y se guarda en `benchmark_report.txt` (carpeta de datos de la aplicación).
- `Docs/VERIFICACION_EN_CELULAR.md`: lista de lo que nunca se ejecutó en Unity, con objetivos medibles.

## D-31 · Pruebas de carrera, dificultad y asignaciones; reacciones de la hinchada y APK por script — Aceptada (pedido del usuario, 23/11/2026)
- **Carrera**: 5 temporadas simuladas mostraron planteles que crecían sin tope (los contratos no vencen) y sueldos iguales para todos los clubes con ingresos muy distintos. Se ajusta la masa salarial inicial a la facturación estimada
  y los clubes de la computadora sueltan el excedente (`MaxAiSquadSize` 32, `MinAiGoalkeepers` 3). Números en `BALANCE.md` §12. Queda pendiente que los fichajes de la computadora también paguen según el club.
- **Núcleo sin basura**: `AllocationTests` mide 0 bytes por tick y 0 al copiar el estado. No reemplaza el benchmark en el celular (IL2CPP y Unity pueden asignar por su cuenta).
- **Hinchada**: reacciones breves tomadas del archivo del grito (¡ooh! en remates y atajadas, protesta más grave en faltas, amarillas, rojas y offsides), con enfriamiento de 1,6 s para que no se pisen.
- **APK**: `CONSTRUIR_APK.bat` compila sin abrir Unity (`BuildAndroid.BuildBatch`) y, si hay un celular con depuración USB y `adb`, lo instala. **No se probó**: requiere Unity y Android SDK.

## D-32 · Cuerpo humano importado (Quaternius, CC0) — Aceptada (pedido del usuario, 23/11/2026)
- El usuario aportó el pack «Universal Base Characters» (gratuito, CC0). El juego no puede leer glTF sin un paquete extra y el modelo trae 12.500 triángulos y texturas de 2K–4K, así que se **convierte fuera de Unity**
  (`Tools/ImportHumanoid/convert.py`) a un archivo propio de 84 KB: cuerpo del cuello para abajo en pose A, reducido (LOD0 ≈ 3.700 triángulos, LOD1 ≈ 760), pesos sobre los 20 huesos del juego y una región por vértice.
  Sin texturas: se pinta por vértice con los colores del club (camiseta, short, medias, botín, guantes del arquero, pantalón largo, manga larga). Los diseños (rayas, franja, banda, V, mitades) también por vértice, con bordes algo irregulares de cerca.
- **Cabeza, pelo, cara y barba siguen siendo procedurales** (peinados y rasgos por jugador); el modelo importado no trae cabeza útil para eso. Cuello y cuello de camiseta son piezas procedurales.
- Se usa en calidad MEDIA y ALTA; en BAJA (y si falta el archivo) queda el cuerpo procedural de siempre. Jugador completo: 4.540 triángulos en LOD0 y 970 en LOD1 (antes 2.700 y 860). **Costo en el A13 sin medir**: si baja el FPS, bajar `LOD0_TRIS` en `convert.py` o usar calidad BAJA.
- `BindPositions` pasó de campo a propiedad: con el cuerpo importado el esqueleto del juego toma las posiciones de ese modelo (muslo 0,43 m, pierna 0,46 m, altura 1,82 m con cabeza).
- Sin verificar en Unity: la apariencia con la luz y el shader del juego, la deformación al correr y patear (los pesos se redujeron de 4 huesos a 2), el anclaje de los pies y el tamaño del dorsal. Lo único comprobado es la malla armada (SkinCheck) y una vista previa por software.
- Se quitaron del repositorio las texturas del pack (30 MB, sin uso) y los modelos pasaron a `ThirdParty/`.

## D-33 · Canciones de La 12 continuas en los partidos de Boca — Aceptada (pedido del usuario, 23/11/2026)
- Los cánticos de Boca ahora son dos canciones de La 12 que se encadenan **sin silencios** durante todo el partido: cuando a la que suena le quedan 3 s, la otra entra con fundido cruzado; arranca por una al azar y después se alternan.
  Antes sonaban a tramos al azar con 12–40 s de silencio (decisión de D-29), que el usuario percibió como baches. Se recortó además el silencio de ~2 s que tenía la primera canción al principio y al final.
- Solo suenan si el equipo local es «Boca Juniors». Se retiró el archivo anterior de cánticos. Las canciones son de origen y licencia desconocidos (uso personal): ver `ASSETS_LICENSES.md`.

## D-34 · Alineación del modo carrera: once inicial y banco a elección del DT — Aceptada (pedido del usuario, 23/11/2026)
- Antes la carrera armaba sola el once y el banco (el mejor de cada línea). Ahora `UserTactics.Lineup` guarda lo que elige el DT («ids de titulares por puesto ; ids de suplentes») y `MatchPreparation.Resolve` lo respeta puesto por puesto.
  Si un elegido no puede jugar (lesión, sanción o ya no está en el club) lo reemplaza el mejor disponible para ese puesto, sin tocar el resto; los suplentes que no puedan jugar se descartan. Sin elección (o texto inválido) sigue la alineación automática de siempre.
- Banco de hasta 9 (`MatchPreparation.BenchSize`). Se guarda en el archivo de la partida (versión 4) y en SQLite (migración 11). Las partidas viejas cargan con alineación automática.
- Pantalla `CareerLineupScreen` (botón «ARMAR ONCE Y BANCO» en Plantilla): cancha con el once, banco y no convocados; se toca un jugador y otro para intercambiarlos; «AUTOMÁTICA» devuelve la elección a la computadora. Sin verificar en Unity.
- Al cambiar de formación se mantiene el orden de los titulares (el jugador i sigue en el puesto i): conviene revisar el once después de cambiar el dibujo. No hay penalización por jugar fuera de posición en los atributos.

## D-35 · Pase de primera, cambios visibles en el once y modo carrera de visitante — Aceptada (pedido del usuario, 23/11/2026)
- **Pase de primera**: si la persona pulsa un botón de golpeo mientras un pase de un compañero viene hacia ella (balón suelto, tocado por último por un compañero), la orden espera hasta
  `HumanFirstTimeBuffer` (1,5 s; antes solo 0,2 s) y, al recibir, el jugador espera `FirstTimeControlDelay` (0,03 s; antes 0,18 s) y pasa o patea. Si el rival se queda con el balón la orden se descarta; fuera de juego vuelve a valer 0,2 s
  (para no disparar un saque manual por error). Medido: de primera 0,033 s con el balón; sin pedirlo antes, 0,183 s. Solo afecta a la persona (la IA no cambia: mismas métricas).
- **Cambios en Plantel y táctica**: al pedir un cambio, el que entra aparece ya en su puesto de la lista y de la cancha («ENTRA POR …») y en el banco como «(ENTRA)»; se hace en el próximo parón, como siempre.
- **Carrera de visitante**: el estadio ya era el del local (capacidad y obras), pero la tribuna, los carteles y el marcador usaban los colores del usuario. Ahora el dueño de casa va a la izquierda del marcador, la tribuna lleva sus colores y el visitante usa su camiseta alternativa si se confunden.
  Los cánticos de La 12 suenan **solo si Boca es el dueño de casa**; de visitante queda la hinchada genérica. El rótulo LOCAL/VISITANTE de la presentación sigue al dueño de casa.
  Sin verificar en Unity. El usuario sigue siendo el equipo 0 del motor (ataca hacia +X): el lado de la cancha no cambia.

## D-36 · Modo carrera: calendario, historial, cruces de la copa y cartel de campeón — Aceptada (pedido del usuario, 23/11/2026)
- **Calendario** (`CareerCalendarScreen`, botón en la Central): todos los partidos del club en una temporada (Apertura, Copa y Clausura) con el resultado (verde ganó, amarillo empató, rojo perdió; en la copa, el que definió por penales) y los que faltan; flechas para ver temporadas anteriores. Se lee de `Data.Fixtures`, que conserva todas las temporadas.
- **Historial** (`CareerHistoryScreen`): cada competición con su campeón y cómo terminó el usuario (campeón, puesto, o hasta qué ronda llegó en la copa) y el conteo de títulos. Se calcula de los partidos guardados: **no hay datos nuevos que guardar** y las partidas viejas ya lo tienen.
- **Cruces de la copa**: la Central tiene una cuarta pestaña COPA con todos los cruces de la ronda del usuario (con flechas para otras rondas y el club que pasó directo); se abre sola cuando el próximo partido es de copa. Antes mostraba la tabla del torneo, que en esas fechas está en cero.
- **Cartel de campeón**: al terminar una competición (la última fecha o la final) aparece un cartel: «¡CAMPEÓN!» con estrellas si ganó el usuario, o el final del torneo con el campeón y la posición del usuario. También llega una noticia al buzón. `CareerSession.TakeCeremonies`; las competiciones que ya habían terminado al cargar una partida no se anuncian de nuevo.
- La Central ya no dibuja las camisetas de los dos equipos (queda el escudo) y la calificación pasó de círculos a estrellas pequeñas (`UiKit.StarSprite`, también en la selección de equipos y de club).
- El mensaje de fin de temporada ahora dice que se pulse AVANZAR para empezar la siguiente (la temporada nueva se arma al avanzar, no al jugar el último partido).
- Sin verificar en Unity: distribución de las pantallas nuevas y legibilidad de las tablas.
- **Menú de la carrera arriba (ajuste posterior)**: la barra de secciones (Central, Plantilla, Mercado, Oficina, Finanzas) pasó de abajo a arriba, en la fila de Guardar y Salir; abajo quedan la cabecera del club (nombre, fecha, caja, DT) y la línea de estado.
  Así los botones propios de cada pantalla (Calendario, Historial, paginadores, Volver) ya no se pisan con la barra ni con la línea de estado. Las pantallas de alineación, calendario e historial subieron su borde inferior a y = 160.

## D-37 · Primera y Nacional B, contratos y renovaciones, jugadores libres y buscador por nombre — Aceptada (pedido del usuario, 23/11/2026)
- **Divisiones**: al crear la carrera se elige cuántos clubes juegan Primera (30 = todos, sin Nacional B; 24, 22, **20 (predeterminado)**, 18 o 16; el Nacional B tiene como mínimo 6). Empiezan en Primera los mejores por su puesto en la tabla anual anterior
  (`ClubInfo.AnnualRank`, editable en `argentina.json`) o, **sin ese dato —que es lo que hay hoy, no tengo la tabla real—, por la media de los 11 mejores del plantel**; el resto juega el Nacional B. Con menos de 24 clubes, Apertura y Clausura son todos contra todos; el Nacional B, un torneo ida y vuelta. La Copa Argentina sigue siendo de los 30.
- **Ascensos y descensos**: los dos últimos de los **promedios** (puntos por partido de las últimas 3 temporadas, ligas de Primera y de Nacional B) bajan; los dos primeros del Nacional B suben; el tercero del Nacional B juega la **promoción** (ida y vuelta) contra el club de Primera que quedó
  `PlayoffFromBottom` = 3 puestos antes del final de los promedios (el 17.º con 20 clubes); el ganador juega en Primera. La cantidad de clubes de cada división no cambia. Un ascendido arrastra el promedio que hizo en el Nacional B (simplificación: no se descuenta).
  Hay cartel de ascenso/descenso, noticias y la promoción figura en el calendario y el historial.
- **Contratos**: duran como mucho 5 años y vencen al cerrar la temporada (`Contract.ExpiresOn.Year ≤ temporada`). El usuario renueva desde **Plantilla → CONTRATOS** (más años de los que le quedan, hasta 5; los veteranos solo aceptan contratos cortos; pide +10 % de sueldo; se niega si está descontento o si el sueldo no entra en el presupuesto de sueldos).
  Si no se renueva, **se va libre**. Los clubes de la computadora renuevan a casi todos (los de menor nivel del plantel tienen más chances de irse) y los que quedan cortos firman a los mejores libres.
- **Jugadores libres**: aparecen en el mercado con «LIBRE» (con filtro «SOLO LIBRES»); se firman sin transferencia, con sueldo propio de libre (70 % del de mercado) y 1 a 5 años a elección. Los libres que pasaron la edad de retiro no figuran (equivale a haberse retirado: no se agregó un indicador nuevo).
- **Buscador por nombre** en el mercado: se escribe con el teclado (físico o en pantalla), sin importar mayúsculas ni tildes, y la lista se actualiza mientras se tipea.
- **Guardado**: archivo de la partida versión 5 y migración 12 de SQLite (`division` por club y `primera_size`); las partidas viejas cargan con una sola división.
- Sin verificar en Unity: pantallas de contratos y de elección de cantidad de clubes, buscador y firma de libres.

## D-38 · El pie llega al balón al patear — Aceptada (pedido del usuario, 23/11/2026)
- El motor suelta el balón en el primer instante del golpeo, pero la animación de tiro dedicaba el 40 % (0,18 s) a armar la pierna: el balón salía antes de que el pie llegara. Ahora el armado dura el 12 % (≈ 0,05 s) y el pase alcanza su punto de contacto al 15 %.
  Es solo visual (el motor no cambia). Sin verificar en Unity. La sincronización fina requiere animaciones de captura de movimiento con un instante de contacto marcado (pendiente del pack de animaciones).

## D-39 · «Fútbol vintage» (Primera de 20 + Nacional de 29) y tabla completa — Aceptada (pedido del usuario, 23/11/2026)
- **Modalidades de carrera**: al elegir club hay un botón de modo. **Clásico** = los 30 clubes de siempre (con el selector de cantidad de D-37 y sin cambios). **Fútbol vintage** = **49 clubes**: Primera de **20** (los mejores 20 de la tabla anual 2026 que pasó el usuario)
  y **Nacional de 29** (los puestos 21 a 30 de esa tabla, más Godoy Cruz y San Martín (SJ), más 17 clubes del Nacional B/Primera Nacional). El Torneo, los amistosos y el modo clásico solo ven los 30 originales (`TeamDefinition.VintageOnly`, `CareerSession.PoolFor`).
- **Tabla anual 2026**: `ClubInfo.AnnualRank` ahora lleva el puesto real de cada club (datos de las capturas del usuario). Los 30 clubes viejos y los 2 vintage con puesto (Gimnasia de Mendoza 17.º, Estudiantes de Río Cuarto 30.º) se reparten por ese puesto; Godoy Cruz y San Martín no figuran y juegan el Nacional.
- **Nacional de 29**: se juega como la Primera grande: **dos zonas** (cabezas de serie por reputación en serpentina) más cruces interzonales, a una rueda (`StartSecondDivision` usa `FixtureGenerator.TwoZones` desde `ZoneSplitFrom` clubes; con menos, ida y vuelta como antes). Ascensos/descensos y promoción son los de D-37 (siguen siendo 20 y 29 clubes).
- **Clubes nuevos (17)**: nombre real, pero **planteles GENERADOS** (nombres ficticios, medias estimadas 55–74, 22 jugadores con dorsal y edad) y camiseta, estadio y capacidad aproximados. Se crean con `Tools/DataGen/vintage_clubs.py` (determinista, idempotente). Son editables en `argentina.json`.
  Para que los jugadores del Nacional sean reales hay que cargar los planteles a mano (mismo formato que los 30 de Primera); el juego los toma sin cambiar código.
- **Tabla de posiciones de la Central**: ahora muestra PTS, PJ, PG, PE, PP, GF, GC y DG, con la sigla del club para que entren las diez columnas (los promedios muestran PROM).
- Sin verificar en Unity: botón de modo en la elección de club (se movieron título, filtro y cantidad), legibilidad de la tabla con diez columnas y la zona del Nacional en el calendario. Núcleo probado con `dotnet test` (`VintageModeTests`).

## D-40 · El saque de banda sale de las manos — Aceptada (video del usuario, 23/11/2026)
- Con el saque de banda, el sacador levantaba las dos manos pero el balón salía rodando desde los pies. Ahora el balón sale desde `ThrowInReleaseHeight` (2 m, sobre la cabeza) con un arco de `ThrowInLoftDeg` (28°) (`KickPass` detecta el saque de banda); el sacador ya mostraba la acción `ThrowIn`, verificado con prueba.
- Las animaciones de `ThirdParty/Quaternius_UniversalAnimationLibrary` (D-38/ASSETS_LICENSES) **todavía no están integradas**: por eso el movimiento no cambió. Pendiente: herramienta de conversión de clips al esqueleto de 20 huesos y reproducción en `SkinnedBodyGeometry`.
- Pendiente de detallar con el usuario: «movilidad de los arqueros muy mala» (¿desplazamiento, zambullidas o animación?).

## D-41 · Ofertas con motivo, nivel igual en carrera y amistosos, dorsales y renovaciones negociadas — Aceptada (pedido del usuario, 23/11/2026)
- **Ofertas rechazadas**: el pie de la Central mostraba la **caja**, no el presupuesto de fichajes (el límite real de las ofertas); ahora muestra los dos (`FICHAJES`) y la oferta dice el motivo exacto: libro cerrado, sin presupuesto (con las cifras) o rechazada por el club (`CareerSession.CheckOffer`, `OfferBlock`).
- **Nivel de los jugadores**: en carrera la «media» salía unos 10 puntos más alta que en los amistosos (77 contra 67 de promedio) porque se mostraba el promedio de los atributos propios del puesto, que están por encima de la media por construcción (`RosterFactory.FillAttributes`). **Los partidos no cambian** (los atributos son los mismos). Lo que se muestra ahora es `RosterFactory.DisplayRating` (descuenta ese sesgo) en la plantilla, mercado, alineación, ficha y presentación: carrera y amistosos muestran el mismo número, el del archivo de equipos. Valor de mercado y sueldos siguen calculándose con los atributos (la economía no se recalibra).
- **Córner**: un toque en la pantalla salta la espera del sacador (`MatchInput.SkipWait`); el sacador aparece en el balón.
- **Dorsales**: cada jugador lleva su dorsal (`Player.ShirtNumber`, del archivo si lo trae; sin repetir en el club). En Plantilla se marca un jugador y `+`/`-` cambian el dorsal (si otro lo lleva, intercambian). El partido usa el dorsal del jugador. Guardado: archivo v6 y migración 13.
- **Nombres**: nombre de pila = primera palabra, apellido = el resto (antes «Lautaro Di» / «Lollo»); la plantilla y el mercado muestran el nombre completo.
- **Edades**: la carrera ya respeta la edad del archivo; **351 de 671 jugadores del archivo no tienen edad** y reciben una al azar (19–33). Falta cargar las reales en `argentina.json` (campo `Age`): no puedo inventarlas con seguridad.
- **Renovaciones negociadas** (reemplazan a la automática): cada jugador pide sueldo (más si es ambicioso, menos si es leal), años mínimos (más si es joven) y, si es de los mejores y juega poco, que se le prometa ser titular. El usuario ofrece sueldo, años y promesa; el jugador **se toma de 3 a 14 días** para contestar (más los ambiciosos) y acepta, contraoferta (hasta 3 rondas) o se niega y se va libre; con la moral baja se niega en el acto. Parámetros en `career.json`. Guardado: archivo v7 y migración 14.
- Sin verificar en Unity: pantalla de contratos rediseñada, botones de dorsal y toque para saltar la espera del córner.

## D-42 · Menú principal en carrusel, tema «Noche + verde cancha» — Aceptada (diseño aportado por el usuario, 23/11/2026)
- El usuario subió un diseño del menú (`Menu_Principal.dc.html`, con tres temas y dos disposiciones). La miniatura que acompañaba muestra la combinación **carrusel + «Noche + verde cancha» (2b)**, y esa es la que se aplicó: tarjetas de modo que se deslizan (la elegida grande con su descripción, las demás chicas), puntos de posición abajo a la izquierda, botón de acción abajo a la derecha, líneas de cancha tenues de fondo y los tres clubes de la portada como etiquetas arriba a la derecha («TU CLUB»). Tocar una tarjeta la elige, tocar la elegida o el botón la abre, deslizar pasa a la siguiente (`MainMenuScreen`).
- **Qué no se copió**: la tipografía Barlow (el proyecto no la tiene; se usa la fuente del juego), los íconos de las tarjetas (eran SVG; Unity no los lee) y el engranaje de ajustes (Ajustes ya es una tarjeta). El fondo es plano, así que **las figuras 3D de la portada quedan tapadas** en este menú (siguen en el código; se vuelven a ver bajando la opacidad de `FondoNoche`).
- Solo cambia el menú principal: el resto de las pantallas conserva su tema. Sin verificar en Unity.

## D-43 · Tema «Noche + verde cancha» en carrera, pausa y partido — Aceptada (diseño aportado por el usuario, 23/11/2026)
- Se subió `Pantallas.dc.html` (3a alineación, 3b HUD, 3c pausa, 3d plantel en partido, 3e–3i carrera) y `Career_Bar.dc.html`. **Aplicado**: (1) la paleta de `UiTheme` (verde césped sobre verde casi negro, texto claro, líneas y acento claro; nuevos `OnAccent`, `Line`, `AccentStrong`, `Danger`) que usan todas las pantallas del juego: el tema cambia en toda la carrera, las pestañas y el resto de los menús; (2) la barra de la carrera: GUARDAR relleno con texto oscuro, SALIR con borde, pestaña activa resaltada; (3) la **pausa** (3c): acciones a la izquierda (REANUDAR relleno, AJUSTES RÁPIDOS y SALIR AL MENÚ con borde, este en rojo), panel TÁCTICA a la derecha con selectores segmentados (mentalidad de 5 opciones y presión de 3, se aplican con un toque) y la charla del entretiempo al centro; (4) el **HUD** (3b): colores de los botones táctiles con la paleta (TIRO y PASE destacados, el resto neutros; despeje y barrida en rojo).
- **No aplicado todavía** (cambia la disposición, no solo los colores): alineación 3a, plantel y táctica en partido 3d, y las pantallas de carrera 3e–3i (Central, Plantilla, Mercado, Oficina, Finanzas). Ya toman la paleta nueva, pero conservan su disposición. El HUD no cambió de forma (botones circulares) ni de posición.
- Sin verificar en Unity.

## D-44 · Menú ordenado y estilo «plano técnico» en todas las pantallas — Aceptada (capturas del usuario, 23/11/2026)
- **Menú principal**: el fondo ahora cubre toda la pantalla (antes dejaba franjas a los costados), márgenes uniformes de 90 unidades, se quitaron las líneas de áreas que se cruzaban con las tarjetas y el texto de las tarjetas ya no se superpone: en la tarjeta grande el título va arriba y la descripción debajo; en las chicas, el título abajo.
- **Estilo de las pantallas** (de `Pantallas.dc.html`): `UiTheme.Frame` dibuja los paneles con borde fino y una marca verde en cada esquina (se usa en todos los paneles de la carrera), las tablas llevan separadores finos y encabezados en verde claro, el fondo de todos los menús lleva las líneas de la cancha tenues (marco, línea media y círculo) y el marcador del partido lleva el reloj verde con texto oscuro.
- **Sigue sin aplicarse** la reorganización de las pantallas de carrera (la disposición actual ya es parecida a la de los diseños, pero no idéntica) ni la forma del HUD. Pendiente de revisar con capturas del usuario pantalla por pantalla. Sin verificar en Unity.

## D-45 · Manual de diseño POTRERO V1 aplicado al resto del juego — Aceptada (PDF del usuario, 23/11/2026)
- El PDF «Manual de diseño» fija: plano técnico (líneas finas, esquinas rectas, marcas «+» en las esquinas), **un solo verde** de acento, pulgares primero, botón primario relleno abajo a la derecha y los secundarios solo con contorno, selectores segmentados de 2 a 5 opciones, etiquetas de prioridad solo con contorno (crítica roja, alta/media ámbar, baja neutra), tarjeta de modo elegida con sombra sólida, barra de pestañas de la carrera de 36–40 px (en el lienzo de 844 px; aquí ≈ 84 unidades), nada de texto menor a 10 px y un solo color de estado por elemento.
- **Aplicado**: marcas «+» en las esquinas de todos los paneles (`UiTheme.Frame`, ahora también en la elección de equipos y el partido amistoso); **todos los botones secundarios con contorno fino** (`UiKit.Button`); etiquetas de prioridad con contorno en la elección de club (con ámbar y rojo del manual); barra de pestañas de la carrera más alta (84 unidades) con GUARDAR/SALIR a juego; sombra sólida de la tarjeta elegida en el menú principal.
- Las pantallas «Creá tu entrenador» (3l) y «Elegí tu club» (3m) ya tenían la misma disposición que los diseños (formulario a la izquierda y vista previa a la derecha; tres columnas con selector de modo, cantidad y filtro arriba); con esto toman también el estilo. **Pendiente**: la tipografía Barlow Condensed (no está en el proyecto: habría que agregar la fuente con licencia abierta) y los íconos de línea. Sin verificar en Unity.

## D-46 · Menús de la carrera contra `Pantallas.dc.html` — Aceptada (pedido del usuario, 23/11/2026)
- Se comparó cada pantalla de carrera con su diseño (3e Central, 3f Plantilla, 3g Mercado, 3h Oficina, 3i Finanzas): **la disposición ya coincide** (columnas, paneles, tabla, tira de calendario, mosaicos y detalle, filtros y acciones); lo que no coincidía eran los estados y los colores. Cambios: los botones y mosaicos **elegidos** muestran el contorno claro (verde 900) sobre fondo verde 100 (`OutlineSkin`), la tira de calendario resalta el día de hoy y marca con borde verde los días con partido (`UiTheme.FrameEdge`), el pedido de los socios es un panel con contorno ámbar (antes marrón relleno), el cartel de campeón usa el panel del diseño, y los **colores de estado** son los del manual (verde de acento, ámbar y rojo `#e0685a`: contratos por vencer, barras de objetivos, prioridades); las barras usan la pista gris del manual.
- No se rehízo la disposición ni se movieron elementos: sin capturas de cómo se ve hoy en Unity, mover coordenadas a ciegas arriesga dejar pantallas rotas. Si alguna pantalla no se parece a su diseño, se corrige con una captura. Sin verificar en Unity.

## D-47 · Nombres completos en las tablas y fondo opaco en los menús — Aceptada (captura del usuario, 23/11/2026)
- La tabla de posiciones de la Central muestra el **nombre completo** del club (se recorta con «…» solo si no entra, p. ej. «Central Córdoba (Santiago del Es…»): para eso la Central se reorganizó con tres columnas de otro ancho (izquierda 500, partido 560, tabla 760 unidades). Lo mismo en los cruces de la copa, el calendario y el mercado.
- El fondo de todos los menús (`UiTheme.Background`) pasó a ser **opaco** (antes 82 % y dejaba ver las figuras 3D de la portada detrás de la carrera). Sin verificar en Unity.

## D-48 · Alineación unificada y jugadores libres iniciales — Aceptada (pedido del usuario, 23/11/2026)
- **Una sola vista de alineación** (`LineupBoard`, la de la foto de la pantalla «Alineación» del amistoso): cancha verde con los once como fichas (media dentro, puesto arriba, apellido abajo), dibujo con flechas, media del equipo, y columnas de suplentes y no convocados. La usan: la alineación del amistoso, **la del modo carrera** (antes una cancha vertical con colores por línea; ahora fichas del color de la camiseta del club, con flechas de dibujo y paginado de no convocados) y la **presentación previa al partido** (antes una pizarra 3D con la cámara sobre el campo; ahora la misma pantalla para el equipo propio y luego el rival, con SALTAR). El panel **PLANTEL Y TÁCTICA** del partido conserva sus tres columnas (como el diseño 3d) y pasa a pintar las fichas con la camiseta y la media, y a usar la paleta común.
- **Jugadores libres**: `argentina.json` tiene la clave `FreeAgents` (16 jugadores de la lista del usuario: nombre, puesto y edad reales de su captura; **la media es una estimación mía** a partir del valor de mercado y la edad). Se crean al empezar la carrera sin club ni contrato y se firman desde el mercado (SOLO LIBRES) sin transferencia. Para que los de 34 a 36 años no figuren como retirados, `RetirementAge` pasó de 34 a 37. Se pueden agregar más jugadores en esa clave con el mismo formato.
- Sin verificar en Unity.

## D-49 · Nacionalidad, cupo de extranjeros, más libres, orden/filtro de plantilla y contratos a trueque — Aceptada (pedido del usuario, 23/11/2026)
- **Nacionalidad** (`Player.Nationality`, `PlayerDefinition.Nationality`; archivo v8 y migración 15). La de los libres sale de las capturas del usuario; la de los clubes **la completé de memoria** para unos 40 extranjeros conocidos (Boca arranca con el cupo completo: Montero, Villa, Romero, Merentiel, Valencia, Bareiro) y **el resto figura como argentino: puede tener errores, se corrige en `argentina.json`**.
- **Libres**: ahora 86 (16 + 70 de las cinco capturas: argentinos, uruguayos, paraguayos, colombianos y chilenos), con la media **estimada** por valor de mercado y edad (`Tools/DataGen/free_agents.py`). Los que figuraban también en un plantel (p. ej. Vietto en San Lorenzo, Facundo González en River) se quitaron del plantel: manda la lista. `FreeAgentMaxAge` = 40 para que Cavani y Jara figuren.
- **Cupo de extranjeros** (reglamento de la Liga Profesional): máximo **6 con contrato** por club y **5 en la planilla** de cada partido. Fichar (oferta, cláusula o libre) con el cupo completo muestra un **cartel de alerta** y no se concreta; vendiendo o liberando a uno se puede. La alineación del partido nunca lleva más de 5 extranjeros (la automática reemplaza al de menor media; una elegida a mano con más no se guarda). **No implementado**: la regla de «2 con 10 partidos en su selección» (no hay datos de partidos internacionales), los nacionalizados (campo no cargado) ni los préstamos al exterior. Los clubes de la computadora respetan el cupo al completar plantel.
- **Plantilla**: columna NAC (extranjeros resaltados), **orden por cualquier columna** tocando su encabezado (ascendente, descendente, sin orden) y filtros por puesto (arqueros, defensores, medios, delanteros) y por nacionalidad (argentinos, extranjeros con su cupo, o una nacionalidad).
- **Contratos**: se **ofrece primero**; el jugador no muestra sus pretensiones hasta responder con una contraoferta. La decisión es un **trueque**: pesa sueldo, años y rol (figura, titular, rotación, suplente) según su personalidad (los ambiciosos valoran más el rol, los leales menos el sueldo), así que aceptar el mismo sueldo con menos años, o menos sueldo con un rol más importante, puede ser suficiente; pagar de más suma hasta un tope; si reclama minutos, el rol pesa el doble. Se puede aceptar lo que pide con un botón (se firma al instante) u ofrecer más o menos. Parámetros en `career.json`. Archivo v9 y migración 16.
- Sin verificar en Unity.

## D-50 · Minimapa con cancha, giros de la IA, salida al vestuario y medición de asignaciones — Aceptada (pedido del usuario, 24/11/2026)
- **Minimapa**: dibuja el contorno de la cancha (bordes, línea media, círculo central y las dos áreas, con proporciones reglamentarias). Se quitó «TU CLUB» del menú principal.
- **Giros de la IA al recibir el balón**: medido en simulaciones sin pantalla (`OwnerTurningTests`), el poseedor giraba de media **179° de más** en los primeros 1,5 s y **uno de cada cinco daba más de una vuelta** (decidía un rumbo nuevo para conducir o proteger el balón en cada decisión, con ruido, y cambiaba de lado). Ahora el rumbo elegido se sostiene `OwnerCommitDecisions` = 2 decisiones (un pase o un tiro sí lo interrumpen; `TurnPenalty`, un castigo por girar, queda en 0 porque con la persistencia alcanza y empeoraba la presión del jugador): el giro de más baja a la mitad o menos y no hay vueltas enteras. Balance en 300 partidos sin pantalla: 2,47 goles por partido y 84,8 % de pase (dentro de la banda). Se aflojaron tres pruebas sensibles a la semilla (robar el balón presionando: más de la mitad en vez del 55 %; el cierre del partido tras el tiempo agregado y el arquero que vuelve al arco en cancha cerrada) porque la IA decide distinto.
- **Fin del partido**: los jugadores dejan de correr, caminan a la banda de los bancos, los dos equipos se saludan en dos filas enfrentadas (9 s) y después entran al **túnel de vestuarios** (en el medio de la banda de la cámara, con hueco en los carteles LED) de a uno, escalonados y a ritmos distintos (primero el local, los arqueros más lentos); un toque lo salta y aparece el resumen. Es solo visual: el motor ya había terminado. No aplica a la prueba de rendimiento.
- **Asignaciones (prueba de rendimiento: 587 KB/s)**: con `AllocationTests`, el motor y la copia de la instantánea asignan **menos de 64 B por paso (en la práctica 0)**, así que la basura viene de la capa Unity. Revisé las rutas calientes (etiquetas, radar, HUD, marcador) y no encontré asignaciones por fotograma. La cifra del panel era una diferencia de la memoria de Mono, que **en el editor incluye lo que asigna el propio editor**; ahora se mide con el contador «GC Allocated In Frame» de Unity (exacto por fotograma). Para ubicar el resto hace falta el Profiler de Unity (módulo de memoria, filtrando `GC.Alloc`) o medir en el celular.
