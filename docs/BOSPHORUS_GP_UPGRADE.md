# Bosphorus Grand Prix: Upgrade zum Flaggschiff-Kurs

Auftrag: „Ultimate Bosphorus Grand Prix Upgrade“ (2026-10-26). Statusstufen: **geplant / implementiert / getestet / poliert**.

> **Wichtige Einschränkung:** In der Cloud-Umgebung, in der ich arbeite, gibt es **keinen Unity-Editor**.
> - Geprüft wird per Kompilierung gegen Unity-2021.3-Referenzen, NUnit-Tests (`tools/verify.sh`), den Simulationsprüfstand (`build/Apex.Tuning`) und Offline-Vorschauen (three.js, `tools/carrender`).
> - **Echte Unity-Screenshots und Messungen auf Mobilgeräten muss jemand mit Unity 6 bzw. einem Gerät machen.** Offline-Vorschauen sind kein Beweis für das Aussehen in Unity und werden nirgends als solcher bezeichnet.

## Phase A: Sichern und validieren (implementiert, getestet)

### Sicherungen
- Branch `backup/bosphorus-schematic-v1`: alter schematischer Kurs mit Umgebung
- Branch `backup/pre-bosphorus-upgrade`: Stand direkt vor diesem Upgrade

### Bestandsaufnahme
| System | Ort | Zustand / Bezug zum Upgrade |
|---|---|---|
| Streckendaten | `Runtime/Resources/Tracks/*.json`, `CircuitDefinition`, `TrackGeometry` | **neu:** `bosphorus_a.json` (Variante A). Lauf-/Barrierenabstand pro Punkt, Höhenraster, Kurvenliste |
| Streckenbau | `Game/Tracks/CircuitBuilder.cs` | Straße, Kerbs, Auslauf/Wände, Boxengasse, Gelände, Startlinie/Portal, Startplätze, Kurven- und Bremsschilder. **Neu:** Barrierenabstand pro Abschnitt statt pauschal 16 m |
| Umgebung | `Game/Environment/*` | Stadtgenerator, Wahrzeichen-Silhouetten, Wasser, Flaggen, Fähren, Zuschauer, Möwen, Bäume, Audio-Zonen, Nachtlicht. Läuft jetzt auch auf Variante A |
| Rennsysteme | `Simulation/Race`, `AI`, `Vehicle`, `Career` | unverändert; nur Strecken-IDs migriert |
| Wetter/Licht | `WeatherVisuals`, `SunModel` | Sonnenstand aus Wahr-Nord (bei Variante A 0°, Karte ungedreht) |
| Geodaten | `data/geo`, `tools/geo` | OSM (ODbL) und Copernicus DEM, Pipeline reproduzierbar |

### Umsetzung Variante A im Spiel
- **Streckendatei** `bosphorus_a.json` aus dem freigegebenen Plan (`tools/geo/build_track_a.py`):
  - 7,64 km, 13 Kurven mit Namen, gegen den Uhrzeigersinn, 3 Sektoren
  - Start/Ziel auf der Dolmabahçe Cd., Boxengasse landseitig, 20 Boxen
- **Ungedrehtes Koordinatensystem:** Wahr-Nord = +z, Ursprung Dolmabahçe. Alle Elemente nutzen dieselbe Transformation.
- **Gelände aus Copernicus DEM:**
  - Raster 100 m, Straßenniveau-Schätzung mit 5. bzw. 10. Perzentil
  - Dachpixel unter OSM-Gebäudegrundrissen ausgeschlossen
  - Uferstraße damit auf ≈ 4–8 m statt zuvor überschätzter ≈ 18 m
- **Landmassen aus der DEM-Küste:** Europa inkl. historischer Halbinsel (über das Ende des Goldenen Horns verbunden), Asien; Goldenes Horn und Leanderturm-Insel korrekt.
- **Fähranleger aus OSM:** Beşiktaş, Üsküdar, Karaköy, Eminönü. Fährrouten weichen automatisch dem Land aus.
- **Stadtkurs-Barrieren statt 16 m Gras-Auslauf:**
  - 1,5 m bis zur Wand
  - Notausfahrten 6–12 m an T1, T5, T6, T8, T10, T11
  - Beşiktaş-Parallelstück: zwei Richtungsfahrbahnen à 9,5 m mit gemeinsamer Mittelwand (getestet: keine Überschneidung)
