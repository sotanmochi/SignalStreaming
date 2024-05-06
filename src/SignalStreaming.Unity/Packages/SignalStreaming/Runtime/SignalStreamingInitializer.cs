#if ENABLE_MONO || ENABLE_IL2CPP
#define UNITY_ENGINE
#endif

using SignalStreaming.Serialization;

namespace SignalStreaming
{
    public static class SignalStreamingInitializer
    {
        static bool _isInitialized = false;

#if UNITY_ENGINE
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
#elif NET5_0_OR_GREATER
        [System.Runtime.CompilerServices.ModuleInitializer]
#endif
        public static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            RegisterSignalFormatters();
        }

        static void RegisterSignalFormatters()
        {
            SignalFormatterProvider.Register(new ClientConnectionRequestFormatter());
            SignalFormatterProvider.Register(new ClientConnectionResponseFormatter());
            SignalFormatterProvider.Register(new GroupJoinRequestFomatter());
            SignalFormatterProvider.Register(new GroupJoinResponseFormatter());
            SignalFormatterProvider.Register(new GroupLeaveRequestFormatter());
            SignalFormatterProvider.Register(new GroupLeaveResponseFormatter());
        }
    }
}