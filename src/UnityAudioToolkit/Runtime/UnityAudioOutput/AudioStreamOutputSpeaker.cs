using System;
using R3;
using UnityEngine;

namespace UnityAudioToolkit
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioStreamOutputSpeaker : MonoBehaviour
    {
        /// <summary>
        /// Reciprocal of resampling ratio.
        /// </summary>
        public float SamplesCountScaleFactor { get; set; } = 1f;
        public int BufferedPcmDataSamplesCount { get; set; } = 0;
        public AudioSource AudioSource { get; private set; }

        void Awake()
        {
            AudioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Called on the audio thread.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            var outputSamplesCount = data.Length / channels;
            var outputPcmDataSamplesCount = (int)(SamplesCountScaleFactor * outputSamplesCount);

            if (BufferedPcmDataSamplesCount <= 0)
            {
                // Silent output
                new Span<float>(data).Fill(0f);
            }
            else if (BufferedPcmDataSamplesCount < outputPcmDataSamplesCount)
            {
                var passthroughOutputSamplesCount = (int)(BufferedPcmDataSamplesCount / SamplesCountScaleFactor);
                var passthroughOutputLength = passthroughOutputSamplesCount * channels;

                // Partial filter
                new Span<float>(data, start: passthroughOutputLength, length: data.Length - passthroughOutputLength).Fill(0f);
            }
            else
            {
                // No filter
            }
        }
    }
}
