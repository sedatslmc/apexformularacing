# Bosphorus Grand Prix – Umgebungsplan

Stand: 2026-10-25. Auftrag: „Bosphorus Grand Prix Environment Master Brief“.
Status je Schritt: **geplant / implementiert / getestet / poliert**.

## 1. Bestand (Inspektion)
- **Strecke:** 20 Kurven, 5,1 km, 3 Sektoren, Boxengasse, Startaufstellung, Kurvenschilder (Graybox, Daten in `bosphorus_gp.json`).
- **Umgebung vorher:**
  - Gelände aus Höhen-Interpolation, flache Wasserfläche östlich einer schematischen Uferlinie
  - Mauern, Auslauf, Kerbs, Boxengebäude
  - **keine** Gebäude, Wahrzeichen, Schiffe, Flaggen, Zuschauer, kein asiatisches Ufer, keine Nachtbeleuchtung
- Die bisherigen Koordinaten waren ausdrücklich „schematisch, nicht georeferenziert“.

## 2. Geografische Referenzdaten
Verifiziert per Websuche (Wikipedia, Structurae, Pleiades/OSM):

| Ort | Breite | Länge | Quelle | Status |
|---|---|---|---|---|
| Dolmabahçe-Palast | 41,03944 | 29,00167 | Wikipedia | geprüft |
| Ortaköy-Moschee | 41,04694 | 29,02694 | Wikipedia | geprüft |
| 15-Juli-Märtyrerbrücke, europ. Ende | 41,05102 | 29,02858 | Structurae | geprüft |
| 15-Juli-Märtyrerbrücke, asiat. Ende | 41,03998 | 29,04010 | Structurae | geprüft |
| Galata-Turm | 41,02566 | 28,97416 | Wikipedia | geprüft |
| Leanderturm (Kız Kulesi) | 41,02117 | 29,00425 | Wikipedia | geprüft |
| Sultan-Ahmed-Moschee | 41,00526 | 28,97687 | Pleiades/OSM | geprüft |
| Hagia Sophia, Beşiktaş-/Üsküdar-Anleger, Çırağan, Beylerbeyi | – | – | Näherung | **nicht geprüft** |
| Uferlinien (europäisch/asiatisch) | – | – | Näherung (±50–100 m) | **nicht geprüft**; vor dem finalen Art-Pass durch OSM-Küstenlinie ersetzen (ODbL, Namensnennung) |

**Brücke:** Hauptspannweite 1.074 m, Pylone 165 m, Durchfahrtshöhe 64 m (Wikipedia/Structurae). Aus den Endpunkten ergibt sich eine Gesamtlänge von ~1.563 m. Das stimmt mit der veröffentlichten Länge (~1.560 m) überein.

**Flagge:** Gesetz Nr. 2994 (1936) und Verordnung 85/9034:
- Seitenverhältnis 2:3, Rot ≈ Pantone 186 C
- äußerer Halbmondkreis: Durchmesser ½G, Mittelpunkt ½G vom Liek
- innerer Kreis: Durchmesser 2/5 G, um 1/16 G zum Flugende versetzt
- Stern: in einem Kreis mit Durchmesser ¼G, der die Verbindungslinie der Halbmondspitzen berührt; eine Spitze zeigt zum Liek
- Einfass 1/30 G

Die Sternausrichtung ist aus Sekundärquellen (FOTW) abgeleitet. Die amtliche Konstruktionszeichnung konnte ich nicht öffnen.

## 3. Geografischer Plan
`tools/bosphorus_geo.py` legt die echten Koordinaten **maßstabsgetreu (1:1)** mit einer einzigen Drehung und Verschiebung auf die Strecke.
- **Erhalten bleiben:** Abstände, die Reihenfolge entlang des Ufers (Dolmabahçe → Beşiktaş → Çırağan → Ortaköy → Brücke) und die Blickrichtungen zwischen den Orten.
- **Gedreht ist nur der Kompass:** Wahr-Nord zeigt im Spiel auf 308°. Sonnenstand und Kompass werden entsprechend gedreht.
- Ergebnis: `docs/img/bosphorus_geo_plan.png`, Daten im Block `geo` von `bosphorus_gp.json`.

