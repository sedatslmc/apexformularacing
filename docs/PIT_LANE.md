# Boxengasse, Boxencrew und Boxenstopp-Animation

**Stand:** 2026-11-03.

**Status:**
- **Implementiert und getestet:** Logik in `Apex.Simulation` mit NUnit-Tests und Fahrtest auf der echten Boxengassen-Fläche von `bosphorus_a`.
- **Darstellung:** gegen die Unity-Referenzen kompiliergeprüft, **im Editor nicht gesehen**.
- **Platzhalter-Art:** Die Crew besteht aus denselben einfachen Figuren wie die Zuschauer. Es gibt keine Skelett-Animation, die Bewegung entsteht aus Position, Blickrichtung und Knien.

## Vorher
- **Vorhanden:** Fahrbahn, Boxenmauer, Boxengebäude mit 20 Garagen, gelbe Boxenrahmen, Ein- und Ausfahrtschilder, der Stopp-Ablauf in `RaceController`.
- **Fehlte:** Es gab **keine Crew**. Beim Stopp stand das Auto einfach still, bis die Servicezeit um war.
- **Fehler in der Spurführung:** Automatisch gefahrene Autos fuhren die ganze Boxengasse auf der Linie der Boxen, also durch alle anderen Boxen hindurch.

## Spuren und Markierungen (`PitLaneLayout`, `CircuitBuilder.BuildPitLaneMarkings`)
- **Aufteilung, gemessen von der Streckenkante:**

  | Bereich | Abstand |
  |---|---|
  | Vorfeld | 0–2 m |
  | schnelle Spur | 2–5,5 m (Mitte 3,75 m) |
  | Arbeitsspur mit den Boxen | ab 5,5 m (Auto in der Box bei 7,5 m) |
  | Crews | bis zur Garagenfront |

- **Fahrweg:**
  - Autos fahren auf der schnellen Spur.
  - 28 m vor der eigenen Box biegen sie in die Arbeitsspur ein; nach dem Stopp fahren sie über 24 m zurück auf die schnelle Spur.
  - Nach 60 m vor der Ausfahrt geht es wie bisher zurück auf die Strecke.
- **Markierungen:**
  - Weiße Trennlinie zwischen den Spuren, so weit die Boxenmauer reicht.
  - Weiße Querlinien bei Beginn und Ende des Tempolimits.
  - Ein Streifen in der Teamfarbe am Garagenende jeder Box (Box b gehört Team b/2).
- **Tests:**
  - `RoadSurfaceDriveTests.F_PitLaneIsDrivableEntryToExit`: erste, mittlere und letzte Box, mit Tempolimit, ohne Kontakt.
  - `PitCrewTests.CarsUseTheFastLaneAndTurnInOnlyForTheirBox`.

## Boxencrew (`PitCrewChoreography`, `PitCrewComponent`)
- **17 Personen je Box** in der Teamfarbe, eine Crew für jede Box, in der ein Auto der Session steht:
  - je Rad drei: Schlagschrauber, Rad ab, Rad drauf
  - Wagenheber vorn und hinten
  - zwei Stabilisierer an der Seite
  - einer für die Freigabe
- **Ablauf, gekoppelt an die echte Servicezeit** (`PitState.serviceTotal`, typisch 2,3–3,0 s, länger bei klemmender Radmutter oder neuer Nase):
  1. **Warten:** zwei Reihen vor der Garage.
  2. **Bereit:** Ist das eigene Auto in der Boxengasse und weniger als 160 m entfernt, laufen alle in 1,6 s an ihre Plätze neben der Box.
     - Der hintere Wagenheber wartet seitlich, weil das Auto von hinten kommt.
     - Niemand steht auf dem Weg des Autos oder auf der schnellen Spur.
  3. **Service** (Anteile der Servicezeit):

     | Anteil | Schritt |
     |---|---|
     | 0–8 % | Wagenheber unter Nase und Heck |
     | 16–36 % | Schlagschrauber kniet, alte Räder ab und zur Seite |
     | 38–62 % | neue Räder drauf |
     | bis 78 % | Anziehen |
     | ab 86 % | Wagenheber ab |
     | Ende | Freigabe |

     - Während die Räder ab sind, werden die Räder am Auto ausgeblendet; die Crew trägt sie.
     - Die Crew arbeitet um das Auto herum, auch wenn es ein paar Meter neben der Boxenmitte hält.
  4. **Rückkehr:** in 2,4 s zurück vor die Garage; die alten Räder werden mitgetragen.
- **Nur Reparatur** (keine Reifen): Die Radcrews bleiben stehen, Heber und Freigabe arbeiten.
- **Freigabe-Ampel je Box:** an einem Pfosten vor der Garage. Rot, solange das Auto stehen muss, grün bei der Freigabe.
- **Mobil** (Umgebungsdetails Niedrig): Nur Crews mit einem Auto auf dem Weg in die Box, in der Box oder gerade freigegeben werden gezeichnet. Wartende Crews entfallen.
- **Leistung:**
  - Instanziert gezeichnet: Personen, Räder und Werkzeuge, je Box ~4 Draw Calls.
  - Nur bis 300 m Entfernung zur Kamera.

![Boxenstopp aus dem Modell, Draufsicht – keine Spielaufnahme](img/pit_stop_choreography.png)

## Tests
- `PitCrewTests` (4):
  - Spuren
  - niemand auf der schnellen Spur, in allen Phasen
  - richtige Reihenfolge innerhalb der Servicezeit, auch bei langen Stopps
  - Wege frei vor der Ankunft, Crew danach zurück an der Garage
- `RoadSurfaceDriveTests.F_PitLaneIsDrivableEntryToExit` (Boxen 0, 10, 19).
- **Werkzeug:** `cd build/Apex.Tuning && dotnet run -c Release -- pitcrew` liefert die Positionen als CSV.

## Im Editor zu prüfen
- Ein Rennen mit Pflichtstopp und Boxen-Assistent, Stopp mit P anfordern.
  - Läuft die Crew rechtzeitig heraus?
  - Sitzt sie richtig am Auto?
  - Verschwinden die Räder am Auto und tauchen wieder auf?
- **Lage:** Stehen die Figuren auf der Fahrbahn? Ihre Höhe kommt aus der Streckenebene der Box (±1 cm).
- **Fahrweg:** KI-Autos in der Boxengasse fahren auf der schnellen Spur und biegen erst kurz vor ihrer Box ein.
- **Ampel:** Ist die Freigabe-Ampel aus der Verfolger- und Cockpit-Kamera sichtbar?
