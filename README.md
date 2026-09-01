# Globe Effect Master Thesis

Dieses Unity-Projekt ist der aktuelle Arbeitsstand für die Untersuchung des
Globe Effects in VR. Im Moment gibt es zwei Testszenen: ein kreisrundes
Checkerboard und ein Random-Dot-Punktfeld für Kopfbewegungen. Die Mathematik,
die Darstellung, der Versuchsablauf und das Eye Tracking liegen in getrennten
Skripten, damit einzelne Teile später leichter geändert werden können.

## Was aktuell funktioniert

- Das Checkerboard ist kreisrund. Winkeldurchmesser, Abstand, `k`,
  Vergrößerung `m` und die Darstellung für beide, nur das linke oder nur das
  rechte Auge können im Inspector eingestellt werden.
- Wenn der Abstand verdoppelt wird, verdoppelt sich auch der physische
  Durchmesser. Dadurch bleibt das Checkerboard aus Sicht der Versuchsperson
  gleich groß.
- Mit `R` kann der Stimulus in der aktuellen Blickrichtung neu platziert
  werden. `k` lässt sich während Play mit den Pfeiltasten verändern.
- Die Varjo XR-4 läuft mit dem Varjo Unity XR Plugin `3.7.3`. Auf dem bisher
  verwendeten Laborrechner funktioniert die Darstellung im Modus `Multi Pass`.
- Eye Tracking, Fixationskontrolle und CSV-Aufzeichnung sind eingebaut. Das
  Fenster `Experiment Monitor` zeigt `ON TARGET`, `OFF TARGET` oder
  `NO VALID GAZE` nur auf dem Kontrollmonitor.
- Der Sitzungscontroller kombiniert alle im Inspector eingetragenen
  Bedingungen, mischt die fertigen Trials und speichert Plan, Antworten,
  Gaze-Daten und Kopfbewegung getrennt ab.
- Die zweite Szene enthält ein fest im Raum stehendes Schwarz-Weiß-Punktfeld.
  Hier kann `k` während einer echten Links-Rechts-Kopfbewegung eingestellt
  werden. Für Tests ohne HMD gibt es zusätzlich einen simulierten Schwenk.
- Die Gleichungen für Merlitz, Winkelgröße und die `(x,y)`-/`(u,v)`-Plots liegen
  in eigenen Mathematikdateien und werden durch EditMode-Tests geprüft.

## Schnellstart

1. Das Projekt mit Unity 6.5 öffnen. Zielversion ist
   konkret Editor `6000.5.6f1`.
2. `Assets/GlobeEffect/Demo/CheckerboardDemo.unity` öffnen. Die Szene enthält
   bereits XR Origin, Main Camera und Checkerboard-Stimulus.
3. Unter `Edit > Project Settings > XR Plug-in Management` kontrollieren, dass
   im Standalone-Reiter `Initialize XR on Startup` und `Varjo` aktiv sind. Diese
   Zuordnung ist im Repository bereits gespeichert. Für den Laborbetrieb nicht
   gleichzeitig den generischen OpenXR-Loader aktivieren. Unter `Varjo` ist
   `Stereo Rendering Mode = Multi Pass` gespeichert, da nur dieser Modus auf
   dem verwendeten XR-4-Rechner ein Bild in Game View und Headset lieferte.
4. Für einen Einzeltest am Objekt `Checkerboard Stimulus` die Parameter
   `Angular Diameter Degrees`, `Viewing Distance Meters`, `Merlitz K` und
   `Eye Presentation` einstellen.
5. Für einen weltfesten Trial `Follow Observer Every Frame` deaktiviert lassen.
   Im Play Mode mit `R` in der aktuellen Blickrichtung platzieren.
6. Vor Eye-Tracking-Tests in Varjo Base `Allow eye tracking` aktivieren und mit
   `C` die Blickkalibrierung starten. Die XR-4 sollte nach jedem erneuten
   Aufsetzen für die betreffende Person neu kalibriert werden.
7. Für einen kompletten Testdurchlauf am Objekt `Checkerboard Trial Session`
   einen Teilnehmercode ohne Klarnamen, das `Session Label` und die gewünschten
   Bedingungslisten kontrollieren. Danach im Play Mode mit `F5` starten.

