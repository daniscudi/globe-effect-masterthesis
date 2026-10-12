# Natürliche Szene: Field 01

Öffne Assets/GlobeEffect/Demo/NaturalScenePreview.unity und drücke Play.
Die Vorschau ist separat: keine Teilnehmerdaten, Trainingsabläufe oder CSVs.

- Alle Parameter sind im Inspector live einstellbar.
- m = 1, k = 1: Überblick. m = 10 oder 20: vergrößerter Ausschnitt.
- k verwendet dieselbe Instrumentenformel wie der Random-Dot-Shader.
- SimulatedYaw: sinusförmiger horizontaler oder vertikaler Schwenk mit fester Ausgangsblickrichtung.
- HeadTracked: freie Kopfbewegung in der Welt; am Desktop Pfeiltasten.
- Leertaste pausiert, R setzt Bewegung und Ausgangsblickrichtung zurück.
- FOV ist die runde Öffnung, kein zusätzlicher Kamera-Zoom.
- Background Color bestimmt die Farbe außerhalb der Öffnung.
- Edge Softness blendet den Rand nach innen aus.
- Die virtuelle Schwenkgeschwindigkeit und Amplitude werden durch m × Content Zoom geteilt.
  Damit bleibt die Geschwindigkeit im Bildzentrum vergleichbar, nicht überall im Feld identisch.

Das 8192 × 4096 große 360°-Foto ist keine rekonstruierte 3D-Umgebung.
Keine Translationsparallaxe oder Nahdisparität; fotografische Tiefenhinweise bleiben vorhanden.
Hohe Vergrößerung kann wegen der begrenzten Fotoauflösung unscharf sein.
Die Abbildung ist eine technische Instrumentenfamilie, kein eingebautes Wahrnehmungsmodell.
Headset-Abnahme bleibt erforderlich.

Technische Prüfung: 14 Unity-Tests bestanden (Mathematik, Assets, Play-Modus,
Live-Einstellungen und gerendertes Bild). Shader für normale, instanzierte und
Stereo-instanzierte Darstellung kompiliert. Die bestehenden Experimente wurden nicht geändert.
