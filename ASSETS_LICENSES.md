# Assets de terceros

Uso personal, sin monetización. Los audios los aportó el usuario (descargados de bibliotecas gratuitas); **el origen y la licencia salen de los
nombres de archivo y no están verificados** contra la página de cada sonido: confirmarlos antes de compartir el juego con otras personas.

| Archivo en `Game/Assets/Football/Resources/Audio/` | Original | Origen probable | Uso en el juego |
|---|---|---|---|
| `music_menu.mp3` | `substancial-latin-loop-brazil-154637.mp3` | Pixabay (autor «substancial») | Música de los menús; nunca en el partido ni en la pausa |
| `crowd_ambient.mp3` | `freesound_community-football-crowd-3-69245.mp3` | Pixabay / Freesound (comunidad) | Hinchada de fondo del partido |
| `crowd_cheer.mp3` | `vishiv-crowd-cheering-in-stadium-435357_1.mp3` | Pixabay (autor «vishiv») | Grito de gol |
| `boca_song_1.mp3` | `La12_QUIERO_QUEMAR_EL_GALLINERO.mp3` (aportado por el usuario) | Grabación de la hinchada «La 12»; **licencia desconocida, uso personal** | Ambiente de los partidos de Boca Juniors (se recortó el silencio inicial y final) |
| `boca_song_2.mp3` | `La12_Se_ores_soy_de_Boca_y_lo_sigo_a_todos_lados.mp3` (aportado por el usuario) | ídem | ídem |

| `boca_song_3.mp3`, `boca_song_4.mp3` | `La12_-_Canciones_de_Boca__Esta_es_la_barra_de_Boca.mp3`, `La12_-_SIEMPRE_ESTAR__A_TU_LADO_BOCA_JUNIORS_QUERIDO.mp3` (aportados por el usuario, 24/11/2026) | Hinchada «La 12»; **licencia desconocida, uso personal** | Partidos de Boca de local (prioridad) |
| `music_magalenha.mp3` | `Magalenha - Sergio Mendes.mp3` (aportado por el usuario) | Grabación comercial con derechos de autor; **solo uso personal, no distribuir** | Música de los menús (alterna con `music_menu.mp3`) |
| `songs/*.mp3` (13 temas) | Cómo olvidar (Los Auténticos Decadentes), Para no olvidar (Calamaro), Cuando yo muera (Rubén Rada), Estás para ganar (Johny Tolengo), No me arrepiento de este amor y Se me ha perdido un corazón (Gilda), Mi historia entre tus dedos (Grignani), Bad moon rising (Creedence), Y dale alegría a mi corazón (Fito Páez), Beso a beso (La Mona Jiménez), Una calle nos separa (Leo Dan), El negro José (Roberto Ternán), Dale vieja dale (Los Pugliese) — aportados por el usuario, 24/11/2026 | Grabaciones comerciales con derechos de autor; **solo uso personal, no distribuir ni publicar el APK** | Temas que suenan al azar durante los partidos |

| `music_chega_de_saudade.mp3`, `music_insensatez.mp3`, `music_garota_de_ipanema.mp3`, `music_desafinado.mp3`, `music_wave.mp3` | `Baila Nova - Chega de Saudade / Insensatez / Garota de Ipanema / Desafinado / Wave.mp3` (aportados por el usuario, 24/11/2026) | Grabaciones comerciales con derechos de autor; **solo uso personal, no distribuir** | Música de los menús (se elige cuáles suenan en Ajustes → Audio) |

Las canciones de los menús (`music_*.mp3`) llevan un `.meta` con el audio en modo *Streaming* (se lee del disco mientras suena en vez de descomprimirse entera: una canción de 8 minutos serían ~85 MB de memoria) y mono; Unity las recomprime al exportar.

Sin usar: «La colita» (La Sonora Colombiana) e «It's a heartache» (Bonnie Tyler) llegaron dañados (621 bytes, sin audio); si se vuelven a subir, basta agregarlos a `SongPaths` en `GameAudio.cs`.

Se recodificaron con ffmpeg a MP3 (hinchada, cánticos y temas: mono, 32 kHz, 64 kb/s, sin silencios en los extremos y a volumen parejo con `loudnorm` −20 LUFS; música de menús: estéreo, 44,1 kHz, 96 kb/s) para que pesen menos en el celular.

## Modelo humanoide

| Archivo | Origen | Licencia | Uso |
|---|---|---|---|
| `ThirdParty/Quaternius_UniversalBaseCharacters/*` (modelos `Superhero_Male/Female_FullBody`) | Quaternius, «Universal Base Characters Kit» (versión gratuita), https://quaternius.com | CC0 1.0 (ver `License_Standard.txt` del pack) | Cuerpo de los jugadores |
| `Game/Assets/Football/Resources/Humanoid/body.bytes` | Derivado del anterior con `Tools/ImportHumanoid` (reducido, sin texturas, pintado por regiones) | CC0 | Lo que carga el juego |
| `ThirdParty/Quaternius_UniversalAnimationLibrary/UAL1_Standard*.glb` | Quaternius, «Universal Animation Library» (Standard), https://quaternius.com (subido por el usuario, 23/11/2026) | CC0 1.0 (ver `License.txt` del pack) | 43 animaciones (reposo, caminar, trotar, correr, salto, caída, golpes…) para reemplazar las procedurales; mismo esqueleto de 65 huesos que el cuerpo |


Las texturas del pack no se usan (el juego pinta por vértice con los colores de cada club); se quitaron del repositorio para no sumar 30 MB. Siguen en la descarga original.
