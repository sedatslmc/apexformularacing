# Fahrverhalten – Validierung

Stand: 2026-10-25. Erzeugt mit `cd build/Apex.Tuning && dotnet run -c Release -- validate`.
Status: **implementiert, auf dem Prüfstand getestet.** Im Unity-Editor und auf dem Gerät **nicht getestet.**

## Methode
- Ebener Prüfstand (`FlatGroundRig`, 3 Freiheitsgrade, 120 Hz) mit derselben `VehicleSimulation` wie im Spiel.
- Lastverlagerung ist quasistatisch, die Fahrhöhe kommt aus der Federeinfederung.
- Im Spiel rechnet Unitys Rigidbody mit 6 Freiheitsgraden (Nick- und Wankbewegung, echte Kerb-Geometrie). Die Zahlen hier sind daher Richtwerte.
- **Profile:**
  - **Ausgewogen** (Standard): TC mittel, ABS, Lenkhilfe leicht (Stärke 0,5), Automatik
  - **Profi:** alles aus
  - **Einsteiger:** TC hoch, Lenkhilfe voll
- **Lenkstoß:**
  - Der Sollwert springt. Die Zahnstange begrenzt die Rate (140°/s ohne Hilfe, bei Tempo mit Hilfe langsamer).
  - „Am Grip-Limit“ = der Radwinkel, den die volle Lenkhilfe bei diesem Tempo zulässt.
  - „Überlenken“ = der Winkel, den „Ausgewogen“ bei Vollanschlag erlaubt (40 % Reserve über dem Grip-Limit).
  - Gas: Der Fahrer hält das Tempo mit höchstens 50 % Gas. In den „Vollgas“-Zeilen ist nach 0,5 s Vollgas.
- **t63:** Zeit bis die Gierrate 63 % ihres Endwerts erreicht. Das ist das Lenkansprechen inklusive Zahnstange.

## Ergebnisse
### Kurven (Lenkstoss, konstantes Tempo)

| Szenario | Profil | Radwinkel | t63 Gierrate | Gierrate | Querbeschl. | Schräglauf v/h | max. Schwimmwinkel | Federweg außen v | Ergebnis |
|---|---|---|---|---|---|---|---|---|---|
| Haarnadel 60 km/h | Ausgewogen, Vollanschlag Eingabe | 22.0° | 92 ms | 44.8 °/s | 1.31 g | 13.4° / 2.2° | 2.2° | 8.7 mm | stabil, schiebt (Untersteuern) |
| Haarnadel 60 km/h | Profi, am Grip-Limit | 18.4° | 100 ms | 50.9 °/s | 1.49 g | 9.3° / 2.7° | 2.3° | 9.5 mm | stabil, schiebt (Untersteuern) |
| Haarnadel 60 km/h | Profi, Überlenken + Vollgas | 22.0° | – | – | – | 3.8° / 37.2° | 158.1° | 8.5 mm | Dreher |
| Haarnadel 60 km/h | Ausgewogen, Vollanschlag + Vollgas | 16.0° | 67 ms | 31.7 °/s | 1.52 g | 13.8° / 2.5° | 2.2° | 10.3 mm | stabil, schiebt (Untersteuern) |
| Mittelschnell 140 km/h | Ausgewogen, Vollanschlag Eingabe | 13.0° | 50 ms | 27.5 °/s | 1.86 g | 12.3° / 2.7° | 2.5° | 17.0 mm | stabil, schiebt (Untersteuern) |
| Mittelschnell 140 km/h | Profi, am Grip-Limit | 9.2° | 42 ms | 30.2 °/s | 2.05 g | 9.0° / 3.2° | 3.3° | 17.8 mm | stabil, schiebt (Untersteuern) |
| Mittelschnell 140 km/h | Profi, Überlenken + Vollgas | 12.9° | – | – | – | 1.3° / 14.7° | 180.0° | 17.0 mm | Dreher |
| Mittelschnell 140 km/h | Ausgewogen, Vollanschlag + Vollgas | 11.7° | 50 ms | 26.8 °/s | 2.41 g | 12.5° / 3.7° | 2.8° | 22.4 mm | stabil, schiebt (Untersteuern) |
| Schnell 240 km/h | Ausgewogen, Vollanschlag Eingabe | 9.3° | 33 ms | 25.4 °/s | 2.65 g | 10.1° / 2.9° | 5.0° | 35.0 mm | stabil, schiebt (Untersteuern) |
| Schnell 240 km/h | Profi, am Grip-Limit | 5.1° | 33 ms | 28.3 °/s | 2.98 g | 6.7° / 3.6° | 5.2° | 35.4 mm | stabil |
| Schnell 240 km/h | Profi, Überlenken + Vollgas | 7.1° | 33 ms | 26.0 °/s | 3.17 g | 8.5° / 3.2° | 5.3° | 35.2 mm | stabil, schiebt (Untersteuern) |
| Schnell 240 km/h | Ausgewogen, Vollanschlag + Vollgas | 6.8° | 42 ms | 26.2 °/s | 3.20 g | 8.2° / 3.2° | 5.0° | 35.0 mm | stabil, schiebt (Untersteuern) |
| Nass 140 km/h (Inter) | Ausgewogen, Vollanschlag Eingabe | 13.0° | 50 ms | 21.0 °/s | 1.43 g | 13.0° / 2.7° | 3.0° | 14.2 mm | stabil, schiebt (Untersteuern) |
| Nass 140 km/h (Inter) | Profi, am Grip-Limit | 9.2° | 42 ms | 23.0 °/s | 1.56 g | 9.7° / 3.2° | 4.0° | 14.8 mm | stabil, schiebt (Untersteuern) |
| Nass 140 km/h (Inter) | Profi, Überlenken + Vollgas | 12.9° | – | – | – | 5.8° / 4.7° | 179.8° | 14.2 mm | Dreher |
| Nass 140 km/h (Inter) | Ausgewogen, Vollanschlag + Vollgas | 11.8° | 42 ms | 19.4 °/s | 1.66 g | 13.5° / 3.8° | 3.3° | 15.9 mm | stabil, schiebt (Untersteuern) |

