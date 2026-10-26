# Fahrverhalten – Validierung im Unity-Editor (Protokoll)

**Zweck:** Das neue Fahrverhalten aus PR #10 im echten Spiel prüfen (PhysX-Rigidbody, echte Eingabegeräte) und direkt mit dem alten vergleichen.

**Status:**
- Protokoll und Werkzeuge sind **implementiert**.
- Die Durchführung steht **aus**: Die Cloud-Umgebung hat keinen Unity-Editor, deshalb wurde **nichts davon in Unity ausgeführt**.
- Die Offline-Zahlen unten stammen vom 6-DOF-Prüfstand (`SurfaceRig`).

## Werkzeuge im Spiel
- **Vergleichsschalter:** Einstellungen → „Fahrverhalten (Vergleich, ab nächster Session)“
  - **Neu (Okt. 2026):** Setup aus PR #10, ESC, schnellere Tastenrampen, Stick-Kurve
  - **Alt (vor Okt. 2026):**
    - exakt das Setup von `main` vor PR #10 (`VehicleSetup.Legacy()`)
    - kein ESC
    - alte Tastenrampen, linearer Stick
    - Test `LegacySetupReproducesThePreOverhaulBaseline`: 1,72 g, Balance 2,00 wie gemessen
  - Gilt nur für das Spielerauto; die KI fährt immer das neue Setup.
  - Bei „Alt“ steht oben im HUD **„ALTES FAHRVERHALTEN (VERGLEICH)“**.
  - **Wechsel:** Einstellungen umschalten → zurück → **F5** (Session neu starten, nicht im Rennen).
- **Telemetrie (F3):** Neue Zeile mit
  - `NEU`/`ALT`
  - Schlupfausnutzung vorne/hinten (> 1 = über dem Grip-Maximum)
  - Querbeschleunigung in g, Gierrate in °/s, Schwimmwinkel β
  - ESC/ABS-Anzeige
- **Offline vorab:**
  - `cd build/Apex.Tuning && dotnet run -c Release -- handling` (Kennwerte)
  - `dotnet run -c Release -- errors` (Fahrfehler-Matrix)
  - `dotnet run -c Release -- step-trace 250 0.25 2` (Lenkstoß-Zeitverlauf)

## Vorbereitung
- Unity 6, Projekt mit URP. **Physik: 120 Hz** (Fixed Timestep 0,008333).
- Strecke `bosphorus_a`, freies Training, trocken, goldene Stunde; danach einmal nass.
- **Geräte:** Tastatur, Gamepad (Xbox/PS) und, falls vorhanden, ein Lenkrad.
- Pro Test zuerst **Alt**, dann **Neu**, dann noch einmal **Alt**. Bei Bedarf blind: Eine zweite Person schaltet um und verdeckt das HUD-Banner.

## Prüfpunkte

| # | Frage | Wie prüfen | Woran erkennen (Telemetrie) | Offline-Erwartung (Neu) | Ergebnis Unity |
|---|---|---|---|---|---|
| 1 | Wie direkt lenkt das Auto ein? | Lange Gerade, 200 km/h, kurzer Lenkimpuls mit Gamepad (¼ Ausschlag) und Taste | Gierrate steigt ohne spürbare Pause; β bleibt < 2° | 63 % der Gierrate nach 42–50 ms, Gierverzug bei 1 Hz 17–27 ms | |
| 2 | Wendig bei niedrigem Tempo? | T10 Bayıldım (56 km/h) und T1 Ortaköy (63 km/h) | Kein Vollanschlag nötig; Schlupf V ≈ 1,0 am Scheitel, nicht > 1,3 | Grip 1,93 g bei 80 km/h (alt 1,72), Balance 1,43 (alt 2,0) | |
| 3 | Wendig bei hohem Tempo? | T6 Barbaros (138 km/h), Yıldız-Knick (196 km/h), Saray 1/2 (200–240 km/h) | Schlupf V/H beide 0,7–1,0; kein Schieben über den Kerb | 2,67 g bei 150, 4,16 g bei 250 km/h | |
| 4 | Ist das Grip-Limit nachvollziehbar? | Tempo in T2/T7 Runde für Runde um 5 km/h steigern | Am Limit wächst Schlupf V langsam über 1 (Auto schiebt hörbar und sichtbar), kein Abriss ohne Vorwarnung | weicher Abriss (Gleitboden 0,82/0,78) | |
| 5 | Profi: Übersteuern durch Fehler möglich? | (a) T10-Ausgang im 2. Gang Vollgas bei vollem Lenkeinschlag; (b) in T6 am Limit plötzlich vom Gas; (c) zu spät, voll gebremst einlenken in T1 | β > 10° oder Dreher; Schlupf H > Schlupf V | **siehe Befund unten:** mit TC 1 bleibt das Auto offline stabil | |
| 6 | Überschwingen bei 250 km/h ein Problem? | Profi, 250 km/h auf der Geraden, schneller ¼-Lenkimpuls und Spurwechsel | Gierrate: eine Spitze, dann Plateau, kein Pendeln | eine Spitze 39 °/s → 31 °/s in ~0,4 s, kein Nachschwingen | |
| 7 | Fährt die KI zuverlässig? | Rennen mit 19 KI, 5 Runden, trocken und nass; Zuschauen per TV-Kamera | Keine Abflüge, kein Stehenbleiben, kein Tempoverlust auf der Geraden | 6-DOF-Offline: Runde 130,1 s, Vollgas auf der Geraden bis 331 km/h | |
| 8 | Bodenaufsetzen / Kerbs | Bosporus-Gerade Vollgas, Kerbs in T1/T5/T8 | Telemetrie „AUFSETZEN“, Fahrhöhe H | 4–5 mm Bodenfreiheit bei 330 km/h (alt 2 mm) | |

