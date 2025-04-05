using Assets.Game.Inventory.Model;
using Assets.Game.Inventory.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory
{
    public class InventoryToggle : MonoBehaviour
    {
        [Header("Input Settings")]
        [SerializeField] private KeyCode toggleInventoryKey = KeyCode.I;

        [Header("Components")]
        [SerializeField] private MouseCameraController cameraController;
        [SerializeField] private RealisticPlayerMovement playerMovement;

        private bool inventoryOpen = false;

        private void Start()
        {
            // Auto-find components if not assigned
            if (cameraController == null)
                cameraController = FindObjectOfType<MouseCameraController>();

            if (playerMovement == null)
                playerMovement = FindObjectOfType<RealisticPlayerMovement>();

            // Check if we found them
            if (cameraController == null)
                Debug.LogWarning("No MouseCameraController found. Camera won't be disabled when inventory is open.");

            if (playerMovement == null)
                Debug.LogWarning("No RealisticPlayerMovement found. Player movement won't be disabled when inventory is open.");

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnContainerClosed += OnContainerClosed;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleInventoryKey))
            {
                ToggleInventory();
            }
        }

        public void ToggleInventory()
        {
            inventoryOpen = !inventoryOpen;

            // Enable/disable player and camera controls
            if (cameraController != null)
                cameraController.SetCameraControlEnabled(!inventoryOpen);

            if (playerMovement != null)
                playerMovement.enabled = !inventoryOpen;

            // Show/hide the cursor
            Cursor.lockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = inventoryOpen;

            // Open/close inventory UI
            if (inventoryOpen)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.playerInventory != null)
                {
                    InventoryManager.Instance.OpenContainer(InventoryManager.Instance.playerInventory);
                }
                else
                {
                    Debug.LogError("Cannot open inventory - InventoryManager or playerInventory is null");
                }
            }
            else
            {
                if (InventoryUIManager.Instance != null)
                {
                    InventoryUIManager.Instance.CloseAllContainers();
                }
            }
        }

        // Public method that can be called from other scripts
        public void SetInventoryOpen(bool open)
        {
            if (open != inventoryOpen)
                ToggleInventory();
        }

        private void OnContainerClosed(InventoryContainer container)
        {
            inventoryOpen = false;
            ToggleInventory();
        }
    }
}