Die Checkerboard-Szene kann bei Bedarf über
`Tools > Globe Effect > Create or Reset Demo Scene` neu aufgebaut werden.
Dabei werden die Startwerte 70 Grad, 1 Meter, `k = 0.7` und `m = 10` gesetzt.

Unity-Einheit ist hier gleich ein Meter. Für exakte Skalierung sollte das
Stimulusobjekt kein übergeordnetes Objekt mit unterschiedlich skalierten Achsen
besitzen.

## Wie die Unity-Szene aufgebaut wurde

Die Szenen sind normale `.unity`-Szenen und können ganz normal in der Hierarchy
angesehen und im Inspector verändert werden. Die benötigten Objekte wurden aber
nicht jedes Mal einzeln per Hand angelegt. Dafür gibt es zwei Editor-Skripte:

- `CheckerboardDemoSceneBuilder.cs` baut die Checkerboard-Szene auf;
- `RandomDotDemoSceneBuilder.cs` baut die Random-Dot-Szene auf.

Wenn eine Demoszene nach einem neuen Pull noch fehlt, legt das jeweilige Skript
sie beim ersten Import an. Dasselbe kann über das `Tools > Globe Effect`-Menü
von Hand ausgelöst werden. Danach liegt eine ganz normale gespeicherte Szene im
Projekt. Das Checkerboard wird ungefähr so aufgebaut:

```text
XR Origin
  Camera Offset
    Main Camera
Checkerboard Stimulus
Eye Tracking Toolbox
Checkerboard Trial Session
Environment
```

Der Scene Builder verbindet auch die Referenzen. Die Main Camera wird zum
Beispiel als Observer beim Checkerboard eingetragen, und die Trial Session
bekommt den Stimulus, die Tastatursteuerung, die Eye-Tracking-Toolbox und den
Fixationsmonitor zugewiesen. Das ist im Prinzip dasselbe, was man sonst per
Drag-and-drop im Inspector machen würde. Die eigentliche Merlitz-Mathematik
steckt nicht im Scene Builder, sondern in den weiter unten genannten
Mathematik- und Stimulusdateien.

## Vorschau und XR-Diagnose

Die normale Unity Game View zeigt das Checkerboard auch ohne angeschlossene
Brille als flache Vorschau. Kopfbewegung, Stereo-Augenauswahl und die reale
Winkelgröße lassen sich damit nicht prüfen. Für einen vollständigen
Test ohne angeschlossene XR-4 stellt Varjo Base einen Headset-Simulator bereit.

Bei einem schwarzen Bild zuerst die Game View vor und während des Play Mode
vergleichen:

1. Ist das Checkerboard vor Play sichtbar, funktionieren Mesh, Material,
   Grundshader und die normale Kamera grundsätzlich.
2. Unter `XR Plug-in Management` müssen `Initialize XR on Startup` und `Varjo`
   aktiv sein.
3. Auf dem verwendeten XR-4-Rechner muss aktuell `Stereo Rendering Mode =
   Multi Pass` gewählt sein; `Stereo` und `Two Pass` lieferten dort ein
   schwarzes Bild.

Beim Start einer XR-Sitzung wird die Kamera erst auf die reale HMD-Pose gesetzt,
wenn der Provider eine gültige Center-Eye-Pose meldet. Der Stimulus wartet auf
diesen Tracking-Status und bleibt danach standardmäßig weltfest.

## Bedienung im Play Mode

Die Komponente `Checkerboard Keyboard Controller` ist nur eine einfache
technische Bedienung. Vor dem Tastendruck muss die Game View den Eingabefokus
haben.

- `R`: Stimulus in der aktuellen HMD-Blickrichtung neu platzieren;
- Pfeiltaste links: `k` um `0.01` verringern;
- Pfeiltaste rechts: `k` um `0.01` erhöhen;
- `Shift` zusammen mit einer Pfeiltaste: Schrittweite `0.05`;
- `C`: Varjo-Blickkalibrierung starten;
- `F9`: technische Gaze-/Head-Aufzeichnung starten oder beenden.

Jede Änderung wird in der Console ausgegeben. `F9` dient nur zur technischen
Prüfung der Datenpipeline und sollte nicht während einer mit `F5` gestarteten
Sitzung benutzt werden.

