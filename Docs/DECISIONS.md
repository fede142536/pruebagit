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
