using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 지금까지 획득한 단서 목록을 관리하는 컴포넌트 (씬에 하나만 둔다).
// "단서 획득 처리"는 반드시 이 클래스의 TryAcquire 한 곳에서만 일어나게 한다.
// 이유: 나중에 2인 협동 네트워크를 붙일 때, 이 한 곳에서만 "획득 상태"를 상대에게 보내면 되도록 하기 위함이다.
// 사용법: 씬의 관리 오브젝트(지금은 Player)에 붙인다.
public class ClueJournal : MonoBehaviour
{
    // 단서를 처음 획득했을 때 알려주는 이벤트. 단서 목록 UI 등 나중에 만들 기능이 여기에 구독하면 된다.
    public event Action<ClueData> OnClueAcquired;

    // 획득한 단서 ID 목록. 순서(획득 순)를 보존하려고 List를 쓰고, 빠른 중복 검사는 HashSet으로 한다.
    private readonly List<string> acquiredIds = new List<string>();
    private readonly HashSet<string> acquiredIdSet = new HashSet<string>();

    // 외부에서는 목록을 읽기만 할 수 있다 (직접 추가/삭제하지 못하게 해서 획득 경로를 TryAcquire 하나로 유지).
    public IReadOnlyList<string> AcquiredClueIds => acquiredIds;

    // 단서 획득을 시도한다. 처음 얻는 단서면 true, 이미 얻었거나 잘못된 데이터면 false.
    // 호출하는 쪽(InspectableClue)은 true일 때만 "단서 획득" 알림을 띄운다.
    public bool TryAcquire(ClueData clue)
    {
        if (clue == null || string.IsNullOrEmpty(clue.clueId))
        {
            Debug.LogWarning("[ClueJournal] clueId가 비어 있는 단서는 획득할 수 없습니다.", clue);
            return false;
        }

        // HashSet.Add는 이미 있으면 false를 돌려주므로 "처음인지" 검사와 추가를 한 번에 할 수 있다.
        if (!acquiredIdSet.Add(clue.clueId)) return false;

        acquiredIds.Add(clue.clueId);
        OnClueAcquired?.Invoke(clue);
        return true;
    }

    // 특정 단서를 이미 획득했는지 확인한다 (예: 단서를 얻어야 열리는 문 같은 기능에서 사용).
    public bool HasClue(string clueId)
    {
        return !string.IsNullOrEmpty(clueId) && acquiredIdSet.Contains(clueId);
    }
}
