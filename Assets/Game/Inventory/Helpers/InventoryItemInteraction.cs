using Assets.Game.Inventory.Model;
using Assets.Game.Inventory.UI;
using NUnit.Framework.Internal.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.Helpers
{
    public class InventoryItemInteraction : MonoBehaviour
    {
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private LayerMask itemLayer;
        [SerializeField] private Transform interactionOrigin;
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private KeyCode inventoryToggleKey = KeyCode.I;

        private Camera mainCamera;
        private bool inventoryOpen = false;

        private void Start()
        {
            mainCamera = Camera.main;

            if (interactionOrigin == null)
                interactionOrigin = transform;
        }

        private void Update()
        {
            // Check for interaction input
            if (Input.GetKeyDown(interactionKey))
            {
                TryInteractWithItem();
            }

            // Check for inventory toggle
            if (Input.GetKeyDown(inventoryToggleKey))
            {
                ToggleInventory();
            }
        }

        private void ToggleInventory()
        {
            inventoryOpen = !inventoryOpen;

            if (inventoryOpen)
            {
                // Open player inventory
                if (InventoryManager.Instance != null && InventoryManager.Instance.playerInventory != null)
                {
                    InventoryManager.Instance.OpenContainer(InventoryManager.Instance.playerInventory);
                }
            }
            else
            {
                // Close all open inventories
                if (InventoryUIManager.Instance != null)
                {
                    InventoryUIManager.Instance.CloseAllContainers();
                }
            }
        }

        private void TryInteractWithItem()
        {
            // First try camera raycast for more precise interaction
            if (TryRaycastInteraction())
                return;

            // Fall back to sphere cast if raycast fails
            TrySpherecastInteraction();
        }

        private bool TryRaycastInteraction()
        {
            if (mainCamera == null)
                return false;

            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, interactionRange, itemLayer))
            {
                WorldItem worldItem = hit.collider.GetComponent<WorldItem>();
                if (worldItem != null)
                {
                    InteractWithWorldItem(worldItem);
                    return true;
                }

                ContainerInteraction container = hit.collider.GetComponent<ContainerInteraction>();
                if (container != null)
                {
                    OpenContainer(container);
                    return true;
                }
            }

            return false;
        }

        private bool TrySpherecastInteraction()
        {
            Collider[] colliders = Physics.OverlapSphere(interactionOrigin.position, interactionRange, itemLayer);

            // Find the closest interactable
            float closestDistance = float.MaxValue;
            WorldItem closestItem = null;
            ContainerInteraction closestContainer = null;

            foreach (Collider collider in colliders)
            {
                float distance = Vector3.Distance(interactionOrigin.position, collider.transform.position);

                WorldItem worldItem = collider.GetComponent<WorldItem>();
                if (worldItem != null && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestItem = worldItem;
                }

                ContainerInteraction container = collider.GetComponent<ContainerInteraction>();
                if (container != null && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestContainer = container;
                }
            }

            // Interact with closest item/container
            if (closestItem != null)
            {
                InteractWithWorldItem(closestItem);
                return true;
            }
            else if (closestContainer != null)
            {
                OpenContainer(closestContainer);
                return true;
            }

            return false;
        }

        private void InteractWithWorldItem(WorldItem worldItem)
        {
            if (worldItem != null && InventoryManager.Instance != null)
            {
                // Try to add directly to player inventory
                InventoryItem item = worldItem.GetInventoryItem();

                if (TryAddItemToPlayerInventory(item))
                {
                    // Successfully added to inventory - destroy world representation
                    Destroy(worldItem.gameObject);

                    // Play pickup sound or show effect
                    PlayPickupEffect(worldItem.transform.position);
                }
                else
                {
                    Debug.Log("Inventory full - couldn't pick up item");
                    // Could show UI message here
                }
            }
        }

        private void PlayPickupEffect(Vector3 position)
        {
            // This could play a sound or spawn a particle effect
            // For example:
            // AudioSource.PlayClipAtPoint(pickupSound, position);
        }

        private void OpenContainer(ContainerInteraction containerInteraction)
        {
            if (containerInteraction != null && InventoryManager.Instance != null)
            {
                containerInteraction.OpenContainer();
            }
        }

        private bool TryAddItemToPlayerInventory(InventoryItem item)
        {
            if (InventoryManager.Instance == null || InventoryManager.Instance.playerInventory == null)
                return false;

            // First check for stacking with existing items
            if (TryStackWithExistingItems(item))
                return true;

            // If can't stack, find first available spot
            return TryAddToFirstAvailableSlot(item);
        }

        private bool TryStackWithExistingItems(InventoryItem item)
        {
            if (!item.isStackable)
                return false;

            InventoryContainer playerInventory = InventoryManager.Instance.playerInventory;

            // Find existing stacks of the same item
            for (int x = 0; x < playerInventory.gridSize.x; x++)
            {
                for (int y = 0; y < playerInventory.gridSize.y; y++)
                {
                    ItemSlot slot = playerInventory.slots[x, y];
                    if (!slot.isEmpty && slot.item.id == item.id && slot.item.isStackable)
                    {
                        // Found matching stack with room
                        if (slot.item.currentStackSize < slot.item.maxStackSize)
                        {
                            int spaceAvailable = slot.item.maxStackSize - slot.item.currentStackSize;
                            int amountToAdd = Mathf.Min(item.currentStackSize, spaceAvailable);

                            // Add to existing stack
                            slot.item.currentStackSize += amountToAdd;
                            item.currentStackSize -= amountToAdd;

                            // If we've added all of the item, we're done
                            if (item.currentStackSize <= 0)
                                return true;
                        }
                    }
                }
            }

            // If we get here and item stack size is 0, we successfully stacked everything
            return item.currentStackSize <= 0;
        }

        private bool TryAddToFirstAvailableSlot(InventoryItem item)
        {
            InventoryContainer playerInventory = InventoryManager.Instance.playerInventory;

            // Try every position to see if the item can fit
            for (int x = 0; x < playerInventory.gridSize.x - (item.size.x - 1); x++)
            {
                for (int y = 0; y < playerInventory.gridSize.y - (item.size.y - 1); y++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    if (playerInventory.CanPlaceItemAt(item, position))
                    {
                        // Found a valid position, add the item
                        return InventoryManager.Instance.AddItemToContainer(item, playerInventory, position);
                    }
                }
            }

            return false;
        }
    }
}
