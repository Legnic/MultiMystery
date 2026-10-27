using System;
using UnityEngine;

namespace PotionCraft
{
<<<<<<< Updated upstream
    // 1층 쪽지에 적힌 최종 레시피. 문구와 정답 순서(3칸)를 데이터로 둔다.
=======
    // 1층 쪽지에 적힌 최종 레시피. 문구(또는 그림)와 정답 순서(3칸)를 데이터로 둔다.
>>>>>>> Stashed changes
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Final Recipe", fileName = "FinalRecipeData")]
    public class FinalRecipeData : ScriptableObject
    {
        public string title = "쪽지";
<<<<<<< Updated upstream
        public string noteText = "빨간색 가루 + 파란색 가루 + 빨간색 가루";
        public PowderColor[] sequence = { PowderColor.Red, PowderColor.Blue, PowderColor.Red };
=======
        public string noteText = "빨간색 가루 + 파란색 가루 + 분홍색 가루";
        [Tooltip("쪽지 그림. 있으면 글 대신 이 그림을 화면에 띄운다 (그림 내용과 정답 순서를 맞출 것).")]
        public Texture2D noteImage;
        public PowderColor[] sequence = { PowderColor.Red, PowderColor.Blue, PowderColor.Pink };
>>>>>>> Stashed changes

        public bool Matches(int[] slotColors, int count)
        {
            if (sequence == null || count != sequence.Length) return false;
            for (int i = 0; i < count; i++) if (slotColors[i] != (int)sequence[i]) return false;
            return true;
        }
    }
}
