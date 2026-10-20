using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LGH
{
    // Sits next to DoorNetworkSync. The host puts 1-4 candles on every candle spot each game, and decides
    // who picked them up and which wall holders have a candle in them.
    // Every candle has its own burn time (3 minutes when new). It only burns while it is in a holder;
    // taking it out keeps what is left. Everyone else mirrors the host, and a late joiner gets the full state.
    public class CandleNetworkSync : NetworkBehaviour
    {
        public static CandleNetworkSync Instance { get; private set; }

        // Burn time of a new candle, read by the holders, loose candles and the inventory
        public static float BurnSeconds = 180f;
        // true = candles never burn down (current setting)
        public static bool Infinite = true;

        [Tooltip("Fewest candles on each candle spot")]
        [Min(0)] public int minCandles = 1;
        [Tooltip("Most candles on each candle spot")]
        [Min(0)] public int maxCandles = 4;
        [Tooltip("How long a new candle burns once it is put in a holder (seconds)")]
        [Min(5f)] public float burnSeconds = 180f;
        [Tooltip("Candles never burn down or go out. Turn off to use burnSeconds again")]
        public bool infiniteCandles = true;
        [Tooltip("A candle taken out with less than this left is used up")]
        public float minUsefulSeconds = 2f;

        CandlePickup[] pickups = new CandlePickup[0];
        CandleSconce[] sconces = new CandleSconce[0];
        InteractableDrawer[] drawers = new InteractableDrawer[0];
        readonly Dictionary<ulong, List<float>> carried = new Dictionary<ulong, List<float>>();   // host: what each player holds

        static bool Online { get { return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening; } }

        void Awake()
        {
            Instance = this;
            BurnSeconds = burnSeconds;
            Infinite = infiniteCandles;
            pickups = FindObjectsByType<CandlePickup>(FindObjectsInactive.Include);
            sconces = FindObjectsByType<CandleSconce>(FindObjectsInactive.Include);
            Array.Sort(pickups, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
            Array.Sort(sconces, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
            drawers = FindObjectsByType<InteractableDrawer>(FindObjectsInactive.Include);
            Array.Sort(drawers, (a, b) => string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)));
            foreach (var p in pickups) p.SetPresent(0, 0f);
        }

        void OnValidate() { BurnSeconds = burnSeconds; Infinite = infiniteCandles; }

        void Start()
        {
            if (!Online) Randomize();   // playing without the multiplayer menu
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

        public int PickupCount { get { return pickups.Length; } }
        public int SconceCount { get { return sconces.Length; } }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                carried.Clear();
                foreach (var s in sconces) s.SetLit(s.startLit, burnSeconds);
                foreach (var d in drawers) d.SetOpen(false, true);
                Randomize();
                NetworkManager.OnClientDisconnectCallback += OnClientLeft;
            }
            else RequestFullStateRpc();
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        }

        // host / single player: every candle spot gets 1-4 candles this game
        void Randomize()
        {
            int lo = Mathf.Min(minCandles, maxCandles), hi = Mathf.Max(minCandles, maxCandles);
            foreach (var p in pickups) p.SetPresent(UnityEngine.Random.Range(lo, hi + 1), burnSeconds);
        }

        static int[] Shuffled(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); int t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }

        // ---------- called on the local player's machine ----------
        // Returns a message to show right away (single player), or null when the host will answer.

        public string RequestPickup(CandlePickup pickup, PlayerInventory inventory)
        {
            int i = Array.IndexOf(pickups, pickup);
            if (i < 0) return null;
            if (IsSpawned) { PickupRpc(i); return null; }
            if (Online || inventory == null) return null;
            return DoPickup(i, inventory, 0);
        }

        public string RequestSconce(CandleSconce sconce, PlayerInventory inventory)
        {
            int i = Array.IndexOf(sconces, sconce);
            if (i < 0) return null;
            if (!sconce.IsLit && !CanPlace(inventory)) return NoCandleMessage;
            if (IsSpawned) { SconceRpc(i); return null; }
            if (Online || inventory == null) return null;
            return DoSconce(i, inventory, 0);
        }

        bool HeldUsable(PlayerInventory inv)
        {
            var h = inv != null ? inv.GetComponentInChildren<HeldCandle>(true) : null;
            return h != null && h.Holding && h.Remaining >= minUsefulSeconds;
        }

        bool CanPlace(PlayerInventory inv) { return inv != null && (inv.Candles > 0 || HeldUsable(inv)); }

        public string RequestDrawer(InteractableDrawer drawer)
        {
            int i = Array.IndexOf(drawers, drawer);
            if (i < 0) return null;
            if (IsSpawned) { DrawerRpc(i); return null; }
            if (!Online) drawer.SetOpen(!drawer.IsOpen);
            return null;
        }

        [Rpc(SendTo.Server)]
        void DrawerRpc(int index)
        {
            if (index < 0 || index >= drawers.Length) return;
            bool open = !drawers[index].IsOpen;
            drawers[index].SetOpen(open);
            DrawerStateRpc(index, open);
        }

        [Rpc(SendTo.NotServer)]
        void DrawerStateRpc(int index, bool open)
        {
            if (index >= 0 && index < drawers.Length) drawers[index].SetOpen(open);
        }

        public string RequestSwapHeld(PlayerInventory inventory)
        {
            if (inventory == null) return null;
            if (inventory.Candles <= 0) return "교체할 양초가 없다";
            if (IsSpawned) { SwapRpc(); return null; }
            if (Online) return null;
            return DoSwap(inventory, 0);
        }

        const string NoCandleMessage = "양초가 없다. 저택 어딘가에서 양초를 찾아보자";

        // ---------- host side ----------

        string DoPickup(int i, PlayerInventory inv, ulong client)
        {
            if (!pickups[i].IsPresent) return null;   // the other player was quicker
            float left = pickups[i].Remaining;
            int count = pickups[i].Count;
            SetPickup(i, 0, 0f);
            for (int k = 0; k < count; k++) inv.AddCandle(left);
            Remember(inv, client);
            return "양초 " + count + "개를 주웠다 (가진 양초 " + inv.Candles + "개)";
        }

        string DoSconce(int i, PlayerInventory inv, ulong client)
        {
            var s = sconces[i];
            var held = inv.GetComponentInChildren<HeldCandle>(true);
            if (s.IsLit)
            {
                float left = s.Remaining;
                SetSconce(i, false, 0f);
                if (left < minUsefulSeconds) return "양초가 다 타 버렸다";
                if (held != null && !held.IsBurning)
                {
                    held.SetRemaining(left);   // empty hand: hold it
                    return "양초를 빼서 손에 들었다";
                }
                inv.AddCandle(left);
                Remember(inv, client);
                return "양초를 뺐다";
            }
            if (inv.Candles > 0)
            {
                float burn = inv.TakeShortest();
                Remember(inv, client);
                SetSconce(i, true, burn);
                return "양초를 끼웠다";
            }
            if (HeldUsable(inv))
            {
                float burn = held.Remaining;
                held.Drop();
                SetSconce(i, true, burn);
                return "들고 있던 양초를 걸었다";
            }
            return NoCandleMessage;
        }

        // swap the candle in the player's hand for the fullest one in the inventory;
        // the old one goes back to the inventory unless it is used up
        string DoSwap(PlayerInventory inv, ulong client)
        {
            var held = inv.GetComponentInChildren<HeldCandle>(true);
            if (held == null) return null;
            float fresh = inv.TakeLongest();
            if (fresh < 0f) return "교체할 양초가 없다";
            float old = held.Remaining;
            bool had = old > 0f;
            held.SetRemaining(fresh);
            if (old >= minUsefulSeconds) inv.AddCandle(old);
            Remember(inv, client);
            return had ? "들고 있는 양초를 교체했다" : "양초를 손에 들었다";
        }

        [Rpc(SendTo.Server)]
        void SwapRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var inv = InventoryOf(sender);
            if (inv == null) return;
            Reply(DoSwap(inv, sender), sender);
        }

        public void Remember(PlayerInventory inv, ulong client)
        {
            if (IsSpawned) carried[client] = inv.Snapshot();
        }

        void SetPickup(int i, int count, float remaining)
        {
            pickups[i].SetPresent(count, remaining);
            if (IsSpawned) PickupStateRpc(i, count, remaining);
        }

        void SetSconce(int i, bool lit, float remaining)
        {
            sconces[i].SetLit(lit, remaining);
            if (IsSpawned) SconceStateRpc(i, lit, remaining);
        }

        static PlayerInventory InventoryOf(ulong client)
        {
            foreach (var inv in PlayerInventory.All)
                if (inv != null && inv.IsSpawned && inv.OwnerClientId == client) return inv;
            return null;
        }

        // a player left while carrying candles: put them back on free candle spots so they are not lost
        void OnClientLeft(ulong client)
        {
            if (!IsServer) return;
            List<float> held;
            if (!carried.TryGetValue(client, out held)) return;
            carried.Remove(client);
            var order = Shuffled(pickups.Length);
            int h = 0;
            for (int k = 0; k < order.Length && h < held.Count; k++)
                if (!pickups[order[k]].IsPresent) SetPickup(order[k], 1, held[h++]);
        }

        [Rpc(SendTo.Server)]
        void PickupRpc(int index, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= pickups.Length) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            var inv = InventoryOf(sender);
            if (inv == null) return;
            Reply(DoPickup(index, inv, sender), sender);
        }

        [Rpc(SendTo.Server)]
        void SconceRpc(int index, RpcParams rpcParams = default)
        {
            if (index < 0 || index >= sconces.Length) return;
            ulong sender = rpcParams.Receive.SenderClientId;
            var inv = InventoryOf(sender);
            if (inv == null) return;
            Reply(DoSconce(index, inv, sender), sender);
        }

        void Reply(string msg, ulong client)
        {
            if (string.IsNullOrEmpty(msg)) return;
            MessageRpc(msg, RpcTarget.Single(client, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.NotServer)]
        void PickupStateRpc(int index, int count, float remaining)
        {
            if (index >= 0 && index < pickups.Length) pickups[index].SetPresent(count, remaining);
        }

        [Rpc(SendTo.NotServer)]
        void SconceStateRpc(int index, bool lit, float remaining)
        {
            if (index >= 0 && index < sconces.Length) sconces[index].SetLit(lit, remaining);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void MessageRpc(string message, RpcParams rpcParams)
        {
            if (PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage(message);
        }

        [Rpc(SendTo.Server)]
        void RequestFullStateRpc(RpcParams rpcParams = default)
        {
            var present = new int[pickups.Length];
            var pickupLeft = new float[pickups.Length];
            var lit = new bool[sconces.Length];
            var litLeft = new float[sconces.Length];
            var open = new bool[drawers.Length];
            for (int i = 0; i < pickups.Length; i++) { present[i] = pickups[i].Count; pickupLeft[i] = pickups[i].Remaining; }
            for (int i = 0; i < sconces.Length; i++) { lit[i] = sconces[i].IsLit; litLeft[i] = sconces[i].Remaining; }
            for (int i = 0; i < drawers.Length; i++) open[i] = drawers[i].IsOpen;
            FullStateRpc(present, pickupLeft, lit, litLeft, open, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void FullStateRpc(int[] present, float[] pickupLeft, bool[] lit, float[] litLeft, bool[] open, RpcParams rpcParams)
        {
            for (int i = 0; i < pickups.Length; i++)
            {
                int c = i < present.Length ? present[i] : 0;
                pickups[i].SetPresent(c, c > 0 && i < pickupLeft.Length ? pickupLeft[i] : 0f);
            }
            for (int i = 0; i < sconces.Length && i < lit.Length; i++)
                sconces[i].SetLit(lit[i], i < litLeft.Length ? litLeft[i] : 0f);
            for (int i = 0; i < drawers.Length && i < open.Length; i++)
                drawers[i].SetOpen(open[i], true);
        }
    }
}
