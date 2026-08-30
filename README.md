# Globe Effect Master Thesis

Aktueller Projektstand: Unity/C#-Implementierung eines kreisrunden, radial
verzeichneten Checkerboard-Stimulus und eines dynamischen Random-Dot-k-Tests
fuer die Untersuchung des Globe Effects. Stimulusgeometrie, Merlitz-Abbildung,
Versuchssteuerung und Eye-Tracking-Anbindung sind modular getrennt.

## Stand

- kreisrunde Stimulusflaeche;
- Winkeldurchmesser/FOV und Abstand getrennt einstellbar;
- physischer Durchmesser wird mit dem Abstand skaliert;
- Merlitz-Parameter `k` im belegten Bereich 0 bis 1;
- Instrumentvergroesserung `m` separat einstellbar, Default `m = 10`;
- beidseitige, nur linke oder nur rechte XR-Darbietung;
- winkelkonstantes zentrales Fixationskreuz;
- Laufzeit-API, Parameter-Snapshot und Events fuer spaetere Trial-/Eye-Tracking-Anbindung;
- vorkonfigurierte Demo-Szene mit XR Origin und getrackter Center-Eye-Kamera;
- Input System, XR Interaction Toolkit und XR Plugin Management als fest
  versionierte Unity-Pakete;
- Varjo Unity XR Plugin `3.7.3` und gespeicherter Varjo-Loader fuer die
  Varjo XR-4;
- Varjo-Rendering im auf dem Laborrechner funktionsfaehigen `Multi Pass`-Modus;
- Tastatursteuerung fuer Recenter und schrittweise Aenderung von `k`;
- uebernommene Provider-Struktur der Lab-Eye-Tracking-Toolbox;
- Varjo-XR-4-Provider mit vollstaendiger Abfrage der Gaze-Sample-Queue;
- Dummy-Provider fuer Tests ohne Headset;
- getrennte CSV-Dateien fuer Gaze- und Transformdaten sowie Ereignismarker;
- Fixationskontrolle relativ zum aktuellen Checkerboard-Mittelpunkt;
- vollfaktorieller Trialplan mit reproduzierbarer Seed-Randomisierung;
- automatische Sitzungsordner, Plan-, Antwort-, Gaze- und Head-CSV-Dateien;
- Trialsteuerung fuer Start, k-Einstellung, Bestaetigung, Recenter und Abbruch;
- explizites 2D-Koordinatenmodul fuer den linearen `(u,v)`-Bildraum und die
  Schwenkgleichungen der begleitenden Plots;
- reproduzierbares, weltfestes Schwarz-Weiss-Punktfeld mit realer
  Kopfbewegung und separatem simuliertem Debug-Schwenk;
- dynamischer Einstelltest, bei dem `k` waehrend wiederholter Links-Rechts-
  Kopfbewegungen auf subjektive Stabilitaet eingestellt wird;
- automatische Erkennung und Protokollierung gueltiger Kopf-Seitenwechsel;
- EditMode-Tests fuer Winkelgeometrie und Merlitz-Abbildung.

## Schnellstart

1. Das Projekt mit Unity 6.5 oeffnen. Zielversion ist
   konkret Editor `6000.5.6f1`.
2. `Assets/GlobeEffect/Demo/CheckerboardDemo.unity` oeffnen. Die Szene enthaelt
   bereits XR Origin, Main Camera und Checkerboard-Stimulus.
3. Unter `Edit > Project Settings > XR Plug-in Management` kontrollieren, dass
   im Standalone-Reiter `Initialize XR on Startup` und `Varjo` aktiv sind. Diese
   Zuordnung ist im Repository bereits gespeichert. Fuer den Laborbetrieb nicht
   gleichzeitig den generischen OpenXR-Loader aktivieren. Unter `Varjo` ist
   `Stereo Rendering Mode = Multi Pass` gespeichert, da nur dieser Modus auf
   dem verwendeten XR-4-Rechner ein Bild in Game View und Headset lieferte.
4. Fuer einen Einzeltest am Objekt `Checkerboard Stimulus` die Parameter
   `Angular Diameter Degrees`, `Viewing Distance Meters`, `Merlitz K` und
   `Eye Presentation` einstellen.