## Pilot-Sitzung

Die Komponente `Checkerboard Trial Session` kombiniert alle Einträge aus
`Angular Diameters Degrees`, `Viewing Distances Meters`, `Eye Presentations`
und `Starting K Values` miteinander. Jede mögliche Kombination kommt pro
`Repetitions Per Condition` gleich oft vor. Beim Mischen bleibt ein Trial mit
allen seinen Werten zusammen. FOV, Abstand, Augenmodus und Startwert können
also nicht versehentlich getrennt werden. Mit demselben `Random Seed` und
denselben Listen entsteht wieder dieselbe Reihenfolge.

Die Demoszene enthält zum Ausprobieren sechs Trials: drei Augenmodi mal zwei
Startwerte für `k`. Diese Werte sind noch keine endgültigen Bedingungen für die
Studie. Weitere FOV- oder Abstandswerte können direkt im Inspector ergänzt
werden.

- `F5`: neue Sitzung starten;
- Pfeiltasten links/rechts: `k` einstellen;
- `Shift` plus Pfeiltaste: größere Schrittweite;
- `R`: bei Bedarf in der aktuellen Blickrichtung neu zentrieren;
- `Enter`: aktuellen `k`-Wert bestätigen und zum nächsten Trial wechseln;
- `F6`: laufende Sitzung kontrolliert abbrechen und den begonnenen Trial
  als abgebrochen speichern.

Standardmäßig wird der Fixationsstatus mitgespeichert, blockiert die Antwort
aber noch nicht. `Require Fixation Before Confirmation` kann im Inspector
aktiviert werden, sobald Toleranz, Mindestdauer und Umgang mit ungültigen
Samples im Versuchsprotokoll festgelegt sind.

Pro Sitzung entsteht unter `Application.persistentDataPath/Measurements` ein
eigener Ordner mit:

```text
..._plan.csv    randomisierte Reihenfolge und alle Sollbedingungen
..._trials.csv  Start-/Endwert von k, Antwortzeit, Recenter und Fixationsstatus
..._gaze.csv    rohe Varjo-Blicksamples und Ereignismarker
..._head.csv    Kamera-/Stimulus-Transformationen und Ereignismarker
```

Die Antwortdatei wird nach jedem Trial erweitert. Ein Abbruch verliert daher
nicht die bereits bestätigten Antworten.

## Dynamischer Random-Dot-k-Test

Die separate Szene
`Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity` ist für den Test mit
Kopfbewegung gedacht. Falls sie nach einem neuen Pull noch fehlt, wird sie beim
ersten Unity-Import automatisch erstellt. Sie kann außerdem über
`Tools > Globe Effect > Create or Reset Random Dot Demo Scene` neu aufgebaut
werden.

Das Punktfeld besteht standardmäßig aus 4000 schwarzen und weißen Punkten auf
einer Art gekrümmter Kuppel um die Versuchsperson. Es bleibt nach dem Start an
derselben Stelle im Raum und bewegt sich nicht von selbst. Im Modus
`HeadTracked` dreht die Versuchsperson den Kopf abwechselnd nach links und
rechts. Auf die dadurch sichtbare Bewegung wird dieselbe Merlitz-Abbildung wie
beim Checkerboard angewendet. Die Aufgabe lautet:

> Stelle `k` so ein, dass das Punktfeld während der Kopfbewegung möglichst
> stabil und nicht schwimmend erscheint.

Zum Ausprobieren gibt es zwei Trials mit `k = 0.3` beziehungsweise `k = 0.9`
als gemischte Startwerte. Standardmäßig müssen vor `Enter`
vier Wechsel zwischen linker und rechter Schwelle abgeschlossen sein. Die
Startschwelle von 2.5 Grad ist nur ein erster Testwert für `m = 10` und noch
keine festgelegte Studienbedingung. Die Punkte werden zunächst in einem
unverzerrten Winkelbereich von 20 Grad erzeugt. Durch `m = 10` wird dieser
Bereich im sichtbaren Bild stark vergrößert. Die 20 Grad beschreiben daher
nicht dasselbe wie der angezeigte Winkeldurchmesser von 70 Grad.

