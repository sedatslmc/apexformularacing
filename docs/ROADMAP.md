# Roadmap

Status: **geplant / implementiert / getestet / poliert**. „Getestet“ heißt automatisierte Tests und, wo nötig, manuelle Abnahme.

| Meilenstein | Inhalt | Status |
|---|---|---|
| **M0** Audit und Architektur | Migrationsplan, Unity-Paket, Zustandsautomat, versioniertes Speichern, Einrichtungsmenü, Prüf-Pipeline, Windows-Einrichtung per Doppelklick | implementiert, getestet; läuft in Unity 6.6 (Spielertest 1) |
| **M1** Fahr-Vertical-Slice | Fahrphysik, Assistenzsysteme, Kamera, Telemetrie, Tastatur/Gamepad/Touch/Neigung | implementiert, getestet; Spielertest 1 umgesetzt (Lenkung). **Offen:** erneute Prüfung durch dich, Gerätetest |
| **M2** Istanbul-Graybox | Layout v0.2 (20 Kurven), Boxengasse mit Boxenmauer und 20 Boxen, gemalte Startplätze, Kurvenschilder T1–T20 mit Namen, Bremsschilder 150/100/50, Minimap aus Spline-Daten, Gelände, Wasser | implementiert; Layout getestet. **Offen:** Fahrtest im Editor, Vorbacken der Geometrie |
| **M3** Istanbul-Visual-Slice | Ortaköy-Anstieg, Start/Ziel in Zielqualität | geplant; braucht Referenzboard und lizenzierte bzw. eigene Assets |
| **M4** HUD und Eingabe-UX | Timing-Tower (Tab), Infopanel, Tacho mit RPM-Leiste, Reifenanzeige, Startampel, priorisierte Meldungen, Teamfunk (R/Touch, nicht-modal), Minimap (M), HUD-Presets (H), Layout-Prüfung (F2), Tastenbelegung laut Brief | implementiert (IMGUI-Übergang). **Offen:** Umstieg auf layoutverwaltetes UI, Tastenbelegung frei änderbar, Seitenverhältnis-Testmatrix |
| **M5** KI und Rennwochenende | Rennlinie (minimale Krümmung), KI mit 20 fiktiven Fahrern und Persönlichkeiten, Überholen/Verteidigen/Platz lassen, Qualifying, 5-Ampel-Start mit Frühstart, Rennen, Ergebnisse, Punkte, gelbe/blaue Flaggen, Tracklimit-Verwarnungen und -Strafen | implementiert; KI-Rennen auf beiden Strecken getestet (Prüfstand). **Offen:** Test im Editor, Safety Car/VSC |
| **M6** Erweiterte Physik | Reifentemperatur, Verschleiß, Mischungen S/M/H/I/W, Nässe-Grip | implementiert, getestet. **Offen:** Schäden, Ride-Height-Empfindlichkeit, Windschatten |
| **M7** Wetter und Strategie | Wetterzustände, Regen, Nässe und Abtrocknen, dynamischer Wechsel mit Prognose, Boxenstopps (Reifenwechsel, Crew-Zeit, Begrenzer, Boxen-Assistent), KI-Strategie, Pflichtstopp-Regel | implementiert, Logik getestet. **Offen:** Ideallinie trocknet schneller, Gischt |
| M8 | Boxencrew-Animationen, Replays, TV-Kameras | geplant (Boxenstopp-Timer und Zustand vorhanden) |
| M9 | Istanbul komplett poliert | geplant |
| **M10** Menüs, Garage, Karriere | Grand-Prix-Setup mit Streckenvorschau, Meisterschaft (Kalender, Punkte, Speichern, Fortsetzen), Zeitfahren, Training, Garage (Team, 20 Lackierungen, Startnummer), Level 1–100 mit Belohnung pro Level, XP aus Ergebnissen | implementiert; Fortschritt getestet. **Offen:** Karriere-Entwicklungsmodus, Challenges |
| M11 | Weitere Strecken | geplant (Pipeline vorhanden) |
| M12 | Release-Vorbereitung | geplant |

## Nächste kleine Schritte
1. Du testest die neue Version im Editor: ein Grand Prix (Qualifying + Rennen) auf der Bosporus-Strecke und einen Boxenstopp. Bitte Fehler aus der Konsole und Eindrücke melden.
2. Danach: Safety Car/VSC, Schäden, Replays und TV-Kameras, Vorbacken der Strecke, Umstieg auf layoutverwaltetes UI.
