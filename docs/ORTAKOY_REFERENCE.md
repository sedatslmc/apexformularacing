# Ortaköy-Referenzabschnitt – Priorität B (Architektur) und C (Menschen, Tiere)

**Stand:** 2026-10-31.

**Status:**
- implementiert; automatisch getestet (Simulation, Kompiliertests).
- Die Optik ist nur in three.js-Vorschauen geprüft. **Im Unity-Editor noch nicht gesehen.**
- Alles hier ist **prozedurale Platzhalter-Art, nicht fotorealistisch.** Die Premium-Assets für Ortaköy sind in `docs/ORTAKOY_VERTICAL_SLICE_PLAN.md` geplant, aber noch nicht beauftragt.

## Umfang
**Referenzabschnitt:** alle OSM-Gebäude im Umkreis von 320 m um die Ortaköy-Moschee (170 Gebäude, davon 145 mit Straßenfront) und die Basargassen. Dort gilt die volle Detailstufe; der Rest der Stadt bekommt einen Teil davon.

| Detail | Ortaköy | übrige Stadt | Code |
|---|---|---|---|
| Läden im Erdgeschoss | ~70 % der Straßenachsen und alle Geschäftshäuser | ~20 % der Achsen und alle Geschäftshäuser | `StreetFronts.IsShopBay` |
| Ladenschild (türkischer Branchenname) über jeder Ladenachse | ja; 70 % aus dem Ortaköy-Sortiment | ja; allgemeines Sortiment | `StreetFronts.SignFor`, `EnvironmentGeometry.ShopSign` |
| Markise in vier Farben, je Laden wechselnd | ja | ja | `FacadeOrnaments` |
| Senkrecht hängende Türkei-Fahnen (Balkon oder Fenster) | jede 4. Straßenfront, 1–2 Fahnen | jede 10. Straßenfront | `StreetFronts.FlagFront/FlagSpot`, `HangingFlag` |
| Haustür mit Steinrahmen und Vordach | ja | nein | `FacadeOrnaments` (`detail`) |
| Steinerne Fensterbänke | ja | nein | dto. |
| Rollladenkästen über den Läden | ja | nein | dto. |
| Café-Tische mit Stühlen und Sonnenschirm, Simitwagen | 141 Tische und 14 Wagen in den Basargassen | nein | `CityLife.Cafes`, `StreetProp` |

**Grafikstufen:**
- **Mobil:** kein Fassadenrelief, also auch keine Schilder, Fahnen oder Türen; keine Cafés.
- **Ab Mittel:** alles aus der Tabelle.
- Das Relief liegt auf der nahen LOD-Stufe der Stadtchunks; ab etwa 400 m trägt die Fassadentextur allein.

## Ladenschilder
- **Sortiment:** 43 generische Branchennamen in korrekter türkischer Großschreibung (İ/I, Ç, Ğ, Ö, Ş, Ü), teils mit Unterzeile. Keine realen Geschäfte, Marken oder Logos.
  - **Ortaköy:** KUMPİR, WAFFLE, KUMPİR & WAFFLE, BALIK EKMEK, DONDURMA, LOKUM & BAKLAVA, ÇAY BAHÇESİ, KAFE, TÜRK KAHVESİ, HEDİYELİK EŞYA, GÜMÜŞ TAKI, ANTİKACI, SAHAF, SERAMİK & ÇİNİ, HALI · KİLİM.
  - **Überall:** SİMİTÇİ, FIRIN, BÖREKÇİ, PASTANE, DÖNER, LOKANTA, PİDE SALONU, KEBAPÇI, KURUYEMİŞ, MANAV, KASAP, KAHVEHANE, ÇAY OCAĞI, BAKKAL, BÜFE, MARKET, ECZANE, KIRTASİYE, NALBUR, ÇİÇEKÇİ, KUAFÖR, BERBER, TERZİ, KUNDURACI, SAATÇİ, OPTİK, EMLAK, KUYUMCU.
- **Atlas:** `Resources/Textures/Signs/ShopSigns.jpg`, 4 × 16 Zellen à 512 × 128 px auf einer 3,1 × 0,78 m großen Tafel. Nachts leuchten die Schilder schwach (Emission = Atlas).
- **Neu erzeugen:** zuerst `cd build/Apex.ModelPreview && dotnet run -c Release -- <dir> --sign-catalog`, dann `python3 tools/signs/build_sign_atlas.py <dir>/sign_catalog.json`.
  - Der Test `SignAtlasManifestMatchesTheCatalog` meldet einen veralteten Atlas.
- Übersicht: `docs/img/ortakoy_ref_signs.jpg`.

