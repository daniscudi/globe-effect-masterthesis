# Globe Effect

In diesem Unity-Projekt entsteht der Versuchsaufbau für meine Masterarbeit zum
Globe Effect. Der statische Checkerboard-Test orientiert sich an Helmholtz und
Oomes et al. (2009). Daneben gibt es einen getrennten Random-Dot-Test für die
Wahrnehmung während einer simulierten Schwenkbewegung.

Eine kurze Bedienungs- und Arbeitsübersicht steht in `AKTUELLER_STAND.md`.
Die aktuelle Pilot-Checkliste mit Inspector-Zuständigkeiten, vorgefundenen
Szeneneinstellungen und offenen Protokollentscheidungen steht in
[CHECKERBOARD_PILOT.md](CHECKERBOARD_PILOT.md).

## Was der Checkerboard-Test macht

Die Versuchsperson fixiert zuerst das rote Kreuz und sieht danach kurz ein
kreisrundes Schachbrett mit einem festen Verzerrungswert. Direkt danach beginnt
die Antwortphase. Je nach gewähltem Ablauf ist dabei eine flimmernde
Schwarz-Weiß-Noise-Maske (Ablauf A) oder eine graue Fläche (Ablauf B) zu sehen,
jeweils mit Fixationskreuz. Die andere Darstellung folgt nach der Antwort als
kurze Phase vor dem nächsten Schachbrett. Im Hauptversuch wird kein zusätzlicher
Antworttext mehr eingeblendet, damit er nicht vom Reiz oder von der Fixation
ablenkt.

Die Tastenbelegung steht auf dem Welcome Screen, im Training und vor dem
Hauptversuch im Headset. Sie richtet sich nach der im Inspector gewählten
Zuordnung, zum Beispiel:

* `TRIGGER = A / OUTWARD`

* `TRACKPAD CLICK = B / INWARD`

Auf der Tastatur gelten zusätzlich Pfeil hoch (Category A) und Pfeil runter
(Category B).

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

Am `Checkerboard Experiment Manager` wird unter `Trial Sequence` einer von zwei
Abläufen gewählt:

* A: Checkerboard → Noise-Maske mit Antwort → graue Fläche → nächstes
  Checkerboard

* B: Checkerboard → graue Fläche mit Antwort → Noise-Maske → nächstes
  Checkerboard

Die Schritte im Einzelnen:

1. Vor dem Checkerboard steht die Phase vor dem Stimulus: bei A die graue
   Fläche, bei B die Noise-Maske, jeweils mit Fixationskreuz. Sie dauert
   mindestens `Pre Stimulus Seconds` (voreingestellt 0,5 s). Mit
   Fixationskontrolle geht es erst weiter, wenn zusätzlich der Blick ruhig auf
   dem Kreuz liegt.
2. Das Checkerboard wird für die eingestellte Zeit eingeblendet. Der Startwert
   beträgt 600 ms.
3. Direkt danach beginnt die Antwortphase: bei A mit der Noise-Maske, bei B mit
   der grauen Fläche, jeweils mit Fixationskreuz.
4. Die im Training gelernte Antwort wird ohne erneuten Hinweistext gegeben. Die
   Antwortzeit begrenzt `Response Timeout Seconds`; `0` bedeutet ohne Zeitlimit.
5. Antwort, Reaktionszeit, `l`, FOV, Augenmodus und Fixationswerte werden sofort
   gespeichert. Pro Durchgang zählt nur die erste Antwort; weitere Tastendrücke
   werden bis zur nächsten Antwortphase ignoriert.
6. Nach der Antwort beginnt sofort die Phase vor dem nächsten Stimulus mit der
   jeweils anderen Darstellung. Ein schwarzer Zwischenbildschirm wird vermieden.

Die Noise-Maske sieht aus wie das Ameisenmuster eines Fernsehers ohne Empfang:
viele kleine schwarze und weiße Punkte, die ständig neu ausgewürfelt werden. Sie
erscheint in derselben runden Öffnung wie das Checkerboard. Punktgröße und
Aktualisierungsrate stehen am `Checkerboard Stimulus` (`Noise Dot Size Degrees`,
voreingestellt 0,15°, und `Noise Refresh Rate Hz`, voreingestellt 30 Hz; `0`
lässt das Bild stehen).

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

* Trackpad-Klick = konkav, Trigger = konvex

* Trackpad-Klick = konvex, Trigger = konkav

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

* `Visual Space L Values`: alle zu präsentierenden Verzerrungswerte

