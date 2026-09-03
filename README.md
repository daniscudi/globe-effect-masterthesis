# Globe Effect

In diesem Unity-Projekt entsteht der Versuchsaufbau für meine Masterarbeit zum
Globe Effect. Der statische Checkerboard-Test orientiert sich an Helmholtz und
Oomes et al. (2009). Daneben gibt es einen getrennten Random-Dot-Test für die
Wahrnehmung während einer simulierten Schwenkbewegung.

Eine kurze Bedienungs- und Arbeitsübersicht steht in `AKTUELLER_STAND.md`.

## Was der Checkerboard-Test macht

Die Versuchsperson sieht ein kreisrundes Schachbrett mit einem roten
Fixationskreuz. Vor jedem Durchgang wird ein fester Verzerrungswert eingestellt.
Die Versuchsperson antwortet:

* Pfeil links: Muster wirkt konkav.

* Pfeil rechts: Muster wirkt konvex.

Aus mehreren Antworten pro Verzerrungsstufe kann später der Wert geschätzt
werden, bei dem beide Antworten gleich häufig vorkommen. Das ist der Punkt, an
dem das Muster subjektiv geradlinig erscheint (PSE).

Der Test kann beidäugig, nur links oder nur rechts gezeigt werden. Die
gewünschten Bedingungen werden am `Checkerboard Trial Session` im Inspector
eingestellt.

## Was der Random-Dot-Test macht

Die Versuchsperson fixiert ein rotes Kreuz in der Mitte einer runden Öffnung.
Schwarze und weiße Punkte bewegen sich dahinter automatisch von links nach
rechts und wieder zurück. Die Öffnung und das Fixationskreuz bleiben dabei
kopffest. Eine tatsächliche Kopfbewegung ist für die Hauptbedingung nicht nötig.

Vor jedem Durchgang setzt Unity einen festen Merlitz-Parameter `k`. Dieser Wert
wird nicht angezeigt und kann von der Versuchsperson nicht verändert werden.
Nach der festgelegten Bewegungsdauer verschwindet das Punktfeld und es folgt
wieder nur die Entscheidung:

* Pfeil links: Bewegung wirkt konkav.

* Pfeil rechts: Bewegung wirkt konvex.

Aus `P(konvex | k)` kann später der Übergang bestimmt werden, an dem konkav und
konvex gleich häufig geantwortet werden. Dieser dynamische `k`-PSE kann mit dem
PSE des statischen `l`-Tests verglichen werden. Er wird aber nicht automatisch
als derselbe Parameter bezeichnet.

## l und k

In der ersten Version wurde die Instrumentengleichung von Merlitz benutzt:

```text
tan(k · a) = m · tan(k · A)
```

Hier beschreibt `k` die Form der Abbildung eines optischen Instruments. `m` ist
daneben ein eigener, unabhängiger Parameter. Beide stehen in derselben
Gleichung, aber `k` wird nicht aus `m` berechnet und verändert sich nicht, wenn
nur die Vergrößerung geändert wird. Die sichtbare Wirkung eines festen `k` kann
sich mit `m` trotzdem ändern. Beim statischen Checkerboard ohne simuliertes
Fernglas wären `m` und diese Instrumentengleichung unnötig.

Merlitz führt zusätzlich den Parameter `l` für eine radiale Abbildung des
visuellen Raums ein:

```text
y_l(a) = tan(l · a) / l
```

In dieser Funktion kommt keine Fernglasvergrößerung vor. Deshalb wird `l` im
statischen Test als eigentlicher Verzerrungsparameter verwendet.

Die wichtigen Referenzpunkte sind:

* `l = 1`: gnomonische Abbildung und gerades kartesisches Gitter

* `l = 0,5`: stereografische Abbildung und Helmholtz-Endpunkt

* `l → 0`: äquidistanter Grenzfall

* `l > 1`: Fortsetzung in die tonnenförmige Richtung