- `F5`: Random-Dot-Sitzung starten;
- Pfeiltasten links/rechts: `k` verändern;
- `Shift` plus Pfeiltaste: größere Schrittweite;
- `R`: Punktfeld an der aktuellen Kopfpose neu verankern und Sweep-Zähler
  zurücksetzen;
- `Enter`: finalen `k`-Wert bestätigen, sobald das Kopfkriterium erfüllt ist;
- `F6`: Sitzung kontrolliert abbrechen;
- `C`: Varjo-Eye-Tracking vor Sitzungsbeginn kalibrieren.

Für eine Vorschau ohne HMD kann im Inspector der Liste `Motion Modes` statt
`HeadTracked` der Wert `SimulatedYaw` zugewiesen werden. Dieser Modus simuliert
nur den visuellen Schwenk und ist nicht als Ersatz für die reale
Versuchsbedingung gedacht.

Plan-, Antwort-, Gaze- und Head-Dateien werden wie beim Checkerboard in einem
eigenen Sitzungsordner gespeichert. Die Trialdatei enthält zusätzlich
Punkt-Seed, Bewegungsmodus, Winkelschwelle, abgeschlossene Seitenwechsel,
tatsächlichen Gierwinkelbereich sowie den Fixationsstatus. Marker wie
`HeadHalfSweep`, `KAdjusted` und `TrialConfirmed` verbinden diese Ereignisse
mit Gaze- und Head-Samples.

## Mathematik

Die Merlitz-Gleichung sagt vereinfacht, an welchem sichtbaren Winkel `a` ein
Punkt erscheint, der ursprünglich am Winkel `A` liegt:

```text
tan(k a) = m tan(k A)
a(A) = atan(m tan(k A)) / k
```

`A` ist der Winkel im ursprünglichen Objekt, `a` der Winkel im dargestellten
Bild und `m` die Vergrößerung nahe der Bildmitte. `k` verändert, wie stark sich
diese Abbildung zum Rand hin krümmt. Wichtige Fälle sind:

- `k = 1`: Tangensbedingung; ein planes Gitter bleibt im Bildraum geradlinig;
- `k = 0.5`: Kreisbedingung nach Slevogt/Helmholtz;
- `k -> 0`: Winkelbedingung mit dem Grenzwert `a = m A`.

### Was mit der Rückwärtsrechnung im Shader gemeint ist

Der Shader geht für jeden sichtbaren Bildpunkt rückwärts vor. Er nimmt also
nicht ein Feld aus dem ursprünglichen Schachbrett und schiebt es nach außen.
Stattdessen fragt er für jeden Pixel im fertigen Kreis: **Von welcher Stelle im
ursprünglich geraden Schachbrett muss die Farbe kommen?** Diese Rückwärtsrechnung
verhindert Lücken oder überlappende Flächen im Muster.

Dabei bedeutet `r`, wie weit der aktuelle Pixel von der Kreismitte entfernt
ist: `r = 0` ist die Mitte und `r = 1` der Rand. `alpha` ist die Hälfte des
eingestellten Winkeldurchmessers. Die Rechnung lautet:

```text
a(r) = atan(r tan(alpha))
A(a) = atan(tan(k a) / m) / k
s(r) = tan(A(a(r))) / tan(A(alpha))
```

Das Ergebnis `s` sagt, wie weit die passende Stelle im ursprünglichen geraden
Schachbrett von dessen Mitte entfernt ist. Die Richtung bleibt gleich; nur der
Abstand zur Mitte wird umgerechnet. Am Rand wird die Rechnung so skaliert, dass
`r = 1` immer wieder am Rand des ursprünglichen Musters landet. Deshalb bleibt
der eingestellte Winkeldurchmesser bei jedem `k` gleich. Nur die Linien im
Inneren verändern ihre Form.

Das ist eine Entscheidung für unseren VR-Test. Merlitz hielt in seinem Paper
stattdessen ein wahres Feld von 7 Grad bei 10-facher Vergrößerung fest und kam
dadurch auf ein scheinbares Feld von ungefähr 70 Grad.

Die physische Größe der ebenen Kreisfläche ist

```text
D = 2 d tan(theta / 2)
```

