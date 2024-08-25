using System.Threading.Tasks;
using UnityEngine;

namespace UnityAudioToolkit.Samples
{
    public sealed class AudioStreamTesterEntryPoint : MonoBehaviour
    {
        [SerializeField] AudioFilePlayer _audioFilePlayer;
        [SerializeField] AudioStreamOutput _audioStreamOutput;
        [SerializeField] AudioStreamOutputSpeaker _speaker;

        void Awake()
        {
            _audioStreamOutput.Create(1, 48000);
            _audioStreamOutput.Add(_speaker, AudioSpeakerChannelType.AllChannels);
        }

        async void Start()
        {
            await Task.Delay(3000);
            Debug.Log("[AudioStreamTesterEntryPoint] StartAsync");

            var audioStreamTester = new AudioStreamTester(_audioFilePlayer, _audioStreamOutput);
            audioStreamTester.Initialize();

            _audioFilePlayer.StartAsync(Application.streamingAssetsPath + "/Sample.wav");
        }
    }
}