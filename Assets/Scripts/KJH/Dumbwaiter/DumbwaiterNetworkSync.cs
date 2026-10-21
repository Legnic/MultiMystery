using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace KJH
{
    // LGH 플레이어 시스템을 쓰므로, 이름이 겹치는 전역 PlayerInventory(KJH 열쇠용) 대신 LGH 것을 명시한다.
    // (using 별칭을 namespace 안에 두어야 전역 이름보다 우선한다.)
    using CandleNetworkSync = LGH.CandleNetworkSync;
    using PlayerInteractor = LGH.PlayerInteractor;
    using PlayerInventory = LGH.PlayerInventory;

    // 씬의 모든 Dumbwaiter(도르래 승강기)를 호스트가 관리하게 하는 동기화 스크립트.
    // CandleNetworkSync/DoorNetworkSync와 같은 방식: 플레이어는 "요청"만 보내고, 호스트가 검사·결정한 뒤 모두에게 결과를 알린다.
    // 나중에 접속한 플레이어는 전체 상태(층/화물)를 받는다.
    // 사용법: 이 스크립트가 붙은 네트워크 프리팹 Assets/Prefabs/KJH/Dumbwaiter/DumbwaiterSync.prefab을 씬에 하나 둔다.
    // (씬 관리가 꺼져 있어서 게스트는 씬에 직접 둔 NetworkObject를 지우므로, 반드시 DefaultNetworkPrefabs에 등록된 프리팹이어야 한다.)
    public class DumbwaiterNetworkSync : NetworkBehaviour
    {
        public static DumbwaiterNetworkSync Instance { get; private set; }

        private Dumbwaiter[] boxes = new Dumbwaiter[0];

        private static bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private void Awake()
        {
            Instance = this;
            boxes = FindObjectsByType<Dumbwaiter>(FindObjectsInactive.Include);
            Array.Sort(boxes, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) RequestFullStateRpc();
        }

        // ---------- 로컬 플레이어 쪽 진입점 ----------
        // 호스트가 대답하는 경우(네트워크 플레이) null, 혼자 플레이(네트워크 없음)면 바로 처리하고 문구를 돌려준다.

        public string RequestLoad(Dumbwaiter d, int floor, PlayerInventory inventory)
        {
            int i = Array.IndexOf(boxes, d);
            if (i < 0) return null;
            if (IsSpawned) { LoadRpc(i, floor); return null; }
            if (Online) return null;
            return DoLoad(i, floor, inventory, 0UL);
        }

        public string RequestTake(Dumbwaiter d, int floor, PlayerInventory inventory)
        {
            int i = Array.IndexOf(boxes, d);
            if (i < 0) return null;
            if (IsSpawned) { TakeRpc(i, floor); return null; }
            if (Online) return null;
            return DoTake(i, floor, inventory, 0UL);
        }

        public string RequestMove(Dumbwaiter d, int buttonFloor)
        {
            int i = Array.IndexOf(boxes, d);
            if (i < 0) return null;
            if (IsSpawned) { MoveRpc(i, buttonFloor); return null; }
            if (Online) return null;
            return DoMove(i, buttonFloor);
        }

        // ---------- 호스트(또는 혼자 플레이) 처리 ----------

        private string DoLoad(int i, int floor, PlayerInventory inv, ulong client)
        {
            Dumbwaiter d = boxes[i];
            if (!d.IsIdleAt(floor)) return null;
            if (d.IsFull) return "도르래가 가득 찼다";
            if (d.AcceptedCargo == Dumbwaiter.CargoKind.PotionLump) return LoadLump(i, d, client);
            if (inv == null || inv.Candles <= 0) return "담을 양초가 없다";

            float remaining = inv.TakeShortest();
            if (remaining < 0f) return "담을 양초가 없다";
            d.TryLoad(remaining);
            PublishCargo(i);
            RememberCarried(inv);
            return "양초를 도르래에 담았다";
        }

        private string DoTake(int i, int floor, PlayerInventory inv, ulong client)
        {
            Dumbwaiter d = boxes[i];
            if (!d.IsIdleAt(floor) || d.CargoCount == 0) return null;
            if (d.AcceptedCargo == Dumbwaiter.CargoKind.PotionLump) return TakeLump(i, d, client);
            if (inv == null) return null;

            List<float> taken = d.TakeAll();
            foreach (float left in taken) inv.AddCandle(left);
            PublishCargo(i);
            RememberCarried(inv);
            return "양초 " + taken.Count + "개를 꺼냈다 (가진 양초 " + inv.Candles + "개)";
        }

        // ---------- 약품 제조 퍼즐 덩이 (PotionCraft) ----------
        // 화물 값은 덩이 아이템 코드(float로 보관). 손에 든 아이템은 PotionCraftSync가 관리한다.

        private string LoadLump(int i, Dumbwaiter d, ulong client)
        {
            var pc = PotionCraft.PotionCraftSync.Instance;
            if (pc == null) return null;
            int held = pc.GetHeld(client);
            if (held == 0) return "실을 덩이가 없다";
            if (PotionCraft.Item.KindOf(held) != PotionCraft.ItemKind.Lump) return "도르래에는 덩이만 실을 수 있다";
            if (!d.TryLoad((float)held)) return null;
            pc.ServerSetHeld(client, 0);
            PublishCargo(i);
            return "덩이를 도르래에 실었다";
        }

        private string TakeLump(int i, Dumbwaiter d, ulong client)
        {
            var pc = PotionCraft.PotionCraftSync.Instance;
            if (pc == null) return null;
            if (pc.GetHeld(client) != 0) return "덩이를 꺼내려면 손을 비워야 한다";
            List<float> taken = d.TakeAll();
            if (taken.Count == 0) return null;
            pc.ServerSetHeld(client, (int)taken[0]);
            PublishCargo(i);
            return "도르래에서 덩이를 꺼냈다";
        }

        // 버튼이 있는 층에 케이지가 있으면 반대 층으로, 아니면 그 층으로 부른다. 호스트가 지금 층을 기준으로 정하므로 동시에 눌러도 꼬이지 않는다.
        private string DoMove(int i, int buttonFloor)
        {
            Dumbwaiter d = boxes[i];
            if (d.IsMoving) return null;
            int target = d.CurrentFloor != buttonFloor ? buttonFloor : (buttonFloor > 0 ? buttonFloor - 1 : buttonFloor + 1);
            if (!d.GoTo(target)) return null;
            if (IsSpawned) MoveStateRpc(i, target);
            return null;
        }

        // 플레이어가 도중에 나가도 양초를 잃지 않게 CandleNetworkSync의 "가진 양초" 기록을 갱신한다.
        private static void RememberCarried(PlayerInventory inv)
        {
            if (CandleNetworkSync.Instance != null && inv != null && inv.IsSpawned)
                CandleNetworkSync.Instance.Remember(inv, inv.OwnerClientId);
        }

        private void PublishCargo(int i)
        {
            if (IsSpawned) CargoStateRpc(i, boxes[i].Cargo.Count > 0 ? new List<float>(boxes[i].Cargo).ToArray() : new float[0]);
        }

        private static PlayerInventory InventoryOf(ulong client)
        {
            foreach (var inv in PlayerInventory.All)
                if (inv != null && inv.IsSpawned && inv.OwnerClientId == client) return inv;
            return null;
        }

        private void Reply(string msg, ulong client)
        {
            if (string.IsNullOrEmpty(msg)) return;
            MessageRpc(msg, RpcTarget.Single(client, RpcTargetUse.Temp));
        }

        // ---------- RPC ----------

        [Rpc(SendTo.Server)]
        private void LoadRpc(int index, int floor, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= boxes.Length) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            Reply(DoLoad(index, floor, InventoryOf(sender), sender), sender);
        }

        [Rpc(SendTo.Server)]
        private void TakeRpc(int index, int floor, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= boxes.Length) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            Reply(DoTake(index, floor, InventoryOf(sender), sender), sender);
        }

        [Rpc(SendTo.Server)]
        private void MoveRpc(int index, int buttonFloor, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= boxes.Length) return;
            DoMove(index, buttonFloor);
        }

        [Rpc(SendTo.NotServer)]
        private void MoveStateRpc(int index, int target)
        {
            if (index >= 0 && index < boxes.Length) boxes[index].GoTo(target);
        }

        [Rpc(SendTo.NotServer)]
        private void CargoStateRpc(int index, float[] cargo)
        {
            if (index >= 0 && index < boxes.Length) boxes[index].SetCargo(cargo);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void MessageRpc(string message, RpcParams rpcParams)
        {
            if (PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage(message);
        }

        [Rpc(SendTo.Server)]
        private void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            // 배열 안의 배열은 직렬화가 안 되므로 화물을 한 줄로 펴서 보낸다 (counts로 상자별 개수를 나눔).
            var floors = new int[boxes.Length];
            var counts = new int[boxes.Length];
            var flat = new List<float>();
            for (int i = 0; i < boxes.Length; i++)
            {
                // 이동 중이면 도착할 층으로 보낸다 (연출 없이 바로 놓이는 것이 가장 안전).
                floors[i] = boxes[i].IsMoving ? boxes[i].TargetFloor : boxes[i].CurrentFloor;
                counts[i] = boxes[i].CargoCount;
                flat.AddRange(boxes[i].Cargo);
            }
            FullStateRpc(floors, counts, flat.ToArray(), RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void FullStateRpc(int[] floors, int[] counts, float[] flat, RpcParams rpcParams)
        {
            int offset = 0;
            for (int i = 0; i < boxes.Length && i < floors.Length; i++)
            {
                boxes[i].SnapTo(floors[i]);
                var items = new List<float>();
                for (int k = 0; k < counts[i] && offset < flat.Length; k++) items.Add(flat[offset++]);
                boxes[i].SetCargo(items);
            }
        }
    }
}
