# Fahrzeug-Asset-Spezifikation „Apex FA-1“

Stand: 2026-10-25.

## APEX A01 – Stand nach der Neumodellierung (2026-10-25)

### 1. Inspektion des alten Modells
- Unity-Hierarchie: `Visual` mit `Body`, `FrontWing` und `RearWing` (jeweils LOD0–2), dazu `Wheel_*` mit `Spin` und `Upright`, `Links*`, `SteeringWheel` mit Handschuhen, Armen, Helm und `RainLight`. Alles unter einer `LODGroup`.
- **Probleme:**
  - Rumpf, Seitenkästen und Motorabdeckung waren getrennte Superellipsen-Lofts, die sich durchdrangen. Das wirkte zusammengesteckt und facettiert.
  - Die Flügel waren gerade Profile ohne Schwung.
  - Die Platten waren flach.
  - Das Lenkrad war ein Kasten, die Handschuhe waren Achteck-Körper.
  - Bargeboards (gibt es seit 2022 nicht mehr).

### 2. Was neu modelliert wurde
| Bereich | Neu |
|---|---|
| Karosserie | **eine durchgehende Fläche** von der Nasenspitze bis zum Heck. 18 Querschnitts-Keyframes: Nase, Cockpit mit Öffnung, Seitenkästen mit Unterschnitt und Schulter, Coke-Bottle-Heck. Überschwingfreie kubische Interpolation (PCHIP), Catmull-Rom im Querschnitt, 72 Punkte pro Ring (LOD0), feste Stützpunkte je Kontrollsegment gegen Verdrehen. Materialien pro Fläche: Lack, Carbon-Unterseite, Cockpit-Innenraum, Cyan-Linien. |
| Frontflügel | 4 Elemente als Spannweiten-Flächen mit NACA-Profilen: Anstieg und Verwindung nach außen, gepfeilte Vorderkante. Gewölbte Endplatten, Nasenhalter. |
| Heckflügel | Hauptblatt mit Mittelsenke, dessen Enden 90° in die Endplatten rollen („Löffel“). DRS-Klappe, zweiteiliger Beam Wing, Schwanenhals-Halter, angeschrägte Endplatten. |
| Unterboden | Bodenplatte mit Kantenflügel, Venturi-Einlass-Finnen, Planke, Diffusor-Rampe mit 6 Finnen |
| Aufbau | Airbox mit Tropfen-Querschnitt, Haifischflosse, Crash-Struktur, Auspuff (Drehkörper), LED-Regenlicht |
| Halo | Bügel und Mittelstrebe mit flachem, aerodynamischem Querschnitt |
| Spiegel | aerodynamische Gehäuse mit Spiegelglas auf Profil-Haltern |
| Räder | Reifen mit Schulter- und Seitenwand-Profil (48 Segmente), Felge mit Radabdeckung, Ring und Zentralmutter (Akzentfarbe), Mischungsring, Bremsscheibe, Radträger mit Bremsbelüftung, Radkappen-Deflektor vorne |
| Radaufhängung | Querlenker, Spurstange, Push- und Pullrod als flache Profilrohre, folgen den Rädern |
| Cockpit | Sitzschale mit Kopfstützen, 6-Punkt-Gurt, Overall-Torso, HANS, Armaturenbrett. Lenkrad mit Griffen, Display und Rahmen, 8 Tasten, 4 Drehschaltern, Schalt- und Kupplungswippen, Schaltlichtern. Handschuhe mit Daumen und Stulpe, links und rechts gespiegelt. Helm mit Visier. |

**Budgets:** LOD0 ca. 55k Dreiecke (Budget 60k), LOD1 ca. 14k (15k), LOD2 ca. 5,5k (6k). `tools/verify.sh` prüft die Budgets und die Normalenrichtung (jede Grundform einzeln und jede Baugruppe).

### 3. Materialien (URP)
- **Midnight Blue** (`CarPaint`): Complex Lit mit Klarlack, wenn verfügbar, sonst Lit
- **Electric Cyan** (`CarAccent`)
- **Carbon glänzend / satiniert:** prozedurale 2×2-Köper-Textur, metrische UVs
- Gummi, Metall (Felgen, Titan, Auspuff), Spiegel
- Emission für Display, Schaltlichter, Regenlicht und Bremsglühen

Fahrzeugmaterialien werden zur Laufzeit erzeugt. Das Editor-Setup legt sie zusätzlich als Dateien an, damit Builds die Shader enthalten.