mit Abstand `d` und Winkeldurchmesser `theta`. Deshalb verdoppelt sich `D`
exakt, wenn `d` bei gleichem `theta` verdoppelt wird.

## Was `k` hier bedeutet – und was nicht

Merlitz verwendet zwei verschiedene Parameter:

- `k` beschreibt die Verzeichnung des Instruments beziehungsweise des
  dargestellten Checkerboard-Stimulus;
- `l` beschreibt in seinem Modell eine zusätzliche radiale Abbildung des
  menschlichen visuellen Raums: `y = tan(l a) / l`.

Diese Implementierung rechnet `l` **nicht** in den Stimulus. Im
Checkerboard-Versuch stellt die Versuchsperson `k` so ein, dass das Gitter für
sie gerade erscheint. Erst dieser eingestellte Wert kann später mit Merlitz'
Modell verglichen werden. Würden wir `k` und `l` schon vorher im Code
gleichsetzen, würden wir das Ergebnis des Versuchs vorwegnehmen.

Oomes et al. ließen 20 stationäre, monokular beobachtende Personen die
Krummung eines Checkerboards zwischen Tonnen- und Kissenform einstellen. Sie
verglichen zentrale Fixation mit freiem Blick. Die meisten Personen stellten
eine Kissenverzeichnung als subjektiv gerade ein. Im Mittel war der Effekt etwa
halb so stark wie von Helmholtz behauptet, allerdings mit großen Unterschieden
zwischen den Personen. Oomes verwendete dabei nicht Merlitz' späteren
`k`-Regler als solchen. Die Übersetzung des Resultats auf ungefähr `k = 0.8`
ist Merlitz'
Modellinterpretation; sein eigener einfacher Online-Test ergab eher Werte um
`k = 0.7`.

## Verbindung zu den x-y-/u-v-Plots

Der Checkerboard-Test und die Schwenk-Plots gehören zum selben Thema, machen
aber nicht dasselbe. Beim Checkerboard wird ein stehendes Muster über `k`
eingestellt. Die Plots zeigen dagegen, wie sich ein ruhender Punkt im Bild
bewegt, wenn die Kamera horizontal geschwenkt wird. Ausgangspunkt ist der
lineare `(u,v)`-Bildraum:

```text
x = X/Z,  y = Y/Z
u = m x,  v = m y

du/dpsi = -(m + u^2/m)
dv/dpsi = -u v/m
```

Erst danach vergleichen die Plotunterlagen verschiedene Zielkoordinaten:

```text
linear:   (x_L, y_L) = (u, v)
Schön:   (x_S, y_S) = (atan(u), atan(v))
Merlitz:  (x_M, y_M) = atan(r)/r * (u, v),  r = sqrt(u^2+v^2)
```

`GlobeEffectCoordinateMapping2D` enthält dieselben Definitionen auch in C#.
Außerdem ist dort geprüft, dass die Merlitz-Abbildung bei
`k = 1` genau in die lineare Abbildung `(u,v)=m(x,y)` übergeht. Damit besitzen
Plot und Unity-Projekt dieselbe mathematische Grundlage.

Die Plotpfeile steuern den sichtbaren Checkerboard-Trial weiterhin nicht: Sie
beschreiben eine **dynamische Schwenkgeschwindigkeit**, während das
Checkerboard ein **statischer Einstelltest** ist. Der Random-Dot-Test setzt die
dynamische Frage deshalb in einer eigenen Szene um. Beide verwenden dieselbe
Merlitz-Gleichung, aber mit unterschiedlichen Aufgaben für die Versuchsperson.

## Eye Tracking

Die Grundstruktur stammt aus der im Lab verwendeten Eye-Tracking-Toolbox. Dazu
gehören `IEyeTracker`, `EyeTrackingEvent`, `EyeTrackingToolbox` und der
`DummyEyeTracker`. Die Originaldateien im PLACES-Projekt bleiben unverändert.
Im vorliegenden Repository wurde die Kopie an die Varjo XR-4 und unsere
Messdateien angepasst. Neu für dieses Projekt sind vor allem
`VarjoEyeTracker`, `CheckerboardFixationMonitor` und
`RandomDotFixationMonitor`.

