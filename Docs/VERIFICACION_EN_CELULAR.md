# Verificación en el celular (lo que todavía no se ejecutó en Unity)

Todo lo de esta lista está **escrito y compilado contra un reemplazo de Unity, pero nunca se corrió en Unity ni en el Galaxy A13**. Marcá cada punto y mandá capturas de lo que falle.
Antes de empezar: `ACTUALIZAR_JUEGO.bat`, abrí Unity y mirá que la consola no tenga errores rojos.

## 1. Rendimiento (5 minutos)
1. Menú → **Benchmark** (partido IA contra IA, semilla fija). Dejalo correr completo.
2. Al final copiá el informe de la pantalla de resultado. También queda en `benchmark_report.txt` dentro de la carpeta de datos de la aplicación (ruta en el log: «informe del benchmark guardado en…»).
3. Objetivos: **≥ 30 FPS sostenidos**, p99 < 40 ms, basura (gen0) casi 0, asignaciones ≈ 0 KB/s.
4. Repetilo en calidad **baja** y **alta** (Ajustes → Gráficos). Si en alta cae de 30 FPS, es la sombra proyectada o el LOD: avisame cuál.
5. Jugá 10 minutos seguidos: ¿se calienta o baja el FPS con el tiempo?

## 2. Jugadores
- [ ] Se ven brazos pegados al torso, cuello de camiseta de otro color, muslos y pantorrillas con volumen.
- [ ] Dorsal en la espalda y escudo en el pecho legibles de cerca.
- [ ] Sombra proyectada sin parpadeo; sin recuadros negros (si hay, es la pasada de stencil).
- [ ] A distancia (11 contra 11) cambia al modelo simple sin «salto» visible.
- [ ] Marca celeste bajo tus jugadores y roja bajo el rival.
- [ ] **Primer toque** (nuevo): al recibir un pase el jugador sube el muslo y amortigua; no «patina» ni se tuerce el pie. (El cabezazo está implementado pero casi nunca ocurre: casi todo viaja por el piso.)

## 3. Táctica y audio
- [ ] Pausa → PLANTEL Y TÁCTICA: la cancha muestra a los 11 en su puesto; al cambiar la formación se reacomodan; tocar un punto selecciona al jugador.
- [ ] Música de Brasil solo en menús (nunca en el partido ni en la pausa).
- [ ] Hinchada real durante el partido; grito de gol.
- [ ] Con **Boca Juniors** como local: cánticos a tramos. Con otro equipo: no suenan.
- [ ] Ajustes → Audio → «Música de los menús» cambia el volumen.

## 4. Partido
- [ ] Reloj: no corre hasta el puntapié inicial; marca 45' por parte; tiempo agregado anunciado.
- [ ] Al terminar el tiempo agregado el árbitro espera al lateral, saque de arco o gol; un penal se juega entero.
- [ ] No aparece el cartel rojo «EL PARTIDO DEJÓ DE AVANZAR» (si aparece, captura).

## 5. Modo Carrera (nunca probado)
- [ ] Crear DT (nombre y apellido con teclado), elegir club, jugar una fecha, volver al hub.
- [ ] Buzón, finanzas, estadio y objetivos abren sin errores en consola.
- [ ] Guardar, cerrar y reabrir: sigue la misma temporada.
