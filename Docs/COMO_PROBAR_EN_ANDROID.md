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

## Actualizar a una versión nueva

> **No copies las carpetas encima de las viejas:** Windows mezcla los archivos nuevos con los viejos y Unity da decenas de
> errores ("already defines a member…"). Usa el actualizador, que además borra lo que sobra.

Cada vez que te avise de cambios:
1. **Cierra Unity.**
2. Descarga el ZIP de la rama `ccr-8f161947-ai8n46` y extráelo en una carpeta **nueva** (si el navegador lo guarda como
   `… (1).zip`, usa ese: es el más reciente).
3. Dentro de la carpeta extraída, haz doble clic en **`ACTUALIZAR_JUEGO.bat`**. Pulsa Enter si tu proyecto está en
   `C:\Users\<tu usuario>\Juego de futbol`, o escribe la ruta correcta.
4. Abre Unity y espera a que termine de compilar (si pregunta por la Library, **Rebuild Library**).

Si Windows muestra "Windows protegió tu PC", pulsa **Más información → Ejecutar de todas formas** (es un archivo de texto que
solo copia las carpetas del juego; puedes abrirlo con el Bloc de notas para verlo).

## 4. Probar en el ordenador

1. Pulsa el botón **▶ Play**. Aparece el menú: elige **rival**, **dificultad** y **duración** y pulsa **JUGAR**.
   **Haz un clic dentro de la ventana Game** para que reciba el teclado.
   Tu jugador es el que tiene la **flecha amarilla** encima. Los demás de tu equipo (azul) los mueve la máquina; cuando el rival
   tiene el balón, la flecha pasa sola al jugador más cercano (o pulsa **Tab**).
2. **Controles** (los botones cambian según tengas o no el balón):

   | Situación | En el teléfono | En el ordenador |
   |---|---|---|
   | Mover | arrastrar en la mitad izquierda | WASD o flechas |
   | Correr más | ESPRINT (mantener) | Shift |
   | Con balón | PASE (toque: automático · mantener: más potencia) · TIRO (mantener y soltar) · DESPEJAR (en tu campo) | J · K · R |
   | Opcionales | FILTRADO · CENTRO (se activan en Ajustes) | L · I |
   | Sin balón | PRESIONAR (mantener: quita sin barrer) · BARRER · CAMBIAR | E · Q · Tab |
   | Táctica | En la pausa: MENTALIDAD (muy defensiva … muy ofensiva) y PRESIÓN (baja, normal, alta) | Esc |

   **Cómo defender y regatear ahora.** Quitar el balón depende de *dónde* estás: de frente al rival y mirándolo tienes más
   opciones; por la espalda casi nunca lo ganas y es falta fácil. Al mantener PRESIONAR estando detrás del rival, tu jugador
   primero lo rodea para ponerse entre él y tu arco, y recién ahí entra. Con balón, un **giro brusco con el joystick justo antes de
   que el defensor entre** (un recorte) lo deja atrás durante un instante; funciona mejor con buenos regateadores. La IA rival
   también contiene, cubre al compañero que presiona y, si va perdiendo desde el minuto 60, se vuelca al ataque (lo verás anunciado).
   | Arquero | ARQUERO (tomar el control para salir, barrer, pasar o despejar); JUGADOR lo devuelve | G |
   | Pausa | II | Esc |

   **Teclado del ordenador:** las teclas se cambian en **Ajustes → Controles de teclado (ordenador)**: toca una acción y
   pulsa la tecla nueva (si otra acción la usaba, se intercambian). En Unity, haz clic una vez dentro de la ventana *Game*
   para que reciba el teclado. La guía de controles con tus teclas está en el menú de pausa.

   **Durante el partido:** abajo al centro hay un radar (solo en 11 contra 11); cuando tu equipo tiene la pelota, los
   compañeros que quedan fuera de cámara aparecen como fichas con su dorsal en el borde. Bajo tu jugador se ve la barra de
   potencia al mantener PASE o TIRO (verde → amarillo → rojo) y la de resistencia cuando se cansa. El aro tenue marca a quién
   pasarías con CAMBIAR. Tras un gol hay una repetición en cámara lenta (toca para saltarla). En Ajustes se pueden apagar el
   sonido y la vibración.

   **Saques (laterales, de arco, córners y tiros libres de tu equipo):** el juego espera a que el ejecutante llegue a la
   pelota y a que el resto se acomode; después **apuntas con el joystick** y tocas PASE (mantén para más fuerza). En córners y
   tiros libres aparece CENTRO, y en tiros libres también TIRO. Si no haces nada en 8 s, saca la máquina.

   **Mando físico** (Xbox, PlayStation o Bluetooth genérico; requiere el sistema de entrada nuevo): stick izquierdo mover,
   A/✕ pase (mantener = potencia), B/○ tiro (mantener) o barrida sin balón, X/□ filtrado, Y/△ centro, RB esprint, LB cambiar,
   RT presionar, LT despejar, Select arquero, Start pausa.

   **Calidad gráfica** (Ajustes): BAJA (70 % de resolución, sin suavizado; recomendada para el A13), MEDIA o ALTA.

   **3 contra 3:** la alineación se elige igual que en el amistoso (Equipos → editar alineación), pero con tres titulares y
   tres suplentes, y se guarda aparte de la de 11.

   En **Ajustes** eliges la **velocidad del juego** (lenta, normal o rápida), si quieres los botones de filtrado y centro y si
   prefieres botones grandes. Ahí mismo hay una guía de controles.