* `Content Zoom Values`: unabhängiger Zoom; `1` lässt die Größe unverändert

* `Repetitions Per Condition`: Wiederholungen jeder Kombination

* `Require Fixation`: Vorfixation und Kontrolle während des Musters

* `Maximum Off Target Seconds`: erlaubte zusammenhängende Blickabweichung

* `Maximum Invalid Gaze Seconds`: erlaubte Dauer fehlender oder ungültiger Daten

* `Maximum Attempts Per Trial`: 0 für unbegrenzte Wiedervorlage

* `Stimulus Duration Seconds`: Dauer des Checkerboards; voreingestellt auf 0,6 s

* `Trial Sequence`: Ablauf A (Noise-Maske in der Antwortphase, danach graue
  Fläche) oder B (graue Fläche in der Antwortphase, danach Noise-Maske)

* `Pre Stimulus Seconds`: Mindestdauer der Phase vor dem nächsten Checkerboard;
  voreingestellt auf 0,5 s

* `Response Timeout Seconds`: maximale Antwortzeit; 0 bedeutet ohne Zeitlimit

* `Convex Response Key` und `Concave Response Key`: Tasten der beiden Antworten

* `Controller Mapping`: welche Controller-Taste welche Antwort gibt

* `Require Training Before Session`: verlangt ein abgeschlossenes Training vor F5

* `Training Category A/B Visual Space L`: deutliche, noch zu pilotierende Beispiele

* `Training Visual Space L Values`: einstellbare `l`-Werte der Übungstrials

* `Training Repetitions Per Value`: Wiederholungen jedes Übungswertes

* `Random Seed`: macht die zufällige Reihenfolge reproduzierbar

Die aktuell eingetragenen `l`-Werte und drei Wiederholungen sind nur für einen
technischen Pilotlauf gedacht. Sie sind noch keine festgelegten Bedingungen der
Masterarbeit. Die Listen können im Inspector vollständig geändert werden.

Am Objekt `Checkerboard Stimulus` werden Aussehen, Felderzahl, Farben, Größe des
Fixationskreuzes, die weiche Blendenkante sowie Punktgröße und
Aktualisierungsrate der Noise-Maske eingestellt. Während einer Sitzung
setzt der Experiment Manager FOV, `l`, Content Zoom und Augenmodus automatisch.

Für den Bewegungstest liegen die wichtigsten Einstellungen am Objekt
`Random Dot Experiment Manager`:

* `Instrument Distortion K Values`: die zu präsentierenden
  Instrumentenverzeichnungen; geometrisch dieselbe Kandidatenfamilie wie beim
  Checkerboard

* `Instrument Magnification M Values`: eine oder mehrere
  Fernglasvergrößerungen zwischen `1` und `20`; voreingestellt ist `10`

* `Content Zoom Values`: optionaler Nach-Zoom; für die Instrumentensimulation
  normalerweise `1`

* `Repeats Per Condition`: Wiederholungen jeder Kombination

* Keine Unterblock-Pausen mehr: Alle Trials einer Bewegungsart laufen in
  gemischter Reihenfolge durch, ohne erneutes `F5` innerhalb dieses Blocks.
  Die Pause zwischen Simulated und Active bleibt bestehen. Für die Kompatibilität
  der Messdateien bleibt `mini_block_index` erhalten und steht pro Bewegungsblock auf `1`.

* `Simulated Sweep Seconds`: wie lange die Punkte im simulierten Block zu
  sehen sind; voreingestellt `0,8 s`. In dieser Zeit schwenkt das Feld
  einmal in eine Richtung und kehrt nicht um.

* `Head Tracked Seconds`: wie lange die Punkte im aktiven
  `HeadTracked`-Block zu sehen sind; der Pilotwert beträgt `5 s`

* `Simulated Sweep Axis`: `Horizontal` (Standard, links/rechts) oder
  `Vertical` (oben/unten) für den simulierten Block. Der aktive
  geführte `HeadTracked`-Block und sein Training bleiben horizontal. Freie aktive
  Bewegung kann auch oben/unten oder schräg erfolgen. Die Achse und
  die Richtung werden in Plan, Ergebnissen und Trial-Markern gespeichert:
  beim simulierten Schwenk `Right`/`Left` bzw. `Up`/`Down`, beim
  Kopfschwenk die Seite des ersten Schwenks (`RightFirst`/`LeftFirst`). Die Auswahl erzeugt keine zusätzlichen Trials; für einen
  Vergleich beider Achsen sind getrennte Sitzungen nötig.

