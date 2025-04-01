using Assets.Game.Managers;
using Assets.Game.UI.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Assets.Game.UI.Factory.Strategies
{
    public class ButtonCreationStrategy : IUIElementCreationStrategy
    {
        private readonly GameObject _buttonPrefab;
        private readonly GameStateManager _gameStateManager;

        public ButtonCreationStrategy(GameObject buttonPrefab, GameStateManager gameStateManager)
        {
            _buttonPrefab = buttonPrefab;
            _gameStateManager = gameStateManager;
        }

        public GameObject Create(UIElementData data, Transform parent)
        {
            // Instantiate prefab
            var newButtonObj = Object.Instantiate(_buttonPrefab, parent);
            newButtonObj.name = data.elementName;

            // Position & size
            var rt = newButtonObj.GetComponent<RectTransform>();
            rt.anchoredPosition = data.position;
            rt.sizeDelta = data.size;

            // Display text
            var txt = newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = data.displayText;
            }

            // OnClick
            var btn = newButtonObj.GetComponent<Button>();
            if (btn != null)
            {
                Debug.Log($"click event added!");
                btn.onClick.AddListener(() =>
                {
                    Debug.Log($"{data.elementName} clicked!");

                    // Call SetState with the new state. 
                    // If data.nextState is null or not assigned, handle accordingly.
                    if (data.nextState != null)
                    {
                        _gameStateManager.SetState(data.nextState);
                    }
                    else
                    {
                        Debug.LogWarning($"{data.elementName} button has no nextState assigned!");
                    }
                });
            }

            return newButtonObj;
        }
    }
}
