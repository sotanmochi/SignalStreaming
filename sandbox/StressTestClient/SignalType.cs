namespace Sandbox.StressTest
{
    public enum SignalType
    {
        PlayerObjectColor = 1,
        PlayerObjectPosition = 2,
        PlayerObjectRotation = 3,
        PlayerObjectQuantizedPosition = 4,
        PlayerObjectQuantizedRotation = 5,
        QuantizedHumanoidPose = 6,

        // Stress Test Manager
        ChangeStressTestState = 90,
        ChangeColor = 91
    }
}