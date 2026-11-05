# Wiederholung, TV-Kameras und Nasenkamera

Stand: **implementiert und in der Simulation getestet**. In Unity noch nicht geprüft (siehe „In Unity zu prüfen“).

## Aufzeichnung
- `ReplayClip` liegt in `Runtime/Simulation/Race/Replay.cs` und ist reines C#.
- Pro Auto werden Position, Lage (Quaternion) und Geschwindigkeit mit **20 Hz** gespeichert, das sind 8 Floats pro Auto.
- Die Aufzeichnung startet mit dem Startsignal. Der `RaceController` ruft sie am Ende jedes Physikschritts auf (`RecordReplay`).
- Der Speicher ist begrenzt: höchstens 30 Minuten, die ältesten Bilder fallen zuerst weg. 20 Autos brauchen etwa 13 KB pro Sekunde, also etwa 23 MB für 30 Minuten.
- Bei der Wiedergabe werden die Positionen linear und die Lagen per Slerp zwischen den Bildern interpoliert.
- Die Aufzeichnung wird nicht auf die Festplatte gespeichert. Sie gilt nur für die laufende Session.

## Wiedergabe
- Im Ergebnisbildschirm erscheint „Wiederholung“, sobald mehr als 2 s aufgezeichnet sind (`ResultsModel.Actions(..., replay)`).
- Danach wechselt der Zustand zu `GameState.Replay`. Esc oder das Ende der Aufnahme führt zurück zu den Ergebnissen.
- `ReplayPlayer` (Runtime) hält Rennlogik und Fahrzeugphysik an. Alle Autos werden kinematisch auf ihre aufgezeichnete Lage gesetzt. Das Safety Car wird während der Wiedergabe ausgeblendet.
- Danach bleiben die Autos am Ende der Aufnahme stehen, denn die Session ist ohnehin vorbei.
- Die Steuerung erfolgt über die Tastatur:

| Taste | Funktion |
|---|---|
| Leertaste | Pause/Weiter (am Ende: von vorn) |
| ← / → | 10 s zurück/vor |
| ↑ / ↓ | Tempo 0,1× · 0,25× · 0,5× · 1× · 2× · 4× |
| C | Kamera wechseln |
| Tab / Umschalt+Tab | nächstes/voriges Auto |
| Pos1 | an den Anfang |
| Esc | zurück zu den Ergebnissen |

- Das Overlay zeigt:
  - oben das Abzeichen „WIEDERHOLUNG“ mit Position, Nummer, Name und Tempo des Autos sowie den Kameranamen
  - unten die Zeitleiste, die Uhr, das Tempo und die Tastenhilfe

## Kameras
| Kamera | Umsetzung |
|---|---|
| TV | Kamerapositionen entlang der Strecke (`TvDirector.Sites`): alle 140 m, 3 m hinter der Barriere, 5 m hoch, auf der Kurvenaußenseite. Wenn der Platz auf einem anderen Streckenteil läge, nimmt der Director die Innenseite oder lässt den Platz aus. Er bleibt bei einer Kamera, solange das Auto weniger als 170 m entfernt und höchstens 45 m vorbei ist. Danach schneidet er auf die nächste Kamera vor dem Auto. Die Brennweite folgt der Entfernung (7–55°). |
| Verfolger | wie die Fahrkamera, 6,5 m hinter und 2 m über dem Auto |
| Nase | auf der Nase vor der Vorderachse (`RaceCamera.NoseEye` = 0 / 0,16 / 1,75 m vom Schwerpunkt) |
| Cockpit | Augenpunkt des Fahrers; das Fahrzeug wird dabei als Ego-Ansicht gezeigt |
| Helikopter | 42 m hoch, 38 m dahinter, pendelt langsam seitlich |

Die **Nasenkamera** gibt es auch beim Fahren. Sie ist der neue fünfte Modus in `RaceCamera` („Nase“) und lässt sich mit der Kamerataste durchschalten.

## Tests (`Tests/EditMode/ReplayTests.cs`)
- Interpolation und Begrenzung auf den Clip
- 20 Hz über 60 s bei 120-Hz-Physik und begrenzter Speicher
- Die TV-Plätze auf `bosphorus_a` stehen auf keinem Streckenteil (≥ 1,9 m neben jeder Fahrbahn). Über eine Runde liegt die gewählte Kamera nie mehr als 50 m hinter dem Auto. Jeder Schnitt geht nach vorn.
- Transport: Tempo, Springen, Halt am Ende, Neustart, Auto- und Kamerawechsel

## Fehlt noch / bekannte Grenzen
- Räder drehen und lenken in der Wiedergabe nicht. Pose und Geschwindigkeit werden aufgezeichnet, die Radwinkel nicht.
- Motorklang: Die Fahrzeuggeräusche folgen der angehaltenen Physik und passen nicht zum Bild.
- Die TV-Plätze stehen nicht auf den gebauten TV-Türmen, und nichts prüft, ob Gebäude die Sicht verdecken. Die Kamera kann deshalb in Stadtabschnitten durch Fassaden schauen.
- Kein Speichern oder Teilen von Replays, kein Zeitlupen-Highlight-Schnitt.

## In Unity zu prüfen
1. „Wiederholung“ nach einem Rennen: Laufen die Autos flüssig, ohne Ruckeln an den 20-Hz-Stützstellen? Stimmen die Lagen in Kurven?
2. TV-Kamera auf `bosphorus_a`: Wie oft verdecken Gebäude oder Bäume das Bild? Sind die Schnitte zu häufig oder zu selten?
3. Nasenkamera beim Fahren: Sieht man Nase und Flügel? Liegt die Kamera im Fahrzeugmodell?
4. Rückkehr zu den Ergebnissen: Podiumsaufnahme und Menü wieder korrekt.
