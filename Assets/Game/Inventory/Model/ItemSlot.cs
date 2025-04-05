using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory.Model
{
    [Serializable]
    public class ItemSlot
    {
        public Vector2Int position;
        public InventoryItem item;
        public bool isEmpty => item == null;
        public ItemType[] allowedTypes = null; // null means all types allowed

        public bool CanAcceptItem(InventoryItem item)
        {
            if (allowedTypes == null || allowedTypes.Length == 0)
                return true;

            foreach (var type in allowedTypes)
            {
                if (item.itemType == type)
                    return true;
            }

            return false;
        }
    }
}
