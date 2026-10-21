using UnityEngine;

namespace Gim002
{
    // GIM-002에서 쓰는 열쇠 정보 (음악실 열쇠 = 높은음자리표, 태엽 열쇠 = 나비 — 가안).
    // 열쇠와 사용처(문, 태엽 구멍)가 같은 에셋을 가리키면 "문양이 맞는" 것으로 본다. 문양 이름이 바뀌면 이 에셋만 고친다.
    [CreateAssetMenu(menuName = "GIM-002/Key Data", fileName = "Gim002Key")]
    public class Gim002KeyData : ScriptableObject
    {
        [Tooltip("화면에 표시할 이름 (예: 음악실 열쇠)")]
        public string displayName = "열쇠";
        [Tooltip("열쇠에 새겨진 문양 (예: 높은음자리표)")]
        public string emblem = "";
    }
}
