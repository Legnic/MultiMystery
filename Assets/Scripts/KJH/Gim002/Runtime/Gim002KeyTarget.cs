using LGH;
using Unity.Netcode;
using UnityEngine;

namespace Gim002
{
    // 열쇠 사용처. 두 종류:
    //  - MusicDoor   : 1층 음악실 문 (높은음자리표 명판). 열쇠를 쓰면 문이 풀리면서 열리고 → "음악실 문 열림"(2층 '반주자' 표시등 ON)
    //  - StageWindup : 2층 무대 측면 태엽 구멍 (나비 테두리). 열쇠를 쓰면 → "무대 작동"(무대 조명, 1층 '무대' 표시등 ON)
    // 열쇠를 들고 바라보며 E → 문양이 맞으면 열쇠가 사라지고 판정 담당에게 알린다. 맞지 않으면 "문양이 맞지 않는다".
    //
    // 음악실 문 사용법: LGH 문(InteractableDoor)이 있는 오브젝트에 같이 붙인다.
    //  열쇠를 들고 있지 않을 때는 CanInteract가 false라서 LGH 문 처리(잠김 문구)가 그대로 동작한다.
    //  잠금 상태는 동기화 상태(musicDoorOpened)를 보고 모든 컴퓨터에서 같게 맞춘다.
    public class Gim002KeyTarget : MonoBehaviour, IWorldInteractable
    {
        public enum TargetKind { MusicDoor, StageWindup }

        [SerializeField] TargetKind kind;
        [Tooltip("여기에 맞는 열쇠 (같은 에셋을 가진 열쇠만 사용 가능)")]
        [SerializeField] Gim002KeyData requiredKey;
        [Tooltip("MusicDoor일 때 잠그고 열 문 (비우면 같은 오브젝트의 InteractableDoor)")]
        [SerializeField] InteractableDoor door;
        [Tooltip("문이 잠겨 있을 때 표시할 문구 (명판 문양 힌트)")]
        [SerializeField] string lockedHint = "잠겨 있다. 손잡이 위 명판에 높은음자리표가 새겨져 있다";
        [Tooltip("태엽 구멍을 빈손으로 볼 때 표시할 문구")]
        [SerializeField] string idlePrompt = "태엽 구멍 (테두리에 나비 문양)";
        [Tooltip("열쇠를 사용할 때 소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip useClip;

        bool waitingToOpen;   // 내가 방금 열쇠를 써서, 문이 풀리면 바로 열어 줘야 함

        static Gim002Controller C { get { return Gim002Controller.Instance; } }

        // 이 사용처에 맞는 열쇠의 인벤토리 아이템. requiredKey(Gim002KeyData) 하나에만 연결해 두면
        // Gim002KeyItem(WorldItem)도 같은 참조를 쓰므로 어디서도 따로 맞출 필요가 없다.
        ItemData RequiredItem { get { return requiredKey != null ? requiredKey.inventoryItem : null; } }

        bool HasRequiredKey
        {
            get
            {
                var item = RequiredItem;
                // 주의: KJH.KeySafe에도 네임스페이스 없는 PlayerInventory가 있어서 이름이 겹친다. 반드시 LGH.로 명시한다.
                return item != null && LGH.PlayerInventory.Local != null && LGH.PlayerInventory.Local.HasItem(item);
            }
        }

        bool Done
        {
            get
            {
                if (C == null) return false;
                return kind == TargetKind.MusicDoor ? C.State.musicDoorOpened : C.State.stageWound;
            }
        }

        void Awake()
        {
            if (kind == TargetKind.MusicDoor && door == null) door = GetComponent<InteractableDoor>();
        }

        void Start()
        {
            if (kind == TargetKind.MusicDoor && door != null)
            {
                door.lockedMessage = lockedHint;
                ApplyDoorLock();
            }
            if (C != null) C.StateChanged += OnState;
        }

        void OnDestroy()
        {
            if (C != null) C.StateChanged -= OnState;
        }

        // 방 초기화(RoomResetter.ResetAll, 새 방을 만들 때 호출됨)가 모든 문을 "에디터에 적힌 기본값"으로
        // 되돌리는데, 이 문의 진짜 잠금 여부는 그 기본값이 아니라 Gim002 상태라서 그때 풀려버릴 수 있다.
        // GimmickDoorLink 등 다른 기믹 문들도 같은 이유로 매 프레임 다시 맞추므로 여기도 그렇게 한다.
        void Update()
        {
            if (kind == TargetKind.MusicDoor && door != null) ApplyDoorLock();
        }

        void OnState(Gim002SyncState prev, Gim002SyncState now)
        {
            if (kind != TargetKind.MusicDoor || door == null) return;
            ApplyDoorLock();
            // 내가 열쇠를 써서 판정 담당이 "문 열림"을 확정했으면 문을 실제로 연다
            if (waitingToOpen && now.musicDoorOpened)
            {
                waitingToOpen = false;
                OpenDoor();
            }
        }

        // 음악실 문 잠금 = 아직 열쇠를 안 썼음
        void ApplyDoorLock()
        {
            if (Done) door.Unlock(); else door.Lock();
        }

        void OpenDoor()
        {
            Transform player = LGH.PlayerInteractor.Active != null ? LGH.PlayerInteractor.Active.transform : transform;
            var sync = DoorNetworkSync.Instance;
            // 네트워크 게임이면 호스트에게 문 열기 요청 (모두에게 같은 각도로 열림), 아니면 직접 연다
            if (sync != null && sync.IsSpawned) sync.RequestInteract(door, player.position);
            else if (!door.IsOpen) door.Interact(player.position);
        }

        // ---------------- IWorldInteractable ----------------

        public bool CanInteract
        {
            get
            {
                if (C == null || Done) return false;
                if (kind == TargetKind.MusicDoor) return HasRequiredKey;   // 안 가지고 있으면 LGH 문 처리에 양보
                return C.State.state >= Gim002State.Preparing || HasRequiredKey;
            }
        }

        public string InteractPrompt
        {
            get
            {
                if (!HasRequiredKey) return idlePrompt;
                var item = RequiredItem;
                return "[E] " + (item != null ? item.NameOrFallback : requiredKey.displayName) + " 사용";
            }
        }

        public void Interact(GameObject interactor)
        {
            if (C == null || Done || !HasRequiredKey) return;

            // 인벤토리에서 열쇠 1개 소비 + 판정 담당에게 알림
            AudioSource.PlayClipAtPoint(useClip != null ? useClip : Gim002Tones.Wind(), transform.position, 0.8f);
            var sync = ItemNetworkSync.Instance;
            if (sync != null) sync.RequestConsume(LGH.PlayerInventory.Local, RequiredItem);
            if (kind == TargetKind.MusicDoor)
            {
                waitingToOpen = true;
                C.Net.RequestMusicDoorOpened();
            }
            else
            {
                C.Net.RequestStageWound();
            }
        }
    }
}
