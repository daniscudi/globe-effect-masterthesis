# Globe Effect

In diesem Unity-Projekt entsteht der Versuchsaufbau für meine Masterarbeit zum
Globe Effect. Der statische Checkerboard-Test orientiert sich an Helmholtz und
Oomes et al. (2009). Daneben gibt es einen getrennten Random-Dot-Test für die
Wahrnehmung während einer simulierten Schwenkbewegung.

Eine kurze Bedienungs- und Arbeitsübersicht steht in `AKTUELLER_STAND.md`.

## Was der Checkerboard-Test macht

Die Versuchsperson fixiert zuerst das rote Kreuz und sieht danach kurz ein
kreisrundes Schachbrett mit einem festen Verzerrungswert. Anschließend erscheint
eine Schwarz-Weiß-Noise-Maske. Sie bleibt zusammen mit dem Fixationskreuz bis
zur Antwort sichtbar. Im Hauptversuch wird kein zusätzlicher Antworttext mehr
eingeblendet, damit er nicht vom Reiz oder von der Fixation ablenkt.

Im Training wird die feste Tastenbelegung vorher erklärt:

* `UP = CATEGORY A`

* `DOWN = CATEGORY B`

Category A wird dort als nach außen gewölbt und Category B als nach innen
gewölbt beschrieben. Dazu werden zuerst zwei deutliche Beispiele gezeigt.
Anschließend folgen zufällig gemischte Übungstrials mit mehreren mittleren
`l`-Werten. Intern bleiben die Antworten als `Convex` und `Concave` gespeichert,
damit die bestehende Auswertung weiter funktioniert.

Aus mehreren Antworten pro Verzerrungsstufe kann später der Wert geschätzt
werden, bei dem beide Antworten gleich häufig vorkommen. Das ist der Punkt, an
dem das Muster subjektiv geradlinig erscheint (PSE).

Der Test kann beidäugig, nur links oder nur rechts gezeigt werden. Die
gewünschten Bedingungen werden am `Checkerboard Experiment Manager` im Inspector
eingestellt.

## Was der Random-Dot-Test macht

Die Versuchsperson fixiert ein rotes Kreuz in der Mitte einer runden Öffnung.
Schwarze und weiße Punkte bewegen sich dahinter automatisch von links nach
rechts und wieder zurück. Die Öffnung und das Fixationskreuz bleiben dabei
kopffest. Eine tatsächliche Kopfbewegung ist für die Hauptbedingung nicht nötig.

Die aktuelle Instrumentenfassung setzt vor jedem Durchgang eine feste
Fernglasvergrößerung `m` und eine feste Instrumentenverzeichnung `k`. Beide Werte
werden nicht angezeigt und können von der Versuchsperson nicht verändert werden.
Nach der festgelegten Bewegungsdauer verschwindet das Punktfeld und es folgt
wieder nur die Entscheidung:

* Pfeil links: Bewegung wirkt konkav.

* Pfeil rechts: Bewegung wirkt konvex.

Aus `P(konvex | k, m)` kann später für jede Vergrößerung der Übergang bestimmt
werden, an dem konkav und konvex gleich häufig geantwortet werden. Dieser
dynamische Neutralpunkt kann mit dem im statischen Checkerboard geschätzten
persönlichen `l` verglichen werden.

## l und k

Der Random-Dot-Test benutzt die Instrumentengleichung von Merlitz:

```text
tan(k · a) = m · tan(k · A)
```

Hier beschreibt `k` die Form der Abbildung eines optischen Instruments. `m` ist
daneben ein eigener, unabhängiger Parameter. Beide stehen in derselben
Gleichung, aber `k` wird nicht aus `m` berechnet und verändert sich nicht, wenn
nur die Vergrößerung geändert wird. Die sichtbare Wirkung eines festen `k` kann
sich mit `m` trotzdem ändern. Beim statischen Checkerboard ohne simuliertes
Fernglas sind `m` und diese Instrumentengleichung nicht aktiv.

