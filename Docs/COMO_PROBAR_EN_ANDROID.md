# Cómo probar el juego en tu PC y en tu Galaxy A13

> **Aviso importante.** La capa de Unity (pantallas, cámara, controles táctiles) está escrita pero **todavía no se ha ejecutado
> dentro de Unity**: yo no puedo abrir Unity en mi entorno. El motor del partido sí está probado con 146 pruebas automáticas.
> Es probable que en tu primera prueba aparezca algún error o algo se vea raro. Es normal: copia el mensaje o haz una captura y
> mándamelo, y lo corrijo.

Tiempo estimado: 1 hora la primera vez (casi todo es esperar descargas). Necesitas Windows, ~25 GB libres y conexión a internet.

## 1. Instalar Unity (gratis)

1. Entra en <https://unity.com/download> y descarga **Unity Hub**. Instálalo y crea una cuenta gratuita.
2. En Unity Hub: **Installs → Install Editor → Unity 6 LTS** (versión 6000.x).
3. En la lista de módulos marca **Android Build Support** y, dentro de él, **OpenJDK** y **Android SDK & NDK Tools**.
4. Espera a que termine (varios GB).

## 2. Descargar los archivos del juego

1. Abre <https://github.com/fede142536/pruebagit>.
2. Cambia la rama a **`ccr-8f161947-ai8n46`** (desplegable arriba a la izquierda).
3. **Code → Download ZIP** y extráelo. Dentro hay una carpeta `Game/Assets/` con dos carpetas: `Football` y `Plugins`.

## 3. Crear el proyecto y copiar el juego

1. En Unity Hub: **Projects → New project**, plantilla **Universal 3D** (si no aparece, descárgala desde la propia lista;
   también vale *3D (Built-in Render Pipeline)*). Nómbralo `FootballGame` y pulsa **Create project**. La primera vez tarda varios minutos.
2. Cuando Unity abra, cierra nada: simplemente copia desde el Explorador de Windows las carpetas **`Football`** y **`Plugins`** (del ZIP,
   dentro de `Game/Assets/`) dentro de la carpeta **`Assets`** de tu proyecto (en Unity Hub, el icono de carpeta junto al proyecto la abre).
3. Vuelve a Unity y espera a que termine "Compiling…" (abajo a la derecha). En la pestaña **Console** no debe haber mensajes rojos.

## 4. Probar en el ordenador

1. Pulsa el botón **▶ Play**. Aparece el menú: elige **rival**, **dificultad** y **duración** y pulsa **JUGAR**.
2. Controles de teclado: **WASD** mover · **J** pase · **K** tiro (mantén para cargar potencia y suelta) · **L** pase filtrado ·
   **I** centro · **E** presionar (sin balón) · **Tab** cambiar de jugador · **Shift** esprint · **Esc** pausa.
3. Consejo: en la pestaña *Game* elige una proporción 16:9 para ver la pantalla como en el móvil.

## 5. Generar la aplicación para el móvil

1. **File → Build Profiles** (o *Build Settings*), elige **Android** y pulsa **Switch Platform**. Espera.
2. En el menú superior elige **Football → Construir APK para Android**.
3. Al terminar se abre la carpeta con **`FootballGame.apk`** (está en `Builds/`). Si Unity pide aceptar licencias del SDK de Android, acepta.

## 6. Instalarla en el Galaxy A13

1. Pasa el `.apk` al móvil (cable USB, Google Drive, enviártelo por correo…).
2. Ábrelo desde el móvil. Android pedirá permiso para **instalar apps de fuentes desconocidas** para esa aplicación: permítelo.
3. Abre **Football Game** (se juega en horizontal).

## 7. Medir el rendimiento (lo que necesito de ti)

1. En el menú toca **PRUEBA DE RENDIMIENTO**: juegan solos durante ~5 minutos con una semilla fija (mismas condiciones siempre).
   Al terminar muestra FPS medios, tiempo de fotograma medio y p99, recolecciones de basura y el modelo del móvil. **Mándame una captura.**
2. En un partido normal: **Pausa (II) → PANEL DE DEPURACIÓN: SÍ**. Verás FPS, latencia estimada, memoria y asignaciones.

Metas en el A13 (gama baja): **30 FPS estables** (p99 ≤ 40 ms), recolecciones de basura ≈ 0 durante el partido y memoria ≤ 1,5 GB.
La latencia mostrada es una **estimación** (proceso interno + un fotograma + refresco de pantalla): a 30 FPS puede quedar cerca o por
encima de los 100 ms objetivo; si es así probaremos 60 FPS o ajustes.

## 8. Cuéntame cómo se siente (10 preguntas)

1. ¿El pase va hacia donde apuntas con el joystick?
2. ¿El jugador al que controlas es el que esperas cuando el rival tiene el balón?
3. ¿El esprint se siente pesado, ligero o bien?
4. ¿El joystick responde bien o se siente con retraso?
5. ¿Los botones están en un lugar cómodo para tu mano derecha? ¿Alguno es demasiado pequeño o grande?
6. ¿Se entiende qué pasa en el campo con esta cámara? ¿Prefieres más cerca o más lejos?
7. ¿La potencia del tiro (mantener y soltar) es intuitiva?
8. ¿Los rivales juegan de forma distinta entre sí? ¿Cuál se siente más difícil?
9. ¿Alguna jugada rara (jugadores quietos, balón atascado, goles raros)?
10. ¿Cómo va de fluidez en tu móvil? ¿Se calienta o se traba?

## Si algo falla

| Síntoma | Qué hacer |
|---|---|
| Objetos rosados o negros | Menú **Football → Preparar proyecto** y vuelve a probar |
| Error rojo sobre *Input* (`InvalidOperationException`) | **Edit → Project Settings → Player → Other Settings → Active Input Handling → Both** y reinicia Unity |
| Errores rojos al compilar | Copia el texto de la pestaña Console y mándamelo |
| No aparece el menú "Football" | Espera a que termine de compilar; si persiste, revisa que copiaste `Football` dentro de `Assets` |
| El APK no se genera | Comprueba que el módulo Android (con OpenJDK y SDK/NDK) está instalado y que cambiaste la plataforma a Android |