### Beschleunigung (Ausgewogen, TC mittel)

0–100 km/h 3.03 s · 0–200 km/h 5.77 s · Höchstgeschwindigkeit 330 km/h · max. Längsschlupf hinten 0.14 · Bodeneffekt-Gewinn bei Vmax ×1.57

### Bremsen (Vollbremsung, Ausgewogen mit ABS / Profi ohne ABS)

| Start | Bedingung | Profil | Bremsweg | Zeit | max. Verzögerung | min. Schlupf v/h (>20 km/h) |
|---|---|---|---|---|---|---|
| 100 km/h | trocken | Ausgewogen | 23 m | 1.73 s | 2.0 g | -0.09 / -0.08 |
| 100 km/h | trocken | Profi | 26 m | 1.97 s | 1.9 g | -1.00 / -1.00 |
| 100 km/h | nass (Inter) | Ausgewogen | 30 m | 2.28 s | 1.6 g | -0.09 / -0.08 |
| 100 km/h | nass (Inter) | Profi | 35 m | 2.58 s | 1.5 g | -1.00 / -1.00 |
| 200 km/h | trocken | Ausgewogen | 67 m | 2.77 s | 3.2 g | -0.09 / -0.08 |
| 200 km/h | trocken | Profi | 82 m | 3.33 s | 3.1 g | -1.00 / -1.00 |
| 200 km/h | nass (Inter) | Ausgewogen | 88 m | 3.68 s | 2.5 g | -0.09 / -0.08 |
| 200 km/h | nass (Inter) | Profi | 106 m | 4.31 s | 2.4 g | -1.00 / -1.00 |
| 300 km/h | trocken | Ausgewogen | 114 m | 3.43 s | 5.0 g | -0.09 / -0.08 |
| 300 km/h | trocken | Profi | 139 m | 4.18 s | 4.8 g | -1.00 / -1.00 |
| 300 km/h | nass (Inter) | Ausgewogen | 147 m | 4.53 s | 4.0 g | -0.09 / -0.08 |
| 300 km/h | nass (Inter) | Profi | 178 m | 5.38 s | 3.9 g | -1.00 / -1.00 |

### Kerb (linke Räder auf Kerb, 150 km/h geradeaus)

Radlast vorne links 2585–3601 N · Kerb-Anregung bis 6.8 mm · max. Gierstörung 0.07 °/s · Querversatz nach 2 s -0.08 m

### Rechenzeit Simulation

5.9 µs pro Fahrzeug-Schritt (Desktop-CPU, ohne Unity) → 20 Autos × 120 Hz ≈ 14.2 ms CPU pro Sekunde

