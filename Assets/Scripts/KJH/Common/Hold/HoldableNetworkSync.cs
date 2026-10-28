using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// 공용 들기 시스템(HoldableItem)은 원래 "손에 든 물건은 네트워크로 동기화하지 않는다"(상대가 볼 수 없음)는
// 설계였다. 이 컴포넌트는 그 설계를 바꿔서, "지금 어떤 물건을 어느 클라이언트가 들고 있는지"만 가볍게
// 동기화하고, 다른 클라이언트들이 그 정보를 보고 자기 쪽 로컬 복사본을 상대 플레이어의 손 위치로 옮겨
// 붙이는 방식으로 "상대가 실제로 손에 들고 있는 모습"을 보여준다 (2026-10-26).
//
// 집기/옮기기 판정 자체는 그대로 각자 로컬(PlayerHand/HoldableItem)이 하고, 최종 배치(PlaceAt)는 각
// 기믹의 기존 RPC가 그대로 담당한다 — 이 컴포넌트는 "누가 들고 있는지 표시 + 상대 손에 보여주기"만 한다.
// 씬에는 하나만 두면 된다(프리팹 인스턴스를 씬 루트에).
public class HoldableNetworkSync : NetworkBehaviour
{
    public static HoldableNetworkSync Instance { get; private set; }

    [Tooltip("CharacterAvatar/왼손 소켓을 못 찾았을 때만 쓰는 대체 위치(상대 플레이어 오브젝트 기준 로컬 좌표)")]
    [SerializeField] Vector3 fallbackHoldOffset = new Vector3(-0.25f, 1.1f, 0.5f);
    [SerializeField] Vector3 fallbackHoldEuler = Vector3.zero;

    struct HeldEntry : INetworkSerializeByMemcpy, System.IEquatable<HeldEntry>
    {
        public int itemIndex;
        public ulong clientId;
        public HeldEntry(int itemIndex, ulong clientId) { this.itemIndex = itemIndex; this.clientId = clientId; }
        public bool Equals(HeldEntry o) { return itemIndex == o.itemIndex && clientId == o.clientId; }
    }

    NetworkList<HeldEntry> held;
    HoldableItem[] items = new HoldableItem[0];
    // 지금 이 컴퓨터가 "상대 손"으로 보이게 옮겨둔 아이템들 (itemIndex 기준), 그리고 어느 모델의 왼손에 붙였는지
    readonly HashSet<int> attached = new HashSet<int>();
    readonly Dictionary<int, CoopDemo.CharacterAvatarModel> attachedModel = new Dictionary<int, CoopDemo.CharacterAvatarModel>();

    ulong MyId { get { return NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0; } }

