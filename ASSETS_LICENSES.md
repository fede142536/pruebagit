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

Se recodificaron con ffmpeg a MP3 (hinchada y cánticos: mono, 32 kHz, 64 kb/s; música: estéreo, 44,1 kHz, 96 kb/s) para que pesen menos en el celular.

## Modelo humanoide

| Archivo | Origen | Licencia | Uso |
|---|---|---|---|
| `ThirdParty/Quaternius_UniversalBaseCharacters/*` (modelos `Superhero_Male/Female_FullBody`) | Quaternius, «Universal Base Characters Kit» (versión gratuita), https://quaternius.com | CC0 1.0 (ver `License_Standard.txt` del pack) | Cuerpo de los jugadores |
| `Game/Assets/Football/Resources/Humanoid/body.bytes` | Derivado del anterior con `Tools/ImportHumanoid` (reducido, sin texturas, pintado por regiones) | CC0 | Lo que carga el juego |

Las texturas del pack no se usan (el juego pinta por vértice con los colores de cada club); se quitaron del repositorio para no sumar 30 MB. Siguen en la descarga original.
