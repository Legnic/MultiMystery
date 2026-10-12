using Unity.Netcode;
using UnityEngine;

namespace LGH
{
    // The candle each player carries. It is lit when the player enters the game and burns for
    // CandleNetworkSync.BurnSeconds (same as the wall candles), visibly getting shorter, then goes out.
    // It can be hung on a wall holder (the hand is then empty) and swapped for one from the inventory with R.
    // The host stores when it was lit and whether the hand holds one, so both players see the same thing.
    public class HeldCandle : NetworkBehaviour
    {
        public Transform wax;
        [Tooltip("Wick + flame + light; rides down on top of the wax")]
        public Transform flameRoot;
        public CandleFlicker flicker;
        [Tooltip("Seconds before burning out where the flame starts to weaken")]
        public float dyingSeconds = 20f;

        readonly NetworkVariable<double> litAt = new NetworkVariable<double>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> holding = new NetworkVariable<bool>(true,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        Vector3 waxPos, waxScale, flamePos;
        float baseIntensity, offlineStart;
        bool offlineHolding = true, captured, shown = true, wasBurning = true;
        Renderer[] renderers;

        public bool Holding { get { return IsSpawned ? holding.Value : offlineHolding; } }

        public float Remaining
        {
            get
            {
                if (!Holding) return 0f;
                double now = IsSpawned ? NetworkManager.ServerTime.Time : Time.time;
                double start = IsSpawned ? litAt.Value : offlineStart;
                return Mathf.Max(0f, CandleNetworkSync.BurnSeconds - (float)(now - start));
            }
        }
        public bool IsBurning { get { return Remaining > 0f; } }

        void Capture()
        {
            if (captured) return;
            captured = true;
            if (wax != null) { waxPos = wax.localPosition; waxScale = wax.localScale; }
            if (flameRoot != null) flamePos = flameRoot.localPosition;
            if (flicker != null) baseIntensity = flicker.baseIntensity;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void Awake()
        {
            Capture();
            offlineStart = Time.time;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) { litAt.Value = NetworkManager.ServerTime.Time; holding.Value = true; }
        }

        // ---- host / single player ----
        // put a candle with this much burn left in the player's hand
        public void SetRemaining(float remaining)
        {
            float burnt = Mathf.Max(0f, CandleNetworkSync.BurnSeconds - remaining);
            if (IsSpawned) { if (IsServer) { litAt.Value = NetworkManager.ServerTime.Time - burnt; holding.Value = true; } }
            else { offlineStart = Time.time - burnt; offlineHolding = true; }
        }

        // the candle left the hand (hung on a wall)
        public void Drop()
        {
            if (IsSpawned) { if (IsServer) holding.Value = false; }
            else offlineHolding = false;
        }

        void Show(bool on)
        {
            if (shown == on) return;
            shown = on;
            foreach (var r in renderers) if (r != null) r.enabled = on;
        }

        void Update()
        {
            bool hold = Holding;
            Show(hold);
            float left = Remaining;
            bool burning = left > 0f;
            if (hold && wasBurning && !burning && (!IsSpawned || IsOwner) && PlayerInteractor.Active != null)
            {
                var inv = GetComponentInParent<PlayerInventory>();
                PlayerInteractor.Active.ShowMessage(inv != null && inv.Candles > 0 ? "들고 있던 양초가 다 타서 꺼졌다  [R] 양초 교체" : "들고 있던 양초가 다 타서 꺼졌다");
            }
            wasBurning = burning;

            if (flameRoot != null && flameRoot.gameObject.activeSelf != burning) flameRoot.gameObject.SetActive(burning);
            if (flicker != null && flicker.enabled != burning) flicker.enabled = burning;
            if (!hold) return;

            float f = Mathf.Clamp01(left / Mathf.Max(1f, CandleNetworkSync.BurnSeconds));
            float k = Mathf.Lerp(CandleSconce.StubFraction, 1f, f);
            if (wax != null)
            {
                wax.localScale = new Vector3(waxScale.x, waxScale.y * k, waxScale.z);
                wax.localPosition = waxPos + Vector3.up * (waxScale.y * (k - 1f));
            }
            if (flameRoot != null) flameRoot.localPosition = flamePos - Vector3.up * (2f * waxScale.y * (1f - k));
            if (flicker != null)
            {
                float life = dyingSeconds > 0f ? Mathf.Clamp01(left / dyingSeconds) : 1f;
                flicker.baseIntensity = baseIntensity * Mathf.Lerp(0.3f, 1f, life);
            }
        }
    }
}
