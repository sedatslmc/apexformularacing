# Fahrphysik und Tastatursteuerung – Überarbeitung November 2026

**Stand:** 2026-11-01.

**Status:**
- **Implementiert und getestet:**
  - NUnit-Tests auf dem 6-DOF-Prüfstand und auf der echten Collider-Fläche von `bosphorus_a`
  - Analysewerkzeuge in `Apex.Tuning`
- **Im Unity-Editor nicht gefahren.** Alle Zahlen stammen aus der Simulation (`SurfaceRig`, derselbe Kraftcode wie `VehicleController`), nicht von einem Gerät.
- **Vergleich im Spiel:** Einstellungen → Fahrverhalten → „Fahrverhalten (Vergleich)“: **Bisherig (Okt. 2026)** oder **Neu (Nov. 2026)**, wirksam ab der nächsten Session.

## 1. Analyse: Woher kamen Untersteuern, Übersteuern und Hektik?
**Werkzeug:** `KeyboardHandlingProbe` (`cd build/Apex.Tuning && dotnet run -c Release -- kbprobe 1 1`).
- **Eingabe:** Es fährt über die echte Eingabekette des Spiels: Tasten → Tastenrampe → Fahrzeug.
- **Messgröße:** Die Reifenausnutzung ist der kombinierte Schlupf relativ zum Haftungsgipfel. 1 heißt „am Gipfel“, über 1 heißt „überzogen“.
- **Messgröße statt Schräglauf:** Der reine Schräglaufwinkel überzeichnet das Untersteuern. Die Reifenkurve ist am Gipfel flach: Schon 69 % des Gipfelschräglaufs liefern 95 % der Kraft. Deshalb gibt es jetzt zusätzlich eine kraftbasierte Balance (`Capacity.ForceBalance`).

**Messwerte vorher** (Profil Ausgewogen, Taste gehalten):

| | 60 km/h | 120 km/h | 200 km/h | 300 km/h |
|---|---|---|---|---|
| Vorderreifen (max.) | **2,26** | 1,96 | 1,87 | 1,31 |
| Querbeschleunigung | 1,47 g | 2,03 g | 3,03 g | 4,67 g |
| Gier-Überschwingen | 0 % | 40 % | **76 %** | 67 % |
| Anbremsen und Einlenken: Hinterreifen (max.) | 0,93 (150 km/h) | – | 1,16 (250) | **1,18** (300) |

### Ursache 1: Langsames Untersteuern kam von der Tastatur, nicht vom Reifen
- **Mechanismus:** Eine gehaltene Taste bedeutet vollen Lenkeinschlag.
  - Bei 60 km/h erlaubt die Lenkhilfe „Leicht“ den vollen mechanischen Einschlag von 22°.
  - Die Vorderreifen stehen damit beim **2,1-Fachen** ihres Gipfelschräglaufs: Sie radieren und schieben.
- **Folge:** Das Auto fuhr mit Taste nur 1,47 g statt der möglichen 1,93 g.
- **Warum nur auf der Tastatur:** Ein Lenkrad- oder Gamepad-Fahrer dosiert das selbst, eine Taste kann das nicht.

### Ursache 2: Hektik bei hohem Tempo war dieselbe Überlenkung
Bei 200–300 km/h stand die Front kurz bei 1,3–1,9. Das Auto zuckte hinein (Gier-Überschwingen bis 76 %) und schob dann.

### Ursache 3: Das Übersteuern in schnellen Kurven ist ein Heckproblem beim Anbremsen
- **Bremsnicken:** Es verschob bis zu 6 % des Abtriebs nach vorn (`aeroRakeSensitivity` 1,0). Das entlastete das Heck genau beim Einlenken.
- **Querstabilisatoren 25/80 kN/m:** Sie legten die Wanklastverlagerung stark aufs Heck. Das war im Oktober gegen das Tastatur-Untersteuern gedacht und hat dieses nur kaschiert.
- **Heckreifen:** Sie fielen nach dem Gipfel stärker ab als die Front (Gleitboden 0,78 statt 0,82).

### Was keine Ursache war
- **Bremsbalance:** keine Wirkung, weil ABS die Räder ohnehin am Limit hält.
- **Ackermann:** steht seit Oktober bei 0.
- **Federn:** passen zum Abtrieb.

## 2. Änderungen (mit Begründung)

### Fahrzeugphysik (`VehicleSimulation`, `VehicleSetup`)

