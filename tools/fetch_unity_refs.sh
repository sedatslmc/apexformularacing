#!/usr/bin/env sh
# Downloads reference material for compile-checking the Unity code outside the
# Unity editor (CI / cloud containers without Unity):
#   - UnityEngine module reference assemblies (NuGet "UnityEngine.Modules", 2021.3)
#   - UnityEditor reference assembly (NuGet "Unity3D.SDK", 2021.1) for editor code
#   - Input System package sources (GitHub, tag 1.11.2), compiled by build/InputSystemRef
# This only proves the code compiles against these APIs; it does not replace
# opening the project in the Unity 6 editor.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
REFS="$ROOT/build/refs"
mkdir -p "$REFS"
if [ ! -f "$REFS/UnityEngine/UnityEngine.CoreModule.dll" ]; then
  curl -sSL -o "$REFS/unityengine.modules.nupkg" \
    https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  rm -rf "$REFS/nupkg" && mkdir -p "$REFS/nupkg" "$REFS/UnityEngine"
  unzip -q -o "$REFS/unityengine.modules.nupkg" -d "$REFS/nupkg"
  find "$REFS/nupkg" -name "UnityEngine*.dll" -exec cp {} "$REFS/UnityEngine/" \;
fi
if [ ! -f "$REFS/sdk/lib/UnityEditor.dll" ]; then
  curl -sSL -o "$REFS/unity3d.sdk.nupkg" https://api.nuget.org/v3-flatcontainer/unity3d.sdk/2021.1.14.1/unity3d.sdk.2021.1.14.1.nupkg
  rm -rf "$REFS/sdk" && mkdir -p "$REFS/sdk" && unzip -q -o "$REFS/unity3d.sdk.nupkg" -d "$REFS/sdk"
fi
if [ ! -d "$REFS/InputSystem" ]; then
  git clone -q --depth 1 --branch 1.11.2 https://github.com/Unity-Technologies/InputSystem "$REFS/InputSystem"
fi
echo "Unity references ready in $REFS"
