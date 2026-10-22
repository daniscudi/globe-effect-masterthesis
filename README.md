# Globe Effect

In diesem Unity-Projekt entsteht der Versuchsaufbau für meine Masterarbeit zum
Globe Effect. Der statische Checkerboard-Test orientiert sich an Helmholtz und
Oomes et al. (2009). Daneben gibt es einen getrennten Random-Dot-Test für die
Wahrnehmung während einer simulierten Schwenkbewegung.

Eine kurze Bedienungs- und Arbeitsübersicht steht in `AKTUELLER_STAND.md`.
Die aktuelle Pilot-Checkliste mit Inspector-Zuständigkeiten, vorgefundenen
Szeneneinstellungen und offenen Protokollentscheidungen steht in
[CHECKERBOARD_PILOT.md](CHECKERBOARD_PILOT.md).

## Checkerboard-Standard: Instrumentenabbildung, Standard m = 10 (22. Oktober 2026)

Die frühere Variante beruhte auf Merlitz' Visual-Space-Funktion mit zusätzlicher
Randnormierung. Die neue Standardvariante verwendet seine Instrumentenabbildung
bei m = 10 ohne zusätzliche Randnormierung.

* Gleichung: `tan(k·a) = m·tan(k·A)`, im Shader rückwärts
  `A = atan(tan(k·a)/m)/k`, bei k → 0 der Grenzwert `A = a/m`. Dieselbe Gleichung
  wie bei den Random Dots (dort vorwärts für die Punktmittelpunkte, hier rückwärts
  pro Bildpunkt). C#-Referenz: `MerlitzBinocularReferenceMath`,
  `CheckerboardInstrumentMapping.SourceRadius`; Shader: `GlobeEffectMerlitzInstrument.cginc`
  (`MerlitzObjectAngle`), Aufruf in `GlobeEffectHelmholtzCheckerboard.shader`.
* Quellgitter: in perspektivischen Bildkoordinaten `u = x/z`, `v = y/z`,
  `R = √(u²+v²)`, `a = atan(R)`, abgelesen bei `[m·tan(A)/R]·(u, v)`. Der Shader
  rechnet dasselbe in Einheiten des Blendenrands (`r = R/tan(FOV/2)`,
  `s = m·tan(A)/tan(FOV/2)`, Gitterweite `tan(Δ)/tan(FOV/2)`); das Verhältnis zur
  Gitterweite ist identisch. Es gibt keine Rand- und keine Zentrumskorrektur und keine
  nachträgliche Größenanpassung. Dass die lokale Vergrößerung in der Mitte bei jedem k
  gleich m ist, folgt allein aus der Gleichung; endlich große Kästchen in der Mitte
  sind nicht bei jedem k gleich groß. Am Rand sind je nach k mehr oder weniger Felder
  sichtbar (Szene: 7,5 bei k = 0,2, 8,6 bei k = 1,2). Das ist gewollt.
* Blende, FOV und weicher Rand werden in Bildkoordinaten ausgewertet und hängen nicht
  von k ab.
* m ist einstellbar (`Magnification (m)`, 1–20), Standard **10**
  (`CheckerboardInstrumentMapping.DefaultMagnificationM`). Trials, Übung und Beispiele
  verwenden das m des Versuchsplans. Ein in der Live-Vorschau verstelltes m wird beim
  Verlassen zurückgesetzt; nur „Use Preview Values For The Experiment“ übernimmt es.
* Wirkung von m (FOV 60°, Gitter 8°, Felder über den Durchmesser bei k = 0,2 / 1 / 1,2 / 2):
  m = 1: 8,22 / 8,22 / 8,22 / 8,22 (gerade für jedes k); m = 2: 7,65 / 8,22 / 8,50 / 10,61;
  m = 5: 7,51 / 8,22 / 8,60 / 11,98; m = 10: 7,49 / 8,22 / 8,61 / 12,23;
  m = 20: 7,48 / 8,22 / 8,61 / 12,30. Ab etwa m = 5 ändert sich kaum noch etwas: Für
  m → ∞ geht die Abbildung in s = tan(k·a)/(k·tan(FOV/2)) über, also Merlitz'
  Visual-Space-Funktion ohne Randnormierung. Nur zwischen m = 1 und etwa 5 ändert m
  die Verzeichnung deutlich.
* Ungültige Kombinationen (Tangens-Grenze aus FOV + weichem Rand und k, negative
  Werte ohne die Vorschau-Erweiterung) werden abgewiesen oder in der Vorschau als
  Fehler gemeldet und dann nicht gezeichnet. Sie werden nicht still auf einen anderen
  Wert gesetzt.
* Diese Abbildung ist die einzige für Training und Trials; es gibt keine Varianten-Auswahl
  im Inspector (siehe nächsten Abschnitt).
* Daten: Kennung unverändert `merlitz-instrument-checkerboard-fixed-source-grid-v2`
  (das Bild ist dasselbe wie vorher). Jede Zeile enthält außerdem das verwendete
  `instrument_magnification_m` (Standard 10), `grid_mapping = FixedSourceGrid`,
  `grid_source_scale = 1`, FOV, weichen Rand und Gitterabstand. Alte l-Daten haben
  eine eigene Kennung; die MATLAB-Auswertung poolt sie nicht.

Das ist keine vollständige Replikation von Merlitz' Online-Experiment. Ein PSE in k
beim Checkerboard und bei den Random Dots hat nicht automatisch dieselbe Bedeutung;
ob beide übereinstimmen, ist eine zu prüfende Vorhersage.

## Was für den Test verwendet wird (22. Oktober 2026)

Für den Checkerboard-Test wurden nicht verwendete Optionen entfernt oder ausgeblendet,
damit Inspector, Dateien und Beschreibung nur noch das zeigen, was wirklich benutzt wird:

* **Entfernt:** Ablauf B (graue Fläche beim Antworten, Noise danach). Es gibt nur noch
  einen Ablauf: Schachbrett → Noise-Maske beim Antworten → graue Fläche → nächstes
  Schachbrett. Die CSV-Spalten `trial_sequence`, `noise_until_response` und
  `post_response_noise_s` entfallen in neuen Dateien.
* **Entfernt:** die Noise-Typen „Fine Pixels“ und „One Spot Size“. Die Maske besteht
  immer aus feinen und groben Punkten (`Smallest Spots` bis `Largest Spots`).
* **Nur noch Live-Vorschau:** die eigene tanh-Erweiterung für stärkere
  Kissenverzeichnung (negative Werte, nicht Merlitz). Training und Trials nehmen nur
  Merlitz-k von 0 bis 2 an; die Erweiterungsspalten (`stimulus_distortion_parameter`,
  `mapping_family`, `extension_kappa`, `allow_pincushion_extension`) entfallen, und
  die MATLAB-Auswertung weist Dateien mit der Erweiterungskennung ab.
* **Ausgeblendet (Code bleibt):** das randnormierte Gitter
  (`S_norm(r) = S_fixed(r)/S_fixed(1)`, Kennung
  `merlitz-instrument-checkerboard-edge-normalized-v1`). Im Inspector gibt es keine
  Varianten-Auswahl mehr; Trials verwenden immer das feste Quellgitter. Ist die
  Option in einer alten Szene noch aktiv, zeigt der Inspector eine Warnung.
  `grid_mapping`, `grid_source_scale` und `effective_source_grid_spacing_uv` stehen
  weiter in den Dateien (bei der Standardabbildung `FixedSourceGrid`, 1, Referenzabstand).
* **Unsichtbar behalten:** die alte l-Abbildung, nur damit Yannicks Pilotdaten
  auswertbar bleiben.

## Umstellung der Checkerboards am 18. Oktober 2026

Checkerboard und Random Dots verwenden jetzt dieselbe **Instrumentenabbildung**
nach Merlitz (2010), Gleichungen 4–6, mit dem Stimulusparameter **k** und
Vergrößerung **m**. Beim Checkerboard gilt ein m pro Sitzung (Standard 10);
bei den Dots ist m ein Trialfaktor. Der frühere Regler `visualSpaceL` ist
nicht mehr die aktive Checkerboard-Abbildung.

**Yannicks Pilot (`pilot_ys`, 105 Trials, alter l-PSE ca. 0,872;
95%-Glaubwürdigkeitsintervall ca. 0,810–0,937) gehört zur ALTEN Abbildung.**
Er bleibt eine technische Voruntersuchung, kein Pilot der neuen k-Version.
Alte CSVs, Abbildungen und Schätzwerte bleiben unverändert; l nicht in k umbenennen
und alte/neue Messungen nicht poolen. Vor Beginn der Hauptstudie sind **neue
Pilotmessungen mit der Instrumentenversion** und die Abnahme am Headset vorgesehen.
Die früheren Szenenwerte werden über Feldmigration als numerische Startwerte
erhalten; daraus folgt keine Identität alter und neuer Reize.

Kennung am 18. Oktober: `merlitz-instrument-checkerboard-fixed-source-grid-v1`,
seit 19. Oktober `…-v2` (Polaritätsoption, siehe nächster Abschnitt).
Neue Plan-/Trialdateien enthalten `stimulus_task=Checkerboard`,
`instrument_distortion_k`, `instrument_magnification_m` und `mapping_version`.
Die Felder `visual_space_l` und `oomes_endpoint_equivalent` werden für neue
Checkerboard-Sitzungen nicht mehr ausgegeben.

**Automatisch geprüft am 18. Oktober:** 288/288 Unity-Tests bestanden, kein
übersprungener Test. Darunter gerenderte Checkerboards für k = 0 / 0,5 / 1 /
1,4 / 2, Vorwärts-/Rückrechnung gegen dieselbe Instrumentengleichung,
Vorschau-Rücksetzung von k und m, Trialabläufe und CSVs. Testbericht:
`Logs/checkerboard_instrument_final.xml` (lokal, von Git ignoriert).
MATLAB wurde mit ausschließlich synthetischen Daten für neue Checkerboard-k-,
historische l- und Random-Dot-k-CSVs sowie gegen gemischte Mappingkennungen geprüft.
Diese Software-/GPU-Prüfung ist kein Headset-Abnahmetest und keine Pilotmessung.

## Ergänzung 19. Oktober 2026: Polarität, festes Training, Übungsanzeige

**Neue Kennung `merlitz-instrument-checkerboard-fixed-source-grid-v2`.** Die
Abbildung (k, m, festes Quellgitter) ist unverändert. Neu ist nur die Option
`Invert Checkerboard Each Trial` (Standard aus). Ist sie aus, zeichnet v2 Pixel
für Pixel dasselbe Bild wie v1; das prüft ein Bildtest gegen eine eingefrorene
Kopie des v1-Shaders (`Tests/EditMode/CheckerboardShaderV1Reference.txt`) für
k = 0 bis 2, Noise-Maske und graue Fläche.