| Änderung | vorher | jetzt | Warum |
|---|---|---|---|
| **Lenkbegrenzer** (`steerSlipTarget`) | – | volle Eingabe = Vorderreifen am Haftungsgipfel | Behebt Ursache 1 und 2 an der Wurzel (siehe unten) |
| Heck-Schutz im Lenkbegrenzer | – | ab 80 % Heckausnutzung weniger Lenkwinkel | Nötig, weil die Front jetzt voll greift; vorher drehte sich das Auto beim schnellen Links-rechts |
| Reifen μ vorn/hinten | 1,80 | **1,95** | Mehr nutzbarer, großzügiger Grip mit klarer Grenze |
| Gleitboden hinten | 0,78 | 0,84 | Das Heck lässt nach dem Gipfel weicher los: rettbares Rutschen statt Abriss |
| Querstabilisatoren vorn/hinten | 25/80 kN/m | **30/70 kN/m** | Mechanische Balance (langsame Kurven): das Heck dreht weiter mit |
| Aero-Balance vorn | 0,44 | **0,41** | Aerodynamische Balance (schnelle Kurven): mehr Abtrieb hinten, ruhiges Heck |
| Bremsnicken → Aero-Balance | 1,0 | 0,3 | Weniger Abtriebsverschiebung nach vorn beim harten Anbremsen |

- **Wie der Lenkbegrenzer rechnet:**
  - Er schätzt keinen Grip. Er nimmt die Fahrtrichtung der Vorderräder relativ zum Auto (φ = Schräglauf + Radwinkel, nach Radlast gewichtet).
  - Daraus folgt der Einschlag für den Ziel-Schräglauf: δ = φ + Ziel × Gipfelschräglauf.
  - Beim Einlenken mit geradem Auto sind das ~7°. Sobald das Auto giert, wächst der Wert mit; in der Haarnadel sind es 15–22°, bei 300 km/h 2–4°.
  - Teileingaben und Gegenlenken skalieren mit. Unter 15 km/h (Parken, Wenden) gilt der volle mechanische Einschlag.
  - Grip kommt dabei nie hinzu.
- **Trennung von langsam und schnell:**
  - Langsame Kurven bestimmt die mechanische Balance (Stabilisatoren), schnelle Kurven die Aero-Balance.
  - So wurde das Untersteuern langsam und das Übersteuern schnell gleichzeitig angegangen, statt mit einem Zahlenwert hin und her zu schieben.
  - Die Kandidaten 50/50 · 0,44, 35/65 · 0,42 und 30/70 · 0,41 wurden alle auf dem Prüfstand und auf der Strecke verglichen.

### Tastatur (`KeyboardSteering`, `InputRouter`)
- **Rampe:**
  - Ein interner Lenkweg fährt mit Drück-, Loslass- und Umkehrrate.
  - Bei Tempo baut sich der Lenkweg langsamer auf; das „voll“ selbst begrenzt das Auto.
  - Danach folgt eine **progressive Kurve** (Ausgabe = Weg^γ): Kurzes Tippen gibt eine kleine Korrektur, Halten den vollen Einschlag in derselben Zeit wie linear. Smooth heißt hier also nicht langsam.
- **Takt:** Die Rampe läuft jetzt im **Physiktakt** (`FixedUpdate`), vorher im Bildtakt. Die Tastatur fühlt sich damit bei 30 und 240 fps gleich an.
- **Profile** (Einstellungen → Steuerung → „Tastatur-Lenkprofil“):

  | Profil | Zeit bis voller Einschlag 60 / 300 km/h | Loslassen | γ | Ziel-Schlupf |
  |---|---|---|---|---|
  | Direkt | ~0,12 / 0,18 s | ~0,08–0,13 s | 1,15 | 1,10 (etwas über den Gipfel, für Könner) |
  | **Ausgewogen** (Standard) | ~0,15 / 0,28 s | ~0,1–0,17 s | 1,35 | 1,00 |
  | Smooth | ~0,20 / 0,38 s | ~0,13–0,22 s | 1,60 | 0,95 |

- **Weitere Einstellungen:**
  - „Lenkempfindlichkeit“ (50–150 %): skaliert die Rampen; beim Gamepad die Kennlinie, Exponent 1,7 bis 1,0.
  - „Lenkglättung“ (aus bis 100 % = 60 ms): zusätzlicher Filter, aus als Standard.
