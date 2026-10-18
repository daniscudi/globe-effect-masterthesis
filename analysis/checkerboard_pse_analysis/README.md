# psignifit für Checkerboard und Random Dots

Das bestehende `fit_checkerboard_pse_psignifit.m` kann beide Trials-CSV-Typen
auswerten. Trotz des bisherigen Dateinamens erkennt es den Versuch automatisch.

In MATLAB:

```matlab
clear condition_filter
csv_path = 'D:/Pfad/zur/Teilnehmer_trials.csv';
run('D:/Tolga/Globe-Effect-Master/VRCheckerboard/analysis/checkerboard_pse_analysis/fit_checkerboard_pse_psignifit.m');
```

Oder das Skript ohne `csv_path` starten und die CSV im Dateidialog auswählen.
`psignifit_path` kann bei Bedarf ebenfalls im Workspace gesetzt werden.

Neues Checkerboard (ab Umstellung 18. Oktober): x-Achse `k`, festes `m`,
Ergebnis `pse_instrument_distortion_k`. Erkennung über `stimulus_task=Checkerboard`
und die Mappingkennung; ein k-Feld allein bedeutet nicht mehr Random Dots.
Ausgaben beginnen mit `checkerboard_instrument`, damit historische Ergebnisse
nicht überschrieben werden. Verschiedene m werden getrennt gefittet.

Ab 19. Oktober tragen Checkerboard-CSVs die Kennung
`merlitz-instrument-checkerboard-fixed-source-grid-v2` und die letzte Spalte
`checkerboard_inverted` (0/1, schwarze und weiße Felder vertauscht; nur mit der
Option `Invert Checkerboard Each Trial` auch 1). Mit ausgeschalteter Option ist das
Bild identisch zu v1. Die Fits ignorieren die Spalte und poolen beide Polaritäten.
Einen Fit je Polarität liefert der vorhandene Filter, z. B.
`condition_filter = struct('checkerboard_inverted', 0);`. v1- und v2-Sitzungen sind
verschiedene Sitzungen und werden deshalb ohnehin getrennt ausgewertet.

Historisches Checkerboard: x-Achse `l`, Ergebnis `pse_visual_space_l`.
Das betrifft insbesondere Yannicks alten `pilot_ys` (PSE ca. 0,872).
Keine automatische Umrechnung und keine Zusammenfassung alter/neuer Abbildungen.
Gemischte `mapping_version` werden als Fehler zurückgewiesen.

Random Dots: x-Achse `k`, Ergebnis `pse_instrument_distortion_k`.
Fit: Wahrscheinlichkeit einer Convex-Antwort, PSE bei 50 %, 95%-Credible-Intervall.
`k = 1` ist die Tangensbedingung des Instruments, kein vorausgesetzter
wahrgenommener Neutralpunkt. Es findet keine l/k-Umrechnung statt.

Nur gültige Concave/Convex-Antworten werden verwendet. Verschiedene Personen,
Sitzungen, FOVs, Augenmodi, Bewegungsarten, Vergrößerungen oder Sollgeschwindigkeiten
werden nicht ungeprüft zusammen ausgewertet. Wiederholte ungültige Versuche zählen
nicht; doppelte gültige `sequence_index` werden abgewiesen.

Neue Random-Dot-CSVs (ab 7. Oktober) enthalten nur eine Bewegungsart pro
Sitzung: `F5` speichert z. B. Simulated Panning, `F7` aktive Kopfbewegung in
einer eigenen CSV. Jede Datei ergibt einen Fit.

Ältere Random-Dot-CSVs (bis 7. Oktober, z. B. 350 Trials mit `SimulatedYaw`
und `HeadTracked` in einer Datei) werden automatisch aufgeteilt: Jede
Bewegungsart wird getrennt gefittet und bekommt eigene PNG/XLSX-Dateien.
Zusätzlich entsteht `random_dot_pse_summary_psignifit.xlsx` mit allen PSEs.
Eine Bewegungsart ohne ausreichende Daten wird mit Warnung übersprungen.
Verschiedene Personen oder Sitzungen in einer Datei bleiben ein Fehler.

Wurde während einer Sitzung etwas umgestellt (FOV, m, Zoom, Geschwindigkeit,
Edge Softness, Augenmodus, … – alles, was die CSV pro Trial speichert), wird
trotzdem gemeinsam gefittet. Eine Warnung nennt je Spalte die Werte mit
Trialzahl, die XLSX enthält z. B. `60 (80); 70 (95)`, der Plot zeigt `mixed`.
Gemischte Spalten erscheinen nicht im Dateinamen. Nur einen Wert auswerten:
`condition_filter = struct('angular_diameter_deg', 70);`

Nur eine Bedingung einer gemischten CSV auswerten:

```matlab
condition_filter = struct('motion_mode', 'SimulatedYaw', ...
    'angular_diameter_deg', 60, 'instrument_magnification_m', 10);
```

Für aktive Bewegung lautet `motion_mode` `HeadTracked`. Vor einer anderen
Auswertung `clear condition_filter` verwenden oder den Filter neu setzen.
PNG und XLSX werden im Ordner der Eingabe-CSV gespeichert. Random-Dot-Dateinamen
enthalten die Bedingung, damit Simulated/Active-Fits sich nicht überschreiben.
Mindestens drei Reizstufen und beide Antwortkategorien sind notwendig; dies allein
garantiert noch keinen zuverlässigen PSE. Warnungen zur 50%-Abdeckung beachten.
