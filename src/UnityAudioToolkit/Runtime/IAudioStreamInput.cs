using System;
using R3;

namespace UnityAudioToolkit
{
    public interface IAudioStreamInput
    {
        Observable<float[]> OnAudioFrameProcessedAsObservable();
    }
}
