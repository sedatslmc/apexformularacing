# Bosphorus GP – Istanbul-Welt und visuelle Überarbeitung

**Stand:** 2026-11-01.

**Status:**
- **Phase 1 (Analyse):** abgeschlossen.
- **Phase 2, Uferhöhen:** implementiert und getestet (Simulation, Messwerkzeuge, Vorschau-Renders).
  - **Im Unity-Editor nicht geprüft.** Die Bilder sind three.js-Vorschauen der Spielgeometrie, keine Unity-Aufnahmen.
- **Phase 3, Referenzzone Dolmabahçe:** implementiert und getestet (Abschnitt 2b). Im Unity-Editor nicht geprüft.
- **Phasen 4–6:** geplant (Abschnitt 4).

**Git-Checkpoint:** `main` bei Commit `8a3377a` (Merge von PR #25), dem Stand vor dieser Überarbeitung. Das lokale Tag `checkpoint-pre-istanbul-overhaul` ließ sich über den Proxy nicht hochladen.

## 1. Technische Bestandsaufnahme (Phase 1)

| System | Code | Stand | Ursache sichtbarer Probleme |
|---|---|---|---|
| Höhenmodell | `tools/geo/build_track_a.py` → `geo.heights` (100-m-Raster), `LandModel` | Copernicus GLO-30 je Zelle 10. Perzentil, OSM-Dächer ausgeschnitten | GLO-30 ist ein **Oberflächenmodell**: Bäume, Mauern, Böschungen zählen mit. Die 100-m-Zellen mitteln den Hang bis ans Wasser. Messwerte am Ufer: Dolmabahçe 6–17 m roh, im Raster 10–36 m; real ist es aufgeschüttetes Land auf Kaihöhe. |
| Nahgelände | `TerrainField.NearHeight` | Straßenband 30 m, dann bis 18° zum DEM | Kein Ufermodell: Landzellen behielten die DEM-Höhe bis zur Küste, Wasserzellen −4 m. **Das ergab die Klippe** (Abschnitt 2). |
| Küstenlinie | `tools/geo/osm_land.py`, `TerrainField.IsWater` | OSM-Küste, horizontal korrekt (PR #19) | Horizontal richtig, vertikal falsch: Genau das beschreibt der Brief. |
| Kai und Promenade | `Waterfront`, `EnvironmentGeometry.WaterfrontEdge` | 4,5 km Kai, 3,5 km Promenade | Kaioberkante war auf höchstens 6 m begrenzt. Darüber lag die 10–36 m hohe Erdwand frei; das war die „braune Steilwand“. Alle Abschnitte bekommen dieselbe Kaigeometrie (Abschnitt 4, Phase 4). |
| Gebäude | `OsmCity`, `EnvironmentGeometry.FootprintBuilding`, `FacadeOrnaments`, `StreetFronts` | 4.088 OSM-Grundrisse, Höhen aus OSM-Tags oder Schätzung, Fassadenornamente nur an Straßenfronten | Grundkörper sind extrudierte Grundrisse mit einer gemeinsamen Fassadentextur. Erker, Balkone, Klimageräte, Markisen und Alterung fehlen außerhalb von Ortaköy. Daher wirkt es kastenförmig. |
| Wahrzeichen | `LandmarkModels`, `LandmarkGeometry` | Dolmabahçe aus Grundrissen mit Kuppel und Uhrturm, die zwei Landtore Saltanat und Hazine, Ortaköy-Moschee, Esma Sultan, Brücke | Die **Ufertore auf der Bosporusseite fehlen**. Die Palastfassade ist ein klassizistisches Grundmuster ohne die Gliederung der echten Bosporusfront. |
| Vegetation | `OsmCity` (Parks, Friedhöfe, Straßenbäume), `EnvironmentGeometry.TreeMesh` | 16.169 Bäume, 4 Arten, LOD über Mesh-Kombination | Kronen aus wenigen Polygonen ohne Alpha-Blätter. Palastgärten werden dicht bepflanzt und verdecken die Fassade vom Wasser aus. |
| Strecken-Infrastruktur | `TrackInfrastructurePlan`, `TrackKitLayout`, `FlagPanelComponent` | Beton, Armco, TecPro-Module, Reifenstapel, Fangzaun nach Gefahrenregel, **LED-Flaggenpanels mit Rennzustand verbunden** (`FlagPanelLogic`) | Fangzaun auf 71 % der Barrierenlänge. Distanztafeln, Marshal-Posten, TV-Türme und Rettungsfahrzeuge: im Kit teilweise vorhanden, im Brief erneut gefordert. Abgleich in Phase 4. |
| Wasser | `Apex/Water` (`docs/WATER_SHADER.md`) | 3 Normal-Map-Schichten, Fresnel, GGX, Uferabstand | Laut Brief sind regelmäßige Muster sichtbar. Der Shader war bei Abgabe nicht kompiliert. Ob im Bild der Shader oder sein Lit-Rückfall zu sehen ist, muss die Unity-Prüfung zeigen. |
| Licht | `SunModel`, `SkyModel`, `WeatherVisuals`, `docs/NIGHT_LIGHTING.md` | Sonnenstand nach Ort und Datum, Tageszeiten, Nachtlichter, rot-weiße Brücke | Ohne Unity nicht bewertbar; im Brief „teilweise künstlich“. |
| Leistung | `GraphicsPreset` (Niedrig bis Cinema), `EnvironmentBuilder.Quality` | Stufen vorhanden | **Nicht gemessen.** Unity-Profiling fehlt; die Dreieckszahlen der Vorschau stehen in `Apex.ModelPreview --environment`. |

## 2. Fehler Dolmabahçe-Klippe (Phase 2): Ursache und Behebung

### Messung
Neues Werkzeug `dotnet run -- coast-audit bosphorus_a`: Es prüft jede Landzelle neben Wasser (4-m-Raster, 6,3 km Ufer).

| | vorher | nachher |
|---|---|---|
| Uferzellen über 4 m | **84 %** | **0 %** |
| Höhe der Uferkante | bis 56 m (Kabataş–Gümüşsuyu 35 m, Çırağan 41 m, Kuruçeşme 55 m) | höchstens 2,1 m (Test) |
| Dolmabahçe Selamlık / Muayede / Harem | 27–35 / 25–37 / 12–32 m | 2,9–6,0 / 2,4–5,6 / 2,4–6,7 m |
| Çırağan-Palast | 12–32 m | 2,9–5,9 m |
| Ortaköy-Moschee (Büyük Mecidiye) | 7,5–8,6 m | 1,7–4,3 m |
| Esma Sultan Yalısı | 5,7–9,9 m | 2,5–5,7 m |

(`dotnet run -- landmark-ground bosphorus_a`: Geländehöhe unter den OSM-Grundrissen.)

### Ursache in zwei Teilen
1. **Höhendaten:** Das Oberflächenmodell mit 100-m-Raster zieht die Hügel hinter der Uferstraße bis ans Wasser. Die Uferebene ist schmal, real Kaihöhe: Dolmabahçe heißt „aufgefüllter Garten“, die Bucht wurde im 17./18. Jahrhundert aufgeschüttet. Das Raster erfasst sie nicht.
2. **Gelände-Code:** Kein Übergang zum Wasser. Landzelle mit DEM-Höhe neben Wasserzelle mit −4 m ergibt eine senkrechte Wand. Die Kaimauer endete bei 6 m.

### Behebung: Ufermodell statt Kaschierung
`CoastalProfile` (Simulation, getestet), angewendet in `TerrainField.Height`. Es gilt damit für Nah- und Ferngelände, Kollision, Gebäudesockel, Bäume, Kai und Promenade, die alle dieselbe Funktion nutzen.
- **Am Wasser:** Kaioberkante 1,6 m über dem Bosporus. Davor steht die bestehende steinerne Kaimauer, die jetzt bündig mit dem Gelände abschließt.
- **Promenade:** 8 m eben auf Kaihöhe.
- **Seeseitig der Uferstraße** (Strecke höchstens 260 m vom Wasser): Uferebene. Das Gelände steigt gleichmäßig von der Promenade bis zum Straßenband an, daher weder Klippe am Wasser noch Wand an der Straße. Dort liegen Paläste, Plätze, Moschee und Kais.
- **Ohne nahe Straße:** 60 m Uferebene mit 3°, dahinter Hang bis höchstens 19° bis zur echten Höhe.
- **Landseitig der Uferstraße:** unverändert. Die Hügel von Yıldız, Gümüşsuyu und Akaretler bleiben, wie sie sind.
- **Abstand zum Wasser:** ein neues Distanzfeld vom Land zum Wasser (`CoastDistanceField`, 8 m, über den Nahbereich plus 1,5 km).

Nicht verändert wurden Wahrzeichen-Positionen, Küstenlinie, Streckenverlauf und Streckenhöhen.

### Tests
- **`TheBosphorusShoreIsAQuayNotACliff`:**
  - Alle 705 Uferzellen liegen höchstens 2,5 m über der Kaihöhe.
  - Die Grundrisse von Palästen, Moschee und Yalı liegen auf 1–4 m (tiefster Punkt) und unter 8 m (höchster Punkt).
  - Keine Kaimauer hat mehr als 3 m Erde dahinter.
  - Die Profilregeln sind abgesichert.
- **Unverändert grün:** Wandanteil neben der Strecke (`terrain-audit`, jetzt 6,0 % statt 6,3 %), Uferlage der Wahrzeichen, Fahrtests auf der Collider-Fläche.

### Bilder (three.js-Vorschau der Spielgeometrie, keine Unity-Aufnahme)
![Dolmabahçe vorher/nachher](img/coast_dolmabahce_before_after.jpg)
![Dolmabahçe aus Bootshöhe](img/coast_dolmabahce_low_before_after.jpg)
![Çırağan vorher/nachher](img/coast_ciragan_before_after.jpg)

In der Vorschau sind die Paläste einfache Grundriss-Körper. Im Spiel stehen dort die detaillierten Landmarken-Modelle.

### Offen und ehrlich
- **Streckenhöhe:** umgesetzt in Abschnitt 2a.
- **Bäume vor den Palastfassaden:** Die Palastgärten werden dicht bepflanzt. Freie Sicht vom Wasser kommt in Phase 3 (Dolmabahçe-Referenzzone).
- **In Unity zu prüfen:** Gelände-Shader am neuen Uferstreifen (Gras/Erde/Mauer nach Neigung), Kaimauer-Anschluss, Kollision an Kai und Promenade.

## 2a. Streckenhöhe an der Uferstraße (freigegeben am 2026-11-01)
Das Straßenprofil (`plan.profile.street`, 10. Perzentil des Oberflächenmodells) hatte am Ufer denselben Fehler wie das
Gelände. `build_track_a.py` korrigiert es jetzt vor dem Höhenentwurf (`coastal_street`):
- **Obergrenze:** höchstens Kaihöhe + 0,6 m Fahrbahnwölbung, + 5 % auf den ersten 60 m vom Wasser, danach + 12 %.
- **Geltungsbereich:** nur bis 260 m vom Wasser. Die Absenkung wird über 160 m verteilt, damit keine Stufe entsteht.
- **Danach:** derselbe Entwurf gegen die Fahrzeuggrenzen (`vertical_alignment.py`) wie bisher.

**Ergebnis:**
- **Çırağan Caddesi** (Runde 1,55–2,06 km): um 0,7–2,8 m abgesenkt, auf 7,4–9,4 m statt 9,1–12,1 m.
- **Rest der Runde:** unverändert.
- **Länge:** unverändert 8,16 km.
- **Steigung:** höchstens 11 %.
- **Tests:** alle 211 grün, darunter die Tastatur-Runden aller Fahrprofile trocken und nass.

**Grenze dieser Korrektur:**
- Die Bremszone vor der Ortaköy-Kehre (letzte ~300 m) und die freigegebene R20-Kehre behalten ihr Profil (12–14 m bzw. ~10 m).
- **Versuch mit voller Korrektur** (`APEX_FULL_SHORE=1 python3 tools/geo/build_track_a.py`):
  - senkt dort um bis zu 11 m ab,
  - erzeugt aber eine Kuppe in der Bremszone aus 330 km/h (Experten-Tastaturrunde ohne ABS verlässt die Strecke),
  - verändert die Ideallinie auf dem Platz, sodass Gummiabrieb und Zaun-Mittelwand-Prüfung brechen.
- Das braucht einen Neuentwurf der R20-Kehre mit Fahrtests. Mein Vorschlag ist ein eigener Schritt.

## 2b. Phase 3: Referenzzone Dolmabahçe
**Quellen:**
- Die Palastfront steht auf einem ummauerten Kai von etwa 600 m.
- Zum Bosporus öffnen sich **fünf Seetore** (yalı kapıları). Das Tor in der Achse des Muayede-Saals ist größer und reicher verziert als die anderen vier.
- Das Haupttor ist in OSM erfasst: Knoten 13084855510 „Dolmabahçe Sarayı Deniz Kapısı“ (ODbL).

### Was gebaut ist
| Teil | Code | Inhalt |
|---|---|---|
| Palastkai | `PalaceWaterfront`, `Waterfront.Segment.palace`, `EnvironmentGeometry.PalaceRailing` | Kaistücke höchstens 75 m vor den Bosporus-Flügeln (Selamlık, Muayede, Harem): statt öffentlicher Promenade mit Pollern und Laternen ein weißer Marmorsockel mit Pfeilern und Kugelaufsatz, ein schmiedeeisernes Geländer mit vergoldeten Spitzen. An den Toren ist es unterbrochen. 417 m Palastkai. |
| Seetore | `LandmarkGeometry.SeaGate`, `PalaceWaterfront.PlaceGates` | Marmorportal mit Rundbogen, Archivolte und Schlussstein, Säulen auf Postamenten (am Haupttor gepaart), Gebälk, barocke Bekrönung (Kartusche mit vergoldetem Medaillon ohne Inschrift, Bogenhaube, Volutenstufen, Urnen), schmiedeeiserne Torflügel mit Rankenband. Drei Detailstufen. Nachts von der Kaiseite warm angestrahlt (ab Hoch). |
| Lage der Tore | wie oben | **Haupttor:** OSM, 8 m vom Knoten. **Die vier anderen:** nicht in OSM, daher genähert auf den Achsen von Selamlık und Harem und an den beiden Enden der Palastfront, mindestens 30 m Abstand. |
| Freier Vorplatz | `OsmCity.AddTree`, `PalaceWaterfront.InForecourt` | Zwischen Fassade und Kai (bis 55 m vom Wasser und von den Flügeln) wachsen keine Bäume mehr. Die Fassade ist vom Wasser aus frei. Die Gärten an den Enden bleiben. |
| Festliche Fassaden | `OsmCity.Building.raceFront`, `StreetFronts.FlagFront/FlagScale` | Straßenfronten zur Strecke hin (bis 25 m vom Fahrbereich) tragen zu 40 % hängende türkische Flaggen, Ortaköy zu 25 %, sonst zu 10 %. Flaggengröße zwischen 0,7 × 1,1 m und 1,6 × 2,4 m. Die Auswahl über eine Hashfunktion verhindert ein Raster. |

### Tests
- **`DolmabahceHasItsPalaceQuayFiveSeaGatesAndAnOpenForecourt`:**
  - Palastkai 250–700 m, ohne Promenade und Laternen.
  - Fünf Tore, das Haupttor höchstens 25 m vom OSM-Knoten, alle an Land mit Blick aufs Wasser, mindestens 30 m Abstand.
  - Geländer an den Toren unterbrochen.
  - Kein Baum im Vorplatz.
- **`FacadesAlongTheCircuitAreFestiveWithFlagsOfDifferentSizes`:** 216 Fronten zur Strecke, davon 41 % mit Flaggen, sonst 10 %. Größen gestreut.

### Bilder (three.js-Vorschau der Spielgeometrie, keine Unity-Aufnahme)
![Bosporusfront](img/dolmabahce_ref_bosphorus.jpg)
![Haupt-Seetor](img/dolmabahce_ref_sea_gate.jpg)
![Palastkai](img/dolmabahce_ref_quay.jpg)
![Verfolgerkamera](img/dolmabahce_ref_chase.jpg)
![Cockpit Akaretler](img/akaretler_ref_cockpit.jpg)

### Sichtbarkeit aus dem Fahrbetrieb (ehrlich)
- **Seetore:** Die Strecke liegt an der Dolmabahçe Caddesi, also auf der Landseite. Die Tore sind daher **vom Fahrer nicht zu sehen**, nur vom Wasser, aus Wiederholungs- und TV-Kameras. Das ist geografisch korrekt; ich habe keine Sichtachse erfunden.
- **Aus der Verfolgerkamera** sieht man an der Dolmabahçe Caddesi die Palast-Nebengebäude und das Saltanat-Tor.

### Offen nach Phase 3
- **Palastfassaden:** Die Grundriss-Modelle haben Fensterachsen, Gesimse und Balustraden. Die feine Gliederung der echten Bosporusfront (Pilaster und Säulenstellungen, reiche Fensterrahmungen) fehlt noch.
- **Tore:** Die Proportionen beruhen auf Fotos, nicht auf Bauaufnahmen. Für die vier genäherten Positionen bräuchte es eine Bauaufnahme oder Millî-Saraylar-Pläne.
- **Fangzaun an der Akaretler-Steigung:** beidseitig und im Cockpit dominant (Bild). Er gehört zur Infrastruktur-Phase 4.
- **Vorschau-Werkzeug:** zeigt die Flaggen an Straßenfronten aus Streckensicht schlecht (keine Schatten-/Licht-Abstimmung). Die Prüfung im Unity-Editor ist nötig.
- **Wind:** Die Bewegung der Balkonflaggen ist im Material nicht animiert; nur die Monumentalflagge bewegt sich.

## 3. Werkzeuge
```sh
cd build/Apex.Tuning
dotnet run -c Release -- coast-audit bosphorus_a              # Uferhöhen, Zellen über 3 m
dotnet run -c Release -- landmark-ground bosphorus_a [namen]  # Gelände unter Wahrzeichen-Grundrissen
cd build/Apex.ModelPreview
dotnet run -c Release -- <out> --environment --real --quality 2 --crop x,z,radius   # Ausschnitt als OBJ
cd tools/carrender && APEX_PAGE=env.html APEX_VIEWS='name|eye|target|fov|&tod=golden' node render.mjs <out>/environment.obj <prefix>
```

## 4. Nächste Phasen (Plan)
3. **Referenzzone Dolmabahçe (300–500 m):**
   - Bosporusfassade mit Gliederung, die weißen Ufertore an der Kaimauer nach Referenzen, Ufermauer mit Geländer.
   - Gartenbepflanzung, die die Fassade freilässt.
   - Uferpromenade, Wohnhäuser mit Balkonen und türkischen Flaggen.
   - Barrieren und Zaun der Dolmabahçe Caddesi.
   - Prüfung aus Verfolger-, Cockpit- und TV-Sicht (Vorschau; Unity-Prüfung durch dich).
4. **Übertragung:** Gebäudefamilien mit Balkonen, Erkern, Klimageräten und Markisen; Flaggenverteilung; Ufertypen (historisch, urban, natürlich); Infrastruktur-Abgleich; Sektor-Charaktere.
5. **Atmosphäre:** Zuschauer, Fähren-Routen, Licht der goldenen Stunde, Audio.
6. **Tests und Leistung:** Fahrbarkeit, KI, Sichtachsen, Profiling in Unity auf PC und Mobil (nur mit Unity-Messung als Zahl).
