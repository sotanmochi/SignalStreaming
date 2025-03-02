using System;
using System.Buffers;
using SignalStreaming.EngineBridge;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;
using SignalStreaming.Transports;
using UnityEngine.Profiling;
using UnityEngine;

namespace SignalStreaming.Sandbox.StressTest
{
    public sealed class CharacterPoseService : IDisposable
    {
        readonly CharacterRepository _characterRepository;
        readonly ISignalStreamingClient _streamingClient;

        readonly QuantizedHumanoidPose _deserializedData = new(QuantizedHumanoidPoseHandler.AllMuscleCount, 12);

        bool _enableSelfOwnedCharacter = true;
        bool _enableTransmission = true;

        public CharacterPoseService(
            CharacterRepository characterRepository,
            ISignalStreamingClient streamingClient)
        {
            _characterRepository = characterRepository;
            _streamingClient = streamingClient;
            _streamingClient.OnConnected += OnConnected;
            _streamingClient.OnDisconnected += OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued += OnIncomingSignalDequeued;
        }

        public void Dispose()
        {
            _streamingClient.OnConnected -= OnConnected;
            _streamingClient.OnDisconnected -= OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued -= OnIncomingSignalDequeued;
        }

        public void LateTick()
        {
            if (!_enableSelfOwnedCharacter) return;

            var pose = _characterRepository.GetSelfOwnedCharacterPose();
            if (pose != null && _enableTransmission)
            {
                Profiler.BeginSample("CharacterPoseService.Send");
                _streamingClient.Send((int)SignalType.QuantizedHumanoidPose, pose, new SendOptions(StreamingType.All, reliable: false));
                Profiler.EndSample();
            }
        }

        public void SetEnableTransmission(bool enable)
        {
            _enableTransmission = enable;
        }

        public void SetEnableSelfOwnedCharacter(bool enable)
        {
            _enableSelfOwnedCharacter = enable;
        }

        void OnConnected(uint clientId)
        {
            if (!_enableSelfOwnedCharacter) return;

            var posX = UnityEngine.Random.Range(-10f, 10f);            
            var posY = 0f;
            var posZ = UnityEngine.Random.Range(-10f, 10f);

            _characterRepository.TryAddSelfOwnedCharacter(clientId, new Vector3(posX, posY, posZ), Quaternion.identity, out var _);
        }

        void OnDisconnected(string reason)
        {
            _characterRepository.RemoveSelfOwnedCharacter();
        }

        void OnIncomingSignalDequeued(int signalId, ReadOnlySequence<byte> payload, uint senderClientId)
        {
            if (signalId == (int)SignalType.QuantizedHumanoidPose)
            {
                Profiler.BeginSample("CharacterPoseService.Deserialize");
                //-------------------------
                // Avoid GC allocation
                SignalSerializer.DeserializeTo<QuantizedHumanoidPose>(_deserializedData, payload);
                _characterRepository.SetReplicatedCharacterPose(senderClientId, _deserializedData);
                //-------------------------
                Profiler.EndSample();
            }
        }
    }
}