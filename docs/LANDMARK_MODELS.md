# Eigene Wahrzeichen-Modelle (Phase 4)

**Stand:** 2026-10-26. **Status:** implementiert und offline gesehen (three.js-Renders, Kompiliertests, Tests grün). Im Unity-Editor noch nicht geprüft.

Alle Modelle sind **eigener prozeduraler Code**: keine gekauften Assets, keine Scans, keine Google-Daten.
- **Grundlage:**
  - Grundrisse aus OSM (ODbL, siehe `CITY_FROM_OSM.md`)
  - veröffentlichte Eckdaten (Brücke: Spannweiten und Höhen; Uhrturm: 27 m auf 8,5 m Grundfläche)
- **Fassadengliederung:** typisch für die Balyan-Bauten nachempfunden, nicht vermessen. Viele Höhen sind Schätzungen; sie stehen im Code.
- Die Modelle sind detailliert, aber **nicht fotorealistisch**. Die Abnahme bleibt beim Motorsport-Foto-Test im Masterplan.

Code: `Runtime/Game/Environment/LandmarkModels.cs` mit Fassaden-Kit, Moschee, Paläste, Reihenhäuser und Uhrturm, außerdem `LandmarkGeometry.Bridge`.

## LOD-Stufen
Jedes Modell entsteht aus demselben Code in drei Detailstufen und hängt an einer `LODGroup` (`EnvironmentBuilder.PlaceDetailed`).

- **Detailstufen:**
  - **0 (nah):** Säulen, Fenstersprossen, gedrechselte Baluster, Geländerstäbe, Zahnschnitt
  - **1 (mittel):** Gesimse und Rahmen, weniger Segmente
  - **2 (fern):** Baukörper und Fensterbänder
- **Umschaltpunkte** (Bildschirmhöhe des Objekts):

  | Objekt | LOD 0 | LOD 1 | LOD 2 |
  |---|---|---|---|
  | Moschee | 0,25 | 0,06 | 0,004 |
  | Palastgebäude (je Gebäude) | 0,3 | 0,08 | 0,01 |
  | Reihenhäuser | 0,12 | 0,03 | 0,004 |

- **Autoren-Modell:** Liegt unter `Resources/Landmarks/<id>` ein echtes Modell, ersetzt es das prozedurale (Drop-in bleibt erhalten).

## Modelle

| Wahrzeichen | Grundlage | Dreiecke (LOD 0 / 1 / 2) | Inhalt |
|---|---|---|---|
| **Ortaköy-Moschee** (Büyük Mecidiye, 1853–56) | OSM-Grundriss: Halle 21,2 m im Quadrat, Achse 140,8°; Minarettblock 30 × 6 m; U-förmiger Hünkâr-Pavillon | 50.800 / 13.700 / 4.400 | siehe unten |
| **Dolmabahçe-Palast** | OSM-Grundrisse: Selamlık, Muayede-Saal, Harem, Veliaht-Flügel, Tore, Dienstgebäude | gesamt ca. 241.000 / 136.000 (je Gebäude eigenes LOD) | siehe unten |
| **Dolmabahçe-Uhrturm** | OSM-Position; 27 m, 8,5 m Grundfläche | 13.700 / 9.800 / 1.000 | siehe unten |
| **Çırağan-Palast** | OSM-Grundriss (122 × 42 m), 17 m | ca. 49.000 / 27.000 | dasselbe Fassadensystem; das Kempinski-Hotel und der Fußgängersteg bleiben generisch |
| **Akaretler-Reihenhäuser** (1875) | 18 OSM-Grundrisse an der Şair Nedim Caddesi | 43.800 / 22.200 / 5.600 (alle 18) | siehe unten |
| **15-Juli-Märtyrerbrücke** | Spannweiten und Höhen wie zuvor (1.074 m Hauptfeld, 165-m-Pylone) | 18.800 (nah) | siehe unten |

**Ortaköy-Moschee:**
- Halle: drei Schaufassaden mit großem Bogenfenster (Steinpfosten, zwei Kämpfer, Speichen im Bogenfeld), Gesims als Bogen über dem Mittelfeld, Eckpfeiler mit Säulenpaaren, Balustraden und Urnen
- Tambour mit 16 Bogenfenstern und Strebepfeilern, Bleikuppel, Laterne, vergoldeter Alem
- zwei schlanke Minarette mit Şerefe-Balustrade und Bleikegel
- Pavillon mit Portal, Säulen und Walmdächern
- Kaiterrasse mit Eisengeländer

**Dolmabahçe-Palast:**
- jede Grundrisskante über 4 m mit modellierter Fassade: rustizierter Sockel, Bogenfenster im Erdgeschoss, Fenster mit Dreiecks- bzw. Segmentgiebeln, Pilaster, Gurtgesimse, Hauptgesims, Balustrade mit Urnen
- lange Fronten mit Mittelrisalit: Säulenpaare und Giebel
- Kuppel auf Fenstertambour über dem Muayede-Saal (ca. 36 m)

**Dolmabahçe-Uhrturm:**
- vier Geschosse mit Säulenpaaren, Bogenöffnungen und Giebel
- vier Zifferblätter mit Goldrand
- oktogonale Laterne und Kuppel
- Plattform mit Eckbecken

**Akaretler-Reihenhäuser:**
- rustiziertes Erdgeschoss mit Bogentür, Stufen und Fenster
- Obergeschoss mit Eisenbalkon auf Konsolen und Dreiecksgiebeln, zweites Obergeschoss mit Segmentgiebeln
- Eckpilaster, Zahnschnittgesims, Attika
- Gartenseite mit einfachen Fenstern