Merlitz führt zusätzlich den Parameter `l` für eine radiale Abbildung des
visuellen Raums ein:

```text
y_l(a) = tan(l · a) / l
```

In dieser Funktion kommt keine Fernglasvergrößerung vor. Im statischen Test
werden verschiedene Kandidatenwerte gezeigt; der subjektiv gerade Kandidat
schätzt das persönliche `l`.

`Content Zoom` bleibt eine zusätzliche Skalierung nach der
Instrumentenabbildung. Im Random-Dot-Hauptversuch bleibt er auf `1`. Die echte
paraxiale Fernglasvergrößerung wird getrennt als `m` geführt und zusammen mit
`k` direkt im Shader ausgewertet.

Die wichtigen Referenzpunkte sind:

* `l = 1`: gnomonische Abbildung und gerades kartesisches Gitter

* `l = 0,5`: stereografische Abbildung und Helmholtz-Endpunkt

* `l → 0`: äquidistanter Grenzfall

* `l > 1`: Fortsetzung in die tonnenförmige Richtung

Beim Random-Dot-Test heißen dieselben dargestellten Kandidatenwerte `k`, weil sie
die Verzeichnung des simulierten Instruments steuern. Der neutrale `k`-Wert ist
die dynamische Schätzung, die mit dem statischen `l` verglichen wird.

Im Random-Dot-Shader wird aus dem wirklichen radialen Punktwinkel `A` direkt der
scheinbare Fernglaswinkel berechnet:

```text
a = atan(m · tan(k · A)) / k
```

Für `k → 0` wird der Grenzfall `a = m · A` verwendet. Erst danach wird der
optionale `Content Zoom` angewendet. Bei `k = 1` und `Content Zoom = 1` entspricht
die Abbildung genau der klassischen Tangentenbedingung eines Fernglases.

## Die radiale Abbildung im Checkerboard-Shader

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
2. Nach stabiler Fixation wird das Checkerboard für die eingestellte Zeit
   eingeblendet. Der Startwert beträgt 600 ms.
3. Direkt danach erscheint eine statische Schwarz-Weiß-Noise-Maske mit
   Fixationskreuz. Sie bleibt bis zur Antwort sichtbar.
4. Die im Training gelernte Antwort wird ohne erneuten Hinweistext gegeben. Die
   voreingestellte maximale Antwortzeit beträgt 5 Sekunden.
5. Antwort, Reaktionszeit, `l`, FOV, Augenmodus und Fixationswerte werden sofort
   gespeichert.
6. Nach der Antwort erscheint für 500 ms eine neue Noise-Verteilung. Danach
   beginnt direkt die graue Fixationsphase des nächsten Durchgangs. Ein schwarzer
   Zwischenbildschirm wird vermieden.

## Welcome Screen und Training

Nach dem Start des Play Modes erscheint zunächst ein englischer Welcome Screen:

* `T`: Training starten

* `F5`: Hauptversuch starten

* `F6`: laufenden Versuch oder laufendes Training abbrechen

Das Training zeigt zunächst je ein deutliches Beispiel für Category A und B.
Danach folgen standardmäßig die vier Werte `0,2`, `0,4`, `0,8` und `1,2`, jeweils
dreimal und in zufälliger Reihenfolge. Es gibt bewusst keine
Richtig-/Falsch-Rückmeldung. Nach dem Training geht es zurück zum Welcome Screen.
Solange `Require Training Before Session` aktiv ist, startet `F5` erst nach einem
vollständig durchlaufenen Training. Trainingsdurchgänge werden nicht in den
Messdateien gespeichert.

Wenn die Fixation während der Darbietung zu lange verloren geht:

1. Das Muster wird ausgeblendet.
2. Die Präsentation wird als ungültig gespeichert und nicht ausgewertet.
3. Dieselbe Bedingung wird ans Ende des aktuellen Unterblocks gehängt.
4. Die übrige zufällige Reihenfolge bleibt erhalten.

