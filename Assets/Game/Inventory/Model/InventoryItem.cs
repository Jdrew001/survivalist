using Assets.Game.Inventory.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Game.Inventory
{
    [Serializable]
    public class InventoryItem
    {
        public string id;
        public string displayName;
        public string description;
        public Sprite icon;
        public ItemType itemType;
        public Vector2Int size = Vector2Int.one; // Size in grid cells (1x1 default)
        public int maxStackSize = 1;
        public bool isStackable => maxStackSize > 1;
        public List<ItemProperty> properties = new List<ItemProperty>();

        [NonSerialized]
        public int currentStackSize = 1;
    }
}
