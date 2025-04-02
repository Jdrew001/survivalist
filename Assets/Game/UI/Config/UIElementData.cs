using Assets.Game.Managers;
using System;
using UnityEngine;

namespace Assets.Game.UI.Config
{
    [Serializable]
    public class UIElementData
    {
        public string elementName;       
        public UIElementType elementType;
        public string displayText;       
        public Vector2 position;         
        public Vector2 size;             

        [SerializeReference]                
        public GameState nextState;         
    }
}
