# Istanbul Grand Prix – Bestandsaufnahme und Masterplan Welt & Umgebung

**Stand:** 2026-10-26, Branch `istanbul-world` (baut auf PR #11 `track-kit` und PR #10 auf).

**Grundsätze:**
- Nichts in diesem Dokument behauptet fotorealistische Qualität. Diesen Anspruch erfüllt erst eine Unity-Aufnahme aus der Rennkamera im Vergleich mit Referenzfotos (siehe „Motorsport-Foto-Test“).
- **Statusbegriffe:** geplant / implementiert / getestet / poliert.
- **Platzhalter** sind als solche gekennzeichnet.

## 1. Bestandsaufnahme (Audit)

| Bereich | Heute | Wirkung | Engpass-Rang |
|---|---|---|---|
| Gebäude | `CityLayout` + `EnvironmentGeometry.Building`: **Quader** mit 6 prozeduralen Fassadentexturen, Walm- oder Flachdach, 3 Entfernungsbänder (z. B. 2.863 nahe Gebäude bei niedriger Qualität) | Größter Bruch zum Ziel: „Boxen mit gemalten Fenstern“, gleiche Fassaden wiederholen sich, keine Tiefe (Fenster, Gesimse, Balkone) | **1** |
| Wahrzeichen | `LandmarkGeometry`: Silhouetten aus Grundkörpern (Moschee, Paläste, Brücke, Galata, Leanderturm, Sultan-Ahmed, Hagia Sophia); Drop-in für Prefabs vorhanden | Auf Distanz erkennbar, aus der Nähe Platzhalter | **2** |
| Material | URP Lit mit Farbe und einigen prozeduralen Texturen (Asphalt mit Normal-Map, Beton, Fassaden, Gitter); keine Masken-, Detail- oder AO-Maps, keine Decals | „Plastisch“ und steril, fehlende Abnutzung | **3** |
| Licht | `SkyModel` (echter Sonnenstand, Atmosphäre, Himmelslicht), Trilight-Ambient nach Tageszeit, linearer Nebel, prozeduraler Himmel, `PostFxStack` (ACES, Farbe, Bloom), Grafikstufen mit SSAO und einer globalen Reflexionssonde (seit 2026-10-26); kein GI | Richtung, Farbe und Verhältnis Sonne/Himmel stimmen; GI und lokale Sonden fehlen | 4 |
| Wasser | Fläche mit prozeduraler Normal-Map | Kein Fresnel, keine Tiefe, kein Schaum: „flache Ebene“ | 5 |
| Vegetation | 3 prozedurale Baumtypen (Platane, Schirmpinie, Zypresse) | Grob; keine Sträucher, Hecken, Wiesen | 6 |
| Straßenraum | Gehweg-Streifen, Laternen, 6 Straßenschilder, Sponsorentafeln | Leer: keine Möbel, Läden, Cafés, geparkten Autos | 7 |
| Rennstrecken-Infrastruktur | **Neues Kit (PR #11):** Betonblöcke, Leitplanke, Fangzaun, Tore, Aufprallschutz, Reifenstapel, Banner, Posten, LED-Tafeln mit LODs | Formen glaubwürdig, Materialien noch Platzhalter | – |
| Event | Startportal, Boxengebäude, Zeitturm, Rennleitung, Podium, Pokal, Tribünen mit instanzierten Zuschauern, Handfahnen | Vorhanden, Platzhalter-Art | – |
| Flaggen | `FlagComponent` mit Wind-Wellenmodell, 3 Detailstufen; Dolmabahçe, Tribünen, Boxendach, Portal, Galata, Leanderturm | Zu wenige, keine Monumentalflagge (jetzt ergänzt, siehe 3.) | – |
| Leben | Fähren und Schiffe auf Routen, Möwen, Umgebungsgeräusche | Grundlage gut | – |
| Wetter/Tageszeit | 5 Tageszeiten, Regen mit nassen Straßenmaterialien, Partikel | Grundlage gut; Nacht-Beleuchtung einfach | – |
| **Platzierungsfehler** | Laternen, Gehwege und Schilder wurden nur gegen den *eigenen* Streckenabschnitt geprüft | **10 Laternen und 72 Gehwegstücke standen auf der Strecke**; behoben (siehe 3.) | behoben |

## 2. Was Code leisten kann – und was nicht

**Code kann (ohne externe Assets):**
- korrekte Platzierung
- Validierung
- Detailstufen (LOD)
- Wind und Flaggen
- Tageszeit, Farbe, Post-Processing
- Verteilung von Leben
- modulare Bausätze aus geometrisch einfachen Formen (Barrieren, Masten, Terrassen)
- Shader-Logik, die im Editor geprüft wird

**Code kann nicht:**
- Fotorealismus für Ornamentik, Fassaden, Bäume, Menschen. Das kommt aus modellierten oder gescannten Assets und gescannten PBR-Texturen.
- **Prozedurale Gebäude bleiben Platzhalter**, bis die Fassaden-Kits (externe Angebotsanfrage `docs/rfq/`) oder lizenzierte Assets da sind.

## 3. In diesem Schritt umgesetzt (implementiert und getestet, in Unity nicht gesehen)
1. **Freiraumprüfung (Phase 1, kritisch):**
   - `TrackClearance` ist die Sperrzone aller Streckenteile (zwischen den Barrierenlinien inklusive Auslauf und Boxengasse).
   - `StreetFurnitureLayout` setzt Laternen, Gehwege und Schilder nur dort, wo kein Streckenteil verläuft.
   - **Tests** (`PlacementValidationTests`) prüfen Laternen, Gehwege, Gebäude, Bäume, Event-Flaggen und Dachflaggen gegen die Sperrzone der echten `bosphorus_a`.
2. **Monumentale türkische Flagge (Signatur):**
   - **Standort:** Yıldız-Hang über der Bosporus-Geraden. Er ist von **60 % der Runde** aus über das Gelände sichtbar, liegt 73 m vom nächsten Barrierenrand entfernt, und der Hang darunter fällt höchstens 3,5 m ab.
   - **Ermittlung:** `MonumentalFlagSite` (Werkzeug `dotnet run -- flagsite`). Der Standort steht in der Streckendatei; Spiel und Tests prüfen ihn nur.
   - **Einordnung:** Projektentscheidung, kein realer Flaggenstandort.
   - **Aufbau:**
     - Steinterrasse mit Brüstung und gepflasterter Oberfläche
     - achteckiges Stufenfundament, Manschette
     - konischer 60-m-Stahlmast, vergoldete Kugel als Mastspitze
     - Fahne 18 × 12 m (Seitenverhältnis 3:2, Halbmond und Stern aus `TurkishFlagDesign`)
   - **Stoff:** 46 × 31-Gitter, jedes Bild aktualisiert. Große Fahnen wellen sich langsamer (Frequenz ÷ √(L / 2,4 m)).
   - **Detailstufen:** drei für Mast und Sockel.
   - Gebäude halten 28 m Abstand.
   - Prozedurale Geometrie mit realistischen Proportionen; Offline-Vorschau geprüft.
3. **Dachflaggen:**
   - 32 türkische Flaggen auf Flachdächern und Türmen nahe der Strecke (14 bei niedriger Qualität), gebündelt um Start/Ziel, Ortaköy/T1 und Yıldız, mindestens 70 m Abstand zueinander.
   - Jeweils an der straßenseitigen Dachecke.
4. Aus PR #11: modulares Rennstrecken-Kit mit LODs; dabei wurde Aufprallschutz auf dem Asphalt entfernt.

## 4. Umsetzungsplan

### Phase 1 – Fundament
| Aufgabe | Art | Status |
|---|---|---|
| Laternen, Gehwege, Schilder, Flaggen gegen die Sperrzone prüfen | Code | **getestet** |
| Validierung auf alle weiteren Objekte ausdehnen (Ampeln, Poller, Bänke, Bäume an Straßen, Werbetafeln), sobald es sie gibt; ein gemeinsamer Test pro Objektart | Code | geplant |
| Bodenhaftung: jedes Objekt am Gelände bzw. der Straße ausrichten (kein Schweben), automatischer Test über alle Platzierungen | Code | geplant |
| Grafikstufen Mobil/Mittel/Hoch/Ultra zur Laufzeit (Schatten, Kaskaden, Render-Scale, FXAA/SMAA/MSAA/TAA, SSAO ab Hoch, LOD, Anisotropie), Menü „Grafikstufe“ (`LIGHTING_AND_GRAPHICS.md`) | Code (+ Editor-Menü für SSAO) | **implementiert, getestet ohne Unity**; Editor-Prüfung und Messung offen |
| Licht aus Sonnenstand und Himmelsmodell (NOAA, Atmosphäre, Himmelslicht, Belichtungsanpassung); Umgebungslicht folgt jetzt Tageszeit und Wolken | Code | **implementiert, getestet**; Unity-Prüfung offen |
| Globale Echtzeit-Reflexionssonde nach Grafikstufe (nasse Straße, Lack, Glas) | Code | **implementiert, kompiliergeprüft**; Unity-Prüfung offen |
| Asphalt, Auslauf und Kerbs mit gescannten CC0-Texturen (Poly Haven); Lizenzzeilen | Code + Assets (frei) | **importiert, offline geprüft**; Unity-Prüfung offen (`CC0_MATERIALS.md`) |
| Ultra-Stufe: 4K-Sätze für PC, bei Bedarf geladen (`fetch_cc0.py --tier ultra`), A/B-Schalter in den Optionen | Code + Assets (frei) | **implementiert, offline geprüft**; kein sichtbarer Gewinn in der Vorschau, Unity-Prüfung offen |
| Beton (Barrieren), Gehwege, Detail-Maps für Nahsicht | Code + Assets (frei) | geplant |
| Reifenabrieb- und Bremsspur-Decals entlang der Ideallinie | Code + Textur | geplant |

### Phase 2 – Ortaköy T1 (300–500 m Vertical Slice)
| Aufgabe | Art | Status |
|---|---|---|
| Ortaköy-Moschee als Hero-Asset (LOD0–3) | **extern** (Angebotsanfrage liegt vor) | Angebote einholen |
| Bosporus-Brücke (Kauf + Retopo oder eigene parametrische Brücke) | extern bzw. Code | Bewertung offen (`BRIDGE_ASSET_EVALUATION.md`) |
| Fassaden-Kit Ortaköy (Läden, Erker, Gesimse, Dächer) | **extern** | Angebote einholen |
| Kai, Uferpromenade, Platz: Pflaster, Geländer, Poller, Bänke, Laternen, Café-Möbel | Code (Platzierung) + Assets | geplant |
| Wasser-Shader (Wellen-Normals in Schichten, Fresnel, Sonnenglitzer, Tiefenfarbe, Uferschaum) | Shader (Editor-Prüfung nötig) | **geschrieben, Physik getestet, offline-Vorschau**; nicht kompiliert, Editor-Prüfung offen (`WATER_SHADER.md`) |
| Reflexionssonde Ortaköy, Adaptive Probe Volume | Editor | geplant |
| Flaggen an Platz und Promenade (Masten), Event-Banner | Code | geplant |

### Phase 3 – Wahrzeichen
- **Aufgaben:** Dolmabahçe mit Uhrturm, Çırağan, Akaretler-Reihenhäuser, Brücke; Fernwahrzeichen (Galata, Leanderturm, Hagia Sophia, Sultan Ahmed, Beylerbeyi) als hochwertige Impostors.
- **Weg:** externe Produktion bzw. Kauf über den vorhandenen Drop-in (`Resources/Landmarks/<id>`, Höhen-Plausibilitätsprüfung).

### Phase 4 – Stadt entlang der ganzen Runde
- **Gebäudekits je Viertel:**
  - Beşiktaş/Ortaköy: gemischt, Läden
  - Akaretler: neoklassische Reihen
  - Yıldız: Villen und Grün
  - Kabataş
- **Regelbasierte Zuordnung:** Viertel und Straßentyp bestimmen den Kit, Varianten über Masken. Keine identischen Fassaden nebeneinander (Test).
- **Straßenraum:** Gehwege mit Bordstein, Querstraßen, Treppen, Stützmauern statt grauer Wände, Höfe, geparkte Autos nur außerhalb der Sperrzone.
- **Fenster:** Interior-Mapping statt dunkler Flächen.

### Phase 5 – Türkische Identität & Event
- **Flaggen:** Monumentalflagge, Dachflaggen und Tribünenflaggen sind da. Ergänzen:
  - Balkonflaggen (schräge Wandhalter)
  - Mastreihen an Eingängen und im Fahrerlager
  - Flaggen an Kreuzungen
  - Handfahnen-Anteil der Zuschauer erhöhen
- **Fahnen-System:**
  - Die günstige, vertexbasierte Animation für ferne Fahnen ist vorhanden (`FlagComponent` Detail 2, seltenere Updates).
  - **Offen:** GPU-Vertex-Animation im Shader für viele kleine Fahnen; leichte Durchscheinwirkung des Stoffs.
- **Türkische Schrift:**
  - Straßenschilder nach Istanbuler Vorbild (weiß auf blau) sind vorhanden.
  - Ladenbeschriftung: lizenzierte Schrift nötig (`ASSET_LICENSES.md`, M4).
- **Event-Bauten im Stil „ISTANBUL GRAND PRIX – PROJECT APEX“** (Palette #C8102E, #087F8C, #C9A66B, #1D2835, #F0E9D9):
  - Medienplattformen, Kameratürme, Eingänge, Hospitality-Terrassen
  - Bremsmarker-Tafeln
- **Leben:** Streckenposten, Fotografen, Sicherheitspersonal (Figuren-Assets nötig), Kamerablitze, Bewegung in der Ferne.

### Phase 6 – Wetter & Nacht
- **Regen:** nasser Asphalt mit Pfützenmaske, Gischt hinter Autos.
- **Nacht:**
  - Flutlicht, beleuchtete Brücke, Schaufenster, LED-Werbung
  - Scheinwerfer und Rücklichter
  - dunkle Bereiche bewusst lassen

### Phase 7 – Optimierung
- **Profiling auf Zielgeräten** (keine Leistungsangaben vorher):
  - Draw Calls, Dreiecke im Bild
  - Kosten der Schattenkaskaden
  - Fahnen-Updates auf der CPU
  - Streaming der Stadtbänder

## 5. Qualitätsstufen
| Stufe | Ziel | Inhalt (Plan) |
|---|---|---|
| Mobil | Mittelklasse-Gerät, 60 fps (Ziel, ungemessen) | Umgebungsdetails Niedrig, LOD1 nah, 2 Schattenkaskaden, FXAA, kein SSAO, 2K-Texturen |
| Mittel | PC/Konsole Mittelklasse | LOD0 nah, 4 Kaskaden, FXAA bzw. SMAA |
| Hoch | starker PC | SSAO, Reflexionssonden, höhere Texturauflösung, weitere LOD-Distanzen |
| Ultra/Cinematic | sehr starker PC | maximale Texturen und LOD-Distanzen, TAA, hochwertiges Wasser, Hero-Fahnen überall jedes Bild |

Die umgesetzten Werte je Stufe stehen in `LIGHTING_AND_GRAPHICS.md` (Abschnitt 2); Mobil nutzt die 1K-Straßentexturen.

Nicht jede Funktion ist auf jeder Hardware gleichzeitig sinnvoll. Die Stufen werden im Editor festgelegt und gemessen (Aufgabe #6 „Grafik-Presets“).

## 6. Abnahmekriterien

### Vertical Slice Ortaköy
- Moschee, Brücke, Häuserzeile und Kai als abgenommene Assets (kein Platzhalter im Bild der Rennkamera).
- Keine schwebenden oder durchdringenden Objekte, nichts in der Sperrzone (automatische Tests grün).
- Kein sichtbares LOD-Umschalten bei 330 km/h aus Rennkamera und Cockpit.
- Goldene Stunde ohne Orangefilter, Wasser mit Fresnel und Glitzern, Fahnen beleuchtet.

### Motorsport-Foto-Test (endgültige Abnahme)
1. **Screenshots** im normalen Spiel aus Verfolger-, Cockpit- und TV-Kamera an 6 festen Punkten: Start/Ziel, T1-Anbremsen, T1-Scheitel, Yıldız-Kuppe, Akaretler, Bosporus-Gerade bei 300 km/h.
2. **Daneben je ein Referenzfoto** (eigene oder lizenzierte Aufnahme) derselben Stelle.
3. **Bewertung durch mindestens 3 Personen**, je Kriterium 1–5:
   - Gebäude, Wahrzeichen, Material, Licht, Schatten
   - Dichte, Straße, türkische Atmosphäre, Tiefe
   - Kantenglättung, LOD, Wiederholungen
4. **Bestanden:** Durchschnitt ≥ 4 in jedem Kriterium. Keine Post-Processing-Tricks, die schwache Geometrie verdecken.

**Bis dahin wird nichts als „fotorealistisch“ bezeichnet.**

## 7. Externe Assets – offen
- Ortaköy-Moschee, Fassaden-Kit (Angebotsanfragen in `docs/rfq/`)
- Brücke (Bewertung)
- Dolmabahçe, Çırağan, Akaretler, Fern-Impostors
- Bäume und Sträucher (lizenzierte SpeedTree-/Scan-Bibliotheken prüfen)
- Menschen (Zuschauer, Posten, Fotografen) und Fahrzeuge (geparkt): lizenzierte Assets ohne Marken
- Lizenzierte Schrift für türkische Ladenbeschriftung
- **Freigaben:** Jede Anschaffung braucht die Freigabe des Product Owners und eine Zeile in `ASSET_LICENSES.md`.
