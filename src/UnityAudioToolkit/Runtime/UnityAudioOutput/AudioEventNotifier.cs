using R3;
using UnityEngine;

namespace UnityAudioToolkit
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioEventNotifier : MonoBehaviour
    {
        readonly Subject<(float[] data, int channels)> _audioFilterReadEventNotifier = new();

        /// <summary>
        /// Runs on the audio thread.
        /// </summary>
        public Observable<(float[] data, int channels)> OnAudioFilterReadAsObservable() => _audioFilterReadEventNotifier;

        /// <summary>
        /// Called on the audio thread.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            _audioFilterReadEventNotifier.OnNext((data, channels));
        }
    }
}