### 4. Spielkompatibilität
Physik, Collider, Rad-Pivots, Lenkung, Federungsanzeige, Schaden (Flügel als eigene Gruppen), KI und Kameras sind unverändert angebunden. Die Handschuhe sind Kinder des Lenkrads und drehen mit, die Arme folgen.

### 5. Inspektions-Renders
`docs/img/a01_sheet.png` (Seite, Front, Heck, Draufsicht, 3/4 vorne und hinten, Frontflügel, Cockpit) und `docs/img/a01_lods.png` (LOD0/1/2).
Gerendert mit three.js (PBR, Klarlack, Umgebungsreflexionen) im Headless-Chromium, Werkzeug: `tools/carrender`. **Das ist nicht die Unity-Darstellung**, im Editor sieht es ähnlich, aber nicht identisch aus.

### 6. Vergleich mit den Referenzbildern (ehrlich)
| Aspekt | Referenz (Formel-1-Autos 2024) | A01 jetzt |
|---|---|---|
| Proportionen, Silhouette | lang, flach, breiter Frontflügel, hohes Heck | stimmig: 5,5 m, ~1,95 m breit, Heckflügel als höchster Punkt |
| Karosseriefläche | Class-A-Flächen, scharfe Kanten an Seitenkasten-Einlässen und Unterschnitten | glatt und durchgehend, aber weicher; Einlass als dunkler Einsatz statt echter Öffnung mit Lippe |
| Aerodynamische Komplexität | Hunderte Details: Flügelschlitze, Finnen, Floor-Kanten, Louvres, Halter | Grundelemente vorhanden, Feindetails fehlen (Louvres, Flügelschlitze, Floor-Scrolls, Spiegelhalter-Flügel) |
| Materialien | echtes Carbon-Gewebe, Klarlack, Abnutzung, Decals | Klarlack und Gewebe vorhanden; keine Decals, keine Abnutzung, keine gebackenen Normal- oder AO-Maps |
| Reifen | Seitenwand-Beschriftung, Gummi-Struktur | Profil und Mischungsring; keine Beschriftung (bewusst keine echte Reifenmarke) |
| Cockpit | Lenkrad mit Dutzenden Tasten, Kabeln, Polstern | Grundform, Tasten, Display und Hände; vereinfacht |
| Fahrer | geriggter Fahrer mit Fingern | starre Hände am Lenkrad, Arme als Zweiknochen-Näherung |

**Fazit:** Der A01 ist ein deutlich besseres, stimmig proportioniertes Spielmodell. Er erreicht **nicht** die Qualität der Referenzbilder und ist **nicht fotorealistisch**. Dafür fehlen modellierte Feindetails, gebackene Texturen (Normal, AO, Abnutzung) und echtes Rigging. Das sind Aufgaben für ein DCC-Werkzeug wie Blender und eine 3D-Artistin.

### 7. Verbleibende Grenzen und was extern nötig ist
- **Hero-Modell aus einem DCC-Werkzeug** mit High-Poly-Bake (Normal und AO) nach dieser Spezifikation. Alternativ ein gekauftes Modell mit kommerzieller Lizenz. Dafür brauche ich deine Freigabe, weil das eine Lizenzentscheidung ist.
- **Decals und Lackierungs-Atlas** (Startnummer, fiktive Sponsoren) mit Maskentextur
- **Geriggter Fahrer** (Hände mit Fingern, Schalt-Animation)
- Fein-Aero: Seitenkasten-Louvres, Floor-Scrolls, Flügelschlitze, Spiegel-Flügelchen
- Sichtprüfung im Unity-Editor steht aus.

## Ausgangslage (ehrlich)
- **Im Repository gibt es kein lizenziertes 3D-Fahrzeugmodell.** Geprüft wurden FBX, GLB, OBJ und Blend: keine Treffer. In `docs/ASSET_LICENSES.md` sind keine Fremd-Assets eingetragen.
- **Das Auto im Spiel ist prozedural erzeugt** (APEX A01, `CarGeometry` und `CarBuilder`, siehe oben). Er besteht aus gelofteten Rümpfen, gewölbten Flügelprofilen, Rohren und Drehkörpern und hat drei LODs, URP-Lit-Materialien und animierte Teile.
  - Er ist ordentlich proportioniert, aber **nicht fotorealistisch**: keine Class-A-Flächen, keine Texturen, keine gebackenen Normalen, kein geriggter Fahrer.
  - Vorschau: `docs/img/a01_sheet.png`, `a01_lods.png`, `a01_front34.png`, `a01_cockpit.png`. Erzeugen mit `build/Apex.ModelPreview` und `tools/carrender` (PBR) bzw. `tools/render_car_preview.py` (schnelle Flachansicht).