Damit bleiben die Helmholtz- und Oomes-Fragestellung erhalten, ohne die
Verzerrung an eine Fernglasvergrößerung zu koppeln.

## Die radiale Abbildung im Shader

`r` ist der Radius im fertigen sichtbaren Kreis. Die Mitte hat `r = 0`, der
Blendenrand `r = 1`. `β` ist der halbe Winkeldurchmesser des sichtbaren Feldes.

Zuerst wird aus der ebenen Position der tatsächliche Sehwinkel bestimmt:

```text
ρ = atan(r · tan(β))
```

Danach berechnet der Shader, an welcher Stelle des geraden Ausgangsgitters er
abtasten muss:

```text
s_l(r) = tan(l · ρ) / tan(l · β)
```

Für `l → 0` wird der Grenzfall benutzt:

```text
s_0(r) = ρ / β
```

Die Division durch den Wert am Blendenrand sorgt dafür, dass für jedes `l`
weiterhin `s(1) = 1` gilt. `l` verändert also die Linienform, aber nicht den
eingestellten Winkeldurchmesser.

Der eingestellte `l`-Wert bleibt auch dann gleich, wenn im Inspector nur das FOV
geändert wird. Die sichtbare Krümmung kann bei einem größeren Sehfeld trotzdem
deutlicher wirken, weil die Abbildung über einen größeren Winkelbereich gezeigt
wird.

Sehr große Kombinationen aus FOV und `l` können einen Umkehrpunkt der
Tangensfunktion erreichen. Der Trialplan prüft deshalb vor dem Start, ob die
eingestellten Kombinationen noch monoton sind. Die üblichen Pilotwerte bei 70°
oder 90° liegen deutlich innerhalb des erlaubten Bereichs.

Die Versionskennung dieser Abbildung lautet:

```text
visual-space-l-tangent-normalized-cartesian-grid-v2
```

Sie wird in jeder Plan- und Trialdatei mitgeschrieben.

## Winkelabstand der Gitterlinien

Die Dichte des Checkerboards wird nicht mehr als beliebige Anzahl von Feldern
über den Durchmesser angegeben. Oomes et al. beschreiben einen Abstand von
10 Grad. Deshalb gibt es am `Checkerboard Stimulus` den Wert
`Grid Line Spacing Degrees`, der standardmäßig auf `10` steht.

Für die eigentliche Berechnung wird dieser Winkel einmal in die lineare
Gitterweite des normierten u/v-Koordinatensystems umgerechnet:

```text
grid_spacing_uv = tan(grid_spacing_deg) / tan(FOV / 2)
```

Bei 90 Grad FOV und 10 Grad Abstand ergibt das ungefähr `0,176327`. Danach wird
ein gleichmäßiges kartesisches Gitter mit Linien bei `u = n * 0,176327` und
`v = n * 0,176327` erzeugt. Die mittlere horizontale und vertikale Linie laufen
dabei durch das Fixationszeichen. Erst anschließend wird die radiale
`l`-Abbildung angewendet.

In der Trialdatei und in den Eye-Tracking-Markern werden sowohl
`grid_spacing_deg` als auch `grid_spacing_uv` gespeichert. Damit bleibt die
Einstellung in Grad nachvollziehbar, während die mathematische Darstellung in
linearen u/v-Koordinaten dokumentiert ist.

## Runde Öffnung und weicher Rand

Das Checkerboard wird intern weiterhin als quadratisches Muster berechnet. Eine
davon getrennte Kreisblende entscheidet erst danach, welcher Ausschnitt davon
sichtbar ist. Dadurch kann das FOV geändert werden, ohne gleichzeitig die
Verzerrungsformel umzudefinieren.

Am `Checkerboard Stimulus` und am `Random Dot Field` gibt es den Wert
`Aperture Edge Softness Degrees`:

* `0`: harter, klar abgeschnittener Rand

* kleiner positiver Wert: kurzer transparenter Übergang

* größerer Wert: breiterer weicher Verlauf nach innen