* `Sweep Amplitude Degrees`: Zielschwenkweite des Kopfes je Seite im
  geführten `HeadTracked`-Block und im Kopftraining; für `10x` zunächst `2°`

* `Simulated Speed Reference`: worauf sich die Geschwindigkeit des simulierten
  Schwenks bezieht. `Bildmitte` (Voreinstellung): Die Punkte laufen in der
  Bildmitte bei jedem `m` gleich schnell, nämlich mit
  `Simulated Image Center Speed`. `Objektwinkel`: die bisherige Definition, das
  Instrument schwenkt bei jedem `m` mit `Sweep Speed` über die Außenwelt, und
  die Punkte laufen in der Bildmitte `m`-mal so schnell. Siehe Abschnitt
  „Geschwindigkeit des simulierten Schwenks“.

* `Simulated Image Center Speed`: sichtbare Winkelgeschwindigkeit der Punkte in
  der Bildmitte, voreingestellt `12°/s`. Das entspricht dem bisherigen Stand
  mit `10x` und `1,2°/s`.

* `Sweep Speed`: reale Instrument-/Kopfgeschwindigkeit in Grad Objektwinkel pro
  Sekunde, voreingestellt `1,2°/s`. Im `HeadTracked`-Block ist das die mittlere
  Geschwindigkeit des Sinusprofils. Im simulierten Block gilt der Wert nur bei
  der Einstellung `Objektwinkel`.

* Der simulierte Schwenk geht nur in eine Richtung und hat eine feste
  Geschwindigkeit. Bei `10x` läuft er mit `1,2°/s` und `0,8 s` von `-0,48°`
  nach `+0,48°`, die Mitte der Bewegung liegt also genau geradeaus. Ob nach
  links oder rechts, wechselt von Trial zu Trial in zufälliger Reihenfolge;
  beide Richtungen kommen bei gerader Wiederholungszahl gleich oft vor,
  bei 25 Wiederholungen pro Bedingung 12- beziehungsweise 13-mal.

* Vorschau: Im Play Mode schaltet `P` eine reine Live-Vorschau ein/aus;
  `F6` beendet sie ebenfalls. Kein Training, keine Trials, keine Aufzeichnung.
  Das gilt auch beim Checkerboard. Im Manager-Inspector unter
  **Live-Vorschau ohne Messung** werden die verknüpften Stimuluswerte direkt
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
  **Live-Vorschau ohne Messung** übernimmt **Vorschau-Werte ins Experiment
  übernehmen** FOV, m, Zoom, Auge, Bewegungsart (`Session Motion Mode`),
  Schwenkachse und Geschwindigkeit (Bezug + Wert) in den Versuchsplan. Listen
  werden durch den einen Vorschau-Wert ersetzt. Das Punktbild (Dichte,
  Punktgröße, Edge Softness, Farben) bleibt danach erhalten. Die k-Liste wird
  nicht verändert. Im Play Mode geht das nur bei laufender Vorschau (`P`); es
  gilt sofort für `F5`/`F7` und wird nach dem Stoppen des Play Mode in die Szene
  geschrieben (Konsole meldet es, dann Szene mit Strg+S speichern).
  Beim ersten Übernehmen wird der bisherige Stand gesichert;
  **Originalwerte wiederherstellen** holt ihn zurück (auch nach mehrmaligem
  Übernehmen). Auf dem Lab-PC vor `git pull` die Originalwerte wiederherstellen
  oder die Szenenänderung verwerfen, sonst gibt es einen Konflikt in der Szene.

* `Free Head Movement` ist standardmäßig an: In aktiven Haupttrials darf der
  Kopf frei nach links/rechts, oben/unten oder schräg bewegt werden. Es gibt
  keine Sollbahn, Wende-, Amplituden- oder Geschwindigkeitsprüfung und keine
  Wiederholung wegen solcher Abweichungen. Blickkontrolle/Wiedervorlage bleibt
  davon unabhängig. Die Punktwelt ist endlich: `Head Turn Safety Degrees`
  beschreibt die Reserve um die Startblickrichtung, keine unbegrenzte 360°-Welt.

* Wenn `Free Head Movement` aus ist, bleibt der geführte Links-Rechts-Schwenk
  erhalten. Mit `±2°`,
  `1,2°/s` und `5 s` startet er in der Mitte, erreicht nach etwa `1,67 s` die
  erste Seite und nach `5 s` die andere Seite. An den Umkehrpunkten steht die
  Bewegung kurz.

