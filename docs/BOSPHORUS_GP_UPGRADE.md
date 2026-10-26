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