- **Ein Premium-Modell braucht externe 3D-Modellierung.** Das ist in einer Code-Umgebung ohne DCC-Werkzeug nicht seriös herstellbar. Diese Spezifikation ist der Auftrag dafür.

## Was der Platzhalter schon kann (bleibt beim Austausch erhalten)
| Funktion | Umsetzung | Status |
|---|---|---|
| Räder drehen und lenken getrennt (Radträger und Bremssattel lenken mit, drehen nicht) | `VehicleController.WheelSpinners` | implementiert |
| Federweg sichtbar, inklusive Kerb-Riffelung | Pivot-Position aus Einfederung und `roadOffset` | implementiert |
| Querlenker, Spurstangen, Push-/Pullrods folgen dem Rad | `CarVisuals.links` (innen Chassis, außen Radträger) | implementiert |
| Lenkrad dreht mit dem Lenkwinkel (Übersetzung 5:1), Handschuhe drehen mit, Arme folgen (2-Bone-Näherung) | `CarVisuals`, `SteeringWheelAngleDeg` | implementiert |
| Lenkrad-Display (Gang, Tempo) und Schaltlichter (Begrenzer blinkt) | TextMesh und Emissions-LEDs | implementiert |
| Regenlicht: an bei Nässe, blinkt beim Boxen-Begrenzer | Emission über MaterialPropertyBlock | implementiert |
| Bremsscheiben glühen nach Bremsleistung | einfaches Wärmemodell | implementiert |
| Reifen-Seitenwand zeigt die Mischungsfarbe (S rot, M gelb, H weiß, I grün, W blau) | `CarVisuals.UpdateCompound` | implementiert |
| Helm neigt sich mit Quer- und Längsbeschleunigung, wird in der Cockpit-Kamera ausgeblendet | `CarVisuals.FirstPerson` | implementiert |
| LOD0/1/2 (~12,7k / 5,2k / 3,0k Dreiecke inkl. Räder) | `LODGroup`, Übergänge 22 % / 7 % / 1,2 % Bildhöhe | implementiert |
| Lackierung pro Team (Grund- und Akzentfarbe) | Material-Instanzen, gecacht | implementiert |

Nichts davon ist im Unity-Editor visuell abgenommen. Die Geometrie ist offline geprüft: Normalen per Vorzeichen des Volumens, Ansichten mit Rückseiten-Culling.

## Auftrag: Premium-Modell
### Design und Recht
- Originales, fiktives Formel-Design. Proportionen: Länge ~5,4 m, Breite ~1,9 m, Radstand 3,60 m, Spur 1,62 / 1,56 m, Raddurchmesser 0,71 / 0,72 m (18″-Felgen), Reifenbreite 0,30 / 0,405 m.
- **Verboten:** Nachbau realer Fahrzeuge, F1-Marken und -Logos, echte Team-, Sponsor- und Reifenmarken, echte Lackierungen. Die Reifenbeschriftung ist eine fiktive Marke.
- **Lizenz:** Auftragsarbeit mit Übertragung aller Nutzungsrechte, oder eigenes Werk, oder CC0. Jede Datei kommt mit Autor, Quelle, Datum und Budget in `docs/ASSET_LICENSES.md`.

### Teile (Pflicht)
- **Frontflügel**
  - 4 Elemente mit Verwindung und nach außen ansteigenden Flaps
  - geschwungene Endplatten
  - Nasenhalter
- **Nase, Monocoque und Cockpitöffnung**
  - Cockpit mit Polsterrand
  - Sitz und 6-Punkt-Gurte
  - Pedalbereich nur angedeutet
- **Halo** (Titan-Optik) mit Mittelstrebe
- **Seitenkästen** mit Kühlereinlass, Unterschnitt und Kühlauslässen
- **Unterboden** mit Kanten-Flügeln, Planke, Diffusor mit Finnen und Bodenkanal-Andeutung
- **Motorabdeckung** mit Airbox/Überrollbügel und Haifischflosse
- **Heckflügel**
  - Hauptblatt und DRS-Klappe, getrenntes Objekt mit Pivot an der Vorderkante
  - Endplatten, Schwanenhals-Halter, Beam Wing