* `Train Head Movement`: zeigt vor dem aktiven Block ein Kopfbewegungstraining
  mit blauem Sollmarker, gelbem Marker für den echten Kopf und kurzen Tönen
  an den Umkehrpunkten. `F5` startet das Training; `F6` bricht die Sitzung ab.
  Das geführte Training bleibt auch bei freien Haupttrials erhalten.
  Seine Dauer wird nötigenfalls bis zum zweiten Wendepunkt verlängert; eine
  kurze freie Haupttrial-Dauer wird dadurch nicht verboten. Nach dem Training
  erklären separate Instruktionen die tatsächlich gewählte Haupttrial-Regel.

* `Train Simulated Motion`: eigenes Training vor dem simulierten Block. Kopf
  stillhalten, Kreuz anschauen, kurze simulierte Bewegung sehen, dann antworten.
  Die Werte stehen unter `Simulated Training K Values`; Wiederholungen unter
  `Simulated Training Repeats Per Value`. Subjektive Formurteile erhalten keine
  vorgegebene Richtig-/Falsch-Rückmeldung und zählen nicht als Mess-Trials.

* `T` startet auch ohne Sitzung ein eigenständiges Training. Mit `Practice
  Motion Mode` wird Simulated oder Active gewählt. `F5` bestätigt die
  Trainingsinstruktionen, `F6` beendet das Training. Es entstehen keine
  Sitzungsdateien. In einer Messung wählt der Manager das Training passend zum
  jeweiligen Bewegungsblock; diese Übungen erhöhen die Haupttrial-Zähler nicht.

* `Show Reference Grid` in der Live-Vorschau zeigt ein gerades, kopffestes
  Karopapier-Raster. Es wird weder durch `k`, `m` oder Zoom verzerrt noch
  mitgeschwenkt. Die Punkte bewegen sich darüber. Abstand, Linienbreite und
  Farbe sind einstellbar. Das Raster bleibt während Training und Messung aus.

* `Required Good Training Sweeps`: Anzahl unmittelbar aufeinanderfolgender
  gelungener Übungsschwenks, voreingestellt auf vier. Die Anfangsrichtung
  wechselt zwischen den Versuchen.

* `Maximum Profile Error Degrees`: tolerierte mittlere Abweichung der
  tatsächlichen Kopfbewegung vom Sinusprofil, im Training und in geführten aktiven
  Haupttrials voreingestellt auf `0,9°`. Zusätzlich werden Auslenkung,
  Seitenwechsel und Geschwindigkeitsgrenzen geprüft.

* `Maximum Training Endpoint Error Degrees`: tolerierter Fehler an den beiden
  Umkehrpunkten während des Trainings, voreingestellt auf `0,9°`.

* `Session Motion Mode`: genau eine Bewegungsart pro Sitzung. `F5` startet diese
  Auswahl, `F7` startet die andere Variante als neue Sitzung. Beide Tasten sind
  einstellbar. `T` übt dieselbe Auswahl ohne Messdateien.

* `Head Turn Safety Degrees`: so weit darf der Kopf im `HeadTracked`-Block von
  der Blickrichtung zu Trialbeginn weg drehen (in jede Richtung), ohne dass am
  Rand eine leere Fläche erscheint; voreingestellt auf `30°`. Der Inspector zeigt
  darunter für jedes m, wie viele Punkte das braucht (Grenze 500.000). Gilt auch
  in der Vorschau, wenn dort `Motion Mode = HeadTracked` gewählt ist; `World
  Coverage` am Random Dot Field gilt nur für die Simulated-Vorschau.

* `Check Head Motion` (früher `Validate Head Tracked Motion`): wiederholt geführte aktive Durchgänge, wenn der
  Seitenwechsel fehlt oder Auslenkung beziehungsweise Geschwindigkeit außerhalb
  der eingestellten Grenzen liegen

* Random-Dot-Ergebnisse enthalten zusätzlich `head_motion_constraint`:
  `simulated`, `free` oder `guided`. Alle bisherigen CSV-Spalten bleiben an
  ihrer Position. Bei freien Haupttrials stehen Sollamplitude, Sollgeschwindigkeit,
  nominelle Bildmitten-Geschwindigkeit und Sinusfehler auf `NaN`, da es dort
  keine Sollbahn gibt. Die bestehenden Sweep-Messwerte beschreiben weiterhin
  Yaw (links/rechts), nicht die gesamte 3D-Bewegung. Soweit die Blickaufnahme
  aktiv ist, enthält sie zusätzlich die Kopfpose. Plan-Achse und Plan-Richtung
  sind bei `free` keine Bewegungsanweisung. Die Mapping-Version ist jetzt `v8`.

