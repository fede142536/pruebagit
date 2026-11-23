# ImportHumanoid

Convierte el modelo humanoide CC0 de Quaternius (`ThirdParty/Quaternius_UniversalBaseCharacters`, «Universal Base Characters», versión gratuita, licencia CC0)
al formato del juego: `Game/Assets/Football/Resources/Humanoid/body.bytes`.

```
pip install numpy scipy fast-simplification
python3 Tools/ImportHumanoid/convert.py ThirdParty/Quaternius_UniversalBaseCharacters/Superhero_Male_FullBody.gltf \
        Game/Assets/Football/Resources/Humanoid/body.bytes [--ply carpeta]
```

Qué hace: baja los brazos de la T a la pose A, corta la cabeza (la arma el código con su pelo y cara), reduce la malla (LOD0 ≈ 3.700 triángulos, LOD1 ≈ 760),
reparte los pesos de los 65 huesos sobre los 20 del juego, corta la malla en los límites camiseta/short/media/botín para que los colores queden nítidos y guarda
una región por vértice. Los colores del club y del jugador se aplican al cargar (`SkinnedBodyGeometry.BuildImportedBody`).

Ajustes en `convert.py`: `LOD0_TRIS`, `LOD1_TRIS`, `longest` (finura de la camiseta, para los diseños) y las alturas de los cortes (`HEM`, `SHORT_HEM`, `SOCK_TOP`…).
Verificación sin Unity: `dotnet run --project Tools/SkinCheck -- --humanoid Game/Assets/Football/Resources/Humanoid/body.bytes` (y `--ply archivo.ply 0 --pattern 1`
para volcar la malla).
