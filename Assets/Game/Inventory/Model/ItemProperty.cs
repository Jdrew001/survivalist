using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Game.Inventory.Model
{
    [Serializable]
    public class ItemProperty
    {
        public string propertyName;
        public float value;
    }

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Ammunition,
        Resource,
        Tool,
        Currency,
        Quest,
        Generic
    }
}
