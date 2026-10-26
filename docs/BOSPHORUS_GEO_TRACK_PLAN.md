# Bosphorus GP: geografische Neuplanung (Phasen 1–3)

Stand: 2026-10-26. Auftrag: „Complete Master Prompt – Authentic Istanbul“.
**Status: Phase 3 im Entwurf. Ich warte auf die Freigabe. Die bestehende Unity-Strecke ist unverändert.**

## Phase 1: Bestandsaufnahme

**Sicherung:** Der Stand vor dem Umbau liegt auf dem Branch `backup/bosphorus-schematic-v1` (Commit `649a9be`). Ein Tag-Push war in dieser Umgebung nicht möglich.

| Bereich | Ist-Zustand | Bewertung für die Neuplanung |
|---|---|---|
| Streckenführung | 5,1 km, 20 Kurven, gegen den Uhrzeigersinn, 3 Sektoren, Boxengasse links an der Start/Ziel-Geraden. Verlauf **fiktiv** | wird ersetzt |
| Geo-Bezug | Eine starre Transformation (Drehung 52°, Maßstab 1:1) legt echte Wahrzeichen um die fiktive Strecke. Norden zeigt im Spiel auf 308° | Wahrzeichen in sich konsistent, die Strecke folgt aber keinen echten Straßen |
| Küstenlinien | von Hand genähert (±50–100 m) | wird durch DEM-Küste (jetzt) bzw. OSM-Küstenlinie (später) ersetzt |
| Stadt | prozedurale Blöcke (`CityLayout`), drei Dichtezonen, Sichtachsen | Basis bleibt. Künftig OSM-Gebäudegrundrisse in Zone A/B, Viertel-Stile |
| Wahrzeichen | eigene Platzhalter-Silhouetten (`LandmarkGeometry`) | bleiben vorerst, Detailstufe muss steigen |
| Höhenprofil | aus den fiktiven Streckenpunkten | wird aus Copernicus-DEM abgeleitet |
| Betroffene Rennsysteme | Strecken-JSON, `TrackGeometry`, KI-Linie (`RacingLine`), Boxengasse, Startplätze, Sektoren, Bestzeiten (Schlüssel = Strecken-ID), Kurvenschilder, Tests in `TrackTests`/`EnvironmentTests` | nur diese werden angepasst. Physik, Regeln, Reifen, Wetter, HUD, Karriere bleiben unberührt |
| Performance | nur Dreieckszahlen bekannt. **Keine Messung auf Geräten** | Phase 7 |

## Phase 2: Geodaten

### Erreichbarkeit (geprüft am 2026-10-26 aus der Cloud-Umgebung)
| Quelle | Host | Ergebnis |
|---|---|---|
| Copernicus DEM GLO-30 | `copernicus-dem-30m.s3.amazonaws.com` | **erreichbar**: Kacheln N41E028 und N41E029 geladen |
| SRTM/Terrain Tiles | `s3.amazonaws.com/elevation-tiles-prod` | erreichbar (nicht benötigt) |
| Overpass API | `overpass-api.de`, `overpass.kumi.systems` | **gesperrt** (Proxy 403) |
| OSM-Downloads | `download.geofabrik.de`, `osmdata.openstreetmap.de` | **gesperrt** |
| OSM-Kacheln/API | `tile.openstreetmap.org`, `www.openstreetmap.org` | **gesperrt** |

### Benötigte OSM-Daten (falls die Freigabe nicht klappt)
| Datei | Ausschnitt (S, W, N, O) | Inhalt | Format | Ablage |
|---|---|---|---|---|
| `bosphorus_core.osm` | 41.020, 28.970, 41.065, 29.050 | Straßen, Küste, Gebäude, Parks, Mauern, Moscheen/Historisches, Laternen/Ampeln | OSM-XML | `data/geo/osm/` |
| `bosphorus_coast.osm` | 40.990, 28.930, 41.100, 29.100 | Küstenlinie, Wasserflächen, Brücken, Anleger | OSM-XML | `data/geo/osm/` |

So geht's: Die fertigen Abfragen liegen in `data/geo/osm/*.overpassql`. Inhalt bei https://overpass-turbo.eu einfügen, „Ausführen“ klicken, dann „Exportieren → Rohdaten (OSM)“ und unter dem Dateinamen oben speichern.
Alternativ diese Domains in der Umgebung freigeben: `overpass-api.de` (Reserve: `overpass.kumi.systems`). Dann lade ich die Daten selbst.

