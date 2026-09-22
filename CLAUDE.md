# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projektüberblick

Unity-Projekt (Editor-Version `6000.5.6f1`) für den VR-Versuchsaufbau einer
Masterarbeit zum **Globe Effect**. Es enthält zwei unabhängige Wahrnehmungstests
auf der Varjo XR-4:

1. **Statischer Checkerboard-Test** (`Assets/GlobeEffect/Demo/CheckerboardDemo.unity`) –
   orientiert sich an Helmholtz und Oomes et al. (2009). Ein kreisrundes,
   kopffestes Schachbrett wird kurz gezeigt, danach eine Noise-Maske; die
   Person antwortet konkav/konvex (intern `Category A`/`Category B`).
2. **Dynamischer Random-Dot-Test** (`Assets/GlobeEffect/Demo/RandomDotMotionDemo.unity`) –
   ein Punktfeld führt eine simulierte Schwenkbewegung (Yaw) aus; die Person
   beurteilt danach konkav/konvex.

Beide Tests sind eine methodische Grundlage, keine fertige Hauptstudie. Details
zur Aufgabe, Bedienung, den Inspector-Parametern und den gespeicherten
Messspalten stehen ausführlich in [`README.md`](README.md); der aktuelle
Arbeitsstand und offene Entscheidungen stehen in
[`AKTUELLER_STAND.md`](AKTUELLER_STAND.md). Beide Dateien vor größeren
Änderungen lesen – sie sind die primäre fachliche Dokumentation, dieses
CLAUDE.md beschreibt nur die Code-Architektur.

Zentrale Literatur (siehe README für vollständige Zitate): Helmholtz'
Checkerboard-Phänomen, Oomes et al. (2009) zur Helmholtz'schen Gitterverzerrung
und Merlitz (2010) zur Fernglas-Schwenkverzerrung (`k`, `m`, `l`).

## Befehle

Es handelt sich um ein reines Unity-Projekt ohne npm/Make-Tooling und ohne
CI-Konfiguration. Alle Aktionen laufen über den Unity Editor bzw. dessen
Kommandozeile.

**Projekt öffnen:** Unity Hub → Projekt `VRCheckerboard` mit Editor-Version
`6000.5.6f1` öffnen. Danach eine der beiden Demo-Szenen aus
`Assets/GlobeEffect/Demo/` laden.

**Play Mode testen (auch ohne Headset am Laptop möglich):** Play Mode starten,
`F5` bereitet die Sitzung vor, `SPACE` startet den ersten Trial. Beim
Checkerboard-Test vorher `T` für das Training. `F6` bricht ab, `C` kalibriert
das Eye Tracking. Für einen reinen Tastaturtest ohne echte Blickdaten muss am
`Checkerboard Experiment Manager` bzw. `Random Dot Experiment Manager`
`Require Fixation` vorübergehend deaktiviert werden (bei einer echten Messung
muss das wieder aktiv sein).

**EditMode-Tests ausführen:**
- Im Editor: `Window → General → Test Runner` → Reiter `EditMode` → `Run All`.
- Über die Kommandozeile (Batch-Modus):
  ```
  Unity.exe -batchmode -projectPath "D:\Tolga\Globe-Effect-Master\VRCheckerboard" -runTests -testPlatform EditMode -testResults results.xml -quit
  ```
- Ein einzelner Test lässt sich im Test Runner per Doppelklick gezielt
  ausführen, oder per CLI mit zusätzlichem `-testFilter <Namespace.Klasse.Methode>`.

Die Tests liegen unter `Assets/GlobeEffect/Tests/EditMode/` und prüfen reine
C#-Logik (Mapping-Formeln, Trialplaner, Warteschlangen) ohne Play-Mode-Setup.

**PSE-Auswertung:** Die MATLAB-Skripte unter
`analysis/checkerboard_pse_analysis/` (`fit_checkerboard_pse_palamedes.m`,
`fit_checkerboard_pse_psignifit.m`) passen eine psychometrische Funktion an
exportierte Trial-CSVs an und liegen außerhalb der Unity-Pipeline.

## Architektur

