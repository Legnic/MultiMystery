using System;
using Unity.Netcode;
using UnityEngine;

namespace LGH
{
    // One per scene (placed with a NetworkObject). The host owns every door's state:
    // a player asks to open/close, the host checks locks, then tells everyone what happened.
    // A player who joins later receives the current state of all doors.
    public class DoorNetworkSync : NetworkBehaviour
    {
        public static DoorNetworkSync Instance { get; private set; }

        InteractableDoor[] doors = new InteractableDoor[0];

        void Awake()
        {
            Instance = this;
            doors = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include);
            Array.Sort(doors, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
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

        int IndexOf(InteractableDoor d) { return Array.IndexOf(doors, d); }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) RequestFullStateRpc();
        }

        // ---- called on the local player's machine ----
        public void RequestInteract(InteractableDoor door, Vector3 userPosition)
        {
            int i = IndexOf(door);
            if (i >= 0) InteractRpc(i, userPosition);
        }

        // ---- host side helper for puzzle scripts ----
        public void SetLocked(InteractableDoor door, bool isLocked)
        {
            if (!IsServer) { Debug.LogWarning("DoorNetworkSync.SetLocked must be called on the host"); return; }
            int i = IndexOf(door);
            if (i >= 0) LockRpc(i, isLocked);
        }

        [Rpc(SendTo.Server)]
        void InteractRpc(int index, Vector3 userPosition, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= doors.Length) return;
            var d = doors[index];
            string blocked = d.CheckBlocked(userPosition);
            if (blocked != null)
            {
                MessageRpc(blocked, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
                return;
            }
            bool open = !d.IsOpen;
            ApplyRpc(index, open, d.ComputeAngle(open, userPosition));
            if (d.partner != null)
            {
                int p = IndexOf(d.partner);
                if (p >= 0) ApplyRpc(p, open, d.partner.ComputeAngle(open, userPosition));
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ApplyRpc(int index, bool open, float angle)
        {
            if (index >= 0 && index < doors.Length) doors[index].ApplyState(open, angle);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void LockRpc(int index, bool isLocked)
        {
            if (index >= 0 && index < doors.Length) doors[index].locked = isLocked;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void MessageRpc(string message, RpcParams rpcParams)
        {
            if (PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage(message);
        }

        [Rpc(SendTo.Server)]
        void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            var open = new bool[doors.Length];
            var angle = new float[doors.Length];
            var locks = new bool[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                open[i] = doors[i].IsOpen;
                angle[i] = doors[i].TargetAngle;
                locks[i] = doors[i].locked;
            }
            FullStateRpc(open, angle, locks, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void FullStateRpc(bool[] open, float[] angle, bool[] locks, RpcParams rpcParams)
        {
            int n = Mathf.Min(doors.Length, open.Length);
            for (int i = 0; i < n; i++)
            {
                doors[i].locked = locks[i];
                doors[i].ApplyState(open[i], angle[i], true);
            }
        }
    }
}
