using UnityEngine;
using Text = UnityEngine.UI.Text;

namespace SignalStreaming.Samples.StressTest
{
    public sealed class FrameRateView : MonoBehaviour
    {
        [SerializeField] Text _fpsText;
        [SerializeField] float _measurementIntervalSec = 0.5f;

        private float _previousFrameTime;
        private float _averageDeltaTime;
        private float _previousMeasuredTime;
        private int _frameCount;

        void Update()
        {
            var currentFrameTime = Time.realtimeSinceStartup;
            var deltaTime = currentFrameTime - _previousFrameTime;
            _previousFrameTime = currentFrameTime;

            _averageDeltaTime += deltaTime;
            _frameCount++;

            if (currentFrameTime - _previousMeasuredTime > _measurementIntervalSec)
            {
                _averageDeltaTime /= _frameCount;
                _fpsText.text = $"FPS: {(1f / _averageDeltaTime):0.00}";

                _averageDeltaTime = 0f;
                _frameCount = 0;

                _previousMeasuredTime = currentFrameTime;
            }
        }
    }
}
