# Bosporus-Brücke – Bewertung vor dem Kauf

**Stand:** 2026-10-26.

**Status:**
- **Bewertung offen, kein Kauf.**
- Die Produktseite (renderhub.com) ist aus der Arbeitsumgebung **gesperrt** (Netzwerkrichtlinie, HTTP 403). Vorschaubilder, Wireframes und der Lizenztext konnten deshalb **nicht selbst geprüft werden**.
- Was hier steht, stammt aus Suchergebnissen und ist als solches markiert.

## 1. Was über das Angebot bekannt ist (aus Suchergebnissen, ungeprüft)

| Punkt | Angabe | Bewertung |
|---|---|---|
| Anbieter, Plattform | BsTen, RenderHub, veröffentlicht 2022-02-18 | – |
| Preis | 40 $, zeitweise 28 $ | gering |
| Geometrie | 5.812.739 Polygone, 6.722.490 Vertices, „high poly / subdivision“ | **nicht spielfertig**. Höchstens Quelle für Retopologie und Baking. **Darf nie direkt ins Spiel** (Mobile-Budget für die ganze Szene: < 600k Dreiecke im Bild). |
| Texturen | „PBR 2K PNG“, UV-gemappt | für eine 1,5-km-Brücke zu grob für Nahsicht; neu baken bzw. Trim-Sheets |
| Formate | Blender (EEVEE-Szene), FBX, OBJ, STL | ok (Blender als Quelle) |
| Lizenz | „Extended Use License“ der Plattform | **prüfen:** Einbau in ein verkauftes Spiel, Weitergabe als kompilierte Datei, Änderung erlaubt? |
| Dateigröße | ~1,9 GB | Quelle, nicht für das Repo |

## 2. Referenzdaten für die Geometrieprüfung
Quellen: Wikipedia, Structurae, ICE.

- **Bauart:** erdverankerte Hängebrücke mit Stahlpylonen und **schrägen (zickzack) Hängern**, aerodynamischem Stahl-Deck (in mindestens einer Quelle als Kastenträger beschrieben).
- **Hauptmaße:**
  - Hauptspannweite 1.074 m
  - Pylonhöhe 165 m (Bezugspunkt in den Quellen uneinheitlich: über Wasser oder über Deck)
  - Deckbreite 33,4 m
  - lichte Höhe ca. 64 m
- **Planung:** Freeman Fox & Partners (Gilbert Roberts, William Brown, Michael Parsons); eröffnet 1973.
- **Abnahme:** Das Modell muss diese Maße auf ±1 % treffen. Das Hängerbild (schräg, nicht senkrecht) ist ein **K.-o.-Kriterium**: senkrechte Hänger wären eine andere Brücke.

## 3. Prüfliste vor dem Kauf (vom Product Owner bzw. mit Zugriff auf die Seite)
1. **Vorschaubilder und Wireframe:** Pylonform (Portalrahmen mit Querriegeln), Sattel oben, Hauptkabel-Durchhang, schräge Hänger, Deckquerschnitt, Verankerungen an beiden Ufern, Zufahrtsviadukte.
2. **Topologie:** Ist das Modell aus Teilen aufgebaut (Pylon, Deck-Segment, Kabel, Hänger) oder ein verschmolzenes Netz? Saubere Teile vereinfachen die Retopologie erheblich.
3. **Maßstab:** reale Meter? Spannweite 1.074 m nachmessbar?
4. **Lizenztext** (Kopie in `docs/licenses/` ablegen):
   - kommerzielle Nutzung in einem verkauften Spiel
   - Änderung erlaubt
   - keine Pflicht zur Quellenweitergabe
   - Namensnennung nötig?
   - KI-Klauseln
5. **Anfrage an den Anbieter:** Wireframe-Screenshots, Polygonzahl pro Teil, Herkunft (eigene Modellierung, keine Scans oder fremden Daten).

## 4. Optimierungsplan (falls gekauft)

