# CC0-Materialien für Asphalt und Kerbs (Phase 1)

**Stand:** 2026-10-26.

**Status:**
- **Pipeline implementiert und getestet:** Import-Werkzeug mit Selbsttest, Laden und Anwenden im Spiel, Nässe-Korrektur, Import-Einstellungen, metrische UVs mit Test.
- **Echte Texturen fehlen noch.** Poly Haven und ambientCG sind aus der Arbeitsumgebung gesperrt (Netzwerkrichtlinie, HTTP 403).
- Ohne importierte Sätze sieht das Spiel aus wie bisher (prozedurale Texturen).

## Weg in das Spiel
1. **CC0-Satz besorgen**, eine der beiden Möglichkeiten:
   - **a)** Im Browser von polyhaven.com oder ambientcg.com laden (2K, PNG oder JPG, Karten Diffuse bzw. Color, Normal GL, Roughness, AO).
   - **b)** In den Umgebungseinstellungen `api.polyhaven.com` und `dl.polyhaven.org` freigeben; dann lädt das Werkzeug selbst (`--polyhaven <id>`).
2. **Importieren:**
   ```sh
   python3 tools/textures/import_cc0.py <ordner-oder-zip> --name asphalt --metres <kachelgröße> --source-url <seite> --author "<autor>"
   python3 tools/textures/import_cc0.py <ordner-oder-zip> --name kerb --metres <kachelgröße> --paint-base --source-url <seite> --author "<autor>"
   ```
   - Ergebnis in `Runtime/Resources/Textures/CC0/<name>/`:
     - `BaseColor`, `Normal` (OpenGL; DirectX wird umgerechnet), `MetallicSmoothness` (URP: Glätte im Alpha), `Occlusion`
     - bei Kerbs zusätzlich `PaintBase`: entsättigte Struktur, die mit Rot bzw. Weiß eingefärbt wird
     - `source.json` mit Herkunft, Autor, Lizenz, Kachelgröße, mittlerer Glätte und SHA-256 der Dateien
   - Das Werkzeug gibt die Zeile für `docs/ASSET_LICENSES.md` aus; **die Zeile muss eingetragen werden.**
3. **Im Spiel wird automatisch verwendet:**
   - `asphalt` → Fahrbahn (leicht aufgehellt) und Auslauf (heller getönt, älterer Belag)
   - `kerb` → Kerbs (Rot und Weiß aus `PaintBase` × Kerbfarbe, Normal-, Glätte- und AO-Karte)
   - Ohne Satz bleibt der jeweilige Fallback.

## Technische Punkte
- **Maßstab:** Fahrbahn, Kerbs und Auslauf haben metrische UVs (`RoadSurface.UvMetres` = 10 m je UV-Einheit; Test `RoadKerbAndRunoffUvsAreMetricSoScannedTexturesKeepTheirSize`). Die Kachelung ist 10 m / Kachelgröße des Satzes; ein 4-m-Scan bleibt also 4 m groß.
  - Der Auslauf hatte vorher einen eigenen Maßstab (4 m). Er ist jetzt metrisch; die prozedurale Kachelung ist so angepasst, dass das Bild gleich bleibt.
- **Nässe:**
  - URP skaliert die Glättekarte mit `_Smoothness`, kann sie aber nicht über den Scan hinaus anheben.
  - Bei Nässe schaltet `WeatherVisuals` die Karte ab und blendet von der mittleren Scan-Glätte zum Nassfilm (0,88). Trocken gilt die Karte unverändert.
- **Import-Einstellungen** (`Editor/CC0TextureImporter.cs`):
  - Normal-Map-Typ, Datenkarten linear, trilinear mit Anisotropie 8
  - Mobil höchstens 2048 px ASTC 6×6; PC höchstens 4096 px, BC7 bzw. BC5 für Normalen
- **Repo-Größe:** Ein 2K-Satz hat etwa 10–20 MB als PNG. Für 4K-PC-Texturen ist Git LFS zu prüfen, bevor sie eingecheckt werden.

## Auswahlkriterien (vor dem Import ansehen)
- **Asphalt:**
  - feine Körnung, mittel- bis dunkelgrau
  - wenig Risse und keine Schlaglöcher (die Fahrfläche bleibt glatt, AGENTS.md)
  - keine Markierungen, gleichmäßig kachelbar
  - Kachelgröße 2–5 m
- **Kerb-Basis:**
  - glatter, gegossener Beton oder lackierte Fläche mit feiner Struktur und Gebrauchsspuren
  - geringe Sättigung (sie wird eingefärbt)
- **Poly Haven:**
  - Im Netz gefundene Kandidaten (Websuche, nicht selbst geprüft, die Seite war nicht erreichbar): `concrete_tiles`, `gravel_embedded_concrete`, `painted_concrete` (laut einer Spiegelseite von Dario Barresi und Dimitrios Savva).
  - **Asphalt-IDs konnte ich nicht bestätigen**; sie werden vor dem Import auf der Seite ausgewählt.
- **ambientCG:** Die Asphalt-Reihe (Namen wie `AsphaltNNN`) ist vor dem Import auf der Seite zu prüfen.

## Prüfung nach dem Import (Unity)
- Rennkamera bei 300 km/h:
  - keine sichtbare Kachelwiederholung auf den Geraden
  - Moiré-Freiheit bei flachem Blickwinkel (Anisotropie)
- Kerbs aus der Nähe: Lack mit Abnutzung, nicht plastisch.
- Regen: glänzender Nassfilm ohne Sprung beim Übergang trocken/nass. Bei sichtbarem Sprung das Schaltniveau (2 % Nässe) anpassen.
