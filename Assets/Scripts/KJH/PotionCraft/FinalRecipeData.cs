using System;
using UnityEngine;

namespace PotionCraft
{
    // 1층 쪽지에 적힌 최종 레시피. 문구(또는 그림)와 정답 가루 3개(순서 무관)를 데이터로 둔다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Final Recipe", fileName = "FinalRecipeData")]
    public class FinalRecipeData : ScriptableObject
    {
        public string title = "쪽지";
        public string noteText = "빨간색 가루 + 파란색 가루 + 분홍색 가루";
        [Tooltip("쪽지 그림. 있으면 글 대신 이 그림을 화면에 띄운다 (그림 내용과 정답 가루를 맞출 것).")]
        public Texture2D noteImage;
        public PowderColor[] sequence = { PowderColor.Red, PowderColor.Blue, PowderColor.Pink };

        // 넣은 순서는 보지 않고, 같은 가루가 같은 개수만큼 들어갔는지만 본다.
        public bool Matches(int[] slotColors, int count)
        {
            if (sequence == null || count != sequence.Length) return false;
            var used = new bool[count];
            foreach (var want in sequence)
            {
                int found = -1;
                for (int i = 0; i < count; i++)
                    if (!used[i] && slotColors[i] == (int)want) { found = i; break; }
                if (found < 0) return false;
                used[found] = true;
            }
            return true;
        }
    }
}
