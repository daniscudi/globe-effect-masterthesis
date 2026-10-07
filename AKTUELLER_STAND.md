
## Worum geht es

Das Unity-Projekt enthält im Moment zwei getrennte VR-Tests. Beide sollen den
Übergang finden, bei dem eine Verzerrung weder klar konkav noch klar konvex
wirkt. Die Versuchsperson bekommt
feste, zufällig gemischte Verzerrungswerte gezeigt und antwortet nur mit einer von
zwei Möglichkeiten: beim Checkerboard `l`, beim Random-Dot-Instrument `k`.

Aktueller Pilot- und Prüfstand vom 5. Oktober 2026:
[CHECKERBOARD_PILOT.md](CHECKERBOARD_PILOT.md). Dort stehen auch die tatsächlich
gespeicherten Szenenwerte; die Checkerboard-Szene ist noch ein technischer Teststand.

Ergänzung vom 6. Oktober 2026: Beide Manager haben eine Live-Vorschau mit `P`
(erneut `P` oder `F6` beendet), ohne Sitzung/Dateien. Die Vorschauwerte sind im
Manager-Inspector direkt am Stimulus bearbeitbar. Random Dots: freie aktive
Haupttrials sind über `Free Head Movement` auswählbar und standardmäßig aktiv;
die bisherigen Sinus-/Tempo-/Wendeprüfungen greifen dann nicht. Geführtes
Kopftraining bleibt erhalten. Zusätzlich gibt es ein eigenes Simulated-Training
und `T` für ein eigenständiges Training der gewählten `Practice Motion Mode`.
Ein gerades, kopffestes Referenzraster ist nur in der Vorschau zuschaltbar und
bleibt in Training/Messung aus. Punktwelt und Bewegungsreserve bleiben endlich.

Vorschau-Korrektur: Die gesamte Stimuluskonfiguration wird vor der Vorschau
gesichert und bei `P`, `F6`, Training-/Sitzungsstart oder Deaktivierung des
Managers wiederhergestellt. Vorschauwerte beeinflussen die anschließende
Messung nicht. Random-Dot-Unterblock-Einstellung und Unterblock-Pausen sind
entfernt; alle Trials einer Bewegungsart laufen ohne weiteres `F5` durch.
Pausen zwischen Bewegungsarten bleiben erhalten, die kompatible CSV-Spalte
`mini_block_index` ist pro Bewegungsblock immer `1`.

Simulated-Vorschau: `Preview Speed Reference = Bildmitte` hält die gewünschte
sichtbare Geschwindigkeit nahe der Bildmitte auch beim Verstellen von `m`
konstant. Wie im Versuch wird dafür der virtuelle Schwenk durch `m * Content Zoom`
geteilt. Randprofile bleiben abhängig von der Instrumentenabbildung. Die
Manager-Normierung wird in Training/Messung nicht doppelt angewendet.

Separate technische Punktbahn-Diagnose:
`Assets/GlobeEffect/Demo/RandomDotTrajectoryDiagnostic.unity`. Neun farbige
Marker und diskrete Bewegungsspuren verwenden den Original-Random-Dot-Shader;
keine Experiment-Manager, Antworten oder Messdateien. Anleitung:
[RANDOM_DOT_DIAGNOSE.md](RANDOM_DOT_DIAGNOSE.md). Startanordnung in fünf
Optikbedingungen geprüft, insgesamt 119 Offline-Tests erfolgreich. Die neue
Szene sowie ihre Darstellung in Unity/VR sind noch praktisch zu prüfen.

Die Diagnose enthält jetzt eine zuschaltbare Heatmap und Geschwindigkeitspfeile
am gemeinsamen Bildgitter. A (Bildraum) ist voreingestellt; S (separate Winkel)
und M (radiale Winkel, l = 0) ändern nur die Koordinatenraten, nicht die Markerbahnen.
Der Wechsel des Messlineals erhält Phase/Pause. Horizontales und vertikales
Panning sowie die bekannten 8x/60°-Randwerte sind rechnerisch geprüft.
137 Offline-Tests erfolgreich; die neuen Overlay-Shader und die Darstellung
in Unity/VR sind noch nicht praktisch geprüft. Experimentszenen unverändert.

Das ist noch nicht das endgültige Hauptexperiment. Es ist eine technische und
methodische Grundlage, mit der wir Oomes' und Merlitz' Versuche in VR nachbilden möchten.

## Was aktuell vorhanden ist

### Statischer Checkerboard-Test

