#if ENABLE_MONO || ENABLE_IL2CPP
#define UNITY_ENGINE
#endif

namespace SignalStreaming
{
    public static class DevelopmentOnlyLogger
    {
        /// <summary>
        /// Logs a message to the console 
        /// only when DEVELOPMENT_BUILD or UNITY_EDITOR is defined.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [
            System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), 
            System.Diagnostics.Conditional("UNITY_EDITOR"),
        ]
        public static void Log(object message)
#if UNITY_ENGINE
            => UnityEngine.Debug.Log(message);
#else
            => System.Console.WriteLine($"[DEBUG] {message}");
#endif

        /// <summary>
        /// Logs a warning message to the console 
        /// only when DEVELOPMENT_BUILD or UNITY_EDITOR is defined.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [
            System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), 
            System.Diagnostics.Conditional("UNITY_EDITOR"),
        ]
        public static void LogWarning(object message)
#if UNITY_ENGINE
            => UnityEngine.Debug.LogWarning(message);
#else
            => System.Console.WriteLine($"[WARNING] {message}");
#endif

        /// <summary>
        /// Logs an error message to the console 
        /// only when DEVELOPMENT_BUILD or UNITY_EDITOR is defined.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [
            System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), 
            System.Diagnostics.Conditional("UNITY_EDITOR"),
        ]
        public static void LogError(object message)
#if UNITY_ENGINE
            => UnityEngine.Debug.LogError(message);
#else
            => System.Console.WriteLine($"[ERROR] {message}");
#endif
    }
}
