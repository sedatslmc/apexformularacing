# Roadmap

Status: **geplant / implementiert / getestet / poliert**. „Getestet“ heißt automatisierte Tests und, wo nötig, manuelle Abnahme.

| Meilenstein | Inhalt | Status |
|---|---|---|
| **M0** Audit und Architektur | Repo-Audit, Migrationsplan, Unity-Paketstruktur, Zustandsautomat, Speichern mit Versionierung, Hauptmenü ohne tote Buttons, Einrichtungsmenü, Prüf-Pipeline ohne Unity | implementiert; automatisiert getestet. **Noch offen:** Öffnen in Unity 6 durch dich |
| **M1** Fahr-Vertical-Slice | Auto, flaches Testoval, Tastatur/Gamepad/Touch/Neigung mit Kalibrierung, Kamera (4 Modi), Telemetrie, TC/ABS/Lenkhilfe, Zeitnahme, Tracklimits, Reset, Pause, prozeduraler Sound | implementiert; Physik und Logik automatisiert getestet. **Noch offen:** M1-Abnahme im Editor und auf dem Gerät (`TEST_PLAN.md`) |
| **M2** Istanbul-Graybox | Layout v0.2 mit 20 Kurven als Daten, fahrbare Graybox mit Gelände und Wasser | Layout implementiert und getestet; Graybox fahrbar (Laufzeit-Mesh). **Offen:** Boxengasse, Kurvenschilder, Bremsmarken, Minimap, 10-Runden-Test im Editor, Vorbacken im Editor |
| M3 | Istanbul-Visual-Slice (Ortaköy-Anstieg, Start/Ziel) | geplant; braucht Referenzboard und lizenzierte bzw. eigene Assets |
| M4 | Rennen-HUD und Eingabe-UX (Layout-Manager, Funk, Remapping, Layout-Tests) | geplant |
| M5 | KI und Rennwochenende | geplant; Autopilot ist der Ausgangspunkt |
| M6 | Erweiterte Physik (Reifentemperatur, Verschleiß, Mischungen, Nässe, Schäden) | geplant |
| M7 | Wetter und Strategie | geplant |
| M8 | Boxencrew-Animationen, Broadcast, Replays | geplant |
| M9 | Istanbul komplett poliert | geplant |
| M10 | Menüs, Garage, Karriere, Level 1–100 | geplant |
| M11 | Weitere Strecken (Alpine Crown, Neo Tokyo zuerst) | geplant |
| M12 | Release-Vorbereitung | geplant |

## Nächste kleine Schritte
1. Projekt in Unity 6 öffnen (`UNITY_SETUP.md`), M1-Abnahme durchführen und Ergebnisse melden.
2. Handling anhand deines Feedbacks abstimmen (Telemetrie F3).
3. M2: Boxengasse, Kurvenschilder T1–T20, Bremsmarken 150/100/50, Minimap aus Spline-Daten, Geometrie im Editor vorbacken.
