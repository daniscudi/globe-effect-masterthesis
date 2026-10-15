# Natural photo preview: Puno

Open Assets/GlobeEffect/Demo/NaturalPhotoPreview.unity and press Play.
Separate preview only: no trials, eye tracking, responses or data collection.
The earlier Field 01 panorama scene remains available and keeps its defaults.

## Controls

- Defaults: Active / HeadTracked, magnification 10, k = 1, 60-degree circular aperture,
  1-degree soft edge, neutral gray outside, red fixation cross.
- Move the head freely. At the desktop use arrow keys (20 degrees/s).
- R recenters the source photo on the current head direction and restarts simulated motion.
- Inspector: k, magnification, FOV, edge softness, background and fixation update live.
- Photo Rotation Degrees changes the source orientation. Default pitch -7 degrees
  selects the city roofs rather than the sky/lake at the photograph's center.
- Switching k or magnification does not automatically move the source photo with the head.
- SimulatedYaw is also available; Space pauses only simulated movement, not real head tracking.
- Context menu / inspector button: Center Photo On Current Head Direction (Play Mode).

## Photo limits

The source covers about 39.41 x 26.86 degrees, estimated from EXIF, not calibrated.
At m=10, k=1, FOV=60, the visible source diameter is about 6.61 degrees.
A horizontal head sweep of roughly +/-10 degrees has reserve with these defaults.
Vertical reserve is asymmetric because the initial view is aimed down at the roofs.
This is not a prescribed head path or a hard head-movement limit.

Outside the photo the chosen background is shown. Pixels are never repeated or
stretched to invent more scenery. A thin amber aperture ring warns when the
visible source region reaches the photo boundary. The desktop also shows PHOTO EDGE.
The boundary test samples the center and 64 boundary directions, so it is a diagnostic,
not an exact pixelwise coverage guarantee. R or turning back restores the view.
The warning can be disabled; it is a preview aid, never a planned measurement cue.

## Scientific limits

Same inverse instrument formula as the existing natural panorama / random-dot family:
tan(k a) = m tan(k A). No perceptual l model is added.
A direction-only image removes artificial near disparity and translation parallax.
Perspective, shading and other photographic depth cues remain.
A real three-dimensional environment is not reconstructed.

The image is licensed CC BY 2.0; source, author and geometry estimate are in
Assets/GlobeEffect/Demo/NaturalPhoto/ATTRIBUTION.md.
A higher-detail headset may still reveal the source resolution limit.
Practical headset validation remains necessary.

## Verification

23 focused Unity tests passed, including existing panorama tests and new photo
projection / source import / Play Mode / GPU image comparisons. Verified: full
6000 x 4000 import, visible detail at 10x, k changes, a 10-degree horizontal
head turn, no translation parallax, gray outside the photo, recentering and
disable/enable. Normal, instanced and stereo-instanced shader variants compile.
No practical headset session was performed.
