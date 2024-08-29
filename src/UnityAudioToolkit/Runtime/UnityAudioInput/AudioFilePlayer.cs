using System;
using R3;
using UnityEngine;

namespace UnityAudioToolkit
{
    public sealed class AudioFilePlayer : MonoBehaviour, IAudioStreamInput
    {
        readonly Subject<float[]> _audioFrameProcessedEventNotifier = new();

        AudioSource _audioSource;
        AudioClip _audioClip;
        float[] _audioFrameBuffer;

        int _dequeuedHeadPosition;

        public bool IsMute { get; set; }

        void Update()
        {
            Tick();
        }

        void OnDestroy()
        {
            Stop();
        }

        public Observable<float[]> OnAudioFrameProcessedAsObservable()
        {
            return _audioFrameProcessedEventNotifier;
        }

        public async void StartAsync(string filePath, int sampleRate = Constants.DefaultSampleRate, int audioFrameDurationMs = Constants.DefaultAudioFrameDurationMs)
        {
            _audioClip = await AudioFileLoader.LoadAsync(filePath);
            if (_audioClip.frequency != sampleRate)
            {
                var audioClipFrequency = _audioClip.frequency;
                Destroy(_audioClip);
                _audioClip = null;
                throw new ArgumentException($"The sample rate of the audio file is {audioClipFrequency}. It should be {sampleRate}.");
            }

            _audioFrameBuffer = new float[(int)(sampleRate * audioFrameDurationMs / 1000f)];

            if (!gameObject.TryGetComponent(out _audioSource))
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.Stop();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.clip = _audioClip;

            _audioSource.volume = 0f; // Silent

            _audioSource.Play();
        }

        public void Stop()
        {
            _dequeuedHeadPosition = 0;

            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }

            if (_audioClip != null)
            {
                Destroy(_audioClip);
                _audioClip = null;
            }
        }

        void Tick()
        {
            if (_audioSource == null || !_audioSource.isPlaying)
            {
                return;
            }

            var playbackPosition = _audioSource.timeSamples;
            if (playbackPosition > _dequeuedHeadPosition)
            {
                while (playbackPosition - _dequeuedHeadPosition > _audioFrameBuffer.Length)
                {
                    _audioClip.GetData(_audioFrameBuffer, _dequeuedHeadPosition);
                    _dequeuedHeadPosition += _audioFrameBuffer.Length;
                    _audioFrameProcessedEventNotifier.OnNext(_audioFrameBuffer);
                }
            }
        }
    }
}
