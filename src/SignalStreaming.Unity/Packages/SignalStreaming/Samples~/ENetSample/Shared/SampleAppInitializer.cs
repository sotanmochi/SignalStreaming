using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace SignalStreaming.Samples.ENetSample
{
    class SampleAppInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            RegisterResolvers();
            Debug.Log($"<color=lime>[{nameof(SampleAppInitializer)}] Initialized</color>");
        }

        static void RegisterResolvers()
        {
            Debug.Log($"<color=lime>[{nameof(SampleAppInitializer)}] RegisterResolvers</color>");

            // NOTE:
            // Currently, CompositeResolver doesn't work on Unity IL2CPP build.
            // Use StaticCompositeResolver instead of it.
            StaticCompositeResolver.Instance.Register(
                SignalStreamingGeneratedResolver.Instance,
                BuiltinResolver.Instance,
                PrimitiveObjectResolver.Instance,
                StandardResolver.Instance
            );

            MessagePackSerializer.DefaultOptions =
                MessagePackSerializer.DefaultOptions.WithResolver(StaticCompositeResolver.Instance);
        }
    }
}
