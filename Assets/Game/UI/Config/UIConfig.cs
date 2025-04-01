using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.UI.Config
{
    [CreateAssetMenu(fileName = "UIConfig", menuName = "UI/Config")]
    public class UIConfig : ScriptableObject
    {
        public UIElementData[] elements;
    }
}
