# Formel-Fahrverhalten – Überarbeitung (Oktober 2026)

**Status:**
- **Implementiert**, auf dem 6-DOF-Prüfstand **getestet** (NUnit + `Apex.Tuning`).
- Im Unity-Editor und auf Geräten **nicht getestet**; das Fahrgefühl mit Gamepad oder Lenkrad muss ein Spieler bestätigen.
- Alle Zahlen stammen aus der Simulation, nicht aus Messungen am Gerät.

## Methode
- `HandlingBenchmark` (Apex.Simulation) fährt den `SurfaceRig` auf einer ebenen 6×6-km-Fläche. Der Rig hat sechs Freiheitsgrade und nutzt denselben Kraftcode wie `VehicleController` im Spiel: Federn, Stabis, Nicken und Wanken.
- Aufruf: `cd build/Apex.Tuning && dotnet run -c Release -- handling [profil]`
- **Gemessen wird:**
  - Querbeschleunigungsgrenze bei 80/150/250 km/h und Balance (Schlupfausnutzung vorne/hinten am Limit, > 1 = Untersteuern)
  - Lenkstoß 25 % mit t63 und Überschwingen
  - 1-Hz-Sinuslenkung mit Gierverzug
  - Vollanschlag-Stoß, Anbremsen mit Lenkung (Trail-Braking), Vollgas aus der Kurve
  - Bremswege und Traktion
- Die Tastatur-Runden laufen auf der echten Collider-Fläche von `bosphorus_a` (`CircuitTestBed`, 6 DOF). Eine KI entscheidet, ans Auto gehen nur digitale Tasten über die Rampen aus `InputTuning`.
- Der alte quasistatische `FlatGroundRig` ignorierte die Stabilisatoren und eignet sich nicht für die Balance. Er bleibt nur für die alten Grenzwert-Tests (`validate`).

## Warum sich das Auto träge anfühlte (Audit)
1. **Starkes Untersteuern am Limit:**
   - Die Schlupfausnutzung vorne/hinten lag bei 2,0 / 1,8 / 1,8 (80/150/250 km/h). Die Vorderreifen waren gesättigt, während die Hinterreifen nur gut die Hälfte ihres Grips nutzten.
   - Ursache: Die Stabis 60/40 kN/m legten 57 % der Radlastverlagerung auf die Vorderachse. Das kurveninnere Vorderrad fiel auf ~600 N.
2. **Pro-Ackermann 0,12** drehte das entlastete Innenrad über seinen Spitzenschräglauf. Die Lenkung wurde am Limit „leer“, statt Seitenkraft aufzubauen.
3. **Wenig Abtrieb** (3,6 m², 1,25 × Gewicht bei 240 km/h): 3,2 g bei 250 km/h. Schnelle und langsame Kurven fühlten sich kaum verschieden an.
4. **Langsame Tastenrampen:**
   - Lenkrampe 4,5–8/s beim Drücken, 7–10/s beim Loslassen
   - Gamepad linear ohne Kurve: feine Korrekturen bei hohem Tempo waren schwer.
5. **KI-Referenz** (für die Kurvenanalyse relevant): Die KI skalierte ihre Zielgeschwindigkeit mit kalten Reifen auch auf Geraden mit √Grip. Sie fuhr dort nur ~75 % Gas und verlor ~25 km/h (siehe unten).

## Änderungen am Fahrzeug (`VehicleSetup`, `VehicleSimulation`)

| Parameter | vorher | jetzt | Grund |
|---|---|---|---|
| Stabi vorne / hinten | 60 / 40 kN/m | 25 / 80 kN/m | Wanksteifigkeit nach hinten; beide Achsen arbeiten |
| Feder vorne / hinten | 140 / 125 kN/m | 190 / 170 kN/m | proportional zum höheren Abtrieb; Bodenfreiheit bei Tempo wie vorher oder besser |
| Ackermann | 0,12 | 0 | Formelwagen fahren nahezu parallel |
| Abtriebsfläche | 3,6 m² | 4,6 m² | ~1,6 × Gewicht bei 240 km/h |
| Aero-Balance vorne | 0,43 | 0,44 | hält die Balance bei mehr Abtrieb |
| Reifen vorne | μ 1,75, Lastempf. 0,12, Gleitboden 0,72 | μ 1,8, Lastempfindlichkeit 0,08, Gleitboden 0,82 | weniger Grip-Verlust unter Last, weicher Abriss |
| Reifen hinten | μ 1,8, Lastempf. 0,12, Gleitboden 0,72 | μ 1,8, Lastempfindlichkeit 0,08, Gleitboden 0,78 | Heck etwas früher spürbar als Front |
| ESC (neu) | – | ab Schlupfausnutzung hinten > 1,0 | nur mit Lenkhilfe (Einsteiger/Ausgewogen) |

