# Fahrphysik-Modell

Ziel ist glaubwürdiges, belohnendes Simcade-Fahrverhalten, keine Behauptung professioneller F1-Genauigkeit.
Quelle: `Apex.Simulation/Vehicle/*`. Alle Werte sind Startwerte für die Abstimmung.

## Fahrzeug „Apex FA-1“ (fiktiv)
| Größe | Wert |
|---|---|
| Masse inkl. Fahrer | 800 kg, 45 % vorne |
| Radstand / Spur v/h | 3,60 m / 1,62 m / 1,56 m |
| Schwerpunkthöhe | 0,30 m |
| Trägheit (Nick/Gier/Roll) | 1100 / 1250 / 320 kg·m² |
| Federn v/h | 140 / 125 kN/m, Stabis 60 / 40 kN/m, Weg 90 mm, Endanschlag 900 kN/m |
| Dämpfer | 6000 Druck / 8500 Zug N·s/m |
| Motor | 600 N·m Spitze bei 10 000 1/min, Begrenzer 12 300, ca. 685 kW |
| Getriebe | 8 Gänge (6,80 … 1,66) × Achse 3,0, Schaltzeit 50 ms |
| Aero | CdA 1,25 m², ClA 3,6 m², Balance 43 % vorne |
| Bremse | 5200 N·m pro Achse (×2), Balance 57 % vorne |
| Differenzial | Sperre: Vorspannung 120 N·m + 35 % des Moments, Rate 220 N·m/(rad/s) |

## Reifen
Vorne und hinten gibt es getrennte Reifenmodelle. Die Hinterreifen sind breiter: Nennlast 3900 N statt 2800 N, μ 1,80 statt 1,75, Schräglauf-Maximum bei 6,3° statt 6,9°. Sonst wäre die Hinterachse wegen der Lastempfindlichkeit trotz 55 % Gewicht die schwächere Achse, und das Heck bräche beim Einlenken aus (Spielertest-Feedback vom 2026-10-25).

Normierter kombinierter Schlupf: sx = κ/κ_peak, sy = tan α / tan α_peak, s = √(sx² + sy²).
Kraft F = μ(Fz) · Grip · Fz · f(s), aufgeteilt in Fx = F·sx/s und Fy = −F·sy/s.
- f(s) = sin(C·atan(B·s)) für s ≤ 1, mit B = tan(π/2C). Dadurch liegt das Maximum genau bei s = 1.
- Darüber geht f(s) weich auf den Gleitboden (72 %) zurück.
- Lastempfindlichkeit: μ = μ_peak · (1 − 0,12 · (Fz/Fz_nom − 1)), begrenzt auf 0,6–1,25.
- Der Schräglauf läuft über eine Relaxationslänge (0,35 m). Bei kleiner Geschwindigkeit dämpft das das Zittern.
- Der Schlupf-Nenner hat Mindestwerte (Längs 3 m/s, Quer 2 m/s), damit das Modell im Stand stabil bleibt.

## Raddrehung und Antrieb
- Pro Physik-Schritt 6 Substeps: Differenzial, Reifenkraft und Radbeschleunigung.
- Bremsen wirken gegen die Drehrichtung, kehren sie aber nie um.
- Auto-Kupplung: Unter der Startdrehzahl (7500 1/min) rutscht die Kupplung und liefert das Motormoment bei Startdrehzahl.
- Die Automatik schaltet nach der Fahrgeschwindigkeit, nicht nach einem durchdrehenden Rad. Eine Sperrzeit von 0,45 s verhindert Gangpendeln.

## Assistenzsysteme (optional, gekennzeichnet)
- **Traktionskontrolle:** Vorsteuerung. Das Antriebsmoment wird auf die übertragbare Kraft der Hinterreifen begrenzt, also μ·Fz·f(0,92) mal dem Längsanteil, der nach dem Schräglauf noch übrig ist. Zusätzlich gibt es eine sanfte Rückkopplung. Das greift beim Start und bei Power-Oversteer in der Kurve.
- **ABS:** Begrenzt jedes Bremsmoment auf die übertragbare Kraft. Die Motorbremse wird abgezogen, und bei beginnendem Blockieren wird der Druck abgebaut. Unter 3 m/s ist ABS aus.
- **Lenkhilfe** (überarbeitet nach dem Spielertest):
  - Einschlag 22° bei niedrigem Tempo, für ca. 9 m Wenderadius.
  - Bei Tempo begrenzt das kleinere von zwei Limits: eine Tempo-Überblendung auf 4,5° oder ein Grip-Limit. Das Grip-Limit ist der kinematische Winkel für die maximale Querbeschleunigung inklusive Abtrieb plus 90 % des Schräglauf-Maximums vorne.
  - Die Zahnstange ist langsamer bei Tempo: 140°/s im Stand, 35°/s ab ca. 200 km/h.
  - **Gegenlenk-Assistent:** Übersteigt der Schräglauf hinten 75 % seines Maximums (und arbeitet hinten mehr als vorne), lenkt die Hilfe begrenzt gegen. Der Fahrer kann übersteuern.
  - Tastatur und Touch: Ein voller Einschlag dauert langsam ca. 0,3 s, bei Tempo ca. 0,6 s.
  - Ohne Lenkhilfe bleibt der volle mechanische Einschlag mit 140°/s.

## Gemessene Kennwerte (flacher Prüfstand, `dotnet test`)
Stand nach den letzten Änderungen dieser Sitzung:

| Test | Ergebnis | Testgrenze |
|---|---|---|
| 0–100 km/h (TC an) | 3,19 s | 2,0–3,6 s |
| 0–200 km/h | 6,13 s | 4,0–7,5 s |
| Höchstgeschwindigkeit nach 40 s | 330 km/h | 300–365 km/h |
| 200–0 km/h mit ABS | 63,9 m / 2,61 s, kein Blockieren über 5 m/s | 45–110 m |
| Stationäre Kurve 150 km/h, Lenkung 0,15 (ohne Lenkhilfe) | stabil | Schwimmwinkel unter 5,7° |
| Schneller Tastatur-Einschlag mit Lenkhilfe, 100/150/250 km/h | Schräglauf hinten max. 4,4° (Limit 6,3°), kein Ausbrechen | unter dem Maximum, Schwimmwinkel unter 5,7° |
| Wenderadius 20 km/h, voller Einschlag | 9,0 m (vorher 10,4 m) | unter 9,5 m |
| Vollgas-Übersteuern bei 90 km/h ohne TC | ohne Lenkhilfe Dreher, mit Lenkhilfe abgefangen (ca. 21°) | mit Lenkhilfe unter 26° |
| Autopilot Testoval (2,87 km) | 46,5 s, nie neben der Strecke | 40–75 s |
| Autopilot Bosphorus GP v0.2 (5,11 km) | 120,7 s, nie neben der Strecke | 85–140 s |

Der Prüfstand ist ein ebenes 3-DOF-Modell mit quasistatischer Lastverlagerung. Im Spiel rechnet Unitys Rigidbody (6-DOF) mit Federung und Raycasts.
Die Werte sind also Richtwerte. Im Editor müssen sie mit der Telemetrie (F3) bestätigt werden.

## Offene Physik-Themen (M6)
Reifentemperatur, Verschleiß, Mischungen, Nässe-Grip, Ride-Height-Empfindlichkeit, Windschatten, Schäden und ein Realismus-Regler.
