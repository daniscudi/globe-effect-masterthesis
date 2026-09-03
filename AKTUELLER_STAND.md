# Aktueller Projektstand

Stand: 1. September 2026

## Worum geht es

Das Unity-Projekt enthält im Moment zwei getrennte VR-Tests. Beide sollen den
Übergang finden, bei dem eine Verzerrung weder klar konkav noch klar konvex
wirkt. Die Versuchsperson bekommt
feste, zufällig gemischte l-Werte gezeigt und antwortet nur mit einer von zwei
Möglichkeiten.

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
- Antwort: Pfeil links = konkav, Pfeil rechts = konvex
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
- feste `k`-Werte, Wiederholungen und Reihenfolge über den Inspector
- `m` bleibt ein eigener Instrumentparameter und wird nicht aus `k` berechnet
- Antwort erst nach der Bewegung: Pfeil links = konkav, Pfeil rechts = konvex
- dieselbe Fixationskontrolle und Wiedervorlage wie beim Checkerboard


## So wird ein Test bedient

1. Aktuellen Stand aus GitHub laden und das Projekt in Unity öffnen.
2. Eine der beiden Szenen aus `Assets/GlobeEffect/Demo` öffnen.
3. Am Objekt `Checkerboard Trial Session` oder `Random Dot Trial Session`
   Versuchsperson-ID, Reizwerte, Wiederholungen, Augenbedingung und
   Fixationseinstellungen kontrollieren.
4. Für die XR-4 prüfen: Varjo Provider aktiv, `Initialize XR on Startup` aktiv
   und Stereo Rendering Mode auf `Multi Pass`.
5. Play Mode starten. Mit `C` bei Bedarf das Eye Tracking kalibrieren und mit
   `F5` die Sitzung starten.
6. Mit Pfeil links oder rechts antworten. `F6` bricht die Sitzung ab.

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

Die bekannten PLACES-Spalten von `unity_timestamp` bis `gaze_distance` stehen
weiterhin in derselben Reihenfolge am Anfang der Gaze-Datei. Zusätzliche
Varjo-Statuswerte folgen danach.

Der Ordner `measurements` wird nicht zu GitHub hochgeladen.

## Was das für die Masterarbeit bedeutet

Für jeden `l`- beziehungsweise `k`-Wert kann später der Anteil der Antwort
„konvex“ berechnet werden. Mit einer psychometrischen Funktion lässt sich der
50-%-Punkt schätzen. Dieser PSE ist der Wert, bei dem konkav und konvex gleich
häufig wahrgenommen werden.

Der statische `l`-PSE und der dynamische `k`-PSE können anschließend verglichen
werden. Daraus allein folgt aber noch nicht automatisch, ob die gesamte
Wahrnehmung besser als Globus- oder Zylindereffekt beschrieben wird. Die schon
vorhandenen linearen x/y- beziehungsweise u/v-Abbildungen sind bisher
theoretische Vergleichsrechnungen. Sie sind noch nicht direkt mit den Trial-CSV
oder einer Versuchsauswertung verbunden.

## Sinnvolle nächste Schritte

1. Beide Szenen einmal vollständig auf der XR-4 testen: Rand, Monokularmodus,
   Fixation, virtuelle Unendlichkeit, Bewegungsrichtung und gespeicherte CSV.
2. Endgültige Reizwerte, FOV, Zeiten, Wiederholungen und
   Augenbedingungen festlegen.
3. Einen kleinen Pilottest mit wenigen Personen durchführen und prüfen, ob die
   Antworten über die gewählten Werte tatsächlich von konkav zu konvex wechseln.
4. Eine Auswertung für die psychometrischen Funktionen und PSEs ergänzen.