Die Angabe erfolgt in Winkelgrad und nicht in Pixeln. Dadurch bleibt die
Randbreite auch bei anderer Auflösung oder auf der XR-4 vergleichbar. 

Am `Checkerboard Stimulus` kann `Use Circular Aperture` für eine technische
Kontrolle ausgeschaltet werden. Dann sieht man das vollständige quadratische
Gitter. Für den eigentlichen Versuch bleibt der Haken eingeschaltet. Der
verwendete Zustand wird in der Trialdatei und in den Eye-Tracking-Markern
mitgespeichert.

## Verhältnis zur α-Skala von Oomes

Oomes et al. verwendeten eine Skala von `α = -0,8` bis `α = 2`. Dabei gilt:

* `α = 0`: gerades Gitter

* `α = 1`: Helmholtz-Muster

Die Veröffentlichung enthält aber keine Formel, mit der die Zwischenwerte
erzeugt wurden. Deshalb wird `α` nicht mehr als eigentlicher Inspectorparameter
verwendet.

Zur Orientierung wird in den CSV-Dateien zusätzlich berechnet:

```text
oomes_endpoint_equivalent = 2 · (1 - l)
```

Damit stimmen die gemeinsamen Endpunkte überein:

* `l = 1` entspricht `0`

* `l = 0,5` entspricht `1`

* `l = 0` entspricht `2`

* `l = 1,4` entspricht `-0,8`

Diese zusätzliche Zahl ist ausdrücklich keine Rekonstruktion der
Originalinterpolation von Oomes. Der primär ausgewertete und berichtete
Stimulusparameter bleibt `l`.

## Head-locked und ohne Nahdisparität

Das Checkerboard bleibt immer in der aktuellen HMD-Blickrichtung. Eine
Kopfbewegung verschiebt den Blick also nicht über das Muster. Das ist für diesen
statischen Test beabsichtigt.

Der Shader behandelt die Eckpunkte als Blickrichtungen und nicht als Punkte auf
einer nahen Unity-Fläche. Die Kameraposition geht dadurch nicht in die Projektion
ein. Linkes und rechtes Auge erhalten dieselben Winkelrichtungen und müssen nicht
auf eine künstliche Ebene in beispielsweise einem Meter Entfernung konvergieren.

Der Stimulus verhält sich damit wie ein Objekt in virtueller Unendlichkeit bzw.
ohne Nahdisparität. Die Akkommodationsentfernung bleibt trotzdem durch die Optik
der Varjo XR-4 vorgegeben und wird durch Unity nicht tatsächlich unendlich.

Das Random-Dot-Feld verwendet inzwischen dasselbe Richtungsprinzip. Sein
`Field Radius Meters` ist nur noch die technische Größe des erzeugten Meshes und
keine wahrgenommene Entfernung. Das zentrale Kreuz bleibt unverzerrt und
unbewegt, während ausschließlich die Punkte durch die simulierte Schwenkung
laufen.

## Ablauf eines Durchgangs

1. Auf neutralem Hintergrund erscheint zunächst nur das Fixationskreuz.
2. Erst nach stabiler Fixation wird das Checkerboard eingeblendet.
3. Die Versuchsperson antwortet konkav oder konvex.
4. Antwort, Reaktionszeit, `l`, FOV, Augenmodus und Fixationswerte werden sofort
   gespeichert.
5. Nach einer kurzen Pause folgt der nächste zufällig gemischte Durchgang.

Wenn die Fixation während der Darbietung zu lange verloren geht:

1. Das Muster wird ausgeblendet.
2. Die Präsentation wird als ungültig gespeichert und nicht ausgewertet.
3. Dieselbe Bedingung wird hinten an die Warteschlange gehängt.
4. Die übrige zufällige Reihenfolge bleibt erhalten.

