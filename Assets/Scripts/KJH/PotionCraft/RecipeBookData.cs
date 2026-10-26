using System;
using UnityEngine;

namespace PotionCraft
{
    // 2층 제조법 책 내용. 재료와 물만 적고 결과 색은 적지 않는다 (함정 항목 포함). 항목 순서는 여기서 바꾼다.
    // 모든 쪽에 그림(image)이 있으면 책을 그림으로 한 쪽씩 넘기고, 하나라도 비어 있으면 글(펼침면 2쪽씩)로 보인다.
    // label이 있는 쪽(예: 맨 앞 "안내")은 조합법 쪽 번호를 세지 않고 그 이름으로 표시한다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Recipe Book", fileName = "RecipeBookData")]
    public class RecipeBookData : ScriptableObject
    {
        [Serializable] public class Page { public string label; public string entryA; public string entryB; public Texture2D image; }

        public string title = "제조법";
        public Page[] pages;

        // 화면 아래에 쓸 쪽 이름. label이 없는 쪽은 label 있는 쪽을 빼고 센 "조합법 N쪽".
        public string PageName(int index)
        {
            if (pages == null || index < 0 || index >= pages.Length) return string.Empty;
            if (pages[index] != null && !string.IsNullOrEmpty(pages[index].label)) return pages[index].label;
            int n = 0;
            for (int i = 0; i <= index; i++)
                if (pages[i] == null || string.IsNullOrEmpty(pages[i].label)) n++;
            return "조합법 " + n + "쪽";
        }

        public bool UsesImages
        {
            get
            {
                if (pages == null || pages.Length == 0) return false;
                foreach (var p in pages) if (p == null || p.image == null) return false;
                return true;
            }
        }
    }
}