### Assembly-Aufteilung

Drei Assembly-Definitionen erzwingen eine klare Schichtung, Namespace jeweils
`GlobeEffect.VRCheckerboard.*`:

- `GlobeEffect.VRCheckerboard.Runtime` (`Assets/GlobeEffect/Runtime/`) – das
  eigentliche Experiment, referenziert Input System und Varjo XR.
- `GlobeEffect.VRCheckerboard.Editor` (`Assets/GlobeEffect/Editor/`) –
  referenziert Runtime, nur `Tools → Globe Effect → Open Experiment Monitor`.
- `GlobeEffect.VRCheckerboard.Tests` (`Assets/GlobeEffect/Tests/EditMode/`) –
  referenziert nur Runtime, reine EditMode-Tests.

### Ablaufkette pro Test (Checkerboard und Random-Dot spiegeln sich)

Beide Experimente folgen demselben Muster aus je fünf zusammenspielenden
Skripten in `Runtime/Experiment/`, `Runtime/EyeTracking/` und dem jeweiligen
Stimulus-Skript:

1. **`…ExperimentManager`** (`CheckerboardExperimentManager.cs` /
   `RandomDotExperimentManager.cs`) – zustandsbasierte Steuerung (State Machine
   über ein `SessionState`-Enum: Welcome, Training, Fixation, RunningTrial,
   WaitingForResponse, …). Reagiert auf Tastatureingaben, startet die Sitzung,
   holt Trials aus der Warteschlange und speichert Antworten.
2. **`…TrialPlanner`** (`CheckerboardTrialPlanner.cs` /
   `RandomDotTrialPlanner.cs`) – erzeugt aus den Inspector-Wertelisten
   (FOV, `l`, Content Zoom, Augenmodus, Wiederholungen) das vollständige,
   geseedete Kombinationskreuz und mischt die Reihenfolge reproduzierbar
   (`Random Seed`). Ungültige Trials werden hinten an dieselbe Warteschlange
   angehängt statt einfach übersprungen zu werden.
3. **Stimulus-Skript** (`VrCheckerboardStimulus.cs` /
   `RandomDotFieldStimulus.cs`) – trägt nur eine einfache Quad-/Feld-Mesh und
   reicht die aktuellen Parameter über ein `MaterialPropertyBlock` an den
   zugehörigen Shader weiter. Bleibt per `LateUpdate`/`Application.onBeforeRender`
   kopffest vor dem `observer` (HMD-Kamera), unabhängig von echter Tiefe.
4. **`…FixationMonitor`** (`CheckerboardFixationMonitor.cs` /
   `RandomDotFixationMonitor.cs`) – prüft während der Darbietung laufend, ob
   der Blick innerhalb der Toleranz bleibt (`Maximum Off Target Seconds`,
   `Maximum Invalid Gaze Seconds`); bei Verletzung markiert der
   ExperimentManager den Trial als ungültig und plant ihn erneut ein.
5. **`…ExperimentFiles`** (`CheckerboardExperimentFiles.cs` /
   `RandomDotExperimentFiles.cs`, gemeinsam genutzt:
   `ExperimentOutputPath.cs`) – schreibt `*_plan.csv` und `*_trials.csv` in
   einen automatisch angelegten Sitzungsordner unter `measurements/` (per
   `.gitignore` ausgeschlossen, siehe unten).

Eye-Tracking-Zugriff läuft für beide Tests über dieselbe Abstraktion in
`Runtime/EyeTracking/`: `IEyeTracker` (Interface) mit `VarjoEyeTracker.cs`
(echtes Headset) und `DummyEyeTracker.cs` (Tests/Laptop ohne Headset),
gebündelt in `EyeTrackingToolbox.cs`, die auch die Rohdaten-CSV der
Eye-Tracking-Toolbox aus dem Lab schreibt und um Stimulusmarker (`visual_space_l`,
FOV, Augenmodus, Gitterabstand, Blendenkante) ergänzt.

Die Tastatureingabe ist pro Test in einem eigenen Controller gekapselt
(`CheckerboardKeyboardController.cs`, `RandomDotKeyboardController.cs`), der
u. a. `Swap Response Keys` zur Gegenbalancierung zwischen Versuchspersonen
umsetzt.