Kurze Blickunterbrechungen und vollständig ungültige Blickdaten besitzen
getrennte Zeitgrenzen. Ein einzelner Lidschlag muss dadurch nicht automatisch den
ganzen Durchgang ungültig machen. Mit `Maximum Attempts Per Trial = 0` wird so
lange wiederholt, bis ein gültiger Versuch vorliegt. Ein positiver Wert setzt
stattdessen eine Obergrenze.

## Bedienung

* `F5`: Sitzung starten

* `F6`: Sitzung abbrechen

* Pfeil links: konkav

* Pfeil rechts: konvex

* `C`: Eye-Tracking-Kalibrierung der vorhandenen Toolbox

Beim Random-Dot-Test werden die Pfeiltasten erst angenommen, nachdem die
Bewegungsphase beendet und das Punktfeld ausgeblendet wurde.

Am `Checkerboard Keyboard Controller` kann `Swap Response Keys` aktiviert
werden. Dadurch lässt sich die Bedeutung der beiden Pfeiltasten zwischen
Versuchspersonen ausbalancieren. Der Experimenter Monitor zeigt automatisch die
gerade gültige Belegung an.

## Einstellungen im Inspector

Die wichtigsten Einstellungen befinden sich am Objekt
`Checkerboard Trial Session`:

* `Angular Diameters Degrees`: ein oder mehrere FOV-/Winkeldurchmesser

* `Eye Presentations`: Both Eyes, Left Eye Only oder Right Eye Only

* `Visual Space L Values`: alle zu präsentierenden Verzerrungswerte

* `Repetitions Per Condition`: Wiederholungen jeder Kombination

* `Require Fixation`: Vorfixation und Kontrolle während des Musters

* `Maximum Off Target Seconds`: erlaubte zusammenhängende Blickabweichung

* `Maximum Invalid Gaze Seconds`: erlaubte Dauer fehlender oder ungültiger Daten

* `Maximum Attempts Per Trial`: 0 für unbegrenzte Wiedervorlage

* `Random Seed`: macht die zufällige Reihenfolge reproduzierbar

Die aktuell eingetragenen `l`-Werte und drei Wiederholungen sind nur für einen
technischen Pilotlauf gedacht. Sie sind noch keine festgelegten Bedingungen der
Masterarbeit. Die Listen können im Inspector vollständig geändert werden.

Am Objekt `Checkerboard Stimulus` werden Aussehen, Felderzahl, Farben, Größe des
Fixationskreuzes und die weiche Blendenkante eingestellt. Während einer Sitzung
setzt die Trialsteuerung FOV, `l` und Augenmodus automatisch.

Für den Bewegungstest liegen die wichtigsten Einstellungen am Objekt
`Random Dot Trial Session`:

* `Stimulus K Values`: alle fest vorgegebenen k-Werte

* `Repetitions Per Condition`: Wiederholungen jeder Kombination

* `Motion Duration Seconds`: sichtbare Dauer der Bewegung

* `Sweep Amplitude Degrees`: Schwenkweite je Seite

* `Sweep Speed Degrees Per Second`: Winkelgeschwindigkeit

* `Motion Modes`: für den Haupttest `Simulated Yaw`

* `Eye Presentations`: beide, nur linkes oder nur rechtes Auge

* Fixations- und Wiederholungsgrenzen wie beim Checkerboard

Die Richtung des ersten Schwenks wird über die Wiederholungen möglichst gleich
auf links und rechts verteilt. Die Reihenfolge wird anschließend mit dem Seed
gemischt. Gleiche Wiederholungen verschiedener `k`-Stufen verwenden
vergleichbare Punkt-Seeds, damit nicht eine bestimmte Punktverteilung nur mit
einem einzigen `k` verbunden ist.

## Eye Tracking und Messdateien

Die Eye-Tracking-Toolbox aus dem Lab bleibt die Grundlage der Rohdaten. Ihre
vorhandenen Blickspalten mit Augenstatus, Pupillendurchmesser, Ursprung und
Blickrichtung werden weiterhin geschrieben. Im Toolbox-Code wurde für den
Checkerboard-Test nur der Stimulusmarker um `visual_space_l`, FOV, Augenmodus,
Winkelabstand der Gitterlinien und die Breite der Blendenkante ergänzt. Der
Random-Dot-Ablauf schreibt seine Trialmarker über dieselbe vorhandene
Nachrichtenfunktion.

