using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // One stack in the inventory: an item kind (index in ItemDatabase_LGH) and how many.
    public struct ItemStack : INetworkSerializeByMemcpy, IEquatable<ItemStack>
    {
        public int id;
        public int count;
        public ItemStack(int id, int count) { this.id = id; this.count = count; }
        public bool Equals(ItemStack o) { return id == o.id && count == o.count; }
        public override bool Equals(object o) { return o is ItemStack s && Equals(s); }
        public override int GetHashCode() { return id * 397 ^ count; }
    }

    // What the player carries: candles (with burn time) and any other items (keys, letters, coins...).
    // I opens the bag and frees the mouse: click a slot to pick it, double-click reads, right-click puts it down
    // (the buttons do the same). Arrow keys / wheel / 1-9 and F / G still work. I / Esc closes.
    // The host owns the contents (CandleNetworkSync / ItemNetworkSync change them); each player only sees their own bag.
    public class PlayerInventory : NetworkBehaviour
    {
        public static PlayerInventory Local { get; private set; }
        public static readonly List<PlayerInventory> All = new List<PlayerInventory>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); Local = null; }

        [Tooltip("Slots per row in the bag panel (two rows are shown).")]
        public int slotCount = 6;
        public int Capacity { get { return slotCount * 2 - 1; } }   // one slot is kept for candles

        // seconds of burn time left on each candle carried (the host writes, everyone reads)
        NetworkList<float> candles;
        NetworkList<ItemStack> items;
        readonly List<float> offlineCandles = new List<float>();
        readonly List<ItemStack> offlineItems = new List<ItemStack>();
        bool open;
        int escFrame = -1;
        float flashUntil;
        int selected;
        ItemData reading;
        string toast;
        float toastUntil;
        readonly Dictionary<int, int> lastCounts = new Dictionary<int, int>();

        GUIStyle titleStyle, hintStyle, countStyle, nameStyle, descStyle, smallStyle, readStyle, toastStyle, buttonStyle;
        int styleVersion = -1;   // CoopDemo.MeetYouGuiSkin.Version — 스킨이 새로 만들어지면 스타일도 다시 만든다
        Texture2D candleIcon;

        public int Candles { get { return IsSpawned ? candles.Count : offlineCandles.Count; } }
        public float CandleAt(int i) { return IsSpawned ? candles[i] : offlineCandles[i]; }
        public bool IsOpen { get { return open || reading != null; } }

        // true while the local bag (or something read from it) is on screen - the mouse belongs to the UI then
        public static bool UiOpen { get { return (Local != null && Local.IsOpen) || StarLockBox.Inspecting != null || Telescope.Viewing || PaperNote.Current != null; } }
        // Esc pressed this frame was used by the bag (to close it), so it must not open the pause menu
        public static bool EscapeConsumed { get { return (Local != null && (Local.IsOpen || Local.escFrame == Time.frameCount)) || StarLockBox.BlocksPause || PaperNote.BlocksPause; } }

        public int StackCount { get { return IsSpawned ? items.Count : offlineItems.Count; } }
        public ItemStack StackAt(int i) { return IsSpawned ? items[i] : offlineItems[i]; }

        bool Mine { get { return IsSpawned ? IsOwner : Local == this; } }

        void Awake()
        {
            candles = new NetworkList<float>(new List<float>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            items = new NetworkList<ItemStack>(new List<ItemStack>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public List<float> Snapshot()
        {
            var l = new List<float>();
            for (int i = 0; i < Candles; i++) l.Add(CandleAt(i));
            return l;
        }

        public List<ItemStack> ItemSnapshot()
        {
            var l = new List<ItemStack>();
            for (int i = 0; i < StackCount; i++) l.Add(StackAt(i));
            return l;
        }

        // ---------------- items: reading (anyone) ----------------

        public int CountOf(int id)
        {
            int n = 0;
            for (int i = 0; i < StackCount; i++) { var s = StackAt(i); if (s.id == id) n += s.count; }
            return n;
        }

        public int CountOf(ItemData item) { var db = ItemDatabase.Instance; return db == null || item == null ? 0 : CountOf(db.IndexOf(item)); }
        public bool HasItem(ItemData item) { return item != null && CountOf(item) > 0; }
        public bool HasItem(string itemId) { var db = ItemDatabase.Instance; return db != null && HasItem(db.Find(itemId)); }

        // ---------------- items: changing (host / single player only) ----------------

        public bool AddItem(ItemData item, int count = 1) { var db = ItemDatabase.Instance; return db != null && AddItem(db.IndexOf(item), count); }

        public bool AddItem(int id, int count)
        {
            if (!CanWrite) return false;
            var data = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(id) : null;
            if (data == null || count <= 0) return false;
            var list = ItemSnapshot();
            int max = data.stackable ? Mathf.Max(1, data.maxStack) : 1;
            // how many new slots this needs
            int left = count;
            foreach (var s in list) if (s.id == id && s.count < max) left -= Mathf.Min(left, max - s.count);
            int newSlots = (left + max - 1) / max;
            if (list.Count + newSlots > Capacity) return false;

            left = count;
            for (int i = 0; i < list.Count && left > 0; i++)
            {
                var s = list[i];
                if (s.id != id || s.count >= max) continue;
                int add = Mathf.Min(left, max - s.count);
                s.count += add; left -= add;
                Write(i, s);
            }
            while (left > 0) { int add = Mathf.Min(left, max); Append(new ItemStack(id, add)); left -= add; }
            Changed();
            return true;
        }

        public bool RemoveItem(ItemData item, int count = 1) { var db = ItemDatabase.Instance; return db != null && RemoveItem(db.IndexOf(item), count); }

        public bool RemoveItem(int id, int count)
        {
            if (!CanWrite || count <= 0 || CountOf(id) < count) return false;
            for (int i = StackCount - 1; i >= 0 && count > 0; i--)
            {
                var s = StackAt(i);
                if (s.id != id) continue;
                int take = Mathf.Min(count, s.count);
                s.count -= take; count -= take;
                if (s.count <= 0) RemoveAt(i); else Write(i, s);
            }
            Changed();
            return true;
        }

        bool CanWrite { get { return !IsSpawned || IsServer; } }
        void Write(int i, ItemStack s) { if (IsSpawned) items[i] = s; else offlineItems[i] = s; }
        void Append(ItemStack s) { if (IsSpawned) items.Add(s); else offlineItems.Add(s); }
        void RemoveAt(int i) { if (IsSpawned) items.RemoveAt(i); else offlineItems.RemoveAt(i); }
        void Changed() { if (!IsSpawned) OnItemsChanged(); }

        // ---------------- candles (host / single player only) ----------------

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

        // ---------------- lifetime ----------------

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) Local = this;
            candles.OnListChanged += OnCandlesChanged;
            items.OnListChanged += OnItemsListChanged;
            SnapshotCounts();
        }

        public override void OnNetworkDespawn()
        {
            // a leaving player leaves their items on the floor (the host decides)
            if (IsServer && ItemNetworkSync.Instance != null) ItemNetworkSync.Instance.ServerDropAll(this, transform.position);
            candles.OnListChanged -= OnCandlesChanged;
            items.OnListChanged -= OnItemsListChanged;
            if (Local == this) Local = null;
        }

        void OnCandlesChanged(NetworkListEvent<float> e) { flashUntil = Time.time + 1.2f; }
        void OnItemsListChanged(NetworkListEvent<ItemStack> e) { OnItemsChanged(); }

        void SnapshotCounts()
        {
            lastCounts.Clear();
            for (int i = 0; i < StackCount; i++) { var s = StackAt(i); int c; lastCounts[s.id] = (lastCounts.TryGetValue(s.id, out c) ? c : 0) + s.count; }
        }

        // shows "+ 놋쇠 열쇠" when something new arrives
        void OnItemsChanged()
        {
            flashUntil = Time.time + 1.2f;
            var db = ItemDatabase.Instance;
            var before = new Dictionary<int, int>(lastCounts);
            SnapshotCounts();
            if (db != null)
            {
                foreach (var kv in lastCounts)
                {
                    int had; if (!before.TryGetValue(kv.Key, out had)) had = 0;
                    if (kv.Value <= had) continue;
                    var d = db.Get(kv.Key);
                    if (d == null) continue;
                    toast = "+ " + d.NameOrFallback + (kv.Value - had > 1 ? " ×" + (kv.Value - had) : "");
                    toastUntil = Time.time + 2.5f;
                }
            }
            int total = EntryCount();
            if (selected >= total) selected = Mathf.Max(0, total - 1);
        }

        // ---------------- input ----------------

        // entries shown in the bag: the candle slot (if any) then every item stack
        int EntryCount() { return (Candles > 0 ? 1 : 0) + StackCount; }
        bool EntryIsCandle(int e) { return Candles > 0 && e == 0; }
        int EntryStack(int e) { return e - (Candles > 0 ? 1 : 0); }

        void Update()
        {
            if (!Mine) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (reading != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) escFrame = Time.frameCount;
                if (kb.fKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) reading = null;
                return;
            }

            if (kb.iKey.wasPressedThisFrame)
            {
                if (open) SetOpen(false);
                else if (Cursor.lockState == CursorLockMode.Locked) SetOpen(true);   // not while the pause menu is up
            }
            if (!open) return;
            if (kb.escapeKey.wasPressedThisFrame) { escFrame = Time.frameCount; SetOpen(false); return; }
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            int total = EntryCount();
            if (total == 0) { selected = 0; return; }
            int step = 0;
            if (kb.rightArrowKey.wasPressedThisFrame) step = 1;
            if (kb.leftArrowKey.wasPressedThisFrame) step = -1;
            if (kb.downArrowKey.wasPressedThisFrame) step = slotCount;
            if (kb.upArrowKey.wasPressedThisFrame) step = -slotCount;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) step = -1; else if (wheel < -0.1f) step = 1;
            }
            for (int k = 0; k < 9; k++) if (kb[Key.Digit1 + k].wasPressedThisFrame && k < total) selected = k;
            selected = Mathf.Clamp(selected + step, 0, total - 1);

            if (EntryIsCandle(selected)) return;
            int si = EntryStack(selected);
            if (si < 0 || si >= StackCount) return;
            var data = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(StackAt(si).id) : null;
            if (data == null) return;
            if (kb.fKey.wasPressedThisFrame && data.Readable) reading = data;
            if (kb.gKey.wasPressedThisFrame) Drop(data);
        }

        // opening the bag frees the mouse (and stops mouse-look); closing it captures the mouse again
        void SetOpen(bool value)
        {
            open = value;
            if (!value) reading = null;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        ItemData SelectedData()
        {
            if (EntryIsCandle(selected)) return null;
            int si = EntryStack(selected);
            if (si < 0 || si >= StackCount) return null;
            return ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(StackAt(si).id) : null;
        }

        // mouse on a slot: left click selects, double click reads, right click puts it down
        void ClickEntry(int e, int button, int clicks)
        {
            selected = e;
            var data = SelectedData();
            if (data == null) return;
            if (button == 1) Drop(data);
            else if (clicks >= 2 && data.Readable) reading = data;
            else if (clicks >= 2 && PistolLoader.Local != null && PistolLoader.Local.IsPistol(data)) PistolLoader.Local.SetEquipped(!PistolLoader.Local.Equipped);
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void Drop(ItemData data)
        {
            var sync = ItemNetworkSync.Instance;
            if (sync == null) return;
            var cam = GetComponentInChildren<Camera>();
            Transform view = cam != null ? cam.transform : transform;
            Vector3 fwd = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            Vector3 from = transform.position + Vector3.up * 1.0f;
            Vector3 target = transform.position + fwd * 0.7f;
            // don't drop it through a wall: stop short of anything in front
            RaycastHit wall;
            if (Physics.Raycast(from, fwd, out wall, 0.9f, ~0, QueryTriggerInteraction.Ignore)) target = transform.position + fwd * Mathf.Max(0.15f, wall.distance - 0.25f);
            Vector3 drop = target;
            RaycastHit floor;
            if (Physics.Raycast(target + Vector3.up * 1.2f, Vector3.down, out floor, 3f, ~0, QueryTriggerInteraction.Ignore)) drop = floor.point + Vector3.up * 0.01f;
            string msg = sync.RequestDrop(this, data, drop, Quaternion.LookRotation(fwd, Vector3.up));
            if (!string.IsNullOrEmpty(msg) && PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage(msg);
        }

        // ---------------- GUI ----------------

        void Styles()
        {
            if (titleStyle != null && styleVersion == CoopDemo.MeetYouGuiSkin.Version) return;
            styleVersion = CoopDemo.MeetYouGuiSkin.Version;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = new Color(0.93f, 0.82f, 0.58f);
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleRight };
            hintStyle.normal.textColor = new Color(0.75f, 0.70f, 0.62f);
            countStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            countStyle.normal.textColor = Color.white;
            nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            nameStyle.normal.textColor = new Color(0.95f, 0.90f, 0.80f);
            descStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true };
            descStyle.normal.textColor = new Color(0.80f, 0.76f, 0.70f);
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.85f, 0.80f, 0.70f);
            readStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
            readStyle.normal.textColor = new Color(0.20f, 0.13f, 0.08f);
            toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            toastStyle.normal.textColor = new Color(1f, 0.86f, 0.55f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (!Skinned)
            {
                // Meet You 스킨이 없을 때만 예전 갈색 단색 버튼을 쓴다 (스킨이 있으면 스킨의 버튼 이미지를 그대로 쓴다)
                var bn = Solid(new Color(0.24f, 0.17f, 0.10f, 1f));
                var bh = Solid(new Color(0.42f, 0.30f, 0.15f, 1f));
                buttonStyle.normal.background = bn; buttonStyle.focused.background = bn;
                buttonStyle.hover.background = bh; buttonStyle.active.background = bh;
                var bt = new Color(0.96f, 0.88f, 0.68f);
                buttonStyle.normal.textColor = bt; buttonStyle.hover.textColor = Color.white; buttonStyle.active.textColor = Color.white; buttonStyle.focused.textColor = bt;
                buttonStyle.border = new RectOffset(0, 0, 0, 0);
            }
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

        static bool Skinned
        {
            get { var t = CoopDemo.MeetYouGuiSkin.Theme; return t != null && t.buttonNormal != null; }
        }

        void OnGUI()
        {
            if (!Mine) return;
            var prevSkin = CoopDemo.MeetYouGuiSkin.Begin();
            try { DrawGui(); }
            finally { CoopDemo.MeetYouGuiSkin.End(prevSkin); }
        }

        void DrawGui()
        {
            Styles();
            var v = UiScale.Apply();
            float w = v.x, h = v.y;
            bool paused = Cursor.lockState != CursorLockMode.Locked && !IsOpen;
            var brass = new Color(0.72f, 0.56f, 0.28f, 1f);
            var db = ItemDatabase.Instance;

            if (reading != null && !paused) { DrawReading(w, h); return; }

            // corner hint + pickup toast
            int n = Candles;
            bool flash = Time.time < flashUntil;
            if (!open && !paused)
            {
                int things = 0;
                for (int i = 0; i < StackCount; i++) things += StackAt(i).count;
                var hr = new Rect(16f, h - 46f, 270f, 32f);
                Fill(hr, new Color(0f, 0f, 0f, flash ? 0.7f : 0.45f));
                if (candleIcon != null) GUI.DrawTexture(new Rect(hr.x + 4f, hr.y + 2f, 28f, 28f), candleIcon);
                var s = new GUIStyle(descStyle) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
                if (flash) s.normal.textColor = new Color(1f, 0.85f, 0.5f);
                GUI.Label(new Rect(hr.x + 36f, hr.y, hr.width - 40f, hr.height), "×" + n + (things > 0 ? "   물건 " + things : "") + "    [I] 소지품", s);
            }
            if (Time.time < toastUntil && !string.IsNullOrEmpty(toast) && !paused)
            {
                var tr = new Rect(16f, h - 84f, 320f, 32f);
                Fill(tr, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(new Rect(tr.x + 12f, tr.y, tr.width - 16f, tr.height), toast, toastStyle);
            }
            if (!open || paused) return;

            const float slot = 80f, gap = 10f;
            int cols = slotCount;
            float pw = cols * slot + (cols - 1) * gap + 48f, ph = 2 * slot + gap + 200f;
            var r = new Rect((w - pw) / 2f, h - ph - 50f, pw, ph);
            var theme = CoopDemo.MeetYouGuiSkin.Theme;
            bool skinned = Skinned;
            if (skinned) GUI.Box(r, GUIContent.none, CoopDemo.MeetYouGuiSkin.Window);
            else
            {
                Fill(r, new Color(0.06f, 0.045f, 0.035f, 0.93f));
                Frame(r, brass, 2f);
                Frame(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f), new Color(brass.r, brass.g, brass.b, 0.35f), 1f);
            }
            GUI.Label(new Rect(r.x + 24f, r.y + 12f, 200f, 32f), "소지품", titleStyle);
            GUI.Label(new Rect(r.xMax - 470f, r.y + 12f, 370f, 32f), "클릭 선택 · 더블클릭 읽기 · 우클릭 내려놓기", hintStyle);
            if (GUI.Button(new Rect(r.xMax - 92f, r.y + 14f, 70f, 28f), "닫기", buttonStyle)) { SetOpen(false); return; }

            float sy = r.y + 54f;
            int total = EntryCount();
            var ev = Event.current;
            for (int i = 0; i < cols * 2; i++)
            {
                var sr = SlotRect(r, sy, slot, gap, i, cols);
                bool hover = i < total && sr.Contains(ev.mousePosition);
                if (skinned && theme.slotNormal != null)
                {
                    // 선택된 칸은 아이템 아래에 먼저 깔아야 해서(칸 이미지 안쪽이 불투명) 여기서 같이 그린다
                    var slotTex = (i < total && i == selected && theme.slotSelected != null) ? theme.slotSelected
                                : (hover && theme.slotHover != null) ? theme.slotHover : theme.slotNormal;
                    GUI.DrawTexture(sr, slotTex, ScaleMode.StretchToFill, true);
                }
                else
                {
                    Fill(sr, hover ? new Color(0.22f, 0.16f, 0.10f, 1f) : new Color(0.13f, 0.10f, 0.08f, 1f));
                    Frame(sr, new Color(brass.r, brass.g, brass.b, 0.3f), 1.5f);
                }
                if (hover && ev.type == EventType.MouseDown) { ClickEntry(i, ev.button, ev.clickCount); ev.Use(); }
            }
            total = EntryCount();
            for (int e = 0; e < total && e < cols * 2; e++)
            {
                var sr = SlotRect(r, sy, slot, gap, e, cols);
                if (EntryIsCandle(e)) DrawCandle(sr, slot, n);
                else
                {
                    var st = StackAt(EntryStack(e));
                    var d = db != null ? db.Get(st.id) : null;
                    if (d != null && d.icon != null) GUI.DrawTexture(new Rect(sr.x + 12f, sr.y + 12f, slot - 24f, slot - 24f), d.icon, ScaleMode.ScaleToFit);
                    else GUI.Label(new Rect(sr.x + 4f, sr.y + 4f, slot - 8f, slot - 8f), d != null ? d.NameOrFallback : "?", smallStyle);
                    if (st.count > 1) GUI.Label(new Rect(sr.x, sr.y, sr.width - 6f, sr.height - 2f), "×" + st.count, countStyle);
                }
                if (!(skinned && theme.slotNormal != null))
                    Frame(sr, new Color(brass.r, brass.g, brass.b, e == selected ? 1f : 0.6f), e == selected ? 3f : 1.5f);
            }

            float ty = sy + 2 * slot + gap + 14f;
            var nameR = new Rect(r.x + 24f, ty, pw - 48f, 26f);
            var descR = new Rect(r.x + 24f, ty + 30f, pw - 48f, 80f);
            var keysR = new Rect(r.x + 24f, r.yMax - 38f, pw - 48f, 26f);
            if (total == 0)
            {
                GUI.Label(nameR, "비어 있다", nameStyle);
                GUI.Label(descR, "저택 곳곳에 쓸 만한 물건이 놓여 있다. 보고 E를 누르면 주울 수 있다.", descStyle);
                return;
            }
            selected = Mathf.Clamp(selected, 0, total - 1);
            if (EntryIsCandle(selected))
            {
                GUI.Label(nameR, "양초  " + n + "개", nameStyle);
                GUI.Label(descR, "벽의 양초대에 끼우면 불이 켜진다. [R] 들고 있는 양초와 교체", descStyle);
                return;
            }
            var stack = StackAt(EntryStack(selected));
            var data = db != null ? db.Get(stack.id) : null;
            if (data == null) return;
            GUI.Label(nameR, data.NameOrFallback + (stack.count > 1 ? "  ×" + stack.count : ""), nameStyle);
            GUI.Label(descR, data.description, descStyle);
            float bx = keysR.x;
            if (data.Readable)
            {
                if (GUI.Button(new Rect(bx, keysR.y - 6f, 120f, 34f), "읽기  [F]", buttonStyle)) reading = data;
                bx += 132f;
            }
            var gun = PistolLoader.Local;
            if (gun != null && gun.IsPistol(data))
            {
                bool eq = gun.Equipped;
                if (GUI.Button(new Rect(bx, keysR.y - 6f, 130f, 34f), eq ? "집어넣기" : "손에 들기", buttonStyle)) gun.SetEquipped(!eq);
                bx += 142f;
            }
            if (GUI.Button(new Rect(bx, keysR.y - 6f, 150f, 34f), "내려놓기  [G]", buttonStyle)) Drop(data);
        }

        static Rect SlotRect(Rect r, float sy, float slot, float gap, int i, int cols)
        {
            return new Rect(r.x + 24f + (i % cols) * (slot + gap), sy + (i / cols) * (slot + gap), slot, slot);
        }

        void DrawCandle(Rect sr, float slot, int count)
        {
            float full = Mathf.Max(1f, CandleNetworkSync.BurnSeconds);
            float best = 0f;
            for (int i = 0; i < Candles; i++) best = Mathf.Max(best, CandleAt(i));
            float f = Mathf.Clamp01(best / full);
            // the candle just looks shorter the more it has burnt (no numbers shown)
            float ih = (slot - 14f) * Mathf.Lerp(0.45f, 1f, f);
            if (candleIcon != null) GUI.DrawTexture(new Rect(sr.x + 10f, sr.y + 7f + (slot - 14f - ih), slot - 20f, ih), candleIcon, ScaleMode.StretchToFill);
            if (count > 1) GUI.Label(new Rect(sr.x, sr.y, sr.width - 6f, sr.height - 2f), "×" + count, countStyle);
        }

        void DrawReading(float w, float h)
        {
            float pw = Mathf.Min(620f, w - 80f), ph = Mathf.Min(720f, h - 80f);
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.55f));
            Fill(r, new Color(0.86f, 0.80f, 0.66f, 0.98f));
            Frame(r, new Color(0.45f, 0.33f, 0.18f, 1f), 3f);
            Frame(new Rect(r.x + 8f, r.y + 8f, r.width - 16f, r.height - 16f), new Color(0.55f, 0.42f, 0.25f, 0.6f), 1f);
            // keep the ink dark in every state (the skin's hover/focus colours are white)
            GUI.color = Color.white; GUI.contentColor = Color.white;
            var ink = readStyle.normal.textColor;
            readStyle.hover.textColor = readStyle.active.textColor = readStyle.focused.textColor = ink;
            readStyle.onNormal.textColor = readStyle.onHover.textColor = readStyle.onActive.textColor = readStyle.onFocused.textColor = ink;
            var head = new GUIStyle(readStyle) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(r.x + 30f, r.y + 20f, r.width - 60f, 34f), reading.NameOrFallback, head);
            GUI.Label(new Rect(r.x + 40f, r.y + 70f, r.width - 80f, r.height - 120f), reading.readText, readStyle);
            var foot = new GUIStyle(readStyle) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            if (GUI.Button(new Rect(r.x + (r.width - 140f) / 2f, r.yMax - 50f, 140f, 36f), "닫기  [F]", buttonStyle)) reading = null;
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
