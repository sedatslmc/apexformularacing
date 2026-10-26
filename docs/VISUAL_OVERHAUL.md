# Grafik-Überarbeitung Bosphorus GP – Bestand, Umsetzung, Asset-Pipeline

**Stand:** 2026-10-26.

**Ehrlicher Status:**
- **Implementiert und kompiliergeprüft:** Post-Processing-Stack und Landmarken-Drop-in.
- **Im Unity-6-Editor nicht gesehen, auf Geräten nicht gemessen.**
- Alle sichtbaren Bosporus-Modelle sind weiterhin **prozedurale Platzhalter** und werden nicht als fotorealistisch bezeichnet.

## 1. Bestandsaufnahme (Audit)

| Bereich | Stand | Bewertung |
|---|---|---|
| Landmarken (Dolmabahçe, Ortaköy, Çırağan, Brücke, Galata, Leanderturm, Moscheen) | `LandmarkGeometry`: Silhouetten aus Boxen, Drehkörpern und Kuppeln; Maße aus veröffentlichten Zahlen, wo bekannt | auf Renndistanz erkennbar; aus der Nähe klar Platzhalter; keine Ornamentik, keine echten Fassadentexturen |
| Akaretler | kein Landmarken-Eintrag; generische Stadtblöcke (`CityLayout`) | die Reihenhäuser fehlen als eigenes Objekt |
| Stadt | prozedurale Blöcke mit prozeduralen Fassadentexturen, Dächer, Bäume | Dichte stimmt, Detail und Variation sind gering |
| Material | URP Lit, Farbwerte und prozedurale Texturen (`ApexMaterials`), Nässe über Glätte/Albedo | keine Normal-, Masken- oder Detail-Maps, keine Trim-Sheets, keine Decals |
| Licht | `SunModel` pro Tageszeit (goldene Stunde im WSW), Trilight-Ambient, linearer Nebel, prozeduraler Himmel | richtige Richtung und Farbe; vorher ohne Tonemapping, Bloom, Farbkorrektur, Reflexionssonden, GI oder AO |
| Wasser | seit 2026-10-26 Shader `Apex/Water` (Fresnel, Sondenspiegelung, Sonnenglitzern, Uferschaum; `WATER_SHADER.md`), Fallback Lit mit Normal-Map | keine Refraktion; Editor-Prüfung offen |
| Fahrzeug | `CarGeometry`, 3 LODs, Clearcoat (Complex Lit) | Platzhalter; Premium-Spezifikation in `VEHICLE_ASSET_SPEC.md` |

## 2. In diesem Schritt umgesetzt

### 2.1 URP-Post-Processing (`PostFxStack`, `ColorGrading`)
- **Aufbau:** Ein globales Volume wird zur Laufzeit erzeugt mit:
  - ACES-Tonemapping
  - ColorAdjustments (Belichtung, Kontrast, Sättigung) und WhiteBalance
  - Bloom (Filterqualität nach Grafikstufe) und eine leichte Vignette
  - Kantenglättung gehört seit der Grafikstufen-Umstellung zu `GraphicsQuality` (`LIGHTING_AND_GRAPHICS.md`)
- **Werte:**
  - Kommen aus `ColorGrading.Get(Tageszeit, Bewölkung, Regen)` in Apex.Simulation (NUnit-getestet).
  - Goldene Stunde: warm (+18) und etwas weicher.
  - Bewölkung und Regen: kühler und flacher.
  - Nacht: mehr Bloom der Straßenlichter.
  - Bewusst natürlich, kein Teal-Orange-Look.
- **Nur mit URP:**
  - `Apex.Runtime.asmdef` definiert `APEX_URP` über `versionDefines`, wenn `com.unity.render-pipelines.universal` ≥ 17 installiert ist.
  - Mit der Built-in-Pipeline ist die Komponente wirkungslos, und die Einstellung erscheint nicht im Menü (keine Fake-UI).