Kurze Blickunterbrechungen und vollständig ungültige Blickdaten besitzen
getrennte Zeitgrenzen. Ein einzelner Lidschlag muss dadurch nicht automatisch den
ganzen Durchgang ungültig machen. Mit `Maximum Attempts Per Trial = 0` wird so
lange wiederholt, bis ein gültiger Versuch vorliegt. Ein positiver Wert setzt
stattdessen eine Obergrenze.

## Bedienung

* `F5`: Sitzung starten beziehungsweise nach einer Blockpause fortsetzen

* `F6`: Sitzung abbrechen

* Pfeil hoch: Category A

* Pfeil runter: Category B

* `C`: Eye-Tracking-Kalibrierung der vorhandenen Toolbox

Beim Random-Dot-Test werden die Pfeiltasten erst angenommen, nachdem die
Bewegungsphase beendet und das Punktfeld ausgeblendet wurde.

Am `Checkerboard Keyboard Controller` kann `Swap Response Keys` aktiviert
werden. Dadurch lässt sich die Zuordnung zwischen Versuchspersonen
ausbalancieren. Innerhalb einer Person bleibt sie für Training und Hauptversuch
fest, damit nicht bei jedem Trial eine neue Belegung gelesen werden muss. Der
Experimenter Monitor und alle Antwortanzeigen zeigen automatisch die gerade
gültige Belegung. Die verwendete Zuordnung steht zusätzlich im Trialplan und in
den Eye-Tracking-Markern.

## Einstellungen im Inspector

Die wichtigsten Einstellungen befinden sich am Objekt
`Checkerboard Experiment Manager`:

* `Angular Diameters Degrees`: ein oder mehrere FOV-/Winkeldurchmesser

* `Eye Presentations`: Both Eyes, Left Eye Only oder Right Eye Only

* `Visual Space L Values`: alle zu präsentierenden Verzerrungswerte

* `Content Zoom Values`: unabhängiger Zoom; `1` lässt die Größe unverändert

* `Repetitions Per Condition`: Wiederholungen jeder Kombination

* `Require Fixation`: Vorfixation und Kontrolle während des Musters

* `Maximum Off Target Seconds`: erlaubte zusammenhängende Blickabweichung

* `Maximum Invalid Gaze Seconds`: erlaubte Dauer fehlender oder ungültiger Daten

* `Maximum Attempts Per Trial`: 0 für unbegrenzte Wiedervorlage

* `Stimulus Duration Seconds`: Dauer des Checkerboards; voreingestellt auf 0,6 s

* `Post Response Noise Seconds`: Dauer der neuen Noise-Verteilung nach der
  Antwort; voreingestellt auf 0,5 s

* `Response Timeout Seconds`: maximale Antwortzeit; 0 bedeutet ohne Zeitlimit

* `Convex Response Key` und `Concave Response Key`: Tasten der beiden Antworten

* `Require Training Before Session`: verlangt ein abgeschlossenes Training vor F5

* `Training Category A/B Visual Space L`: deutliche, noch zu pilotierende Beispiele

* `Training Visual Space L Values`: einstellbare `l`-Werte der Übungstrials

* `Training Repetitions Per Value`: Wiederholungen jedes Übungswertes

* `Random Seed`: macht die zufällige Reihenfolge reproduzierbar

Die aktuell eingetragenen `l`-Werte und drei Wiederholungen sind nur für einen
technischen Pilotlauf gedacht. Sie sind noch keine festgelegten Bedingungen der
Masterarbeit. Die Listen können im Inspector vollständig geändert werden.

Am Objekt `Checkerboard Stimulus` werden Aussehen, Felderzahl, Farben, Größe des
Fixationskreuzes und die weiche Blendenkante eingestellt. Während einer Sitzung
setzt der Experiment Manager FOV, `l`, Content Zoom und Augenmodus automatisch.

Für den Bewegungstest liegen die wichtigsten Einstellungen am Objekt
`Random Dot Experiment Manager`:

* `Instrument Distortion K Values`: die zu präsentierenden
  Instrumentenverzeichnungen; geometrisch dieselbe Kandidatenfamilie wie beim
  Checkerboard

* `Instrument Magnification M Values`: eine oder mehrere
  Fernglasvergrößerungen; voreingestellt ist `10`

* `Content Zoom Values`: optionaler Nach-Zoom; für die Instrumentensimulation
  normalerweise `1`

* `Repeats Per Condition`: Wiederholungen jeder Kombination

* `Repeats Per Block`: Wiederholungen jeder Bedingung pro Unterblock;
  mit `25` insgesamt und `5` pro Unterblock entstehen fünf Abschnitte je
  Bewegungsart

* `Motion Seconds`: sichtbare Dauer der Bewegung; der Pilotwert beträgt
  `5 s`

* `Sweep Amplitude Degrees`: Zielschwenkweite je Seite; für `10x` zunächst `2°`

* `Sweep Speed`: reale Instrument-/Kopfgeschwindigkeit;
  für `10x` zunächst `1,2°/s`, entsprechend ungefähr `12°/s` in der Bildmitte

* `Motion Modes`: Basisreihenfolge der getrennten Bewegungsblöcke

* `Counterbalance Motion Block Order By Participant Id`: kehrt diese Reihenfolge
  automatisch für jede zweite fortlaufende Versuchsperson um (`pilot_001` Basis,
  `pilot_002` umgekehrt)

* `Head Tracked Coverage Yaw Degrees`: unsichtbarer Sicherheitspuffer der
  Punktwelt; voreingestellt auf `15°` je Seite

* `Validate Head Tracked Motion`: wiederholt aktive Durchgänge, wenn der
  Seitenwechsel fehlt oder Auslenkung beziehungsweise Geschwindigkeit außerhalb
  der eingestellten Grenzen liegen

* `Eye Presentations`: beide, nur linkes oder nur rechtes Auge

* Fixations- und Wiederholungsgrenzen wie beim Checkerboard

Die beiden Bewegungsarten werden nicht trialweise vermischt, sondern als
getrennte Blöcke in der Reihenfolge der Liste ausgeführt. Nach jedem Unterblock
und zwischen den Bewegungsblöcken hält die Sitzung an; `F5` setzt sie fort. Nur
innerhalb eines Unterblocks wird mit dem Seed gemischt. Die Richtung des ersten
Schwenks wird über die Wiederholungen möglichst gleich auf links und rechts
verteilt. Gleiche Wiederholungen verschiedener `k`-Stufen und beider
Bewegungsarten verwenden vergleichbare Punkt-Seeds, damit die Punktverteilung
nicht mit einer Bedingung verwechselt wird.

Nach einer Bewegungsphase verschwinden nur die Punkte. Der neutrale graue Kreis
und das Fixationskreuz bleiben während der Antwort, zwischen den Trials und in
den Blockpausen sichtbar. Dadurch entsteht kein Wechsel vom helleren Punktfeld
auf einen vollständig schwarzen Bildschirm. Außerhalb der Kreisöffnung bleibt
der Hintergrund in allen Phasen unverändert schwarz.

Das sichtbare Feld hat innen einen neutralgrauen Hintergrund für symmetrischen
Kontrast der schwarzen und weißen Punkte. Außerhalb der kreisförmigen Feldblende
ist der Hintergrund nahezu schwarz. Für Head-Tracked-Trials wird die erzeugte
Punktwelt automatisch bis zum Sicherheitspuffer erweitert. Die Punktzahl wächst
dabei proportional zur Kugelkappenfläche, damit die sichtbare Punktdichte nicht
vom Bewegungsbereich abhängt. Für die aktuellen `70°`, `10x` und `±15°`
Sicherheitsabdeckung sind rund `40,6°` Quellenfeld und `24.500` Punkte
voreingestellt.

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

* Beginn und Ende des Musters, Beginn der Antwortanzeige sowie die tatsächlich
  gemessenen Stimulus-, Noise- und Reaktionszeiten