- **Spielprofil:** max. 11,5 % Steigung über 50 m. Die Straße erreicht laut DEM bis ≈ 12,6 %.
- **Dokumentierte Fahrbarkeits-Anpassung:** S-Kurve am Yıldız-Kamm (Runde 3,36–3,74 km) leicht entschärft.
- **Spiel umgestellt:** „Bosphorus Grand Prix“ ist jetzt `bosphorus_a`, auch in der Meisterschaft und als Standard.
  - Alte Speicherstände werden migriert (Session und Meisterschafts-Kalender).
  - Der alte Kurs bleibt als Datei erhalten, „Archiv“, nicht mehr im Menü, wird nicht gelöscht.

### Prüfergebnisse (Simulationsprüfstand, flaches Rig, ohne Wandkontakt)
| Test | Ergebnis |
|---|---|
| Autopilot, 2 Runden | gültig; Grenzen im Test 115–165 s, gemessen ≈ 2:14–2:21 min |
| Tastatur-Fahrer (Einsteiger, Ausgewogen, Profi) trocken | 0 Dreher, ≈ 2:12 min |
| dito nass | 0 Dreher, ≈ 2:30 min |
| „Profi ohne ABS“ und „Alles aus“ mit Tasten | unfahrbar, wie auf allen Strecken (nur für analoge Pedale gedacht) |
| KI-Feld, 12 Autos | alle fahren durch; max. 2,3 m über den Fahrbahnrand (innerhalb Kerb und Auslauf, eine Stelle an T7 an der Mittelwand knapp) |
| Unit-Tests | 119/119 grün, darunter neu: Plan-Kennzahlen, Barrieren ohne Überschneidung, Geografie/Stadt/Fähren auf Variante A, KI-Feld, Tastaturrunden |
| Unity-Kompilierung | OK (Referenzen 2021.3) |

### Sichtachsen (DEM-Sichtlinie inkl. Gebäude, Augenhöhe 1 m über Straßenniveau)
Nach der Höhenkalibrierung liegt das Auge tiefer, deshalb verdecken Gebäude mehr als im Phase-3b-Plan. Diese Werte ersetzen die dortigen:

| Wahrzeichen | sichtbar | wo |
|---|---|---|
| Ortaköy-Moschee | ≈ 115 m | unmittelbar vor der Ortaköy-Haarnadel, **zusammen mit der Brücke** (Signaturmoment) |
| Brücke (Pylon) | ≈ 2,1 km | Çırağan Cd. ab km 1,6, Palanga-Anstieg, Akaretler, Kabataş |
| Dolmabahçe-Palast | ≈ 1,1 km | Start/Ziel-Gerade, Beşiktaş, Anfahrt Kabataş |
| Çırağan-Palast | ≈ 0,2 km | Çırağan Cd. km 1,1–1,4 |

Vom Yıldız-Kamm aus gibt es laut DEM **kein freies Bosporus-Panorama**: Gebäude und Bäume verdecken es. Die Brücke ist im Palanga-Anstieg sichtbar. Ein Panorama erfinde ich nicht.

### Offline-Vorschau (nicht Unity)
`docs/img/bosphorus_a_preview_sheet.png`, Verfolgerperspektive: Start, Ortaköy-Anfahrt mit Moschee und Brücke, Kamm, Barbaros, Akaretler, Nacht.
Zu sehen sind nur Gelände, Straßenband, Stadtblöcke, Bäume und Wahrzeichen. Barrieren, Zäune, Tribünen und Flaggen erzeugt erst der Unity-Builder.
Die Vorschau zeigt auch die aktuelle Schwäche: Neben der Straße liegt nacktes Gelände (Böschungen), und die Fassaden sind Platzhalter.

