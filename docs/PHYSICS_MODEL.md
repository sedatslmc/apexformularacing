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
| Motor | 600 N·m Spitze bei 10 000 1/min, Begrenzer 12 300, ca. 685 kW, Trägheit 0,06 kg·m² |
| Getriebe | 8 Gänge (6,80 … 1,66) × Achse 3,0, Schaltzeit 50 ms |
| Aero | CdA 1,25 m², ClA 3,6 m² (davon 50 % Unterboden mit Bodeneffekt), Balance 43 % vorne, Fahrhöhe statisch 35 / 75 mm |
| Bremse | 4000 N·m gesamt (×2 je Achse verteilt), Balance 57 % vorne (einstellbar 50–65 %) |
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
- **Motor mit Trägheit** (0,06 kg·m²). Die Drehzahl ist ein eigener Zustand und wird nicht mehr direkt aus der Raddrehzahl gesetzt.
- **Auto-Kupplung** mit Coulomb-Reibung:
  - **Haften:** Die Kupplung haftet, solange ihre Kapazität das Motormoment trägt. Dann ist die Motorträgheit an die Hinterräder gekoppelt (reflektiert mit i²).
  - **Start:** Die Kapazität entspricht dem Moment bei Startdrehzahl (7500 1/min). Ein Regler hält die Drehzahl, die Kupplung schließt mit zunehmendem Tempo.
  - **Anti-Abwürgen:** Unter Leerlaufdrehzahl öffnet die Kupplung. Eine Leerlaufregelung hält 4000 1/min.
  - **Nahtloser Schaltvorgang:** Während der 50 ms ist die Kupplung offen. Der Motor gleicht die Drehzahl an (Zündunterbrechung beim Hochschalten, Zwischengas beim Runterschalten, max. 450 N·m). Danach schließt die Kupplung ohne Stoß, das Heck blockiert beim Runterschalten nicht (Test).
- **Drehzahlbegrenzer:** Zündunterbrechung bei 12 300 1/min. Der Status ist in der Telemetrie und in den Schaltlichtern sichtbar.
- Die Automatik schaltet nach der Fahrgeschwindigkeit, nicht nach einem durchdrehenden Rad. Eine Sperrzeit von 0,45 s verhindert Gangpendeln.

## Aerodynamik mit Bodeneffekt (Näherung)
- **Fahrhöhe** vorne und hinten = statische Höhe minus Einfederung (Mittel der Achse). Ein Rad in der Luft zählt als ausgefedert.
- **Unterboden:** ClA_Boden ∝ (h_ref / h)^0,3 mit h_ref = 45 mm (typisch bei ~150 km/h, darauf ist die Rennlinie geplant). Begrenzt auf ×1,6. Unter 10 mm reißt die Strömung ab, bis −30 %. „Aufsetzen“ heißt: Planke unter 0 mm.
- **Balance** wandert mit dem Anstellwinkel: Eintauchen beim Bremsen schiebt sie nach vorne (1 %/cm, maximal ±6 %).
- **Induzierter Widerstand:** +5 % CdA pro Einheit Bodengewinn über 1.
- **Gemessen:** 97 % des Nenn-Abtriebs bei 60 km/h, 100 % bei 175 km/h, 112 % bei 300 km/h (Modus `aero`).
- **Oberflächen-Textur:** Kerbs (7 mm, 1,1 m Welle), Gras und Kies regen die Federung an. Die Frequenz ist auf 24 Hz begrenzt (120-Hz-Physik, kein Aliasing).

## Fahrprofile
| Profil | TC | ABS | Lenkhilfe | Getriebe |
|---|---|---|---|---|
| Einsteiger | hoch | an | voll (Stärke 1) | Automatik |
| **Ausgewogen (Standard)** | mittel | an | leicht (Stärke 0,5) | Automatik |
| Profi | aus | aus | aus | manuell |
| Individuell | frei | frei | frei | frei |

Keine Hilfe erhöht den Grip oder zwingt das Auto auf die Ideallinie. Bei leichter Lenkhilfe bleiben 40 % Einschlag über dem Grip-Limit: Die Front kann überfahren werden. Die Gegenlenk-Verstärkung skaliert mit der Stärke. Alte Einstellungen (v1) werden auf „Ausgewogen“ migriert.

