using Assets.Game.Inventory.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [SerializeField] private List<InventoryContainer> containers = new List<InventoryContainer>();
        public InventoryContainer playerInventory;

        public event Action<InventoryContainer> OnContainerOpened;
        public event Action<InventoryContainer> OnContainerClosed;
        public event Action<InventoryItem, InventoryContainer, Vector2Int> OnItemAdded;
        public event Action<InventoryItem, InventoryContainer, Vector2Int> OnItemRemoved;

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
            }

            InitializeContainers();
        }

        private void InitializeContainers()
        {
            // Create player inventory if not already assigned
            if (playerInventory == null)
            {
                playerInventory = new InventoryContainer(
                    "player_inventory",
                    "Player Inventory",
                    new Vector2Int(8, 5),
                    ContainerType.PlayerInventory
                );
                containers.Add(playerInventory);
            }
        }

        public void OpenContainer(InventoryContainer container)
        {
            OnContainerOpened?.Invoke(container);
        }

        public void CloseContainer(InventoryContainer container)
        {
            OnContainerClosed?.Invoke(container);
        }

        public bool AddItemToContainer(InventoryItem item, InventoryContainer container, Vector2Int position)
        {
            if (container.TryAddItem(item, position))
            {
                OnItemAdded?.Invoke(item, container, position);
                return true;
            }
            return false;
        }

        public void RemoveItemFromContainer(InventoryContainer container, Vector2Int position)
        {
            if (position.x < 0 || position.y < 0 ||
                position.x >= container.gridSize.x ||
                position.y >= container.gridSize.y)
                return;

            if (container.slots[position.x, position.y].isEmpty)
                return;

            InventoryItem item = container.slots[position.x, position.y].item;
            Vector2Int topLeft = FindItemTopLeftCorner(container, position);

            container.RemoveItemAt(position);
            OnItemRemoved?.Invoke(item, container, topLeft);
        }

        private Vector2Int FindItemTopLeftCorner(InventoryContainer container, Vector2Int position)
        {
            if (container.slots[position.x, position.y].isEmpty)
                return position;

            InventoryItem item = container.slots[position.x, position.y].item;

            // Find the top-left (minimum x,y) position of the item
            int minX = position.x;
            int minY = position.y;

            // Scan in decreasing x direction
            while (minX > 0 && container.slots[minX - 1, position.y].item == item)
                minX--;

            // Scan in decreasing y direction    
            while (minY > 0 && container.slots[position.x, minY - 1].item == item)
                minY--;

            return new Vector2Int(minX, minY);
        }

        public bool CanStackItems(InventoryItem item1, InventoryItem item2)
        {
            return item1.id == item2.id && item1.isStackable && item2.isStackable;
        }

        public bool TryStackItems(InventoryItem source, InventoryItem target)
        {
            if (!CanStackItems(source, target))
                return false;

            int availableSpace = target.maxStackSize - target.currentStackSize;

            if (availableSpace <= 0)
                return false;

            int amountToAdd = Mathf.Min(source.currentStackSize, availableSpace);
            target.currentStackSize += amountToAdd;
            source.currentStackSize -= amountToAdd;

            return source.currentStackSize == 0;
        }

        public InventoryContainer CreateContainer(string id, string displayName, Vector2Int size, ContainerType type)
        {
            InventoryContainer container = new InventoryContainer(id, displayName, size, type);
            containers.Add(container);
            return container;
        }

        public void DestroyContainer(string containerId)
        {
            containers.RemoveAll(c => c.id == containerId);
        }
    }
}