### Bekannte Punkte nach Phase A
1. KI kennt keine Wände. Auf dem Rig (ohne Wände) bleibt sie zwar innerhalb von Kerb und Auslauf, in Unity sind Wandkontakte an T7 und an engen Stellen aber möglich. Lösung in Phase F: Wandabstand in der KI-Linie.
2. Die Umgebung lief auf dem alten Kurs mit synthetischen Hügeln. Auf Variante A sind Gebäude-Platzierung und Wahrzeichen-Ausrichtung noch nicht im Editor geprüft. Dolmabahçe und Çırağan stehen nach OSM-Position, aber nicht als OSM-Grundriss.
3. Stadt-Dreiecke (Mittel): ≈ 227 k LOD0 insgesamt (nicht alles gleichzeitig sichtbar). Muss in Phase F reduziert und auf Geräten gemessen werden.
4. Copernicus-Quellenhinweis im Wortlaut der Lizenz und ein Credits-Bildschirm fehlen noch; OSM-Hinweis steht in der Streckenbeschreibung.

## Phase B: Professionelle Renninfrastruktur (implementiert, Logik getestet, in Unity noch nicht gesehen)

Alles wird aus der Strecke abgeleitet: `TrackInfrastructurePlan` (reine Logik, getestet) → `TrackInfrastructureGeometry` (Meshes) → `TrackInfrastructureBuilder` (Unity-Objekte). Das gilt für jede Strecke; der Bosphorus GP nutzt dabei seine Stadtkurs-Abstände.

| Baustein | Umsetzung | Status |
|---|---|---|
| Asphalt | prozedurale, kachelbare Asphalt-Textur (Albedo mit Gesteinskörnung) plus passende Normal-Map, Kachel ≈ 3 m, matt (Glanz 0,22); Nässe wie bisher über `WeatherVisuals` | implementiert |
| Fahrbahndetails | 52 Flickstellen, 44 Kanaldeckel, verblasste Mittel-Strichlinien der Originalstraßen auf breiten Geraden (445 Striche), Gummiabrieb entlang der KI-Ideallinie in jeder Bremszone und an jedem Kurvenausgang (26 Zonen, weich auslaufend, transparent) | implementiert |
| Kerbs | 3D-Profil mit gerundeter Krone und Außenkante. Innen vom Einlenken bis zum Ausgang, außen am Ausgang. Höhe nach Kurvenradius: 2,5 cm schnell, 3,8 cm mittel, 5 cm langsam. Rot-weiß in 2,5-m-Blöcken. Nie breiter als der Auslauf (Mittelwand in Beşiktaş bleibt frei). Physik über die vorhandene Kerb-Oberfläche | implementiert, Platzierung getestet; Fahrgefühl in Unity ungeprüft |
| Auslauf | Stadtkurs: gepflasterte Auslaufflächen und Notausfahrten (Asphalt) statt Gras; offene Strecken behalten Gras | implementiert |
| Barrieren | Betonelemente mit Schutzwand-Profil, 1,0 m hoch, 3,8-m-Blöcke mit Fugen, Kratzspuren und Schmutz am Fuß (Textur). Leitplanke (doppelte W-Schiene) auf Notausfahrten. Kollision weiter über die einfache Wand (unsichtbar) | implementiert |
| Aufprallschutz | 10 Zonen an den Außenseiten der Bremszonen, abgeleitet aus Anfahrts- und Kurvengeschwindigkeit der Ideallinie: Energie-Module in Event-Farben Navy/Türkis/Elfenbein bei schnellen Anfahrten, Reifenstapel mit Fördergurt-Front an 3 langsamen Kurven; mit eigener Kollision | implementiert, Zonen getestet |
| Fangzäune | auf der Barriere, 3 m Maschendraht plus 0,9 m Überhang zur Strecke, Pfosten alle 5 m, 48 Tore (Notausgang/Wartung) mit Rahmen, an jedem dritten ein Schild „ACİL ÇIKIŞ“. Kein Zaun auf der Beşiktaş-Mittelwand und an der Boxengasse | implementiert |
| LED-Flaggentafeln | 23 Tafeln, an jeder Kurve ≈ 120 m vorher auf der Außenseite, dazu Lücken auf max. 450 m gefüllt. Gekoppelt an die echte Rennleitung (`FlagPanelLogic`): Gelb blinkt vor einem Unfall, **Doppelgelb**, wenn ein Auto ≤ 150 m voraus auf der Strecke steht, **Grün** hinter der Zone und nach Ende einer Neutralisation, **SC/VSC** als Schrift, **Blau** für überrundete Autos mit dem Führenden dicht dahinter. Es gibt keine Rot-Regel, deshalb kein Rot. Außerhalb von Rennen bleiben die Tafeln dunkel | implementiert, Logik getestet (3 Tests) |
| Streckenposten | an jeder Tafel ein Unterstand (Betonwände, Dach in Türkis), Feuerlöscher, zwei Posten in Orange hinter der Barriere | implementiert, **ohne Animation** |
| Schilder | 150/100/50-m-Bremstafeln und Kurventafeln jetzt an der Barriere (vorher frei im Auslauf); Sektortafeln „SEKTÖR 1–3“; „PİT GİRİŞİ ▸“, „PİT 80 km/h“, „PİT ÇIKIŞI“. DRS gibt es im Spiel nicht, daher keine DRS-Schilder | implementiert |