3. Consejo: en la pestaña *Game* elige una proporción 16:9 para ver la pantalla como en el móvil.

### Menús, equipos y alineación

1. **Menú principal:** toca **PARTIDO AMISTOSO** (Modo carrera y Torneo aparecen como «próximamente»). En **AJUSTES** están los
   controles para zurdos, los nombres sobre los jugadores, los FPS y el panel de depuración.
2. **Equipos:** una tarjeta para **TU EQUIPO** y otra para el **RIVAL**. Con **<** y **>** cambias de club y de camiseta
   (titular o alternativa). Cada tarjeta muestra estrellas, media, estilo, formación y la figura del equipo.
3. **EDITAR ALINEACIÓN** (en cada tarjeta): eliges la formación (4-4-2, 4-3-3, 4-2-3-1 o 3-5-2) con **<** y **>**, y para
   cambiar jugadores tocas uno y luego otro: titulares en la cancha, **suplentes** y **no convocados** a la derecha. Si alguien
   juega fuera de su posición, su media aparece en naranja y más baja. **AUTOMÁTICA** vuelve al mejor once. La alineación queda
   guardada para la próxima vez.
4. **CONTINUAR** → ajustes del partido (dificultad, minutos, nombres) → **JUGAR**.

Los planteles están en `Data/teams/argentina.json` (en Unity, `Assets/Football/Resources/teams.json`); puedes abrirlo con el Bloc
de notas para corregir jugadores, posiciones (POR, DFC, LI, LD, MCD, MC, MCO, MI, MD, EI, ED, DC) o medias. **Las medias son
estimaciones mías**, no oficiales.

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
8. ¿Los rivales juegan de forma distinta entre sí? ¿Cuál se siente más difícil? ¿Notas el cambio al tocar MENTALIDAD o PRESIÓN?
   ¿El recorte deja atrás al defensor? ¿Presionar se siente útil o lento?
9. ¿Alguna jugada rara (jugadores quietos, balón atascado, goles raros)?
10. ¿Cómo va de fluidez en tu móvil? ¿Se calienta o se traba?

## Si algo falla

| Síntoma | Qué hacer |
|---|---|
| Objetos rosados o negros | Menú **Football → Preparar proyecto** y vuelve a probar |
| Toda la pantalla se vuelve rosa durante el partido | Mira la Console en ese momento y mándame los mensajes, y dime qué tarjeta de video tiene tu PC (Administrador de dispositivos → Adaptadores de pantalla) |
| Error rojo sobre *Input* (`InvalidOperationException`) | **Edit → Project Settings → Player → Other Settings → Active Input Handling → Both** y reinicia Unity |
| Errores rojos `CS0101` o `CS0111` ("already contains a definition"), o archivos con `(1)` en el nombre | Hay copias duplicadas del juego. Descomprime el ZIP en una carpeta **nueva y vacía** y ejecuta `ACTUALIZAR_JUEGO.bat`: borra las copias `(1)` dentro de `Assets\Football` y avisa si hay otras en otro lugar de `Assets` |
| El partido se congela | Ahora el juego muestra en rojo «ERROR EN EL PARTIDO» o «EL PARTIDO DEJÓ DE AVANZAR» con los datos del fallo: manda una captura de ese texto y de la Console |
| Errores rojos al compilar | Copia el texto de la pestaña Console y mándamelo |
| No aparece el menú "Football" | Espera a que termine de compilar; si persiste, revisa que copiaste `Football` dentro de `Assets` |
| Al construir el APK dice que falta la escena `Assets/Football/Scenes/Main.unity` | Ya no se usa: la escena ahora se crea en `Assets/FootballGenerated`. Vuelve a ejecutar **Football → Construir APK** |
| Al instalar el APK dice «no es compatible con el teléfono» | El APK anterior era solo de 64 bits y algunos Samsung (como el A13) usan Android de 32 bits. Ahora **Football → Construir APK** incluye 32 y 64 bits: vuelve a construirlo e instálalo (pesa más). Si sigue igual, mándame la versión de Android del móvil (Ajustes → Información del teléfono → Información del software) |
| El APK no se genera | Comprueba que el módulo Android (con OpenJDK y SDK/NDK) está instalado y que cambiaste la plataforma a Android |
