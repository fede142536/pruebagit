# GDD — Documento de diseño del juego

## 1. Visión

Un juego de fútbol móvil para Android e iOS que une dos mitades en un solo producto coherente:

- **Gestión:** dirigir un club (ficticio por defecto, real mediante base de datos editable) con finanzas, plantilla, cantera, fichajes, estadio, socios y directiva.
- **Partido jugable:** partidos 3D con controles táctiles, física creíble y varios niveles de dificultad.

**Pilares**

1. **El partido manda en la sensación; la gestión manda en la profundidad.** Lo que decides en el club cambia el partido, y el partido cambia el club.
2. **Honestidad de la IA.** La dificultad sube por mejor decisión y ejecución, nunca por trampas ocultas.
3. **Offline-first y personal.** Sin monetización, anuncios ni microtransacciones.
4. **Datos antes que código.** Equipos, balance y reglas viven en datos editables.
5. **Arte intercambiable.** El juego es completo con placeholders; el arte definitivo se enchufa después.

**Fuera de alcance:** multijugador online, monetización, licencias oficiales, selecciones nacionales (opcional tras la Fase 8).

## 2. Modos de interacción con un partido

| Modo | Qué hace | Motor |
|---|---|---|
| **Jugar** | El usuario controla a un jugador con controles táctiles | Motor en tiempo real (3D) |
| **Ver** | El partido se simula en 3D sin control del usuario | Motor en tiempo real (3D) |
| **Simular** | Resultado rápido sin gráficos | Motor estadístico calibrado contra el de tiempo real |

Los tres usan el mismo modelo de jugadores y la misma táctica. Requisito medible: las distribuciones de resultados de los tres modos son estadísticamente comparables (ver `BALANCE.md`).

## 3. Modo Carrera

### 3.1 Club
Elegir club de la base de datos o crear uno (nombre, escudo, colores, equipaciones, estadio, país/liga, presupuesto inicial, dificultad económica).

### 3.2 Sistemas

| Sistema | Alcance | Fase |
|---|---|---|
| Plantilla y jugadores | Atributos técnicos/físicos/mentales/portero, potencial oculto, posiciones y roles, rasgos, forma, moral, fatiga, lesiones, contratos, cesiones, capitanía | 2–3 |
| Entrenamiento | Planes individuales y colectivos, evolución y declive por edad | 2 |
| Finanzas | Ingresos (entradas, socios, TV, patrocinios, merchandising, premios, traspasos, estadio), gastos (salarios, primas, staff, mantenimiento, intereses, obras, cantera), presupuestos separados de fichajes y salarios, deuda, préstamos, fair play financiero configurable | 2 |
| Calendario y ligas | Calendarios, tablas, desempates, congestión de partidos | 2, 5 |
| Fichajes | Ventanas, ojeo, negociación con IA (oferta, contraoferta, agentes, cláusulas), rumores | 4 |
| Cantera | Generación de promesas, ojeadores, ascensos | 4 |
| Estadio e inmediaciones | Ampliación por fases, césped, accesos, tiendas, hostelería, ciudad deportiva, impacto en afluencia e ingresos | 4 |
| Socios y afición | Abonos, cuotas, lista de espera, fidelidad, peñas, satisfacción, precio de entradas | 4 |
| Directiva y entorno | Objetivos de temporada, confianza, riesgo de despido, prensa, patrocinadores | 4 |
| Cuerpo técnico | Entrenadores, preparadores, médicos, ojeadores, analistas con efecto real | 4 |
| Competiciones | Ligas, copas nacionales, competiciones internacionales de clubes (liguilla + eliminatorias) | 5 |

### 3.3 Reglas de integración gestión ↔ partido
- La **forma, moral, fatiga, lesiones y sanciones** de cada jugador entran al partido como modificadores de sus atributos efectivos.
- El **estadio** aporta ventaja de local (ambiente) proporcional a capacidad llena y afición.
- El **resultado y los eventos** del partido (goles, tarjetas, lesiones, minutos) actualizan carrera, finanzas (premios, afluencia) y moral.

## 4. Partido 3D

