# Bosporus-Ufer und Ortaköy – Analyse, Küstenkorrektur, Streckenvorschlag

**Stand:** 2026-10-31.

**Status:**
- **Küstenkorrektur (Priorität 1):** implementiert und getestet (Simulation), im Unity-Editor nicht gesehen.
- **Ortaköy-Neuführung (Priorität 2):** nur Plan, **nicht umgesetzt**; die Freigabe des Product Owners steht aus.

## 1. Befund: Warum lagen Wahrzeichen so weit vom Wasser entfernt?
Geprüft habe ich gegen die OSM-Küstenlinie (`data/geo/osm/bosphorus_osm.json`, Wege `natural=coastline`):
- **Koordinaten und Projektion:** in Ordnung. Strecke, Gebäude und Wahrzeichen nutzen denselben lokalen Rahmen (Ursprung Dolmabahçe 41.03944/29.00167, x = Ost, z = Nord). Die OSM-Grundrisse von Dolmabahçe, Çırağan und der Moschee liegen exakt an der OSM-Küste.
- **Platzierung der Wahrzeichen:** in Ordnung. Die Positionen stammen aus den OSM-Grundrissen.
- **Straßenführung:** in Ordnung. Die Strecke folgt den echten Straßen (Çırağan Cd. 73–150 m landeinwärts, bebaut bis zum Wasser).
- **Falsch war die Land-/Wassergrenze im Spiel.**
  - Sie stammte aus der 0-m-Kontur des Copernicus-Höhenmodells (30 m). Über Wasser liefert dieses Modell in Ufernähe Werte über 0 m: Mischpixel, Kaianlagen, Schiffe, Rauschen.
  - Deshalb lief die Landmasse **200–450 m** in den Bosporus hinein.
  - Abstand des Spiel-Ufers zu den Wahrzeichen vorher: Dolmabahçe 381–410 m, Çırağan 320 m, Ortaköy-Moschee 250 m. Real sind es 54–93 m, 36 m und 21 m.
  - Auf diesem erfundenen Landstreifen pflanzte die Stadtgenerierung einen gleichmäßigen „Wald“ (Freiflächen-Bäume mit 30 % Dichte).

| | vorher (DEM) | nachher (OSM) |
|---|---|---|
| Ortaköy-Moschee – Ufer | 250 m | 25 m |
| Çırağan-Palast – Ufer | 320 m | 38 m |
| Dolmabahçe (Selamlık/Harem) – Ufer | 381 / 410 m | < 110 m |
| Bäume in der Stadt | 18.167 | 16.169 |

Karte: `docs/img/waterfront_coast_before_after.jpg` (oben vorher, unten nachher).

## 2. Umgesetzt: Küstenkorrektur
- **Land und Uferlinien:**
  - `tools/geo/osm_land.py` erzeugt die Landmassen und die beiden Uferlinien aus der OSM-Küstenlinie.
  - Technisch: doppelte Wege entfernt, Wege zu Ketten verbunden, offene Ketten am Auszugsrand mit Land zur Linken geschlossen, Douglas-Peucker mit 2,5 m.
  - `build_track_a.py` nutzt jetzt diesen Weg; das Höhenmodell liefert nur noch Höhen und verschiebt die Küste nicht mehr.
  - In `bosphorus_a.json` wurden nur `geo.land`, `shoreline` und `geo.shoreline_asia` ersetzt. Streckenpunkte, Höhen und Kurven sind unverändert.
- **Folgen im Spiel:** Wasserfläche, Gelände (unter Wasser −4 m) und Kollision folgen alle derselben Landmasse (`TerrainField.IsWater`). Gebäude, Bäume, Straßenmöbel und Stadtleben auf dem früheren Scheinland entfallen damit automatisch.
- **Ufer:** `Waterfront`, Darstellung `EnvironmentGeometry.WaterfrontEdge`.
  - Steinerne Kaimauer mit Abdeckstein entlang der echten Küste; die Oberkante folgt dem Gelände dahinter, mindestens 1,2 m über dem Wasser.
  - Auf der europäischen Seite eine 8 m breite gepflasterte Promenade mit Eisengeländer, Pollern (alle 18 m) und Laternen (alle 32 m, nachts leuchtend).
  - Die Promenade weicht der Strecke, Gebäuden, schmalen Molen und der Moschee-Terrasse aus.
  - Umfang: 4,5 km Kai, 3,5 km Promenade.
