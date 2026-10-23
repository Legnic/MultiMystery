using System;
using System.Collections.Generic;
using LGH;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace GimmickDoors
{
    // 어느 기믹이 끝나면 열리는가
    public enum DoorGimmick
    {
        Gim002_PuppetShow,     // GIM-002 인형극 (Gim002Controller 상태가 Complete)
        Gim003_PotionCraft,    // GIM-003 약품 제조 (PotionCraftSync.Completed)
    }

    [Serializable]
    public class LinkedDoor
    {
        [Tooltip("LGH 문(InteractableDoor) 오브젝트 이름. 양문이면 한쪽만 적어도 짝(partner)까지 같이 잠기고 열린다")]
        public string doorName;
        [Tooltip("문을 열 때 '이 위치에서 연 것처럼' 문짝이 반대쪽으로 열린다 (월드 좌표, 보통 복도 쪽 → 방 안으로 열림)")]
        public Vector3 openFrom;
    }

    // 기믹이 끝날 때까지 문을 잠가 두고, 끝나면 연다 (2026-10-23 요청: GIM-002 → 주방·객실2 문, GIM-003 → 서재·온실 문).
    //  - LGH 맵 프리팹(Ravenwood_LGH)을 고치지 않도록 문을 "이름으로" 찾는다 → KJH·Develop 둘 다 같은 이름이라 그대로 동작.
    //  - 잠금은 상태 기반: 모든 컴퓨터가 매 프레임 "기믹 완료 여부"를 보고 잠금을 맞춘다
    //    → 늦게 들어온 사람, 방 초기화(RoomResetter) 뒤에도 같게 적용된다. 완료 후에는 잠금을 풀기만 하고 자유롭게 여닫을 수 있다.
    //  - 문을 실제로 여는 건 "이번 실행 중에 완료되는 순간을 본" 판정 담당(호스트, 혼자면 로컬) 한 곳에서 한 번:
    //    네트워크 게임이면 LGH 문 동기화(DoorNetworkSync)에 요청 → 모두에게 같은 각도로 열린다.
    //    (늦게 들어온 사람은 DoorNetworkSync가 보내 주는 현재 문 상태를 그대로 받는다)
    //  - 씬에 해당 기믹이 없으면(아직 Develop에 안 들어옴 등) 기본적으로 잠그지 않는다 → 영원히 못 여는 문이 생기지 않게.
    //  - 완료 문구는 각 기믹 쪽(Gim002Config.msgComplete, PotionCraftSync.completionSubtitle)이 띄운다. 여기서는 띄우지 않는다.
    public class GimmickDoorLink : MonoBehaviour
    {
        [SerializeField] DoorGimmick gimmick;
        [SerializeField] LinkedDoor[] doors = new LinkedDoor[0];
        [Tooltip("문 이름이 여러 개일 때, 부모 경로에 이 글자가 포함된 것만 고른다 (비우면 이름만으로)")]
        [SerializeField] string doorParentFilter = "Ravenwood_LGH";
        [Tooltip("잠겨 있을 때 문을 열려고 하면 뜨는 문구")]
        [SerializeField] string lockedMessage = "문이 잠겨 있다";
        [Tooltip("완료되는 순간 문을 자동으로 연다 (끄면 잠금만 풀린다)")]
        [SerializeField] bool openOnComplete = true;
        [Tooltip("씬에 해당 기믹이 없을 때도 문을 잠근다 (기본 꺼짐: 기믹이 없는 씬에서 문이 영원히 잠기지 않게)")]
        [SerializeField] bool lockWhenGimmickMissing = false;
        [SerializeField] bool logToConsole = true;

        [Tooltip("문이 열릴 때(완료 순간) 판정 담당에서 한 번 실행")]
        public UnityEvent onOpenedAuthority;

        struct Binding { public InteractableDoor door; public LinkedDoor target; }
        readonly List<Binding> bound = new List<Binding>();

        bool sawIncomplete;   // 이번 실행 중에 "아직 안 끝남"을 본 적이 있다 → 그 뒤 완료되면 "완료되는 순간"
        bool opened;
        bool warnedMissing;

        public DoorGimmick Gimmick { get { return gimmick; } }

        void Start()
        {
            var all = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include);
            foreach (var t in doors)
            {
                if (t == null || string.IsNullOrEmpty(t.doorName)) continue;
                InteractableDoor found = null;
                foreach (var d in all)
                {
                    if (d.name != t.doorName) continue;
                    if (!string.IsNullOrEmpty(doorParentFilter) && !HasParentNamed(d.transform, doorParentFilter)) continue;
                    found = d;
                    break;
                }
                if (found == null) { Debug.LogWarning("[GimmickDoors] 문 '" + t.doorName + "'을(를) 씬에서 찾지 못했습니다.", this); continue; }
                bound.Add(new Binding { door = found, target = t });
            }
        }

        static bool HasParentNamed(Transform t, string part)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.Contains(part)) return true;
            return false;
        }

        // 기믹 완료 여부. 기믹이 씬에 없으면 null
        bool? IsComplete()
        {
            switch (gimmick)
            {
                case DoorGimmick.Gim002_PuppetShow:
                    var g2 = Gim002.Gim002Controller.Instance;
                    return g2 == null ? (bool?)null : g2.State.state == Gim002.Gim002State.Complete;
                case DoorGimmick.Gim003_PotionCraft:
                    var g3 = PotionCraft.PotionCraftSync.Instance;
                    return g3 == null ? (bool?)null : g3.Completed;
            }
            return null;
        }

        static bool IsAuthority
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm == null || !nm.IsListening || nm.IsServer;
            }
        }

        void Update()
        {
            if (bound.Count == 0) return;
            bool? state = IsComplete();
            if (state == null)
            {
                if (!warnedMissing && logToConsole)
                {
                    warnedMissing = true;
                    Debug.Log("[GimmickDoors] " + gimmick + "이(가) 씬에 없어 " + (lockWhenGimmickMissing ? "문을 잠근 채로 둡니다." : "문을 잠그지 않습니다."), this);
                }
                foreach (var b in bound) SetLocked(b.door, lockWhenGimmickMissing);
                return;
            }

            if (!state.Value)
            {
                sawIncomplete = true;
                foreach (var b in bound) SetLocked(b.door, true);
                return;
            }

            foreach (var b in bound) SetLocked(b.door, false);
            if (opened || !sawIncomplete) return;
            opened = true;
            if (!IsAuthority) return;
            if (logToConsole) Debug.Log("[GimmickDoors] " + gimmick + " 완료 → 문 열기", this);
            if (openOnComplete) foreach (var b in bound) Open(b);
            onOpenedAuthority?.Invoke();
        }

        void SetLocked(InteractableDoor d, bool locked)
        {
            if (d == null) return;
            if (locked)
            {
                d.lockedMessage = lockedMessage;
                if (d.partner != null) d.partner.lockedMessage = lockedMessage;
                if (!d.locked || (d.partner != null && !d.partner.locked)) d.Lock();
            }
            else if (d.locked || (d.partner != null && d.partner.locked)) d.Unlock();
        }

        static void Open(Binding b)
        {
            var d = b.door;
            if (d == null || d.IsOpen) return;
            var sync = DoorNetworkSync.Instance;
            if (sync != null && sync.IsSpawned) sync.RequestInteract(d, b.target.openFrom);   // 양문 짝까지 같이 열린다
            else
            {
                d.SetOpen(true, b.target.openFrom);
                if (d.partner != null) d.partner.SetOpen(true, b.target.openFrom);
            }
        }
    }
}
