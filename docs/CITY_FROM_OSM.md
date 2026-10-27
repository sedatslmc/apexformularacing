# Echte Stadt aus OpenStreetMap (Bosphorus GP, Variante A)

**Stand:** 2026-10-26. **Status:** implementiert und getestet (Simulation, Kompiliertests); im Unity-Editor noch nicht gesehen.

## 1. Daten
- **Erzeugung:** `python3 tools/geo/build_city_a.py` schreibt `Runtime/Resources/City/bosphorus_a.json` (Radius 750 m um die Strecke).
- **Inhalt:**
  - 4.088 Gebäude mit Grundriss und Höhe
  - 2.081 Straßen und Wege
  - 277 Grünflächen
  - 47 Mauern
- **Höhen:** aus OSM-Tags (`height`, `building:levels` × 3,2 m), sonst der Median der getaggten Nachbarn im Umkreis von 300 m, ±1 Geschoss.
  - Das DEM wird für Gebäudehöhen nicht verwendet. Gegen 106 getaggte Gebäude lag es um den Faktor 2,8 zu niedrig (Korrelation 0,3).
- **Koordinatenrahmen:** wie die Strecke: x = Ost, z = Nord, Ursprung Dolmabahçe.
- **Lizenz:** ODbL 1.0, abgeleitete Datenbank (`ASSET_LICENSES.md`).

## 2. Gebäude (`OsmCity`, `EnvironmentGeometry.FootprintBuilding`)
- **Form:**
  - Grundrisse werden extrudiert und bekommen ein Gesims.
  - Flachdach mit Brüstung und Wassertanks; Walmdach bei vierseitigen Grundrissen.
- **Rendering:** Chunks von 192 m mit zwei LOD-Stufen.
- **Abstände:** Gebäude halten 3 m Abstand zur Rennfläche (Test `TheRealCityStandsBesideTheTrackNotOnIt`).

## 3. Straßen (`StreetNetwork`)
Straßen sind rein visuell: kein Collider, nicht befahrbar.

- **Fahrbahnen:** gealterter Stadtasphalt mit metrischen UVs wie die Strecke; Mittelstrich-Markierung auf Primär-, Sekundär- und Tertiärstraßen.
- **Gehwege:** 2,2 m breite Gehwege mit 15 cm hohen Bordsteinkanten.
- **Fußwege und Fußgängerzonen:** gepflastert.
- **Treppen:** echte Stufen, Setzstufen auf 17 cm gerastert.
- **Am Rennkurs:** Querstraßen enden mit 1,5 m Abstand an den Barrieren, wie bei einem echten Stadtrennen. Wasserflächen bleiben frei.
- **Test `SideStreetsEndAtTheBarriersAndStayOnLand`:**
  - 109,3 km Straßen, 42,9 km Fußwege, 1.571 Stufen
  - kein Punkt in der Sperrzone

## 4. Stadtleben (`CityLife`, Darstellung `CityLifeComponent`)
- **Straßenmöbel:**
  - Bänke, Mülleimer, Poller und Pflanzkübel auf den Gehwegen
  - Bushaltestellen alle ca. 300 m an Hauptstraßen
- **Geparkte Autos:** am Rand von Wohnstraßen.
- **Fußgänger:**
  - laufen mit 1,1–1,6 m/s auf Gehwegen und Fußwegen
  - kehren an Wegenden und an der Strecke um
  - zwei Schrittposen im Wechsel
- **Verkehr:**
  - fährt rechts mit 8–14 m/s, Busse mit 7–9 m/s
  - hält 9 m Abstand zum Vordermann und wendet an Sackgassen bzw. an der gesperrten Strecke
  - Mischung: etwa ein Drittel gelbe Taxis, dazu Pkw, Transporter, Roller und Busse
  - Busse in neutraler Lackierung, ohne Betreibermarke
- **Budgets je Umgebungsstufe** (Niedrig / Mittel / Hoch):

  | | Niedrig | Mittel | Hoch |
  |---|---|---|---|
  | Fußgänger | 300 | 900 | 1.600 |
  | Fahrzeuge | 80 | 160 | 240 |
  | Sichtweite | 150 m | 250 m | 350 m |

  Fußgänger werden bis 60 % der Sichtweite gezeichnet.
- **Rendering:**
  - GPU-Instancing mit `Graphics.DrawMeshInstanced` in Gruppen zu je 1.023
  - statische Objekte in 120-m-Zellen
  - Bewegung ohne Physik und ohne Collider
- **Kreuzungen** (seit 2026-10-27):
  - Jeder befahrbare Straßenabschnitt ist ein Knoten im Straßengraph. Endet ein Abschnitt höchstens 5 m von einem Punkt eines anderen, ist dort eine Kreuzung.
  - Fahrzeuge biegen dort in die verbundene Straße ab, wobei sie die Ausfahrten reihum wählen. Nur an echten Sackgassen und an der gesperrten Strecke wenden sie.
  - Abschnitte auf beiden Seiten der Rennstrecke werden nie verbunden.
  - Ergebnis: 1.575 verbundene Straßenenden, 1.877 Abbiegevorgänge in zwei Minuten bei 211 Fahrzeugen.
- **Fahrzeuglichter:**
  - Scheinwerfer und Rücklichter leuchten nachts; sie werden über das Nachtlicht-System der Umgebung gesteuert.
  - Geparkte Autos bleiben dunkel.
- **Test `CityLifeStaysOffTheRacingSurfaceAndMoves`:**
  - 120 s Simulation, nichts in der Sperrzone, der Verkehr fließt
  - Fahrzeuge biegen an Kreuzungen ab

## 5. Grenzen (ehrlich)
- **Modelle:** Fahrzeuge, Personen und Möbel sind einfache prozedurale Low-Poly-Platzhalter (zusammen ca. 2.000 Dreiecke). Sie sind für Entfernungen ab etwa 20 m gedacht, nicht für Nahaufnahmen.
  - Vorschau: `dotnet run -- <dir> --citylife`, dann `python3 tools/render_car_preview.py <dir>/citylife.obj <bild.png>`.
- **Fehlt noch:**
  - Ampeln und Vorfahrt: Fahrzeuge biegen ab, warten aber nicht an Kreuzungen und können sich dort kurz überlappen.
  - Fußgängeranimation über die zwei Posen hinaus
- **Kosten:** Draw-Call- und Frame-Kosten sind nicht gemessen (Phase 7).
