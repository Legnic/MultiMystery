using System.Collections.Generic;
using UnityEngine;

namespace LGH
{
    // Every item kind in the game. Items travel over the network as their index in this list,
    // so host and clients must use the same asset (it lives in a Resources folder).
    [CreateAssetMenu(fileName = "ItemDatabase_LGH", menuName = "LGH/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        public List<ItemData> items = new List<ItemData>();

        static ItemDatabase instance;
        public static ItemDatabase Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<ItemDatabase>("ItemDatabase_LGH");
                return instance;
            }
        }

        public int IndexOf(ItemData item) { return item == null ? -1 : items.IndexOf(item); }

        public ItemData Get(int index) { return index >= 0 && index < items.Count ? items[index] : null; }

        public ItemData Find(string itemId)
        {
            foreach (var it in items) if (it != null && it.itemId == itemId) return it;
            return null;
        }
    }
}
