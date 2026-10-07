using System;
using UnityEngine;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    public enum RandomDotDiagnosticCoordinates
    {
        [InspectorName("A: flacher Bildraum (u, v)")]
        LinearImage = 0,
        [InspectorName("S: getrennte Winkelumrechnung")]
        SeparateAngles = 1,
        [InspectorName("M: radiale Winkelumrechnung (l = 0)")]
        RadialAngles = 2
    }

    /// <summary>
    /// Geschwindigkeits-Messung für die Diagnose, keine zusätzliche Reizverzeichnung.
    /// Start: Bildstrahl (u,v). Über die vorhandene inverse Instrumentengleichung
    /// zur Außenwelt, dort ein winziger echter Schwenk, anschließend wieder ins Bild.
    /// Die Differenz liefert die momentane Koordinatenrate pro Radiant Schwenk.
    /// S/M sind die beiden Umrechnungen aus den Python-Plots, kein gemessenes Sehen.
    /// </summary>
    public static class RandomDotDiagnosticFlowMath
    {
        public static (double x, double y) Velocity(double u, double v, double m, double k,
            RandomDotSweepAxis axis, RandomDotDiagnosticCoordinates coordinates)
        {
            double radius = Math.Sqrt(u * u + v * v);
            double angle = MerlitzBinocularReferenceMath.ObjectAngleFromApparent(Math.Atan(radius), m, k);
            double scale = radius > 1e-12 ? Math.Tan(angle) / radius : 1d / m;
            double x = u * scale;
            double y = v * scale;
            const double step = 1e-5;
            var before = Project(x, y, -step, m, k, axis, coordinates);
            var after = Project(x, y, step, m, k, axis, coordinates);
            return ((after.x - before.x) / (2d * step),
                (after.y - before.y) / (2d * step));
        }

        private static (double x, double y) Project(double x, double y, double sweep,
            double m, double k, RandomDotSweepAxis axis, RandomDotDiagnosticCoordinates coordinates)
        {
            double cosine = Math.Cos(sweep);
            double sine = Math.Sin(sweep);
            double z;
            if (axis == RandomDotSweepAxis.Horizontal)
            {
                z = x * sine + cosine;
                x = x * cosine - sine;
            }
            else
            {
                z = y * sine + cosine;
                y = y * cosine - sine;
            }
            x /= z;
            y /= z;
            double radius = Math.Sqrt(x * x + y * y);
            double apparentAngle = MerlitzBinocularReferenceMath.ApparentAngleFromObject(
                Math.Atan(radius), m, k);
            double scale = radius > 1e-12 ? Math.Tan(apparentAngle) / radius : m;
            x *= scale;
            y *= scale;
            if (coordinates == RandomDotDiagnosticCoordinates.SeparateAngles)
                return (Math.Atan(x), Math.Atan(y));
            if (coordinates == RandomDotDiagnosticCoordinates.RadialAngles)
            {
                radius = Math.Sqrt(x * x + y * y);
                scale = radius > 1e-12 ? Math.Atan(radius) / radius : 1d;
                return (x * scale, y * scale);
            }
            return (x, y);
        }

        public static double RelativeSpeed(double u, double v, double m, double k,
            RandomDotSweepAxis axis, RandomDotDiagnosticCoordinates coordinates)
        {
            var velocity = Velocity(u, v, m, k, axis, coordinates);
            return Math.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y) / m;
        }
    }
}
