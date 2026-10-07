# Checkerboard: vom Teststand zum Pilotdurchlauf

Stand der Codeprüfung: 5. Oktober 2026. Die Präsentation wurde nicht verändert.

Prüfergebnis: 157/157 Unity-Tests bestanden, einschließlich Ablauf- und GPU-Noise-Tests.
psignifit reproduziert den alten Piloten an einer Kopie mit PSE 0,8721 und
95%-Glaubwürdigkeitsintervall 0,8100–0,9368. Zusätzliche MATLAB-Prüfungen bestätigen,
dass gemischte FOVs und doppelte gültige Trial-Nummern abgelehnt werden.
Diese Prüfung ersetzt nicht die Abnahme im Headset mit realem Eye-Tracking.

## Einschätzung

Der Versuch muss nicht neu gebaut werden. Plan, Training, beide Abläufe, Controller-Antworten,
Fixationsprüfung, Wiederholungen und fortlaufendes CSV-Speichern sind vorhanden.
Die verbleibende Arbeit ist vor allem: ein festes Protokoll wählen und den kompletten Ablauf
auf dem Messrechner abnehmen. Eine fehlerfreie Rechnung allein prüft weder das Headset noch
die Kalibrierung oder die tatsächlich am Display gezeigte Dauer.

## Wo stelle ich was ein?

Der Experiment Manager ist jetzt die Einstellungszentrale mit aufklappbaren Gruppen.
Die verknüpften Einstellungen werden dort direkt an ihrer ursprünglichen Komponente geändert.
Es gibt keine zusätzliche Kopie, die auseinanderlaufen könnte.

| Einstellung | Zuständige Komponente | Im Manager-Inspector |
| --- | --- | --- |
| Person, Seed, FOV, Augen, Verzerrungsstufen, Wiederholungen | CheckerboardExperimentManager | Sitzung und Versuchsplan |
| Darbietungsdauer, Antwortzeit, Ablauf A/B, Vorphase | CheckerboardExperimentManager | Ablauf und Zeiten |
| Blicktoleranz und ruhige Fixationsdauer | CheckerboardFixationMonitor | Blickkontrolle, verknüpft |
| Erlaubtes Wegschauen, fehlende Daten, Wiederholungsgrenze | CheckerboardExperimentManager | Blickkontrolle |
| Karogröße, Farben, Kreuz, Rand, Noise | VrCheckerboardStimulus | Muster, Fixationskreuz und Noise, verknüpft |
| Antworttasten und Controller-Zuordnung | Manager; Tastaturtausch am Controller | Antworten und Tasten |
| Tracker-Auswahl | EyeTrackingToolbox | Blickkontrolle, verknüpft |

FOV, l und Augenmodus am Stimulus sind **Vorschauwerte**. Im Experiment setzt der Manager
die Werte aus dem Trial Plan. Ein Stimulus-Wert ersetzt also nicht die Liste im Manager.
Bei Random Dots gilt dasselbe für FOV, k, m, Content Zoom und Bewegung.
Punktdichte, Punktgröße und Farben gehören dort weiterhin zum Stimulus; die Antwortbelegung
gehört zum RandomDotKeyboardController und ist im Manager-Inspector ebenfalls zugänglich.

Die Einstellungen in dieser zentralen Ansicht sind während einer Sitzung gesperrt;
auch den eigenständigen Stimulus- oder Toolbox-Inspector während einer Messung nicht verändern.
Laufende Statuswerte sind in der zentralen Ansicht tatsächlich schreibgeschützt.

## Aktuell gespeicherte Checkerboard-Szene

Dies sind die vorgefundenen Werte, keine Empfehlung für das endgültige Protokoll:

- 60° FOV, beide Augen.
- l = 0,2 / 0,6 / 0,7 / 0,8 / 0,9 / 1,0 / 1,2; 15 Wiederholungen = 105 gültige Trials.
- Muster 800 ms; Antworttimeout 50 s; Vorphase mindestens 500 ms.
- Ablauf B: Muster → Grau mit Antwort → Noise vor dem nächsten Muster.
- Require Fixation AUS; verpflichtendes Training AUS.
- Noise Refresh Rate = 0 Hz: **statische** Maske, kein zeitliches Flimmern.
- Trackpad = konvex; Trigger = konkav.

Die Szene wurde bewusst nicht auf ein vermutetes Wunschprotokoll umgestellt.
25 Wiederholungen pro sieben Stufen ergeben 175 gültige Trials. Antworttimeout 0 bedeutet
unbegrenzte Antwortzeit. Wenn die Maske flimmern soll, muss ihre Refresh Rate größer als 0 sein.
F5 bereitet die Sitzung vor; die Weiter-Taste startet danach den eigentlichen Versuch.

## Vor dem nächsten echten Piloten entscheiden

1. **Ein Ablauf für alle festlegen.** A maskiert direkt nach dem Schachbrett;
   B zeigt zunächst Grau und maskiert erst vor dem nächsten Trial. B ist daher nicht dieselbe
   unmittelbare Nachmaske. Bei A hängt die Maskendauer von der Antwortzeit ab.
2. **Blickkontrolle zeitlich festlegen.** Momentan gilt sie beim Checkerboard während Muster
   und Antwortphase. Bei Random Dots nur während der Bewegung. Beim Checkerboard kann daher
   auch ein langes Blinzeln während des Antwortens eine Wiederholung auslösen.
   Falls nur die Musterphase kontrolliert werden soll, wäre das eine bewusste Protokolländerung.