### Pipeline (fertig, wartet auf Daten)
- `tools/geo/fetch_dem.sh`: lädt die DEM-Kacheln. Sie werden nicht eingecheckt.
- `tools/geo/dem_tools.py`: DEM-Abfrage (bilinear) und Umrechnung WGS84 → lokales Metersystem.
- `tools/geo/osm_import.py`: OSM-XML → `data/geo/osm/bosphorus_osm.json`, mit Straßen (Name, Klasse, Breite, Einbahn, Brücke/Tunnel, Höhe aus DEM), Küstenlinie, Gebäuden (Grundriss, Höhe/Geschosse), Grünflächen, Mauern und Wahrzeichen. Danach prüft das Skript, ob die benötigten Straßen vorhanden sind. Selbsttest: `--selftest`.
- `tools/geo/track_plan.py`: Planungskarte und Höhenprofil.

### Koordinatensystem (neu)
- Lokale Tangentialebene um den Dolmabahçe-Palast (41,03944 N, 29,00167 O).
  - x = Osten: (lon − 29,00167) · 111.320 · cos(41,03944°) m
  - z = Norden: (lat − 41,03944) · 110.574 m
- **Keine Drehung:** Wahr-Nord = +z, Maßstab 1:1. Fehler der Näherung im 10-km-Ausschnitt < 0,1 %.
- Alle Elemente (Strecke, Küste, Gebäude, Wahrzeichen, Gelände) nutzen ausschließlich diese Transformation.
- Die alte Drehung um 52° entfällt mit dem Umbau.

### Datenqualität
| Daten | Status |
|---|---|
| Wahrzeichen Dolmabahçe, Ortaköy-Moschee, Brückenenden, Galata, Leanderturm, Sultan-Ahmed | **verifiziert** (Wikipedia/Structurae/Pleiades) |
| Hagia Sophia, Çırağan, Beylerbeyi, Anleger, Yıldız-Park | **Näherung**. Wird mit OSM überprüft |
| Gelände und Küste | **verifiziert**: Copernicus DEM, 30 m. Das DEM ist ein Oberflächenmodell (Dächer und Bäume enthalten), deshalb wird das Straßenprofil geglättet. Meer = 0 m |
| Straßenverläufe | **fehlen**: Im Entwurf skizziert, nicht verifiziert |
| Gebäudegrundrisse und -höhen | **fehlen** (OSM). Höhen sind in OSM oft lückenhaft und werden dann aus Geschossen bzw. dem Viertel-Stil ergänzt |

### Lizenzen (keine Rechtsberatung)
- **OSM (ODbL 1.0):** In den Credits steht „© OpenStreetMap-Mitwirkende“. Das Spiel gilt als *Produced Work*. Die daraus abgeleitete Geodatenbank (`bosphorus_osm.json` und die daraus erzeugten Streckendaten) muss bei Veröffentlichung unter ODbL angeboten werden (ODbL 4.6). Deshalb bleiben diese Daten getrennt vom Code und lassen sich per Skript reproduzieren. Kombinationen mit anderen Höhendaten gelten in der OSM-Community teils als abgeleitete Datenbank. Vor dem Release sollte das juristisch geprüft werden.
- **Copernicus DEM:** frei nutzbar, Quellenangabe Pflicht. Bei veränderten Daten lautet sie „produced using Copernicus WorldDEM-30 …“, dazu kommt ein Haftungsausschluss. Wortlaut siehe `docs/ASSET_LICENSES.md`.

## Phase 3: Streckenplan (Entwurf zur Freigabe)

![Entwurf](img/bosphorus_track_plan_draft.png)
![Höhenprofil](img/bosphorus_track_plan_profile.png)

**Wichtig:** Gelände, Küste und Wahrzeichen in der Karte sind echt. Die rote Linie ist eine **Skizze der Straßenkorridore** (±50–100 m), noch **nicht** an OSM-Straßenachsen ausgerichtet. Länge und Kurven werden sich nach dem OSM-Abgleich verschieben.

**Variante A:** gegen den Uhrzeigersinn, ≈ 6,8 km (Skizze), Höhenunterschied ≈ 105 m (DEM, geglättet).