### 4.1 Realismo
- **Balón:** gravedad, arrastre, rebote, rodadura con fricción, efecto (Magnus simplificado). Tipos: pase raso, filtrado, centro, disparo colocado/potente/con efecto, vaselina, despeje.
- **Jugadores:** aceleración/desaceleración reales, giros con inercia, velocidad según atributos y fatiga, mezcla de animaciones.
- **Reglas:** fuera de juego, faltas, tarjetas, penaltis, saques de banda/esquina/puerta, ventaja, prolongación, lesiones, sustituciones. VAR opcional (Fase 7).
- **Resultado emergente:** el marcador sale del juego, no de un número predefinido.

### 4.2 IA
- Posicionamiento por formación y rol (zonas dinámicas, líneas en bloque, presión, repliegue, desmarques, amplitud, profundidad).
- Utility AI / árbol de comportamiento: pasar, conducir, regatear, tirar, presionar, cubrir, interceptar.
- Portero con posicionamiento, salidas, paradas y reflejos según atributos.
- Instrucciones tácticas del entrenador (mentalidad, línea defensiva, presión, ritmo, anchura, instrucciones individuales).
- Decisiones y errores dependen de atributos (visión, técnica, compostura…), no de aleatoriedad uniforme.

### 4.3 Dificultad

Niveles: Principiante, Amateur, Profesional, Mundial, Leyenda. Parámetros por nivel (valores concretos en `BALANCE.md`):

| Parámetro | Efecto |
|---|---|
| Tiempo de reacción de la IA | Retardo entre cambio de situación y respuesta |
| Precisión de pases/tiros | Dispersión de la ejecución |
| Agresividad de presión | Radio y umbral de activación |
| Calidad de decisión | Nitidez de la función de utilidad |
| Tolerancia al error del jugador | Margen de error del input del usuario |
| Asistencias de control | Auto-apuntado de pase, auxilio de cambio de jugador |

**Prohibido:** rubber banding oculto o ventajas físicas ocultas. La **dificultad adaptativa** es un ajuste separado, visible y desactivable.

### 4.4 Cámara y presentación
Cámara de transmisión por defecto; alternativas táctica, lateral y detrás del jugador. Repeticiones, presentación pre/post partido, marcador y estadísticas en vivo. Duración configurable (3/5/8/10 min por tiempo, escalada a 90').

### 4.5 Controles táctiles
Joystick virtual flotante o fijo (tamaño, posición, zurdos). Botones contextuales: pase corto, pase filtrado, centro, disparo, presión/entrada, cambio de jugador, esprint, regate/finta. Gestos opcionales (deslizar para rematar/centrar, tocar compañero para pasar). Vibración háptica, vista previa de dirección de pase, asistencias graduables.

## 5. UX

- Gestión en **vertical** (una mano, navegación inferior); partido en **horizontal**.
- Datos densos pero escaneables: tablas ordenables, filtros, comparadores, gráficos de tendencia.
- Accesibilidad: tamaños de fuente, contraste, modo daltónico, subtítulos.
- Localización es/en desde el inicio mediante archivos de recursos.
- Onboarding corto y tutoriales contextuales.

## 6. Criterios de calidad ("AAA" medible)

| Área | Criterio |
|---|---|
| Jugabilidad | Latencia toque→acción < 100 ms; balón consistente; animaciones sin saltos; IA sin trampas visibles |
| Rendimiento | 60 FPS en gama media-alta; 30 FPS en gama baja; sin tirones de GC en partido; RAM ≤ 1,5 GB en gama baja |
| Simulación | La gestión influye en el partido y viceversa |
| Robustez | Guardado con migraciones; simulación determinista con semilla; temporada completa sin crashes; suite de pruebas automáticas |
| Pulido | UI coherente, transiciones fluidas, háptica y sonido, accesibilidad, es/en |

## 7. Contenido y licencias

- Base de datos ficticia por defecto (clubes, jugadores, ligas y países inventados).
- Importador/editor JSON/CSV para datos reales cargados por el usuario.
- Arte, audio y VFX: placeholders procedurales o CC0 hasta el arte definitivo; todo registrado en `ASSETS_LICENSES.md`.