- **Radaufhängung** vorne Pushrod, hinten Pullrod, als getrennte Objekte. Pivot-Konvention siehe unten.
- **Radträger** mit Bremsbelüftungstrommel und Bremssattel. Bremsscheibe als eigenes Objekt mit Emissions-Maske.
- **Räder:** Felge mit Radabdeckung, Reifen mit Seitenwand-Beschriftung und Mischungsring als eigenes Material oder Maske
- **Kleinteile:** Spiegel (Spiegelglas eigenes Material), Regenlicht (Emission), T-Kamera und Onboard-Kameragehäuse
- **Lenkrad**
  - Displayfläche als eigenes Material und UV-Rechteck für eine Render-Textur oder ein TextMesh
  - 11–15 Schaltlicht-LEDs als Einzelobjekte oder per Vertex-ID
  - Schaltwippen, Drehregler
- **Fahrer**
  - Helm mit Visier
  - HANS, Schultern und Overall im Cockpitbereich
  - geriggte Arme und Hände (mindestens Schulter, Ellbogen, Handgelenk; Finger optional) mit IK-Zielen an den Griffen

### Hierarchie und Pivots (Unity: Meter, +Z vorne, +Y oben, +X rechts)
- **Ursprung:** Schwerpunkt, 0,30 m über dem Boden und 1,62 m vor der Hinterachse. Kein Scale ≠ 1 auf Transforms.
- **Räder:**
  - `Wheel_FL|FR|RL|RR` liegt in der Radmitte.
  - Darunter `Spin`: Drehachse lokal X, mit Reifen, Felge und Scheibe.
  - Darunter `Upright`: lenkt mit, dreht nicht.
- **Lenkrad:** `SteeringWheel`, Pivot auf der Lenksäulenachse (lokal Z), Drehung um lokal Z.
- **Lenker:** Je Lenker zwei Empties (`Link_<Ecke>_<Name>_In`, `_Out`). `_Out` ist ein Kind des Radträgers. Das Mesh ist ein Einheitsrohr entlang +Z.
- **Benannte Emissions-Teile:** `RainLight`, `BrakeDisc_*`, `ShiftLight_00…`, `Display`.
- **Collider gehören nicht zum Modell.** Die Physik-Collider baut `CarBuilder` aus dem `VehicleSetup`.

### Budgets (Mobil, Android zuerst)
| LOD | Verwendung | Dreiecke (inkl. Räder) | Draw Calls |
|---|---|---|---|
| LOD0 Spieler | Cockpit- und Verfolgerkamera, inkl. Cockpit-Innenraum und Fahrer | ≤ 60k | ≤ 12 |
| LOD0 KI | nah | ≤ 35k (ohne Innenraumdetails) | ≤ 10 |
| LOD1 | mittel | ≤ 12k | ≤ 6 |
| LOD2 | fern | ≤ 4k | ≤ 3 |

### Materialien und Texturen (URP Lit, PBR, Metallic-Workflow)
- **Lackierung:** Basistextur 2048² mit Masken für Grundfarbe, Akzent und Carbon-Sichtfelder (R/G/B). Die Teamfarben kommen per Shader-Parameter, nicht per Textur pro Team.
- **Carbon:** Detail-Normalmap mit Gewebe, gekachelt. Klarlack-Glanz über Smoothness.
- **Weitere Sets:** Mechanik/Titan, Reifen (Seitenwand-Normal und Beschriftung), Cockpit (Display, Gurte, Polster) und Fahrer, je 1024–2048².
- **Mobil:** ASTC-Komprimierung, insgesamt ≤ 24 MB Texturspeicher pro Auto inklusive Mipmaps.
- Normalmaps aus einem High-Poly-Modell gebacken.

### Lieferumfang
- FBX (Unity-Achsen, Meter)
- Texturen als PNG/TGA mit Quelldateien
- Quell-Szene (z. B. .blend)
- Kurze Liste der Objektnamen
- Lizenznachweis

## Einbindung (geplant, noch nicht implementiert)
1. Ein Prefab `Resources/Vehicles/ApexFA1.prefab` mit der Hierarchie oben.
2. `CarBuilder` nutzt das Prefab, sobald es vorhanden ist. Andernfalls baut es weiter den Platzhalter. Physik, `VehicleController` und `CarVisuals` bleiben gleich, nur die Referenzen (Spinner, Lenker, Lenkrad, LEDs) werden über die Namen gefunden.
3. Abnahme:
   - Cockpit-Kamera zeigt Lenkrad, Hände, Halo und Spiegel.
   - LOD-Wechsel ohne sichtbares Springen.
   - Budgets eingehalten (Unity Frame Debugger).
   - Draw Calls mit 20 Autos auf dem Referenzgerät gemessen.
