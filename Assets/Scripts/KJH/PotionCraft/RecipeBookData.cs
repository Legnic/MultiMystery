using System;
using UnityEngine;

namespace PotionCraft
{
    // 2층 제조법 책 내용. 재료와 물만 적고 결과 색은 적지 않는다 (함정 항목 포함). 항목 순서는 여기서 바꾼다.
    [CreateAssetMenu(menuName = "KJH/PotionCraft/Recipe Book", fileName = "RecipeBookData")]
    public class RecipeBookData : ScriptableObject
    {
        [Serializable] public class Page { public string entryA; public string entryB; }

        public string title = "제조법";
        public Page[] pages;
    }
}
