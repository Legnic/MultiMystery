using System;
using UnityEngine;

namespace LGH
{
    // Wall candle holder, one per room. Starts empty. Aim at it and press E with a candle in your inventory
    // to put it in and light the room; press E again to take the candle back out (it keeps the time it has left).
    // A lit candle burns down for CandleNetworkSync.BurnSeconds (3 minutes), visibly getting shorter,
    // then goes out and the holder is empty again.
    // In multiplayer the host decides through CandleNetworkSync and every player calls SetLit().
    public class CandleSconce : MonoBehaviour
    {
        public string roomName = "";
        [Tooltip("Wax, flame and light. Shown only while a candle is in the holder")]
        public GameObject candle;
        [Tooltip("Wax cylinder that gets shorter while burning (pivot in the middle, bottom sits in the socket)")]
        public Transform wax;
        [Tooltip("Wick + flame + light; rides down on top of the wax")]
        public Transform flameRoot;
        [Tooltip("Melted wax collecting in the drip pan")]
        public Transform meltPool;
        public CandleFlicker flicker;
        [Tooltip("Seconds before burning out where the flame starts to weaken")]
        public float dyingSeconds = 20f;
        public bool startLit = false;

        public const float StubFraction = 0.06f;   // length of what is left when the candle is spent

        public bool IsLit { get; private set; }
        public float Remaining { get; private set; }

        // How many holders are lit right now, and an event puzzle scripts can listen to
        public static int LitCount { get; private set; }
        public static event Action<CandleSconce> Changed;

        // statics survive between plays when domain reload is off
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { LitCount = 0; Changed = null; }

        Vector3 waxPos, waxScale, flamePos, poolScale;
        float baseIntensity;
        bool captured;

        void Capture()
        {
            if (captured) return;
            captured = true;
            if (wax != null) { waxPos = wax.localPosition; waxScale = wax.localScale; }
            if (flameRoot != null) flamePos = flameRoot.localPosition;
            if (meltPool != null) poolScale = meltPool.localScale;
            if (flicker != null) baseIntensity = flicker.baseIntensity;
        }

        void Awake()
        {
            Capture();
            IsLit = false;
            if (candle != null) candle.SetActive(false);
            if (startLit) SetLit(true, CandleNetworkSync.BurnSeconds);
        }

        void OnDestroy()
        {
            if (IsLit) LitCount--;
        }

        public void SetLit(bool lit, float remaining)
        {
            Capture();
            bool changed = lit != IsLit;
            if (changed) LitCount += lit ? 1 : -1;
            IsLit = lit;
            Remaining = lit ? Mathf.Max(0f, remaining) : 0f;
            if (candle != null) candle.SetActive(lit);
            if (lit) ApplyBurn();
            if (changed && Changed != null) Changed(this);
        }

        void Update()
        {
            if (!IsLit || CandleNetworkSync.Infinite) return;   // infinite candles never burn down
            Remaining -= Time.deltaTime;
            if (Remaining <= 0f) { SetLit(false, 0f); return; }   // burnt out (every player runs the same clock)
            ApplyBurn();
        }

        void ApplyBurn()
        {
            float f = Mathf.Clamp01(Remaining / Mathf.Max(1f, CandleNetworkSync.BurnSeconds));
            float k = Mathf.Lerp(StubFraction, 1f, f);
            float drop = 2f * waxScale.y * (1f - k);   // how far the top of the wax has come down
            if (wax != null)
            {
                wax.localScale = new Vector3(waxScale.x, waxScale.y * k, waxScale.z);
                wax.localPosition = waxPos + Vector3.up * (waxScale.y * (k - 1f));
            }
            if (flameRoot != null) flameRoot.localPosition = flamePos - Vector3.up * drop;
            if (meltPool != null)
            {
                float s = Mathf.Lerp(0.35f, 1f, 1f - f);
                meltPool.localScale = new Vector3(poolScale.x * s, poolScale.y, poolScale.z * s);
            }
            if (flicker != null)
            {
                float life = dyingSeconds > 0f ? Mathf.Clamp01(Remaining / dyingSeconds) : 1f;
                flicker.baseIntensity = baseIntensity * Mathf.Lerp(0.3f, 1f, life);
            }
        }
    }
}