### Budget (Offline-Messung, ganze Runde, Stufe „Mittel“)
Barrieren ≈ 29 k, Zäune ≈ 28 k, Stahl (Pfosten, Tore, Leitplankenpfosten) ≈ 24 k, Reifenstapel ≈ 22 k, Leitplanke ≈ 7 k, Auslauf ≈ 15 k Dreiecke.
In 400-m-Stücken gebaut, damit Unity Unsichtbares wegschneidet. Zäune sind alpha-getestet, nicht transparent; transparent ist nur der Gummiabrieb.

### Vorschau (Offline-Renderer, NICHT Unity)
`docs/img/bosphorus_phaseB_preview_sheet.png`, Verfolger-Höhe:
- Start
- Anbremsen Ortaköy (Moschee und Brücke voraus)
- Ortaköy-Haarnadel
- Yıldız-Kamm
- Barbaros-Ende/T6 mit Leitplanke
- Akaretler
- Bayıldım-Kehre mit Aufprallmodulen
- Cockpit-Höhe

Die Vorschau zeigt keine Texturen (Asphalt, Beton) und keine LED-Tafeln, nur Geometrie und Grundfarben.

### Nicht umgesetzt / bekannte Punkte
- **Fahrbahnquerneigung (Camber):** Die Streckengeometrie kennt keine Querneigung. Das zu ergänzen betrifft Physik, KI und Tests; ich würde es als eigenen Schritt vorschlagen.
- **Pfützen, Drainage, Nachtspiegelungen:** Phase E (Wetter und Licht).
- **Posten-Animation, Fotografen, Kamerateams:** Phase E.
- **KI kennt die Wände nicht:** Phase F.
- **Kerbs:** Bei 5 cm Höhe an langsamen Kurven muss das Rumpeln in Unity geprüft werden. Auf dem Prüfstand-Rig gibt es keine Kerbs.

## Umsetzungsplan Phasen B–F

Grundsatz: Alles datengetrieben aus Mittellinie, Breiten, Barrierenabständen, Krümmung, Fahrtrichtung, Gelände und Geodaten. Vorhandene Builder werden erweitert, keine handplatzierten Objekte. Jede Phase endet mit grünem `tools/verify.sh` und einem Prüfstand-Lauf.