- **Einstellung:** „Nachbearbeitung (Farbe, Bloom)“, An/Aus, wirkt sofort. Die Kantenglättung bleibt dabei an.
- **Prüfung ohne Unity:** `build/Apex.Runtime.CompileCheck.URP` kompiliert den URP-Pfad gegen API-Stubs (`build/URPStubRef`). Die Stubs bilden nur die genutzten Mitglieder nach. **Die echte Prüfung ist der Unity-6-Editor.**
- **Voraussetzung im Projekt:** URP-Asset mit HDR an und dem Universal Renderer mit Post-Processing (Standard der Vorlage „Universal 3D“).

### 2.2 Landmarken-Drop-in (`LandmarkAssets`)
- **Regel:** Liegt ein Prefab unter `Resources/Landmarks/<id>`, ersetzt es den prozeduralen Platzhalter mit derselben Position und Ausrichtung.
- **IDs:**
  - `dolmabahce`, `dolmabahce_clock_tower`
  - `ortakoy_mosque`, `ciragan`, `beylerbeyi`, `galata`, `maidens_tower`, `sultan_ahmed`, `hagia_sophia`
  - `bridge_15temmuz`
- **Konvention:**
  - Meter; Ursprung in der Mitte der Grundfläche; +y oben; +z entlang der Hauptachse (Palast: Uferfassade; Moschee: Qibla-Achse).
  - Brücke: Ursprung auf Wasserhöhe mittig zwischen den Verankerungen, +z von Europa nach Asien.
- **Plausibilitätsprüfung:** Weicht die Modellhöhe um mehr als 25 % vom Referenzwert ab (Streckendatei bzw. 165 m Pylone), meldet das Log eine Warnung, z. B. bei einem Zentimeter-Export.
- Die Nachtbeleuchtung der Brücke bleibt auch mit einem Fremdmodell erhalten.

## 3. Was für Fotorealismus nötig ist (extern)
Prozeduraler Code kann keine fotorealistischen Wahrzeichen erzeugen. Nötig sind modellierte und texturierte Assets.

### 3.1 Beschaffung (Reihenfolge nach Sichtbarkeit)

| # | Objekt | Sichtbarkeit | Weg | Budget LOD0 / LOD1 / LOD2 / Impostor | Texturen |
|---|---|---|---|---|---|
| 1 | Ortaköy-Moschee mit Platz und Kai | T1-Bremszone, Hero-Shot mit Brücke | Auftragsmodell nach eigenen oder lizenzierten Referenzfotos | 120k / 40k / 10k / Billboard | 2× 4K-Trim-Sheets (Stein, Ornament) + Unique-Atlas 4K, Decals |
| 2 | 15-Juli-Märtyrerbrücke | ganze Runde im Hintergrund | Kauf mit kommerzieller Lizenz oder Auftrag (simple Geometrie, Kabel als Instanzen) | 60k / 15k / 3k | 2K-Stahl-Trim, Emissive-Maske für Nachtlicht |
| 3 | Dolmabahçe-Palast mit Uhrturm | Start/Ziel | Auftragsmodell; Fassade modular (Achsen-Kit) | 150k / 50k / 12k / Impostor | Marmor- und Ornament-Trim 4K, Decals |
| 4 | Çırağan-Palast (Uferfassade) | Bosporus-Gerade | modularer Kit wie Dolmabahçe | 80k / 25k / 6k | gemeinsamer Trim mit #3 |
| 5 | Akaretler-Reihenhäuser | T7/T8 | modularer Kit (Fenster, Gesims, Erker); Platzierung über OSM-Grundrisse | 15k pro Haus / 4k / 1k | 2K-Trim + Farbvarianten per Maske |
| 6 | Stadtfüllung | überall | Fassaden-Kits pro Viertel (Beşiktaş, Ortaköy, Kabataş), Dachlandschaft, Straßenmöbel | Instanzen | Trim-Sheets, Atlas |

