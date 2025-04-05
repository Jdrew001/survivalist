using Assets.Game.Inventory.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.UI
{
    public class InventoryUIManager : MonoBehaviour
    {
        public static InventoryUIManager Instance { get; private set; }

        [SerializeField] private Transform containerParent;
        [SerializeField] private Transform dragLayer;
        [SerializeField] private GameObject containerUIPrefab;

        private Dictionary<string, InventoryContainerUI> activeContainers = new Dictionary<string, InventoryContainerUI>();
        private InventoryItemUI currentDraggedItem;

        public Transform DragLayer => dragLayer;
        public InventoryItemUI CurrentDraggedItem => currentDraggedItem;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Make sure we have a drag layer
            if (dragLayer == null)
            {
                GameObject dragLayerObject = new GameObject("DragLayer");
                dragLayerObject.transform.SetParent(transform);
                RectTransform dragLayerRect = dragLayerObject.AddComponent<RectTransform>();
                dragLayerRect.anchorMin = Vector2.zero;
                dragLayerRect.anchorMax = Vector2.one;
                dragLayerRect.offsetMin = Vector2.zero;
                dragLayerRect.offsetMax = Vector2.zero;
                dragLayer = dragLayerObject.transform;
            }
        }


        public bool IsContainerOpen(string containerId)
        {
            return activeContainers.ContainsKey(containerId);
        }

        private void Start()
        {
            // Subscribe to inventory manager events
            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager != null)
            {
                inventoryManager.OnContainerOpened += HandleContainerOpened;
                inventoryManager.OnContainerClosed += HandleContainerClosed;
                inventoryManager.OnItemAdded += HandleItemAdded;
                inventoryManager.OnItemRemoved += HandleItemRemoved;
            }
            else
            {
                Debug.LogError("InventoryManager instance not found! UI will not function correctly.");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from inventory manager events
            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager != null)
            {
                inventoryManager.OnContainerOpened -= HandleContainerOpened;
                inventoryManager.OnContainerClosed -= HandleContainerClosed;
                inventoryManager.OnItemAdded -= HandleItemAdded;
                inventoryManager.OnItemRemoved -= HandleItemRemoved;
            }
        }

        #region Inventory Manager Event Handlers

        private void HandleContainerOpened(InventoryContainer container)
        {
            // Skip if this container is already open
            if (activeContainers.ContainsKey(container.id))
                return;

            // Create container UI
            GameObject containerGO = Instantiate(containerUIPrefab, containerParent);
            InventoryContainerUI containerUI = containerGO.GetComponent<InventoryContainerUI>();

            if (containerUI != null)
            {
                containerUI.Initialize(container);
                activeContainers.Add(container.id, containerUI);
            }
        }

        private void HandleContainerClosed(InventoryContainer container)
        {
            if (activeContainers.TryGetValue(container.id, out InventoryContainerUI containerUI))
            {
                Destroy(containerUI.gameObject);
                activeContainers.Remove(container.id);
            }
        }

        private void HandleItemAdded(InventoryItem item, InventoryContainer container, Vector2Int position)
        {
            if (activeContainers.TryGetValue(container.id, out InventoryContainerUI containerUI))
            {
                // Refresh the grid to show the new item
                InventoryGridUI gridUI = containerUI.GetComponentInChildren<InventoryGridUI>();
                if (gridUI != null)
                {
                    gridUI.RefreshAllItems();
                }
            }
        }

        private void HandleItemRemoved(InventoryItem item, InventoryContainer container, Vector2Int position)
        {
            if (activeContainers.TryGetValue(container.id, out InventoryContainerUI containerUI))
            {
                // Refresh the grid to update after item removal
                InventoryGridUI gridUI = containerUI.GetComponentInChildren<InventoryGridUI>();
                if (gridUI != null)
                {
                    gridUI.RefreshAllItems();
                }
            }
        }

        #endregion

        #region Drag and Drop Management

        public void OnItemBeginDrag(InventoryItemUI itemUI)
        {
            currentDraggedItem = itemUI;
        }

        public void OnItemEndDrag(InventoryItemUI itemUI)
        {
            // Check if item was dropped on a valid target
            // If not, return it to its original position
            if (currentDraggedItem == itemUI)
            {
                itemUI.ReturnToOriginalPosition();
                currentDraggedItem = null;
            }
        }

        #endregion

        #region Public Helper Methods

        public void OpenAllPlayerContainers()
        {
            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager != null && inventoryManager.playerInventory != null)
            {
                inventoryManager.OpenContainer(inventoryManager.playerInventory);
            }
        }

        public void CloseAllContainers()
        {
            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager != null)
            {
                // Make a copy of the keys to avoid modification during iteration
                string[] containerIds = new string[activeContainers.Count];
                activeContainers.Keys.CopyTo(containerIds, 0);

                foreach (string containerId in containerIds)
                {
                    InventoryContainerUI containerUI = activeContainers[containerId];
                    inventoryManager.CloseContainer(containerUI.Container);
                }
            }
        }

        #endregion
    }
}
