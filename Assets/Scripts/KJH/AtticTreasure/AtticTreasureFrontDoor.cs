using LGH;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace AtticTreasure
{
    // 현관 양문(Door_Front_L / Door_Front_R)을 "현관 열쇠"로 열 수 있게 연결하고, 열리면 탈출 성공으로 넘긴다.
    //  - LGH 맵 프리팹을 고치지 않도록 문을 "이름으로 찾아" 시작할 때 keyItem만 런타임에 넣는다.
    //    열쇠 확인·잠금 해제·문 동기화는 LGH의 InteractableDoor / DoorNetworkSync가 원래 하던 방식 그대로 한다
    //    (열쇠를 가진 사람이 E → "[E] 현관 열쇠(으)로 열기" → 호스트가 잠금 해제 후 양문을 연다).
    //  - 판정 담당(호스트, 혼자면 로컬)이 "잠겨 있던 문이 풀리고 열렸다"를 감지하면 한 번:
    //    onFrontDoorOpened 실행 + (설정 시) KSM GameFlowSync.RequestEscapeSuccess() → 엔딩 (2026-10-23 결정: 열면 탈출 성공).
    //    GameFlowSync가 없는 씬(KJH 테스트 씬)이면 로그만 남긴다. GameFlowSync는 Playing 단계일 때만 받아들인다.
    public class AtticTreasureFrontDoor : MonoBehaviour
    {
        [Tooltip("현관문 이름. 양문이면 한쪽만 적어도 짝(partner)까지 같이 적용된다")]
        [SerializeField] string doorName = "Door_Front_L";
        [Tooltip("문 이름이 여러 개일 때, 부모 경로에 이 글자가 포함된 것만 고른다 (비우면 이름만으로)")]
        [SerializeField] string doorParentFilter = "Ravenwood_LGH";
        [Tooltip("이 문을 여는 열쇠 아이템")]
        [SerializeField] ItemData keyItem;
        [Tooltip("열쇠를 쓰면 인벤토리에서 없앤다")]
        [SerializeField] bool consumeKey = false;
        [Tooltip("문이 열리면 GameFlowSync의 탈출 성공(엔딩)을 요청한다")]
        [SerializeField] bool triggerEscape = true;
        [SerializeField] bool logToConsole = true;

        [Tooltip("현관문이 열쇠로 열렸을 때 판정 담당에서 한 번 실행")]
        public UnityEvent onFrontDoorOpened;

        InteractableDoor door;
        bool armed;   // 잠긴 상태를 한 번이라도 봤다 → 그 뒤 풀리고 열리면 "열쇠로 열었다"
        bool fired;

        void Start()
        {
            foreach (var d in FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include))
            {
                if (d.name != doorName) continue;
                if (!string.IsNullOrEmpty(doorParentFilter) && !HasParentNamed(d.transform, doorParentFilter)) continue;
                door = d;
                break;
            }
            if (door == null) { Debug.LogWarning("[AtticTreasure] 현관문 '" + doorName + "'을(를) 찾지 못했습니다.", this); return; }
            Apply(door);
            if (door.partner != null) Apply(door.partner);
            if (!door.locked) door.Lock();   // 원래 잠겨 있는 문이지만 혹시 풀려 있으면 잠근다 (양문 같이)
        }

        void Apply(InteractableDoor d)
        {
            d.keyItem = keyItem;
            d.consumeKey = consumeKey;
        }

        static bool HasParentNamed(Transform t, string part)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.Contains(part)) return true;
            return false;
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
            if (door == null || fired) return;
            if (door.locked) { armed = true; return; }
            if (!armed || !door.IsOpen || !IsAuthority) return;
            fired = true;
            if (logToConsole) Debug.Log("[AtticTreasure] 현관문이 열쇠로 열림", this);
            onFrontDoorOpened?.Invoke();
            if (!triggerEscape) return;
            var flow = CoopDemo.GameFlowSync.Instance;
            if (flow != null) flow.RequestEscapeSuccess();
            else if (logToConsole) Debug.Log("[AtticTreasure] 이 씬에 GameFlowSync가 없어 탈출 성공 요청은 건너뜀 (Develop 씬에서는 엔딩으로 넘어감)", this);
        }
    }
}