| Streckenabschnitt | reale Entsprechung | Wahrzeichen im Blick |
|---|---|---|
| A Start/Ziel, Boxen, Paddock | Beşiktaş-Ufer, Anleger direkt nebenan | Dolmabahçe südlich, Üsküdar und Leanderturm gegenüber, Brücke im Norden |
| B Bosporus-Schwung | Çırağan-Ufer | asiatisches Ufer, Beylerbeyi, Brücke |
| C Ortaköy-Anstieg / Kuppe | Hang oberhalb Ortaköy | **Moschee ~550 m entfernt am Wasser, Brücke dahinter** (Signaturmoment) |
| D/E Hügel-Esses, Haarnadel | Yıldız-/Ortaköy-Hügel | Stadtdächer, fern die Brücke |
| F (vorher „Fährterminal“) | **Yıldız-Hang**, 400–500 m im Landesinneren | dicht bebaut, Steinmauern |

**Korrektur am Layout:** Abschnitt F liegt bei dieser geografisch korrekten Platzierung im Landesinneren. Ein Fährterminal dort wäre falsch. Deshalb heißt er jetzt „Yıldız-Abfahrt / Steinschikane“, die Kurven T13–T15 heißen „Yıldız 1–3“. Das Fährterminal steht am realen Ort, dem Beşiktaş-Anleger neben Start und Ziel.
- Die Streckengeometrie selbst ist unverändert. Damit bleiben Fahrfluss, Längen- und Kurventests, KI-Linie und Boxen-Logik gültig.
- Ein neuer Test prüft die Geografie: Brückenlänge, Moschee südlich des Brückenkopfes, Strecke ≥ 10 m vom Wasser, Wahrzeichen nicht auf der Strecke, asiatisches Ufer gegenüber.

## 4. Fahrgeometrie und Fahrfluss
- **Unverändert:** 20 Kurven, 3 Sektoren, zwei Überholzonen (T1 nach der Uferstraße, T12-Haarnadel bergab) plus T16–T18
- KI und Tastatur-Runden sind getestet (`KeyboardDrivingTests`, `AIFieldRacesCleanlyOnRig`).
- Mindestabstand der Strecke zum Wasser: 21 m.

## 5.–12. Umsetzung (Reihenfolge laut Briefing)
Statusstufen: **implementiert** = Code steht und kompiliert gegen die Unity-Referenzen; **getestet** = NUnit-Tests decken die Logik ab.
Im Unity-6-Editor gesehen habe ich die Szene noch **nicht**. Die Bilder unten stammen aus dem Offline-Vorschauwerkzeug (three.js), nicht aus dem Spiel.

