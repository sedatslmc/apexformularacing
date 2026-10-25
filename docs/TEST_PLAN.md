# Testplan

## Automatisiert (ohne Unity): `tools/verify.sh`
| Bereich | Tests | Status |
|---|---|---|
| Reifenmodell | Null-Schlupf, Maximum, Abfall, Reibkreis, Lastempfindlichkeit, Oberflächen-Grip | ✅ 6/6 |
| Antrieb | Motorkurve und Begrenzer, Getriebe, Übersetzungen und Höchsttempo, Automatik ohne Gangpendeln | ✅ 4/4 |
| Zeitnahme | Interpolation unterhalb eines Ticks, Abkürzung, Rückwärtsfahren über die Linie, ungültige Runde, Live-Delta, Teleport | ✅ 6/6 |
| Strecken | Stetigkeit, gegen den Uhrzeigersinn, Abstände, Bosporus-Designziele, Projektion, JSON | ✅ 9/9 |
| Rennen/KI/Reifen/Wetter/Karriere | Startampel, Frühstart, Reihenfolge/Abstände/Ziel/Punkte, Live-Abstand, Qualifying, Tracklimit-Strafen, Rennlinie, Reifen heizen/kühlen, Mischungen und Nässe, Regen/Abtrocknen, reproduzierbares Wetter, Boxenzeit, 6-Auto-KI-Rennen auf beiden Strecken, 100 Level, XP-Kurve, Meisterschaft, Roster | ✅ 19/19 |
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

## Abnahme Rennbetrieb (M2/M4/M5/M7/M10, manuell im Editor)
| # | Prüfung | Erwartung | Ergebnis |
|---|---|---|---|
| R1 | Grand Prix, Bosporus, Qualifying + Rennen, 11 KI | Qualifying-Uhr läuft, KI fährt nacheinander los, Ergebnis ordnet nach Bestzeit, „Weiter zum Rennen“ übernimmt die Startaufstellung | ☐ |
| R2 | Start | 5 rote Lichter, zufällige Pause, „LIGHTS OUT“; Losrollen vor dem Erlöschen gibt +5 s | ☐ |
| R3 | Rennen | Tower zeigt Positionen und Abstände (Tab klappt auf), Infopanel P x/N und Runde | ☐ |
| R4 | KI | überholt auf Geraden, lässt Platz, fährt nicht absichtlich ins Heck; fährt keine Abkürzungen | ☐ |
| R5 | Funk (R / FUNK-Taste) | Fahren geht weiter; 1–6 bzw. Pfeile + Enter; schließt nach 6 s ohne Eingabe | ☐ |
| R6 | Boxenstopp (P oder Funk 1) | Boxen-Assistent fährt in die Box, Timer ca. 2,6–3,5 s, neue Reifen im HUD; ohne Assistent: Begrenzer 80 km/h, in eigener Box anhalten | ☐ |
| R7 | Wetter „Starker Regen“ | Regen, nasse Straße glänzt, weniger Grip; Intermediates/Regenreifen sind schneller als Slicks | ☐ |
| R8 | Zielflagge | Ergebnis mit Punkten, XP und ggf. Level-Aufstieg | ☐ |
| R9 | Meisterschaft | 2 Runden, Zwischenstand nach Runde 1, „Weiter“ im Hauptmenü nach Neustart der App | ☐ |
| R10 | Garage | Team- und Lackierungswechsel ist am Auto sichtbar; Belohnungen nach Level freigeschaltet | ☐ |
| R11 | F2 | Layout-Prüfung zeigt keine Überlappungen (Tastatur und Touch-Presets) | ☐ |
| R12 | Performance | FPS mit 12 Autos im Editor und auf dem Gerät notieren | ☐ |

## Spätere Matrizen (aus dem Brief, ab M4)
Eingabe × HUD-Presets × Funk offen/zu × Pause × Seitenverhältnisse (16:9, 19,5:9, 20:9, Tablet); Rennmatrix; Rendering-Matrix; Thermik-Dauertest 20–30 min.