- **Freiflächen-Bäume:**
  - Dichte von 30 % auf 8 % gesenkt; in den ersten 40 m hinter dem Ufer gar keine mehr, damit der Blick aufs Wasser frei bleibt.
  - Echte Parks, Gärten (auch die Palastgärten), Friedhöfe und Straßenbäume bleiben unverändert.
- **Schiffsverkehr:** Die Transitrouten folgen jetzt der echten Fahrrinne (offene Wasserspanne je Querschnitt).
- **Test `TheWaterfrontLandmarksStandAtTheRealCoast`:**
  - Moschee < 40 m, Çırağan < 60 m und die Dolmabahçe-Flügel < 110 m vom Ufer
  - Kaimauern ≥ 4 km
  - Promenade nie im Wasser und nie in der Sperrzone
  - kein Baum auf dem Wasser

## 3. Ortaköy – räumliche Lage (Grundlage für den Streckenvorschlag)
- **Platz:** Der echte Ortaköy-Platz (OSM: Fußgängerfläche) ist ein 15–35 m breiter Streifen von der Moschee nach Norden zur Muallim Naci Caddesi. Ringsum liegt der dichte Basar mit 5-m-Gassen.
- **Freiraum:** `docs/img/waterfront_free.jpg` zeigt den Abstand zum nächsten Gebäude bzw. zum Wasser. Nur die Muallim Naci Cd. und der Platzstreifen bieten Platz für eine 11-m-Fahrbahn mit Barrieren.
- **Heute:** Die Ortaköy-Kehre liegt am Ende der Çırağan Cd. Die Moschee ist von dort 200 m entfernt und hinter Häusern verborgen (`docs/img/waterfront_ortakoy_now.jpg`).
- **Engstelle:** Zwischen der heutigen Kehre und dem Platz ist die Muallim Naci Cd. auf 180 m nur 10–18 m breit (Fassade zu Fassade).
- **Folge für die Planung:** Jede Kurve am Platz braucht dort Hin- und Rückweg nebeneinander.
- **Ausgeschlossen:** Eine erste Variante mit zwei Fahrbahnen hätte 13 Gebäude geschnitten, darunter die **Etz-Ahayim-Synagoge**. Diese Linienführung ist verworfen.

Der Vorschlag „Meydan-Kehre“ (Variante A) und die Untersuchung der Kurvenradien folgen in Abschnitt 4. Umgesetzt wird erst nach Freigabe.

## 4. Ortaköy-Haarnadel – Radiusvergleich (Plan, nicht umgesetzt)
Grundidee (Variante A, vom PO grundsätzlich befürwortet):
- **Linienführung:** Die Strecke verlässt die Çırağan Cd. über die Muallim Naci Cd., biegt rechts auf den Ortaköy-Platz ab, wendet dort in einer Linkskehre und kehrt parallel dazu zurück. Danach mündet sie wie heute in die Palanga-Steigung.
- **Korrektur gegenüber der ersten Skizze:** Der Hinweg nutzt die westliche Spur nach Süden, der Rückweg die östliche. Nur so kreuzen sich die Spuren an der Platzeinfahrt nicht.

Jede Variante habe ich als vollständige Streckendatei mit den Projektwerkzeugen geprüft (`Apex.Tuning corners`, `lap`, `validate-road`, temporäre Dateien, nicht eingecheckt). Die Sicht ist aus der Chase-Kamera gerechnet: 6 m hinter und 2,2 m über dem Auto, 75° Blickfeld, verdeckt durch die OSM-Gebäudehöhen. Die Prozentwerte geben den Anteil der Positionen auf dem Platz und in der Kehre an.

| | heute (Kehre Çırağan/Palanga) | R 10 Süd (am Ufer) | R 15 Süd (am Ufer) | **R 20 Nord (Platz)** | R 25 Nord (Platz) |
|---|---|---|---|---|---|
| Scheitel-Speed (KI) | 62 km/h | 33 km/h | 41 km/h | 42 km/h Einfahrt, 47 km/h Kehre | 48 / 54 km/h |
| Lenkeinschlag | 40 % | **100 %** (Anschlag) | 91 % | 69 % | 55 % |
| Bremsen vor der Platz-Einfahrt | – | 330 → 50 km/h auf 233 m | dito | 330 → 42 km/h auf 244 m | dito |
| Runde (KI, fliegend) | 130,8 s | 148,1 s | 148,7 s | 143,2 s | 144,0 s |
| Länge | 7,63 km | 8,29 km | 8,31 km | 8,15 km | 8,17 km |
| Bosporus im Bild (Platz/Kehre) | 0 % | 49 % | 52 % | 34 % | 32 % |
| Moschee im Bild (Platz/Kehre) | 0 % | 21 % | 25 % | 20 % | 22 % |
| Brückenpylon im Bild (Anfahrt Muallim Naci) | 0 % | 70 % | 70 % | 70 % | 70 % |
| Abstand zu Gebäuden | – | Rückweg schneidet Ostbebauung (30–40 m) | schneidet Moschee-Ecke und Ostbebauung | ≥ 4,5 m, Moschee ~55 m | Barriere direkt an Fassaden |
| Kollisionsfläche / KI-Runde | ok | ok / gültig | ok / gültig | ok / gültig | ok / gültig |

