
using Assets.Game.UI.Config;
using UnityEngine;

namespace Assets.Game.UI.Factory.Strategies
{
    public interface IUIElementCreationStrategy
    {
        GameObject Create(UIElementData data, Transform parent);
    }
}