## Offline-Befunde, die Unity bestätigen oder widerlegen soll

### Fahrfehler-Matrix (`errors`, Dreher = Schwimmwinkel > 15°)

| Setup / Profil | Vollgas bei vollem Lenkeinschlag, 50 km/h | dasselbe bei 90 km/h | Lastwechsel 120 / 200 km/h | Bremsen + voller Lenkeinschlag bei 200 km/h | Vollanschlag 250 |
|---|---|---|---|---|---|
| Neu: Einsteiger / Ausgewogen / Profi | stabil | stabil | stabil | stabil | stabil |
| Neu: Profi, TC aus | **Dreher** | **Dreher** | stabil | stabil | stabil |
| Neu: TC aus, ohne tempoabhängige Lenkung | **Dreher** | stabil | stabil | stabil | stabil |
| Alt: alle drei Profile | stabil | stabil | stabil | stabil | stabil |
| Alt: Profi, TC aus | **Dreher** | **Dreher** | stabil | stabil | stabil |

**Bewertung (ehrlich):**
- Im Profi-Profil kann man das Auto **offline durch Fahrfehler kaum verlieren**. Das gilt für das alte wie für das neue Setup.
  - Leistungsübersteuern fängt die TC-Stufe 1 ab.
  - Lastwechsel und Bremsfehler enden in Untersteuern, nicht in einem Dreher.
- Ursachen:
  - Profi behält aus Tastatur-Gründen ABS und TC 1 (`SettingsData.ApplyProfile`).
  - Viel Abtrieb und eine hecklastige Gewichtsverteilung stabilisieren das Heck.
- Für ein glaubwürdiges Profi-Gefühl ist das vermutlich **zu gutmütig**. Der Offline-Prüfstand hat aber keine Bodenwellen und keine Kerb-Stöße; die prüft erst Unity.
- **Mögliche Folgeschritte (nicht umgesetzt, erst nach dem Unity-Test entscheiden):**
  1. **Profi mit TC aus, wenn ein Gamepad oder Lenkrad erkannt wird.** Analoge Pedale machen das fahrbar; die Tastatur behält TC 1.
  2. **Lastwechsel-Empfindlichkeit erhöhen,** z. B. Aero-Balance bei Nicken nach vorne oder ein etwas früherer Heck-Abriss, nur für die Profi-Abstimmung. Ziel: Lastwechsel bei 120 km/h am Limit gibt β 8–12° und bleibt mit Gegenlenken fangbar.
  3. **TC-Stufe 1 weniger eingreifend:** mehr Schlupf erlauben, damit ein Gasstoß das Heck sichtbar bewegt, ohne sofort zu drehen.

### Überschwingen 250 km/h (Profi)
- Der ¼-Lenkstoß erzeugt eine einzelne Gierspitze, etwa 25 % über dem Endwert, nach rund 0,11 s. Danach bleibt die Gierrate ruhig.
- Ursache: Die Vorderreifen bauen Seitenkraft schneller auf, als sie danach im gesättigten Bereich halten. Das ist kein instabiles Pendeln.
- Im Spiel fühlt sich das vermutlich wie ein „spitzes Einlenken, dann leichtes Schieben“ an.
- **Abnahme in Unity:** Wird das als nervös empfunden, kommen kleinere Schritte infrage, etwa eine höhere Reifen-Relaxationslänge (0,35 → 0,45 m) nur oberhalb von 200 km/h. Erst testen, dann ändern.

## Abnahmekriterien (Vorschlag)
- **Tester-Urteil:** Mindestens 2 von 3 Testern bewerten „Neu“ bei 1 bis 4 als besser als „Alt“ (blind).
- **Profi:** Punkt 5 erzeugt bei groben Fehlern (a oder b) einen fangbaren Rutscher oder Dreher. Sonst wird Folgeschritt 1 oder 2 umgesetzt.
- **KI:** 3 Rennen ohne KI-Abflug und ohne steckengebliebene KI.
- **Physik-Takt:** Der Profiler im Editor zeigt keine verpassten Physikschritte (120 Hz). Gerätemessungen folgen separat.

## Ergebnisvorlage
Pro Test notieren:
- Gerät, Profil, Alt/Neu
- Runde und Kurve
- Telemetriewerte (Screenshot mit F9)
- Urteil in einem Satz
