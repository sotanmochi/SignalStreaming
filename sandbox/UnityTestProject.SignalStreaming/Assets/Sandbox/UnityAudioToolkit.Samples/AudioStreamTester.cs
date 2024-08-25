using R3;

namespace UnityAudioToolkit.Samples
{
    public sealed class AudioStreamTester
    {
        readonly IAudioStreamInput _audioStreamInput;
        readonly IAudioStreamOutput _audioStreamOutput;

        public AudioStreamTester(IAudioStreamInput audioStreamInput, IAudioStreamOutput audioStreamOutput)
        {
            _audioStreamInput = audioStreamInput;
            _audioStreamOutput = audioStreamOutput;
        }

        public void Initialize()
        {
            _audioStreamInput.OnAudioFrameProcessedAsObservable()
                .Subscribe(audioFrameBuffer =>
                {
                    UnityEngine.Debug.Log("[AudioStreamTester] AudioFrameBuffer.Length: " + audioFrameBuffer.Length);
                    _audioStreamOutput.EnqueuePcmData(audioFrameBuffer);
                });
        }
    }
}
