using UnityEngine;
using UnityEngine.UI;

namespace SignalStreaming.Samples
{
    public sealed class UIView : MonoBehaviour
    {
        [SerializeField] Canvas _uiView;
        [SerializeField] Canvas _ingameDebugConsole;

        bool _uiViewEnabled;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _uiViewEnabled = !_uiViewEnabled;
                _uiView.enabled = _uiViewEnabled;
                _ingameDebugConsole.enabled = _uiViewEnabled;
            }
        }
    }
}