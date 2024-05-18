namespace Sandbox.EngineLooper
{
    public sealed class LooperOptions
    {
        public bool AutoStart { get; set; }
        public int TargetFrameRate { get; set; }
        public int InitialActionsCapacity { get; set; }

        public LooperOptions()
        {
            AutoStart = false;
            TargetFrameRate = 60;
            InitialActionsCapacity = 4;
        }
    }
}