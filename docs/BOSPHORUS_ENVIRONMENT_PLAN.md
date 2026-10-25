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
| # | Schritt | Umsetzung | Status |
|---|---|---|---|
| 5 | 500-m-Uferprototyp | Abschnitt A (Start/Ziel, Beşiktaş-Ufer) in voller Dichte: Tribünen am Wasser, Boxen und Hospitality, Zäune, Bürgersteige, Laternen, Straßenschilder, fiktive Sponsoren | implementiert (Code), Sichtprüfung im Editor offen |
| 6 | Wahrzeichen | Brücke, Ortaköy-Moschee, Dolmabahçe, Çırağan, Beylerbeyi, Galata-Turm, Leanderturm, Sultan-Ahmed-Moschee, Hagia Sophia: vereinfachte **eigene** Silhouettenmodelle an den Geo-Positionen | implementiert (Platzhalter-Silhouetten, nicht fotorealistisch) |
| 7 | Stadt und Skyline | prozedurale Istanbuler Wohnblöcke (Putzfarben, Ziegeldächer, Fensterbänder) auf dem Hang, asiatisches Ufer mit Hügeln und Häuserteppich, historische Halbinsel als ferne Masse | implementiert (Platzhalter) |
| 8 | Flaggen, Fähren, Zuschauer | türkische Flaggen (exakte Geometrie, Stoffanimation, gemeinsame Windrichtung), Fähren auf der realen Linie Beşiktaş–Üsküdar, Schiffe im Bosporus, Zuschauer als Instanzen, Möwen | implementiert |
| 9 | Goldene Stunde | Sonnenrichtung aus Wahr-Nord, warmes Licht auf den europäischen Fassaden | implementiert |
| 10 | Nacht und Nässe | Nachtpreset mit Straßenlaternen, Fensterlicht, Brückenbeleuchtung; nasse Fassaden und Straßen | implementiert |
| 11 | Profiling auf Geräten | Telemetrie zeigt die Frametime; **Messung auf echter Hardware steht aus** | geplant |
| 12 | Ausweitung auf den ganzen Kurs | Stadt- und Wahrzeichen-Schicht ist kursweit; volle Dichte nur in Abschnitt A | teilweise |

## Ehrliche Grenzen
- Alle Gebäude und Wahrzeichen sind **prozedurale Platzhalter-Silhouetten**, nicht fotorealistisch. Fotorealismus braucht lizenzierte oder eigens modellierte Hero-Assets (Brücke, Moschee, Palast) nach Referenzfotos, Fassaden-Texturen und gebackenes Licht.
- Uferlinien und einige Positionen sind Näherungen (siehe Tabelle).
- Die Strecke selbst ist ein fiktiver, temporär gesperrter Kurs. Es wird nicht behauptet, dass diese Straßen so existieren.
