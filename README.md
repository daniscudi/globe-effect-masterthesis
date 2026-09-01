# Globe Effect – Masterarbeit

In diesem Unity-Projekt entsteht der Versuchsaufbau für meine Masterarbeit zum
Globe Effect. Der aktuelle Schwerpunkt ist ein statischer Checkerboard-Test in
VR, der sich an Helmholtz und Oomes et al. (2009) orientiert. Der spätere
Random-Dot- und Bewegungsteil liegt weiterhin im Projekt, ist aber vom
statischen Checkerboard getrennt.

## Was der Checkerboard-Test macht

Die Versuchsperson sieht ein kreisrundes Schachbrett mit einem roten
Fixationskreuz. Vor jedem Durchgang wird ein fester Verzerrungswert eingestellt.
Die Person verändert diesen Wert nicht selbst, sondern antwortet nur:

- Pfeil links: Das Muster wirkt konkav.
- Pfeil rechts: Das Muster wirkt konvex.

Aus mehreren Antworten pro Verzerrungsstufe kann später der Wert geschätzt
werden, bei dem beide Antworten gleich häufig vorkommen. Das ist der Punkt, an
dem das Muster subjektiv geradlinig erscheint.

Der Test kann beidäugig, nur links oder nur rechts gezeigt werden. Die
gewünschten Bedingungen werden am `Checkerboard Trial Session` im Inspector
eingestellt.

## Warum der statische Test jetzt l verwendet

Im ersten Prototyp wurde die Instrumentengleichung von Merlitz benutzt:

```text
tan(k · a) = m · tan(k · A)
```

Hier beschreibt `k` die Abbildung eines optischen Instruments und hängt mit der
Vergrößerung `m` zusammen. Das ist für den späteren Fernglas- und Bewegungsteil
wichtig. Beim statischen Checkerboard ohne Fernglas wäre diese Kopplung aber
unnötig und schwer zu erklären.

Merlitz führt zusätzlich den Parameter `l` für eine radiale Abbildung des
visuellen Raums ein:

```text
y_l(a) = tan(l · a) / l
```

In dieser Funktion kommt keine Fernglasvergrößerung vor. Deshalb wird `l` im
statischen Test als eigentlicher Verzerrungsparameter verwendet.

Die wichtigen Referenzpunkte sind:

