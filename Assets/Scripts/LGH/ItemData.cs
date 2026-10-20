using UnityEngine;

namespace LGH
{
    // One kind of item the player can carry (key, letter, coin...).
    // Make one with: Project window > right click > Create > LGH > Item Data,
    // then add it to the list in Assets/Objects/LGH/Resources/ItemDatabase_LGH.
    [CreateAssetMenu(fileName = "Item_New", menuName = "LGH/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Unique id, e.g. key_brass. Scripts can look items up by this.")]
        public string itemId;

        [Tooltip("Name shown in the inventory and pickup prompt (e.g. 놋쇠 열쇠).")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("Short description shown when the item is selected in the inventory.")]
        public string description;

        [Tooltip("Inventory icon. Optional - the name is written in the slot when empty.")]
        public Texture2D icon;

        [Tooltip("Several of these share one slot (coins, matches).")]
        public bool stackable = false;
        public int maxStack = 99;

        [TextArea(4, 20)]
        [Tooltip("If filled in, the item can be read from the inventory with [F] (letters, diaries).")]
        public string readText;

        public bool Readable { get { return !string.IsNullOrEmpty(readText); } }
        public string NameOrFallback { get { return string.IsNullOrEmpty(displayName) ? name : displayName; } }
    }
}
