namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Legt fest, wodurch der horizontale Schwenk des Punktfelds entsteht.
    /// Im Versuch wird die reale HMD-Bewegung verwendet. Die Simulation ist
    /// ausschließlich für Entwicklung und Vorführung ohne Headset gedacht.
    /// </summary>
    public enum RandomDotMotionMode
    {
        HeadTracked = 0,
        SimulatedYaw = 1
    }
}
