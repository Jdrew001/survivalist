using Assets.Game.Inventory.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.ScriptableObjects
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Inventory/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();
        private Dictionary<string, InventoryItem> itemsById;

        public IReadOnlyList<InventoryItem> Items => items;

        private void OnEnable()
        {
            InitializeDictionary();
        }

        private void InitializeDictionary()
        {
            itemsById = new Dictionary<string, InventoryItem>();

            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.id) && !itemsById.ContainsKey(item.id))
                {
                    itemsById.Add(item.id, item);
                }
                else if (itemsById.ContainsKey(item.id))
                {
                    Debug.LogWarning($"Duplicate item ID found in database: {item.id}");
                }
            }
        }

        public InventoryItem GetItemById(string id)
        {
            if (itemsById == null)
                InitializeDictionary();

            if (itemsById.TryGetValue(id, out InventoryItem item))
                return item;

            return null;
        }

        public InventoryItem CreateItemInstance(string id)
        {
            InventoryItem template = GetItemById(id);
            if (template == null)
                return null;

            // Create a deep copy of the item
            InventoryItem instance = new InventoryItem
            {
                id = template.id,
                displayName = template.displayName,
                description = template.description,
                icon = template.icon,
                itemType = template.itemType,
                size = template.size,
                maxStackSize = template.maxStackSize,
                currentStackSize = 1,
                properties = new List<ItemProperty>()
            };

            // Copy properties
            foreach (var prop in template.properties)
            {
                instance.properties.Add(new ItemProperty
                {
                    propertyName = prop.propertyName,
                    value = prop.value
                });
            }

            return instance;
        }
    }
}
