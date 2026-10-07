# Random-Dot-Punktbahnen: technische Diagnose

Öffnen: `Assets/GlobeEffect/Demo/RandomDotTrajectoryDiagnostic.unity`, dann Play.
Das Objekt **Random-Dot Trajectory Diagnostic** enthält die Live-Einstellungen.
Die Kamera, der Originalstimulus und die farbigen Marker entstehen beim Start.
Ohne XR läuft die Szene am Bildschirm; mit aktivem XR liest die Kamera die Kopfpose.
Die Bildschirm-Hinweise sind kein VR-Menü. Bedienung per Tastatur oder Inspector.

## Bedienung

- Leertaste bzw. **Pause / Weiter**: Bewegung anhalten/fortsetzen.
- R bzw. **Neustart und Spuren löschen**: am Anfang beginnen.
- **Playback Speed = 0.25**: vierfach langsamer abspielen.
- **Show Trails**: Bewegungsspuren ein/aus.
- **Loop**: nach dem Durchlauf automatisch neu starten; alte Spuren verschwinden.
- Instrument- und Bewegungsänderungen starten neu, damit Bedingungen nicht vermischt werden.
- Wechsel von Messlineal, Heatmap, Pfeilen oder Raster lässt die Phase und die Pause unverändert.

Voreinstellung: k = 1, m = 10, FOV = 60°, Content Zoom fest auf 1,
sichtbare Geschwindigkeit nahe der Mitte 12°/s, einseitiger Schwenk 2 Sekunden.
Die virtuelle Schwenkgeschwindigkeit ist 12/m °/s. Playback Speed verändert
zusätzlich das tatsächliche Abspieltempo, nicht die räumliche Bahn.

## Was genau gezeichnet wird

Ein Punktraster, voreingestellt 3 x 3: drei Spalten (rot, grün, blau), jeweils
drei Höhen mit abgestufter Helligkeit. Mit **Marker Columns** und **Marker Rows**
lassen sich bis zu 15 x 15 Punkte einstellen; die Farben laufen dann von rot über
grün bis blau. Bei einer geraden Anzahl liegt das Raster symmetrisch um die Mitte,
ohne Spalte bzw. Zeile genau in der Mitte. Punkte, die mit dem eingestellten
Abstand weiter als 80° außen lägen, fallen weg. Die geordnete Anordnung bezieht sich auf die mittlere Blickstellung
(Schwenkwinkel null). Der Schwenk beginnt seitlich davon. Das Raster bleibt kopffest.
Die Punkte und Spuren werden weiterhin an der Kreisöffnung abgeschnitten.

Die Anfangsrichtungen werden mit der vorhandenen inversen Referenzgleichung
angeordnet. Die gesamte Bewegung berechnet **GlobeEffectVisualSpaceRandomDots.shader**,
also derselbe Shader wie im Experiment. Jede Spur ist ein kleinerer, halbtransparenter
Marker bei einem früheren Schwenkwinkel. Es werden keine horizontalen Bahnen vorgegeben
und keine zweite Vorwärtsabbildung implementiert.

Die Spur besteht aus diskreten Positionen, nicht aus einer geglätteten Linienkurve.
Trail Samples erhöht die Auflösung, aber auch die Zahl der zusätzlichen Draw Calls.
Das ist eine Diagnoseansicht, kein für den Hauptversuch optimierter Stimulus.

## Wie man damit prüft

Zunächst horizontales Panning bei k = 1 ansehen. Erreichen übereinanderliegende
Punkte dieselben senkrechten Rasterlinien gleichzeitig? Ändert sich dabei ihre Höhe?
Die horizontale Bewegung und die gesamte zweidimensionale Bahn sind getrennte Fragen.
Dann k ändern, bei sonst identischen Einstellungen vergleichen. Randabschnitte, die
das Headset abschneidet, lassen sich besser am Bildschirm betrachten.

Die Ansicht zeigt die berechnete Bewegung, beweist aber nicht, welche räumliche Form
Menschen wahrnehmen oder ob ein reales Fernglas exakt dieselbe Abbildung hat.
Keine Trials, Antwortspeicherung, PSE-Dateien oder Eye-Tracking-Abhängigkeit.
Die beiden Experiment-Szenen und deren Manager werden nicht verändert.

## Live-Geschwindigkeitsfeld

**Show Speed Heatmap** zeigt berechnete Geschwindigkeitsbeträge relativ zur
Bildmitte (1). **Show Velocity Arrows** zeigt Richtung und relative Länge der
Koordinatenraten auf einem festen gemeinsamen (u,v)-Gitter.

