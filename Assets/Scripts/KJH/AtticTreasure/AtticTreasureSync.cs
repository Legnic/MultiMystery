using LGH;
using Unity.Netcode;
using UnityEngine;

namespace AtticTreasure
{
    public enum StatueState : byte { InBox, Cutscene, Taken }

    // 다락 별 상자(StarLockBox_LGH) 속 황금상·현관 열쇠의 상태를 담당한다. 판정은 호스트(혼자면 로컬)에서만.
    // 흐름: 누군가 황금상에 E → 호스트가 "연출 중"으로 바꾸고 두 사람 모두에게 연출 시작을 알림(Rpc Everyone)
    //       → 집은 사람 컴퓨터의 연출이 끝나면 호스트에게 알림 → 호스트가 그 사람 인벤토리(LGH.PlayerInventory)에 황금상을 넣고 "획득됨".
    //  - 상태는 NetworkVariable → 늦게 들어온 사람도 황금상이 있는지/없는지 그대로 본다.
    //  - 연출 도중 집은 사람이 나가면 황금상은 상자로 돌아간다. 연출이 끝났다는 신호가 너무 오래 안 오면 강제로 넣는다.
    //  - 인벤토리가 가득 차 있으면 황금상은 상자에 남는다.
    //  - 현관 열쇠는 연출 없이 바로: E → 호스트가 그 사람 인벤토리에 넣고 "주움"으로 표시.
    //    (LGH WorldItem 줍기를 쓰지 않는 이유: 그 줍기를 처리하는 ItemNetworkSync가 LGH 씬에만 있고
    //     DoorSync_LGH 프리팹에는 빠져 있어서, KJH·Develop 씬에서는 WorldItem을 주울 수 없다. 2026-10-23 확인)
    //  - 네트워크 없이 Play하면 같은 흐름을 로컬로 처리한다 (혼자 테스트).
    //
    // 사용법: Assets/Prefabs/KJH/AtticTreasure/AtticTreasureSync.prefab을 씬 최상위에 프리팹 인스턴스로 두고
    //  DefaultNetworkPrefabs에 등록한다 (Tools/KJH/AtticTreasure/Build All이 해 준다).
    public class AtticTreasureSync : NetworkBehaviour
    {
        public static AtticTreasureSync Instance { get; private set; }

        [Tooltip("인벤토리에 넣을 황금상 아이템 (ItemDatabase_LGH 목록에 있어야 한다)")]
        [SerializeField] ItemData statueItem;
        [Tooltip("인벤토리에 넣을 현관 열쇠 아이템 (ItemDatabase_LGH 목록에 있어야 한다)")]
        [SerializeField] ItemData keyItem;
        [Tooltip("획득 연출. 비우면 임시 연출(AtticTreasurePlaceholderCutscene)")]
        [SerializeField] AtticTreasureCutscene cutscene;
        [Tooltip("집은 사람의 연출 종료 신호를 기다리는 최대 시간(초). 넘으면 판정 담당이 강제로 인벤토리에 넣는다")]
        [SerializeField] float cutsceneTimeoutSeconds = 120f;
        [SerializeField] bool logToConsole = true;

        readonly NetworkVariable<byte> netState = new NetworkVariable<byte>((byte)StatueState.InBox);
        readonly NetworkVariable<ulong> netPicker = new NetworkVariable<ulong>(ulong.MaxValue);
        readonly NetworkVariable<bool> netKeyTaken = new NetworkVariable<bool>(false);

        StatueState offlineState = StatueState.InBox;
        bool offlineKeyTaken;
        float deadline;

        public bool Online { get { return IsSpawned && NetworkManager != null && NetworkManager.IsListening; } }
        public StatueState State { get { return Online ? (StatueState)netState.Value : offlineState; } }
        public ItemData StatueItem { get { return statueItem; } }
        public ItemData KeyItem { get { return keyItem; } }
        public bool KeyTaken { get { return Online ? netKeyTaken.Value : offlineKeyTaken; } }
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
            if (Online) TakeRpc();
            else OfflineTake();
        }

        public void RequestTakeKey()
        {
            if (KeyTaken) return;
            if (Online) { TakeKeyRpc(); return; }
            var inv = LGH.PlayerInventory.Local;
            if (inv == null) return;
            if (inv.AddItem(keyItem)) offlineKeyTaken = true;
            else PlayerHand.Say("더 이상 들 수 없다");
        }

        // ---------------- 판정 담당 ----------------

        [Rpc(SendTo.Server)]
        void TakeKeyRpc(RpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            if (netKeyTaken.Value) return;
            var inv = InventoryOf(sender);
            if (inv == null) return;
            if (!inv.AddItem(keyItem)) { MessageRpc("더 이상 들 수 없다", RpcTarget.Single(sender, RpcTargetUse.Temp)); return; }
            netKeyTaken.Value = true;
            Log("현관 열쇠 → 클라이언트 " + sender + " 인벤토리");
        }

        [Rpc(SendTo.Server)]
        void TakeRpc(RpcParams p = default)
        {
            ulong sender = p.Receive.SenderClientId;
            if ((StatueState)netState.Value != StatueState.InBox || InventoryOf(sender) == null) return;
            netState.Value = (byte)StatueState.Cutscene;
            netPicker.Value = sender;
            deadline = Time.time + cutsceneTimeoutSeconds;
            Log("황금상 집음: 클라이언트 " + sender + " → 연출 시작");
            CutsceneRpc(sender);
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
            var inv = InventoryOf(picker);
            if (inv != null && inv.AddItem(statueItem))
            {
                netState.Value = (byte)StatueState.Taken;
                Log("황금상 → 클라이언트 " + picker + " 인벤토리");
            }
            else
            {
                netState.Value = (byte)StatueState.InBox;
                netPicker.Value = ulong.MaxValue;
                MessageRpc("더 이상 들 수 없다", RpcTarget.Single(picker, RpcTargetUse.Temp));
                Log("황금상: 인벤토리에 넣지 못해 상자로 돌아감");
            }
        }

        void Update()
        {
            if (Online && IsServer && (StatueState)netState.Value == StatueState.Cutscene && Time.time > deadline)
            {
                Log("연출 종료 신호가 오지 않아 강제로 인벤토리에 넣음");
                ServerGive();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if ((StatueState)netState.Value == StatueState.Cutscene && netPicker.Value == clientId)
            {
                netState.Value = (byte)StatueState.InBox;
                netPicker.Value = ulong.MaxValue;
                Log("연출 도중 집은 사람이 나가서 황금상이 상자로 돌아감");
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void MessageRpc(string message, RpcParams p)
        {
            PlayerHand.Say(message);
        }

        // ---------------- 네트워크 없이 (혼자 테스트) ----------------

        void OfflineTake()
        {
            if (LGH.PlayerInventory.Local == null) return;
            offlineState = StatueState.Cutscene;
            PlayCutscene(true);
        }

        // ---------------- 공통 ----------------

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
                    var inv = LGH.PlayerInventory.Local;
                    bool ok = inv != null && inv.AddItem(statueItem);
                    offlineState = ok ? StatueState.Taken : StatueState.InBox;
                    if (!ok) PlayerHand.Say("더 이상 들 수 없다");
                }
            });
        }

        static LGH.PlayerInventory InventoryOf(ulong client)
        {
            foreach (var inv in LGH.PlayerInventory.All)
                if (inv != null && inv.IsSpawned && inv.OwnerClientId == client) return inv;
            return null;
        }

        void Log(string msg) { if (logToConsole) Debug.Log("[AtticTreasure] " + msg, this); }
    }
}
