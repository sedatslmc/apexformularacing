# Game Design Document (Kurzfassung)

**Project Apex**: originales, mobiles Formel-Simcade-Rennspiel im Querformat. Gefühl einer Live-Motorsport-Übertragung, verbunden mit glaubwürdiger, lohnender Fahrzeugkontrolle.

## Säulen
Geschwindigkeit · Präzision · Immersion · Renn-Spannung · ortsspezifisches Spektakel · Fortschritt

## Plattformen und Steuerung
Android zuerst, iOS über einen Mac/Xcode-Build; Windows mit Tastatur zum Entwickeln. Steuerungs-Presets:
- **Neigung (Gyro)** als Kernfeature, mit Kalibrierung, Totzone, Empfindlichkeit, Kennlinie und Glättung
- **Touch-Tasten** mit Finger-Ownership
- **Tastatur/Gamepad**

## Modi (langfristig)
Training, Qualifying, Rennen, Zeitfahren, Meisterschaft, Karriere, Challenges, Garage, Replays, Fotomodus. Offline vollständig spielbar. Kein Pay-to-win.

## Erster spielbarer Umfang (M1)
Freies Training mit einem Auto auf dem flachen Testoval und auf der Bosporus-Graybox: Rundenzeiten, Sektoren, Tracklimits, Assistenzsysteme, vier Kameras, Telemetrie.

## Flaggschiff: Bosphorus Grand Prix
Fiktiver, temporär gesperrter Kurs am europäischen Bosporus-Ufer (inspiriert vom Beşiktaş–Ortaköy-Korridor). Ca. 5,1 km, 20 Kurven, gegen den Uhrzeigersinn, 3 Sektoren:
- S1: Gerade, schwere Bremsung, Ufer
- S2: Anstieg und Hügel
- S3: Abfahrt, Fährterminal, Schlusskurve

Details: `ISTANBUL_BLOCKOUT_PLAN.md`.

## Stil
Graphit und durchscheinende Flächen, kühle neutrale Schrift, zurückhaltendes Cyan als Akzent. Lila = Bestzeit, Grün = Verbesserung, Gelb = Achtung, Rot = dringend. Kompakter Broadcast-Stil im HUD; die Bildmitte bleibt frei.
