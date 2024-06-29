using UnityEngine;

namespace Sandbox
{
    public sealed class PerformanceSpikeSimulatorBehaviour : MonoBehaviour
    {
        [SerializeField] float _fixedSpikeIntervalMs = 1000f;
        [SerializeField] float _randomSpikeMaxIntervalMs = 5000f;

        readonly PerformanceSpikeSimulator _spikeSimulator = new();

        void Start()
        {
            _spikeSimulator.StartFixedIntervalSpikes(_fixedSpikeIntervalMs);
            // _spikeSimulator.StartRandomIntervalSpikes(_randomSpikeMaxIntervalMs);
        }

        void Update()
        {
            _spikeSimulator.Tick();
        }
    }
}