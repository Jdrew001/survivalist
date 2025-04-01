using Assets.Game.Managers;
using System;
using UnityEngine;

namespace Assets.Game.UI.Config
{
    [Serializable]
    public class UIElementData
    {
        public string elementName;       // e.g. "PlayButton"
        public UIElementType elementType;
        public string displayText;       // e.g. "Play Game"
        public Vector2 position;         // anchored position
        public Vector2 size;             // width & height
        public string sceneToLoad;
        public GameState nextState; // State to Load on click event
    }
}
