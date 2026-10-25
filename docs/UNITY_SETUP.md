# Unity-Einrichtung auf Windows (Schritt für Schritt)

Ziel: Das Projekt in Unity 6 öffnen, die Testoval-Session starten und die M1-Abnahme durchführen.
Dauer beim ersten Mal: ca. 20–40 Minuten (vor allem Downloads).

## Schnellweg (empfohlen): ein Doppelklick
1. **Unity Hub** installieren (https://unity.com/download), anmelden und unter *Installs* eine **Unity 6 LTS** installieren (Modul *Android Build Support* ankreuzen).
2. Dieses Repository herunterladen: auf GitHub **Code → Download ZIP** und entpacken, oder per GitHub Desktop klonen.
3. Im Repository-Ordner **`Setup-Windows.bat`** doppelklicken.

Das Skript findet Unity 6 und legt das Projekt `unity\ProjectApex` an. Es trägt das Spielpaket ein, führt die Einrichtung aus und öffnet Unity mit gestartetem Spiel.
Beim ersten Mal dauert das einige Minuten. Logs liegen in `unity\Logs-Setup`.
Später startet derselbe Doppelklick das Spiel schnell wieder. Alternativ in Unity: Menü **Project Apex → Spiel starten**.

Hinweis: Das Skript legt ein Projekt mit der Standard-Render-Pipeline an. Für den Fahr-Prototyp reicht das. Die URP-Umstellung folgt mit dem Grafik-Meilenstein (M3).
Die folgenden Abschnitte beschreiben den manuellen Weg mit URP-Vorlage.

## 1. Voraussetzungen
1. **Git** und dieses Repository lokal klonen, z. B. nach `C:\Dev\project-apex`.
2. **Unity Hub** installieren und eine **Unity 6 LTS**-Version hinzufügen (die neueste 6000.x LTS im Hub).
   Module: *Android Build Support* (mit OpenJDK und Android SDK & NDK Tools). *iOS Build Support* geht nur mit Mac und Xcode.
3. Für deinen Laptop (Ryzen 5 7530U, integrierte Grafik): Unity-Editor mit DirectX 11 (Standard),
   *Edit → Preferences → General → Interaction Mode: Monitor Refresh Rate* hilft der Akkulaufzeit.

## 2. Projekt anlegen (einmalig)
1. Unity Hub → **New project** → Vorlage **Universal 3D** (URP).
2. **Location:** `<Repo>\unity`, **Project name:** `ProjectApex`. Dadurch entsteht `<Repo>\unity\ProjectApex`.
3. Projekt erstellen lassen und Unity wieder schließen.
4. Datei `<Repo>\unity\ProjectApex\Packages\manifest.json` in einem Editor öffnen und ergänzen:
   ```json
   {
     "dependencies": {
       "com.projectapex.game": "file:../../com.projectapex.game",
       "... (bestehende Einträge bleiben) ...": ""
     },
     "testables": [ "com.projectapex.game" ]
   }
   ```
   Den Eintrag `com.projectapex.game` in die vorhandene `dependencies`-Liste einfügen; die Platzhalterzeile oben nicht übernehmen.
   `testables` auf oberster Ebene neben `dependencies` ergänzen.
5. Unity wieder öffnen. Das Input System wird als Abhängigkeit automatisch installiert.
   Fragt Unity, ob das neue Input-System-Backend aktiviert werden soll, mit **Yes** bestätigen (Editor startet neu).

## 3. Projekt einrichten
1. Menü **Project Apex → Projekt einrichten (alles)**:
   - Player-Einstellungen: Querformat links/rechts, Android IL2CPP + ARM64, Bundle-ID `com.projectapex.game`,
     Input Handling = Input System Package
   - Material-Assets in `Assets/ProjectApex/Resources/ApexMaterials` (für Builds nötig)
   - Boot-Szene `Assets/ProjectApex/Scenes/Boot.unity` (als erste Szene in den Build-Einstellungen)
2. Meldet die Konsole „Editor-Neustart nötig“, Unity neu starten.
3. `Assets/ProjectApex/Scenes/Boot.unity` öffnen → **Play**.

## 4. Bedienung im Editor
- Menü: Pfeiltasten + Enter oder Maus.
- Fahren: **W/S** Gas/Bremse, **A/D** lenken, **Q/E** schalten (bei manuellem Getriebe), **C** Kamera,
  **Rücktaste** Auto zurücksetzen, **F3** Telemetrie, **Esc** Pause. Gamepad: Trigger, linker Stick, Schultertasten, Start.
- Touch-Tasten im Editor testen: *Einstellungen → Steuerung: Touch-Tasten*. Die Maus simuliert dann einen Finger.
- Neigung (Gyro) lässt sich nur auf einem echten Gerät testen.

## 5. Tests in Unity
*Window → General → Test Runner → EditMode → Run All*. Erwartet: alle Tests grün (dieselben 43 Tests wie `tools/verify.sh`).

## 6. Android-Testbuild
1. *File → Build Profiles* (bzw. *Build Settings*) → **Android** → *Switch Platform*.
2. Handy per USB anschließen (Entwickleroptionen + USB-Debugging aktivieren) → **Build And Run**.
3. Im Spiel *Einstellungen → Steuerung: Neigung (Gyro)* wählen und die Kalibrierung zu Sessionbeginn bestätigen.

## 7. Häufige Probleme
| Problem | Lösung |
|---|---|
| Rosa Materialien | *Project Apex → 2. Materialien erzeugen* ausführen. Ist das Projekt keine URP-Vorlage, nutzen die Materialien den Standard-Shader. |
| `The type or namespace 'InputSystem' could not be found` | Package Manager → *Input System* installieren. Danach *Edit → Project Settings → Player → Active Input Handling* auf *Input System Package* oder *Both* stellen. |
| Tests erscheinen nicht im Test Runner | Fehlt `"testables": ["com.projectapex.game"]` in `manifest.json`? |
| Streckendaten fehlen | Liegt der Paketpfad in `manifest.json` richtig (`file:../../com.projectapex.game`)? |
