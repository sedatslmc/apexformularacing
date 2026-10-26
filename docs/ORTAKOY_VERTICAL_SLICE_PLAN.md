# Ortaköy-Vertical-Slice – Beschaffungs- und Produktionsplan

**Stand:** 2026-10-26.

**Status:**
- **geplant.** Nichts gekauft, nichts beauftragt; Käufe und Aufträge nur nach Freigabe.
- Preise und Lizenzangaben stammen aus Marktplatz-Suchergebnissen von Oktober 2026. Die Produktseiten von RenderHub, CGTrader und TurboSquid ließen sich aus der Arbeitsumgebung nicht direkt öffnen (DNS gesperrt). **Vor jedem Kauf Lizenztext und Preis auf der Seite prüfen.**

## Entscheidungen des Product Owners
- **T10 Bayıldım bleibt unverändert.** Layoutänderungen erst nach dem Unity-Handlingtest.
- **Ortaköy ist der erste Premium-Abschnitt.** Ziel: nahezu fotorealistisch.
- Keine primitiven Platzhalter werden als fertige Sehenswürdigkeit ausgegeben. Bis ein Asset abgenommen ist, bleibt der prozedurale Platzhalter und ist in `ASSET_LICENSES.md` als Platzhalter markiert.

## Abschnitt
- **Ort:** Bremszone T1 Ortaköy Virajı, Rundenmeter ~1700–2200 (Scheitel ~2150), also rund 500 m.
- **Fahrt:** Ende der Bosporus-Geraden mit 330 km/h, Bremsen auf 63 km/h, Ausgang Richtung Palanga.
- **Im Bild:** Ortaköy-Moschee am Wasser, Brücke dahinter und darüber, Ortaköy-Platz und Häuserzeile, Kai, Tribüne, Barrieren.

## Marktrecherche

| Bedarf | Gefunden | Qualität für unser Ziel | Lizenz (laut Listing) | Preis | Empfehlung |
|---|---|---|---|---|---|
| Ortaköy-Moschee | TurboSquid „Mediciye Mosque“ (ArasTheFighter): Low-Poly, 1024er-Textur, FBX/OBJ/MAX/DAE | **ungeeignet** als Hero-Asset (nur für Fernsicht) | TurboSquid Standard (prüfen) | 22,90 $ | **nicht kaufen**; höchstens als Fern-LOD-Referenz. Hero-Modell **beauftragen** |
| Bosporus-Brücke | RenderHub „Bosphorus Bridge“ (BsTen): High-Poly 5,8 Mio. Polygone, 2K-PBR, Blender/FBX/OBJ, 2022 | als **High-Poly-Quelle** gut; nicht spielfertig | „Extended Use License“, laut Plattform kommerziell; Weitergabe im Spiel-Build prüfen | 28–40 $ | **Kaufkandidat 1** (Freigabe): Retopologie zu Spiel-LODs, Proportionen gegen Structurae prüfen |
| Bosporus-Brücke (alt.) | CGTrader „The Bosphorus Bridge or 15 Temmuz Şehitler Köprüsü“ (VR/AR/Low-Poly) | unbekannt, nicht geprüft | Royalty Free (prüfen) | ~18 $ | nur, falls Kandidat 1 lizenzrechtlich nicht passt |
| Rennstrecken-Barrieren | Unity Asset Store „Race Track Barriers Guardrail Pack PBR“ (devotid): 46 Prefabs, 2K-PBR, Unity 2022.3 | mittel; Stil generisch | Asset Store Standard-EULA | 14,99 $ | optional als Zwischenstand; Ziel ist ein eigener Kit (siehe unten) |
| PBR-Materialien | Poly Haven, ambientCG | hoch (Scans, 4–8K) | **CC0**, kommerziell frei | kostenlos | **sofort nutzbar**: Asphalt, Kopfstein, Kalkstein, Marmor, Metall |
| Golden-Hour-Himmel | Poly-Haven-HDRIs | hoch | **CC0** | kostenlos | **sofort nutzbar** für Himmel und Reflexionen |
| Scans (Vegetation, Steine, Straßenmöbel) | Fab/Megascans | sehr hoch | Fab Standard License, laut Epic in jeder Engine nutzbar; seit 2025 teils kostenpflichtig | je Asset | Kandidat; Lizenztext vor Einsatz prüfen |
| Osmanische Fassaden-Module | nur einzelne Texturen (Fab), kein Gebäude-Kit gefunden | – | – | – | Fassaden-Kit **beauftragen** |