Die Pfeile werden separat als geglättete Schäfte mit gefüllten Spitzen gezeichnet,
nicht mehr in die Heatmap-Textur gemalt. Voreinstellung: 9×9-Raster im Kreis;
gerade Rasterwerte werden auf ungerade gesetzt, damit die Bildmitte enthalten ist.
**Arrow Size**, **Arrow Thickness** und **Arrow Color** steuern Größe, Stärke und
Farbe live. Bei Size = 1 ist ein Pfeil mit relativer Geschwindigkeit 1 genau
6 % des Felddurchmessers lang. Die Länge ist proportional zum Geschwindigkeits-
betrag, die Skalierung ist für A/S/M gleich und unabhängig von der Farbskala.
Hintergrund, Heatmap, Pfeile und Marker/Spuren haben getrennte Zeichenebenen.

Unter **Flow Coordinates** wählt man das Messlineal:

- A: flacher Bildraum. Voreinstellung, passt zur perspektivischen Bildschirmansicht.
- S: horizontale und vertikale Bildposition getrennt mit arctan umrechnen.
- M: Radius mit arctan umrechnen, Richtung beibehalten; Spezialfall l = 0.

Die drei Modi verwenden dieselbe Instrumentenabbildung. S/M sind wie im zweiten
Python-Plot nur Messsymbole an denselben Bildstrahlen. Die Marker werden NICHT
in die ausgewählten Koordinaten umgezeichnet. Daher ist ein S/M-Pfeil nicht
automatisch die tatsächliche Tangente der sichtbaren Bildschirmbahn.
Die Modi behaupten nicht, dass das Auge dieses Messlineal tatsächlich verwendet.

Die Farblegende und Zahlen stehen im Desktop-Game-Fenster; die Farbfeld-/Pfeil-
Darstellung selbst läuft auch in VR. Die voreingestellte Farbskala 0,7–1,1 bleibt
für alle Modi gleich und lässt kleine Werte über 1 zu. Werte außerhalb der Skala
bekommen die jeweilige Endfarbe. Das angezeigte Feld-Minimum/Maximum hilft dabei,
solche Sättigung zu erkennen. Heatmap Opacity steuert die Deckkraft.

Die Rechnung benutzt die vorhandene inverse und vorwärtsgerichtete
Instrumenten-Referenzgleichung. Eine symmetrische numerische Ableitung zweier
minimal verschiedener Schwenkwinkel liefert den lokalen Flow. Bei k = 1 ist
das Ergebnis gegen die exakte Bildraumformel geprüft:
du/dpsi = -(m + u²/m), dv/dpsi = -uv/m (horizontaler Schwenk).
Für vertikales Schwenken werden die Achsen getauscht. Der Richtungsregler
bestimmt das Pfeilvorzeichen; die relativen Geschwindigkeitsbeträge bleiben gleich.

Die Heatmap wird bei Einstellungsänderungen berechnet und ist für einen konstanten
Schwenk bei festen Instrumentenwerten ortsfest. In Pause oder nach dem Ende zeigt
sie weiter dieses theoretische Feld, nicht eine aktuelle Geschwindigkeit von null.
Playback Speed ändert das Abspieltempo; in der Rand/Mitte-Normierung kürzt es sich heraus.

Kontrollbeispiel: k = 1, m = 8, FOV = 60°. Horizontaler Rand: A ≈ 1,0052,
S/M ≈ 0,7539. Vertikaler Rand: A/S ≈ 1, M ≈ 0,9069.
Offline-Tests prüfen zusätzlich Pfeilskalierung, Richtung und das mittige Raster.
Shader, Sichtbarkeit und XR-Darstellung müssen
noch im laufenden Unity geprüft werden; die Offline-Tests prüfen Mathematik,
nicht Grafik oder Wahrnehmung.

Falls Unity "Diagnose-Overlay-Shader fehlt" meldet: Die ursprüngliche Shader-
.meta enthielt versehentlich eine 33-stellige GUID. Sie ist auf die erforderlichen
32 Hexzeichen korrigiert. Unity muss den Shader danach neu importieren (Play Mode
beenden, zum Editor zurückwechseln, neu starten). Ein zusätzlicher Unity-EditMode-
Test prüft nun Resources-Laden und Shader-Compilerfehler; er läuft nicht im
Offline-Runner. Der Materialaufbau verwendet den gemeinsamen UnityTools-Helfer.

Beim Pfeil-Umbau gab es zusätzlich einen Shaderfehler: `point` ist ein reserviertes
HLSL-Wort und wurde versehentlich als Parametername benutzt. Der Parameter heißt
jetzt `localPosition`. Der Unity-EditMode-Test erzwingt die Pass-Kompilierung,
statt nur nach dem Import auf Fehler zu prüfen. Zusätzlich wurde der korrigierte
Vertex-/Fragmentcode mit Unity-Includes über Direct3D kompiliert, einschließlich
Instancing und Stereo-Instancing; das ersetzt weiterhin keinen Sichtbarkeitstest.