5. Fuer einen weltfesten Trial `Follow Observer Every Frame` deaktiviert lassen.
   Im Play Mode mit `R` in der aktuellen Blickrichtung platzieren.
6. Vor Eye-Tracking-Tests in Varjo Base `Allow eye tracking` aktivieren und mit
   `C` die Blickkalibrierung starten. Die XR-4 sollte nach jedem erneuten
   Aufsetzen fuer die betreffende Person neu kalibriert werden.
7. Fuer eine zusammenhaengende Pilot-Sitzung am Objekt
   `Checkerboard Trial Session` eine pseudonymisierte `Participant Id`, das
   `Session Label` und die Bedingungslisten kontrollieren. Danach im Play Mode
   mit `F5` starten.

Die Referenzszene kann bei Bedarf ueber
`Tools > Globe Effect > Create or Reset Demo Scene` reproduzierbar neu erstellt
werden. Dabei werden die Ausgangswerte 70 Grad, 1 Meter, `k = 0.7` und `m = 10`
gesetzt.

Unity-Einheit ist hier gleich ein Meter. Fuer exakte Skalierung sollte das
Stimulusobjekt keinen nicht-uniform skalierten Parent besitzen.

## Vorschau und XR-Diagnose

Die normale Unity Game View zeigt das Checkerboard auch ohne angeschlossene
Brille als flache Vorschau. Kopfbewegung, Stereo-Augenauswahl und die reale
Winkelgroesse lassen sich damit nicht validieren. Fuer einen vollstaendigen
Test ohne angeschlossene XR-4 stellt Varjo Base einen Headset-Simulator bereit.

Bei einem schwarzen Bild zuerst die Game View vor und waehrend des Play Mode
vergleichen:

1. Ist das Checkerboard vor Play sichtbar, funktionieren Mesh, Material,
   Grundshader und die normale Kamera grundsaetzlich.
2. Unter `XR Plug-in Management` muessen `Initialize XR on Startup` und `Varjo`
   aktiv sein.
3. Auf dem verwendeten XR-4-Rechner muss aktuell `Stereo Rendering Mode =
   Multi Pass` gewaehlt sein; `Stereo` und `Two Pass` lieferten dort ein
   schwarzes Bild.

Beim Start einer XR-Sitzung wird die Kamera erst auf die reale HMD-Pose gesetzt,
wenn der Provider eine gueltige Center-Eye-Pose meldet. Der Stimulus wartet auf
diesen Tracking-Status und bleibt danach standardmaessig weltfest.

## Bedienung im Play Mode

Die Komponente `Checkerboard Keyboard Controller` ist nur eine einfache
technische Bedienung. Vor dem Tastendruck muss die Game View den Eingabefokus
haben.

- `R`: Stimulus in der aktuellen HMD-Blickrichtung neu platzieren;
- Pfeiltaste links: `k` um `0.01` verringern;
- Pfeiltaste rechts: `k` um `0.01` erhoehen;
- `Shift` zusammen mit einer Pfeiltaste: Schrittweite `0.05`;
- `C`: Varjo-Blickkalibrierung starten;
- `F9`: technische Gaze-/Head-Aufzeichnung starten oder beenden.

Jede Aenderung wird in der Console ausgegeben. `F9` dient nur zur technischen
Pruefung der Datenpipeline und sollte nicht waehrend einer mit `F5` gestarteten
Sitzung benutzt werden.

## Pilot-Sitzung

Die Komponente `Checkerboard Trial Session` bildet aus allen Eintraegen in
`Angular Diameters Degrees`, `Viewing Distances Meters`, `Eye Presentations`
und `Starting K Values` einen vollfaktoriellen Plan. Jede Kombination kommt pro
`Repetitions Per Condition` gleich oft vor. Es werden immer komplette
Bedingungen gemischt, sodass FOV, Abstand, Augenmodus und Startwert nicht
versehentlich auseinanderfallen. Derselbe `Random Seed` erzeugt bei denselben
Listen dieselbe Reihenfolge.

Die Demoszene enthaelt als kurzen technischen Startplan sechs Trials: drei
Augenmodi mal zwei Startwerte fuer `k`. Diese Werte sind noch kein festgelegtes
Studienprotokoll. Weitere FOV- oder Abstandswerte koennen direkt als neue
Listeneintraege im Inspector ergaenzt werden.

