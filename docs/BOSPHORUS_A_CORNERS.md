# Bosphorus Grand Prix (`bosphorus_a`) – Kurvenanalyse

**Stand:** 2026-10-26, nach der Fahrverhaltens-Überarbeitung (`docs/HANDLING_OVERHAUL.md`).

**Erzeugt mit:** `cd build/Apex.Tuning && dotnet run -c Release -- corners bosphorus_a 1.0` (`CornerAnalysis`).

**Methode:**
- Gemessen wird eine fliegende Runde der KI (Pace 1,0, Profil Ausgewogen) auf der echten Collider-Fläche, mit dem 6-DOF-Rig und reifenwarmer zweiter Runde.
- Die KI fährt die Ideallinie konstant, aber nicht am absoluten Limit. Ein schneller Spieler bremst später und fährt engere Linien.
- Die Zahlen sind Richtwerte für das Streckendesign, keine Rundenrekord-Prognose.

**Runde:**
- 130,1 s, Spitze 331 km/h, Schnitt ≈ 210 km/h
- Vollgas 68 % der Zeit, Bremse 18 %

**Spaltenerklärung:**
- Bremspunkt = Rundenmeter, an dem das Bremsen beginnt.
- Einfahrt = Tempo am Bremspunkt.
- Ausgang = Tempo 150 m nach dem Scheitel.
- Lenkbedarf = maximaler Lenkeingang in % des tempoabhängigen Einschlags. Er ist daher bei schnellen Knicken nicht klein.
- Anlauf = Vollgasstrecke vor dem Bremspunkt.

| # | Kurve | Ri. | Winkel | Bremspunkt | Bremsweg | Einfahrt | Scheitel | Ausgang | Lenkbedarf | Anlauf | Überholstelle |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Ortaköy Virajı | L | 193° | 1943 | 211 m | 330 | 63 | 145 | 41 % | 2083 m | **ja** |
| 2 | Palanga 1 | R | 78° | 2392 | 89 m | 199 | 114 | 174 | 35 % | 222 m | |
| 3 | Palanga 2 | L | 50° | – | – | 159 | 159 | 232 | 30 % | – | |
| 4 | Yıldız Sırtı | R | 21° | – | – | 196 | 196 | 211 | 12 % | – | |
| 5 | Balmumcu | L | 160° | 3594 | 158 m | 264 | 80 | 210 | 51 % | 282 m | möglich |
| 6 | Barbaros İnişi | R | 65° | 5123 | 183 m | 331 | 138 | 219 | 28 % | 1343 m | **ja** |
| 7 | Akaretler 1 | R | 51° | 5461 | 85 m | 223 | 146 | 220 | 30 % | 148 m | |
| 8 | Akaretler 2 | L | 144° | 6078 | 178 m | 288 | 74 | 203 | 51 % | 526 m | **ja** |
| 9 | Bayıldım 1 | R | 193° | 6484 | 158 m | 233 | 108 | 115 | 52 % | 199 m | |
| 10 | Bayıldım Virajı | L | 269° | 6756 | 98 m | 164 | 56 | 198 | 67 % | 0 m | |
| 11 | Kabataş Dönüşü | L | 88° | 7088 | 108 m | 234 | 123 | 216 | 39 % | 215 m | |
| 12 | Saray 1 | L | 21° | – | – | 203 | 203 | 255 | 58 % | – | |
| 13 | Saray 2 | R | 27° | – | – | 241 | 241 | 276 | 58 % | – | |

Tempi in km/h. „–“ = ohne Bremsen gefahren.

## Bewertung
**Stärken:**
- **Drei echte Überholstellen:**
  - T1 nach der 2-km-Bosporus-Geraden (330 → 63 km/h)
  - T6 am Ende der Barbaros-Abfahrt (331 → 138 km/h)
  - T8 (288 → 74 km/h)
  - Alle drei haben lange Bremszonen von 180–210 m, in denen ein Spieler aus dem Windschatten innen hineinstechen kann.
- **Gute Mischung:**
  - Vier langsame Kurven (T1, T5, T8, T10 unter 85 km/h)
  - Vier mittelschnelle Kurven (T2, T6, T7, T11 mit 110–150 km/h)
  - Schnelle Richtungswechsel (T3, T4, T12, T13)
- Die schnellen Knicke T12/T13 direkt nach Start und Ziel und T4 auf dem Yıldız-Rücken sind volllastfähig, aber nicht trivial: Sie fordern präzise Lenkung bei 200–280 km/h.

**Schwächen:**
1. **Komplex T9–T10 (Bayıldım):**
   - Zwei Spitzkehren hintereinander (193° und 269°), dazwischen nur ~100 m Beschleunigung (115 → 164 km/h).
   - T10 ist mit 56 km/h und 67 % Lenkbedarf die langsamste und lenkintensivste Kurve.
   - Für Tastatur und Gamepad ist das die schwierigste Stelle: Fehler kosten viel, Überholen ist dort kaum möglich.
2. **T5 Balmumcu:** Eine gute Bremszone (264 → 80 km/h), aber nur 282 m Anlauf aus Palanga 2. Überholen geht nur mit klarem Tempoüberschuss.
3. **T3, T4, T12, T13 werden ohne Bremsen gefahren.** Sie sind freigegebene Kurven, aber fahrerisch Knicke. Das ist für einen Stadtkurs normal und kein Fehler.

## Entscheidung (2026-10-26)
- Vorschlag A abgelehnt: **T10 bleibt.** Die Doppelkehre T9/T10 ist der technisch anspruchsvollste Abschnitt.
- Über Layoutänderungen wird erst nach dem Unity-Handlingtest entschieden.

## Vorschläge (nicht umgesetzt, Freigabe nötig)
Die Bosporus-Gerade bleibt in jedem Fall unverändert.

| Vorschlag | Wirkung | Aufwand / Risiko |
|---|---|---|
| **A. T10 weiter öffnen** (Radius ~+30 %, Scheitel ~75 km/h) | Komplex Bayıldım flüssiger, weniger Lenkbedarf, besserer Ausgang in die Kabataş-Passage | Liegt auf realen Straßen. Mehr Radius erfordert einen anderen Straßenzug oder eine Platzfläche. Neu-Erzeugung (`build_track_a.py`), Barrieren und Tests A–G neu prüfen. |
| **B. Windschatten- bzw. Überholhilfe-Zonen** auf der Bosporus-Geraden (→ T1) und der Barbaros-Abfahrt (→ T6) | Macht die zwei längsten Bremszonen zu planbaren Überholstellen | Windschatten / Dirty Air ist seit 2026-11-01 umgesetzt (`docs/PHYSICS_MODEL.md`). Eine zusätzliche Überholhilfe (DRS-artig) wäre ein eigenes Feature. Keine Layoutänderung. |
| **C. T5 Bremszone verlängern** (Palanga 2 etwas enger, damit die Ausfahrt früher gerade wird) | +100–150 m Anlauf vor Balmumcu, vierte Überholstelle | Größere Layoutänderung an realer Straßenführung |
| **D. Nichts ändern** | Charakter eines echten Stadtkurses mit einer technischen Schlüsselstelle (Bayıldım) | – |

**Empfehlung:**
- Zuerst B, sobald der Windschatten umgesetzt ist, denn B ändert das Layout nicht.
- Danach Spielertests. A nur, wenn Tester Bayıldım als frustrierend und nicht als herausfordernd empfinden.