## Bewertung
- **Kein Dreher in „Ausgewogen“.** Das gilt für alle Tempi, trocken und nass, auch bei Vollanschlag mit Vollgas.
  - Die leichte Lenkhilfe lässt Überlenken zu: Die Vorderachse schiebt dann (Untersteuern).
  - Der Gegenlenk-Assistent greift nur begrenzt ein.
  - Es gibt keine künstliche Drehung und keinen zusätzlichen Grip.
- **Profi am Grip-Limit:** stabil von 60 bis 240 km/h, auch nass.
- **Profi mit Überlenken und Vollgas:** Ohne TC und ohne Lenkhilfe dreht sich das Auto. Das ist physikalisch korrekt (Leistungsübersteuern beim Heckantrieb). Im Profi-Profil ist das gewollt.
- **Lenkansprechen:** t63 liegt bei 33–100 ms, je nach Tempo und Zahnstange.
- **Querbeschleunigung:** 1,3–1,5 g (Haarnadel), ~2 g (140 km/h), ~3 g (240 km/h).
- **Bremsen:**
  - 200–0 km/h mit ABS in 67 m, 300–0 km/h in 114 m mit 5 g Spitze.
  - Ohne ABS blockieren die Räder. Der Weg ist dann 20–25 % länger, nass entsprechend mehr.
  - 100 % Pedal ohne ABS blockiert unterhalb von ~250 km/h. Bei hohem Tempo trägt der Abtrieb mehr Bremsmoment.
- **Kerb:**
  - Die Kerb-Riffelung regt die Federung mit ~7 mm an, die Radlast schwankt um ~1000 N.
  - Das Auto bleibt geradeaus: Gierstörung unter 0,1 °/s.
  - Echte Kerb-Höhen kommen im Spiel zusätzlich aus der Streckengeometrie.
- **Rechenzeit:** ~6 µs pro Auto und Schritt auf einer Desktop-CPU. Auf dem Gerät ist das noch zu messen. Dafür zeigt die Telemetrie (F3) jetzt die Frametime.

## Behobene Befunde aus dieser Messreihe
1. **Messartefakt:** Ein auf Tempo gesetztes Auto hatte den Motor noch im Leerlauf.
   - Im ersten Schritt riss die Kupplung ihn hoch und erzeugte ~5800 N·m Bremsmoment an den Hinterrädern. Das verursachte Dreher, die es im Spiel nicht gibt.
   - Lösung: `SyncEngineToWheels()` bzw. `FlatGroundRig.SetSpeed()`.
2. **Durchdrehen beim Start trotz TC:**
   - Die Vorsteuerung begrenzte auf die Kraft am Haftmaximum. Ein Rad, das schon rutscht, überträgt aber nur die Gleitkraft.
   - Lösung: Die TC begrenzt dann darunter und wirkt auch unter 3 m/s.
   - 0–100 km/h: 3,03 s (vorher 3,10 s, mit Durchdrehen).
3. **Bremsmoment 5200 → 4000 N·m:** Mit dem alten Wert blockierten vorne und hinten ohne ABS gleichzeitig, auch bei 200 km/h. Jetzt blockiert mit Standard-Bremsbalance zuerst die Vorderachse, mit Balance nach hinten zuerst die Hinterachse (Tests).

## Noch nicht geprüft (Editor/Gerät)
| # | Szenario | Eingabe | Erwartung | Ergebnis |
|---|---|---|---|---|
| V1 | Haarnadel (Bosporus T1) | Tastatur | Einlenken ohne Heckausbruch, Untersteuern bei zu viel Lenkung | ☐ |
| V2 | Mittelschnelle Kurve | Touch (Gas + Bremse gleichzeitig) | Trail-Braking möglich, Auto bleibt kontrollierbar | ☐ |
| V3 | Schnelle Kurve > 230 km/h | Gyro | proportionale Lenkung, keine Überreaktion | ☐ |
| V4 | Lange Gerade | alle | Höchsttempo ~330 km/h, Fahrhöhe sinkt sichtbar (F3) | ☐ |
| V5 | Bremszone 300→80 | alle, ABS an/aus | ohne ABS blockieren die Vorderräder bei Vollbremsung unter ~250 km/h | ☐ |
| V6 | Kerbs | alle | Fahrwerk zittert sichtbar, kein Abflug | ☐ |
| V7 | Nässe | Inter | Grip deutlich geringer, Regenlicht an | ☐ |
| V8 | Profile | Einstellungen | Einsteiger/Ausgewogen/Profi spürbar verschieden | ☐ |
| V9 | Frametime | F3 | Wert für 12 Autos notieren (Editor und Gerät) | ☐ |