### Mathematischer Kern

`VisualSpaceRadialMapping.cs` ist die **maßgebliche C#-Referenzimplementierung**
der radialen Visual-Space-Abbildung (Merlitz' `y_l(a) = tan(l·a)/l`, am
Blendenrand normiert). Sie wird sowohl vom Trialplaner/den Experiment-Skripten
zur Validierung (`ValidateParameters`, verhindert nicht-monotone
FOV/`l`-Kombinationen) als auch – als separat gepflegte, aber mathematisch
identische Implementierung – von den beiden Shadern pro Pixel ausgeführt.
Ändert sich die Formel, muss sie an **beiden** Stellen (C# und Shader)
synchron angepasst werden; die Versionskennung
`visual-space-l-tangent-normalized-cartesian-grid-v2` wird deshalb in jede
Mess- und Plandatei geschrieben.

`GlobeEffectCoordinateMapping2D.cs` und `MerlitzBinocularReferenceMath.cs`
sind **rein theoretische Vergleichsrechnungen** (lineare x/y- bzw.
u/v-Abbildungen, Merlitz' Instrumentengleichung `tan(k·a) = m·tan(k·A)`). Sie
sind bewusst noch nicht mit dem Trialablauf, den Shadern oder der
CSV-Auswertung verbunden – nicht mit den aktiven Stimulus-Skripten verwechseln.

### Shader (`Runtime/Resources/`)

`GlobeEffectHelmholtzCheckerboard.shader` und
`GlobeEffectVisualSpaceRandomDots.shader` berechnen das eigentliche Muster pro
Pixel als **Blickrichtung**, nicht als Punkt auf der nahen Trägerfläche: Beide
Augen erhalten dieselben Richtungsvektoren, wodurch der Stimulus ohne
Nahdisparität wie ein Objekt in virtueller Unendlichkeit wirkt (Details siehe
README, Abschnitt „Head-locked und ohne Nahdisparität"). Die Carrier-Quad/-Mesh
selbst ist nur ein technischer Träger; Kreisblende, weicher Rand, Gitter und
Noise entstehen vollständig im Shader über `MaterialPropertyBlock`-Properties
wie `_VisualSpaceL`, `_ApparentHalfAngleRad`, `_ApertureEdgeSoftnessRad`,
`_GridLineSpacingUv`, `_ObserverWorld*`.

### Editor-Tooling

`ExperimenterMonitorWindow.cs` (`Tools → Globe Effect → Open Experiment
Monitor`) ist ein reines Anzeigefenster für den Versuchsleiter-Kontrollmonitor
– es liest nur Laufzeitwerte der Manager/Monitore und darf niemals in
Trialplan, Antworten oder Stimulus eingreifen.

## Wichtige Konventionen

- **Deutsche Kommentare und Inspector-Tooltips**: Der gesamte Code ist auf
  Deutsch kommentiert, da das Projekt für eine deutschsprachige Masterarbeit
  entsteht. Neue Kommentare und Tooltips im selben Stil auf Deutsch verfassen.
- **`l` ist eine Versuchsbedingung, kein geschätzter Wert**: `l` wird im
  Inspector fest vorgegeben und nirgends aus Daten berechnet. Skripte, die `l`
  verwenden, dürfen es nicht heimlich anpassen oder aus anderen Werten
  ableiten.
- **`measurements/` ist git-ignoriert** (`.gitignore`: `[Mm]easurements/`) –
  Messdaten dürfen nicht versehentlich eingecheckt werden. Der `analysis/`-
  Ordner mit den MATLAB-Fit-Skripten ist dagegen versioniert.
- **Kopffeste Darstellung**: Stimulus-Transforms folgen dem `observer`
  (HMD-Kamera) über `LateUpdate` **und** `Application.onBeforeRender`, damit
  auch kurz vor dem Rendern noch die neueste Tracked-Pose-Aktualisierung
  einfließt. Diesen doppelten Update-Pfad beim Ändern der Pose-Logik erhalten.