Der Varjo-Provider verwendet `VarjoEyeTracking.GetGazeList(...)`. Dadurch
werden alle seit dem letzten Unity-Frame bereitgestellten Samples abgeholt und
nicht nur das jeweils letzte Sample. Rohstatus, Zeitstempel, lokale Blickstrahlen,
Pupillendurchmesser, Augenöffnung, Fokusdistanz und IPD bleiben in der
Gaze-Datei erhalten. Parallel schreibt die Toolbox Position und Drehung der
Main Camera und des Checkerboards in eine Head-Datei.

Ohne eigenes Ausgabeverzeichnis liegen technische `F9`-Aufzeichnungen unter:

```text
Application.persistentDataPath/Measurements
```

Der genaue Windows-Pfad beider Dateien wird beim Start der Aufzeichnung in der
Unity Console ausgegeben. Mit dem Provider `Dummy` kann die Pipeline ohne
Headset getestet werden; der simulierte Blick folgt dann der Maus in der Game
View.

`CheckerboardFixationMonitor` berechnet den Winkel zwischen Blickstrahl und
Checkerboard-Mittelpunkt. Bei `LeftEyeOnly` beziehungsweise `RightEyeOnly`
wird der Blickstrahl des dargestellten Auges benutzt, bei `BothEyes` der
kombinierte Strahl. Voreinstellung sind 3 Grad Toleranz und 0.3 Sekunden
ununterbrochene Fixation. Diese Werte sind technische Startwerte und müssen
vor der eigentlichen Studie als Teil des Versuchsprotokolls festgelegt werden.

### Versuchsleiteranzeige

Das Editorfenster `Experiment Monitor` verwendet in beiden Demoszenen
automatisch den jeweils vorhandenen Fixationsmonitor. Es erscheint beim Start
des Play Mode, ohne der Game View den Tastaturfokus zu nehmen. Alternativ kann
es über `Tools > Globe Effect > Open Experiment Monitor` geöffnet werden.

Die große Statusfläche unterscheidet drei Zustände:

- `ON TARGET`: gültiges Gaze-Sample innerhalb der Winkeltoleranz;
- `OFF TARGET`: gültiges Sample, aber Blick außerhalb der Toleranz;
- `NO VALID GAZE`: kein verwertbares aktuelles Eye-Tracking-Sample.

Darunter stehen Blickabweichung, Toleranz, kontinuierliche Fixationsdauer,
Trial, aktuelles `k` und der Status der Antwortfreigabe. Beim Random-Dot-Test
werden zusätzlich Gierwinkel und abgeschlossene Kopfseitenwechsel angezeigt.
Da es ein reines Unity-Editorfenster ist, erscheint diese Information nur auf
dem Kontrollmonitor und nicht im XR-Bild der Versuchsperson.

Die automatische Öffnung kann über
`Tools > Globe Effect > Auto Open Experiment Monitor on Play` ein- oder
ausgeschaltet werden. `Require Fixation Before Confirmation` bleibt davon
getrennt: Ist dieser Inspector-Haken deaktiviert, wird Fixation angezeigt und
gespeichert, blockiert `Enter` aber nicht.

## Welche C#-Dateien für die Mathematik wichtig sind

Wenn der mathematische Kern gezeigt oder besprochen werden soll, sind vor allem
diese Dateien relevant:

- `MerlitzCheckerboardMath.cs`: enthält die Merlitz-Gleichung in beide
  Richtungen und die Rückwärtsrechnung für das Checkerboard;
- `AngularGeometry.cs`: berechnet aus Abstand und Winkeldurchmesser die nötige
  physische Größe der Fläche;
- `GlobeEffectCoordinateMapping2D.cs`: enthält die `(x,y)`-/`(u,v)`-Abbildung,
  die Schwenkgleichungen sowie den Vergleich zwischen linear, Schön und
  Merlitz;
- `VrCheckerboardStimulus.cs`: verbindet die Mathematik mit dem Unity-Objekt,
  setzt Abstand und Größe und übergibt `k`, `m` und Augenmodus an den Shader;
- `GlobeEffectMerlitzCheckerboard.shader`: zeichnet das eigentliche Muster für
  jeden Pixel und enthält auch die kreisrunde Begrenzung und Augenmaskierung;
