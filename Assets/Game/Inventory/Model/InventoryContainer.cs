using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.Model
{
    [Serializable]
    public class InventoryContainer
    {
        public string id;
        public string displayName;
        public Vector2Int gridSize = new Vector2Int(5, 5);
        public ItemSlot[,] slots;
        public ContainerType containerType;

        public InventoryContainer(string id, string displayName, Vector2Int size, ContainerType type)
        {
            this.id = id;
            this.displayName = displayName;
            this.gridSize = size;
            this.containerType = type;
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            slots = new ItemSlot[gridSize.x, gridSize.y];

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    slots[x, y] = new ItemSlot
                    {
                        position = new Vector2Int(x, y),
                        item = null
                    };
                }
            }
        }

        public bool TryAddItem(InventoryItem item, Vector2Int position)
        {
            // Check if the item fits at the specified position
            if (!CanPlaceItemAt(item, position))
                return false;

            // Add the item to the slots
            PlaceItemAt(item, position);
            return true;
        }

        public bool CanPlaceItemAt(InventoryItem item, Vector2Int position)
        {
            // Check if position is within grid bounds considering item size
            if (position.x < 0 || position.y < 0 ||
                position.x + item.size.x > gridSize.x ||
                position.y + item.size.y > gridSize.y)
                return false;

            // Check if all required slots are empty and can accept this item type
            for (int x = position.x; x < position.x + item.size.x; x++)
            {
                for (int y = position.y; y < position.y + item.size.y; y++)
                {
                    if (!slots[x, y].isEmpty || !slots[x, y].CanAcceptItem(item))
                        return false;
                }
            }

            return true;
        }

        private void PlaceItemAt(InventoryItem item, Vector2Int position)
        {
            // Place the item in all slots it occupies
            for (int x = position.x; x < position.x + item.size.x; x++)
            {
                for (int y = position.y; y < position.y + item.size.y; y++)
                {
                    slots[x, y].item = item;
                }
            }
        }

        public void RemoveItemAt(Vector2Int position)
        {
            if (position.x < 0 || position.y < 0 || position.x >= gridSize.x || position.y >= gridSize.y)
                return;

            if (slots[position.x, position.y].isEmpty)
                return;

            InventoryItem item = slots[position.x, position.y].item;

            // Find the top-left corner of the item
            Vector2Int topLeft = FindItemTopLeftCorner(position, item);

            // Clear all slots the item occupies
            for (int x = topLeft.x; x < topLeft.x + item.size.x; x++)
            {
                for (int y = topLeft.y; y < topLeft.y + item.size.y; y++)
                {
                    if (x < gridSize.x && y < gridSize.y)
                        slots[x, y].item = null;
                }
            }
        }

        private Vector2Int FindItemTopLeftCorner(Vector2Int position, InventoryItem item)
        {
            // Find the top-left (minimum x,y) position of the item
            int minX = position.x;
            int minY = position.y;

            // Scan in decreasing x direction
            while (minX > 0 && minX - 1 < gridSize.x && slots[minX - 1, position.y].item == item)
                minX--;

            // Scan in decreasing y direction    
            while (minY > 0 && minY - 1 < gridSize.y && slots[position.x, minY - 1].item == item)
                minY--;

            return new Vector2Int(minX, minY);
        }
    }
}
