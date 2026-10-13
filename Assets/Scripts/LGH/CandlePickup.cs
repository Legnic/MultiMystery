using UnityEngine;

namespace LGH
{
    // A place where candles lie (on furniture, on a shelf, inside a drawer). Each game the host puts 1-4 candles
    // on every spot (CandleNetworkSync). Aim at them and press E to take them all into your inventory.
    // Candles dropped back here keep the burn they had left and look that much shorter.
    public class CandlePickup : MonoBehaviour
    {
        [Tooltip("Model + collider, hidden while no candle is here")]
        public GameObject visual;
        [Tooltip("Candle models; the first Count of them are shown")]
        public GameObject[] units;
        [Tooltip("Wax of every candle model (shrinks with the burn left)")]
        public Transform[] waxes;
        [Tooltip("Wick of every candle model (moves down with the wax)")]
        public Transform[] wicks;

        public int Count { get; private set; }
        public bool IsPresent { get { return Count > 0; } }
        public float Remaining { get; private set; }

        Vector3[] waxPos, waxScale, wickPos;

        void Capture()
        {
            if (waxPos != null) return;
            int n = waxes != null ? waxes.Length : 0;
            waxPos = new Vector3[n]; waxScale = new Vector3[n]; wickPos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (waxes[i] != null) { waxPos[i] = waxes[i].localPosition; waxScale[i] = waxes[i].localScale; }
                if (wicks != null && i < wicks.Length && wicks[i] != null) wickPos[i] = wicks[i].localPosition;
            }
        }

        public void SetPresent(int count, float remaining)
        {
            Capture();
            Count = Mathf.Max(0, count);
            Remaining = Count > 0 ? remaining : 0f;
            if (visual != null) visual.SetActive(Count > 0);
            if (units != null)
                for (int i = 0; i < units.Length; i++) if (units[i] != null) units[i].SetActive(i < Count);
            if (Count == 0) return;
            float f = Mathf.Clamp01(remaining / Mathf.Max(1f, CandleNetworkSync.BurnSeconds));
            float k = Mathf.Lerp(CandleSconce.StubFraction, 1f, f);
            for (int i = 0; i < waxPos.Length; i++)
            {
                if (waxes[i] == null) continue;
                waxes[i].localScale = new Vector3(waxScale[i].x, waxScale[i].y * k, waxScale[i].z);
                waxes[i].localPosition = waxPos[i] + Vector3.up * (waxScale[i].y * (k - 1f));
                if (wicks != null && i < wicks.Length && wicks[i] != null)
                    wicks[i].localPosition = wickPos[i] - Vector3.up * (2f * waxScale[i].y * (1f - k));
            }
        }
    }
}
