using LGH;
using Unity.Netcode;
using UnityEngine;

namespace AtticTreasure
{
    public enum StatueState : byte { InBox, Cutscene, Taken }

    // 다락 별 상자(StarLockBox_LGH) 속 황금상·현관 열쇠를 "누가 왼손에 들고 있는지" 담당한다. 판정은 호스트(혼자면 로컬)에서만.
    // 2026-10-28: 인벤토리에 넣던 방식 → 공용 들기 시스템(HoldableItem)으로 왼손에 드는 방식으로 변경 (인벤토리 시스템 제거 예정).
    // 흐름:
    //  - 황금상: 처음 집을 때 E → 호스트가 "연출 중"으로 바꾸고 두 사람 모두에게 연출 시작(Rpc Everyone)
    //           → 집은 사람 컴퓨터의 연출이 끝나면 호스트에게 알림 → 호스트가 "그 사람이 들고 있음"으로 확정하고 그 사람에게 쥐여 준다.
    //           한 번 연출을 본 뒤에는(상자에 돌려놨다가 다시 집을 때) 연출 없이 바로 쥐여 준다.
    //  - 현관 열쇠: E → 호스트가 "그 사람이 들고 있음"으로 확정하고 쥐여 준다.
    //  - 한 사람은 한 번에 하나만 든다(손이 하나). 황금상을 든 사람은 열쇠를, 열쇠를 든 사람은 황금상을 집을 수 없다
    //    → 정상 흐름은 한 명이 황금상, 한 명이 현관 열쇠.
    //  - 상자 자리에 "제자리에 두기"(E)를 하면 상자로 돌아간다. 들고 있던 사람이 나가면 상자로 돌아간다.
    //  - 현관문: 열쇠를 든 사람이 문을 보고 E → 호스트가 확인 후 잠금 해제 + 양문을 연다 (AtticTreasureFrontDoor가 탈출 성공으로 넘김).
    //  - 상태는 NetworkVariable → 늦게 들어온 사람도 같은 상태를 본다. 네트워크 없이 Play하면 같은 흐름을 로컬로 처리한다.
    //
    // 사용법: Assets/Prefabs/KJH/AtticTreasure/AtticTreasureSync.prefab을 씬 최상위에 프리팹 인스턴스로 두고
    //  DefaultNetworkPrefabs에 등록한다 (Tools/KJH/AtticTreasure/Build All이 해 준다).
    public class AtticTreasureSync : NetworkBehaviour
    {
        public static AtticTreasureSync Instance { get; private set; }
        const ulong Nobody = ulong.MaxValue;

        [Tooltip("(예전 인벤토리 방식에서 쓰던 아이템 정보 — 지금은 쓰지 않음, 프리팹 연결 유지용)")]
        [SerializeField] ItemData statueItem;
        [Tooltip("(예전 인벤토리 방식에서 쓰던 아이템 정보 — 지금은 쓰지 않음, 프리팹 연결 유지용)")]
        [SerializeField] ItemData keyItem;
        [Tooltip("획득 연출. 비우면 임시 연출(AtticTreasurePlaceholderCutscene)")]
        [SerializeField] AtticTreasureCutscene cutscene;
        [Tooltip("집은 사람의 연출 종료 신호를 기다리는 최대 시간(초). 넘으면 판정 담당이 강제로 쥐여 준다")]
        [SerializeField] float cutsceneTimeoutSeconds = 120f;
        [SerializeField] string oneAtATimeMessage = "한 번에 하나만 들 수 있다";
        [SerializeField] bool logToConsole = true;

        readonly NetworkVariable<byte> netState = new NetworkVariable<byte>((byte)StatueState.InBox);
        readonly NetworkVariable<ulong> netPicker = new NetworkVariable<ulong>(Nobody);      // 황금상: 연출 중인 사람 / 들고 있는 사람
        readonly NetworkVariable<ulong> netKeyHolder = new NetworkVariable<ulong>(Nobody);   // 현관 열쇠를 들고 있는 사람
        readonly NetworkVariable<bool> netCutsceneDone = new NetworkVariable<bool>(false);

