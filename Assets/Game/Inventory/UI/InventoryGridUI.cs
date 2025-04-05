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
    public class InventoryGridUI : MonoBehaviour
    {
        [SerializeField] private GameObject slotPrefab;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private GridLayoutGroup gridLayout;
        [SerializeField] private RectTransform gridContainer;

        private InventoryContainer container;
        private InventorySlotUI[,] slotUIArray;
        private Dictionary<InventoryItem, InventoryItemUI> itemUIMap = new Dictionary<InventoryItem, InventoryItemUI>();

        // Properties to access grid layout settings
        public Vector2 CellSize => gridLayout != null ? gridLayout.cellSize : new Vector2(60, 60);
        public Vector2 Spacing => gridLayout != null ? gridLayout.spacing : new Vector2(5, 5);
        public InventoryContainer Container => container;

        public void Initialize(InventoryContainer container)
        {
            this.container = container;
            CreateGrid();
        }

        private void CreateGrid()
        {
            // Clear existing slots
            foreach (Transform child in gridContainer)
            {
                Destroy(child.gameObject);
            }

            // Initialize slot array
            slotUIArray = new InventorySlotUI[container.gridSize.x, container.gridSize.y];

            // Configure grid layout
            if (gridLayout != null)
            {
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = container.gridSize.x;
            }

            // Create slots
            for (int y = 0; y < container.gridSize.y; y++)
            {
                for (int x = 0; x < container.gridSize.x; x++)
                {
                    GameObject slotGO = Instantiate(slotPrefab, gridContainer);
                    InventorySlotUI slotUI = slotGO.GetComponent<InventorySlotUI>();

                    if (slotUI != null)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        slotUI.Initialize(this, pos);
                        slotUIArray[x, y] = slotUI;
                    }
                }
            }

            // Populate with items from container
            RefreshAllItems();
        }

        public void RefreshAllItems()
        {
            // Clear existing item UIs
            foreach (var itemUI in itemUIMap.Values)
            {
                Destroy(itemUI.gameObject);
            }
            itemUIMap.Clear();

            // Track processed items to avoid duplicates
            HashSet<InventoryItem> processedItems = new HashSet<InventoryItem>();

            // Create item UIs for each item in the container
            for (int x = 0; x < container.gridSize.x; x++)
            {
                for (int y = 0; y < container.gridSize.y; y++)
                {
                    ItemSlot slot = container.slots[x, y];

                    if (!slot.isEmpty && !processedItems.Contains(slot.item))
                    {
                        processedItems.Add(slot.item);

                        // Find the top-left position of the item
                        Vector2Int itemPos = FindItemTopLeftPosition(slot.item, new Vector2Int(x, y));

                        // Create item UI at the correct slot
                        if (slotUIArray[itemPos.x, itemPos.y] != null)
                        {
                            CreateItemUI(slot.item, itemPos);
                        }
                    }
                }
            }
        }

        private Vector2Int FindItemTopLeftPosition(InventoryItem item, Vector2Int currentPos)
        {
            int minX = currentPos.x;
            int minY = currentPos.y;

            // Search left
            while (minX > 0 && container.slots[minX - 1, currentPos.y].item == item)
                minX--;

            // Search up
            while (minY > 0 && container.slots[currentPos.x, minY - 1].item == item)
                minY--;

            return new Vector2Int(minX, minY);
        }

        private InventoryItemUI CreateItemUI(InventoryItem item, Vector2Int position)
        {
            if (slotUIArray[position.x, position.y] == null)
                return null;

            GameObject itemGO = Instantiate(itemPrefab, slotUIArray[position.x, position.y].transform);
            InventoryItemUI itemUI = itemGO.GetComponent<InventoryItemUI>();

            if (itemUI != null)
            {
                itemUI.Setup(item, position, this);
                itemUIMap[item] = itemUI;
                return itemUI;
            }

            return null;
        }

        public bool CanPlaceItemAt(InventoryItem item, Vector2Int position, bool isExternalItem)
        {
            return container.CanPlaceItemAt(item, position);
        }

        public void HighlightItemPlacement(Vector2Int position, Vector2Int size)
        {
            // Clear existing highlights first
            ClearAllHighlights();

            // Highlight all slots this item would occupy
            for (int x = position.x; x < position.x + size.x && x < container.gridSize.x; x++)
            {
                for (int y = position.y; y < position.y + size.y && y < container.gridSize.y; y++)
                {
                    if (slotUIArray[x, y] != null)
                    {
                        slotUIArray[x, y].SetHighlight(true);
                    }
                }
            }
        }

        public void ClearAllHighlights()
        {
            foreach (var slotUI in slotUIArray)
            {
                if (slotUI != null)
                {
                    slotUI.SetHighlight(false);
                    slotUI.SetValidDropTarget(true);
                }
            }
        }

        public void OnItemDroppedInSlot(InventorySlotUI slot, InventoryItemUI droppedItem)
        {
            // Get the inventory manager instance
            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
                return;

            // Check if this is coming from another container
            if (droppedItem.SourceGrid != this)
            {
                // Cross-container transfer
                InventoryContainer sourceContainer = droppedItem.SourceGrid.Container;

                // Remove from source container
                inventoryManager.RemoveItemFromContainer(sourceContainer, droppedItem.GridPosition);

                // Try to add to this container
                if (inventoryManager.AddItemToContainer(droppedItem.Item, container, slot.GridPosition))
                {
                    // Success - refresh both containers
                    droppedItem.SourceGrid.RefreshAllItems();
                    RefreshAllItems();
                }
                else
                {
                    // Failed - return item to original container
                    inventoryManager.AddItemToContainer(droppedItem.Item, sourceContainer, droppedItem.GridPosition);
                    droppedItem.SourceGrid.RefreshAllItems();
                }
            }
            else
            {
                // Same container movement
                InventoryItem item = droppedItem.Item;
                Vector2Int originalPos = droppedItem.GridPosition;
                Vector2Int newPos = slot.GridPosition;

                // Only process if the position changed
                if (originalPos != newPos)
                {
                    // Remove from original position
                    inventoryManager.RemoveItemFromContainer(container, originalPos);

                    // Try to add to new position
                    if (inventoryManager.AddItemToContainer(item, container, newPos))
                    {
                        // Success - refresh container
                        RefreshAllItems();
                    }
                    else
                    {
                        // Failed - return to original position
                        inventoryManager.AddItemToContainer(item, container, originalPos);
                        RefreshAllItems();
                    }
                }
                else
                {
                    // Item dropped in same position - just return to original
                    droppedItem.ReturnToOriginalPosition();
                }
            }

            // Clear all highlighting
            ClearAllHighlights();
        }
    }
}
