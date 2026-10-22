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

// Nur Checkerboard: eigene Erweiterung, KEIN negatives Merlitz-k.
// HLSL/Cg hat nicht überall tanh/atanh; für x >= 0 ist diese Exp-Fassung stabil.
float CheckerboardExtendedObjectAngle(float apparentAngle, float magnification, float setting)
{
    if (setting >= 0.0) return MerlitzObjectAngle(apparentAngle, magnification, setting);
    float kappa = -setting;
    if (kappa < 1e-6) return apparentAngle / magnification;
    float x = kappa * apparentAngle;
    float exponential = exp(-2.0 * x);
    // Reihen nahe null vermeiden die Auslöschung von 1-exp(-2*x).
    float tanhX = x < 0.01 ? x * (1.0 - x*x/3.0 + 2.0*x*x*x*x/15.0)
        : (1.0 - exponential) / (1.0 + exponential);
    float value = tanhX / magnification;
    float inverse = value < 0.001 ? value + value*value*value/3.0
        : 0.5 * log((1.0 + value) / (1.0 - value));
    return inverse / kappa;
}

#endif
