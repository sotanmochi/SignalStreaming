using System.Collections.Generic;
using SignalStreaming.EngineBridge;
using SignalStreaming.Quantization;
using UnityEngine;

namespace SignalStreaming.Sandbox.StressTest
{
    public sealed class CharacterRepository
    {
        readonly Animator _selfOwnedCharacterPrefab;
        readonly Animator _replicatedCharacterPrefab;
        readonly BoundedRange[] _worldBounds;

        readonly Dictionary<uint, QuantizedHumanPoseHandler> _replicatedCharacters = new();
        readonly Dictionary<uint, GameObject> _replicatedCharacterObjects = new();

        uint _selfOwnedCharacterId;
        QuantizedHumanPoseHandler _selfOwnedCharacter;
        GameObject _selfOwnedCharacterObject;

        float _musclePrecision;

        public CharacterRepository(
            BoundedRange[] worldBounds,
            float musclePrecision,
            Animator selfOwnedCharacterPrefab,
            Animator replicatedCharacterPrefab)
        {
            _worldBounds = worldBounds;
            _musclePrecision = musclePrecision;
            _selfOwnedCharacterPrefab = selfOwnedCharacterPrefab;
            _replicatedCharacterPrefab = replicatedCharacterPrefab;
        }

        public bool TryAddSelfOwnedCharacter(uint id, Vector3 position, Quaternion rotation, out QuantizedHumanPoseHandler characterPoseHandler)
        {
            if (_selfOwnedCharacter == null)
            {
                var animator = GameObject.Instantiate(_selfOwnedCharacterPrefab, position, rotation);
                _selfOwnedCharacterId = id;
                _selfOwnedCharacter = new(animator, _worldBounds, _musclePrecision);
                _selfOwnedCharacterObject = animator.gameObject;
            }
            characterPoseHandler = _selfOwnedCharacter;
            return _selfOwnedCharacter != null;
        }

        public void RemoveSelfOwnedCharacter()
        {
            if (_selfOwnedCharacter != null)
            {
                _selfOwnedCharacter = null;
                _selfOwnedCharacterId = 0;
                GameObject.Destroy(_selfOwnedCharacterObject);
            }
        }

        public QuantizedHumanPose GetSelfOwnedCharacterPose()
        {
            return _selfOwnedCharacter?.GetHumanPose();
        }

        public void SetReplicatedCharacterPose(uint id, QuantizedHumanPose pose)
        {
            if (TryGetOrAddReplicatedCharacter(id, out var characterPoseHandler))
            {
                characterPoseHandler.SetHumanPose(pose);
            }
        }

        public void RemoveReplicatedCharacter(uint id)
        {
            _replicatedCharacters.Remove(id);
            if (_replicatedCharacterObjects.TryGetValue(id, out var characterObject))
            {
                GameObject.Destroy(characterObject);
            }
        }

        public void UpdateMusclePrecision(float musclePrecision)
        {
            _musclePrecision = musclePrecision;
            if (_selfOwnedCharacter != null)
            {
                _selfOwnedCharacter.MusclePrecision = musclePrecision;
            }
            foreach (var replicatedCharacter in _replicatedCharacters.Values)
            {
                replicatedCharacter.MusclePrecision = musclePrecision;
            }
        }

        bool TryGetOrAddReplicatedCharacter(uint id, out QuantizedHumanPoseHandler characterPoseHandler)
        {
            if (!_replicatedCharacters.TryGetValue(id, out characterPoseHandler) && id != _selfOwnedCharacterId)
            {
                var animator = GameObject.Instantiate(_replicatedCharacterPrefab);
                characterPoseHandler = new(animator, _worldBounds, _musclePrecision);
                _replicatedCharacters[id] = characterPoseHandler;
                _replicatedCharacterObjects[id] = animator.gameObject;
            }
            return characterPoseHandler != null;
        }
    }
}