**15-Juli-Märtyrerbrücke:**
- neu: schräge Hänger im Zickzack-Muster (Paare treffen sich am Deck)
- Fahrbahn mit drei Spuren je Richtung, Markierungen, Mittelleitwand und Leitplanken
- Gehweggeländer, Laternen alle 36 m (nachts leuchtend)
- Sattelgehäuse auf den Pylonen, Ankerblöcke
- Die rot-weiße Nachtbeleuchtung nutzt dieselben Hängerpunkte.

## Ergänzungen (2026-10-27)
- **Dolmabahçe-Prunktore** (Saltanat- und Hazine-Kapısı; `LandmarkGeometry.GateMonument`):
  - Triumphbogen-Block auf dem OSM-Grundriss: Durchfahrtsbogen mit Bogenlaibung und schmiedeeisernem Tor mit vergoldeten Spitzen
  - auf beiden Seiten Säulenpaare auf Postamenten, Nischen mit Giebel in den Seitenfeldern, tiefes Gesims
  - Attika mit vergoldetem Medaillon (ohne Inschrift) unter einem Segmentgiebel, Urnen
  - 4.900 / 2.900 / 900 Dreiecke; Höhe 14 m geschätzt
  - Erkannt als „Kapı“ mit schlankem Grundriss (Längen-Breiten-Verhältnis ≥ 2). Das fast quadratische Koltuk Kapısı bleibt ein Dienstgebäude.
- **Steintexturen** (`MasonryTextures`, prozedural, keine externen Dateien):
  - Marmor-Quaderwerk für Paläste, Tore, Uhrturm und Moschee: Lagen 0,6 m, Blöcke 1,2 m im Halbverband, 8-mm-Fugen, leichte Tönung je Block, feine Äderung, Normalmap; Wiederholung alle 2,4 m
  - Kalkputz mit Kellenstruktur und Regenspuren für die Akaretler-Häuser; Wiederholung alle 2 m
  - Die Modelle bekommen metrische UVs (Box-Projektion). Auf Wänden schräg zu den Weltachsen wird die Textur bis zum Faktor 1,41 gestreckt (bei 45°).
  - Tests: nahtlos kachelbar, Fugenanteil 1–12 %, Helligkeit im erwarteten Bereich.
  - Vorschau: `dotnet run -- <dir> --masonry` schreibt PPMs. Für `kit.html` als PNG in `tools/carrender/masonry/` ablegen und `&masonry=1` anhängen.

## Zuordnung aus den Daten
- **Paläste:** `OsmCity.ClassicalKind` erkennt sie am OSM-Namen.
  - Palastgebäude: Name enthält „Saray“ bzw. Gebäudetyp `palace`.
  - Dienstgebäude: „Daire“, „Köşk“, „Kapı“, aber nur im Umkreis von 400 m eines Palastes; eine „Vergi Dairesi“ bleibt generisch.
  - Höhen: `ClassicalHeight` (Selamlık 21 m, Muayede 26 m bis zum Gesims, Harem 17 m, Çırağan 17 m).
- **Akaretler:** OSM nennt die Reihenhäuser nicht. `tools/geo/build_city_a.py` markiert sie nach Ort und Form (`ENSEMBLES`: dreigeschossig, unter 130 m², im Umkreis der Reihe).
- **Strecke:** Von den 18 Häusern geben die vier am Südende der Streckenecke nach, denn kein Gebäude darf näher als 3 m an die Sperrzone.
- **Platzhalter entfernt:** Die alten Platzhalterklötze von Dolmabahçe und Çırağan entfallen, wenn OSM-Daten vorhanden sind. Die Reservierungsradien für Paläste sind entfernt, die Nachbarschaft bleibt also echte Stadt.
- **Test:** `ThePalacesStandOnTheirOwnFootprints` prüft:
  - Selamlık, Harem, Muayede und Çırağan sind vorhanden.
  - Es gibt einen Uhrturm und 14 Reihenhäuser.
  - Das Kempinski-Hotel bleibt generisch.
  - Kein Palast liegt in der Sperrzone.
  - Dienstgebäude stehen nur an Palästen.

## Vorschau (ohne Unity)
```sh
cd build/Apex.ModelPreview && dotnet run -c Release -- /tmp/lm --landmarks   # ortakoy_lod*, clocktower_lod*, dolmabahce_osm_lod*, ciragan_osm_lod*, akaretler_lod*, bridge
cd tools/carrender && APEX_PAGE=kit.html APEX_VIEWS='name|eye|target|fov' node render.mjs /tmp/lm/ortakoy_lod0.obj /tmp/lm/ort
```
Bilder: `docs/img/landmark_*.jpg`. three.js mit Abendsonne, nicht die Unity-Darstellung.

## Offen
- **Kosten:** In Unity prüfen: Bild, Bildrate, LOD-Sprünge. Dolmabahçe hat im LOD 1 noch viele Dreiecke; nach dem Profiling (Phase 7) eventuell eine zusätzliche Impostor-Stufe.
- **Dolmabahçe:** Kai-Gitter am Wasser, reichere Reliefs (Rosetten, Girlanden).
- **Materialien:** Die Bleidächer haben noch keine Stehfalz-Textur. Gescannte Marmor-Sätze (CC0) wären eine mögliche Verbesserung gegenüber der prozeduralen Textur.
- **Moschee:** Innenraum-Andeutung hinter den Fenstern (Interior Mapping).
