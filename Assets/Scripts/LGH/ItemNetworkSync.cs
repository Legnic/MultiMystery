using System;
using Unity.Netcode;
using UnityEngine;

namespace LGH
{
    // One per scene (next to DoorNetworkSync, on the object with the NetworkObject).
    // The host owns every WorldItem: a player asks to pick up / drop, the host checks and
    // tells everyone. Players who join later get the current state.
    // Without a running network game everything happens locally (single player test).
    public class ItemNetworkSync : NetworkBehaviour
    {
        public static ItemNetworkSync Instance { get; private set; }

        WorldItem[] items = new WorldItem[0];

        void Awake()
        {
            Instance = this;
            items = FindObjectsByType<WorldItem>(FindObjectsInactive.Include);
            Array.Sort(items, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        bool Online { get { return IsSpawned && NetworkManager != null && NetworkManager.IsListening; } }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) RequestFullStateRpc();
        }

        static int IdOf(ItemData d) { var db = ItemDatabase.Instance; return db != null ? db.IndexOf(d) : -1; }

        static PlayerInventory InventoryOf(ulong client)
        {
            foreach (var inv in PlayerInventory.All)
                if (inv != null && inv.IsSpawned && inv.OwnerClientId == client) return inv;
            return null;
        }

        // ---------------- called on the local player's machine ----------------

        public string RequestPickup(WorldItem w, PlayerInventory inv)
        {
            int i = Array.IndexOf(items, w);
            if (i < 0 || inv == null) return null;
            if (!Online) return DoPickup(i, inv);
            PickupRpc(i);
            return null;
        }

        public string RequestDrop(PlayerInventory inv, ItemData item, Vector3 position, Quaternion rotation)
        {
            if (inv == null || item == null) return null;
            int id = IdOf(item);
            if (id < 0) return null;
            if (!Online) return DoDrop(inv, id, position, rotation);
            DropRpc(id, position, rotation);
            return null;
        }

        // ---------------- host side ----------------

        string DoPickup(int i, PlayerInventory inv)
        {
            var w = items[i];
            if (!w.IsPresent) return null;
            int id = IdOf(w.item);
            if (id < 0) return "이 물건은 아이템 목록(ItemDatabase_LGH)에 없다";
            if (!inv.AddItem(id, w.Amount)) return "더 이상 들 수 없다";
            Apply(i, false, w.transform.position, w.transform.rotation, w.Amount);
            return null;   // the inventory shows "+ name" itself
        }

        string DoDrop(PlayerInventory inv, int id, Vector3 position, Quaternion rotation)
        {
            int have = inv.CountOf(id);
            if (have <= 0) return null;
            // reuse a hidden world object of the same kind so nothing has to be spawned
            int slot = -1;
            for (int k = 0; k < items.Length; k++)
                if (!items[k].IsPresent && IdOf(items[k].item) == id) { slot = k; break; }
            if (slot < 0) return "여기에는 내려놓을 수 없다";
            int count = Mathf.Min(have, Mathf.Max(1, items[slot].amount));
            inv.RemoveItem(id, count);
            Apply(slot, true, position, rotation, count);
            return null;
        }

        void Apply(int i, bool present, Vector3 pos, Quaternion rot, int count)
        {
            if (Online) StateRpc(i, present, pos, rot, count);
            else SetLocal(i, present, pos, rot, count);
        }

        void SetLocal(int i, bool present, Vector3 pos, Quaternion rot, int count)
        {
            var w = items[i];
            if (present)
            {
                // a dropped item no longer rides along inside a drawer
                if (w.transform.parent != null && w.transform.parent.GetComponentInParent<InteractableDrawer>() != null) w.transform.SetParent(null, true);
                w.SetPose(pos, rot);
            }
            w.SetState(present, count);
        }

        [Rpc(SendTo.Server)]
        void PickupRpc(int index, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (index < 0 || index >= items.Length) return;
            var inv = InventoryOf(sender);
            if (inv == null) return;
            string msg = DoPickup(index, inv);
            if (!string.IsNullOrEmpty(msg)) MessageRpc(msg, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.Server)]
        void DropRpc(int itemId, Vector3 position, Quaternion rotation, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var inv = InventoryOf(sender);
            if (inv == null) return;
            string msg = DoDrop(inv, itemId, position, rotation);
            if (!string.IsNullOrEmpty(msg)) MessageRpc(msg, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void StateRpc(int index, bool present, Vector3 pos, Quaternion rot, int count)
        {
            if (index >= 0 && index < items.Length) SetLocal(index, present, pos, rot, count);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void MessageRpc(string message, RpcParams rpcParams)
        {
            if (PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage(message);
        }

        [Rpc(SendTo.Server)]
        void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            int n = items.Length;
            var present = new bool[n]; var pos = new Vector3[n]; var rot = new Quaternion[n]; var count = new int[n];
            for (int i = 0; i < n; i++)
            {
                present[i] = items[i].IsPresent; pos[i] = items[i].transform.position; rot[i] = items[i].transform.rotation; count[i] = items[i].Amount;
            }
            FullStateRpc(present, pos, rot, count, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void FullStateRpc(bool[] present, Vector3[] pos, Quaternion[] rot, int[] count, RpcParams rpcParams)
        {
            int n = Mathf.Min(items.Length, present.Length);
            for (int i = 0; i < n; i++)
            {
                // only move items that are not where the designer put them
                if (present[i] && (items[i].transform.position - pos[i]).sqrMagnitude > 0.0001f) SetLocal(i, true, pos[i], rot[i], count[i]);
                else items[i].SetState(present[i], count[i]);
            }
        }

        // Host: a player is leaving - put everything they carried on the floor where they stood,
        // so a key can't vanish with them.
        public void ServerDropAll(PlayerInventory inv, Vector3 feet)
        {
            if (inv == null || (Online && !IsServer)) return;
            if (NetworkManager != null && NetworkManager.ShutdownInProgress) return;
            var stacks = inv.ItemSnapshot();
            int k = 0;
            foreach (var s in stacks)
            {
                Vector3 p = feet + Quaternion.Euler(0f, k * 47f, 0f) * new Vector3(0.35f, 0.05f, 0f);
                DoDrop(inv, s.id, p, Quaternion.Euler(0f, k * 73f, 0f));
                k++;
            }
        }
    }
}