**Ergebnis:**
- Für die zwei wichtigsten Objekte gibt es **kein kaufbares Hero-Asset in Zielqualität**.
- Die Moschee muss **neu modelliert** werden. Die Brücke lässt sich aus dem RenderHub-Modell ableiten.
- Materialien und Himmel sind aus CC0-Quellen sofort verfügbar.

## Produktionsplan

### Phase 0 – Grundlagen (intern, ohne Kosten)
1. **PR #10 im Unity-Editor validieren** (`docs/UNITY_HANDLING_VALIDATION.md`). Das hat Vorrang.
2. **Szenen-Blockout Ortaköy:**
   - Kamerapunkte festlegen (T-Cam Rundenmeter 1750 und 2100, Cockpit am Bremspunkt, Wasser-Totale).
   - Maßstab der Platzhalter gegen OSM-Grundrisse prüfen.
3. **Licht-Setup in URP:**
   - HDRI-Himmel (Poly Haven, CC0), Sonne aus `SunModel` (goldene Stunde), Schatten 4 Kaskaden bis 300 m.
   - Reflexionssonde Ortaköy, Adaptive Probe Volume.
   - `PostFxStack` ist schon im Code.
4. **Materialbibliothek:** Asphalt, Kerb-Farbe, Kopfstein, Kalkstein, weißer Marmor, Blei- und Kupferdach, verzinkter Stahl, Glas. Alles aus CC0-Scans auf URP Lit, Texeldichte 512 px/m nah, 128 px/m fern.

### Phase 1 – Hero-Assets (extern, Freigabe nötig)

| Asset | Weg | Umfang | Aufwand (Schätzung) | Kosten (Markt-Schätzung, Angebot einholen) |
|---|---|---|---|---|
| **Ortaköy-Moschee** mit Platz und Kaimauer | Auftrag an einen erfahrenen Environment-Artist oder ein Studio (Portfolio mit osmanischer bzw. historischer Architektur) | LOD0 120–180k Dreiecke; Ornamentik über Normal-Maps und Decals; 2 Trim-Sheets 4K + Unique-Atlas 4K; LOD1–3 + Impostor; Innenraum nur durch Fenster angedeutet | 5–8 Wochen | ca. 8.000–20.000 € |
| **Bosporus-Brücke** | RenderHub-Modell kaufen (28–40 $), Retopologie intern oder freelance | Pylone, Deck, Hauptkabel, Hänger als Instanzen; LOD0 ≤ 80k + Kabel-Instanzen; Nachtlicht-Emissive | 2–3 Wochen | Modell 28–40 $ + ca. 2.000–5.000 € Retopo |
| **Fassaden-Kit Ortaköy** | Auftrag | ~40 Module (Erdgeschoss mit Läden, Fensterachsen, Erker/cumba, Gesimse, Dächer), 3 Trim-Sheets, Farbvarianten per Maske; 6–8 Gebäude für den Platz und die Uferstraße | 4–6 Wochen | ca. 6.000–15.000 € |
| **Rennstrecken-Kit** | Auftrag oder intern | Betonbarriere, Tecpro-artige Blöcke, Fangzaun mit Pfosten und Seilen, Tribünenmodul, LED-Flaggenpanel, Fahnenmasten mit Stoff-Shader; nur fiktive Sponsoren | 3–4 Wochen | ca. 4.000–10.000 € oder intern |

**Hinweise zu den Kosten:**
- Die Spannen sind grobe Marktschätzungen, keine Angebote.
- Sie hängen stark von Region, Erfahrung und Anzahl der Revisionen ab.
- Vor einer Freigabe 2–3 Angebote mit Portfolio-Prüfung einholen.

### Phase 2 – Integration (intern)
- **Drop-in über `Resources/Landmarks/<id>`:** Das ist schon implementiert. Konvention: Meter, Ursprung an der Basis, +z entlang der Hauptachse. Ein Höhen-Check warnt bei falscher Skalierung.
- **Fahrfläche und Kollision bleiben bei `RoadSurface` bzw. `TrackInfrastructurePlan` (AGENTS.md-Regel).**
  - Neue Kerb-, Barrieren- und Zaunmodelle ersetzen nur die **Optik**.
  - Kollider und Fahrdreiecke ändern sich nicht. Damit bleiben die Fahrtests A–G gültig.