- `F5`: neue Sitzung starten;
- Pfeiltasten links/rechts: `k` einstellen;
- `Shift` plus Pfeiltaste: groessere Schrittweite;
- `R`: bei Bedarf in der aktuellen Blickrichtung neu zentrieren;
- `Enter`: aktuellen `k`-Wert bestaetigen und zum naechsten Trial wechseln;
- `F6`: laufende Sitzung kontrolliert abbrechen und den begonnenen Trial
  als abgebrochen speichern.

Standardmaessig wird der Fixationsstatus mitgespeichert, blockiert die Antwort
aber noch nicht. `Require Fixation Before Confirmation` kann im Inspector
aktiviert werden, sobald Toleranz, Mindestdauer und Umgang mit ungueltigen
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
nicht die bereits bestaetigten Antworten.

## Dynamischer Random-Dot-k-Test

Die separate Szene
`Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity` setzt den dynamischen Teil
des Globe-Effect-Tests um. Falls die Szene nach einem frischen Pull noch nicht
vorhanden ist, wird sie beim ersten Unity-Import automatisch erstellt. Sie kann
ausserdem ueber
`Tools > Globe Effect > Create or Reset Random Dot Demo Scene` reproduzierbar
neu aufgebaut werden.

Das Punktfeld besteht standardmaessig aus 4000 schwarzen und weissen Punkten
auf einer weltfesten sphaerischen Kappe. Die Punkte werden nicht von selbst
bewegt. Im vorgesehenen Modus `HeadTracked` dreht die Versuchsperson den Kopf
abwechselnd nach links und rechts. Der dadurch entstehende optische Fluss wird
mit der instrumentellen Merlitz-Abbildung verzerrt. Die Aufgabe lautet:

> Stelle `k` so ein, dass das Punktfeld waehrend der Kopfbewegung moeglichst
> stabil und nicht schwimmend erscheint.

Der technische Startplan umfasst zwei Trials mit `k = 0.3` beziehungsweise
`k = 0.9` als randomisierte Startwerte. Standardmaessig muessen vor `Enter`
vier Wechsel zwischen linker und rechter Schwelle abgeschlossen sein. Die
Startschwelle von 2.5 Grad ist ein technischer Wert fuer `m = 10` und noch
keine festgelegte Studienbedingung. Die erzeugte Punktkappe umfasst zunaechst
20 Grad im unverzerrten Objektraum; durch `m = 10` fuellt dieser Bereich das
wesentlich groessere sichtbare Feld. Dieser Wert ist daher nicht mit dem
angezeigten Winkeldurchmesser von 70 Grad gleichzusetzen.

- `F5`: Random-Dot-Sitzung starten;
- Pfeiltasten links/rechts: `k` veraendern;
- `Shift` plus Pfeiltaste: groessere Schrittweite;
- `R`: Punktfeld an der aktuellen Kopfpose neu verankern und Sweep-Zaehler
  zuruecksetzen;
- `Enter`: finalen `k`-Wert bestaetigen, sobald das Kopfkriterium erfuellt ist;
- `F6`: Sitzung kontrolliert abbrechen;
- `C`: Varjo-Eye-Tracking vor Sitzungsbeginn kalibrieren.

Fuer eine Vorschau ohne HMD kann im Inspector der Liste `Motion Modes` statt
`HeadTracked` der Wert `SimulatedYaw` zugewiesen werden. Dieser Modus simuliert
nur den visuellen Schwenk und ist nicht als Ersatz fuer die reale
Versuchsbedingung gedacht.

Plan-, Antwort-, Gaze- und Head-Dateien werden wie beim Checkerboard in einem
eigenen Sitzungsordner gespeichert. Die Trialdatei enthaelt zusaetzlich
Punkt-Seed, Bewegungsmodus, Winkelschwelle, abgeschlossene Seitenwechsel,
tatsaechlichen Gierwinkelbereich sowie den Fixationsstatus. Marker wie
`HeadHalfSweep`, `KAdjusted` und `TrialConfirmed` verbinden diese Ereignisse
mit Gaze- und Head-Samples.

## Mathematik

Merlitz fasst die instrumentelle radiale Abbildung als

```text
tan(k a) = m tan(k A)
a(A) = atan(m tan(k A)) / k
```