## Senkrechte Fahnen
- **Maße:** Tuch 0,9 × 1,35 m (Seitenverhältnis 2:3 wie die Fahne), in drei leicht gewölbten Bahnen.
- **Ausrichtung:** Mondsichel und Stern hängen oben (Liek oben), wie bei senkrecht gehängten Fahnen üblich. Die Fahne ist zur Mittellinie symmetrisch, daher ist nichts gespiegelt.
  - Die genaue Vorschrift der Flaggenverordnung habe ich nicht im Wortlaut geprüft. Bitte bei der Abnahme in Unity kurz bestätigen.
- **Aufhängung:**
  - **Balkon:** am Geländer, 1,12 m vor der Fassade.
  - **Fenster:** an einer kurzen Stange unter dem Fenster, 0,2 m vor der Fassade.
  - Keine Fensterfahne im 1. OG über einem Ladenschild.

## Tests
- **`StreetFrontsTests`:**
  - Katalog in türkischer Großschrift, nur zulässige Zeichen, Kontrast zwischen Schrift und Tafel.
  - Atlas-Manifest entspricht dem Katalog; UVs 4:1.
  - Ortaköy-Mischung: 60–90 % Ortaköy-Schilder, keine davon außerhalb.
  - Mehr Läden und Fahnen in Ortaköy als anderswo.
  - Viertel richtig um die Moschee abgegrenzt.
- **`CityLifeStaysOffTheRacingSurfaceAndMoves`:** Cafés und Simitwagen nur im Viertel, nie in einem Haus, ≥ 3 m von der Strecke entfernt.
- **Freiraum:** Fassadenrelief steht an Gebäuden, die 3 m hinter der Barriere enden (`TheRealCityStandsBesideTheTrackNotOnIt`). Schilder ragen 0,1 m, Fahnen höchstens 1,2 m vor die Fassade.

## Vorschau (three.js, nicht das Spiel)
`cd build/Apex.ModelPreview && dotnet run -c Release -- <dir> --environment --real --quality 2 --frontage 2075,915`, dann
`APEX_PAGE=env.html APEX_VIEWS='name|eye|target|fov|&tod=golden&ground=1' node tools/carrender/render.mjs <dir>/frontage.obj <prefix>`.
- `docs/img/ortakoy_ref_muallim_naci.jpg`: Muallim Naci Cd. Richtung Platz; Schilder, senkrechte Fahnen, Fensterbänke.
- `docs/img/ortakoy_ref_bazaar.jpg`: Ladenzeile am Basar mit Fahnen und Schirmen.
- `docs/img/ortakoy_ref_overview.jpg`: Basargassen von oben mit Café-Tischen und Simitwagen.

Die Vorschau nutzt eine vereinfachte Fassadentextur und Licht. Sie zeigt Lage und Maßstab, nicht die Unity-Optik.

## Grenzen (ehrlich)
- **Fassaden:** Die Gebäude tragen weiter die generische Fassadentextur. Erker, Balkone, Türen und Fensterbänke sind Relief darauf; echte osmanische Holzhäuser, Gesimsprofile und Fensterläden fehlen.
- **Fahnen:** Sie sind statisch (keine Stoffsimulation, kein Wind).
- **Cafés:**
  - Die Möbel sind Low-Poly-Platzhalter.
  - Gäste sitzen nicht; Passanten gehen durch die Tischreihen (kein Ausweichen).
- **Gelände:** In steilen Gassen kann ein Schirm wegen der Geländeinterpolation etwas zu hoch oder zu tief stehen; in Unity prüfen.
- **Nicht gemessen:** Draw Calls und Frame-Kosten. Das Relief der ganzen Stadt hat jetzt ~389.000 statt 318.000 Dreiecke (Stufe 2).

## Priorität C: Menschen, Zuschauer, Tiere (seit 2026-10-31)

| Detail | Umfang (Stufe Hoch, Test) | Code |
|---|---|---|
| Stehende Zuschauer hinter den Barrieren: Muallim Naci Cd., Platz mit Kehre, Palanga-Anfahrt | 3.065 (Stufe Mittel ~60 % davon); 45 % mit türkischer Handfahne, rot-weiße Kleidung überwiegt; jubeln, wenn Autos vorbeifahren | `TracksideCrowd`, `CrowdComponent` |
| Sitzende Gäste an den Café-Tischen | 153 (etwa einer je Tisch), dem Tisch zugewandt | `CityLife.guests`, `SeatedPerson` |
| Straßenkatzen (sitzend oder liegend; rotgetigert, schwarz, grau, weiß) | 21, neben den Café-Tischen | `CityLife.cats`, `Cat` |

**Regeln, getestet in `CityLifeStaysOffTheRacingSurfaceAndMoves`:**
- **Zuschauer:**
  - stehen 1,2–8 m hinter der Barrierenlinie, nach hinten lichter werdend
  - nie im Wasser, in einem Haus oder auf einem Café-Tisch, nie in Tribünen- oder Eventflächen
  - nie zwischen zwei Fahrbahnen; die Mittelzone gehört zur Rennfläche