3. **Stufen und Wiederholungen festlegen.** Der Pilot sollte Antworten auf beiden Seiten
   des 50%-Punkts und mehrere Stufen in seiner Nähe liefern. Mehr Trials allein ersetzen
   keine passend gewählten Stufen. Gleiche geplante Wiederholungszahl je Stufe beibehalten.
4. **Seed und Tastenbelegung dokumentieren.** Unterschiedliche Seeds je Person sind sinnvoll;
   alle Seeds werden gespeichert. Eine vertauschte Controller-Zuordnung muss im Training
   genau so gezeigt werden und bei beiden Aufgaben zur vorgesehenen Gegenbalancierung passen.
5. **Pausen und Helligkeit berücksichtigen.** Pausenregel vorher festlegen. Das eingestellte
   Grau ist eine softwareseitige Näherung, keine gemessene mittlere Displayluminanz.
   Für Aussagen über gleiche Helligkeit braucht es eine Displayprüfung/Kalibrierung.

## Technische Abnahme vor einer Person

Zuerst kurzer Dummy-Durchlauf; das ist ein Funktionstest, kein Nachweis für kontrollierte Fixation.

- Training vollständig durchgehen; Texte und Controller-Zuordnung tatsächlich lesen.
- Antworten während Muster und Vorphase zählen nicht; nach dem Muster zählt nur die erste Antwort.
- Beide Controller-Zuordnungen testen. Halten einer Taste darf keinen zweiten Trial beantworten.
- Gewählten Ablauf, Vorphasendauer und statische/flimmernde Noise im Headset ansehen.
- Einmal mit F6 abbrechen: vorhandene Antworten müssen gespeichert bleiben.
- Sitzung vollständig beenden: Anzahl gültiger Antworten muss dem Plan entsprechen.

Danach mit dem tatsächlichen Tracker und aktivierter Blickkontrolle:

- Kalibrieren und prüfen, ob ein Blick aufs Kreuz als gültig und zentral erkannt wird.
- Wegschauen oder länger fehlende Daten müssen einen ungültigen Trial erzeugen.
- Der ungültige Versuch bleibt in der CSV; seine Wiederholung wird hinten angehängt.
- Keine Darbietung darf allein wegen eines alten Blickwerts starten.
- Blickaufzeichnung nicht mit F9 beenden; bei Require Fixation bricht der Checkerboard-Manager
  jetzt ab, wenn die Aufnahme während der Sitzung gestoppt wurde.
- Console auf Schreibfehler prüfen; gaze/head-Dateien müssen tatsächlich Daten enthalten.
- Gespeicherte Stimulusdauern auf größere Abweichungen prüfen. Die CSV misst Softwarezeiten,
  nicht den physischen Lichtbeginn am Display. Dafür wäre eine Photodiode nötig.

## Was pro Sitzung gespeichert wird

- `*_plan.csv`: festgelegte Bedingungen und randomisierte Reihenfolge.
- `*_trials.csv`: jede Darbietung, auch ungültige/abgebrochene, mit tatsächlichen Softwarezeiten.
- `*_settings.json`: Einstellungen beim Start, einschließlich Fixationskriterium, Noise,
  Training, Antwortbelegung, Unity-Version, Farbraum und aktivem Eye-Tracker.
- `*_gaze.csv` und `*_head.csv`: Aufzeichnung der Lab-Toolbox, wenn diese vorhanden ist.

Die bestehenden CSV-Spalten bleiben erhalten. In der JSON sind Unity-Objektverweise
lokale Instance-IDs; sie ersetzen keine versionierte Szene. Der Versuchsplan ist für die
Trial-Bedingungen maßgeblich, nicht die Stimulus-Vorschau im JSON.
Die Wiederholungsgrenze zählt im bisherigen Code alle Darbietungen einschließlich des
Erstversuchs; 0 bedeutet unbegrenzt. Das ist im neuen Inspector ausdrücklich erklärt.

## PSE nach dem Piloten

In MATLAB `fit_checkerboard_pse_psignifit` ausführen und die gewünschte `*_trials.csv` auswählen.
Falls `csv_path` schon im Workspace gesetzt ist, verwendet das Skript diesen Pfad.
Für eine neue Dateiauswahl vorher `clear csv_path` ausführen.
Den psignifit-Pfad bei Bedarf anpassen oder `psignifit_path` im Workspace setzen.

Das Skript:

- nimmt nur gültige Concave/Convex-Antworten;
- lehnt vermischte Personen/Sitzungen, FOVs, Augenmodi, Abbildungen und Abläufe ab;
- lehnt doppelte gültige Trial-Nummern ab;
- warnt, wenn die beobachteten Anteile den 50%-Punkt nicht abdecken oder das Intervall
  über den getesteten Bereich hinausgeht;
- speichert PSE, 95%-**Glaubwürdigkeitsintervall**, JND, Eingabepfad und Bedingung plus Grafik.

PSE bedeutet hier: geschätzter Wert mit 50% Convex-Antworten, nicht ein direkt eingegebener
"gerade"-Wert. Die automatisch gewählte Kurvenrichtung und die Annahme symmetrischer
Fehlerraten sind Auswertungsentscheidungen. Für die Hauptstudie vorher festlegen und nicht
erst nach Ansicht interessanter Ergebnisse ändern.

Ein schmaleres Intervall bedeutet nicht automatisch einen unverzerrten Versuch. Training,
Fixation, Antwortbelegung, Reizqualität und die passende Modellwahl müssen ebenfalls stimmen.