- Szene: `Assets/GlobeEffect/Demo/CheckerboardDemo.unity`
- quadratisches Schachbrett hinter einer getrennten runden Öffnung
- gleichmäßiges u/v-Gitter; seine lineare Weite wird aus dem bei Oomes
  beschriebenen Abstand von 10 Grad berechnet
- einstellbarer Winkeldurchmesser und einstellbarer Rand der Öffnung
- Öffnung für eine technische Kontrolle abschaltbar; im Versuch bleibt sie an
- kopffeste Darstellung ohne Nahdisparität, also virtuelle Unendlichkeit
- Darstellung für beide Augen, nur links oder nur rechts
- feste `l`-Werte, Wiederholungen und Reihenfolge über den Inspector
- zwei wählbare Abläufe (`Trial Sequence`): A = Checkerboard mit einstellbarer Dauer, dann
  Noise-Maske mit Antwort, dann graue Fläche; B = Checkerboard,
  dann graue Fläche mit Antwort, dann Noise-Maske. Die Phase vor dem nächsten
  Checkerboard dauert mindestens `Pre Stimulus Seconds`; die Antwortzeit
  begrenzt `Answer Timeout Seconds` (0 = unbegrenzt). Noise mit 0 Hz ist statisch,
  mit positiver Refresh Rate flimmernd
- Antwort mit Pfeiltasten oder mit Trigger und Trackpad-Klick am VR-Controller;
  die Zuordnung ist im Inspector wählbar und wird im Headset angezeigt
- englischer Welcome Screen und Training über `T`; zwei deutliche Beispiele
  erklären die Kategorien, danach folgen die eingetragenen Übungswerte in
  zufälliger Reihenfolge
- im Hauptversuch kein Antworttext pro Trial; die feste Category-A-/B-Belegung
  wird vorher im Training gelernt
- Antwortbelegung bleibt innerhalb einer Person fest und kann über
  `Swap Response Keys` zwischen Personen ausgeglichen werden
- Fixationskontrolle; ungültige Trials werden gespeichert, ausgeschlossen und
  am Ende erneut gezeigt

Im statischen Test ist `l` der Verzerrungsparameter. `l = 1` ergibt in der
aktuellen Abbildung das gerade Ausgangsgitter. `l = 0,5` ist der gemeinsame
Helmholtz-Endpunkt. Die zusätzlich gespeicherte Oomes-Zahl dient nur zur
Orientierung zwischen gemeinsamen Endpunkten. Sie ist keine erfundene
Rekonstruktion der in Oomes et al. nicht angegebenen Zwischenformel.

### Dynamischer Random-Dot-Test

- Szene: `Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity`
- schwarze und weiße Punkte hinter einer runden Öffnung mit weichem Rand
- festes, unbewegtes Fixationskreuz in der Mitte
- automatisch simulierte Schwenkbewegung; die Person muss den Kopf nicht drehen
- feste Instrumentenwerte `k` und Vergrößerung `m`; Content Zoom ist nur ein
  optionaler Zusatzzoom und steht für die Instrumentensimulation normalerweise auf 1
- getrennte Blöcke für simulierte Bewegung und echte Kopfbewegung, mit jeweils
  eigenem Training und Blockpausen; aktive Haupttrials wahlweise frei oder geführt
- Antwort erst nach der Bewegung: Pfeil links = konkav, Pfeil rechts = konvex,
  oder Trigger und Trackpad-Klick am VR-Controller (Zuordnung wählbar)
- Vergrößerung `m` bis 20; im simulierten Block laufen die Punkte in der
  Bildmitte bei jedem `m` gleich schnell (`Simulated Speed Reference`)
- dieselbe Fixationskontrolle und Wiedervorlage wie beim Checkerboard


## So wird ein Test bedient

1. Aktuellen Stand aus GitHub laden und das Projekt in Unity öffnen.
2. Eine der beiden Szenen aus `Assets/GlobeEffect/Demo` öffnen.
3. Am Objekt `Checkerboard Experiment Manager` oder `Random Dot Experiment Manager`
   Versuchsperson-ID, Reizwerte, Wiederholungen, Augenbedingung und
   Fixationseinstellungen kontrollieren.
4. Für die XR-4 prüfen: Varjo Provider aktiv, `Initialize XR on Startup` aktiv
   und Stereo Rendering Mode auf `Multi Pass`.
5. Play Mode starten. Mit `C` bei Bedarf das Eye Tracking kalibrieren und beim
   Checkerboard mit `T` das Training durchlaufen. Danach bereitet `F5` die
   Sitzung vor; `SPACE` startet den ersten Trial, sobald die Person bereit ist.
