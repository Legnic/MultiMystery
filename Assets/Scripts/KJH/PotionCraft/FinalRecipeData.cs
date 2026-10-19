using System;
using UnityEngine;

namespace PotionCraft
{
    // 1층 쪽지에 적힌 최종 레시피. 문구와 정답 순서(3칸)를 데이터로 둔다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Final Recipe", fileName = "FinalRecipeData")]
    public class FinalRecipeData : ScriptableObject
    {
        public string title = "쪽지";
        public string noteText = "빨간색 가루 + 파란색 가루 + 빨간색 가루";
        public PowderColor[] sequence = { PowderColor.Red, PowderColor.Blue, PowderColor.Red };

        public bool Matches(int[] slotColors, int count)
        {
            if (sequence == null || count != sequence.Length) return false;
            for (int i = 0; i < count; i++) if (slotColors[i] != (int)sequence[i]) return false;
            return true;
        }
    }
}