    void Awake()
    {
        Instance = this;
        held = new NetworkList<HeldEntry>(new List<HeldEntry>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        BuildIndex();
        HoldableItem.HeldByOther = IsHeldByOther;
    }

    void OnEnable()
    {
        PlayerHand.LocalTaken += OnLocalTaken;
        PlayerHand.LocalReleased += OnLocalReleased;
    }

    void OnDisable()
    {
        PlayerHand.LocalTaken -= OnLocalTaken;
        PlayerHand.LocalReleased -= OnLocalReleased;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        HoldableItem.HeldByOther = null;
        base.OnDestroy();
    }

    // 씬의 모든 HoldableItem에 계층 경로 기준으로 고정된 인덱스를 매긴다 (모든 클라이언트가 같은 씬이라 순서가 같다)
    void BuildIndex()
    {
        var all = FindObjectsByType<HoldableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        System.Array.Sort(all, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
        items = all;
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    int IndexOf(HoldableItem item)
    {
        for (int i = 0; i < items.Length; i++) if (items[i] == item) return i;
        return -1;
    }

    void OnLocalTaken(HoldableItem item)
    {
        int i = IndexOf(item);
        if (i >= 0) RequestSetHeldRpc(i, MyId);
    }

    void OnLocalReleased(HoldableItem item)
    {
        int i = IndexOf(item);
        if (i >= 0) RequestClearHeldRpc(i);
    }

    [Rpc(SendTo.Server)]
    void RequestSetHeldRpc(int itemIndex, ulong clientId)
    {
        for (int i = held.Count - 1; i >= 0; i--)
            if (held[i].itemIndex == itemIndex || held[i].clientId == clientId) held.RemoveAt(i);
        held.Add(new HeldEntry(itemIndex, clientId));
    }

    [Rpc(SendTo.Server)]
    void RequestClearHeldRpc(int itemIndex)
    {
        for (int i = held.Count - 1; i >= 0; i--)
            if (held[i].itemIndex == itemIndex) held.RemoveAt(i);
    }

    bool IsHeldByOther(HoldableItem item)
    {
        if (!IsSpawned) return false;
        int i = IndexOf(item);
        if (i < 0) return false;
        ulong myId = MyId;
        foreach (var e in held) if (e.itemIndex == i && e.clientId != myId) return true;
        return false;
    }

    public override void OnNetworkSpawn()
    {
        held.OnListChanged += OnHeldChanged;
        if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        Resync();
    }

    public override void OnNetworkDespawn()
    {
        held.OnListChanged -= OnHeldChanged;
        if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // 들고 있던 사람이 나가면 그 사람 항목을 지운다 → 남은 사람 화면에서 물건이 원래 자리로 돌아간다 (Release → ForceReturnHome)
    void OnClientDisconnected(ulong clientId)
    {
        for (int i = held.Count - 1; i >= 0; i--)
            if (held[i].clientId == clientId) held.RemoveAt(i);
    }

    void OnHeldChanged(NetworkListEvent<HeldEntry> e) { Resync(); }

    // 매번 전체를 다시 맞춘다 (목록이 2명짜리라 가벼움) — 이벤트 종류별로 분기하는 것보다 훨씬 덜 깨진다.
    void Resync()
    {
        var current = new HashSet<int>();
        foreach (var e in held)
        {
            current.Add(e.itemIndex);
            if (e.clientId != MyId) Apply(e.itemIndex, e.clientId);
        }
        List<int> toRelease = null;
        foreach (var idx in attached)
            if (!current.Contains(idx)) { toRelease ??= new List<int>(); toRelease.Add(idx); }
        if (toRelease != null) foreach (var idx in toRelease) Release(idx);
    }

    void Apply(int itemIndex, ulong clientId)
    {
        if (itemIndex < 0 || itemIndex >= items.Length || items[itemIndex] == null) return;
        var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerObj == null) return;
        var t = items[itemIndex].transform;
        var avatar = playerObj.GetComponentInChildren<CoopDemo.CharacterAvatar>(true);
        // 아바타 모델의 왼손 뼈에 붙인다(걷기 팔 흔들림을 그대로 따라가서 손이 움직이는 게 보인다).
        // 모델이 아직 안 만들어졌으면(스폰 직후 한두 프레임) 대체 위치로 플레이어 루트에 임시로 붙여 둔다.
        if (avatar != null && avatar.Model != null)
        {
            avatar.Model.AttachLeftHandItem(t);
            attachedModel[itemIndex] = avatar.Model;
        }
        else
        {
            if (t.parent != playerObj.transform) t.SetParent(playerObj.transform, false);
            t.localPosition = fallbackHoldOffset;
            t.localRotation = Quaternion.Euler(fallbackHoldEuler);
        }
        attached.Add(itemIndex);
    }

    void Release(int itemIndex)
    {
        attached.Remove(itemIndex);
        if (attachedModel.TryGetValue(itemIndex, out var model))
        {
            attachedModel.Remove(itemIndex);
            if (model != null) model.DetachLeftHandItem();   // 왼팔을 드는 자세에서 평소 걷기 자세로 되돌린다
        }
        if (itemIndex < 0 || itemIndex >= items.Length || items[itemIndex] == null) return;
        var item = items[itemIndex];
        // 이미 이 컴퓨터에서 직접 들었거나(PlayerHand), 기믹 로직이 자리에 꽂아 놓은 상태면 건드리지 않는다.
        if (item.Location == HoldableItem.Where.Held || item.Location == HoldableItem.Where.Placed) return;
        item.ForceReturnHome();
    }
}