- **Eingabegeräte:**
  - **Gamepad:** Der analoge Stick geht direkt durch die Kennlinie. Der Lenkbegrenzer wirkt nur mit Lenkhilfe: Leicht 1,15, Standard 1,0, aus = rohe Mechanik.
  - **Neigung (Gyro):** Die vorhandene Glättung bleibt; der Begrenzer wirkt mit 1,0.
  - **Touch-Tasten:** wie die Tastatur.
- **Fahrhilfen:**
  - TC: Aus, Niedrig, Mittel, Hoch.
  - ABS: An, Aus.
  - Lenkhilfe: Aus, Leicht, **Standard** (früher „Voll“).
  - Alle Werte werden gespeichert (`SettingsData`).

### Gemeinsame Physikbasis (`CarPerformance`)
- **Vorher:** Die KI-Linie plante mit einem festen Reibwert von 1,45, egal welches Auto fuhr.
- **Jetzt:** Die Planung kommt aus dem Fahrzeug: Reifen-μ × 0,806 (dieselbe Kalibrierung wie bisher, also 1,57 für das neue Auto) und dazu der Abtrieb.
- **Wer sie nutzt:** KI, Ideallinie und Testfahrer. Mehr Grip heißt automatisch höhere Kurvengeschwindigkeiten und spätere Bremspunkte für alle.
- **Streckenlayout:** Kerbs, Gummiabrieb und Bremstafeln behalten die feste Referenz (`CarPerformance.LayoutReference`); sie wandern also nicht mit dem Setup.
- **Bremsplanung:**
  - Sie bleibt bei 0,85 des Reibwerts, gemessen sind das 70–75 % der echten Geradeaus-Verzögerung.
  - Spätere Werte (0,88–1,0) wurden getestet. Tastaturfahrer mit An/Aus-Bremse kamen dann im Bremsknick vor T1 von der Strecke ab. Die Reserve bleibt deshalb.

## 3. Ergebnis

**Tastatur, Profil Ausgewogen** (`kbprobe`):

| | bisherig | neu |
|---|---|---|
| Halten 60 km/h: Front / Querbeschl. / Radius | 2,26 / 1,47 g / 19 m | **0,96 / 1,85 g / 15 m** |
| Halten 120 km/h | 1,96 / 2,03 g | **1,04 / 2,41 g** |
| Halten 200 km/h: Front, Überschwingen | 1,87, 76 % | **1,06, 45 %** |
| Halten 300 km/h | 1,31, 4,67 g, 67 % | **1,04, 4,96 g, 41 %** |
| Tippen 300 km/h: Schwimmwinkel | 2,0° | **1,0°** |
| Links-rechts 200 km/h: Heck max. | 1,03 | **0,81** |
| Gas weg in der Kurve 280 km/h: Heck max. | 0,87 | **0,65** |
| Anbremsen und Einlenken 300 km/h: Heck max., Schwimmwinkel | 1,18, 6,4° | **1,06, 5,5°** |

**Prüfstand** (Gamepad-Rampe, `handling 1`):
- **Querbeschleunigung:** 2,03 / 2,75 / 4,16 g bei 80 / 150 / 250 km/h, vorher 1,93 / 2,67 / 4,16 g.
- **Kraftbalance:** 1,06–1,11, also leichtes, kontrolliertes Untersteuern am Limit.
- **Bremsen 300 → 80 km/h:** 83 m statt 89 m.
- **Beschleunigung:** 0–100 km/h in 2,74 s statt 3,00 s, 0–200 km/h in 5,23 s statt 5,63 s.

**Runde Bosphorus GP mit Tastatur** (KI entscheidet die Tasten, jedes Auto plant mit seinem eigenen Grip):
- **Rundenzeit:** bisherig ~144 s, neu ~140,5 s.
- **Fahrfehler:** keine Dreher und kein Verlassen der Strecke, in allen Fahrprofilen und allen Tastaturprofilen, trocken und nass.

**Ehrlich:**
- **Gier-Überschwingen bei Vollausschlag** liegt bei 200–300 km/h weiter bei 40–60 %. Das Auto lenkt sehr bereitwillig ein und beruhigt sich dann. Wer es weicher will, nimmt das Profil Smooth. Ob es sich zu „spitz“ anfühlt, zeigt erst der Editor.
- **Grobe Fehler bleiben möglich:** Ohne Lenkhilfe und mit dem alten Tastaturprofil überzieht das Auto weiterhin (Test `B_TheLimitIsStillThereForGrossErrors`).
- **Gefälle beim Bremsen** ist nur in den Rundentests auf der echten Strecke abgedeckt (Barbaros-Abfahrt, Palanga). Der Prüfstand ist eben.
- **Kamera:** Die Kameradynamik ist unverändert. Der Auftrag sagt zu Recht, dass die Kamera keine schlechte Physik kaschieren darf; zuerst muss die Physik im Editor bestätigt werden.

