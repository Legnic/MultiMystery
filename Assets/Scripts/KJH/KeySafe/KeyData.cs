using UnityEngine;

// 열쇠 하나의 정보를 담는 데이터 에셋 (ScriptableObject).
// 금고(LockedSafe)는 "요구하는 KeyData"를 하나 들고 있고, 플레이어 인벤토리(PlayerInventory)에
// 같은 keyId의 열쇠가 있을 때만 열린다. 열쇠 이름·소모 여부 같은 기획 값은 코드가 아니라 이 에셋에서 고친다.
// 만드는 법: Project 창에서 우클릭 → Create → KJH → Key Data.
[CreateAssetMenu(fileName = "Key_New", menuName = "KJH/Key Data")]
public class KeyData : ScriptableObject
{
    [Tooltip("열쇠를 구분하는 고유 ID (예: key_study). 금고와의 일치 판정, 나중의 네트워크 동기화는 이 ID로 한다. 다른 열쇠와 겹치면 안 된다.")]
    public string keyId;

    [Tooltip("화면에 보여줄 열쇠 이름 (예: 서재 열쇠). '[이름]을(를) 얻었다' 문구에 쓰인다.")]
    public string displayName;

    [Tooltip("나중에 열쇠 목록 UI를 만들 때 쓸 아이콘. 지금은 비워둬도 된다.")]
    public Sprite icon;

    [Tooltip("금고를 열 때 열쇠를 인벤토리에서 없앨지. 대부분의 열쇠는 한 번 쓰면 끝이라 기본값은 true.")]
    public bool consumeOnUse = true;

    [Tooltip("(추후 모션용) 열쇠구멍에 꽂히는 3D 열쇠 모델. 지금은 비워둔다 — KeyInsertOpenSequence 같은 연출이 생기면 그때 쓴다.")]
    public GameObject worldModelPrefab;

    // 이름이 비어 있으면 에셋 이름이라도 보여줘서 문구가 "을(를) 얻었다"처럼 깨지지 않게 한다.
    public string DisplayNameOrFallback => string.IsNullOrEmpty(displayName) ? name : displayName;

#if UNITY_EDITOR
    // Inspector에서 값을 바꿀 때마다 다른 KeyData와 keyId가 겹치는지 검사한다 (ClueData와 같은 방식).
    // 겹치면 엉뚱한 열쇠로 금고가 열리는 버그가 생기므로 미리 경고한다. 에디터 전용이라 빌드에는 빠진다.
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(keyId)) return;

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:KeyData"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            KeyData other = UnityEditor.AssetDatabase.LoadAssetAtPath<KeyData>(path);
            if (other != null && other != this && other.keyId == keyId)
            {
                Debug.LogWarning($"[KeyData] keyId '{keyId}'가 '{path}'와 겹칩니다. 서로 다른 ID를 써주세요.", this);
            }
        }
    }
#endif
}
