using Assets.Game.UI.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.UI.Factory
{
    public interface IUIElementFactory
    {
        GameObject CreateUIElement(UIElementData data, Transform parent);
    }
}