**Polarität.** Vorschlag des Betreuers, um Nachbildeffekte zu verringern; ob es
Teilnehmer irritiert, ist offen.

* Mit der Option tauschen schwarze und weiße Felder von einem gezeigten Trial
  zum nächsten. Der erste Trial ist normal. Wiederholte (ungültige) Trials
  zählen als Darbietung mit, die Polarität hängt deshalb an der Darbietung,
  nicht am Plan.
* Nur das Schachbrett wird invertiert, nicht die Noise-Maske (gleicher Seed,
  gleiches Rauschbild).
* Über viele Trials sieht so jede Stelle im Mittel Grau; es baut sich kein
  Nachbild einer bestimmten Polarität auf. Das negative Nachbild von Trial n
  hat allerdings dieselbe Polarität wie Trial n+1.
* `*_trials.csv` hat die neue letzte Spalte `checkerboard_inverted` (0/1),
  `*_plan.csv` die Spalte `invert_checkerboard_each_trial`; der Marker
  `TrialStart` enthält `checkerboard_inverted`, beide Marker `invert_each_trial`.
  Damit lässt sich später auswerten, ob die Polarität die Antworten beeinflusst
  (zum Beispiel getrennte Fits je Polarität pro k). Die MATLAB-Fits ignorieren
  die Spalte bisher.
* Training: Die beiden Beispiele haben immer dieselbe, normale Polarität (sonst
  würde die Polarität die Kategorie verraten). In den Übungstrials wechselt die
  Polarität nach dem Muster N, I, I, N. Weil die Übungs-k selbst niedrig/hoch
  abwechseln, würde ein Wechsel bei jedem Trial „invertiert“ genau mit „hoch“
  zusammenfallen lassen; so kommt jede Kombination gleich oft vor.
* Vorschau: `Swap Black And White` am Stimulus zeigt das invertierte Muster.
  Bei eingeschalteter Option wechselt jeder Druck auf `M` (Nachbildtest) die
  Polarität. Nach Vorschau, Training und Sitzung ist das Muster wieder normal.

**Training in fester Reihenfolge.** Statt der gemischten Liste gibt es zwei
Teile, jeweils genau in der eingetragenen Reihenfolge, niedrig und hoch im
Wechsel. Die zwei Beispiele kommen weiterhin zuerst. Vor jedem Teil steht ein
neutraler Text („PRACTICE PART 1/2“); dort kann der Versuchsleiter nach einem
Blick auf den Monitor noch einmal erklären. Standardwerte (im Inspector änderbar):

* Teil 1 (leicht): `0, 1.4, 0.2, 1.2, 0, 1.4, 0.2, 1.2, 0, 1.4, 0.2, 1.2`
* Teil 2 (schwerer): `0.4, 1.0, 0.5, 1.2, 0.6, 0.9, 0.4, 1.1, 0.5, 1.0, 0.6, 1.2`
  (0.7 und 0.8 sind absichtlich nicht dabei).

Die alten Felder `Training K Values` und `Training Repetitions Per Value`
(gemischt) sind entfallen; ihre Werte in alten Szenen werden nicht übernommen.

**Übungsantworten nur im Experiment Monitor.** Checkerboard und Random Dots
zeigen im Abschnitt „Practice Answers (Only You See This)“ live jede
Übungsantwort: Nummer, Teil, k, Antwort, Antwortzeit ab Beginn der Antwortphase,
„Closer To“ und eine Notiz (z. B. „inverted“, „no answer“, bei Active Practice 2
„good“/„stopped“), darunter eine Zählung pro k. Die Liste beginnt bei jedem
Übungsstart leer. Es entsteht **keine Datei**; die Versuchsperson sieht nichts.

* „Closer To“ sagt nur, welchem Beispiel k näher liegt (Convex/Concave), bei
  gleichem Abstand bleibt das Feld leer. Es ist **kein** Richtig/Falsch, weil
  sich die Wahrnehmung zwischen Personen unterscheidet.
* Checkerboard: nur in Teil 1 gefüllt, nach `Convex/Concave Example (k)`; in
  Teil 2 immer leer.
* Random Dots: bei allen Übungstrials gefüllt, nach `Convex/Concave Example (k)`
  der Random-Dot-Übung (die Übungslisten dort enthalten nur deutliche k).
* Zweck: Kommt zum Beispiel bei k = 1.4 durchgehend „Concave“, hat die Person
  die Kategorien wahrscheinlich vertauscht verstanden, und man erklärt sie neu.

**Automatisch geprüft am 19. Oktober:** 304/304 Unity-Tests bestanden, keiner
übersprungen (die bisherigen 288 und 16 neue: v1/v2-Pixelgleichheit und
Gegenprobe des invertierten Musters auf der GPU, Polaritätsregeln, Reihenfolge
und Polarität im Training als Play-Mode-Lauf, Polarität in der Trial-CSV
einschließlich Wiederholung, Live-Liste für Checkerboard und beide
Random-Dot-Übungen, keine Übungsdatei). Testbericht:
`Logs/polarity_practice_tests_2.xml` (lokal, von Git ignoriert). Kein
Headset-Abnahmetest.

## Was der Checkerboard-Test macht

Die Versuchsperson fixiert zuerst das rote Kreuz und sieht danach kurz ein
kreisrundes Schachbrett mit einem festen Verzerrungswert. Direkt danach beginnt
die Antwortphase. Dabei ist eine flimmernde Schwarz-Weiß-Noise-Maske mit
Fixationskreuz zu sehen. Nach der Antwort folgt die graue Fläche mit Kreuz als
kurze Phase vor dem nächsten Schachbrett. Im Hauptversuch wird kein zusätzlicher
Antworttext mehr eingeblendet, damit er nicht vom Reiz oder von der Fixation
ablenkt.

Die Tastenbelegung steht auf dem Welcome Screen, im Training und vor dem
Hauptversuch im Headset. Sie richtet sich nach der im Inspector gewählten
Zuordnung, zum Beispiel:

* `TRIGGER = OUTWARD / GLOBE`

* `TRACKPAD CLICK = INWARD / DENT`

Auf der Tastatur gelten zusätzlich Pfeil hoch (Category A) und Pfeil runter
(Category B).

Category A wird dort als nach außen gewölbt und Category B als nach innen
gewölbt beschrieben. Dazu werden zuerst zwei deutliche Beispiele gezeigt.
Anschließend folgen zwei Übungsteile in fester Reihenfolge, erst deutlich
verschiedene, dann mittlere `k`-Werte. Intern bleiben die Antworten als `Convex` und `Concave` gespeichert,
damit die bestehende Auswertung weiter funktioniert.

Aus mehreren Antworten pro Verzerrungsstufe kann später der Wert geschätzt
werden, bei dem beide Antworten gleich häufig vorkommen. Das ist der Punkt, an
dem das Muster subjektiv geradlinig erscheint (PSE).

Der Test kann beidäugig, nur links oder nur rechts gezeigt werden. Die
gewünschten Bedingungen werden am `Checkerboard Experiment Manager` im Inspector
eingestellt.

## Was der Random-Dot-Test macht

Die Versuchsperson fixiert ein rotes Kreuz in der Mitte einer runden Öffnung.
Schwarze und weiße Punkte bewegen sich dahinter im simulierten Trial einmal
in eine Richtung. Die Öffnung und das Fixationskreuz bleiben dabei
kopffest. Eine tatsächliche Kopfbewegung ist für die Hauptbedingung nicht nötig.

Die aktuelle Instrumentenfassung setzt vor jedem Durchgang eine feste
Fernglasvergrößerung `m` und eine feste Instrumentenverzeichnung `k`. Beide Werte
werden nicht angezeigt und können von der Versuchsperson nicht verändert werden.
Nach der festgelegten Bewegungsdauer verschwindet das Punktfeld und es folgt
wieder nur die Entscheidung:

* Pfeil links: Bewegung wirkt konkav.

* Pfeil rechts: Bewegung wirkt konvex.

Im Headset antwortet die Person stattdessen mit Trigger und Trackpad-Klick am
VR-Controller. Welche Taste welche Antwort gibt, liest sie nach `F5` vor dem
ersten Durchgang im Headset; ein weiteres `F5` startet dann den ersten Trial.

Aus `P(konvex | k, m)` kann später für jede Vergrößerung der Übergang bestimmt
werden, an dem konkav und konvex gleich häufig geantwortet werden. Dieser
dynamische Neutralpunkt kann mit dem im statischen Checkerboard geschätzten
statischen k-PSE bei gleicher Vergrößerung verglichen werden.

## Gemeinsame Instrumentenskala k und m

Beide Aufgaben benutzen Merlitz' Instrumentengleichung:

```text
tan(k · a) = m · tan(k · A)
a = atan(m · tan(k · A)) / k
k = 0: a = m · A
```

A ist der wahre Objektwinkel, a der scheinbare Winkel im Instrument.
k = 1 ist die Tangentenbedingung (gerade Abbildung), k = 0,5 die Kreisbedingung,
k = 0 die Winkelbedingung; Werte über 1 setzen dieselbe Formel in die
Tonnenrichtung fort. Der Bereich 0–2 ist keine Erweiterung zu stärkerer
Kissenverzeichnung unter null. Negative Werte sind nicht zugelassen.

Der Checkerboard-Manager hat ein einstellbares `Magnification (m)`, Standard 10.
Dieses m erzeugt ein fertiges statisches Muster, keine Zoomanimation.
Bei m = 1 ist die reine Instrumentenabbildung für jedes k identisch:
Es entsteht dann keine k-abhängige Gitterkrümmung.
Für den ersten direkten Vergleich ist m = 10 in beiden Aufgaben vorgesehen;
eine abweichende Vergrößerung muss als eigene Bedingung festgelegt werden.

Merlitz' persönliches **l** ist dagegen ein theoretischer Wahrnehmungsparameter.
Er wird nicht auf das dargestellte Bild zusätzlich angewandt. Die Vorhersage,
dass statischer und dynamischer k-PSE näherungsweise übereinstimmen, wird durch
die gemeinsame Instrumentenabbildung nicht vorweggenommen. Die gemeinsame Skala
ist keine exakte Wiederholung seiner Versuchssituation: FOV, VR, Darbietung,
Fixationskontrolle und Antwortverfahren müssen gesondert berichtet werden.

## Die Instrumentenabbildung im Checkerboard-Shader

Der Shader rechnet von jedem dargestellten Bildpixel zurück auf das gerade
Ausgangsgitter. Mit r als normiertem Bildradius und β = FOV/2:

```text
a = atan(r · tan(β))
A = atan(tan(k · a) / m) / k
k = 0: A = a / m
s = m · tan(A) / tan(β)
```

