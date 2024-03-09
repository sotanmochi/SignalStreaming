using System.Collections.Generic;
using UnityEngine;

namespace SignalStreaming.Samples.StressTest
{
    public class PlayerMoveSystem : MonoBehaviour
    {
        [SerializeField] PlayerMoveController _prefab;

        readonly Dictionary<uint, PlayerMoveController> _playerInstances = new();
        readonly Dictionary<uint, Material> _playerMaterials = new();

        public bool TryGetOrAdd(uint playerId, out PlayerMoveController playerInstance)
        {
            if (!_playerInstances.TryGetValue(playerId, out playerInstance))
            {
                _playerInstances[playerId] = GameObject.Instantiate(_prefab);
                playerInstance = _playerInstances[playerId];
            }
            return true;
        }

        public void EnableAutopilot(uint playerId, bool enable)
        {
            if (TryGetOrAdd(playerId, out var playerInstance))
            {
                playerInstance.EnableAutopilot(enable);
            }
        }

        public void UpdatePosition(uint playerId, Vector3 position)
        {
            if (TryGetOrAdd(playerId, out var playerInstance))
            {
                playerInstance.transform.position = position;
            }
        }

        public void UpdateRotation(uint playerId, Quaternion rotation)
        {
            if (TryGetOrAdd(playerId, out var playerInstance))
            {
                playerInstance.transform.rotation = rotation;
            }
        }

        public void UpdateColor(uint playerId, Color color)
        {
            if (!_playerMaterials.TryGetValue(playerId, out var material))
            {
                if (TryGetOrAdd(playerId, out var instance))
                {
                    _playerMaterials[playerId] = instance.gameObject.GetComponentInChildren<Renderer>().material;
                    material = _playerMaterials[playerId];
                }
            }
            material.SetColor("_Color", color);
        }
    }
}