- `l = 1`: gnomonische Abbildung und gerades kartesisches Gitter
- `l = 0,5`: stereografische Abbildung und Helmholtz-Endpunkt
- `l → 0`: äquidistanter Grenzfall
- `l > 1`: Fortsetzung in die tonnenförmige Richtung

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
visual-space-l-tangent-normalized-v1
```

Sie wird in jeder Plan- und Trialdatei mitgeschrieben.

## Verhältnis zur α-Skala von Oomes

Oomes et al. verwendeten eine Skala von `α = -0,8` bis `α = 2`. Dabei gilt:

- `α = 0`: gerades Gitter
- `α = 1`: Helmholtz-Muster

Die Veröffentlichung enthält aber keine Formel, mit der die Zwischenwerte
erzeugt wurden. Deshalb wird `α` nicht mehr als eigentlicher Inspectorparameter
verwendet.

Zur Orientierung wird in den CSV-Dateien zusätzlich berechnet:

```text
oomes_endpoint_equivalent = 2 · (1 - l)
```

Damit stimmen die gemeinsamen Endpunkte überein:

- `l = 1` entspricht `0`
- `l = 0,5` entspricht `1`
- `l = 0` entspricht `2`
- `l = 1,4` entspricht `-0,8`

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

- `F5`: Sitzung starten
- `F6`: Sitzung abbrechen
- Pfeil links: konkav
- Pfeil rechts: konvex
- `C`: Eye-Tracking-Kalibrierung der vorhandenen Toolbox

Am `Checkerboard Keyboard Controller` kann `Swap Response Keys` aktiviert
werden. Dadurch lässt sich die Bedeutung der beiden Pfeiltasten zwischen
Versuchspersonen ausbalancieren. Der Experimenter Monitor zeigt automatisch die
gerade gültige Belegung an.

## Einstellungen im Inspector

Die wichtigsten Einstellungen befinden sich am Objekt
`Checkerboard Trial Session`:

- `Angular Diameters Degrees`: ein oder mehrere FOV-/Winkeldurchmesser
- `Eye Presentations`: Both Eyes, Left Eye Only oder Right Eye Only
- `Visual Space L Values`: alle zu präsentierenden Verzerrungswerte
- `Repetitions Per Condition`: Wiederholungen jeder Kombination
- `Require Fixation`: Vorfixation und Kontrolle während des Musters
- `Maximum Off Target Seconds`: erlaubte zusammenhängende Blickabweichung
- `Maximum Invalid Gaze Seconds`: erlaubte Dauer fehlender oder ungültiger Daten
- `Maximum Attempts Per Trial`: 0 für unbegrenzte Wiedervorlage
- `Random Seed`: macht die zufällige Reihenfolge reproduzierbar

Die aktuell eingetragenen `l`-Werte und drei Wiederholungen sind nur für einen
technischen Pilotlauf gedacht. Sie sind noch keine festgelegten Bedingungen der
Masterarbeit. Die Listen können im Inspector vollständig geändert werden, ohne
den Code anzupassen.

Am Objekt `Checkerboard Stimulus` werden Aussehen, Felderzahl, Farben und Größe
des Fixationskreuzes eingestellt. Während einer Sitzung setzt die Trialsteuerung
FOV, `l` und Augenmodus automatisch.

## Eye Tracking und Messdateien

Die Eye-Tracking-Toolbox aus dem Lab bleibt die Grundlage der Rohdaten. Ihre
vorhandenen Blickspalten mit Augenstatus, Pupillendurchmesser, Ursprung und
Blickrichtung werden weiterhin geschrieben. Im Toolbox-Code wurde für diesen
Test nur der Stimulusmarker auf `visual_space_l`, FOV und Augenmodus angepasst.

Pro Sitzung entsteht ein eigener Ordner unter
`Application.persistentDataPath/Measurements`, sofern im Inspector kein anderer
Ausgabeordner gesetzt wurde. Darin liegen unter anderem:

- `*_plan.csv`: der vorher erzeugte und zufällig gemischte Plan
- `*_trials.csv`: jede tatsächliche Präsentation, auch ungültige Wiederholungen
- die Rohdaten-CSV-Dateien der Eye-Tracking-Toolbox

In `*_trials.csv` stehen unter anderem:

- ursprüngliche Plannummer und aktuelle Präsentationsnummer
- `visual_space_l`, FOV und Augenmodus
- `oomes_endpoint_equivalent` als zusätzliche Orientierung
- Antwort und Reaktionszeit
- `valid_for_analysis`
- aktueller Fixationswinkel und Anteil gültiger Samples
- längste zusammenhängende Off-Target- und Invalid-Gaze-Dauer
- Grund für einen Ausschluss
- Versionskennung der verwendeten Abbildung

Marker wie `TrialStart`, `TrialResponse`, `TrialInvalid` und
`TrialRepeatQueued` verbinden den Versuchsablauf zeitlich mit den Rohdaten.

## Varjo- und Unity-Einstellungen

Getestet wird mit der Varjo XR-4. Im bisherigen Projekt funktionierte die
Darbietung mit:

- Varjo als XR Provider unter Windows/Standalone
- `Initialize XR on Startup`
- Stereo Rendering Mode `Multi Pass`
- `XR Origin` mit `Main Camera` und Tracked Pose Driver

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
│   └── CheckerboardDemo.unity
├── Runtime/
│   ├── Scripts/
│   │   ├── VisualSpaceRadialMapping.cs
│   │   ├── VrCheckerboardStimulus.cs
│   │   └── CheckerboardKeyboardController.cs
│   ├── Resources/
│   │   └── GlobeEffectHelmholtzCheckerboard.shader
│   ├── Experiment/
│   │   ├── CheckerboardTrialPlanner.cs
│   │   ├── CheckerboardTrialQueue.cs
│   │   ├── CheckerboardTrialSessionController.cs
│   │   └── CheckerboardExperimentFiles.cs
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

## Abgrenzung zum Random-Dot- und Bewegungsteil

Die Dateien `MerlitzCheckerboardMath.cs`, `GlobeEffectCoordinateMapping2D.cs`
und die Random-Dot-Szene bleiben im Projekt. Dort spielen Vergrößerung,
Instrumentenabbildung, Bewegung und der Merlitz-Parameter `k` weiterhin eine
Rolle. Diese Fragen sollen nicht unbemerkt in den statischen Checkerboard-Test
hineinrutschen.

Kurz gesagt:

- Statisches Checkerboard: Visual-Space-`l`, feste Reize,
  konkav/konvex und head-locked.
- Bewegungs- und Random-Dot-Teil: Instrumentenmodell mit `k`, Vergrößerung und
  mögliche Globe-Effect-Fragestellungen.

## Noch offen

- Die endgültigen `l`-Stufen und die Wiederholungszahl werden mit dem Betreuer
  festgelegt.
- Es muss entschieden werden, welche Augenbedingungen Teil des Hauptversuchs
  werden.
- Shader, Monokularmaskierung und Darstellung ohne Nahdisparität müssen auf der
  XR-4 visuell gegengeprüft werden.
- Für die Auswertung wird später eine psychometrische Funktion für
  `P(konvex | l)` angepasst; der 50-%-Punkt ist der gesuchte PSE.
- Falls die Originalimplementierung von Oomes noch verfügbar wird, kann ihre
  α-Skala nachträglich mit der hier verwendeten l-Familie verglichen werden.

## Literaturgrundlage

- Oomes, A. H. J., Koenderink, J. J., van Doorn, A. J. und de Ridder, H.
  (2009). *What are the uncurved lines in our visual field? A fresh look at
  Helmholtz's checkerboard.* Perception, 38, 1284–1294.
- Helmholtz, H. von: Beschreibung des Checkerboard- bzw.
  Richtungskreis-Phänomens in der physiologischen Optik.
- Merlitz, H. (2010). *Panning Distortion of Binoculars and Its Impact on the
  Globe Effect.* Journal of the Optical Society of America A, 27, 50–57.

Die aktuelle Umsetzung ist eine VR-Adaption und keine exakte Replikation des
Versuchsaufbaus von Oomes. Aufgabe, Eye Tracking, Headsetdarstellung und
mono-/binokulare Bedingungen wurden für die Masterarbeit erweitert.
