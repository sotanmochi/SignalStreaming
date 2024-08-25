using System.Collections.Generic;
using UnityEngine;
using R3;

namespace UnityAudioToolkit
{
    public sealed class AudioStreamOutput : MonoBehaviour, IAudioStreamOutput
    {
        readonly List<AudioStreamOutputSpeaker> _audioSpeakers = new();

        AudioClip[] _audioClips = new AudioClip[0];
        float[] _singleChannelBuffer;
        float[] _zeroBuffer;

        float _samplesCountScaleFactor; // Reciprocal of resampling ratio
        int _channels;
        int _audioClipSamplesCount;
        int _audioFrameSamplesCount;

        int _enqueuedCountInSingleFrame;
        int _bufferedPcmDataSamplesCount;
        int _bufferTailPosition;

        AudioEventNotifier _audioEventNotifier;
        CompositeDisposable _disposables;

        public bool IsMute { get; set; }
        public bool IsPlaying { get; private set; }
        public int DelayFrames { get; set; } = 2;

        void Update()
        {
            Tick();
        }

        void OnDestroy()
        {
            Stop();

            _disposables?.Dispose();
            _audioEventNotifier = null;

            _audioSpeakers.Clear();

            for (var i = 0; i < _audioClips.Length; i++)
            {
                Destroy(_audioClips[i]);
                _audioClips[i] = null;
            }
        }

        public void Create(int channels, int pcmDataSampleRate, float bufferingTimeSeconds = 1f, int audioFrameDurationMs = 40)
        {
            Stop();

            _channels = channels;

            // Default values of AudioSettings.outputSampleRate are as follows:
            //  - Windows/Mac: 48,000 Hz
            //  - iOS/Android: 24,000 Hz
            _samplesCountScaleFactor = (float) pcmDataSampleRate / AudioSettings.outputSampleRate; // Reciprocal of resampling ratio

            _audioClipSamplesCount = (int)(pcmDataSampleRate * bufferingTimeSeconds);
            _audioFrameSamplesCount = (int)(pcmDataSampleRate * audioFrameDurationMs / 1000f);

            _singleChannelBuffer = new float[_audioClipSamplesCount];
            _zeroBuffer = new float[_audioClipSamplesCount * channels];

            var audioClipCount = (channels > 1) ? (channels + 1) : 1;
            _audioClips = new AudioClip[audioClipCount];

            _audioClips[0] = AudioClip.Create($"AudioStreamOutput", _audioClipSamplesCount, channels, pcmDataSampleRate, false);
            for (var i = 1; i < audioClipCount; i++)
            {
                _audioClips[i] = AudioClip.Create($"AudioStreamOutput_{i}", _audioClipSamplesCount, channels, pcmDataSampleRate, false);
            }
        }

        public void Add(AudioStreamOutputSpeaker speaker, AudioSpeakerChannelType channelType)
        {
            if (!speaker.TryGetComponent(out AudioEventNotifier audioEventNotifier))
            {
                audioEventNotifier = speaker.gameObject.AddComponent<AudioEventNotifier>();
            }

            if (_audioEventNotifier == null)
            {
                _disposables = new CompositeDisposable();
                _audioEventNotifier = audioEventNotifier;
                _audioEventNotifier.OnAudioFilterReadAsObservable()
                    .Subscribe(values =>
                    {
                        OnAudioFilterReadEventHandler(values.data, values.channels);
                    })
                    .AddTo(_disposables);
            }

            _audioSpeakers.Add(speaker);

            speaker.AudioSource.loop = true;
            speaker.AudioSource.clip = _audioClips[(int)channelType];
        }

        public void EnqueuePcmData(float[] pcmData)
        {
            if (IsMute)
            {
                return;
            }

            if (IsPlaying && _bufferedPcmDataSamplesCount <= 0)
            {
                Stop();
            }

            // NOTE:
            // If the length from the offset is longer than the clip length,
            // the write will wrap around and write the remaining samples from the start of the clip.
            // https://docs.unity3d.com/jp/2022.3/ScriptReference/AudioClip.SetData.html
            //
            _audioClips[0].SetData(pcmData, _bufferTailPosition); // Enqueue data

            var samplesCount = pcmData.Length / _channels;
            for (var c = 1; c < _channels; c++)
            {
                for (var i = 0; i < samplesCount; i++)
                {
                    _singleChannelBuffer[i] = pcmData[i * _channels + c];
                }

                for (var i = samplesCount; i < _singleChannelBuffer.Length; i++)
                {
                    _singleChannelBuffer[i] = 0f;
                }

                // NOTE:
                // If the length from the offset is longer than the clip length,
                // the write will wrap around and write the remaining samples from the start of the clip.
                // https://docs.unity3d.com/jp/2022.3/ScriptReference/AudioClip.SetData.html
                //
                _audioClips[c].SetData(_singleChannelBuffer, _bufferTailPosition); // Enqueue data
            }

            _bufferedPcmDataSamplesCount += samplesCount;
            _bufferTailPosition += samplesCount;
            _bufferTailPosition = _bufferTailPosition % (_audioClipSamplesCount);

            _enqueuedCountInSingleFrame++;
            if (_enqueuedCountInSingleFrame > DelayFrames)
            {
                var timeSamples = ((_bufferTailPosition - _audioFrameSamplesCount * DelayFrames) + _audioClipSamplesCount) % _audioClipSamplesCount;
                foreach (var audioSpeaker in _audioSpeakers)
                {
                    audioSpeaker.AudioSource.timeSamples = timeSamples;
                }
                _bufferedPcmDataSamplesCount = _audioFrameSamplesCount * DelayFrames;
            }

            if (!IsPlaying && (_bufferedPcmDataSamplesCount >= _audioFrameSamplesCount * DelayFrames))
            {
                foreach (var audioSpeaker in _audioSpeakers)
                {
                    audioSpeaker.AudioSource.Play();
                }
                IsPlaying = true;
            }
        }

        /// <summary>
        /// Runs on the audio thread.
        /// </summary>
        void OnAudioFilterReadEventHandler(float[] data, int channels)
        {
            if (!IsPlaying) return;

            var outputSamplesCount = data.Length / channels;
            var dequeuedPcmDataSamplesCount = (int)(_samplesCountScaleFactor * outputSamplesCount);

            _bufferedPcmDataSamplesCount -= dequeuedPcmDataSamplesCount;
            if (_bufferedPcmDataSamplesCount <= 0) _bufferedPcmDataSamplesCount = 0;

            foreach (var audioSpeaker in _audioSpeakers)
            {
                audioSpeaker.SamplesCountScaleFactor = _samplesCountScaleFactor;
                audioSpeaker.BufferedPcmDataSamplesCount = _bufferedPcmDataSamplesCount;
            }
        }

        void Tick()
        {
            _enqueuedCountInSingleFrame = 0;

            if (_bufferedPcmDataSamplesCount <= 0 && IsPlaying)
            {
                Stop();
            }
        }

        void Stop()
        {
            foreach (var audioSpeaker in _audioSpeakers)
            {
                audioSpeaker.AudioSource.Stop();
                audioSpeaker.AudioSource.timeSamples = 0;
            }

            _bufferedPcmDataSamplesCount = 0;
            _bufferTailPosition = 0;
            IsPlaying = false;

            foreach (var audioClip in _audioClips)
            {
                audioClip.SetData(_zeroBuffer, offsetSamples: 0);
            }
        }
    }
}
