using Assets.Game.Inventory.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Game.Inventory.UI
{
    public class InventoryContainerUI : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private InventoryGridUI gridUI;

        private InventoryContainer container;

        public InventoryContainer Container => container;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }

        public void Initialize(InventoryContainer container)
        {
            this.container = container;

            if (titleText != null)
            {
                titleText.text = container.displayName;
            }

            if (gridUI != null)
            {
                gridUI.Initialize(container);
            }
        }

        private void OnCloseButtonClicked()
        {
            // Notify the inventory manager that this container is being closed
            InventoryManager.Instance.CloseContainer(container);
        }

        private void OnDestroy()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }
        }
    }
}