6. Beim Checkerboard mit der im Training gelernten Category-A-/B-Taste
   antworten. Beim Random-Dot-Test zeigt `F5` zuerst die Tastenbelegung im
   Headset; ein zweites `F5` führt zum passenden Blocktraining, falls aktiviert.
   Weitere `F5` bestätigen Training beziehungsweise aktive Bewegungsinstruktionen.
   Mit `T` kann die gewählte Trainingsart schon ohne Sitzung geübt werden.
   Geantwortet wird mit
   Pfeil links oder rechts beziehungsweise Trigger und Trackpad-Klick.
   `F6` bricht die Sitzung ab.

Die Eye-Tracking-Aufzeichnung startet und endet zusammen mit der Sitzung. Für
einen richtigen Versuch muss deshalb nicht zusätzlich `F9` gedrückt werden.
Der Experimenter Monitor öffnet sich normalerweise automatisch. Manuell liegt
er unter `Tools -> Globe Effect -> Open Experiment Monitor`.

Für einen Tastaturtest am Laptop kann `Require Fixation` vorübergehend
ausgeschaltet werden. Bei einer echten Messung mit der XR-4 muss die
Fixationskontrolle wieder aktiv sein.

## Welche Dateien gespeichert werden

Wenn kein eigener Ausgabeordner eingetragen ist, liegt jede Sitzung automatisch
unter `measurements` direkt im Unity-Projekt. Das funktioniert unabhängig vom
Laufwerksbuchstaben und vom Speicherort des Projekts. Gespeichert werden:

- der vorher erzeugte und zufällig gemischte Trialplan
- alle tatsächlich gezeigten Trials, einschließlich ungültiger Versuche
- Antworten, Reaktionszeiten, Reizparameter und Fixationswerte
- die vorhandenen Eye-Tracking-Rohdaten mit Pupillen-, Blick- und Statuswerten
- Zeitmarker wie Trialstart, Antwort, ungültiger Trial und Wiedervorlage
- eine zusätzliche `*_settings.json` mit den geladenen Einstellungen, dem
  Eye-Tracker und der Unity-Version; die bestehenden CSV-Spalten bleiben erhalten

Die bekannten PLACES-Spalten von `unity_timestamp` bis `gaze_distance` stehen
weiterhin in derselben Reihenfolge am Anfang der Gaze-Datei. Zusätzliche
Varjo-Statuswerte folgen danach.

Der Ordner `measurements` wird nicht zu GitHub hochgeladen.

## Was das für die Masterarbeit bedeutet

Für jeden gezeigten Verzerrungswert kann später der Anteil der Antwort
„konvex“ berechnet werden. Mit einer psychometrischen Funktion lässt sich der
50-%-Punkt schätzen. Dieser PSE ist der Wert, bei dem konkav und konvex gleich
häufig wahrgenommen werden.

Der statische und der dynamische PSE können anschließend verglichen werden, wenn
die verwendeten Skalen und Bedingungen passend zugeordnet werden. Der Random-Dot-Test
verwendet inzwischen Merlitz' Instrumentenabbildung mit `m` und `k`; ein angenommenes
Wahrnehmungs-`l` wird dem Reiz nicht zusätzlich aufgeprägt. Daraus allein folgt nicht, ob die gesamte
Wahrnehmung besser als Globus- oder Zylindereffekt beschrieben wird. Die schon
vorhandenen linearen x/y- beziehungsweise u/v-Abbildungen sind bisher
theoretische Vergleichsrechnungen. Sie sind noch nicht direkt mit den Trial-CSV
oder einer Versuchsauswertung verbunden.

## Sinnvolle nächste Schritte

1. Beide Szenen einmal vollständig auf der XR-4 testen: Rand, Monokularmodus,
   Fixation, virtuelle Unendlichkeit, Bewegungsrichtung und gespeicherte CSV.
2. Zuerst das Checkerboard-Protokoll festlegen: Ablauf A/B und Zeitraum der Blickkontrolle.
3. Reizwerte, FOV, Zeiten, Wiederholungen, Pausen und Augenbedingungen festlegen.
4. Einen kleinen Pilottest mit wenigen Personen durchführen und prüfen, ob die
   Antworten über die gewählten Werte tatsächlich von konkav zu konvex wechseln.
5. Die vorhandene psignifit-Auswertung mit der richtigen Trial-CSV ausführen.
   Sie prüft jetzt vermischte Bedingungen und doppelte gültige Trials und liefert
   den PSE mit 95%-Glaubwürdigkeitsintervall.
