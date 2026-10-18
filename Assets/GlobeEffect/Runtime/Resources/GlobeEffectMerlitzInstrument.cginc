#ifndef GLOBE_EFFECT_MERLITZ_INSTRUMENT_INCLUDED
#define GLOBE_EFFECT_MERLITZ_INSTRUMENT_INCLUDED

// Gemeinsame Instrumentengleichung für Checkerboard und Random-Dot-Mittelpunkte.
// Merlitz (2010), Gl. 4–6: tan(k*a) = m*tan(k*A). Kein Wahrnehmungsparameter l.
// Der Aufrufer prüft den eindeutigen Winkelbereich vor dem Zeichnen.
float MerlitzApparentAngle(float objectAngle, float magnification, float k)
{
    if (k < 1e-6) return magnification * objectAngle;
    return atan(magnification * tan(min(k * objectAngle, 1.560796))) / k;
}

float MerlitzObjectAngle(float apparentAngle, float magnification, float k)
{
    if (k < 1e-6) return apparentAngle / magnification;
    return atan(tan(min(k * apparentAngle, 1.560796)) / magnification) / k;
}

#endif