Für alle Varianten gilt:
- **Breiten:**
  - Muallim Naci Cd. (Engstelle): 2 × 9 m mit 0,3 m Auslauf zur Mittelbarriere, Spurachsen ≥ 14,8 m auseinander
  - Platz: 10 m Fahrbahnbreite
  - Kollisionsfläche ohne Überschneidung: 0,0 mm Abweichung vom Entwurf
- **Eingriffe:** 4 kleine, nicht denkmalgeschützte Gebäude an der Nordseite der Muallim Naci (zusammen 293 m²). Moschee, Etz-Ahayim-Synagoge, alle historischen Gebäude und die Küste bleiben unberührt.
- **Kurven:** Die Ortaköy-Kehre entfällt. Neu sind Muallim-Naci-Knick, Meydan-Einfahrt, Meydan-Kehre und Meydan-Ausfahrt: 13 → 16 Kurven. Die Länge liegt dann über der bisherigen Vorgabe von 7,5–7,8 km; der Test wird angepasst.
- **Höhenprofil:** Das Profil der Studie ist provisorisch. Bei der Umsetzung entwirft es `vertical_alignment.py` gegen die Fahrzeuggrenzen, wie beim Rest der Strecke. Die Kuppe am Palanga-Anschluss wird dabei entschärft.
- **KI und Checkpoints:** Die Ideallinie wird aus der Geometrie neu berechnet (`RacingLine`). Die Sektorgrenzen sind Rundenanteile und verschieben sich mit. Die Kurvenliste in `build_track_a.py` wird ergänzt.

Bewertung:
- **R 10 Süd:** Die Kehre steht am Lenkanschlag, eine Parkhauskurve. Abgelehnt.
- **R 15 Süd:** die beste Wassersicht, aber der Fahrbahnrand schneidet die Moschee und die Ostbebauung. In dieser Form nicht machbar.
- **R 25 Nord:** Die Barrieren stehen ohne Abstand an den Fassaden. Zu knapp.
- **R 20 Nord (empfohlen):** Die Kehre nutzt die offene Nordfläche des Platzes mit ≥ 4,5 m Luft und lässt Moschee, Südplatz und Ufer frei. Dort ist Platz für Zuschauer mit Fahnen vor Moschee und Bosporus. Fahrerisch:
  - starkes Bremsen (330 → 42 km/h) vor der Platz-Einfahrt, mit dem Brückenpylon im Bild
  - präzises Einlenken rechts, dann eine lange, 69 % eingeschlagene Linkskehre
  - Beschleunigen bergauf zurück auf die Muallim Naci
- **Schwächen der Empfehlung:**
  - Einfahrt und Kehre sind ein zusammenhängender langsamer Komplex (42–47 km/h). Das ist langsamer als die heutige Kehre (62 km/h), aber auf Monaco-Niveau.
  - Der Bosporus ist dort zu einem Drittel im Bild, nicht zur Hälfte.

Karten: `docs/img/waterfront_hairpin_variants.jpg` (Vergleich), `docs/img/waterfront_square_free.jpg` (Freiraum des Platzes).

## 5. R20 Platz-Nord – Umsetzung (2026-10-31)
**Status:** implementiert; automatisch getestet (Simulation, Kollisionsfläche, KI- und Tastaturrunden). **In Unity noch nicht gefahren.**

- **Dateien:**
  - `Resources/Tracks/bosphorus_a.json` = Bosphorus Grand Prix mit R20.
  - `Resources/Tracks/bosphorus_a_classic.json` = die bisherige Strecke mit der Ortaköy-Kehre, unverändert. Sie ist in der Rennauswahl als „Bosphorus Grand Prix (klassische Ortaköy-Kehre)" wählbar und nutzt dieselbe Stadt (`city_id`).
  - Rücksprung auf die alte Strecke als Hauptkurs: `python3 tools/geo/build_track_a.py --layout classic` erzeugt sie bitgenau neu.
