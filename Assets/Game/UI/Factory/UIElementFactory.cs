using Assets.Game.Managers;
using Assets.Game.UI.Config;
using Assets.Game.UI.Factory.Strategies;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Assets.Game.UI.Factory
{
    public class UIElementFactory : IUIElementFactory
    {
        private readonly Dictionary<UIElementType, IUIElementCreationStrategy> _strategies;

        // Constructor injection:
        public UIElementFactory(
            [Inject(Id = "ButtonPrefab")] GameObject buttonPrefab,
            [Inject(Id = "LabelPrefab")] GameObject labelPrefab,
            [Inject] GameStateManager gameStateManager)
        {
            _strategies = new Dictionary<UIElementType, IUIElementCreationStrategy>
            {
                { UIElementType.Button, new ButtonCreationStrategy(buttonPrefab, gameStateManager) },
                { UIElementType.Label,  new LabelCreationStrategy(labelPrefab) }
            };
        }


        public GameObject CreateUIElement(UIElementData data, Transform parent)
        {
            if (_strategies.TryGetValue(data.elementType, out var strategy))
            {
                return strategy.Create(data, parent);
            }

            Debug.LogWarning($"No strategy for UIElementType: {data.elementType}");
            return null;
        }
    }
}