| Stufe | Inhalt | Budget (Dreiecke) | Sichtweite |
|---|---|---|---|
| LOD0 | Pylone mit Querriegeln, Sättel, Deck-Kastenträger mit Geländer als Alpha-Karte, Hauptkabel als Rohr (12 Segmente), Hänger als dünne Quads mit Normal-Map | ~80k + Hänger-Instanzen | < 400 m (selten: die Brücke ist in Ortaköy 300–700 m entfernt) |
| LOD1 | Pylone vereinfacht, Deck als Kasten, Kabel 6 Segmente, Hänger als ein Linien-Mesh pro Feld | ~25k | 400–1.500 m |
| LOD2 | Silhouette, Kabel als Linien | ~6k | 1,5–4 km |
| Impostor | Billboard bzw. Oktaeder-Impostor der Pylone | < 500 | > 4 km |
| Nachtlicht | emissive Linien entlang Deck und Kabel (wie heute `BridgeLights`) | – | alle |

- **Texturen:**
  - Stahl-Trim-Sheet 2K (Mobile) bzw. 4K (PC)
  - Detail-Normal für Nieten und Bleche
  - AO aus dem High-Poly gebacken
  - ASTC 6×6 (Mobile), BC7 (PC)
- **Arbeitsschritte:**
  1. Import in Blender, Maße prüfen
  2. Teile trennen
  3. LOD0 retopologisieren
  4. Normal- und AO-Maps baken
  5. LOD1/2 per Decimate und Handarbeit
  6. FBX-Export (Meter, +Y oben, +Z Europa → Asien)
  7. Prefab `Resources/Landmarks/bridge_15temmuz` mit LODGroup (Cross-Fade)
- **Aufwand und Kosten (Schätzung):** 2–3 Wochen Retopologie und Baking; extern ca. 2.000–5.000 €, intern entsprechend Zeit. Dazu der Modellpreis 28–40 $.

## 5. Alternative: eigene parametrische Brücke in Produktionsqualität
- **Idee:** Eine Hängebrücke ist weitgehend durch Parameter bestimmt (Spannweiten, Pylonmaße, Kabelkettenlinie, Hängerwinkel, Deckquerschnitt). Hier zählen genaue Referenzdaten mehr als freies Modellieren.
- **Ausgangslage:** Die heutige `LandmarkGeometry.Bridge` ist ein grober Platzhalter, aber schon parametrisch und maßstäblich (1.074 m Spannweite, 165-m-Pylone, 64 m lichte Höhe).
- **Ausbau:**
  - echter Pylon-Querschnitt mit Querriegeln
  - Kastenträger-Deck mit Geländer und Fahrbahn
  - Kettenlinie der Hauptkabel, schräge Hänger mit korrektem Winkel
  - LODs automatisch
  - PBR-Stahl aus CC0-Material plus gebackene Details
- **Vorteile:** keine Lizenzfrage am Modell, exakt unsere Maße, LODs ohne Retopologie.
- **Nachteil:** Kleinteile (Sättel, Nieten, Verankerungsbauwerke) müssen trotzdem modelliert werden.
- **Grenzen der Ehrlichkeit:** Ob das Ergebnis „nahezu fotorealistisch“ wirkt, hängt vor allem an Materialien und Licht. Bewertet wird das erst im Unity-Render.
- **Aufwand (Schätzung):** 1,5–2,5 Wochen intern.

## 6. Empfehlung
1. **Zuerst Prüfliste 3** mit Zugriff auf die Seite (durch dich oder nach Freigabe der Domain in den Netzwerkeinstellungen der Umgebung).
2. Ist das Modell sauber in Teile gegliedert, maßstäblich und hat schräge Hänger → **Kauf (28–40 $) als High-Poly-Quelle**, dann Optimierungsplan 4.
3. Sonst → **Alternative 5**, die eigene parametrische Brücke.
4. **Unabhängig davon** die rechtliche Frage zur Darstellung des Bauwerks von 1973 klären (siehe `ORTAKOY_VERTICAL_SLICE_PLAN.md`).

## Quellen
- [RenderHub – Bosphorus Bridge (BsTen)](https://www.renderhub.com/bsten/bosphorus-bridge), nicht abrufbar, Angaben aus Suchergebnissen
- [Wikipedia – Bosphorus Bridge](https://en.wikipedia.org/wiki/Bosphorus_Bridge)
- [Structurae – Bosporus Bridge](https://structurae.net/en/structures/bosporus-bridge)
- [ICE – Bosphorus bridges](https://www.ice.org.uk/what-is-civil-engineering/what-do-civil-engineers-do/bosphorus-bridges)
