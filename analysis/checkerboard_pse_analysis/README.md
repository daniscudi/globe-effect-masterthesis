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

Checkerboard: x-Achse `l`, Ergebnis `pse_visual_space_l`.
Random Dots: x-Achse `k`, Ergebnis `pse_instrument_distortion_k`.
Fit: Wahrscheinlichkeit einer Convex-Antwort, PSE bei 50 %, 95%-Credible-Intervall.
`k = 1` ist die Tangensbedingung des Instruments, kein vorausgesetzter
wahrgenommener Neutralpunkt. Es findet keine l/k-Umrechnung statt.

Nur gültige Concave/Convex-Antworten werden verwendet. Verschiedene Personen,
Sitzungen, FOVs, Augenmodi, Bewegungsarten, Vergrößerungen oder Sollgeschwindigkeiten
werden nicht ungeprüft zusammen ausgewertet. Wiederholte ungültige Versuche zählen
nicht; doppelte gültige `sequence_index` werden abgewiesen.

Für ältere CSVs mit mehreren Bedingungen zuerst eine Bedingung wählen:

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
