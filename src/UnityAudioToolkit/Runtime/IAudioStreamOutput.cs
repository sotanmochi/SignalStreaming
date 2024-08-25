namespace UnityAudioToolkit
{
    public interface IAudioStreamOutput
    {
        void EnqueuePcmData(float[] pcmData);
    }
}
