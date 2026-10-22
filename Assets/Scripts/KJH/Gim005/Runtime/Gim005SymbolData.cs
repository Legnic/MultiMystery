using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gim005
{
    // 문양 13종 목록. 덤불 명판과 2층 버튼이 모두 이 에셋 하나를 보고 그린다.
    // 이미지가 나오기 전에는 sprite를 비워 두면 숫자(label)가 표시되고,
    // sprite를 채우면 명판·버튼의 Quad에 그 그림이 나오고 숫자는 자동으로 숨는다 (씬/프리팹 수정 불필요).
    [CreateAssetMenu(menuName = "GIM-005/Symbol Data", fileName = "Gim005SymbolData")]
    public class Gim005SymbolData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("문양 ID (기획서 표 기준, 예: SYM_01)")]
            public string id = "SYM_01";
            [Tooltip("임시 표시 글자. sprite가 비어 있을 때만 보인다")]
            public string label = "1";
            [Tooltip("문양 이미지. 채우면 Quad에 그림이 나오고 label은 숨는다")]
            public Sprite sprite;
        }

        [Tooltip("버튼 패널 순서대로 (5-4-4 배열: 1줄 1~5, 2줄 6~9, 3줄 10~13)")]
        public List<Entry> symbols = new List<Entry>();

        public int Count { get { return symbols.Count; } }

        public Entry Get(int index)
        {
            return index >= 0 && index < symbols.Count ? symbols[index] : null;
        }

        public string LabelOf(int index)
        {
            var e = Get(index);
            return e != null ? e.label : (index + 1).ToString();
        }

        // 기본 13종 (SYM_01 ~ SYM_13, 숫자 1 ~ 13)
        public void FillDefaults(int count = 13)
        {
            symbols.Clear();
            for (int i = 0; i < count; i++)
                symbols.Add(new Entry { id = "SYM_" + (i + 1).ToString("00"), label = (i + 1).ToString() });
        }
    }
}