| # | Schritt | Umsetzung | Status |
|---|---|---|---|
| 5 | 500-m-Uferprototyp | Start/Ziel-Bereich: Tribünen zwischen Barriere und Kai (Platzprüfung gegen Wasser und Strecke), Zuschauer, LED-Tafeln, gepflastertes Paddock mit drei Hospitality-Gebäuden, Gehwege, Fangzäune, Laternen, türkische Straßenschilder (weiß auf blau), Banner mit erfundenen Sponsoren, große Flaggen an den Tribünenenden | implementiert; Sichtprüfung im Editor offen |
| 6 | Wahrzeichen | Brücke, Ortaköy-Moschee (auf Kaiplattform, Achse Richtung Qibla), Dolmabahçe mit Uhrturm, Çırağan, Beylerbeyi, Galata-Turm, Leanderturm, Sultan-Ahmed-Moschee, Hagia Sophia. Alle an den Geo-Positionen, Paläste entlang der Uferlinie ausgerichtet | implementiert (Platzhalter-Silhouetten) |
| 7 | Stadt und Skyline | `CityLayout`: Wohnblöcke mit Istanbuler Putzfarben, Ziegel-Walmdächern oder Flachdächern mit Wassertanks; Fassaden zeigen hangabwärts zum Wasser. Drei Dichtezonen (nah/mittel/fern), Levent-Hochhäuser im Norden. Landmassen: europäisches Ufer, asiatisches Ufer mit Hügelrücken, historische Halbinsel jenseits des Goldenen Horns. Sichtachsen (z. B. Ortaköy-Kamm → Moschee/Brücke) bleiben frei | implementiert, getestet |
| 8 | Flaggen, Fähren, Zuschauer | ~11 türkische Flaggen (exakte Geometrie, analytischer Stoff: hängt bei Flaute, weht bei Wind, Wellen werden zum freien Ende stärker; gemeinsamer Wind aus NNO, Poyraz); große Flaggen an Tribünen und Palast, kleine am Startportal und Hospitality, ferne am Galata- und Leanderturm mit reduzierter Aktualisierung. Fähren Beşiktaş/Karaköy/Eminönü–Üsküdar mit Liegezeit, Frachter im Fahrwasser. Zuschauer als GPU-Instanzen, die jubeln, wenn ein Auto nah ist. Möwen, Bäume (Platanen, Schirmpinien, Zypressen) mit Windbewegung | implementiert, Kinematik getestet |
| 9 | Goldene Stunde | `SunModel`: Sonne aus WSW (wahr 252°, 11°), über Wahr-Nord ins Spiel gedreht. Beleuchtet die westseitigen Fassaden des asiatischen Ufers, Gegenlicht auf den europäischen Hängen | implementiert, getestet |
| 10 | Nacht und Nässe | Tageszeit „Nacht“ (Mond, Fensterlicht über Emissions-Textur, Laternen, Brückenlichter, angestrahlte Moscheen) und „Sonnenuntergang“; bei Regen dunklere, glattere Fassaden, Gehwege und Dächer; bei Bewölkung gehen Lichter früher an. Moderate Intensitäten, kein Neon | implementiert |
| – | Audio-Zonen | Prozedurale Loops für Wasser, Publikum, Stadt und Wind, gemischt nach Kameraposition (`AmbienceMixer`). Hallzonen (`AudioReverbZone`) in engen Häuserschluchten; diese färben auch den Motorsound. Tunnel oder Unterführungen gibt es im Layout nicht | implementiert, Mischlogik getestet |
| – | Qualitätsstufen | Einstellung „Umgebungsdetails“ (Niedrig/Mittel/Hoch): Blockdichte, Ausdehnung, Tribünenreihen, Zuschauerdichte, Zaunumfang, Laternenabstand, Schiffe, Möwen. LODGroups für die nahen Blöcke, ferne Blöcke vergröbert, verteilte Aktualisierung bei fernen Flaggen und Bäumen | implementiert |
| 11 | Profiling auf Geräten | Stadt-Dreiecke (LOD0, gesamt, nicht alle gleichzeitig sichtbar): Niedrig ~81 k, Mittel ~150 k, Hoch ~243 k; Fernterrain ~20 k. **Messung auf echter Hardware steht aus** | geplant |
| 12 | Ausweitung auf den ganzen Kurs | Stadt, Gehwege, Zäune und Laternen sind kursweit; Tribünen und Paddock-Detail nur im Start/Ziel-Bereich und an Bremszonen | teilweise |

### Vorschaubilder (Offline-Vorschau, nicht das Spiel)
- `docs/img/bosphorus_env_aerial.png`: Luftbild in der goldenen Stunde (Strecke, Ufer, Brücke, Levent, asiatisches Ufer)
- `docs/img/bosphorus_env_crest.png`: Blick vom Ortaköy-Kamm auf die Brücke
- `docs/img/bosphorus_env_night.png`: dieselbe Ansicht nachts
- `docs/img/bosphorus_env_straight.png`: Start/Ziel-Gerade (ohne Barrieren, Tribünen und Flaggen, diese erzeugt nur der Unity-Builder)

Erzeugen: `cd build/Apex.ModelPreview && dotnet run -c Release -- <dir> --environment --quality 1`, dann `APEX_PAGE=env.html node tools/carrender/render.mjs <dir>/environment.obj <prefix>` mit `APEX_VIEWS`.

## Ehrliche Grenzen
- Alle Gebäude und Wahrzeichen sind **prozedurale Platzhalter-Silhouetten**, nicht fotorealistisch. Fotorealismus braucht lizenzierte oder eigens modellierte Hero-Assets (Brücke, Moschee, Palast) nach Referenzfotos, Fassaden-Texturen und gebackenes Licht.
- Die Stadt ist generisch: Gleiche Blocktypen wiederholen sich, es gibt keine Straßenzüge zwischen den Blöcken, und die fernen Blöcke wirken flach.
- Das Wasser ist eine Ebene mit wandernder Normal-Map, ohne Wellengeometrie oder Uferschaum. Schiffe ziehen keine Bugwelle.
- Uferlinien, Landmassen und einige Positionen sind Näherungen (siehe Tabelle).
- Die Strecke selbst ist ein fiktiver, temporär gesperrter Kurs. Es wird nicht behauptet, dass diese Straßen so existieren.