| Abschnitt | Reale Straße | Charakter | Sehenswürdigkeiten / Sicht |
|---|---|---|---|
| Start/Ziel, Sektor 1 | **Dolmabahçe Caddesi**, Kabataş → Beşiktaş | breite Hochgeschwindigkeitsgerade an der Palastmauer, ≈ 1,2 km. Boxengasse und Paddock auf der Landseite (Gelände am Stadion, *fiktive Nutzung*) | Dolmabahçe-Palast und Uhrturm direkt am Streckenrand |
| T1 | **Beşiktaş-Platz** | harte Bremszone, 90° rechts. **Überholstelle 1** | Platz, Anleger, Blick auf Üsküdar |
| T2–T5 | **Çırağan Caddesi** | schnelle Uferstraße mit leichten Knicken, eng zwischen Palast- und Parkmauern („Baku-Burgabschnitt“ auf Istanbuler Art) | Çırağan-Palast, Yıldız-Parkmauer. **Signaturblick:** Moschee und Brücke voraus (DEM-Sichtprüfung: frei) |
| T6, Sektor 2 | **Ortaköy** | Haarnadel links. **Überholstelle 2** | Moschee etwa 100 m entfernt, die Brücke darüber |
| T7–T10 | **Palanga Cd.** bzw. Hangstraßen nach Yıldız | Bergaufpassage, S-Kurven, blinde Kuppe | Blick über den Bosporus zur Brücke (DEM-Sichtprüfung: frei) |
| T11–T13, Sektor 3 | **Yıldız** (Kammstraßen), dann **Yıldız Cd.** bergab | technisch, Mauern, Grün; Bergabbremsen | Yıldız-Park |
| T14–T15 | **Spor Cd. / Akaretler** | enge historische Passage (Akaretler-Reihenhäuser) | historische Fassaden |
| T16 | **Kabataş** | Haarnadel auf die Start/Ziel-Gerade. **Überholstelle 3** (nach der Bergabfahrt) | – |

**Kennzeichnung fiktiver Anpassungen (geplant):** temporäre Verbreiterung auf ≥ 11–12 m in engen Passagen, Auslaufzonen an den Haarnadeln, umgestaltete Kreuzungen am Beşiktaş-Platz und in Kabataş, Boxengasse auf temporären Flächen. Wahrzeichen werden nicht verschoben.

### Offene Punkte, die erst echte Straßendaten klären
1. **Steigungen:** Das DEM zeigt am Yıldız-Anstieg bis zu ≈ 19 % (geglättet über 150 m), bergab bis ≈ 27 %. Ein Teil davon sind Dächer und Bäume im Oberflächenmodell. Liegt die echte Straßensteigung über ≈ 12 %, schlage ich entweder eine flachere Nachbarstraße vor oder eine **dokumentierte fiktive Abflachung**.
2. **Engpass Beşiktaş:** Alle Wege zwischen Hang und Ufer treffen sich in Beşiktaş. Ob Hin- und Rückweg ohne Kreuzung getrennt bleiben (Variante A über Spor Cd.), lässt sich nur mit dem Straßengraphen prüfen. **Variante B** wäre Barbaros Bulvarı bergab (breit, lange Bergabgerade, gute Überholchance) mit Fahrtrichtung im Uhrzeigersinn. Dann braucht es am Platz eine fiktive Führung über eine Überführung.
3. **Länge:** Die Skizze kommt auf ≈ 6,8 km. Nach dem Abgleich wird geprüft, ob sich ein Abschnitt ohne spielerischen Mehrwert kürzen lässt.
4. **Weitere Sichtprüfungen (DEM inkl. Gebäude):** Galata-Turm von der Yıldız-Kuppe aus verdeckt (4,6 km), Leanderturm von der Dolmabahçe Cd. aus verdeckt, Sultan-Ahmed von Kabataş aus verdeckt. Diese Wahrzeichen bleiben Fernkulisse und werden nicht in die Sicht gerückt.

### Nach der Freigabe (Phase 4)
OSM-Achsen übernehmen → Strecke glätten (Mindestradius, Breite, Steigungsgrenze) → neues Strecken-JSON mit neuer Strecken-ID, damit alte Bestzeiten nicht vermischt werden → Tests anpassen: Länge und Kurvenzahl aus dem Plan, gegen den Uhrzeigersinn bleibt bei Variante A → KI-Runde und volles Rennen auf dem Prüfstand → Boxengasse und Startplätze prüfen.
