# Escudos reales de los clubes (uso personal)

**Estado**: el usuario subió tres hojas con los escudos (`Game/Assets/escudos completos*.jfif`) y `Tools/slice_crests.py` las recorta en un PNG por club
(47 clubes) dentro de `Game/Assets/MisEscudos/Resources/Crests`, que `ACTUALIZAR_JUEGO.bat` copia a tu proyecto sin borrar nada. Como las hojas son chicas (cada
escudo mide 40–60 px), se ven borrosos de cerca; los de la hoja de la Primera Nacional se identificaron a ojo y los 17 que figuran ahí pueden estar mal asignados:
si alguno no coincide, renómbralo. Faltan Deportivo Madryn y Mitre (Santiago del Estero). Para reemplazar uno, pon tu PNG con el mismo nombre.

El juego usa un escudo dibujado por código para cada club. Si quieres los escudos reales, **los pones tú en tu proyecto de Unity** y el juego los usa solos (en el selector de equipos, la carrera y todos los lugares donde se ve el escudo). Los clubes sin imagen siguen con el escudo dibujado.

## Dónde ponerlos

1. En Unity, dentro de `Assets`, crea las carpetas `MisEscudos/Resources/Crests` (la carpeta se llama exactamente `Crests`, dentro de una carpeta `Resources`).
2. Copia ahí un PNG por club, **con fondo transparente**, cuadrado y de 256 px o más.
3. El nombre del archivo es el nombre del club en minúsculas, sin tildes y con guiones bajos (tabla abajo). Ejemplo: `boca_juniors.png`.

**No los pongas en `Assets/Football`**: `ACTUALIZAR_JUEGO.bat` borra de esa carpeta todo lo que no viene del repositorio. En `Assets/MisEscudos` están a salvo (el .bat solo copia, no borra).

## Por qué no vienen en el repositorio

Los escudos son marcas registradas de los clubes. Por pedido del usuario (D-69) ahora se versionan en el repositorio (privado, uso personal): no lo hagas público ni redistribuyas el juego con ellos.

## Nombres de archivo

| Club | Archivo |
|---|---|
| Boca Juniors | `boca_juniors.png` |
| River Plate | `river_plate.png` |
| Racing Club | `racing_club.png` |
| Estudiantes de La Plata | `estudiantes_de_la_plata.png` |
| San Lorenzo | `san_lorenzo.png` |
| Independiente | `independiente.png` |
| Vélez Sarsfield | `velez_sarsfield.png` |
| Defensa y Justicia | `defensa_y_justicia.png` |
| Banfield | `banfield.png` |
| Lanús | `lanus.png` |
| Gimnasia y Esgrima La Plata | `gimnasia_y_esgrima_la_plata.png` |
| Talleres | `talleres.png` |
| Belgrano | `belgrano.png` |
| Rosario Central | `rosario_central.png` |
| Newell's Old Boys | `newell_s_old_boys.png` |
| Huracán | `huracan.png` |
| Argentinos Juniors | `argentinos_juniors.png` |
| Godoy Cruz Antonio Tomba | `godoy_cruz_antonio_tomba.png` |
| Tigre | `tigre.png` |
| Platense | `platense.png` |
| Unión (Santa Fe) | `union_santa_fe.png` |
| Instituto (Córdoba) | `instituto_cordoba.png` |
| Atlético Tucumán | `atletico_tucuman.png` |
| Central Córdoba (Santiago del Estero) | `central_cordoba_santiago_del_estero.png` |
| Barracas Central | `barracas_central.png` |
| Sarmiento (Junín) | `sarmiento_junin.png` |
| Independiente Rivadavia | `independiente_rivadavia.png` |
| Deportivo Riestra | `deportivo_riestra.png` |
| Aldosivi | `aldosivi.png` |
| San Martín (San Juan) | `san_martin_san_juan.png` |
| Gimnasia y Esgrima (Mendoza) | `gimnasia_y_esgrima_mendoza.png` |
| Estudiantes (Río Cuarto) | `estudiantes_rio_cuarto.png` |
| Colón | `colon.png` |
| Quilmes | `quilmes.png` |
| Chacarita Juniors | `chacarita_juniors.png` |
| Atlético Rafaela | `atletico_rafaela.png` |
| Patronato | `patronato.png` |
| Temperley | `temperley.png` |
| Almagro | `almagro.png` |
| Ferro Carril Oeste | `ferro_carril_oeste.png` |
| All Boys | `all_boys.png` |
| Atlanta | `atlanta.png` |
| Tristán Suárez | `tristan_suarez.png` |
| Deportivo Madryn | `deportivo_madryn.png` |
| Deportivo Morón | `deportivo_moron.png` |
| Gimnasia y Esgrima (Jujuy) | `gimnasia_y_esgrima_jujuy.png` |
| Mitre (Santiago del Estero) | `mitre_santiago_del_estero.png` |
| Los Andes | `los_andes.png` |
| Almirante Brown | `almirante_brown.png` |