- **ESC:**
  - Aktiv, wenn die Lenkhilfe an ist, das Tempo über 36 km/h liegt und das Heck mehr Schlupf nutzt als die Front.
  - Es nimmt Gas zurück und bremst das kurvenäußere Vorderrad (bis 60 % des Bremsmoments, skaliert mit der Lenkhilfe).
  - Das Profil „Profi“ (Lenkhilfe 0) hat kein ESC.
  - Anlass: Einsteiger drehte sich beim Vollanschlag-Stoß mit 250 km/h.
- **Eingabe:**
  - Tastenrampe Drücken 8 → 4,5/s, Loslassen 10 → 7/s (tempoabhängig)
  - Gamepad: Totzone 0,06, danach Exponent 1,35 (`InputTuning.StickSteer`). Feiner um die Mitte, voller Anschlag bleibt erreichbar.
- Die Bremsbalance bleibt bei 0,57; die Lenkübersetzung, die Zahnstangenrate und die Lenkhilfe-Reserve (1,35) bleiben ebenfalls unverändert.

## Ergebnis (Profil Ausgewogen, 6-DOF-Prüfstand)

| Größe | vorher | jetzt |
|---|---|---|
| Querbeschl.-Grenze 80 / 150 / 250 km/h | 1,72 / 2,24 / 3,20 g | 1,93 / 2,67 / 4,16 g |
| Balance (vorne/hinten) 80 / 150 / 250 | 2,0 / 1,82 / 1,84 | 1,43 / 1,33 / 1,31 |
| Lenkstoß 25 %: t63 | – | 42–50 ms |
| Lenkstoß 25 %: Überschwingen | – | 0–9 % |
| Lenkverstärkung (stationäre Gierrate) | – | +8–13 % |
| 1-Hz-Sinus: Gierverzug | – | 17–27 ms |
| Bremsen 300 → 80 km/h | 99 m | 89 m (Mittel 3,29 g, Spitze 6,3 g) |
| 0–200 km/h | 5,75 s | 5,63 s |
| Vollanschlag-Stoß, Trail-Braking, Power-Exit | stabil | stabil, alle Profile |

**Profile** auf derselben Physik:
- Einsteiger: TC 3, Lenkhilfe voll, ESC
- Ausgewogen: TC 2, Lenkhilfe 0,5, ESC halb
- Profi: TC 1, ABS, keine Lenkhilfe, kein ESC

Das entspricht dem Brief so:
- Accessible = Einsteiger
- Balanced Simcade = Ausgewogen
- Advanced = Profi

Die bestehenden Namen im Menü bleiben.

**Grenzen und offene Punkte:**
- **Profi überschwingt** bei 250 km/h bis 24 %. Ohne Lenkhilfe ist der Lenkeinschlag größer und das Auto lebendiger. Das ist gewollt, sollte aber im Spielertest beobachtet werden.
- **Bodenfreiheit:**
  - Bei 330 km/h auf der langen Geraden bleibt die Bodenplatte 4–5 mm über der Fahrbahn (vorher 2 mm).
  - Der Prüfstand zählt das als spekulativen Kontakt unterhalb von `ContactOffset`, ohne Impuls.
- **Keine Gerätezahlen:** Alles ist simuliert. Ein 6-DOF-Rig ist nicht PhysX; Unterschiede bei Kontakten und Kerbs prüft erst der Editor.

## Tastatur-Runden auf `bosphorus_a` (`KeyboardDrivingTests`)
- **Trocken:** etwa 133 s in allen drei Profilen, 0 Dreher, 0 m neben der Strecke.
- **Nass:** etwa 149 s, 0 Dreher.
- **Testgrenzen:** trocken 118–150 s, nass 135–170 s, 0 Dreher (Schwimmwinkel > 0,45 rad), nie neben der Strecke.

## KI-Korrektur (nötig für eine ehrliche Kurvenanalyse)
- **Fehler:** `AIDriver.TargetSpeed` multiplizierte das gesamte Linienprofil mit √(Pace × Grip). Das Profil enthält auf Geraden die Höchstgeschwindigkeit, also eine Motor- und keine Gripgrenze. Mit kalten Reifen (Grip 0,8) zielte die KI deshalb auf ~310 statt 342 km/h und fuhr ~75 % Gas.
- **Behebung:**
  - `RacingLine` führt zusätzlich ein reines Grip-Profil (`gripSpeed`: Kurvenradius plus Bremsweg, ohne Tempodeckel und Beschleunigungsgrenze).
  - Nur dieses Profil skaliert mit Pace und Grip. Die Leistungsgrenze bleibt unskaliert.
- **Ergebnis:** Auf der Geraden fährt die KI Vollgas (329 statt 293 km/h am Ende).
- Das Brems- und Kurvenverhalten bei Grip < 1 ist unverändert, denn das skalierte Grip-Profil ist genau die Bremskurve mit reduziertem Grip.
- Zusätzlich bremsen `RacingLine` und `Autopilot` im Reibkreis: Kurvengrip und Bremsgrip teilen sich das Limit.

## Kurvenanalyse `bosphorus_a`
Siehe `docs/BOSPHORUS_A_CORNERS.md` (`dotnet run -c Release -- corners bosphorus_a 1.0`).