## 4. Tests
- **`HandlingBriefTests`** (Tests A–D aus dem Auftrag):
  - A: Haarnadel
  - B: schnelle Kurve, Lupfen, und dass grobe Fehler weiterhin bestraft werden
  - C: Tastenrampe (Tippen, Halten, Loslassen, Umkehr) und Links-rechts bei 120/200/300 km/h
  - D: Anbremsen und Einlenken bei 150/250/300 km/h, Vollbremsung mit und ohne ABS
- **`KeyboardDrivingTests`:**
  - Alle Fahr- und Tastaturprofile, trocken und nass, auf der Collider-Fläche
  - A/B-Runde bisherig gegen neu
- **`HandlingBenchmarkTests`:** Kraftbalance, Bremsen, Transienten, Vergleichs-Setup
- **`RacingLineGuideTests`:** Test E, siehe `docs/RACING_LINE_GUIDE.md`
- **Werkzeuge** (`cd build/Apex.Tuning`):
  - `kbprobe <Hilfe 0-2> <Tasten 0-3> [legacy] [feld=wert …]`
  - `capacity`
  - `brakecurve`
  - `kblap <Profil> <von> <bis> [Tasten] [legacy] [feld=wert …]`
  - `guide [distanz kmh]`
  - `guide-csv`

## 5. So testest du im Unity-Editor
1. **Vergleich:**
   - Einstellungen → Fahrverhalten → „Fahrverhalten (Vergleich)“ auf **Bisherig**, eine Session Zeitfahren auf dem Bosphorus GP.
   - Dann auf **Neu** und dieselbe Strecke.
   - Achte auf T1 (Bremsung von 330 km/h in die Haarnadel), Ortaköy und die schnellen Knicke T3/T4/T12/T13.
2. **Test A, langsame Haarnadel (T1, Ortaköy, Bayıldım):** Taste voll halten. Das Auto soll einlenken, ohne zu schieben, und das Heck soll leicht mitdrehen.
3. **Test B, schnelle Knicke bei 250–300 km/h:** kurz tippen, dann halten. Das Heck soll ruhig bleiben, das Auto darf sauber einlenken.
4. **Test C:**
   - Alle drei Tastatur-Lenkprofile (Einstellungen → Steuerung) auf einer Geraden mit schnellem A/D-Wechsel bei 120 und 250 km/h ausprobieren.
   - Lenkempfindlichkeit auf 70 % und 130 % stellen.
5. **Test D:** Vollbremsung von 330 km/h vor T1, einmal gerade und einmal mit früh eingelenkter Taste, mit ABS an und aus (Einstellungen → Fahrhilfen).
6. **F3-Telemetrie:**
   - Die Zeile „Schlupf V/H“ sollte bei gehaltener Taste vorn um 1,0 liegen (vorher 1,8–2,2) und hinten darunter.
   - Werte hinten über 1 zeigen ein rutschendes Heck.
7. **Rückmeldung:** Bitte die Stellen nennen, an denen es noch untersteuert oder nervös wirkt, möglichst mit F6-Leistungsbericht oder Screenshot der Telemetrie.

**Stellschrauben** für die Abstimmung nach dem Editor-Test:

| Wert | Ort | Wirkung |
|---|---|---|
| `KeyboardSteering.RatesFor` | Rampen und γ der Tastaturprofile | wie schnell und wie weich die Tasten lenken |
| `KeyboardSteering.SlipTarget` | Ziel-Schlupf je Profil | wie nah am Haftungsgipfel volle Eingabe lenkt |
| `VehicleSimulation.RearGuardStart/Gain` | Heck-Schutz | wie früh der Lenkwinkel zurückgenommen wird, wenn das Heck an seine Grenze kommt |
| `VehicleSetup.antiRollFront/Rear` | Querstabilisatoren | Balance in langsamen Kurven |
| `VehicleSetup.aeroBalanceFront` | Abtriebsverteilung | Balance in schnellen Kurven |
| `tireFront/Rear.muPeak` | Reifenreibwert | wie viel Grip insgesamt |
