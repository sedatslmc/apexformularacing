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