Pro Sitzung entsteht automatisch ein eigener Ordner unter `measurements` direkt
im Unity-Projekt. Der Pfad wird aus dem aktuellen Projektordner bestimmt und
funktioniert deshalb auch dann, wenn das Projekt auf dem Labor-PC auf einem
anderen Laufwerk liegt. Nur wenn im Inspector ausdrücklich ein anderer
Ausgabeordner eingetragen ist, wird dieser verwendet. Darin liegen unter anderem:

* `*_plan.csv`: der vorher erzeugte und zufällig gemischte Plan

* `*_trials.csv`: jede tatsächliche Präsentation, auch ungültige Wiederholungen

* die Rohdaten-CSV-Dateien der Eye-Tracking-Toolbox

Der Ordner `measurements` wird von Git ignoriert, damit Messdaten nicht
versehentlich auf GitHub landen.

In `*_trials.csv` stehen unter anderem:

* ursprüngliche Plannummer und aktuelle Präsentationsnummer

* `visual_space_l`, FOV und Augenmodus

* `oomes_endpoint_equivalent` als zusätzliche Orientierung

* Antwort und Reaktionszeit

* `valid_for_analysis`

* aktueller Fixationswinkel und Anteil gültiger Samples

* längste zusammenhängende Off-Target- und Invalid-Gaze-Dauer

* Grund für einen Ausschluss

* Versionskennung der verwendeten Abbildung

Marker wie `TrialStart`, `TrialResponse`, `TrialInvalid` und
`TrialRepeatQueued` verbinden den Versuchsablauf zeitlich mit den Rohdaten.

Beim Random-Dot-Test werden zusätzlich unter anderem festgehalten:

* der vorgegebene Wert `stimulus_k`

* Schwenkrichtung, Amplitude und Geschwindigkeit

* Bewegungsdauer und Reaktionszeit nach dem Ausblenden

* Dot-Seed und Punktanzahl

* Breite der weichen Blendenkante

* Antwort konkav/konvex und `valid_for_analysis`

* Fixationswerte und Grund einer ungültigen Wiederholung

## Varjo- und Unity-Einstellungen

Getestet wird mit der Varjo XR-4. Im bisherigen Projekt funktionierte die
Darbietung mit:

* Varjo als XR Provider unter Windows/Standalone

* `Initialize XR on Startup`

* Stereo Rendering Mode `Multi Pass`

* `XR Origin` mit `Main Camera` und Tracked Pose Driver

Vor einem echten Durchlauf sollten in Varjo Base Tracking und Eye Tracking
geprüft und anschließend die Kalibrierung durchgeführt werden.

Die Szene liegt unter:

```text
Assets/GlobeEffect/Demo/CheckerboardDemo.unity
```

Falls sie neu aufgebaut werden soll:

```text
Tools → Globe Effect → Create or Reset Demo Scene
```

Die Random-Dot-Szene wird über folgenden Menüpunkt erstellt beziehungsweise
zurückgesetzt:

```text
Tools → Globe Effect → Create or Reset Random Dot Demo Scene
```

Für einen reinen visuellen Test am Laptop kann die Szene auch ohne Headset im
Play Mode geöffnet werden. Das Muster folgt dann der normalen `Main Camera`. Um
einen kompletten Tastaturdurchlauf ohne gültige Eye-Tracking-Daten zu testen,
muss am `Checkerboard Trial Session` vorübergehend `Require Fixation`
deaktiviert werden. Diese Einstellung ist nur für den technischen Test gedacht
und darf bei einer Messung nicht ausgeschaltet bleiben.

## Wichtige Dateien

