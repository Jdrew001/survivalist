using Assets.Game.UI.Config;
using Assets.Game.UI.Factory;
using UnityEngine;
using Zenject;

namespace Assets.Game.UI
{
    public class UICreator : MonoBehaviour
    {
        [Header("Parent for spawned UI (Canvas or Panel)")]
        [SerializeField] private RectTransform uiParent;

        [Inject]
        private UIConfig _uiConfig;

        [Inject]
        private IUIElementFactory _factory;

        private void Start()
        {
            if (_uiConfig == null)
            {
                Debug.LogWarning("UIConfig not found via Zenject injection!");
                return;
            }

            foreach (var elementData in _uiConfig.elements)
            {
                _factory.CreateUIElement(elementData, uiParent);
            }
        }
    }
}
