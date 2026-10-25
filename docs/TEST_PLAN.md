# Testplan

## Automatisiert (ohne Unity): `tools/verify.sh`
| Bereich | Tests | Status |
|---|---|---|
| Reifenmodell | Null-Schlupf, Maximum, Abfall, Reibkreis, Lastempfindlichkeit, Oberflächen-Grip | ✅ 6/6 |
| Antrieb | Motorkurve und Begrenzer, Getriebe, Übersetzungen und Höchsttempo, Automatik ohne Gangpendeln | ✅ 4/4 |
| Zeitnahme | Interpolation unterhalb eines Ticks, Abkürzung, Rückwärtsfahren über die Linie, ungültige Runde, Live-Delta, Teleport | ✅ 6/6 |
| Strecken | Stetigkeit, gegen den Uhrzeigersinn, Abstände, Bosporus-Designziele, Projektion, JSON | ✅ 9/9 |
| Fahrdynamik (Prüfstand) | Federn tragen das Gewicht, Beschleunigung, Bremsen mit ABS, stationäre Kurve, schneller Einschlag bei 3 Tempi, Wenderadius, Gegenlenk-Assistent, Autopilot-Runden auf beiden Strecken | ✅ 11/11 |
| Eingabe und Zustände | Neigungswinkel je Ausrichtung, Kalibrierung/Totzone/Begrenzung, flaches Gerät, Glättung, Digitalachse, Touch-Ownership, hängende Touches, Zustandsübergänge, Zurück-Navigation, Einstellungs-Migration | ✅ 12/12 |
| Kompiliertest | Apex.Simulation (netstandard2.1, Warnungen = Fehler), Apex.Runtime gegen UnityEngine 2021.3 + Input System 1.11.2, Apex.Editor gegen UnityEditor 2021.1 | ✅ |

**Nicht abgedeckt:** Code hinter `#if UNITY_6000_0_OR_NEWER`, Rendering, echtes Rigidbody-Verhalten und Gerätesensoren. Das prüfen Unity-Editor und Gerät.

## M1-Abnahme (manuell im Unity-Editor, danach auf dem Gerät)
Vorbereitung: `docs/UNITY_SETUP.md`, Boot-Szene → Play → „Freies Training – Apex Testoval“.

| # | Prüfung | Erwartung | Ergebnis |
|---|---|---|---|
| 1 | 10 s stehen lassen | Auto steht ruhig, kein Zittern, Telemetrie (F3): Federweg vorne ca. 13 mm, hinten ca. 17 mm | ☐ |
| 2 | Vollgas aus dem Stand (TC an) | zügiger Start ohne Dreher, TC-Anzeige flackert kurz, Gänge schalten hoch, ca. 300 km/h auf der Geraden | ☐ |
| 3 | Vollbremsung aus Höchsttempo (ABS an) | gerade Bremsung, ABS-Anzeige, kein Blockieren | ☐ |
| 4 | Kurven mit Tastatur | vorhersehbares Einlenken, kein „Schienen“-Gefühl, Untersteuern bei Übertreibung. Spielertest 1: Heck brach bei schnellem Einlenken aus, langsam kaum Lenkung → überarbeitet, bitte erneut prüfen | ☐ |
| 5 | TC aus, früh Gas in der Haarnadel | Power-Oversteer möglich und abfangbar | ☐ |
| 6 | Über Randsteine fahren | spürbare Federung, kein absurdes Springen | ☐ |
| 7 | Komplette Runde | Rundenzeit und Sektoren erscheinen; Sektorfarben lila/grün/gelb | ☐ |
| 8 | Alle vier Räder ins Gras | Runde wird als ungültig markiert und zählt nicht als Bestzeit | ☐ |
| 9 | Rücktaste | Auto steht wieder auf der Strecke, die Runde wird abgebrochen | ☐ |
| 10 | Esc während Vollgas, dann Weiter | Simulation steht, nach „Weiter“ kein hängendes Gas | ☐ |
| 11 | Alt-Tab während Vollgas | Spiel pausiert automatisch, keine hängenden Eingaben | ☐ |
| 12 | Kamera C | Cockpit → T-Cam → Verfolger → weit, ohne Sprünge | ☐ |
| 13 | Touch-Tasten (Editor, Maus) | Gas halten und Lenken mit zweitem „Finger“ (Gerät) funktioniert; Pause-Taste oben rechts | ☐ |
| 14 | Gerät: Neigung | Kalibrierung zu Sessionbeginn; Lenken durch Drehen; „MITTE“ zentriert neu; gehalten Gas + Neigung gleichzeitig | ☐ |
| 15 | Gerät: App in den Hintergrund | Pause, beim Zurückkehren keine hängenden Eingaben | ☐ |
| 16 | Einstellungen ändern, App neu starten | Werte bleiben erhalten | ☐ |
| 17 | Bosphorus GP (Graybox) | 20 Kurven fahrbar, Anstieg/Abfahrt sichtbar, Wasser im Osten | ☐ |
| 18 | Performance | FPS-Anzeige oben rechts: Editor und Gerät notieren (Ziel 60/30, **nicht garantiert**) | ☐ |

Fehler bitte mit Schritten zur Reproduktion melden (Gerät, Einstellung, was passiert, was erwartet wurde).

## Spätere Matrizen (aus dem Brief, ab M4)
Eingabe × HUD-Presets × Funk offen/zu × Pause × Seitenverhältnisse (16:9, 19,5:9, 20:9, Tablet); Rennmatrix; Rendering-Matrix; Thermik-Dauertest 20–30 min.
