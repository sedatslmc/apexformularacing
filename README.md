# Project Apex

Originales, mobiles Formel-Simcade-Rennspiel (Unity 6, C#, URP). Android hat Vorrang, iOS folgt später.
Flaggschiff-Strecke ist der fiktive **Bosphorus Grand Prix** (Istanbul).

| Ordner | Inhalt |
|---|---|
| `unity/com.projectapex.game/` | Spielcode als lokales Unity-Paket: `Apex.Simulation` (engine-unabhängig), `Apex.Runtime`, `Apex.Editor`, Tests, Streckendaten |
| `unity/ProjectApex/` | Unity-Projekt; wird lokal über den Unity Hub angelegt, siehe [`docs/UNITY_SETUP.md`](docs/UNITY_SETUP.md) |
| `build/` | .NET-Projekte für die Prüfung ohne Unity (Tests, Kompiliertests) |
| `tools/` | `verify.sh` (alle Prüfungen), Streckenwerkzeuge (Python) |
| `docs/` | Design, Technik, Physik, Testplan, Roadmap, Entscheidungen, Lizenzen |
| `legacy/godot/` | früherer Godot-Prototyp (nur als Referenz) |

Schnellstart: [`docs/UNITY_SETUP.md`](docs/UNITY_SETUP.md) · Entwicklerleitfaden: [`AGENTS.md`](AGENTS.md)
