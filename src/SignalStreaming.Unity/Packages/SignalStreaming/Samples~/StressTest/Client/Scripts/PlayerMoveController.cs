using System;
using UnityEngine;

namespace SignalStreaming.Samples.StressTest
{
    public class PlayerMoveController : MonoBehaviour
    {
        [SerializeField] bool _autopilot = false;

        ObjectPoseCalculator _poseCalculator = new();

        public void EnableAutopilot(bool enable)
        {
            _autopilot = enable;
        }

        void Start()
        {
            _poseCalculator.Startup();
        }

        void Update()
        {
            if (_autopilot)
            {
                _poseCalculator.Tick();
                transform.position = _poseCalculator.Position;
                transform.rotation = _poseCalculator.Rotation;
            }
        }

        // void LateUpdate()
        // {
        //     if (_autopilot)
        //     {
        //         mainCamera.transform.position = transform.position - _poseCalculator.ForwardDirection * 10 + Vector3.up * 5;
        //         mainCamera.transform.forward = _poseCalculator.ForwardDirection;
        //         mainCamera.transform.LookAt(transform.position);
        //     }
        // }
    }
}