        StatueState offlineState = StatueState.InBox;
        bool offlineKeyHeld, offlineCutsceneDone;
        float deadline;

        AtticTreasureStatue statueObj;
        AtticTreasureKey keyObj;
        AtticTreasureFrontDoor frontDoor;

        public bool Online { get { return IsSpawned && NetworkManager != null && NetworkManager.IsListening; } }
        public StatueState State { get { return Online ? (StatueState)netState.Value : offlineState; } }
        public bool KeyTaken { get { return Online ? netKeyHolder.Value != Nobody : offlineKeyHeld; } }
        // 이 컴퓨터에서 지금 연출이 재생 중인가
        public bool LocalCutscenePlaying { get; private set; }

        void Awake()
        {
            Instance = this;
            if (cutscene == null)
            {
                cutscene = GetComponent<AtticTreasureCutscene>();
                if (cutscene == null) cutscene = gameObject.AddComponent<AtticTreasurePlaceholderCutscene>();
            }
        }

        void Start()
        {
            statueObj = FindAnyObjectByType<AtticTreasureStatue>(FindObjectsInactive.Include);
            keyObj = FindAnyObjectByType<AtticTreasureKey>(FindObjectsInactive.Include);
            frontDoor = FindAnyObjectByType<AtticTreasureFrontDoor>(FindObjectsInactive.Include);
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ---------------- 이 컴퓨터 플레이어가 호출 ----------------

        public void RequestTake()
        {
            if (State != StatueState.InBox) return;
            if (Online) { TakeRpc(); return; }
            if (offlineKeyHeld) { PlayerHand.Say(oneAtATimeMessage); return; }
            if (!offlineCutsceneDone) { offlineState = StatueState.Cutscene; PlayCutscene(true); }
            else { offlineState = StatueState.Taken; GiveLocal(statueObj); }
        }

        public void RequestTakeKey()
        {
            if (KeyTaken) return;
            if (Online) { TakeKeyRpc(); return; }
            if (offlineState != StatueState.InBox) { PlayerHand.Say(oneAtATimeMessage); return; }
            offlineKeyHeld = true;
            GiveLocal(keyObj);
        }

        public void RequestReturnStatue()
        {
            if (Online) ReturnRpc(0);
            else if (offlineState == StatueState.Taken) offlineState = StatueState.InBox;
        }

        public void RequestReturnKey()
        {
            if (Online) ReturnRpc(1);
            else offlineKeyHeld = false;
        }

        // 현관 열쇠를 든 사람이 현관문을 보고 E
        public void RequestOpenFrontDoor(Vector3 userPosition)
        {
            if (frontDoor == null || frontDoor.Door == null) return;
            if (Online) { OpenFrontDoorRpc(userPosition); return; }
            if (!offlineKeyHeld) return;
            var d = frontDoor.Door;
            d.Unlock();
            if (!d.IsOpen) d.Interact(userPosition);
            PlayerHand.Say("현관 열쇠로 문을 열었다");
        }

        // ---------------- 판정 담당 ----------------

        [Rpc(SendTo.Server)]
        void TakeRpc(RpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            if ((StatueState)netState.Value != StatueState.InBox) return;
            if (netKeyHolder.Value == sender) { MessageRpc(oneAtATimeMessage, RpcTarget.Single(sender, RpcTargetUse.Temp)); return; }
            netPicker.Value = sender;
            if (!netCutsceneDone.Value)
            {
                netState.Value = (byte)StatueState.Cutscene;
                deadline = Time.time + cutsceneTimeoutSeconds;
                Log("황금상 집음: 클라이언트 " + sender + " → 연출 시작");
                CutsceneRpc(sender);
            }
            else
            {
                netState.Value = (byte)StatueState.Taken;
                Log("황금상 다시 집음: 클라이언트 " + sender);
                GiveRpc(0, RpcTarget.Single(sender, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.Server)]
        void TakeKeyRpc(RpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            if (netKeyHolder.Value != Nobody) return;
            if ((StatueState)netState.Value != StatueState.InBox && netPicker.Value == sender)
            {
                MessageRpc(oneAtATimeMessage, RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }
            netKeyHolder.Value = sender;
            Log("현관 열쇠 → 클라이언트 " + sender + " 왼손");
            GiveRpc(1, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        // which: 0 = 황금상, 1 = 현관 열쇠
        [Rpc(SendTo.Server)]
        void ReturnRpc(byte which, RpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            if (which == 0)
            {
                if ((StatueState)netState.Value != StatueState.Taken || netPicker.Value != sender) return;
                netState.Value = (byte)StatueState.InBox;
                netPicker.Value = Nobody;
                Log("황금상 상자로 돌아감");
            }
            else
            {
                if (netKeyHolder.Value != sender) return;
                netKeyHolder.Value = Nobody;
                Log("현관 열쇠 상자로 돌아감");
            }
        }

        [Rpc(SendTo.Everyone)]
        void CutsceneRpc(ulong picker)
        {
            PlayCutscene(NetworkManager != null && NetworkManager.LocalClientId == picker);
        }

        [Rpc(SendTo.Server)]
        void FinishRpc(RpcParams p = default)
        {
            if ((StatueState)netState.Value != StatueState.Cutscene || p.Receive.SenderClientId != netPicker.Value) return;
            ServerGive();
        }

        void ServerGive()
        {
            ulong picker = netPicker.Value;
            netState.Value = (byte)StatueState.Taken;
            netCutsceneDone.Value = true;
            Log("황금상 → 클라이언트 " + picker + " 왼손");
            GiveRpc(0, RpcTarget.Single(picker, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void GiveRpc(byte which, RpcParams p)
        {
            GiveLocal(which == 0 ? (AtticTreasureBoxItem)statueObj : keyObj);
        }

        [Rpc(SendTo.Server)]
        void OpenFrontDoorRpc(Vector3 userPosition, RpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            if (netKeyHolder.Value != sender || frontDoor == null || frontDoor.Door == null) return;
            var d = frontDoor.Door;
            var doors = DoorNetworkSync.Instance;
            d.Unlock();   // 판정 담당 쪽은 바로 풀어 둬야 아래 문 열기 요청이 "잠김"으로 막히지 않는다
            if (doors != null && doors.IsSpawned)
            {
                doors.SetLocked(d, false);
                if (!d.IsOpen) doors.RequestInteract(d, userPosition);
            }
            else if (!d.IsOpen) d.Interact(userPosition);
            MessageRpc("현관 열쇠로 문을 열었다", RpcTarget.Single(sender, RpcTargetUse.Temp));
            Log("현관문: 클라이언트 " + sender + "가 열쇠로 열음");
        }

        void Update()
        {
            if (Online && IsServer && (StatueState)netState.Value == StatueState.Cutscene && Time.time > deadline)
            {
                Log("연출 종료 신호가 오지 않아 강제로 쥐여 줌");
                ServerGive();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if ((StatueState)netState.Value != StatueState.InBox && netPicker.Value == clientId)
            {
                netState.Value = (byte)StatueState.InBox;
                netPicker.Value = Nobody;
                Log("황금상을 든(집던) 사람이 나가서 상자로 돌아감");
            }
            if (netKeyHolder.Value == clientId)
            {
                netKeyHolder.Value = Nobody;
                Log("현관 열쇠를 든 사람이 나가서 상자로 돌아감");
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void MessageRpc(string message, RpcParams p)
        {
            PlayerHand.Say(message);
        }

        // ---------------- 공통 ----------------

        void GiveLocal(AtticTreasureBoxItem item)
        {
            if (item != null) item.GiveToLocalHand();
        }

        void PlayCutscene(bool isPicker)
        {
            LocalCutscenePlaying = true;
            cutscene.Play(isPicker, () =>
            {
                LocalCutscenePlaying = false;
                if (!isPicker) return;
                if (Online) FinishRpc();
                else
                {
                    offlineCutsceneDone = true;
                    offlineState = StatueState.Taken;
                    GiveLocal(statueObj);
                }
            });
        }

        void Log(string msg) { if (logToConsole) Debug.Log("[AtticTreasure] " + msg, this); }
    }
}