zusammen. `A` ist der wahre Objektwinkel, `a` der scheinbare Bildwinkel und `m`
die paraxiale Vergroesserung. Die belegten Spezialfaelle sind:

- `k = 1`: Tangensbedingung; ein planes Gitter bleibt im Bildraum geradlinig;
- `k = 0.5`: Kreisbedingung nach Slevogt/Helmholtz;
- `k -> 0`: Winkelbedingung mit dem Grenzwert `a = m A`.

Der Shader tastet die Abbildung invers ab. Fuer einen normierten angezeigten
Radius `r` und den scheinbaren Halbwinkel `alpha` gilt:

```text
a(r) = atan(r tan(alpha))
A(a) = atan(tan(k a) / m) / k
s(r) = tan(A(a(r))) / tan(A(alpha))
```

`s` ist der normierte Radius im urspruenglich regelmaessigen Wandgitter. Die
Richtung um die Bildmitte bleibt erhalten. Diese Randnormierung ist eine
bewusste VR-Designentscheidung: Der angezeigte Winkeldurchmesser bleibt fuer
alle `k` exakt gleich. Merlitz' Paper hielt dagegen ein wahres Feld von 7 Grad
bei 10-facher Vergroesserung fest und erhielt ein scheinbares Feld von ungefaehr
70 Grad.

Die physische Groesse der ebenen Kreisflaeche ist

```text
D = 2 d tan(theta / 2)
```

mit Abstand `d` und Winkeldurchmesser `theta`. Deshalb verdoppelt sich `D`
exakt, wenn `d` bei gleichem `theta` verdoppelt wird.

## Was `k` hier bedeutet – und was nicht

Merlitz verwendet zwei verschiedene Parameter:

- `k` beschreibt die Verzeichnung des Instruments beziehungsweise des
  dargestellten Checkerboard-Stimulus;
- `l` beschreibt in seinem Modell eine zusaetzliche radiale Abbildung des
  menschlichen visuellen Raums: `y = tan(l a) / l`.

Diese Implementierung rechnet `l` **nicht** in den Stimulus. Im
Checkerboard-Versuch stellt die Versuchsperson die Stimuluskrummung
beziehungsweise `k` so ein, dass
das Gitter subjektiv gerade erscheint. Der gewaehlte Stimuluswert dient dann –
unter den Modellannahmen – als Schaetzung der visuellen Kompensation. `k` und
`l` im Code gleichzusetzen, bevor die Versuchsperson geantwortet hat, waere ein
Zirkelschluss.

Oomes et al. liessen 20 stationaere, monokular beobachtende Personen die
Krummung eines Checkerboards zwischen Tonnen- und Kissenform einstellen. Sie
verglichen zentrale Fixation mit freiem Blick. Die meisten Personen stellten
eine Kissenverzeichnung als subjektiv gerade ein; die mittlere Wirkung war etwa
halb so stark wie von Helmholtz behauptet, mit grossen interindividuellen
Unterschieden. Oomes verwendete dabei nicht Merlitz' spaeteren `k`-Regler als
solchen. Die Uebersetzung des Resultats auf ungefaehr `k = 0.8` ist Merlitz'
Modellinterpretation; sein eigener einfacher Online-Test ergab eher Werte um
`k = 0.7`.

## Verbindung zu den x-y-/u-v-Plots

Der statische Checkerboard-Test und die Schwenk-Plots beantworten verwandte,
aber nicht identische Fragen. Im Checkerboard-Trial wird die radiale
Instrumentabbildung ueber `k` eingestellt. Die Plots beginnen dagegen mit der
Bahn eines ruhenden Fernpunkts bei einem horizontalen Kameraschwenk im linearen
Bildraum:

```text
x = X/Z,  y = Y/Z
u = m x,  v = m y

du/dpsi = -(m + u^2/m)
dv/dpsi = -u v/m
```

Erst danach vergleichen die Plotunterlagen verschiedene Zielkoordinaten:

```text
linear:   (x_L, y_L) = (u, v)
Schoen:   (x_S, y_S) = (atan(u), atan(v))
Merlitz:  (x_M, y_M) = atan(r)/r * (u, v),  r = sqrt(u^2+v^2)
```

