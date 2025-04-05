using Assets.Game.Inventory.Model;
using Assets.Game.Inventory.ScriptableObjects;
using Assets.Game.Inventory.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.Helpers
{
    public class ContainerInteraction : MonoBehaviour
    {
        [SerializeField] private string containerName = "Storage Container";
        [SerializeField] private Vector2Int containerSize = new Vector2Int(5, 4);
        [SerializeField] private ContainerType containerType = ContainerType.Storage;
        [SerializeField] private string uniqueContainerId;

        private InventoryContainer container;

        private void Awake()
        {
            // Generate unique ID if not specified
            if (string.IsNullOrEmpty(uniqueContainerId))
            {
                uniqueContainerId = System.Guid.NewGuid().ToString();
            }
        }

        public void OpenContainer()
        {
            if (InventoryManager.Instance == null)
                return;

            // Create or get the container
            if (container == null)
            {
                container = InventoryManager.Instance.CreateContainer(
                    uniqueContainerId,
                    containerName,
                    containerSize,
                    containerType
                );

                // If this is the first time opening, you could populate with initial items
                // InitializeContainerContents();
            }

            // Open the container UI
            InventoryManager.Instance.OpenContainer(container);

            // You might also want to open the player inventory alongside it
            if (!IsPlayerInventoryOpen())
            {
                InventoryManager.Instance.OpenContainer(InventoryManager.Instance.playerInventory);
            }
        }

        private bool IsPlayerInventoryOpen()
        {
            return InventoryUIManager.Instance != null &&
                   InventoryUIManager.Instance.IsContainerOpen(InventoryManager.Instance.playerInventory.id);
        }

        // Optional: Initialize container with items
        private void InitializeContainerContents()
        {
            // Example of adding initial items to a container
            if (container != null && InventoryManager.Instance != null)
            {
                ItemDatabase itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
                if (itemDatabase != null)
                {
                    // Add some random items, for example
                    TryAddRandomItem(itemDatabase, "health_potion", new Vector2Int(0, 0));
                    TryAddRandomItem(itemDatabase, "ammo_rifle", new Vector2Int(1, 0));
                }
            }
        }

        private void TryAddRandomItem(ItemDatabase database, string itemId, Vector2Int position)
        {
            InventoryItem item = database.CreateItemInstance(itemId);
            if (item != null)
            {
                InventoryManager.Instance.AddItemToContainer(item, container, position);
            }
        }
    }
}
