using Assets.Game.Inventory.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.Helpers
{
    public static class InventoryExtensions
    {
        // Find a matching item in a container
        public static InventoryItem FindMatchingItem(this InventoryContainer container, string itemId)
        {
            for (int x = 0; x < container.gridSize.x; x++)
            {
                for (int y = 0; y < container.gridSize.y; y++)
                {
                    if (!container.slots[x, y].isEmpty && container.slots[x, y].item.id == itemId)
                    {
                        return container.slots[x, y].item;
                    }
                }
            }
            return null;
        }

        // Count total quantity of an item type in a container
        public static int CountItems(this InventoryContainer container, string itemId)
        {
            int total = 0;
            HashSet<InventoryItem> countedItems = new HashSet<InventoryItem>();

            for (int x = 0; x < container.gridSize.x; x++)
            {
                for (int y = 0; y < container.gridSize.y; y++)
                {
                    if (!container.slots[x, y].isEmpty &&
                        container.slots[x, y].item.id == itemId &&
                        !countedItems.Contains(container.slots[x, y].item))
                    {
                        countedItems.Add(container.slots[x, y].item);
                        total += container.slots[x, y].item.currentStackSize;
                    }
                }
            }

            return total;
        }

        // Try to remove a quantity of items from a container
        public static bool TryRemoveItems(this InventoryContainer container, string itemId, int quantity)
        {
            // First check if we have enough of the item
            int available = container.CountItems(itemId);
            if (available < quantity)
                return false;

            // Track how many we still need to remove
            int remaining = quantity;

            // Find all instances of the item and remove from them
            for (int x = 0; x < container.gridSize.x && remaining > 0; x++)
            {
                for (int y = 0; y < container.gridSize.y && remaining > 0; y++)
                {
                    if (!container.slots[x, y].isEmpty && container.slots[x, y].item.id == itemId)
                    {
                        InventoryItem item = container.slots[x, y].item;

                        // Check if we've already counted this item instance
                        bool alreadyCounted = false;
                        for (int checkX = 0; checkX < x; checkX++)
                        {
                            for (int checkY = 0; checkY < container.gridSize.y; checkY++)
                            {
                                if (!container.slots[checkX, checkY].isEmpty &&
                                    container.slots[checkX, checkY].item == item)
                                {
                                    alreadyCounted = true;
                                    break;
                                }
                            }
                            if (alreadyCounted) break;
                        }

                        if (!alreadyCounted)
                        {
                            // Remove as much as we can from this stack
                            int amountToRemove = Mathf.Min(remaining, item.currentStackSize);
                            item.currentStackSize -= amountToRemove;
                            remaining -= amountToRemove;

                            // If stack is empty, remove the item
                            if (item.currentStackSize <= 0)
                            {
                                InventoryManager.Instance.RemoveItemFromContainer(container, new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }

            return remaining <= 0;
        }
    }
}
