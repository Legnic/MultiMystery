using UnityEngine;

// 단서 하나의 정보를 담는 데이터 에셋 (ScriptableObject).
// 코드가 아니라 "에셋 파일"로 만들어 두면, 기획/디자인 팀원이 코드를 몰라도 Inspector에서 이름/설명을 고칠 수 있다.
// 만드는 법: Project 창에서 우클릭 → Create → KJH → Clue Data.
[CreateAssetMenu(fileName = "Clue_New", menuName = "KJH/Clue Data")]
public class ClueData : ScriptableObject
{
    [Tooltip("단서를 구분하는 고유 ID (예: parchment_01). 다른 단서와 겹치면 안 된다. 획득 여부 저장/나중의 네트워크 동기화는 이 ID로 한다.")]
    public string clueId;

    [Tooltip("화면에 보여줄 단서 이름 (예: 낡은 양피지). 획득 알림에 쓰인다.")]
    public string displayName;

    [Tooltip("단서 설명. 여러 줄 입력 가능. 일반 단서(ViewableClue)는 살펴볼 때 화면 오른쪽에 이 글을 보여준다.")]
    [TextArea(3, 10)]
    public string description;

    [Tooltip("단서 이미지. 일반 단서(ViewableClue)는 살펴볼 때 화면 가운데에 이 이미지를 보여준다. " +
             "비워두면 이미지 없이 글만 보여준다. 텍스처 Import 설정의 Texture Type을 'Sprite (2D and UI)'로 해야 여기에 넣을 수 있다.")]
    public Sprite image;

#if UNITY_EDITOR
    // Inspector에서 값을 바꿀 때마다, 프로젝트 안의 다른 ClueData와 clueId가 겹치는지 검사해서 경고를 띄운다.
    // 에디터에서만 필요한 검사라 #if UNITY_EDITOR로 감싸서 빌드에는 포함되지 않게 한다.
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(clueId)) return;

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ClueData"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ClueData other = UnityEditor.AssetDatabase.LoadAssetAtPath<ClueData>(path);
            if (other != null && other != this && other.clueId == clueId)
            {
                Debug.LogWarning($"[ClueData] clueId '{clueId}'가 '{path}'와 겹칩니다. 서로 다른 ID를 써주세요.", this);
            }
        }
    }
#endif
}