- `RandomDotFieldStimulus.cs` und `GlobeEffectMerlitzRandomDots.shader`:
  übernehmen dieselbe Grundidee für das Punktfeld mit Kopfbewegung.

Für ein Gespräch mit der Betreuung reichen als mathematischer Kern meistens
die ersten drei C#-Dateien zusammen mit dem Checkerboard-Shader. Der
`CheckerboardDemoSceneBuilder` ist dagegen nur dafür zuständig, die passenden
Unity-Objekte anzulegen und miteinander zu verbinden.

## Wie die Skripte zusammenspielen

Der Checkerboard-Stimulus kann zum Beispiel so gesteuert werden:

```csharp
stimulus.SetGeometry(70f, 1.5f);
stimulus.SetMerlitzK(0.7f);
stimulus.SetEyePresentation(CheckerboardEyePresentation.LeftEyeOnly);
stimulus.PlaceInFrontOfObserver();
stimulus.Show();

stimulus.StimulusPresented += snapshot =>
{
    // Trial-Logger oder Eye-Tracking-Marker später hier anbinden.
};
```

`CaptureSnapshot()` liest den aktuell dargestellten Zustand aus: Zeitstempel,
Sichtbarkeit, FOV, Abstand, physischer Durchmesser, `k`, `m`, Anzahl der Felder
und Augenmodus. Der `CheckerboardTrialSessionController` verwendet diese Werte
für die Trialdatei. Eine spätere Bedienoberfläche kann dieselben öffentlichen
Methoden verwenden, ohne die Mathematik neu zu schreiben.

## Annahmen und Dinge, die noch geprüft werden müssen

- Der Checkerboard-Stimulus ist eine **ebene** Fläche, die direkt zur
  Versuchsperson ausgerichtet wird. Er ist keine Kugel.
- Der Abstand wird vom Center-Eye-/Head-Transform zur Flächenmitte gemessen.
  Wegen der IPD ist der exakte Winkel jedes einzelnen Auges bei einer gemeinsamen
  Ebene minimal verschieden. Falls die Fläche später für jedes Auge einzeln
  angepasst werden soll, wäre dafür getrennte Stereo-Geometrie nötig.
- Der monokulare Modus unterdrückt ein Auge im Shader. Er ersetzt keine
  Messung von Display-Crosstalk und keine klinische Okklusion.
- Die XR-Augenauswahl benutzt Unitys `unity_StereoEyeIndex`. Für Varjo
  `Multi Pass` verwendet der Shader zusätzlich die Position der jeweiligen
  Renderkamera relativ zur Center-Eye-Pose. Die
  Unterdrückung in Context- und Focus-Ansicht muss im Headset noch einmal genau
  geprüft werden. Im normalen Game-View entspricht
  die Vorschau dem linken Auge.
- Die kreisrunde Begrenzung und der gleichbleibende Winkeldurchmesser sind
  Entscheidungen für unseren VR-Test. Der Aufbau ist keine exakte Kopie des
  Oomes-Versuchs.
- Fixation wird angezeigt und pro Trial gespeichert. Eine optionale Freigabe
  vor `Enter` ist eingebaut. Wann ein Trial wiederholt oder ausgeschlossen
  wird, muss vor der Studie noch festgelegt werden.
- `leftValidity` und `rightValidity` akzeptieren Varjos Status `Compensated`
  oder `Tracked`; die exakten Rohstatuswerte werden zusätzlich gespeichert.
  Vor der Datenerhebung muss festgelegt werden, welche Statuswerte später als
  gültig gelten.
- Aktuell vorhanden sind der stehende Checkerboard-Test, die mathematische
  Grundlage der Plots und ein erster Random-Dot-Test mit Kopfbewegung.
  Kopfamplitude, Wiederholungszahl und genaue Instruktion sind noch nicht final.

## Roadmap

1. **Checkerboard – vorhanden:** kreisrundes Muster, FOV, Abstand,
   winkelkonstante Skalierung, mono/binokular und Merlitz-`k`.
2. **XR-4 – technisch lauffähig:** Varjo-Loader, Multi Pass,
   HMD-Tracking, Recenter und XR-Augenauswahl; die Augenmaskierung muss noch
   genauer im Headset geprüft werden.
