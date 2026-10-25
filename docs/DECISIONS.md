# Entscheidungsprotokoll

| Datum | Entscheidung | Begründung | Status |
|---|---|---|---|
| 2026-10-25 | Wechsel von Godot 4.7 zu **Unity 6 LTS** (C#, URP); Godot-Prototyp bleibt in `legacy/godot` und im Branch `godot-prototype` | Vorgabe des Master-Prompts; Freigabe durch den Product Owner | angenommen |
| 2026-10-25 | Simulation als engine-unabhängige Assembly `Apex.Simulation` (`noEngineReferences`) | testbar ohne Unity (CI, Cloud), deterministisch, klare Trennung Simulation/Darstellung | angenommen |
| 2026-10-25 | Eigenes Raycast-Fahrzeugmodell statt WheelCollider | volle Kontrolle über Reifenmodell, Substeps, Assistenzsysteme; Vorgabe: WheelCollider nicht ungeprüft nutzen | angenommen |
| 2026-10-25 | Physik mit 120 Hz, Raddrehung mit 6 Substeps | Stabilität der steifen Reifen-Rad-Kopplung | angenommen; Kosten auf dem Gerät noch messen |
| 2026-10-25 | Bosphorus GP **gegen den Uhrzeigersinn** | „Uhrzeigersinn“ widerspricht „Wasser rechts“ und den vorgegebenen Kurvenrichtungen; Freigabe durch den Product Owner | angenommen |
| 2026-10-25 | Strecken als JSON-Daten (`[Ost, Nord, Höhe, Breite]`), Layout per Generator aus Geraden und Bögen | eine Quelle für Python-Werkzeuge, Tests und Unity; leicht abstimmbar | angenommen; Import als ScriptableObject in M2 |
| 2026-10-25 | Traktionskontrolle und ABS als **Vorsteuerung über die Reifenkapazität** | die frühere Zwei-Punkt-Regelung schwang (0–100 in 3,7 s, blockierende Räder); jetzt 3,2 s und kein Blockieren | angenommen |
| 2026-10-25 | Übergangs-UI mit IMGUI für M0/M1 | ohne Assets und Prefabs lauffähig und testbar; wird in M4 ersetzt | vorläufig |
| 2026-10-25 | Spielcode als lokales UPM-Paket `com.projectapex.game`; Unity-Projekt wird lokal per Hub-Vorlage erzeugt | handgeschriebene ProjectSettings/YAML wären fehleranfällig; die Hub-Vorlage setzt URP-Versionen korrekt | angenommen |
| 2026-10-25 | Kompiliertests gegen UnityEngine 2021.3 / UnityEditor 2021.1 aus NuGet | Unity-Downloads sind in der Cloud-Umgebung gesperrt; Unity-6-Zweige über `#if` gekapselt | Übergang, bis Unity-6-Referenzen verfügbar sind |
| 2026-10-25 | Spielertest 1: getrennte Reifen vorne und hinten (Hinterreifen breiter), Lenkhilfe mit Grip-Limit, langsamerer Zahnstange bei Tempo und Gegenlenk-Assistent; 22° Einschlag; T-Cam als Standardkamera | Feedback: Heck bricht bei schnellem Einlenken aus, bei niedrigem Tempo kaum Lenkung; T-Cam bevorzugt | angenommen; Bestätigung im Editor ausstehend |
| 2026-10-25 | Rennlinie per biharmonischer Glättung (minimale Krümmung) statt Mittelpunkt-Glättung | Mittelpunkt-Glättung ergibt den kürzesten Weg, der in Haarnadeln enger ist als die Mittellinie | angenommen |
| 2026-10-25 | KI nutzt dieselbe Fahrphysik und dieselben Assistenzsysteme wie der Spieler; grip-begrenzte Lenkung, Kurvengeschwindigkeit nach tatsächlicher Bahn, Reaktion auf Untersteuern | keine „magische“ Haftung, kein Rubber-Banding (Brief, Abschnitt 12) | angenommen |
| 2026-10-25 | Haltebremse (`holdBrake`) für Start, Pause und Kalibrierung | das Halten der Bremse im Stand legte den Rückwärtsgang ein; Autos rollten vor dem Start rückwärts | angenommen |
| 2026-10-25 | Fiktives Teilnehmerfeld: 10 Teams, 20 Fahrer, frei erfundene Namen | keine echten Teams oder Fahrer (Brief) | angenommen |
| 2026-10-25 | Reifenverschleiß-Multiplikator (Standard 3×) | Strategie soll auch in kurzen Rennen eine Rolle spielen | angenommen |
| 2026-10-25 | Fahrprofile Einsteiger/**Ausgewogen (Standard)**/Profi/Individuell; TC in 3 Stufen, Lenkhilfe als Stärke 0–1 | Overhaul-Auftrag: Balanced als Standard, nicht arcadehaft, Hilfen ohne zusätzlichen Grip und ohne Zwang auf die Ideallinie | angenommen |
| 2026-10-25 | Motorträgheit mit Coulomb-Auto-Kupplung statt kinematisch gesetzter Drehzahl | realistisches Hoch- und Runterdrehen, Zwischengas, Kupplungsschlupf beim Start; Grundlage für Sound und Schaltlichter | angenommen |
| 2026-10-25 | Bodeneffekt als Potenzgesetz über die mittlere Fahrhöhe, Referenz bei ~150 km/h | Fahrhöhen-Empfindlichkeit ohne CFD; Kalibrierung so, dass die Rennlinie (Nenn-ClA) im mittleren Tempo stimmt | angenommen; Näherung |
| 2026-10-25 | Maximales Bremsmoment 5200 → 4000 N·m | vorher blockierten ohne ABS beide Achsen gleichzeitig; jetzt bestimmt die Bremsbalance, welche Achse zuerst blockiert | angenommen |
| 2026-10-25 | Fahrzeug als prozeduraler Platzhalter (eigene Geometrie mit LODs), Premium-Modell per Spezifikation extern | keine lizenzierten Assets vorhanden; keine nicht autorisierten Assets; Primitive werden nicht als fotorealistisch bezeichnet | angenommen; externes Modell offen |
| 2026-10-25 | Geometrie getrennt von GameObjects (`CarGeometry`), Offline-Vorschau mit Unity-Math-Shim | Prüfung ohne Unity-Editor möglich (Normalenrichtung, Ansichten, Dreiecksbudget) | angenommen |
| 2026-10-25 | Spielertest 2: Profi = TC niedrig + ABS + tempoabhängige Lenkung; Gegenlenk-Assistent nur bei Leistungsübersteuern; ABS mit Kurvenbremsfunktion; schnellere Lenkrampen und Zahnstange | „Profi fahren geht gar nicht“, „Lenkung direkter“; Tastatur-Runde: vorher 45 Dreher (Profi), jetzt 0 trocken und nass | angenommen; Editor-Bestätigung ausstehend |