* Antwort

* `valid_for_analysis`

* aktueller Fixationswinkel und Anteil gültiger Samples

* längste zusammenhängende Off-Target- und Invalid-Gaze-Dauer

* Grund für einen Ausschluss

* Versionskennung der verwendeten Abbildung

Marker wie `TrialStart`, `StimulusEnded`, `NoiseMaskStarted`,
`ResponsePromptShown`, `TrialResponse`, `TrialInvalid` und `TrialRepeatQueued`
verbinden den Versuchsablauf zeitlich mit den Rohdaten.

Beim Random-Dot-Test werden zusätzlich unter anderem festgehalten:

* die vorgegebenen Werte `instrument_distortion_k`,
  `instrument_magnification_m` und `content_zoom`

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

Die zweite Szene liegt unter:

```text
Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity
```

Beide Szenen sind als normale Unity-Szenen gespeichert. Die früheren einmaligen
Builder-Skripte wurden nach dem Aufbau entfernt, damit die Projektstruktur
übersichtlicher bleibt.

Für einen reinen visuellen Test am Laptop kann die Szene auch ohne Headset im
Play Mode geöffnet werden. Das Muster folgt dann der normalen `Main Camera`. Um
einen kompletten Tastaturdurchlauf ohne gültige Eye-Tracking-Daten zu testen,
muss am `Checkerboard Experiment Manager` vorübergehend `Require Fixation`
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
│   │   └── GlobeEffectVisualSpaceRandomDots.shader
│   ├── Experiment/
│   │   ├── CheckerboardTrialPlanner.cs
│   │   ├── CheckerboardExperimentManager.cs
│   │   ├── CheckerboardExperimentFiles.cs
│   │   ├── RandomDotTrialPlanner.cs
│   │   └── RandomDotExperimentManager.cs
│   ├── RandomDots/
│   │   ├── RandomDotFieldStimulus.cs
│   │   ├── RandomDotSimulatedSweep.cs
│   │   └── RandomDotKeyboardController.cs
│   └── EyeTracking/
│       └── CheckerboardFixationMonitor.cs
├── Editor/
│   └── ExperimenterMonitorWindow.cs
└── Tests/EditMode/
    ├── VisualSpaceRadialMappingTests.cs
    ├── CheckerboardTrialPlannerTests.cs
    └── CheckerboardTrialQueueTests.cs
```

`VisualSpaceRadialMapping.cs` enthält die C#-Referenzrechnung. Der Shader führt
dieselbe Abbildung für jeden sichtbaren Pixel aus.

## Trennung der beiden Tests

Der statische Test verwendet bewusst keine Fernglassimulation. Beim Random-Dot-
Teil ist die Merlitz-Instrumentenabbildung aktiv. Die Vergrößerung `m` und die
Instrumentenverzeichnung `k` sind getrennt einstellbare Trialfaktoren. Der
unabhängige Content Zoom bleibt für technische Vergleiche vorhanden und steht im
Hauptversuch auf `1`.

Kurz gesagt:

* Statisches Checkerboard: Visual-Space-`l`, feste Reize,
  konkav/konvex und head-locked.

* Random-Dot-Instrumententest: feste Kombinationen aus `k` und `m`, kontrollierte
  oder kopfgesteuerte Bewegung und konkav/konvex.

## Noch offen

* Die endgültigen `l`-/`k`-Stufen und die Wiederholungszahl

* Vergrößerungen `m` sowie die voreingestellten Pilotwerte von `2°`, `1,2°/s`
  und `5 s` sind weiterhin empirisch zu pilotieren.

* Instruktion und Training für den aktiven Head-Tracked-Schwenk werden in einem
  separaten Schritt ergänzt.

* Für die Auswertung wird später eine psychometrische Funktion für
  `P(konvex | k, m)` angepasst; der 50-%-Punkt in `k` ist der gesuchte
  dynamische PSE.

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