* `Eye Presentations`: beide, nur linkes oder nur rechtes Auge

* Fixations- und Wiederholungsgrenzen wie beim Checkerboard

Am `Random Dot Keyboard Controller` (am Objekt `Random Dot Field`) stehen die
Antworttasten: die beiden Tasten der Tastatur, `Use Vr Controller Buttons` und
`Vr Controller Mapping`.

Eine Sitzung enthält nur Simulated Panning ODER aktive Kopfbewegung, jeweils
als einen vollständigen gemischten Block ohne Unterblock-Pausen. Bei sieben
k-Stufen, einem FOV, einem Augenmodus, einem m, Zoom 1 und 25 Wiederholungen
sind das 175 gültige Trials pro Sitzung. Weitere eingestellte Bedingungen
multiplizieren die Anzahl; ungültige Trials werden zusätzlich wiederholt.
Danach endet die Sitzung und die Aufnahme wird gestoppt. `F7` startet die andere
Variante mit eigenem Plan, passenden Anweisungen/Training und neuen Messdateien.
Es gibt keine automatische Reihenfolge oder Zusammenführung zu 350 Trials;
die Reihenfolge der zwei Sitzungen wird im Versuchsprotokoll festgelegt.
Die kompatiblen CSV-Indizes `motion_block_index` und `mini_block_index` bleiben 1.
Die Schwenkrichtung
(beim Kopfschwenk die Richtung des ersten Schwenks) wechselt über die
Wiederholungen jeder Bedingung, sodass links und rechts möglichst gleich oft vorkommen;
durch das Mischen ist die Reihenfolge zufällig. Gleiche Wiederholungen verschiedener `k`-Stufen und beider
Bewegungsarten verwenden vergleichbare Punkt-Seeds, damit die Punktverteilung
nicht mit einer Bedingung verwechselt wird.

Vor dem ersten Head-Tracked-Trial eines Blocks erscheint die Anleitung im
Headset. Nach `F5` folgt die Person dem blauen Marker mit langsamen, kleinen
Kopfdrehungen und beobachtet den gelben Ist-Marker. Die Anzeige verlangt
zwischen Übungsdurchgängen eine Rückkehr zur Mitte. Nach vier passenden
Durchgängen erklärt eine weitere Instruktion die freie oder geführte Hauptbewegung;
`F5` startet danach die Trials. Das Display und die Töne erscheinen
im Hauptversuch nicht. Auch dort wird das tatsächliche Bewegungsprofil
kontrolliert; stark abweichende Versuche werden später wiederholt.

Nach einer Bewegungsphase verschwinden nur die Punkte. Der neutrale graue Kreis
und das Fixationskreuz bleiben während der Antwort, zwischen den Trials und in
den Blockpausen sichtbar. Dadurch entsteht kein Wechsel vom helleren Punktfeld
auf einen vollständig schwarzen Bildschirm. Außerhalb der Kreisöffnung bleibt
der Hintergrund in allen Phasen unverändert schwarz.

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

Mit `Simulated Speed Reference = Bildmitte` wird die Objektgeschwindigkeit für
jeden Trial so gewählt, dass die Punkte in der Bildmitte immer mit
`Simulated Image Center Speed` laufen:

    Objektgeschwindigkeit = Bildmitte-Geschwindigkeit / (m * Content Zoom)

Dieselbe Normierung gilt jetzt auch für die Live-Vorschau: Dort
`Preview Speed Reference = Bildmitte` und `Preview Image Center Speed` einstellen.
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

Mit `Objektwinkel` (bisherige Definition, `1,2°/s`) laufen die Punkte in der
Bildmitte dagegen mit `6`, `12`, `16,8` und `24°/s`.

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
`GrayScreenStarted`, `TrialResponse`, `TrialInvalid` und `TrialRepeatQueued`
verbinden den Versuchsablauf zeitlich mit den Rohdaten. `NoiseMaskStarted` und
`GrayScreenStarted` nennen mit `phase=response` oder `phase=pre_stimulus`, zu
welcher Phase die Darstellung gehört. Der gewählte Ablauf steht als
`trial_sequence` im Plan, in jeder Ergebniszeile und in den Markern.

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
