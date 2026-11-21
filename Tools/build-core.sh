#!/usr/bin/env bash
# Compila el núcleo (C# puro) y copia la DLL al proyecto Unity.
# Uso: Tools/build-core.sh        (requiere .NET SDK 8)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dest="$root/Game/Assets/Plugins/Core"

dotnet build "$root/Core/Core.csproj" -c Release --nologo -v quiet
mkdir -p "$dest"
cp "$root/Core/bin/Release/netstandard2.1/Football.Core.dll" "$dest/"
mkdir -p "$root/Game/Assets/Football/Resources"
cp "$root/Data/teams/argentina.json" "$root/Game/Assets/Football/Resources/teams.json"
cp "$root/Data/balance/match.json" "$root/Game/Assets/Football/Resources/match.json"
cp "$root/Data/balance/match_indoor.json" "$root/Game/Assets/Football/Resources/match_indoor.json"
rm -f "$dest/Football.Core.xml"
echo "Núcleo copiado a: $dest (y equipos y balance a Game/Assets/Football/Resources/)"