`GlobeEffectCoordinateMapping2D` enthaelt diese Definitionen jetzt auch in C#.
Ausserdem ist dort geprueft, dass die instrumentelle Merlitz-Abbildung bei
`k = 1` genau in die lineare Abbildung `(u,v)=m(x,y)` uebergeht. Damit besitzen
Plot und Unity-Projekt nun dieselbe explizite Koordinatenbasis.

Die Plotpfeile steuern den sichtbaren Checkerboard-Trial weiterhin nicht: Sie
beschreiben eine **dynamische Schwenkgeschwindigkeit**, waehrend das
Checkerboard ein **statischer Einstelltest** ist. Der Random-Dot-Test setzt die
dynamische Fragestellung deshalb als eigenes Stimulusmodul um. Beide verwenden
dieselbe instrumentelle Merlitz-Gleichung, ohne die Aufgaben zu vermischen.

## Eye Tracking

Die Eye-Tracking-Schicht folgt der im Lab verwendeten Toolbox-Struktur:
`IEyeTracker` definiert den gemeinsamen Provider-Vertrag,
`EyeTrackingEvent` leitet neue Samples weiter und `EyeTrackingToolbox`
uebernimmt Weltkoordinaten, Aufzeichnung und Ereignismarker. Der bisherige
Vive-/SRanipal-Provider wird in diesem Projekt durch `VarjoEyeTracker` ersetzt.

Der Varjo-Provider verwendet `VarjoEyeTracking.GetGazeList(...)`. Dadurch
werden alle seit dem letzten Unity-Frame bereitgestellten Samples abgeholt und
nicht nur das jeweils letzte Sample. Rohstatus, Zeitstempel, lokale Blickstrahlen,
Pupillendurchmesser, Augenoeffnung, Fokusdistanz und IPD bleiben in der
Gaze-Datei erhalten. Die Toolbox schreibt parallel die konfigurierten
Transformationen von Main Camera und Checkerboard in eine Head-Datei.

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
ununterbrochene Fixation. Diese Werte sind technische Startwerte und muessen
vor der eigentlichen Studie als Teil des Versuchsprotokolls festgelegt werden.

## Laufzeit-Anbindung

Die zentrale Komponente stellt unter anderem bereit:

```csharp
stimulus.SetGeometry(70f, 1.5f);
stimulus.SetMerlitzK(0.7f);
stimulus.SetEyePresentation(CheckerboardEyePresentation.LeftEyeOnly);
stimulus.PlaceInFrontOfObserver();
stimulus.Show();

stimulus.StimulusPresented += snapshot =>
{
    // Trial-Logger oder Eye-Tracking-Marker spaeter hier anbinden.
};
```

`CaptureSnapshot()` liefert Zeitstempel, Sichtbarkeit, FOV, Abstand, physischen
Durchmesser, `k`, `m`, Check-Anzahl und Augenmodus. Der
`CheckerboardTrialSessionController` verwendet diese API und kann spaeter durch
eine Bedienoberflaeche oder weitere Lab-Komponenten erweitert werden, ohne den
Stimulus neu zu schreiben.

## Annahmen und offene Validierung

- Der Stimulus ist eine frontoparallele **ebene** Flaeche, keine Kugel.
- Der Abstand wird vom Center-Eye-/Head-Transform zur Flaechenmitte gemessen.
  Wegen der IPD ist der exakte Winkel jedes einzelnen Auges bei einer gemeinsamen
  realen Ebene minimal verschieden; fuer eine per-Auge kalibrierte Flaeche waere
  spaeter eine separate Stereo-Geometrie noetig.
- Der monokulare Modus unterdrueckt ein Auge im Shader. Er ersetzt keine
  Messung von Display-Crosstalk und keine klinische Okklusion.
- Die XR-Augenauswahl benutzt Unitys `unity_StereoEyeIndex`. Fuer Varjo
  `Multi Pass` verwendet der Shader zusaetzlich die Position der jeweiligen
  Renderkamera relativ zur Center-Eye-Pose. Die
  vollstaendige Unterdrueckung von Context- und Focus-Ansicht muss nach dieser
  Aenderung erneut im Headset geprueft werden. Im normalen Game-View entspricht
  die Vorschau dem linken Auge.
- Die kreisrunde Apertur und der festgehaltene scheinbare Winkeldurchmesser sind
  Festlegungen dieses Versuchsdesigns, keine Behauptung einer exakten
  Replikation des Oomes-Versuchsaufbaus.
