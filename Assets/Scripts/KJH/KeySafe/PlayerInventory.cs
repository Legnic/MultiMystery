using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 한 명이 가지고 있는 열쇠 목록. (사진은 PhotoInventory가 따로 관리한다)
// 전역 싱글톤으로 만들지 않고 "플레이어마다 하나씩" 붙이는 이유: 2인 협동에서 누가 열쇠를 들고 있는지가
// 퍼즐의 일부가 될 수 있고, 금고는 상호작용한 바로 그 플레이어의 인벤토리만 검사해야 하기 때문이다.
// 사용법: Player 오브젝트에 붙인다.
public class PlayerInventory : MonoBehaviour
{
    // 열쇠가 추가/제거될 때마다 알려주는 이벤트. 나중에 열쇠 목록 UI가 여기에 구독하면 된다.
    public event Action OnInventoryChanged;

    // 가지고 있는 열쇠들. 획득 순서를 보존하려고 List를 쓴다 (개수가 적어 검색 비용은 무시할 수준).
    private readonly List<KeyData> keys = new List<KeyData>();

    // 외부에서는 읽기만 가능하게 해서, 추가/제거 경로를 AddKey/RemoveKey 두 곳으로 고정한다 (나중의 동기화 지점).
    public IReadOnlyList<KeyData> Keys => keys;

    // 열쇠를 넣는다. 같은 keyId가 이미 있으면 넣지 않고 false (같은 열쇠 두 개는 의미가 없으므로).
    public bool AddKey(KeyData key)
    {
        if (key == null || string.IsNullOrEmpty(key.keyId))
        {
            Debug.LogWarning("[PlayerInventory] keyId가 비어 있는 열쇠는 넣을 수 없습니다.", key);
            return false;
        }
        if (HasKey(key)) return false;

        keys.Add(key);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // 이 열쇠를 가지고 있는지. 에셋 참조가 아니라 keyId로 비교한다
    // (같은 열쇠를 실수로 에셋 두 개로 만들었거나, 나중에 네트워크로 ID만 받아와도 판정이 맞도록).
    public bool HasKey(KeyData key)
    {
        return key != null && IndexOf(key.keyId) >= 0;
    }

    // 열쇠를 뺀다. 없던 열쇠면 false.
    public bool RemoveKey(KeyData key)
    {
        if (key == null) return false;
        int index = IndexOf(key.keyId);
        if (index < 0) return false;

        keys.RemoveAt(index);
        OnInventoryChanged?.Invoke();
        return true;
    }

    private int IndexOf(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return -1;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] != null && keys[i].keyId == keyId) return i;
        }
        return -1;
    }
}
