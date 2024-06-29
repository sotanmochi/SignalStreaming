using UnityEngine;
using Text = UnityEngine.UI.Text;

namespace SignalStreaming.Sandbox.StressTest
{
    public sealed class FrameRateView : MonoBehaviour
    {
        [SerializeField] Text _fpsText;
        [SerializeField] float _measurementIntervalSec = 0.5f;
        [SerializeField] Text _spikedFpsText;
        [SerializeField] float _spikeFrameDeltaTimeThreshold = 1f / 15f; // 15 FPS = 66.67 ms = 0.06667 sec
        // [SerializeField] Text _frameCountText;
        // [SerializeField] Text _spikedFrameCountText;

        private float _previousFrameTime;
        private float _averageDeltaTime;
        private float _lastMeasuredTime;
        private float _lastSpikedTime;
        private int _frameCount;
        private int _spikedFrameCount;

        void Update()
        {
            var currentFrameTime = Time.realtimeSinceStartup;
            var frameDeltaTimeSec = Time.unscaledDeltaTime;

            _averageDeltaTime += frameDeltaTimeSec;
            _frameCount++;

            if (currentFrameTime - _lastMeasuredTime > _measurementIntervalSec)
            {
                _averageDeltaTime /= _frameCount;
                _fpsText.text = $"Average FPS: {(1f / _averageDeltaTime):0.00}";

                _averageDeltaTime = 0f;
                _frameCount = 0;

                _lastMeasuredTime = currentFrameTime;
            }

            if (currentFrameTime - _lastSpikedTime > 3.0f)
            {
                _spikedFpsText.text = "";
            }

            if (frameDeltaTimeSec > _spikeFrameDeltaTimeThreshold)
            {  
                _spikedFrameCount++;
                _lastSpikedTime = currentFrameTime;
                _spikedFpsText.text = $"<color=red>Spiked FPS: {(1f / frameDeltaTimeSec):0.00}</color>";
            }

            // _frameCountText.text = $"Total Frames: {_frameCount}";
            // _spikedFrameCountText.text = $"Spiked Frames: {_spikedFrameCount}";
        }
    }
}