- Fixation wird angezeigt und pro Trial gespeichert. Eine optionale Freigabe
  vor `Enter` ist implementiert; die wissenschaftliche Regel fuer Wiederholung
  oder Ausschluss ist noch nicht festgelegt.
- `leftValidity` und `rightValidity` akzeptieren Varjos Status `Compensated`
  oder `Tracked`; die exakten Rohstatuswerte werden zusaetzlich gespeichert.
  Das endgueltige Qualitaetskriterium muss vor der Datenerhebung festgelegt und
  in der Auswertung dokumentiert werden.
- Dieser Stand implementiert den statischen Checkerboard-Einstelltest, die
  mathematische 2D-Basis der Plots und einen separaten dynamischen
  Random-Dot-k-Prototyp. Die Kopfamplitude, Wiederholungszahl und genaue
  Instruktion sind noch keine festgelegten Studienparameter.

## Roadmap

1. **Stimulusbasis – umgesetzt:** kreisrundes Checkerboard, FOV, Abstand,
   winkelkonstante Skalierung, mono/binokular und Merlitz-`k`.
2. **XR-4-Betrieb – technisch umgesetzt:** Varjo-Loader, Multi Pass,
   HMD-Tracking, Recenter und XR-Augenauswahl; die Augenmaskierung muss noch
   systematisch im Headset validiert werden.
3. **Messpipeline – umgesetzt:** Varjo-Eye-Tracking, Fixationsmonitor,
   Marker sowie Gaze-/Head-CSV.
4. **Pilotablauf – umgesetzt:** reproduzierbarer Trialplan, zwei
   Startwert-Richtungen, Antwortbestaetigung und automatische Sitzungsdateien.
5. **Dynamischer Random-Dot-Test – Prototyp umgesetzt:** weltfestes Punktfeld,
   reale oder simulierte Gierbewegung, Merlitz-`k`-Einstellung, Sweep-Kriterium
   und vollstaendige Messdateien; Validierung am XR-4 ist offen.
6. **Natuerliche Szene – offen:** dieselbe kontrollierte Abbildung nach der
   Punktfeldvalidierung auf eine alltagsnahe 3D-Szene uebertragen.
7. **Vor Hauptstudie offen:** exakte Bedingungen und Wiederholungszahl mit der
   Betreuung festlegen, Winkel/Fixation/Augenmaskierung am XR-4 messen,
   Test-Retest-Pilot durchfuehren und Ausschlussregeln einfrieren.

## Qualitaetssicherung

- Die EditMode-Tests pruefen die Vorwaerts-/Rueckabbildung fuer
  `k = 0, 0.5, 0.7, 1`, die Randnormierung und die exakte Verdopplung des
  physischen Durchmessers bei doppeltem Abstand.
- Der aktuelle Stand enthaelt 35 EditMode-Testfaelle fuer Mathematik,
  Skalierung, lineare 2D-Koordinaten, reproduzierbare Randomisierung,
  Blickstrahltransformation, Kopf-Sweep-Zaehler und technische Steuerung.
- Runtime- und Testquellen sind fuer Unity `6000.5.6f1` ausgelegt.
- Vor der Datenerhebung sind Shader, Augenmaskierung, Render-Pipeline und reale
  Winkelgroesse auf dem Laborrechner mit dem konkreten Headset zu validieren.

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
  Editor/         reproduzierbarer Aufbau beider Referenzszenen
  Runtime/
    EyeTracking/  Provider, CSV-Aufzeichnung und Fixationskontrolle
    Experiment/   Trialplan, Sitzungssteuerung und Ergebnisdateien
    RandomDots/   weltfestes Punktfeld, k-Bedienung und Sweep-Messung
    Scripts/      reine Mathematik, Stimulussteuerung, Integrations-API
    Resources/    XR-faehige Checkerboard- und Random-Dot-Shader
  Tests/EditMode/ Mathematik- und Skalierungstests
Assets/XR/        versionierte Varjo-/XR-Management-Einstellungen und Loader
Assets/XRI/       Einstellungen des XR Interaction Toolkit
Packages/         fest versionierte Unity-, Test- und XR-Abhaengigkeiten
ProjectSettings/  Unity-Projektversion
```