## Assistenzsysteme (optional, gekennzeichnet)
- **Traktionskontrolle (Stufen niedrig/mittel/hoch = 115 / 100 / 88 % erlaubter Schlupf):** Vorsteuerung. Rutscht ein Rad schon über dem Haftmaximum, wird unter die Gleitkraft begrenzt, auch unter 3 m/s. Das Antriebsmoment wird auf die übertragbare Kraft der Hinterreifen begrenzt, also μ·Fz·f(0,92) mal dem Längsanteil, der nach dem Schräglauf noch übrig ist. Zusätzlich gibt es eine sanfte Rückkopplung. Das greift beim Start und bei Power-Oversteer in der Kurve.
- **ABS:** Begrenzt jedes Bremsmoment auf die übertragbare Kraft. Die Motorbremse wird abgezogen, und bei beginnendem Blockieren wird der Druck abgebaut. Unter 3 m/s ist ABS aus.
- **Lenkhilfe** (überarbeitet nach dem Spielertest):
  - Einschlag 22° bei niedrigem Tempo, für ca. 9 m Wenderadius.
  - Bei Tempo begrenzt das kleinere von zwei Limits: eine Tempo-Überblendung auf 4,5° oder ein Grip-Limit. Das Grip-Limit ist der kinematische Winkel für die maximale Querbeschleunigung inklusive Abtrieb plus 90 % des Schräglauf-Maximums vorne.
  - Die Zahnstange ist langsamer bei Tempo: 140°/s im Stand, 35°/s ab ca. 200 km/h.
  - **Gegenlenk-Assistent:** Übersteigt der Schräglauf hinten 75 % seines Maximums (und arbeitet hinten mehr als vorne), lenkt die Hilfe begrenzt gegen. Der Fahrer kann übersteuern.
  - Tastatur und Touch: Ein voller Einschlag dauert langsam ca. 0,3 s, bei Tempo ca. 0,6 s.
  - Ohne Lenkhilfe bleibt der volle mechanische Einschlag mit 140°/s.

## Gemessene Kennwerte (flacher Prüfstand)
Ausführlicher Bericht mit Kurven-, Brems-, Nässe- und Kerb-Szenarien: [`HANDLING_VALIDATION.md`](HANDLING_VALIDATION.md).

| Test | Ergebnis | Testgrenze |
|---|---|---|
| 0–100 km/h (TC mittel) | 3,03 s | 2,0–3,6 s |
| 0–200 km/h | 5,77 s | 4,0–7,5 s |
| Höchstgeschwindigkeit | 330 km/h | 310–350 km/h |
| 200–0 km/h mit ABS | 67 m / 2,8 s | 45–110 m |
| 300–0 km/h mit ABS | 114 m, Spitze 5,0 g | – |
| Querbeschleunigung 140 / 240 km/h (Ausgewogen) | 1,9 / 2,7 g | – |
| Lenkansprechen t63 (60 / 140 / 240 km/h) | 92 / 50 / 33 ms | – |
| Runterschalten 5→4 bei 160 km/h | Drehzahl ±5 % angeglichen, Heck-Schlupf > −0,1 | Test |
| Autopilot Testoval / Bosphorus GP | 46,6 s / 120,8 s, nie neben der Strecke | 40–75 s / 85–140 s |

Der Prüfstand ist ein ebenes 3-DOF-Modell mit quasistatischer Lastverlagerung. Die Fahrhöhe kommt aus der Federeinfederung. Im Spiel rechnet Unitys Rigidbody mit 6 Freiheitsgraden, Federung und Raycasts. Die Werte sind also Richtwerte. Im Editor müssen sie mit der Telemetrie (F3) bestätigt werden.

## Offene Physik-Themen (M6)
Erledigt: Reifentemperatur, Verschleiß, Mischungen, Nässe-Grip, Fahrhöhen-Empfindlichkeit (Bodeneffekt), Motorträgheit, Fahrprofile.
Offen:
- Windschatten und Dirty Air
- Schäden
- DRS
- Sturz und Vorspur als Setup-Parameter
- Funken beim Aufsetzen
- Am Limit (gesättigte Vorderachse) ist die Gierbewegung schwach gedämpft (wenige Schwinger, siehe Validierung). Das ist im Editor zu bewerten.