```text
Assets/GlobeEffect/
├── Demo/
│   ├── CheckerboardDemo.unity
│   └── RandomDotMotionDemo.unity
├── Runtime/
│   ├── Scripts/
│   │   ├── VisualSpaceRadialMapping.cs
│   │   ├── VrCheckerboardStimulus.cs
│   │   └── CheckerboardKeyboardController.cs
│   ├── Resources/
│   │   ├── GlobeEffectHelmholtzCheckerboard.shader
│   │   └── GlobeEffectMerlitzRandomDots.shader
│   ├── Experiment/
│   │   ├── CheckerboardTrialPlanner.cs
│   │   ├── CheckerboardTrialQueue.cs
│   │   ├── CheckerboardTrialSessionController.cs
│   │   ├── CheckerboardExperimentFiles.cs
│   │   ├── RandomDotTrialPlanner.cs
│   │   ├── RandomDotTrialQueue.cs
│   │   └── RandomDotTrialSessionController.cs
│   ├── RandomDots/
│   │   ├── RandomDotFieldStimulus.cs
│   │   ├── RandomDotSimulatedSweep.cs
│   │   └── RandomDotKeyboardController.cs
│   └── EyeTracking/
│       └── CheckerboardFixationMonitor.cs
├── Editor/
│   ├── CheckerboardDemoSceneBuilder.cs
│   └── ExperimenterMonitorWindow.cs
└── Tests/EditMode/
    ├── VisualSpaceRadialMappingTests.cs
    ├── CheckerboardTrialPlannerTests.cs
    └── CheckerboardTrialQueueTests.cs
```

`VisualSpaceRadialMapping.cs` enthält die C#-Referenzrechnung. Der Shader führt
dieselbe Abbildung für jeden sichtbaren Pixel aus.

## Trennung der beiden Tests

Im Random-Dot-Teil spielen Vergrößerung, Instrumentenabbildung, Bewegung und der
Merlitz-Parameter `k` eine Rolle. Diese Fragen sollen nicht unbemerkt in den
statischen Checkerboard-Test hineinrutschen. Umgekehrt wird im Bewegungstest
kein zusätzliches angenommenes Visual-Space-`l` in das Bild eingerechnet: Die
Wahrnehmung stammt dort von der echten Versuchsperson.

Kurz gesagt:

* Statisches Checkerboard: Visual-Space-`l`, feste Reize,
  konkav/konvex und head-locked.

* Random-Dot-Teil: feste `k`-Reize, kontrollierte Bewegung,
  konkav/konvex und ein dynamischer PSE.

## Noch offen

* Die endgültigen `l`-Stufen und die Wiederholungszahl 

* Dasselbe gilt für `k`-Stufen, Schwenkweite, Geschwindigkeit und Dauer des
  Random-Dot-Tests. Die aktuellen Werte sind Pilotwerte.

* Für die Auswertung wird später eine psychometrische Funktion für
  `P(konvex | l)` angepasst; der 50-%-Punkt ist der gesuchte PSE.

* Falls die Originalimplementierung von Oomes noch verfügbar wird, kann ihre
  α-Skala nachträglich mit der hier verwendeten l-Familie verglichen werden.

## Literaturgrundlage

* Oomes, A. H. J., Koenderink, J. J., van Doorn, A. J. und de Ridder, H.
  (2009). *What are the uncurved lines in our visual field? A fresh look at
  Helmholtz's checkerboard.* Perception, 38, 1284–1294.

* Helmholtz, H. von: Beschreibung des Checkerboard- bzw.
  Richtungskreis-Phänomens in der physiologischen Optik.

* Merlitz, H. (2010). *Panning Distortion of Binoculars and Its Impact on the
  Globe Effect.* Journal of the Optical Society of America A, 27, 50–57.

Die aktuelle Umsetzung ist eine VR-Adaption und keine exakte Replikation des
Versuchsaufbaus von Oomes. Aufgabe, Eye Tracking, Headsetdarstellung und
mono-/binokulare Bedingungen wurden für die Masterarbeit erweitert.