- **Gäste und Katzen:** nur im Viertel, nie in einem Haus, mehr als 2 m von der Rennfläche entfernt.
- Die bestehenden Tribünen, Fußgänger, der Stadtverkehr, die Fähren und Möwen bleiben unverändert.

**Höhenkorrektur der Fassaden (betrifft die ganze Stadt):**
- **Bisher:** Ein Gebäude setzte seine Ladenzeile auf den niedrigsten Eckpunkt seines Grundrisses. Am Ortaköy-Hang (Muallim Naci ~10 m, Platz ~6,5 m) lagen Läden, Türen und Schilder auf der Bergseite dadurch bis zu 4 m unter dem Gehweg.
- **Jetzt:** Jede Fassadenseite beginnt auf dem Gelände 1,5 m vor ihr (`OsmCity.Building.level`). Das gilt für die Textur und für das Relief. Die Wände reichen wie bisher bis zum tiefsten Punkt, auf der Talseite entsteht ein Sockel.
- **Wirkung in Ortaköy:** 49 Ladenfronten liegen jetzt höher.
- **Café-Tische:** Von den Tischen, die höchstens 6 m vor einer Fassade stehen, weichen nur noch 4 um mehr als 1 m vom Straßenniveau dieser Fassade ab, vorher 54 von 145. Die übrigen stehen auf offenen Plätzen.
- **Prüfmodus:** `cd build/Apex.Tuning && dotnet run -c Release -- cafe-heights`.

**Vorschau** (mit Gelände; `--frontage 2075,960`):
- `docs/img/ortakoy_people_muallim_naci.jpg`: Zuschauer an der Muallim Naci aus Höhe der Verfolgerkamera
- `docs/img/ortakoy_people_square.jpg`: Kehre auf dem Platz
- `docs/img/ortakoy_people_overview.jpg`: Übersicht
- `docs/img/ortakoy_people_cafe.jpg`: Café-Gäste

**Grenzen:**
- Zuschauer, Gäste und Katzen sind einfache Low-Poly-Figuren (Kastenformen), gedacht für Entfernungen ab etwa 15 m.
- Gäste und Katzen bewegen sich nicht.
- Die Zuschauer haben nur die Tribünenanimation (leichtes Wiegen, Springen beim Vorbeifahren, Fahnenschwenken).
- Draw-Call- und Frame-Kosten der 3.000 Zuschauer sind nicht gemessen. Sie werden instanziert gezeichnet, in Gruppen nach Farbe und 80-m-Zelle.

## Fehlende Assets und Produktionsplan
Ergänzt den Plan in `docs/ORTAKOY_VERTICAL_SLICE_PLAN.md`. Keine Käufe oder Aufträge ohne Freigabe.

| Bedarf | Heute | Ziel | Weg | Aufwand (Schätzung) |
|---|---|---|---|---|
| Fassaden-Kit Ortaköy (Läden mit Schaufenstern, Holzerker, Gesimse, Fensterläden, Dächer) | generische Textur + Relief | ~40 Module, 3 Trim-Sheets, Farbmasken | Auftrag (siehe Vertical-Slice-Plan) | 4–6 Wochen |
| Ladenfront-Varianten (Schaufenster, Auslagen, Rollläden halb offen) | dunkle Fläche der Textur | 8–10 Module mit Innenraum-Parallax | Teil des Fassaden-Kits | im Kit |
| Schilder in Handwerksqualität (Leuchtkästen, Blechschilder, Neonschrift) | Atlas mit Systemschrift | handgestaltete Schilder mit Gebrauchsspuren, Normal-Map, Emission | intern (Grafik) oder Auftrag | 1–2 Wochen |
| Fahnen mit Stoffbewegung | statische Bahnen | Vertex-Shader-Wind (wie Monumentalfahne) für Balkonfahnen | intern | 2–3 Tage |
| Café-Möbel, Simitwagen, Kumpir-Stände, Silberschmuck-Auslagen | Low-Poly | 10–15 Props, PBR | Fab/Megascans (Lizenz prüfen) oder Auftrag | 1–2 Wochen |
| Menschen (Zuschauer, Café-Gäste, Verkäufer) | Low-Poly-Kastenfiguren, Tribünen-Animation | 6–8 Charaktermodelle mit LODs, Kleidungsvarianten, Animationen (stehen, jubeln, sitzen, gehen, Fahne schwenken) | Auftrag oder Marktplatz (Lizenz prüfen); Mocap-Bibliothek | 3–5 Wochen |
| Katzen, Möwen am Boden, Hunde | Low-Poly-Katzen (statisch) | 2 Katzenmodelle mit Idle-Animationen (putzen, schlafen, laufen) | Auftrag oder Marktplatz | 1–2 Wochen |
| Pflaster der Gassen (Kopfstein) | Asphalt-/Gehwegmaterial | CC0-Kopfsteinscan | Poly Haven (CC0) | 1 Tag |