Azimut bleibt erhalten. An der Quellposition s wird das regelmäßige
kartesische Schwarz-Weiß-Gitter abgelesen. `m*tan(A)` bezeichnet die gerade
Referenzprojektion (k = 1). Deshalb ist das Bild bei k = 1 für jedes m gerade.

**Keine k-abhängige Randnormierung:** Das gerade Ausgangsgitter bleibt bei
wechselndem k fest. Die unabhängige Kreisblende schneidet das optisch verzeichnete
Bild aus. Es wird nicht nach jedem k zusätzlich skaliert, um denselben
Ausgangsgitterpunkt an den Blendenrand zu setzen. Die Zahl und Winkelbreite der
sichtbaren Randfelder dürfen sich dadurch ändern.
Der Rasterabstand wird im unverzeichneten Referenzbild festgelegt; auf der
gedachten Ausgangsebene entspricht er `tan(Grid Spacing)/m`.

Vor dem Start prüfen Planer/Manager k, m und den eindeutigen Winkelbereich,
einschließlich Softness und gegebenenfalls quadratischer Ecken.
Ein großer k-Wert und ein großes FOV können zusammen unzulässig sein
(z. B. k = 2 und FOV = 90°). Die Vorschau zeichnet außerhalb des gültigen
Instrumentenbereichs Hintergrund statt umgeschlagener Linien.

### Historische l-Abbildung — nur alte Pilotdaten

Bis zur Umstellung lautete die Rückabbildung:

```text
s_l(r) = tan(l · a) / tan(l · β)
l = 0: s_l(r) = a / β
```

Die alte Klasse `VisualSpaceRadialMapping` und ihre Tests bleiben als
Rechenreferenz für historische Daten erhalten. Ihre Abbildungsfunktion wird vom
aktuellen Checkerboard-Shader nicht mehr benutzt. Es gibt keine automatische
Umdeutung der alten PSEs in Instrument-k.

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
Instrumentenabbildung mit k und m angewendet.

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

* `0`: harter, klar abgeschnittener Rand bei FOV/2

* positiver Wert: Breite des weichen Übergangs, **mittig auf dem FOV-Rand**
  (seit 13.10.2026; voreingestellt in beiden Szenen `5°`)

Die Angabe erfolgt in Winkelgrad und nicht in Pixeln. Dadurch bleibt die
Randbreite auch bei anderer Auflösung oder auf der XR-4 vergleichbar. 

Genau (beide Shader): θ ist der dargestellte Sehwinkel eines Bildpunkts zur
Bildmitte des jeweiligen Auges (bei Random Dots nach der Instrumentenabbildung
mit m und k). θ_A = FOV/2, s = `Edge Softness Degrees`. Die Deckkraft ist

```text
α(θ) = 1 − smoothstep(θ_A − s/2, θ_A + s/2, θ),   smoothstep(e0, e1, x) = t²(3 − 2t),
t = clamp((x − e0) / (e1 − e0), 0, 1)
```

Bis `θ_A − s/2` ist alles voll sichtbar, zwischen `θ_A − s/2` und `θ_A + s/2`
wird S-förmig zur Außenfarbe übergeblendet, genau bei `θ_A` beträgt die
Deckkraft 50 %. **Das FOV ist damit der Halbwertsdurchmesser der Blende.**
Beispiel FOV 60°, s = 5°: voll sichtbar bis 55° Durchmesser (27,5°
Exzentrizität), 50 % bei 60°, ganz weg ab 65°. Bei s = 0 gibt es eine harte
Kante bei θ_A. Für die Arbeit z. B.: „kreisförmige Blende mit 60°
Durchmesser (Halbwertspunkt) und weichem Rand von 5° Breite (smoothstep)“.
Die gezeichneten Flächen und die Random-Dot-Punktwelt reichen bis `θ_A + s/2`.
Messdateien seit dieser Änderung tragen eine neue `mapping_version`
(`…-aperture-halfmax-…`); ältere Dateien hatten den Übergang ganz innerhalb
des FOV (`θ_A − s` bis `θ_A`).

Gemischt wird unterschiedlich:

* Random Dots: Punkte und Kreisfläche (`Field Background Color`) werden mit
  demselben α über die Kamera-Löschfarbe (`Background Color`) geblendet,
  linear in den gamma-kodierten Farbwerten (Projekt im Gamma-Farbraum). Die
  Kreisfläche wird als Gleitkommafarbe an den Shader übergeben, damit gleiche
  Farben innen und außen exakt gleich gerundet werden (früher
  8-Bit-Vertexfarbe: 0,5 ergab innen 128, außen 127, der Kreis war minimal
  sichtbar).
* Checkerboard: Muster und grauer Hintergrund werden in **linearer
  Helligkeit** gemischt (Gamma → linear → mischen → Gamma), damit die mittlere
  Helligkeit am Rand gleich bleibt und kein Kugel-Schatten „konvex“ vortäuscht.

Am `Checkerboard Stimulus` kann `Use Circular Aperture` für eine technische
Kontrolle ausgeschaltet werden. Dann sieht man das vollständige quadratische
Gitter. Für den eigentlichen Versuch bleibt der Haken eingeschaltet. Der
verwendete Zustand wird in der Trialdatei und in den Eye-Tracking-Markern
mitgespeichert.

## Verhältnis zur α-Skala von Oomes

Oomes' α hat andere Definitionen: α = 0 bezeichnet ein gerades Gitter,
α = 1 das Helmholtz-Muster, negative Werte die Tonnenrichtung.
Aus solchen Bezugspunkten folgt keine allgemeine Umrechnung in unser k.
Die frühere Hilfsspalte `oomes_endpoint_equivalent = 2*(1-l)` belegt weder
eine Gleichheit der Musterfamilien noch eine Gleichheit ihrer Wertebereiche.
Sie wird für neue Messungen nicht mehr ausgegeben.
Oomes ist ein methodischer/theoretischer Bezug, keine behauptete exakte
α-Replikation.

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

