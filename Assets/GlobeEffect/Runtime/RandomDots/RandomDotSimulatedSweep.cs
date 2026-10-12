using System;

namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Rechnet aus, wo der Schwenk gerade steht. Der Winkel kann im Shader als
    /// horizontaler Yaw oder vertikaler Pitch benutzt werden.
    ///
    /// Es gibt zwei Bahnen:
    /// - Einseitig: Im SimulatedYaw-Block schwenkt das Feld einmal mit fester
    ///   Geschwindigkeit von der einen Seite zur anderen und kehrt nicht um.
    /// - Hin und her: Im HeadTracked-Block folgt die Person mit dem Kopf einer
    ///   Sinusbahn. Die startet in der Mitte, erreicht nach einem Viertel der
    ///   Periode eine Seite und nach drei Vierteln die Gegenseite. An beiden
    ///   Umkehrpunkten ist die Geschwindigkeit null.
    ///
    /// Hier steht nur Mathematik drin und nichts von Unity. Deshalb kann man das
    /// ohne Headset testen.
    /// </summary>
    public static class RandomDotSimulatedSweep
    {
        /// <summary>
        /// Der einseitige Schwenk. Er startet eine Amplitude vor der Mitte und
        /// endet eine Amplitude dahinter. So liegt die Mitte der Bewegung genau
        /// geradeaus, und die Punktwelt muss zu beiden Seiten gleich weit reichen.
        /// Ist die Strecke geschafft, bleibt der Winkel am Ende stehen.
        /// </summary>
        public static float EvaluateOneWayDegrees(
            double elapsedSeconds, float amplitudeDegrees, float speedDegreesPerSecond,
            RandomDotSweepDirection direction)
        {
            if (amplitudeDegrees <= 0f || speedDegreesPerSecond <= 0f)
            {
                return 0f;
            }

            // Zurückgelegte Strecke seit dem Start, höchstens aber die ganze Strecke
            // von einer Seite zur anderen, also zwei Amplituden.
            double travelled = Math.Min(
                Math.Max(0d, elapsedSeconds) * speedDegreesPerSecond, 2d * amplitudeDegrees);
            float angle = (float)travelled - amplitudeDegrees;

            // RightFirst heißt hier einfach: Das Fernglas schwenkt nach rechts
            // (bzw. nach oben), die Punkte wandern dabei zur anderen Seite.
            return direction == RandomDotSweepDirection.LeftFirst ? -angle : angle;
        }

        /// <summary>
        /// Die Sinusbahn hin und her, der die Person im HeadTracked-Block mit dem
        /// Kopf folgt.
        /// </summary>
        public static float EvaluateBackAndForthDegrees(
            double elapsedSeconds, float amplitudeDegrees, float speedDegreesPerSecond,
            RandomDotSweepDirection direction)
        {
            if (elapsedSeconds <= 0d || amplitudeDegrees <= 0f || speedDegreesPerSecond <= 0f)
            {
                return 0f;
            }

            // In einer Periode werden vier Amplituden zurückgelegt. Der Speed-
            // Parameter bezeichnet die mittlere absolute Geschwindigkeit und
            // bleibt dadurch mit der bisherigen Einstellung vergleichbar.
            double phase =
                elapsedSeconds * Math.PI * speedDegreesPerSecond / (2.0 * amplitudeDegrees);
            float angle = amplitudeDegrees * (float)Math.Sin(phase);

            // Das Vorzeichen bestimmt rechts/links bzw. oben/unten zuerst.
            return direction == RandomDotSweepDirection.LeftFirst ? -angle : angle;
        }

        /// <summary>
        /// Wie schnell das Instrument im simulierten Schwenk dreht, in Grad
        /// Objektwinkel pro Sekunde. Das ist der Wert, den der Shader braucht.
        ///
        /// Drei Geschwindigkeiten muss man auseinanderhalten:
        /// - Objektwinkel: wie schnell das Instrument über die Außenwelt schwenkt.
        /// - Sichtbarer Bildwinkel: wie schnell ein Punkt im Bild wandert. In der
        ///   Bildmitte ist das bei jedem k genau m * Content Zoom mal so schnell
        ///   wie der Objektwinkel. Zum Rand hin wird es je nach k schneller oder
        ///   langsamer; dieser Unterschied ist der eigentliche Reiz.
        /// - Kartesische Bildkoordinate (Tangens des Bildwinkels): In der Bildmitte
        ///   gleich dem Bildwinkel, zum Rand hin wächst sie zusätzlich mit
        ///   1 / cos².
        ///
        /// ImageCenter rechnet die gewünschte sichtbare Geschwindigkeit der
        /// Bildmitte auf den Objektwinkel zurück. Dadurch laufen die Punkte in der
        /// Bildmitte bei jedem m gleich schnell, und das Verhältnis zwischen Mitte
        /// und Rand bleibt unberührt.
        /// </summary>
        public static float ObjectSpeed(
            RandomDotSweepSpeedReference reference, float objectSpeedDegreesPerSecond,
            float imageCenterSpeedDegreesPerSecond, float magnificationM, float contentZoom)
        {
            return reference == RandomDotSweepSpeedReference.ImageCenter
                ? imageCenterSpeedDegreesPerSecond / (magnificationM * contentZoom)
                : objectSpeedDegreesPerSecond;
        }

        /// <summary>
        /// Die sichtbare Winkelgeschwindigkeit in der Bildmitte, die zu einer
        /// Objektgeschwindigkeit gehört.
        /// </summary>
        public static float ImageCenterSpeed(
            float objectSpeedDegreesPerSecond, float magnificationM, float contentZoom)
        {
            return objectSpeedDegreesPerSecond * magnificationM * contentZoom;
        }

        public static string DirectionLabel(
            RandomDotMotionMode mode, RandomDotSweepAxis axis, RandomDotSweepDirection direction)
        {
            // Der simulierte Schwenk geht nur in eine Richtung, deshalb heißt er
            // einfach "Right" oder "Up". Der Kopfschwenk geht hin und her, da
            // zählt, wohin es zuerst geht: "RightFirst" oder "LeftFirst".
            bool positive = direction == RandomDotSweepDirection.RightFirst;
            string side = axis == RandomDotSweepAxis.Vertical
                ? positive ? "Up" : "Down"
                : positive ? "Right" : "Left";
            return mode == RandomDotMotionMode.SimulatedYaw ? side : side + "First";
        }
    }
}
