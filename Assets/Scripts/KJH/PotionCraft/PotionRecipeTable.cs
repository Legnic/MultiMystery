using System;
using UnityEngine;

namespace PotionCraft
{
    // 1층 제조 결과표. 유효한 조합만 등록하고, 나머지는 전부 defaultResult(검정)가 된다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Recipe Table", fileName = "PotionRecipeTable")]
    public class PotionRecipeTable : ScriptableObject
    {
        [Serializable] public class Entry { public MaterialId material; public WaterId water; public PowderColor result; }

        public PowderColor defaultResult = PowderColor.Black;
        public Entry[] entries;

        public PowderColor Craft(MaterialId material, WaterId water)
        {
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && e.material == material && e.water == water) return e.result;
            return defaultResult;
        }
    }
}