- **Erzeugung:** `tools/geo/ortakoy_r20.py` (Linie, Breiten, Kurven) und `build_track_a.py --layout r20` (Standard).
- **Kennzahlen:** 8,16 km, 16 Kurven, KI-Runde 142,0 s (vorher 130,8 s).
  - Tastatur: Runde 143,7–143,9 s trocken, 160,9–161,0 s nass, in allen drei Profilen ohne Dreher und ohne Abflug.

**Linienführung, gegenüber der Studie nachgeschärft:**
- **Abzweig:** Die Strecke verlässt die Çırağan Cd. 40 m früher, noch auf der Geraden. Dadurch ist die Muallim-Naci-Kurve eine einzige gleichmäßige Rechtskurve mit R ≈ 137 m, ohne Gegenschwenk.
- **Rückweg:** Er läuft in einem sanften Bogen (R > 110 m) über die Fläche der alten Kehre in die Palanga-Steigung.
  - Eine Zwischenfassung hatte dort ein S mit R ≈ 35 m auf 11 % Steigung, an dem sich trockene Tastaturrunden drehten.
- **Engstelle:** Beide Fahrbahnen liegen so weit nördlich, wie die Straße erlaubt.
  - Fahrbahnen 9,5 m, 1 m Auslauf zur Mittelwand.
  - Barriere an der Südseite 0,3 m neben der Fahrbahn.
- **Kerbs:** Die Muallim-Naci-Kurve hat keine (`"kerbs": false` in der Kurvenliste). Sie wird unter Vollbremsung gefahren, und ein Kerb an der Mittelwand brachte das Auto dort aus dem Gleichgewicht.

**Gebäude** (geprüft mit `Apex.Tuning clearance-buildings`, gleiche Regel wie im Spiel: 3 m hinter der Barriere):

| Gruppe | Gebäude |
|---|---|
| Weichen zusätzlich zur alten Strecke | 2 kleine Gebäude an der Nordseite der Muallim Naci (OSM 415084314, 415091034; 59 + 65 m²), beide aus der freigegebenen Liste |
| Weichen wie schon bei der alten Kehre | die Bebauung im Kehrenbereich (u. a. 1414612661/2/3, 415085111/2) |
| Bleiben stehen | Etz-Ahayim-Synagoge (4,7 m Luft), die Wohnhäuser an der Südseite (3,7 m) und der Eckblock an der Platzeinfahrt (4,0 m) |

  Eine Zwischenfassung hätte diese drei Gebäude verdrängt; das ist korrigiert. Moschee und Küste sind unberührt.

**Höhenprofil:**
- **Entwurf:** neu mit `vertical_alignment.py` gegen die Fahrzeuggrenzen; die Grenzen sind nach dem Umbau zweimal neu erzeugt.
- **Ortaköy-Fenster:** Dort gilt auch für Vollgas die halbe Grenze, für Kuppen genauso streng wie für Wannen. Das Raster wird als C2-Spline ausgewertet.
- **Spurkopplung:** Wo die Spuren zusammenlaufen, setzt die Höhenkopplung weich ein (bis 30 cm bei 17 m Achsabstand, 5 cm ab 13 m).
- **Ergebnis `validate-road`:**
  - 0 kritisch; im neuen Abschnitt keine Warnung
  - 5 knappe Wannen-Warnungen auf alten Abschnitten, je bis 11 % über der Grenze (alte Strecke: 4, bis 54 %)
  - Kollisionsfläche 0,0 mm vom Entwurf

**KI im Regen (Simulation, betrifft alle Strecken):**
- **Bisher:** Die KI skalierte ihr trockenes Kurventempo im Regen mit √Grip. Das überschätzt Kurven, die von Abtrieb leben, weil bei weniger Tempo auch weniger Abtrieb wirkt.
- **Jetzt:** Sie rechnet im Regen ein eigenes Profil mit dem nassen Reibwert (`RacingLine.GripSpeedAt(d, grip)`). Nass-Runden werden dadurch etwas langsamer und stabiler.

**Nicht geprüft (braucht Unity):**
- Optik, Kamerablicke, Barrieren und Möbel am Platz
- echte Fahrt mit Tastatur, Gamepad und Lenkrad
- Bildrate
- **Karte:** `docs/img/waterfront_r20_final.png` (alte und neue Linie, Barrierenlinien, verdrängte und geschützte Gebäude).