### Phase B: Professionelle Renninfrastruktur
| Baustein | Umsetzung (vorhandenes System → Erweiterung) |
|---|---|
| Asphalt | `CircuitBuilder.BuildRoad`: prozedurale Asphalt-Textur mit Normal-Map und Farbvariation, Flickstellen, Gummiabrieb in Bremszonen und Kurven (aus Krümmung und Bremspunkten der KI-Linie), Fahrbahnmarkierungen der Originalstraßen (Mittellinien gestrichelt, wo zweispurig), Kanaldeckel. Nässe über das vorhandene `WeatherVisuals`-Binding, Pfützen an Tiefpunkten des Profils |
| Kerbs | 3D-Profil (gerundet), flach an schnellen, höher an langsamen Kurven, Breite nach Kurventyp, rot-weiß, Gummispuren. Physik über die bestehende `SurfaceType.Kerb` (Rumpeln moderat, keine Sprünge) |
| Barrieren | neuer `TrackBarrierGenerator`: Beton-Elemente (3 m, Fugen, Verbinder), Leitplanke auf schnellen Abschnitten, TecPro-artige Module und Reifenstapel an Notausfahrten (Platzierung aus Aufprallrichtung = Außenseite der Bremszonen). Collider wie bisher pro Wand |
| Fangzäune | vorhandener `FenceSegment` mit Pfosten, Überhang, Toren alle ≈ 150 m; alpha-cut wie bisher (geringe Überzeichnung) |
| Bremsschilder, Kurvenschilder, Sektoren | vorhandene `BuildCornerBoards` nutzen jetzt die freigegebenen 13 Kurven; dazu Sektor-, Boxen-Ein- und -Ausfahrt- sowie Tempolimit-Schilder |
| LED-Flaggentafeln | neuer `FlagPanelController` gekoppelt an `RaceControl` (Gelb/Doppelgelb pro Zone über `IsYellowAt`, Grün, SC/VSC aus `TrackStatus`, Blau aus `RaceDirector` falls vorhanden). Rote Flagge nur, wenn eine Regel existiert, sonst nicht angezeigt |
| Streckenposten | Hütten hinter den Wänden an den Gelbzonen-Grenzen, Feuerlöscher, Leuchten, Posten als Instanzen |

### Phase C: Bosphorus-GP-Identität
- Palette #C8102E / #087F8C / #C9A66B / #1D2835 / #F0E9D9 als Material-Schlüssel.
- Eigenes Logo, prozedural als Vektor-Mesh bzw. Textur: Bosporus-Linie, Hängebrücken-Silhouette, Apex-Bogen. Schriftzug „BOSPHORUS GRAND PRIX · ISTANBUL“, Slogan „Where Continents Meet Speed“.
- Eigenes Startportal mit fünf Startlichtern. Die vorhandene Startampel-Logik bleibt.
- Banner auf Türkisch, Rechtschreibung geprüft: „İstanbul'a Hoş Geldiniz“, „Boğaz Grand Prix“, „Hızın Buluşma Noktası“.
- Fiktive Sponsoren, nur dezent.
- İznik-inspirierte geometrische Muster nur an Boxengebäude, VIP-Bereich und Podium.

### Phase D: Istanbul-Umgebung
- Stadtblöcke aus **OSM-Gebäudegrundrissen** in Zone A/B (0–500 m), Höhen aus Geschossen oder Viertel-Stil. Prozedurale Blöcke nur in Zone C.
- Viertel-Stile: Dolmabahçe monumental, Beşiktaş dicht und gemischt, Çırağan/Ortaköy historisch, Yıldız grün mit Mauern, Akaretler Reihenhäuser.
- Wahrzeichen nach OSM-Grundriss ausgerichtet. Dolmabahçe-Palast, Çırağan-Palast, Ortaköy-Moschee und Akaretler bekommen detailliertere eigene Modelle. Grenze: prozedurale Geometrie bleibt Platzhalter-Art; Fotorealismus braucht lizenzierte bzw. eigens modellierte Hero-Assets.
- Wasser-Shader (URP, mobil): Fresnel, Wellen, Uferschaum. Bugwellen der Fähren, Navigationslichter.

### Phase E: Atmosphäre
Sonnenuntergang als Signatur-Preset, Nacht mit Flutlicht und Fassadenlicht, Regen (Gischt, nasse Kerbs/Wände), Zuschauer mit Handflaggen und Jubel bei Überholmanövern, Kamerafahrten und Startkamera, Audio-Zonen (vorhanden) feinjustiert.

### Phase F: Optimierung und Validierung
KI-Wandabstand, Track-Limits, Boxengassen-Test, Sichtbarkeit der Bremspunkte, LOD-/Dreiecks-Budgets je Preset (Low/Medium/High/Ultra), Profiling auf echten Geräten (durch dich bzw. das Team).

## Lizenzen
- Streckenachsen, Gebäude, Küste: © OpenStreetMap-Mitwirkende, ODbL 1.0. `bosphorus_a.json` ist eine abgeleitete Datenbank (siehe `data/geo/README.md`).
- Gelände: Copernicus DEM GLO-30 (Quellenhinweis Pflicht, siehe `docs/ASSET_LICENSES.md`).
- Alle Modelle und Texturen sind prozedural im Projekt erzeugt; keine externen Assets.
