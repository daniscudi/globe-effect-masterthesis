namespace GlobeEffect.VRCheckerboard.RandomDots
{
    /// <summary>
    /// Legt fest, wodurch der horizontale Schwenk des Punktfelds entsteht.
    /// SimulatedYaw ist die kontrollierte Hauptbedingung: Der Stimulus bleibt
    /// kopffest und Unity bewegt die Punkte. HeadTracked bleibt als optionaler
    /// Vergleichs- und Demonstrationsmodus erhalten.
    /// </summary>
    public enum RandomDotMotionMode
    {
        HeadTracked = 0,
        SimulatedYaw = 1
    }
}
