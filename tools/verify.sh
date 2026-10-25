#!/usr/bin/env sh
# Full verification without a Unity editor:
#   1. fetch reference assemblies (once)
#   2. build Apex.Simulation exactly like Unity (netstandard2.1, C# 9, warnings = errors)
#   3. run all EditMode tests with NUnit on .NET
#   4. compile-check Apex.Runtime and Apex.Editor against Unity reference assemblies
# Opening the project in the Unity 6 editor remains the authoritative check.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
"$ROOT/tools/fetch_unity_refs.sh"
cd "$ROOT/build"
dotnet build Apex.Simulation/Apex.Simulation.csproj -nologo -v q
dotnet test Apex.Simulation.Tests/Apex.Simulation.Tests.csproj -nologo -v q
dotnet build Apex.Runtime.CompileCheck/Apex.Runtime.CompileCheck.csproj -nologo -v q
dotnet build Apex.Editor.CompileCheck/Apex.Editor.CompileCheck.csproj -nologo -v q
echo "verify: OK"