- **LOD ohne Aufpoppen:**
  - Unity LODGroup mit Cross-Fade (Dithering)
  - Übergänge 8 / 3 / 1 % Bildschirmhöhe, Impostors ab ~1,5 km
  - Brücke: Kabel-LOD als Linien-Mesh
  - Abnahme per Kamerafahrt mit 330 km/h ohne sichtbares Umschalten
- **Infrastruktur-Upgrade:**
  - LED-Flaggenpanels (`FlagPanelComponent`) erhalten das neue Gehäusemodell.
  - Türkische Flaggen bekommen einen Stoff-Shader mit Vertex-Wind statt Physik-Cloth (Mobile-Kosten).

### Phase 3 – Abnahme
- Vorher/Nachher-Screenshots aus den festgelegten Kamerapunkten, mit Referenzfotos daneben.
- Profiler im Editor (PC) und auf einem Mittelklasse-Gerät. **Erst dann** gibt es Leistungsaussagen.
  - Ziel Mobile: < 250 Draw Calls, < 600k Dreiecke im Bild
- Lizenzzeile pro Asset in `docs/ASSET_LICENSES.md`: Quelle, Autor, Lizenz-Link, Kaufbeleg, Datum.

## Rechtliches und Respekt (keine Rechtsberatung, vor Release juristisch prüfen)
- **Moschee:** Erbaut 1854–56 (Architekten der Familie Balyan). Ein Urheberrecht am Bauwerk ist abgelaufen. Trotzdem gilt:
  - würdevolle Darstellung, keine Werbung an der Moschee
  - keine Zerstörungs- oder Unfalleffekte am Gebäude
- **Bosporus-Brücke (1973):** Als neueres Ingenieurbauwerk prüfen, ob ein Urheberrecht am Entwurf besteht. Auch die Panoramafreiheit nach türkischem Recht (FSEK) für 3D-Darstellungen in kommerziellen Spielen klären, ebenso den Namen und die Beleuchtungsgestaltung.
- **Referenzfotos:** eigene Aufnahmen (Vor-Ort-Termin) oder lizenzierte Fotos. Die Fotos des Columbia-Projekts MCID sind gute Formreferenzen; die Nutzungsbedingungen sind nicht geprüft, daher keine Texturen daraus.
- **Keine Google-Maps/Earth-Daten** (AGENTS.md). Grundrisse aus OSM (ODbL, Namensnennung).
- **Drohnenflüge** über dem Bosporus sind genehmigungspflichtig.

## Freigaben, die ich brauche
1. **Kauf** RenderHub „Bosphorus Bridge“ (BsTen), 28–40 $, nach Lizenzprüfung durch dich.
2. **Ausschreibung bzw. Angebote** für die Ortaköy-Moschee und den Fassaden-Kit, ohne Auftragserteilung.
3. **Rennstrecken-Kit:** extern beauftragen oder intern bauen?
4. **Fab/Megascans:** dürfen wir einzelne Assets unter der Fab Standard License nutzen? Erst Lizenztext lesen.

## Quellen
- [TurboSquid – Mediciye Mosque](https://www.turbosquid.com/3d-models/3d-model-mediciye-mosque-1907649)
- [RenderHub – Bosphorus Bridge (BsTen)](https://www.renderhub.com/bsten/bosphorus-bridge)
- [CGTrader – The Bosphorus Bridge / 15 Temmuz Şehitler Köprüsü](https://www.cgtrader.com/3d-models/exterior/landmark/the-bosphorus-bridge-or-15-temmuz-sehitler-koprusu)
- [Unity Asset Store – Race Track Barriers Guardrail Pack PBR](https://assetstore.unity.com/packages/3d/environments/roadways/race-track-barriers-guardrail-pack-pbr-111203)
- [Unity Asset Store EULA-FAQ](https://assetstore.unity.com/browse/eula-faq), [Asset Store Terms](https://unity.com/legal/as-terms)
- [Poly Haven – Lizenz/FAQ (CC0)](https://polyhaven.com/faq)
- [Epic – Fab-Start und Lizenzmodell](https://www.unrealengine.com/en-US/blog/fab-epics-new-unified-content-marketplace-launches-today)
- [Structurae – Bosporus-Brücke](https://structurae.net/structures/bosphorus-bridge), [Wikipedia – Bosphorus Bridge](https://en.wikipedia.org/wiki/Bosphorus_Bridge)
- [Columbia MCID – Ortaköy Büyük Mecidiye Camii (Referenzfotos)](https://mcid.mcah.columbia.edu/node/5883)