### 3.2 Produktionsschritte pro Wahrzeichen
1. **Referenzen:**
   - Eigene Fotos (Vor-Ort-Termin) oder gekaufte bzw. lizenzierte Bilder; Lizenz in `docs/ASSET_LICENSES.md` eintragen.
   - Keine Google-Maps/Earth-Daten, keine Fremdmodelle aus anderen Spielen.
   - Drohnenflüge sind in Istanbul genehmigungspflichtig; bei Photogrammetrie vorher klären, ebenso die Abbildungsrechte an Bauwerken. Das ist keine Rechtsberatung, vor Release juristisch prüfen.
2. **Blockout in Unity mit dem Drop-in-Prefab:** Maßstab und Konvention aus 2.2; Prüfung aus der Fahrerperspektive (T-Cam, Cockpit) bei Renntempo.
3. **High-Poly bzw. Photogrammetrie:** Retopologie auf LOD0-Budget, Ornamentik als Normal-Map oder Decal statt Geometrie.
4. **Texturen:**
   - PBR Metallic/Smoothness (URP Lit): Albedo, Normal, Maske (Metallic/AO/Detail/Smoothness).
   - Texeldichte 512 px/m für Objekte in Streckennähe, 128 px/m für den Hintergrund.
   - Kompression ASTC 6×6 (Mobile), BC7 (PC).
5. **LODs und Impostors:** LOD-Group mit Übergängen bei 8 / 3 / 1 % Bildschirmhöhe; Impostor für alles jenseits von ~1,5 km.
6. **Licht:** Statische Objekte markieren; Lightmap-UVs oder Adaptive Probe Volumes (Unity 6); Reflexionssonden am Kai.
7. **Abnahme:**
   - Vergleich mit den Referenzfotos aus festgelegten Kamerapositionen (siehe 4.)
   - Polygon- und Draw-Call-Budget im Profiler
   - Lizenzeintrag

## 4. Vertical Slice (300–500 m)
- **Vorschlag:** Bremszone T1 Ortaköy, von etwa 450 m vor dem Scheitel bis 50 m danach (Rundenmeter ~1700–2200; Scheitel bei ~2150). Darin liegen das Ende der Bosporus-Geraden mit 330 km/h, die Ortaköy-Moschee am Wasser und die Brücke im Bild.
- **Alternative:** Start/Ziel mit Dolmabahçe. Dort sieht der Spieler mehr Zeit (Start, Box), der Abschnitt ist aber weniger ikonisch.
- **Inhalt:**
  - Asset #1 und #2 aus 3.1
  - Kai mit Geländer, Pflaster und Straßenmöbeln als Trim-Kit
  - Uferschaum-Decals, Reflexionssonde, Wasser-Shader mit Reflexion (Planar ist auf Mobile zu teuer; Screen-Space oder Sonde)
  - Zuschauer- und Tribünen-Assets
- **Kamerapositionen für den Vorher/Nachher-Vergleich:**
  - T-Cam bei Rundenmeter 1750 und 2100
  - Cockpit am Bremspunkt
  - Totale von der Wasserseite (Replay-Kamera)
- **Leistungsziele (Ziele, keine Messungen):**
  - Mobile (Mittelklasse 2024): 60 fps bei < 250 Draw Calls und < 600k Dreiecken im Bild
  - PC: 120 fps
  - Ob das erreicht wird, zeigt erst der Profiler auf Geräten.

## 5. Noch offen im Editor (nicht ohne Unity machbar)
- **Erledigt durch Laufzeitcode** (`LIGHTING_AND_GRAPHICS.md`): Schatten, Kaskaden, Render Scale, MSAA/SMAA/TAA, SSAO-Schalter, globale Echtzeit-Reflexionssonde und Grafikstufen (Aufgabe #6). Den SSAO-Renderer-Eintrag legt „Project Apex → 4. Grafik einrichten“ an.
- **Weiter offen:**
  - HDR und Soft Shadows im URP-Asset an (Vorlage „Universal 3D“; `supportsSoftShadows` ist zur Laufzeit nicht setzbar)
  - Adaptive Probe Volumes oder Lightmaps für die statische Umgebung
  - lokale Reflexionssonden mit Box-Projektion an Start/Ziel und Ortaköy
