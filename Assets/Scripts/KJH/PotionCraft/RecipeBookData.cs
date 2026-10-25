using System;
using UnityEngine;

namespace PotionCraft
{
    // 2층 제조법 책 내용. 재료와 물만 적고 결과 색은 적지 않는다 (함정 항목 포함). 항목 순서는 여기서 바꾼다.
    // 모든 쪽에 그림(image)이 있으면 책을 그림으로 한 쪽씩 넘기고, 하나라도 비어 있으면 글(펼침면 2쪽씩)로 보인다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Recipe Book", fileName = "RecipeBookData")]
    public class RecipeBookData : ScriptableObject
    {
        [Serializable] public class Page { public string entryA; public string entryB; public Texture2D image; }

        public string title = "제조법";
        public Page[] pages;

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
