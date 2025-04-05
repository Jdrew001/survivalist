using Assets.Game.Inventory.ScriptableObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.Helpers
{
    public class WorldItem : MonoBehaviour
    {
        [SerializeField] public string itemId;
        [SerializeField] private int initialStackSize = 1;
        [SerializeField] private ItemDatabase itemDatabase;

        private InventoryItem cachedItem;

        private void Start()
        {
            if (itemDatabase == null)
            {
                // Try to find item database in resource folder
                itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");

                if (itemDatabase == null)
                {
                    Debug.LogError("WorldItem: No ItemDatabase assigned or found in Resources!");
                }
            }
        }

        public InventoryItem GetInventoryItem()
        {
            if (cachedItem != null)
                return cachedItem;

            if (itemDatabase == null)
            {
                Debug.LogError("WorldItem: No ItemDatabase available to create item!");
                return null;
            }

            // Create a new instance of the item
            cachedItem = itemDatabase.CreateItemInstance(itemId);

            if (cachedItem != null)
            {
                // Set initial stack size
                cachedItem.currentStackSize = initialStackSize;
            }

            return cachedItem;
        }

        // Optional: Show a visual highlight when player looks at this item
        public void Highlight(bool isHighlighted)
        {
            // Implement highlight effect (outline shader, emissive boost, etc.)
        }
    }
}
