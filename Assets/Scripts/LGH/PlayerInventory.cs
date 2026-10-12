using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // What the player carries. Press I to open or close the inventory panel.
    // The host owns the numbers (CandleNetworkSync changes them); each player only sees their own panel.
    public class PlayerInventory : NetworkBehaviour
    {
        public static PlayerInventory Local { get; private set; }
        public static readonly List<PlayerInventory> All = new List<PlayerInventory>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); Local = null; }

        public int slotCount = 6;

        // seconds of burn time left on each candle carried (the host writes, everyone reads)
        NetworkList<float> candles;
        readonly List<float> offlineCandles = new List<float>();
        bool open;
        float flashUntil;

        GUIStyle titleStyle, hintStyle, countStyle, nameStyle, descStyle, timeStyle;
        Texture2D candleIcon;

        public int Candles { get { return IsSpawned ? candles.Count : offlineCandles.Count; } }
        public float CandleAt(int i) { return IsSpawned ? candles[i] : offlineCandles[i]; }
        public bool IsOpen { get { return open; } }

        bool Mine { get { return IsSpawned ? IsOwner : Local == this; } }

        void Awake()
        {
            candles = new NetworkList<float>(new List<float>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public List<float> Snapshot()
        {
            var l = new List<float>();
            for (int i = 0; i < Candles; i++) l.Add(CandleAt(i));
            return l;
        }

        // ---- host / single player only ----
        public void AddCandle(float remaining)
        {
            if (!IsSpawned) { offlineCandles.Add(remaining); flashUntil = Time.time + 1.2f; }
            else if (IsServer) candles.Add(remaining);
        }

        // Takes the most burnt-down candle (so leftovers get used first). Returns its burn time, or -1 when empty.
        public float TakeShortest()
        {
            int n = Candles;
            if (n == 0) return -1f;
            int best = 0;
            for (int i = 1; i < n; i++) if (CandleAt(i) < CandleAt(best)) best = i;
            float r = CandleAt(best);
            if (!IsSpawned) { offlineCandles.RemoveAt(best); flashUntil = Time.time + 1.2f; }
            else if (IsServer) candles.RemoveAt(best);
            return r;
        }

        // Takes the least burnt candle (for swapping into the hand). Returns its burn time, or -1 when empty.
        public float TakeLongest()
        {
            int n = Candles;
            if (n == 0) return -1f;
            int best = 0;
            for (int i = 1; i < n; i++) if (CandleAt(i) > CandleAt(best)) best = i;
            float r = CandleAt(best);
            if (!IsSpawned) { offlineCandles.RemoveAt(best); flashUntil = Time.time + 1.2f; }
            else if (IsServer) candles.RemoveAt(best);
            return r;
        }

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) Local = this;
            candles.OnListChanged += OnCandlesChanged;
        }

        public override void OnNetworkDespawn()
        {
            candles.OnListChanged -= OnCandlesChanged;
            if (Local == this) Local = null;
        }

        void OnCandlesChanged(NetworkListEvent<float> e)
        {
            flashUntil = Time.time + 1.2f;
        }

        void Update()
        {
            if (!Mine) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.iKey.wasPressedThisFrame) open = !open;
            if (open && kb.escapeKey.wasPressedThisFrame) open = false;
        }

        // ---------- GUI ----------

        void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = new Color(0.93f, 0.82f, 0.58f);
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            hintStyle.normal.textColor = new Color(0.75f, 0.70f, 0.62f);
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            countStyle.normal.textColor = Color.white;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            nameStyle.normal.textColor = new Color(0.95f, 0.90f, 0.80f);
            descStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            descStyle.normal.textColor = new Color(0.80f, 0.76f, 0.70f);
            timeStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            timeStyle.normal.textColor = new Color(1f, 0.88f, 0.62f);
            candleIcon = MakeCandleIcon();
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Frame(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void OnGUI()
        {
            if (!Mine) return;
            Styles();
            var v = UiScale.Apply();
            float w = v.x, h = v.y;
            bool paused = Cursor.lockState != CursorLockMode.Locked;
            var brass = new Color(0.72f, 0.56f, 0.28f, 1f);

            // small corner hint, lights up when the count changes
            int n = Candles;
            bool flash = Time.time < flashUntil;
            if (!open && !paused)
            {
                var hr = new Rect(16f, h - 46f, 200f, 32f);
                Fill(hr, new Color(0f, 0f, 0f, flash ? 0.7f : 0.45f));
                if (candleIcon != null) GUI.DrawTexture(new Rect(hr.x + 4f, hr.y + 2f, 28f, 28f), candleIcon);
                var s = new GUIStyle(descStyle) { fontSize = 15 };
                if (flash) s.normal.textColor = new Color(1f, 0.85f, 0.5f);
                GUI.Label(new Rect(hr.x + 36f, hr.y, hr.width - 40f, hr.height), "×" + n + "    [I] 소지품", s);
            }
            if (!open || paused) return;

            const float slot = 84f, gap = 10f;
            float pw = slotCount * slot + (slotCount - 1) * gap + 48f, ph = 236f;
            var r = new Rect((w - pw) / 2f, h - ph - 60f, pw, ph);
            Fill(r, new Color(0.06f, 0.045f, 0.035f, 0.92f));
            Frame(r, brass, 2f);
            Frame(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f), new Color(brass.r, brass.g, brass.b, 0.35f), 1f);

            GUI.Label(new Rect(r.x + 24f, r.y + 12f, 200f, 32f), "소지품", titleStyle);
            GUI.Label(new Rect(r.xMax - 224f, r.y + 12f, 200f, 32f), "[I] 닫기", hintStyle);

            float sy = r.y + 54f;
            float full = Mathf.Max(1f, CandleNetworkSync.BurnSeconds);
            // new candles stack in one slot; partly burnt ones get their own slot and look shorter
            int fresh = 0;
            var partial = new List<float>();
            for (int i = 0; i < n; i++) { float left = CandleAt(i); if (left >= full - 1f) fresh++; else partial.Add(left); }
            partial.Sort((x, y) => y.CompareTo(x));
            int slotIndex = 0;
            for (int i = 0; i < slotCount; i++)
            {
                var sr = new Rect(r.x + 24f + i * (slot + gap), sy, slot, slot);
                Fill(sr, new Color(0.13f, 0.10f, 0.08f, 1f));
                Frame(sr, new Color(brass.r, brass.g, brass.b, 0.3f), 1.5f);
            }
            if (fresh > 0) DrawCandleSlot(r, sy, slot, gap, slotIndex++, 1f, fresh, brass);
            for (int k = 0; k < partial.Count && slotIndex < slotCount; k++)
            {
                bool last = slotIndex == slotCount - 1 && k < partial.Count - 1;
                if (last) { DrawCandleSlot(r, sy, slot, gap, slotIndex++, partial[k] / full, partial.Count - k, brass); break; }
                DrawCandleSlot(r, sy, slot, gap, slotIndex++, partial[k] / full, 1, brass);
            }

            float ty = sy + slot + 12f;
            if (n > 0)
            {
                GUI.Label(new Rect(r.x + 24f, ty, pw - 48f, 26f), "양초  " + n + "개", nameStyle);
                GUI.Label(new Rect(r.x + 24f, ty + 26f, pw - 48f, 44f), "벽의 양초대에 끼우면 불이 켜진다. [R] 들고 있는 양초와 교체", descStyle);
            }
            else
            {
                GUI.Label(new Rect(r.x + 24f, ty, pw - 48f, 26f), "비어 있다", nameStyle);
                GUI.Label(new Rect(r.x + 24f, ty + 26f, pw - 48f, 44f), "저택 어딘가에 양초가 놓여 있다. 보고 E를 누르면 주울 수 있다.", descStyle);
            }
        }

        void DrawCandleSlot(Rect r, float sy, float slot, float gap, int i, float f, int count, Color brass)
        {
            var sr = new Rect(r.x + 24f + i * (slot + gap), sy, slot, slot);
            Frame(sr, new Color(brass.r, brass.g, brass.b, 0.9f), 1.5f);
            f = Mathf.Clamp01(f);
            // the candle just looks shorter the more it has burnt (no numbers shown)
            float ih = (slot - 14f) * Mathf.Lerp(0.45f, 1f, f);
            if (candleIcon != null) GUI.DrawTexture(new Rect(sr.x + 10f, sr.y + 7f + (slot - 14f - ih), slot - 20f, ih), candleIcon, ScaleMode.StretchToFill);
            if (count > 1) GUI.Label(new Rect(sr.x, sr.y, sr.width - 6f, sr.height - 2f), "×" + count, countStyle);
        }

        // little candle drawn in code so the UI needs no image files
        static Texture2D MakeCandleIcon()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = new Color(0, 0, 0, 0);
                float cx = x - 31.5f;
                // brass dish
                float ex = cx / 24f, ey = (y - 7f) / 4f;
                if (ex * ex + ey * ey <= 1f) c = Color.Lerp(new Color(0.55f, 0.40f, 0.16f, 1f), new Color(0.90f, 0.74f, 0.40f, 1f), Mathf.Clamp01(0.5f - cx / 40f + ey * 0.3f));
                // wax
                if (y >= 8 && y <= 44 && Mathf.Abs(cx) <= 7.5f)
                {
                    float shade = 0.80f + 0.20f * Mathf.Clamp01(1f - (cx + 3f) / 10f);
                    c = new Color(0.96f * shade, 0.92f * shade, 0.80f * shade, 1f);
                }
                // drip
                if (y >= 34 && y <= 44 && cx >= 4.5f && cx <= 7.5f && y >= 44 - (cx - 4.5f) * 3f) c = new Color(1f, 0.97f, 0.88f, 1f);
                // wick
                if (y > 44 && y <= 48 && Mathf.Abs(cx) <= 0.9f) c = new Color(0.15f, 0.10f, 0.08f, 1f);
                // flame
                float fy = (y - 46f) / 16f;
                if (fy > 0f && fy < 1f)
                {
                    float half = 6.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(fy, 0.75f)), 0.9f) * (1f - fy * 0.35f);
                    float d = Mathf.Abs(cx) / Mathf.Max(half, 0.01f);
                    if (d <= 1f)
                    {
                        float core = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1.2f - fy * 1.4f);
                        var f = Color.Lerp(new Color(1f, 0.45f, 0.08f, 0.85f), new Color(1f, 0.97f, 0.75f, 1f), core);
                        f.a *= Mathf.Clamp01((1f - d) * 3f);
                        c = Color.Lerp(c, f, f.a);
                        c.a = Mathf.Max(c.a, f.a);
                    }
                }
                px[y * S + x] = c;
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