Die Texte im Headset (Anleitungen, Übungen, Pausen) sind normale Unity-Texte.
Damit sie auf derselben Tiefe liegen wie das Kreuz, stehen sie in beiden Tests
kurz vor der Fernebene der Kopfkamera (90 % von `far clip plane`; die Kameras in
beiden Szenen stehen dafür auf 1000 m, die Texte also bei 900 m) und sind im
selben Verhältnis vergrößert; Layout und Winkelgröße bleiben gleich. Die
Restdisparität (rund 0,25 Bogenminuten) ist auch bei der Varjo XR-4 kleiner als
ein Pixel, die Augen müssen beim Wechsel zwischen Text und Kreuz also nicht neu
einstellen (Wunsch des Betreuers, 2026-10-18). Alle Reiz-Shader schreiben keine
Tiefe, die größere Fernebene ändert an ihnen nichts. Vorher standen die
Texte 1,9 m (Random Dots) bzw. 9 m (Checkerboard) entfernt. Beim Checkerboard
legt `Text Distance Meters` nur noch die Schriftgröße fest ("so groß wie in
dieser Entfernung").

## Ablauf eines Durchgangs

Ablauf: Checkerboard → Noise-Maske mit Antwort → graue Fläche → nächstes
Checkerboard. (Der frühere Ablauf B mit grauer Fläche beim Antworten wurde am
22. Oktober 2026 entfernt.)

Die Schritte im Einzelnen:

1. Vor dem Checkerboard steht die Phase vor dem Stimulus: die graue Fläche mit
   Fixationskreuz. Sie dauert
   mindestens `Pre Stimulus Seconds` (voreingestellt 0,5 s). Mit
   Fixationskontrolle geht es erst weiter, wenn zusätzlich der Blick ruhig auf
   dem Kreuz liegt.
2. Das Checkerboard wird für die eingestellte Zeit eingeblendet. Der Startwert
   beträgt 600 ms.
3. Direkt danach beginnt die Antwortphase mit der Noise-Maske und dem
   Fixationskreuz.
4. Die im Training gelernte Antwort wird ohne erneuten Hinweistext gegeben. Die
   Antwortzeit begrenzt `Response Timeout Seconds`; `0` bedeutet ohne Zeitlimit.
5. Antwort, Reaktionszeit, k, m, FOV, Augenmodus und Fixationswerte werden sofort
   gespeichert. Pro Durchgang zählt nur die erste Antwort; weitere Tastendrücke
   werden bis zur nächsten Antwortphase ignoriert.
6. Nach der Antwort beginnt sofort die Phase vor dem nächsten Stimulus mit der
   grauen Fläche. Ein schwarzer Zwischenbildschirm wird vermieden.

Die Noise-Maske soll ein Nachbild des Schachbretts verhindern. Sie erscheint in
derselben runden Öffnung wie das Checkerboard, schwarz/weiß und mit derselben
mittleren Helligkeit, und wird mit `Noise Refresh Rate Hz` neu ausgewürfelt
(`0` lässt das Bild stehen). Die Maske besteht aus feinen und groben Punkten:
Zufallsmuster in mehreren Zellgrößen von `Smallest Spots` (0,5°) bis
`Largest Spots` (8°), je Faktor 2 (also 0,5°, 1°, 2°, 4°, 8°), gleich gewichtet
addiert und dann schwarz/weiß geschaltet. Jede Oktave erhält so etwa gleich viel
Energie (1/f-Rauschen), von der Grundfrequenz des Schachbretts bis zu feinen Kanten.
(Die früheren Typen „Fine Pixels“ und „One Spot Size“ wurden am 22. Oktober 2026
entfernt.)

Begründung: Eine Maske wirkt nur in Größenordnungen nahe ihrer eigenen, etwa
± 1 Oktave (Stromeyer, C. F. & Julesz, B. (1972). Spatial-frequency masking in
vision: Critical bands and spread of masking. *Journal of the Optical Society
of America, 62*(10), 1221–1232). Das Schachbrett mit 10°-Feldern hat seine
Grundfrequenz bei etwa 0,07 Zyklen pro Grad, feines Rauschen mit 0,1°-Punkten
liegt bei 3–5 Zyklen pro Grad, rund 6 Oktaven entfernt, und maskiert das
Schachbrett deshalb kaum. Zu grob (Zellen so groß wie die Felder) wäre die Maske
selbst ein grobes Muster mit eigenen Kanten; deshalb endet die gröbste Oktave
bei 8°. Der Marker `SessionStart` enthält `noise_broadband_deg` und `noise_octaves`.

Vergleich im Headset: In der Checkerboard-Vorschau (P) spielt `M` bzw. der Knopf
„Play Afterimage Test“ Schachbrett (2 s) → eingestellte Maske (1 s) → graue
Fläche (3 s) ab. Bleibt auf der grauen Fläche ein Nachbild des Schachbretts,
maskiert die Maske zu schwach. Andere Punktgrößen einstellen und wiederholen. Mit
`Invert Checkerboard Each Trial` wechselt jeder Druck auf `M` die Polarität.

## Welcome Screen und Training

Nach dem Start des Play Modes erscheint zunächst ein englischer Welcome Screen:

* `T`: Training starten

* `F5`: Hauptversuch starten

* `F6`: laufenden Versuch oder laufendes Training abbrechen

Das Training zeigt zunächst je ein deutliches Beispiel für Category A und B.
Danach folgen Teil 1 (12 leichte Trials) und Teil 2 (12 schwierigere Trials),
jeweils in der eingetragenen Reihenfolge, nicht gemischt (Werte siehe
„Ergänzung 19. Oktober 2026“). Es gibt bewusst keine Richtig-/Falsch-Rückmeldung.
Nach dem Training geht es zurück zum Welcome Screen.
Solange `Require Training Before Session` aktiv ist, startet `F5` erst nach einem
vollständig durchlaufenen Training. Trainingsdurchgänge werden nicht in den
Messdateien gespeichert; k, Antwort und Antwortzeit erscheinen nur live im
Experiment Monitor.

Wenn die Fixation während der Darbietung zu lange verloren geht:

1. Das Muster wird ausgeblendet.
2. Die Präsentation wird als ungültig gespeichert und nicht ausgewertet.
3. Beim Checkerboard kommt dieselbe Bedingung ans Ende der Trial-Liste;
   bei Random Dots ans Ende des aktuellen Bewegungsblocks.
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

* Trigger und Trackpad-Klick am VR-Controller: die beiden Antworten, in beiden
  Tests

* `C`: Eye-Tracking-Kalibrierung der vorhandenen Toolbox

Beim Random-Dot-Test werden Antworten erst angenommen, nachdem die
Bewegungsphase beendet und das Punktfeld ausgeblendet wurde.

Für die VR-Controller gibt es in beiden Tests dieselbe Auswahl:

* Trackpad Click = Concave, Trigger = Convex

* Trackpad Click = Convex, Trigger = Concave

Beim Checkerboard steht sie am `Checkerboard Experiment Manager`
(`Controller Mapping`), beim Random-Dot-Test am `Random Dot Keyboard Controller`
(`Vr Controller Mapping`). Gemeint ist ein Klick auf das Trackpad; eine bloße
Berührung zählt nicht, und Halten oder Loslassen löst keine weitere Antwort aus.
Mit der Auswahl lässt sich die Zuordnung zwischen Versuchspersonen
ausbalancieren. Innerhalb einer Person bleibt sie für Training und Hauptversuch
fest. Die Anzeigen im Headset und der Experimenter Monitor zeigen automatisch
die gerade gültige Belegung. Sie steht zusätzlich im Trialplan und in den
Eye-Tracking-Markern. Die Tastatur funktioniert daneben weiter;
`Swap Response Keys` vertauscht nur die beiden Tasten der Tastatur.

## Einstellungen im Inspector

Die wichtigsten Einstellungen befinden sich am Objekt
`Checkerboard Experiment Manager`:

* `Angular Diameters Degrees`: ein oder mehrere FOV-/Winkeldurchmesser

* `Eye Presentations`: Both Eyes, Left Eye Only oder Right Eye Only

* `Instrument Distortion K Values`: alle zu präsentierenden Instrumenten-k-Werte

* `Magnification (m)`: Instrumentenvergrößerung des Checkerboards; Standard 10

* `Repetitions Per Condition`: Wiederholungen jeder Kombination

* `Require Fixation`: Vorfixation und Kontrolle während des Musters

* `Maximum Off Target Seconds`: erlaubte zusammenhängende Blickabweichung

* `Maximum Invalid Gaze Seconds`: erlaubte Dauer fehlender oder ungültiger Daten

* `Maximum Attempts Per Trial`: 0 für unbegrenzte Wiedervorlage

* `Stimulus Duration Seconds`: Dauer des Checkerboards; voreingestellt auf 0,6 s

* `Pre Stimulus Seconds`: Mindestdauer der Phase vor dem nächsten Checkerboard;
  voreingestellt auf 0,5 s

* `Response Timeout Seconds`: maximale Antwortzeit; 0 bedeutet ohne Zeitlimit

* `Convex Response Key` und `Concave Response Key`: Tasten der beiden Antworten

* `Controller Mapping`: welche Controller-Taste welche Antwort gibt

* `Require Training Before Session`: verlangt ein abgeschlossenes Training vor F5

* `Convex / Concave Example (k)`: deutliche, noch zu pilotierende Beispiele

* `Practice Part 1: k Order (Easy)` und `Practice Part 2: k Order (Harder)`:
  k-Werte der beiden Übungsteile, genau in dieser Reihenfolge

* `Invert Checkerboard Each Trial`: schwarze und weiße Felder tauschen von Trial
  zu Trial (Standard aus)

* `Random Seed`: macht die zufällige Reihenfolge reproduzierbar

Die aktuell eingetragenen k-Werte und drei Wiederholungen sind nur für einen
technischen Pilotlauf gedacht. Sie sind noch keine festgelegten Bedingungen der
Masterarbeit. Die Listen können im Inspector vollständig geändert werden.

Am Objekt `Checkerboard Stimulus` werden Aussehen, Felderzahl, Farben, Größe des
Fixationskreuzes, die weiche Blendenkante sowie Punktgröße und
Aktualisierungsrate der Noise-Maske eingestellt. Während einer Sitzung
setzt der Experiment Manager FOV, k, festes m und Augenmodus automatisch.

Für den Bewegungstest liegen die wichtigsten Einstellungen am Objekt
`Random Dot Experiment Manager`:

* `Instrument Distortion K Values`: die zu präsentierenden
  Instrumentenverzeichnungen; geometrisch dieselbe Kandidatenfamilie wie beim
  Checkerboard

* `Instrument Magnification M Values`: eine oder mehrere
  Fernglasvergrößerungen zwischen `1` und `20`; voreingestellt ist `10`

* `Content Zoom Values`: optionaler Nach-Zoom; für die Instrumentensimulation
  normalerweise `1`

* k-Werte und Wiederholungen (Inspector-Tabelle „k-Werte und Wiederholungen“,
  Felder `Instrument Distortion K Values` und `Repeats Per K Value`): jedes k hat
  eine eigene Wiederholungszahl. Pilotdesign: k 0 und 0,2 je 8-mal, 0,6 bis 1,4
  in 0,1-Schritten je 25-mal, 1,8 und 2,0 je 8-mal, zusammen 257 Trials pro
  Bewegungsart. k darf 0 bis 2 sein, solange k · FOV/2 unter 90° bleibt (bei
  FOV 70 also bis 2; bei FOV 90 startet k = 2 nicht). Checkerboard analog mit
  `Repeats Per L Value`: l 0,2 16-mal, 0,3 bis 1,1 je 25-mal, 1,2 8-mal = 249.

* `Repeats Per Condition`: gilt für jedes k bzw. l ohne eigenen Eintrag und für
  neu hinzugefügte Zeilen.

* `Blocks Per Session` (beide Tests, voreingestellt 4): Viertelblöcke. Nach 25,
  50 und 75 % der geplanten gültigen Trials (bei 257 nach 64, 128, 193; bei 249
  nach 62, 124, 187) erscheint „SHORT BREAK“; rotes Kreuz und graue Fläche
  bleiben sichtbar, Antworten zählen nicht. Weiter erst, wenn der Versuchsleiter
  `F5` drückt. Wiederholte ungültige Trials zählen nicht mit; am Ende keine
  Pause. Marker `BlockPauseStarted`/`BlockPauseEnded`. `1` = keine Pausen.
  `mini_block_index` in der CSV bleibt aus Kompatibilitätsgründen `1`.

* `Simulated Sweep Seconds`: wie lange die Punkte zu sehen sind, in Simulated
  und Active; voreingestellt `0,8 s`. Im simulierten Block schwenkt das Feld in
  dieser Zeit einmal in eine Richtung und kehrt nicht um.

* `Simulated Sweep Axis`: `Horizontal` (Standard, links/rechts) oder
  `Vertical` (oben/unten) für den simulierten Block. Die Achse und die Richtung
  (`Right`/`Left` bzw. `Up`/`Down`) werden in Plan, Ergebnissen und
  Trial-Markern gespeichert. Bei aktiven Trials steht im Plan nur eine
  Platzhalter-Richtung; die tatsächliche steht in `head_direction`. Die Auswahl
  erzeugt keine zusätzlichen Trials; für einen Vergleich beider Achsen sind
  getrennte Sitzungen nötig.

* `Simulated Image Center Speed`: sichtbare Winkelgeschwindigkeit der Punkte in
  der Bildmitte, bei jedem `m` gleich (Code-Standard `12°/s`, in der Szene nach
  dem Pilot `25°/s`). Siehe Abschnitt „Geschwindigkeit des simulierten Schwenks“.

* Der simulierte Schwenk geht nur in eine Richtung und hat eine feste
  Geschwindigkeit. Bei `10x` läuft er mit `1,2°/s` und `0,8 s` von `-0,48°`
  nach `+0,48°`, die Mitte der Bewegung liegt also genau geradeaus. Ob nach
  links oder rechts, wechselt von Trial zu Trial in zufälliger Reihenfolge;
  beide Richtungen kommen bei gerader Wiederholungszahl gleich oft vor,
  bei 25 Wiederholungen pro Bedingung 12- beziehungsweise 13-mal.

* Vorschau: Im Play Mode schaltet `P` eine reine Live-Vorschau ein/aus;
  `F6` beendet sie ebenfalls. Kein Training, keine Trials, keine Aufzeichnung.
  Das gilt auch beim Checkerboard. Im Manager-Inspector unter
  **Live Preview (No Recording)** werden die verknüpften Stimuluswerte direkt
  bearbeitet. Vor dem Öffnen wird ihre gesamte Konfiguration gesichert.
  Beim Beenden mit `P`/`F6` oder direktem Start von Training/Sitzung werden alle
  Vorschauänderungen verworfen, auch Punkt-/Gittergröße, Farben, Fixationskreuz
  und Raster. Der Trial Plan bleibt davon unabhängig.
  In der Random-Dot-Vorschau schwenkt das Feld optional immer
  weiter (`Loop Sweep In Preview` am `Random Dot Field`). Der Schwenk läuft
  dabei über die ganze Punktwelt und fängt am Ende wieder von vorne an; mit
  `40,6°` World Coverage und `1,2°/s` dauert ein Durchlauf etwa `26 s`. Die
  Vorschau benutzt die Werte am `Random Dot Field` (`m`, `k`, FOV,
  Geschwindigkeit, Richtung). Erst mit `F5` gelten die Werte des Experiment
  Managers, und jeder Schwenk läuft genau einmal.

* Random Dots, Vorschau übernehmen: Im Manager-Inspector unter
  **Live Preview (No Recording)** übernimmt **Use Preview Values For The
  Experiment** FOV, m, Zoom, Auge, Bewegungsart (`Session Motion Mode`),
  Schwenkachse und Geschwindigkeit in der Bildmitte in den Versuchsplan. Listen
  werden durch den einen Vorschau-Wert ersetzt. Das Punktbild (Dichte,
  Punktgröße, Edge Softness, Farben) bleibt danach erhalten. Die k-Liste wird
  nicht verändert. Im Play Mode geht das nur bei laufender Vorschau (`P`); es
  gilt sofort für `F5`/`F7` und wird nach dem Stoppen des Play Mode in die Szene
  geschrieben (Konsole meldet es, dann Szene mit Strg+S speichern).
  Beim ersten Übernehmen wird der bisherige Stand gesichert;
  **Restore Original Values** holt ihn zurück (auch nach mehrmaligem
  Übernehmen). Auf dem Lab-PC vor `git pull` die Originalwerte wiederherstellen
  oder die Szenenänderung verwerfen, sonst gibt es einen Konflikt in der Szene.

* Checkerboard, Vorschau übernehmen (gleiche Bedienung): Die Live-Vorschau
  zeigt FOV, Edge Softness, Kreisblende, k, m, Gitterabstand, Augenmodus,
  Farben (dunkel, hell, Hintergrund), Fixationskreuz und alle Noise-Werte.
  `Preview Shows Noise` schaltet die Vorschau zwischen Schachbrett und
  Noise-Maske um (auch während sie läuft), damit Punktgröße und Flimmerrate
  beurteilt werden können. **Use Preview Values For The Experiment** schreibt
  FOV, Augenmodus und m in den Versuchsplan (Listen werden ersetzt) und behält die
  Darstellung; die k-Liste bleibt unverändert. **Restore Original
  Values**, Übernahme aus dem Play Mode in die Szene und der Hinweis
  zum Lab-PC gelten genauso wie bei Random Dots.

* Aktive Trials (`HeadTracked`): Die Person dreht den Kopf selbst, ohne Sollbahn.
  Die Punktwelt ist endlich: `Head Turn Safety Degrees` beschreibt die Reserve um
  die Startblickrichtung, keine unbegrenzte 360°-Welt. Frühere Varianten (geführte
  Sinusbahn mit Kopftraining, feste Dauer, "No Time Limit") wurden am 2026-10-16
  entfernt; aktive Trials laufen immer mit der Geschwindigkeitserkennung.

* Kopftempo-Regel (in `RandomDotActiveSweepRule`, gleich für die aktiven Trials
  und Active Practice 2; am 2026-10-18 mit dem Betreuer bewusst vereinfacht, damit
  sie sich in der Thesis in einem Satz beschreiben lässt): Im aktiven Trial sind
  zuerst nur graue Fläche und Kreuz zu sehen. `RandomDotHeadVelocityGate` misst
  jeden Frame die Kopfdrehung mit Vorzeichen (`Velocity Gate Axis`: horizontal =
  Yaw, optional vertikal = Pitch), geglättet (`Velocity Smoothing Seconds`, 0,1 s).
  Es gibt **einen** Schwellwert, `Minimum Moving Head Speed` ("Head Counts As
  Still Below", Standard 1 °/s): Darunter gilt der Kopf als still.
  - Die Punkte erscheinen, sobald das Tempo in der erlaubten Richtung darüber
    liegt. Wie schnell, spielt keine Rolle; kein Zieltempo, keine Haltezeit,
    keine Toleranz in Prozent, keine Mindeststrecke, keine Wartezeit.
  - Sie bleiben `Simulated Sweep Seconds` (0,8 s, wie Simulated, ab Beginn des
    Einblendens).
  - Fällt das Tempo **in Schwenkrichtung** (Richtung beim Einblenden) unter den
    Schwellwert, bricht der Trial sofort ab; die Glättung ist der einzige Puffer.
    Das gilt beim Stehenbleiben und beim Zurückdrehen: Dreht der Kopf ruckartig
    zurück, ist das Tempo in Schwenkrichtung negativ. Mit dem Betrag würde das
    geglättete Tempo bei einem Ruck den Bereich um 0 in einem Frame überspringen.
  - Nach einem Abbruch verschwinden Punkte und Kreuz, die Person antwortet
    trotzdem. Die Zeile wird mit Antwort als `invalid:head_stopped` gespeichert
    und der Trial später wiederholt. Wegschauen (`Require Fixation`) bricht genauso
    ab (`invalid:off_target` bzw. `invalid:invalid_gaze_data`), auch bei Simulated.

  CSV-Spalten: `velocity_gate_target_deg_per_s` (Kopftempo des simulierten
  Schwenks, nur zum Vergleich), `head_speed_onset/mean/min/max_deg_per_s`,
  `head_motion_wait_s`, `head_direction` (tatsächliche Schwenkrichtung Right/Left
  bzw. Up/Down), `head_offaxis_speed_mean_deg_per_s` und
  `head_offaxis_max_deviation_deg` (Bewegung neben der Schwenkachse während der
  Punkte, bei horizontalem Schwenk also Nicken), `head_roll_max_deviation_deg`
  (seitliches Kippen); bei Simulated NaN bzw. leer. Nur Messung, kein Abbruch.
  `head_speed_reference_deg_per_s` und `head_turn_before_dots_deg` gibt es seit
  der Vereinfachung nicht mehr. Der Experiment Monitor zeigt die Abbruchquote
  ("Stopped Trials: 12 of 80 shown (15 %)", davon "Head Stopped"). Marker
  `WaitingForHeadMotion`, `TrialStopped`, `TrialInvalid` (mit Antwort).

* Ein Schwenk pro Trial, abwechselnd (`Alternate Sweep Direction`, Standard an):
  Die Punkte erscheinen nur, wenn der Kopf entgegengesetzt zum letzten Trial
  schwenkt (rechts, antworten, links, antworten, ...). Ein Schwenk in dieselbe
  Richtung zählt wie Stillhalten. Auch ein abgebrochener Schwenk zählt als letzte
  Richtung. Das gibt einen ruhigen Rhythmus mit einer Pause bei jeder Antwort und
  gleicht links/rechts aus. Der erste Trial einer Sitzung (und der Übung) darf in
  beide Richtungen gehen. Aus = jede Richtung. Der Experiment Monitor zeigt
  "Next Sweep".

* Einblenden (`Dots Fade In Seconds`, Standard 0,2 s; 0 = kein Einblenden): Die
  Punkte erscheinen nicht schlagartig, sondern werden über diese Zeit eingeblendet
  (Shader-Wert `_DotsOpacity`). Gilt für alle Trials, Beispiele und Übungstrials,
  Simulated wie Active, damit sich die Bedingungen darin nicht unterscheiden. Die
  Darbietungszeit zählt ab Beginn des Einblendens. In Active Practice 1 werden die
  Punkte genauso schnell wieder ausgeblendet. Die Vorschau blendet nicht ein.

* Antwortphase und Flash (keine Töne): Nach den Punkten ist nur die graue Fläche
  zu sehen, ohne Kreuz. Nach der Antwort färbt ein schwacher Flash den ganzen
  grauen Kreis kurz ein (`Answer Flash Color` cyan, `Answer Flash Opacity` 0,1,
  `Answer Flash Seconds` 0,2 s; 0 = kein Flash), auch nach einem Abbruch. Danach
  (nach dem Flash bzw. `Pause Seconds`, was länger ist) kommt das Kreuz wieder,
  und der Kopf darf sich wieder bewegen. Gilt für Simulated, Active und die
  Übungen. Die früheren Farbsignale am Kreuz (cyan, grün, magenta) gibt es nicht
  mehr: Sie waren kaum zu sehen, und Magenta hätte nach "falsch" ausgesehen.

* Active Practice (`Train Active Panning`, Standard an): kommt in der Sitzung
  nach der Tastenbelegung und vor
  der Anleitung der echten aktiven Trials, oder allein mit `T` im Head-Tracked-
  Modus. Nichts wird gespeichert (nur Marker und Konsole). Ablauf:
  1. Optional (`Show Active Speed Example`): "An example of a calm speed". Der
     Computer schwenkt die Punkte einmal mit dem Simulated-Tempo (k = 1,
     `Example Long Seconds`), der Kopf bleibt still.
  2. Practice 1 (`Free Movement Practice`, Standard an): Bewegungsgefühl mit k = 1,
     ohne Antworten. Die Punkte sind da, solange der Kopf schneller als der
     Schwellwert dreht (Richtung egal, Tempo egal, keine Zeitgrenze), und werden
     ausgeblendet, sobald er darunter fällt. Mit `Require Fixation` auch, wenn die
     Person länger als `Max Look Away Seconds` außerhalb der Toleranz um das Kreuz
     schaut; ohne passiert dabei nichts. Kurz vor dem Rand der Punktwelt (`Head
     Turn Safety Degrees`) werden die Punkte ebenfalls ausgeblendet und beim
     nächsten Einblenden neu um den Kopf gelegt. Ende mit `Practice Continue Key`
     (Leertaste) durch den Versuchsleiter, nicht mit dem Trackpad.
  3. Practice 2: Trials genau wie im Test (Regel oben, 0,8 s, Antwort ohne Kreuz,
     Flash, Abbruch mit Antwort), ohne Texte zwischen den Trials, mit k aus
     `Active Practice K Values` (Standard 0,2 und 1,8). Ende nach `Active Practice
     Good Trials` guten Trials (nicht abgebrochen, Standard 6) oder spätestens nach
     `Active Practice Max Trials` (Standard 20).
  Vor Practice 1 und Practice 2 steht je ein kurzer Text zum Weiterklicken.

* Übungstexte zum Weiterklicken (`Practice Texts Wait For Press`, Standard an):
  In allen Übungen (Simulated-Beispiele, "Now practise", Rückmeldungen, ruhiges
  Tempo, Texte vor Active Practice 1 und 2) bleibt der Text stehen, unten steht
  "PRESS TRACKPAD TO CONTINUE". Weiter mit Trackpad-Klick am Controller oder, für
  den Versuchsleiter, mit `Practice Continue Key` (Leertaste). Ein Druck, der noch
  zur Antwort gehörte, zählt nicht. Aus = die Texte laufen wie früher nach fester
  Zeit weiter. Nur in den Übungen, nicht in der Messung.

* `Train Simulated Motion`: eigenes Training vor dem simulierten Block. Kopf
  stillhalten, Kreuz anschauen, kurze simulierte Bewegung sehen, dann antworten.
  Die Werte stehen unter `Simulated Training K Values`; Wiederholungen unter
  `Simulated Training Repeats Per Value`. Subjektive Formurteile erhalten keine
  vorgegebene Richtig-/Falsch-Rückmeldung und zählen nicht als Mess-Trials.

* `T` startet auch ohne Sitzung ein eigenständiges Training der bei `Session
  Motion Mode` gewählten Bewegungsart (Active: Active Practice). `F5` bestätigt die
  Trainingsinstruktionen, `F6` beendet das Training. Es entstehen keine
  Sitzungsdateien. In einer Messung wählt der Manager das Training passend zum
  jeweiligen Bewegungsblock; diese Übungen erhöhen die Haupttrial-Zähler nicht.

* `Show Reference Grid` in der Live-Vorschau zeigt ein gerades, kopffestes
  Karopapier-Raster. Es wird weder durch `k`, `m` oder Zoom verzerrt noch
  mitgeschwenkt. Die Punkte bewegen sich darüber. Abstand, Linienbreite und
  Farbe sind einstellbar. Das Raster bleibt während Training und Messung aus.

* `Session Motion Mode`: genau eine Bewegungsart pro Sitzung. `F5` startet diese
  Auswahl, `F7` startet die andere Variante als neue Sitzung. Beide Tasten sind
  einstellbar. `T` übt dieselbe Auswahl ohne Messdateien.

* `Head Turn Safety Degrees`: so weit darf der Kopf im `HeadTracked`-Block von
  der Blickrichtung zu Trialbeginn weg drehen (in jede Richtung), ohne dass am
  Rand eine leere Fläche erscheint; voreingestellt auf `30°`. Der Inspector zeigt
  darunter für jedes m, wie viele Punkte das braucht (Grenze 500.000). Gilt auch
  in der Vorschau, wenn dort `Motion Mode = HeadTracked` gewählt ist; `World
  Coverage` am Random Dot Field gilt nur für die Simulated-Vorschau.

* Random-Dot-Ergebnisse enthalten zusätzlich `head_motion_constraint`:
  `simulated` oder `free`. Bei aktiven Trials stehen Schwenkweite,
  Schwenkgeschwindigkeit und nominelle Bildmitten-Geschwindigkeit auf `NaN`, da
  es dort keine programmierte Bewegung gibt; das echte Kopftempo steht in den
  `head_*`-Spalten. Die Sweep-Messwerte (`min/max_sweep_deg`, `mean/peak_abs_...`)
  beschreiben Yaw (links/rechts). Die Spalten der früheren geführten Bewegung
  (`completed_half_sweeps`, `profile_rmse_deg`, `first/second_turn_error_deg`)
  gibt es seit Mapping-Version `v10` nicht mehr.

* `Eye Presentations`: beide, nur linkes oder nur rechtes Auge

* Fixations- und Wiederholungsgrenzen wie beim Checkerboard

Am `Random Dot Keyboard Controller` (am Objekt `Random Dot Field`) stehen die
Antworttasten: die beiden Tasten der Tastatur, `Use Vr Controller Buttons` und
`Vr Controller Mapping`.

Eine Sitzung enthält nur Simulated Panning ODER aktive Kopfbewegung, jeweils
als ein vollständig gemischter Plan mit Viertelpausen (`Blocks Per Session`).
Mit dem Pilotdesign (13 k-Stufen, Randwerte 8-mal, Mitte 25-mal), einem FOV,
einem Augenmodus, einem m und Zoom 1 sind das 257 gültige Trials pro
Sitzung. Weitere eingestellte Bedingungen
multiplizieren die Anzahl; ungültige Trials werden zusätzlich wiederholt.
Danach endet die Sitzung und die Aufnahme wird gestoppt. `F7` startet die andere
Variante mit eigenem Plan, passenden Anweisungen/Training und neuen Messdateien.
Es gibt keine automatische Reihenfolge oder Zusammenführung zu 350 Trials;
die Reihenfolge der zwei Sitzungen wird im Versuchsprotokoll festgelegt.
Die kompatiblen CSV-Indizes `motion_block_index` und `mini_block_index` bleiben 1.
Die Schwenkrichtung
der simulierten Trials wechselt über die
Wiederholungen jeder Bedingung, sodass links und rechts möglichst gleich oft vorkommen;
durch das Mischen ist die Reihenfolge zufällig. Gleiche Wiederholungen verschiedener `k`-Stufen und beider
Bewegungsarten verwenden vergleichbare Punkt-Seeds, damit die Punktverteilung
nicht mit einer Bedingung verwechselt wird.

Vor den ersten aktiven Trials einer Sitzung kommt die Active Practice (falls
eingeschaltet), danach die Anleitung für die echten Trials im Headset; `F5`
startet die Trials.

Nach einer Bewegungsphase verschwinden die Punkte, und während der Antwort ist
nur der neutrale graue Kreis zu sehen (ohne Kreuz). Zwischen den Trials und in
den Blockpausen ist das Kreuz wieder da. Dadurch entsteht kein Wechsel vom
helleren Punktfeld auf einen vollständig schwarzen Bildschirm. Außerhalb der
Kreisöffnung bleibt der Hintergrund in allen Phasen unverändert schwarz.

Das sichtbare Feld hat innen einen neutralgrauen Hintergrund für symmetrischen
Kontrast der schwarzen und weißen Punkte. Außerhalb der kreisförmigen Feldblende
ist der Hintergrund nahezu schwarz.

### Geschwindigkeit des simulierten Schwenks

Drei Geschwindigkeiten sind zu unterscheiden:

* Objektwinkel: wie schnell das Instrument über die Außenwelt schwenkt. Das
  ist die Größe, mit der der Shader rechnet.

* Sichtbarer Bildwinkel: wie schnell ein Punkt im Bild wandert. In der
  Bildmitte ist das bei jedem `k` genau `m * Content Zoom` mal so schnell wie
  der Objektwinkel. Zum Rand hin wird es je nach `k` schneller oder langsamer;
  dieser Unterschied ist der eigentliche Reiz.

* Kartesische Bildkoordinate (Tangens des Bildwinkels): in der Bildmitte
  gleich dem Bildwinkel, zum Rand hin wächst sie zusätzlich mit `1 / cos²`.

Die Objektgeschwindigkeit wird für jeden Trial so gewählt, dass die Punkte in
der Bildmitte immer mit `Simulated Image Center Speed` laufen:

    Objektgeschwindigkeit = Bildmitte-Geschwindigkeit / (m * Content Zoom)

Dieselbe Normierung gilt auch für die Live-Vorschau (`Preview Image Center
Speed` am Random Dot Field).
Beispiel ohne zusätzlichen Content Zoom: gewünschte `5°/s` bedeuten bei
`m = 1, 10, 20` virtuelle Schwenkgeschwindigkeiten von `5, 0,5, 0,25°/s`.
Die Vorschau-Einstellungen sind unabhängig von den Trainings-/Messwerten am
Manager und werden beim Verlassen der Vorschau zurückgesetzt.

Das Verhältnis zwischen Mitte und Rand bleibt dabei für jedes `k` unverändert.
Die Dauer des Schwenks bleibt bei jedem `m` gleich; in der Außenwelt wird die
Schwenkweite mit wachsendem `m` kleiner. Mit `12°/s` und `0,8 s`:

| m | Objektgeschwindigkeit | Schwenkweite je Seite | sichtbarer Weg in der Bildmitte |
|---|---|---|---|
| 5 | 2,4°/s | 0,96° | 9,6° |
| 10 | 1,2°/s | 0,48° | 9,6° |
| 14 | 0,86°/s | 0,34° | 9,6° |
| 20 | 0,6°/s | 0,24° | 9,6° |

Der `HeadTracked`-Block ist davon nicht betroffen: Dort bestimmt die echte
Kopfbewegung die Bildbewegung, und die Punkte laufen in der Bildmitte weiterhin
`m`-mal so schnell wie der Kopf dreht. Objekt- und Bildmitte-Geschwindigkeit
stehen für jeden Trial in `sweep_speed_deg_per_s` und
`image_center_speed_deg_per_s`.

### Punktdichte

Die Punktzahl wird nicht direkt eingestellt. Am `Random Dot Field` steht
stattdessen `Dot Density`: wie viele Punkte pro Quadratgrad in der Bildmitte
liegen sollen, voreingestellt `0,19` (ungefähr ein Punkt alle `2,3°`). Daraus
rechnet das Skript für jeden Trial selbst aus, wie groß die Punktwelt sein muss
und wie viele Punkte hineingehören. So sieht das Feld bei jedem `m` gleich dicht
aus; ohne diese Anpassung wäre es bei `14x` etwa doppelt so dünn wie bei `10x`.

Die Rechnung dahinter: In der Bildmitte vergrößert das Instrument bei jedem
`k` genau um `m`, ein optionaler Content Zoom noch einmal um seinen Wert. Eine
kleine Fläche der Außenwelt erscheint im Bild also `(m * Zoom)²`-mal so groß,
und genau um diesen Faktor dichter werden die Punkte in der Außenwelt gesetzt:

    Punktzahl = Dichte * (m * Zoom)² * Fläche der Punktwelt

Die Punktwelt ist eine Kugelkappe. Sie deckt ab, was man durch den Kreis
sieht, plus die Schwenkweite zu jeder Seite und `1°` Reserve. Im
`HeadTracked`-Block kommt statt der Schwenkweite der Sicherheitspuffer dazu.
Am Rand des Kreises staucht oder streckt das Instrument je nach `k`; dort ist
die Dichte deshalb nicht überall gleich. Das ist gewollt, denn genau so
verhält sich auch ein echtes Fernglas. Alle `k`-Stufen zeigen bei gleichem `m`
dieselbe Außenwelt.

Mit `70°` FOV, `0,19` Dichte und den Werten oben ergeben sich pro Trial:

| m | simulierter Block | HeadTracked-Block |
|---|---|---|
| 5 | 1.200 - 1.600 Punkte | 7.800 - 8.800 Punkte |
| 10 | 1.500 - 2.000 Punkte | 22.500 - 24.300 Punkte |
| 14 | 1.700 - 2.300 Punkte | 39.700 - 42.100 Punkte |
| 20 | 2.100 - 2.700 Punkte | 74.700 - 78.000 Punkte |

Die Werte des simulierten Blocks gelten für die Voreinstellung `Bildmitte` mit
`12°/s`. Der `HeadTracked`-Block braucht bei `20x` knapp `78.000` Punkte; der
Aufbau dauert auf dem Entwicklungsrechner etwa `35 ms` und passiert einmal pro
Trial, während nur das Kreuz zu sehen ist.

Beim Start einer Sitzung wird für alle Trials nachgerechnet, ob die Punktwelt
unter `170°` und unter `500.000` Punkten bleibt; sonst startet die Sitzung nicht
und die Konsole nennt die Ursache. Die Grenze ist keine technische Grenze; sie
verhindert, dass die Punktzahl still abgeschnitten und die Dichte damit zwischen
Bedingungen ungleich wird. Mehr Punkte verlängern den Aufbau pro Trial (grob
linear; aus den `35 ms` bei 78.000 Punkten geschätzt etwa `0,2 s` bei 500.000,
nicht gemessen). Punktzahl und Größe der Punktwelt
stehen für jeden Trial in `dot_count` und `world_coverage_diameter_deg`, die
Dichte im Marker `SessionStart`.

Ob der Rechner mitkommt, steht ebenfalls pro Trial in der CSV:
`frames_presented` (Bilder während der Darbietung), `slow_frames` (Bilder, die
länger als das 1,5-Fache der Bildwiederholzeit brauchten, bei 90 Hz also über
16,7 ms), `max_frame_ms` und `expected_frame_ms` (Bildwiederholzeit von Headset
bzw. Monitor). In VR werden ausgefallene Bilder hochgerechnet; die Bewegung kann
dann ruckeln, ohne dass es am Monitor auffällt. Trials mit `slow_frames > 0`
sollte man sich deshalb ansehen. Am Sitzungsende meldet die Konsole, wie viele
Darbietungen betroffen waren.

## Eye Tracking und Messdateien

Die Eye-Tracking-Toolbox aus dem Lab bleibt die Grundlage der Rohdaten. Ihre
vorhandenen Blickspalten mit Augenstatus, Pupillendurchmesser, Ursprung und
Blickrichtung werden weiterhin geschrieben. Im Toolbox-Code wurde für den
Checkerboard-Test nur der Stimulusmarker um `instrument_distortion_k`, m, FOV, Augenmodus,
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

* `instrument_distortion_k`, `instrument_magnification_m`, FOV und Augenmodus

* `checkerboard_inverted`: ob schwarze und weiße Felder in dieser Darbietung
  vertauscht waren (0, solange die Option aus ist)

* `stimulus_task = Checkerboard` als eindeutige Aufgabenkennung

* Beginn und Ende des Musters, Beginn der Antwortanzeige sowie die tatsächlich
  gemessenen Stimulus-, Noise- und Reaktionszeiten

* Antwort

* `valid_for_analysis`

* aktueller Fixationswinkel und Anteil gültiger Samples

* längste zusammenhängende Off-Target- und Invalid-Gaze-Dauer

* Grund für einen Ausschluss

* Versionskennung der verwendeten Abbildung

Marker wie `TrialStart`, `StimulusEnded`, `NoiseMaskStarted`,
`GrayScreenStarted`, `TrialResponse`, `TrialInvalid` und `TrialRepeatQueued`
verbinden den Versuchsablauf zeitlich mit den Rohdaten. `NoiseMaskStarted` und
`GrayScreenStarted` nennen mit `phase=response` oder `phase=pre_stimulus`, zu
welcher Phase die Darstellung gehört (`pre_stimulus` = graue Fläche,
`response` = Noise-Maske).

Beim Random-Dot-Test werden zusätzlich unter anderem festgehalten:

* die vorgegebenen Werte `instrument_distortion_k`,
  `instrument_magnification_m` und `content_zoom`

* Schwenkachse, Startrichtung, Amplitude und Geschwindigkeit

* bei aktiver Kopfbewegung der mittlere Profilfehler und die Abweichungen
  an den beiden Soll-Umkehrpositionen

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

## Vive Pro Eye

Derselbe Projektstand läuft ohne Umstellung auch mit der HTC Vive Pro Eye. Das
Headset wird über OpenXR (SteamVR) angesprochen, das Eye Tracking über das
SRanipal-SDK unter `Assets/ViveSR`.

### Voraussetzungen auf dem Lab-PC

* Unity 6000.5.6f1 und Git (das Varjo-Paket wird beim ersten Öffnen über Git
  geladen)

* SteamVR läuft und ist als OpenXR-Runtime festgelegt (SteamVR-Einstellungen,
  Bereich OpenXR)

* VIVE Console beziehungsweise die SRanipal-Runtime läuft (`sr_runtime.exe`,
  Roboter-Symbol im Infobereich der Taskleiste)

* Varjo Base läuft auf diesem PC nicht

Danach genügt `git pull`. In Unity ist nichts einzustellen: keine Werte im
Inspector, keine Project Settings, keine Pakete.

### Was automatisch gewählt wird

An der `Eye Tracking Toolbox` steht der Provider in beiden Szenen auf `Auto`.
Beim Start des Play Mode wird entschieden:

| Rechner | XR-Loader | Eye Tracker |
| --- | --- | --- |
| Varjo-PC, Varjo Base läuft | `VarjoLoader` | `Varjo` |
| Vive-PC, SteamVR und SRanipal-Runtime laufen | `OpenXRLoader` | `SRanipal` |
| Laptop ohne Headset und ohne Runtime | keiner | `Dummy` |

XR Plug-in Management versucht die Loader in Listenreihenfolge, zuerst Varjo,
dann OpenXR. Der erste Loader, der startet, gewinnt. Die Konsole zeigt die
Entscheidung beim Start, zum Beispiel:

```text
XR-Loader aktiv: OpenXRLoader (OpenXR-Runtime: SteamVR/OpenXR). Eye-Tracker: SRanipal (eingestellt: Auto).
SRanipal Eye Tracking bereit (Eye v2, Callback registriert).
```

Dieselben Angaben stehen im Experimenter Monitor und als Marker
`EyeTrackingProvider;configured=…;active=…;xr_loader=…` am Anfang jeder
Aufzeichnung.

Der `Dummy` lässt den Blick der Maus folgen und ist nur für einen Tastaturtest
am Laptop gedacht. Fällt `Auto` auf den `Dummy`, während ein XR-Headset aktiv
ist oder `Require Fixation` eingeschaltet ist, startet `F5` keine Sitzung. Der
Grund steht dann rot im Experimenter Monitor und in der Konsole.

### Zwei Regeln

* In `Project Settings -> XR Plug-in Management` keine Häkchen umschalten. Unity
  sortiert die Loader dabei alphabetisch neu, OpenXR steht dann vor Varjo, und
  auf dem Varjo-PC würde der falsche Loader starten. Der EditMode-Test
  `XrLoaderSetupTests` meldet eine vertauschte Reihenfolge;
  `git checkout -- Assets/XR` stellt sie wieder her.

* Bleibt die Vive dunkel und die Konsole meldet `XR-Loader aktiv: VarjoLoader`,
  läuft auf dem Vive-PC Varjo Base. Varjo Base beenden und den Play Mode neu
  starten.

### Unterschiede in der Gaze-Datei

Die Spalten und ihre Reihenfolge sind bei allen Eye Trackern gleich. Bei
SRanipal gilt:

* Ursprünge und Richtungen sind wie bei Varjo ins Unity-Kamerasystem
  umgerechnet (linkshändig, Meter, +x nach rechts).

* `left_validata`, `right_validata` und `combined_validata` folgen dem
  SRanipal-Bit „Blickrichtung gültig“.

* `tracking_status`, `left_tracking_status` und `right_tracking_status`
  enthalten die SRanipal-Validitätsmaske: 1 = Ursprung, 2 = Richtung,
  4 = Pupillendurchmesser, 8 = Lidöffnung, 16 = Pupillenposition. Bei
  `tracking_status` kommt 65536 hinzu, wenn SRanipal „kein Nutzer“ meldet.

* `eye_timestamp` bleibt in Nanosekunden. SRanipal liefert Millisekunden, die
  Auflösung ist deshalb 1 ms.

* `ipd_mm` ist `NaN`, weil SRanipal keinen Augenabstand liefert.
  `gaze_distance`, Pupillendurchmesser und Lidöffnung sind `NaN`, solange
  SRanipal den jeweiligen Wert als ungültig markiert.

### Checkliste für den ersten Test am Headset

Die SRanipal-Anbindung wurde ohne Headset entwickelt. Vor der ersten Messung
einmal vollständig durchgehen:

1. Start: Die Konsole meldet `OpenXRLoader`, `SteamVR` als OpenXR-Runtime und
   `SRanipal` als Eye Tracker. Der Experimenter Monitor zeigt dasselbe. Unity
   bleibt beim Start nicht hängen.

2. Bild: Das Muster erscheint im Headset. Mit Augenmodus „nur links“ und „nur
   rechts“ sieht jeweils nur das genannte Auge das Bild.

3. Kalibrierung: `C` startet die SRanipal-Kalibrierung im Headset. Unity läuft
   währenddessen weiter, und danach meldet die Konsole
   `SRanipal-Blickkalibrierung abgeschlossen`.

4. Validität: Beim Blick auf das Kreuz zeigt der Experimenter Monitor
   `ON TARGET`. Beim Schließen der Augen wechselt er auf `NO VALID GAZE`.

5. Blickstrahl: Beim Blick auf das Fixationskreuz liegt die `Blickabweichung`
   deutlich unter der Toleranz. Beim Blick nach rechts wird
   `combined_eye_gaze.x` in der Gaze-Datei positiv, beim Blick nach oben
   `combined_eye_gaze.y`.

6. Augenpositionen: In der Gaze-Datei ist `left_eye_origin.x` negativ (etwa
   -0,03) und `right_eye_origin.x` positiv. Sind die Vorzeichen vertauscht,
   stimmt die Spiegelung in `SRanipalGazeConversion.cs` nicht.

7. Abtastrate: Eine kurze Aufzeichnung mit `F9` enthält etwa 120 Zeilen pro
   Sekunde, und `frame_number` steigt ohne Lücken.

8. Fixationsabbruch: Wegschauen während eines Trials macht den Trial ungültig
   und stellt ihn hinten an (`TrialInvalid`, `TrialRepeatQueued`).

9. Antworten: In beiden Tests lösen Trigger und Trackpad-Klick am
   Vive-Controller die beiden Antworten aus, passend zur eingestellten
   Zuordnung. Eine bloße Berührung des Trackpads, Halten und Loslassen lösen
   nichts aus.

10. Messdatei: Am Anfang der Aufzeichnung steht der Marker
    `EyeTrackingProvider;configured=Auto;active=SRanipal;xr_loader=OpenXRLoader`.

11. Ende: Der Play Mode lässt sich beenden und erneut starten, ohne dass Unity
    hängen bleibt.

Zum Vergleich einmal auf dem Varjo-PC starten: Die Konsole meldet dort
weiterhin `VarjoLoader` und `Varjo`.

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
│   │   ├── ResponseInputController.cs
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
│       ├── CheckerboardFixationMonitor.cs
│       ├── EyeTrackingToolbox.cs
│       ├── EyeTrackerProviderResolver.cs
│       └── EyeTrackers/
│           ├── VarjoEyeTracker.cs
│           ├── SRanipalEyeTracker.cs
│           ├── SRanipalGazeConversion.cs
│           └── DummyEyeTracker.cs
├── Editor/
│   └── ExperimenterMonitorWindow.cs
└── Tests/EditMode/
    ├── VisualSpaceRadialMappingTests.cs
    ├── CheckerboardTrialPlannerTests.cs
    ├── CheckerboardTrialQueueTests.cs
    ├── CheckerboardTrialSequenceTests.cs
    ├── ResponseInputControllerTests.cs
    ├── ExperimentFlowPlayTests.cs
    ├── SRanipalGazeConversionTests.cs
    ├── EyeTrackerProviderResolverTests.cs
    └── XrLoaderSetupTests.cs
```

`CheckerboardInstrumentMapping.cs` enthält die aktive C#-Referenzrechnung. Der Shader führt
dieselbe Abbildung für jeden sichtbaren Pixel aus.

## Gemeinsame Abbildung, unterschiedliche Aufgaben

* Statisches Checkerboard: Instrument-k und festes m, unbewegtes Gitter,
  konkav/konvex und head-locked. Dies gilt für den nichtnegativen Originalzweig;
  die unten beschriebene eigene Erweiterung ist eine ausdrücklich getrennte Option.
* Random Dots: dieselbe Instrumentengleichung, k/m-Bedingungen,
  simulierte oder kopfgesteuerte Bewegung und konkav/konvex.
* Nur die Random Dots besitzen einen zusätzlichen Content Zoom; für direkte
  Vergleiche bleibt er 1. Punktgröße und Geschwindigkeitssteuerung bleiben
  separate experimentelle Festlegungen.

## Checkerboard: stärkere Kissenverzeichnung (eigene tanh-Erweiterung)

**Seit 22. Oktober 2026 nur in der Live-Vorschau.** Am Stimulus lässt sich in
der Vorschau `Allow Pincushion Extension` einschalten; dann sind negative Werte
zum Ansehen erlaubt. Training und Trials schalten die Erweiterung immer aus und
nehmen nur Merlitz-k von 0 bis 2 an. Vorschauänderungen werden beim Verlassen
zurückgesetzt. Random Dots bleiben unverändert.

Der gemeinsame Zahlenregler ist bei aktivierter Erweiterung ein **vorzeichenbehafteter
Reizparameter s**, nicht durchgehend Merlitz-k:

* `s >= 0`: originales Merlitz-k mit `k = s`, `tan(k*a) = m*tan(k*A)`.
* `s = 0`: Grenzfall `A = a/m`.
* `s < 0`: **eigene** tanh-Erweiterung mit `kappa = -s > 0`:
  `tanh(kappa*a) = m*tanh(kappa*A)`, im Shader invers ausgewertet als
  `A = atanh(tanh(kappa*a)/m)/kappa`.

`a` ist der scheinbare Bildwinkel, `A` der Objektwinkel (beide in Radiant),
`m` die Vergrößerung. Danach wird unverändert das feste gerade Quellgitter bei
`S = m*tan(A)/tan(FOV/2)` abgetastet. Kein pro-k-Randzoom wird hinzugefügt.
Bei `m > 1` erzeugen stärker negative Werte stärkere Kissenverzeichnung; bei
`m = 1` ist auch diese Abbildung unverzeichnet. Der Anschluss bei null ist stetig.
Die Reglergrenze `-2` ist eine Softwareentscheidung, kein Wert aus Merlitz.
Das ist weder Oomes' alpha noch ein Wahrnehmungsmodell und kein Nachweis für eine
bestimmte konkave Wahrnehmung.

Die inverse Gleichung ist im zulässigen sichtbaren Winkelbereich verwendbar.
Die Vorwärtsabbildung `a = atanh(m*tanh(kappa*A))/kappa` besitzt hingegen die
Grenze `abs(m*tanh(kappa*A)) < 1`; sie ist nicht für beliebige Objektwinkel definiert.
Die ursprüngliche tan-Gleichung mit negativem k würde keine stärkere Kissenform
liefern, weil sich die Vorzeichen aufheben. Deshalb gibt es einen eigenen Zweig.

**Daten:** Weil die Erweiterung nicht mehr in Trials vorkommt, gibt es dafür keine
CSV-Spalten mehr. Die MATLAB-Auswertung weist Dateien mit der früheren
Erweiterungskennung `custom-tan-tanh-checkerboard-…` ab.

**Vorherige Parametrisierung (historisch, z. B. Yannick-Pilot):**
`S_old = tan(l*a)/tan(l*beta)` mit `a = atan(r*tan(beta))`, `beta = FOV/2`;
bei `l = 0` gilt `S_old = a/beta`. Das war eine randnormierte Musterfamilie ohne
Vergrößerungsparameter m, nicht die finite Instrumentenabbildung mit m = 10.
Der alte Musterwert war außerdem nicht automatisch das persönliche Wahrnehmungs-l.
Die jetzige positive Abbildung nutzt dagegen `A = atan(tan(k*a)/m)/k` und
`S = m*tan(A)/tan(beta)`. Ohne zusätzliche Randnormierung kann die Blende bei
verschiedenem k unterschiedlich viele Felder ausschneiden. Das FOV selbst muss
deshalb nicht geändert werden; es bleibt eine unabhängig festgelegte Bedingung.

**Automatisch geprüft:** 319 Unity-Tests erfolgreich, ohne Fehler oder übersprungene
Tests (`Logs/checkerboard_extension_final.xml`). Darunter sind gerenderte negative
Muster gegen die C#-Referenz, der unveränderte positive Zweig, Vorschau-Rücksetzung
und CSV-Kennzeichnung. Die MATLAB-Schema-Prüfung mit ausschließlich künstlichen
Daten ist ebenfalls erfolgreich (`Logs/checkerboard_extension_matlab.log`). Ein
Headset-Sichttest und neue Pilotmessungen werden dadurch nicht ersetzt.

## Noch offen

* Neue Pilotdaten der k-Version sowie endgültige k-Stufen und die Wiederholungszahl

* Ob `Invert Checkerboard Each Trial` im Hauptversuch an sein soll (Irritation
  vs. weniger Nachbild), und die genauen Übungswerte der Teile 1 und 2.

* **Offene Entscheidung: Randbehandlung des Checkerboards** (Randnormierung
  untersucht am 19. Oktober, nicht umgesetzt). Szene: FOV 60°, weicher Rand 5°, Gitter 8°,
  m = 10. Mit dem festen Quellgitter liegen über den Durchmesser (bei FOV/2,
  50 % Deckkraft) 7,46 Felder bei k = 0, 7,49 bei 0,2, 7,63 bei 0,5, 8,22 bei 1,
  8,61 bei 1,2 und 9,14 bei 1,4. Innerhalb der Testliste 0,2–1,2 ändert sich die
  Feldzahl am Rand also um etwa 15 %, ein möglicher Hinweisreiz ohne Bezug zur
  Form. Die k-Obergrenze hängt vom FOV ab:
  k_max = (π/2 − 0,01) / ((FOV + Rand)/2 in rad), höchstens 2 (60/5: 2,75 → 2;
  90/1: 1,97; 100/5: 1,70; 120/5: 1,43).
  * Option (a): Schalter „gleiche Randnormierung“, S(r) = tan A(r) / tan A(1).
    Dann stehen am Rand bei jedem k 8,22 Felder, dafür ändert sich die
    Feldgröße in der Mitte mit k (etwa −9 % bei k = 0,2, +5 % bei 1,2, +11 % bei
    1,4 gegenüber k = 1). Eigene MappingVersion `…-edge-normalized-…`.
  * Stärkere Kissenverzeichnung über eine eigene tanh-Erweiterung ist inzwischen
    optional implementiert (Abschnitt oben). Nachgerechnet (m = 10, FOV 70,
    r = 0,5, S(r) − r): Einstellung 0 → −0,0189, −1 → −0,0361, −2 → −0,0800.
  * Wo 0 und 2 fest verdrahtet sind: `CheckerboardInstrumentMapping.ValidateParameters`,
    `MerlitzBinocularReferenceMath.Minimum/MaximumDistortionK` (auch im
    Random-Dot-Planer), `Range(0, 2)`/`Clamp` an Stimulus und Managern, die
    Shader-Properties und die Winkelgrenze 1,560796 im gemeinsamen cginc.
    Die Originalfunktionen bleiben auf nichtnegative k beschränkt. Nur das
    Checkerboard verwendet für negative Einstellungen den getrennten tanh-Zweig.

* Die Einstellungen der Geschwindigkeitserkennung (Toleranz, Wartezeiten,
  Mindeststrecke) sind Startwerte und mit weiteren Personen zu pilotieren.

* Für die Auswertung wird später eine psychometrische Funktion für
  `P(konvex | k, m)` angepasst; der 50-%-Punkt in `k` ist der gesuchte
  dynamische PSE.

* Falls die Originalimplementierung von Oomes noch verfügbar wird, kann ihre
  α-Skala nachträglich mit den dargestellten Mustern geometrisch verglichen werden; kein linearer α-k-Umrechnungsanspruch.

## Separate natürliche Szene

`Assets/GlobeEffect/Demo/NaturalScenePreview.unity` zeigt das CC0-Panorama
**Suburban Field 01** mit live einstellbarer Instrumentenabbildung und
simuliertem oder freiem Schwenken. Kein Experiment, keine Datenerfassung.
Bedienung und Grenzen: [NATURAL_SCENE_PREVIEW.md](NATURAL_SCENE_PREVIEW.md).

## Literaturgrundlage

* Oomes, A. H. J., Koenderink, J. J., van Doorn, A. J. und de Ridder, H.
  (2009). *What are the uncurved lines in our visual field? A fresh look at
  Helmholtz's checkerboard.* Perception, 38, 1284–1294.

* Helmholtz, H. von: Beschreibung des Checkerboard- bzw.
  Richtungskreis-Phänomens in der physiologischen Optik.

* Merlitz, H. (2010). *Distortion of binoculars revisited: Does the sweet spot exist?* Journal of the Optical Society of America A, 27, 50–57.

Die aktuelle Umsetzung ist eine VR-Adaption und keine exakte Replikation des
Versuchsaufbaus von Oomes. Aufgabe, Eye Tracking, Headsetdarstellung und
mono-/binokulare Bedingungen wurden für die Masterarbeit erweitert.
