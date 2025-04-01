using Assets.Game.UI.Config;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Assets.Game.UI.Factory.Strategies
{
    public class LabelCreationStrategy : IUIElementCreationStrategy
    {
        private readonly GameObject _labelPrefab;

        public LabelCreationStrategy(GameObject labelPrefab)
        {
            _labelPrefab = labelPrefab;
        }

        public GameObject Create(UIElementData data, Transform parent)
        {
            var newLabelObj = Object.Instantiate(_labelPrefab, parent);
            newLabelObj.name = data.elementName;

            // Position & size
            var rt = newLabelObj.GetComponent<RectTransform>();
            rt.anchoredPosition = data.position;
            rt.sizeDelta = data.size;

            // Display text
            var txt = newLabelObj.GetComponent<Text>();
            if (txt != null)
            {
                txt.text = data.displayText;
            }

            return newLabelObj;
        }
    }
}
