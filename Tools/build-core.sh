#!/usr/bin/env bash
# Compila el núcleo (C# puro) y copia la DLL al proyecto Unity.
# Uso: Tools/build-core.sh        (requiere .NET SDK 8)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dest="$root/Game/Assets/Plugins/Core"

dotnet build "$root/Core/Core.csproj" -c Release --nologo -v quiet
mkdir -p "$dest"
cp "$root/Core/bin/Release/netstandard2.1/Football.Core.dll" "$dest/"
cp "$root/Core/bin/Release/netstandard2.1/Football.Core.xml" "$dest/" 2>/dev/null || true
echo "Núcleo copiado a: $dest"
