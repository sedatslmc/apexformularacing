#!/usr/bin/env sh
# Full verification without a Unity editor:
#   1. fetch reference assemblies (once)
#   2. build Apex.Simulation exactly like Unity (netstandard2.1, C# 9, warnings = errors)
#   3. run all EditMode tests with NUnit on .NET
#   4. compile-check Apex.Runtime (with and without APEX_URP) and Apex.Editor against Unity reference assemblies
#   5. generate the procedural car geometry and check outward-facing normals
#   6. self-test the CC0 texture importer (tools/textures)
# Opening the project in the Unity 6 editor remains the authoritative check.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
"$ROOT/tools/fetch_unity_refs.sh"
cd "$ROOT/build"
dotnet build Apex.Simulation/Apex.Simulation.csproj -nologo -v q
dotnet test Apex.Simulation.Tests/Apex.Simulation.Tests.csproj -nologo -v q
dotnet build Apex.Tests.CompileCheck/Apex.Tests.CompileCheck.csproj -nologo -v q   # EditMode tests against Unity's NUnit 3.5
dotnet build Apex.Runtime.CompileCheck/Apex.Runtime.CompileCheck.csproj -nologo -v q
dotnet build Apex.Runtime.CompileCheck.URP/Apex.Runtime.CompileCheck.URP.csproj -nologo -v q   # APEX_URP paths against URP API stubs
dotnet build Apex.Editor.CompileCheck/Apex.Editor.CompileCheck.csproj -nologo -v q
dotnet run --project Apex.ModelPreview/Apex.ModelPreview.csproj -c Release -v q -- "${TMPDIR:-/tmp}/apex_model_preview" --check
python3 "$ROOT/tools/textures/test_import_cc0.py" -q   # CC0 texture import (synthetic set)
echo "verify: OK"
