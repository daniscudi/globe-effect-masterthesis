# Globe Effect Master Thesis

Aktueller Projektstand: Unity/C#-Implementierung eines kreisrunden, radial verzeichneten
Checkerboard-Stimulus fuer die Untersuchung des Globe Effects. Die
Stimulusgeometrie und die Merlitz-Abbildung sind von der Versuchssteuerung und
der spaeteren Eye-Tracking-Anbindung getrennt.

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
- EditMode-Tests fuer Winkelgeometrie und Merlitz-Abbildung.

## Schnellstart

1. Das Projekt mit Unity 6.5 oeffnen. Zielversion ist
   konkret Editor `6000.5.6f1`.
2. `Assets/GlobeEffect/Demo/CheckerboardDemo.unity` oeffnen. Die Szene enthaelt
   bereits XR Origin, Main Camera und Checkerboard-Stimulus.
3. Unter `Edit > Project Settings > XR Plug-in Management` kontrollieren, dass
   im Standalone-Reiter `Initialize XR on Startup` und `Varjo` aktiv sind. Diese
   Zuordnung ist im Repository bereits gespeichert. Fuer den Laborbetrieb nicht
   gleichzeitig den generischen OpenXR-Loader aktivieren.
4. Am Objekt `Checkerboard Stimulus` die Parameter `Angular Diameter Degrees`,
   `Viewing Distance Meters`, `Merlitz K` und `Eye Presentation` einstellen.
5. Fuer einen weltfesten Trial `Follow Observer Every Frame` deaktiviert lassen
   und am Trial-Anfang `PlaceInFrontOfObserver()` aufrufen.

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
3. Fuer einen unabhaengigen Render-Test in der Hierarchy
   `XR Origin > Camera Offset > Main Camera > XR Render Probe` aktivieren. Der
   magentafarbene Wuerfel sitzt kamerafest einen halben Meter vor der Kamera
   und nutzt keinen Checkerboard-Code.
4. Ist der Wuerfel sichtbar, aber das Checkerboard nicht, liegt der Fehler im
   Stimulus-Material beziehungsweise dessen XR-Shaderpfad. Ist auch der Wuerfel
   unsichtbar, sind XR-Loader, Kamera, Render-Pipeline oder Varjo-Laufzeit zu
   pruefen.

Beim Start einer XR-Sitzung wird die Kamera erst im ersten Frame auf die reale
HMD-Pose gesetzt. Der Stimulus wartet deshalb mit seiner initialen Platzierung
bis `LateUpdate` und bleibt danach standardmaessig weltfest.

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

## Laufzeit- und Eye-Tracking-Anbindung

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
Durchmesser, `k`, `m`, Check-Anzahl und Augenmodus. Damit muss eine spaetere
Toolbox keine privaten Inspector-Felder auslesen.

## Annahmen und offene Validierung

- Der Stimulus ist eine frontoparallele **ebene** Flaeche, keine Kugel.
- Der Abstand wird vom Center-Eye-/Head-Transform zur Flaechenmitte gemessen.
  Wegen der IPD ist der exakte Winkel jedes einzelnen Auges bei einer gemeinsamen
  realen Ebene minimal verschieden; fuer eine per-Auge kalibrierte Flaeche waere
  spaeter eine separate Stereo-Geometrie noetig.
- Der monokulare Modus unterdrueckt ein Auge im Shader. Er ersetzt keine
  Messung von Display-Crosstalk und keine klinische Okklusion.
- Die XR-Augenauswahl benutzt Unitys `unity_StereoEyeIndex`. Links/rechts muss
  mit der konkreten Render-Pipeline, dem Headset und dessen Stereo-Modus im Labor
  geprueft werden. Im normalen Game-View entspricht die Vorschau dem linken Auge.
- Die kreisrunde Apertur und der festgehaltene scheinbare Winkeldurchmesser sind
  Festlegungen dieses Versuchsdesigns, keine Behauptung einer exakten
  Replikation des Oomes-Versuchsaufbaus.
- Fixation wird angezeigt, aber noch nicht per Eye Tracking erzwungen. Ein Trial
  sollte spaeter nur gewertet werden, wenn die definierte Fixations- und
  Datenqualitaetsregel erfuellt ist.
- Dieser Stand implementiert den statischen Checkerboard-Einstelltest. Eine
  kontrollierte Schwenk-/Globe-Effect-Animation ist ein separates Modul.

## Qualitaetssicherung

- Die EditMode-Tests pruefen die Vorwaerts-/Rueckabbildung fuer
  `k = 0, 0.5, 0.7, 1`, die Randnormierung und die exakte Verdopplung des
  physischen Durchmessers bei doppeltem Abstand.
- Der aktuelle Stand besteht alle 15 EditMode-Tests mit Unity `6000.5.6f1`.
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
- Unity, *Single-pass instanced rendering and custom shaders*:
  https://docs.unity3d.com/6000.0/Documentation/Manual/SinglePassInstancing.html

## Projektstruktur

```text
Assets/GlobeEffect/
  Demo/           startfertige Referenzszene
  Editor/         reproduzierbarer Aufbau der Referenzszene
  Runtime/
    Scripts/      reine Mathematik, Stimulussteuerung, Integrations-API
    Resources/    XR-faehiger analytischer Checkerboard-Shader
  Tests/EditMode/ Mathematik- und Skalierungstests
Assets/XR/        versionierte Varjo-/XR-Management-Einstellungen und Loader
Assets/XRI/       Einstellungen des XR Interaction Toolkit
Packages/         fest versionierte Unity-, Test- und XR-Abhaengigkeiten
ProjectSettings/  Unity-Projektversion
```