3. **Messdaten – vorhanden:** Varjo-Eye-Tracking, Fixationsmonitor,
   Marker sowie Gaze-/Head-CSV.
4. **Erster Testablauf – vorhanden:** mit gleichem Seed wiederholbare
   Reihenfolge, zwei
   Startwert-Richtungen, Antwortbestätigung und automatische Sitzungsdateien.
5. **Random-Dot-Test – erster Prototyp:** festes Punktfeld, echte oder
   simulierte Kopfbewegung, `k`-Einstellung, Bewegungszähler und Messdateien;
   der Test am XR-4 steht noch aus.
6. **Natürliche Szene – offen:** nach dem Punktfeldtest dieselbe Abbildung auf
   eine alltagsnahe 3D-Szene übertragen.
7. **Vor Hauptstudie offen:** exakte Bedingungen und Wiederholungszahl mit der
   Betreuung festlegen, Winkel/Fixation/Augenmaskierung am XR-4 messen,
   Test-Retest-Pilot durchführen und Ausschlussregeln einfrieren.

## Technische Prüfungen

- Die EditMode-Tests prüfen die Vorwärts-/Rückabbildung für
  `k = 0, 0.5, 0.7, 1`, die Skalierung am Rand und die exakte Verdopplung des
  physischen Durchmessers bei doppeltem Abstand.
- Der aktuelle Stand enthält 39 EditMode-Testfälle für Mathematik,
  Skalierung, lineare 2D-Koordinaten, wiederholbare Zufallsreihenfolgen,
  Blickstrahltransformation, Kopf-Sweep-Zähler und technische Steuerung.
- Runtime- und Testquellen sind für Unity `6000.5.6f1` ausgelegt.
- Vor der Datenerhebung müssen Shader, Augenmaskierung, Rendering und die reale
  Winkelgröße mit der XR-4 auf dem Laborrechner geprüft werden.

## Quellen

- Holger Merlitz, *Distortion of binoculars revisited: Does the sweet spot
  exist?*, Journal of the Optical Society of America A 27, 50-57 (2010):
  https://holgermerlitz.de/globe/distortion_final.pdf
- A. H. J. Oomes, J. J. Koenderink, A. J. van Doorn, H. de Ridder,
  *What are the uncurved lines in our visual field? A fresh look at Helmholtz's
  checkerboard*, Perception 38, 1284-1294 (2009):
  https://doi.org/10.1068/p6288
- Varjo Technologies, *Varjo Unity XR SDK compatibility*:
  https://developer.varjo.com/docs/unity-xr-sdk/compatibility
- Varjo Technologies, *Developer tools in Varjo Base*:
  https://developer.varjo.com/docs/get-started/developer-tools-in-varjo-base
- Varjo Technologies, *Varjo Unity XR Plugin*:
  https://github.com/varjocom/VarjoUnityXRPlugin
- Varjo Technologies, *Eye tracking with Varjo XR Plugin*:
  https://developer.varjo.com/docs/unity-xr-sdk/eye-tracking-with-varjo-xr-plugin
- Unity, *Single-pass instanced rendering and custom shaders*:
  https://docs.unity3d.com/6000.0/Documentation/Manual/SinglePassInstancing.html

## Projektstruktur

```text
Assets/GlobeEffect/
  Demo/           getrennte Checkerboard- und Random-Dot-Referenzszenen
  Editor/         automatischer Aufbau beider Demoszenen
  Runtime/
    EyeTracking/  Provider, CSV-Aufzeichnung und Fixationskontrolle
    Experiment/   Trialplan, Sitzungssteuerung und Ergebnisdateien
    RandomDots/   weltfestes Punktfeld, k-Bedienung und Sweep-Messung
    Scripts/      Mathematik und Steuerung des Checkerboards
    Resources/    XR-fähige Checkerboard- und Random-Dot-Shader
  Tests/EditMode/ Mathematik- und Skalierungstests
Assets/XR/        versionierte Varjo-/XR-Management-Einstellungen und Loader
Assets/XRI/       Einstellungen des XR Interaction Toolkit
Packages/         verwendete Unity-, Test- und XR-Pakete
ProjectSettings/  Unity-Projektversion
```
