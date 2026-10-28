using System.Collections.Generic;
using LGH;
using UnityEngine;

// 기믹별 소리 (LcwSoundDirector의 나머지 절반). 공통 소리(문/서랍/발소리/엠비언스)는 LcwSoundDirector.cs에 있다.
// 여기도 같은 원칙: 모든 컴퓨터에 똑같이 동기화되는 상태나 연출 신호를 각자 보고 각자 소리를 낸다. LGH/KJH 코드는 건드리지 않는다.
// (소리를 받는 AudioClip 칸이 이미 있는 곳은 칸에 채워 넣었다 — Tools/KSM/Audio/Assign LCW Sounds To Scene)
public partial class LcwSoundDirector
{
    // ---------------------------------------------------------------- 예약 재생 (잠시 뒤에 낼 소리)
    struct Scheduled { public float time; public System.Action action; }
    readonly List<Scheduled> scheduled = new List<Scheduled>();

    void After(float seconds, System.Action action) { scheduled.Add(new Scheduled { time = Time.time + seconds, action = action }); }

    void RunScheduled()
    {
        for (int i = scheduled.Count - 1; i >= 0; i--)
        {
            if (Time.time < scheduled[i].time) continue;
            var a = scheduled[i].action;
            scheduled.RemoveAt(i);
            if (!quiet) a?.Invoke();
        }
    }

    void Sfx(AudioClip clip, Vector3 pos, float vol = 1f, float max = 20f) { if (!quiet) LcwSfx.Play(clip, pos, vol, max); }
    void Sfx2D(AudioClip clip, float vol = 1f) { if (!quiet) LcwSfx.Play2D(clip, vol); }

    // 엠비언스를 낮출 비율 (미로 안에서는 미로 배경음이 대신 깔린다)
    float AmbienceDuck { get { return Mathf.Lerp(1f, 0.15f, mazeBlend); } }

    void OnEnable() { HoldableNetworkSync.ItemTaken += OnItemTaken; }
    void OnDisable() { HoldableNetworkSync.ItemTaken -= OnItemTaken; }

    void UpdateGimmicks()
    {
        RunScheduled();
        UpdateGim01();
        UpdatePickups();
        UpdateGim03();
        UpdateGim04();
        UpdateGim05();
        UpdateGim07();
        UpdateGim08();
    }

    // ---------------------------------------------------------------- GIM-07 안방 가구 — 가구가 한 칸 밀릴 때 두 손잡이에서 각각 (0번=L, 1번=R)
    Gim007.Gim007Controller boundPush;

    void UpdateGim07()
    {
        var c = Gim007.Gim007Controller.Instance;
        if (c == boundPush) return;
        if (boundPush != null) boundPush.EventReceived -= OnPushEvent;
        boundPush = c;
        if (boundPush != null) boundPush.EventReceived += OnPushEvent;
    }

    void OnPushEvent(Gim007.Gim007NetEvent e, int value)
    {
        if (quiet || bank == null || e != Gim007.Gim007NetEvent.Success) return;
        var left = Gim007.Gim007Handle.Find(0);
        var right = Gim007.Gim007Handle.Find(1);
        if (left != null) LcwSfx.Play(bank.moveL, left.transform.position, 0.9f, 16f);
        if (right != null) LcwSfx.Play(bank.moveR, right.transform.position, 0.9f, 16f);
    }

    // ---------------------------------------------------------------- 물건 집기 (공용 들기: 인형/배경카드/화분) — 나와 상대 둘 다 그 물건 위치에서 듣는다
    void OnItemTaken(HoldableItem item, ulong clientId)
    {
        if (quiet || bank == null || item == null) return;
        var stageItem = item as Gim002.Gim002StageItem;
        if (stageItem != null)
        {
            LcwSfx.Play(stageItem.Kind == Gim002.StageSlotKind.Doll ? bank.dollRemove : bank.paperRemove, item.transform.position, 0.9f, 16f);
            return;
        }
        if (item is Gim004.Gim004Pot) LcwSfx.Play(bank.potPickup, item.transform.position, 0.9f, 16f);
    }

    // ---------------------------------------------------------------- GIM-01 초상화
    PortraitTilt[] portraitList;
    int[] portraitSteps;
    bool portraitSolved;

    void UpdateGim01()
    {
        var cfg = PortraitPuzzleScene.Instance;
        if (cfg == null || cfg.portraits == null) return;
        if (portraitList != cfg.portraits)
        {
            portraitList = cfg.portraits;
            portraitSteps = new int[portraitList.Length];
            for (int i = 0; i < portraitList.Length; i++) portraitSteps[i] = portraitList[i] != null ? portraitList[i].Step : 0;
        }
        for (int i = 0; i < portraitList.Length; i++)
        {
            var p = portraitList[i];
            if (p == null || p.Step == portraitSteps[i]) continue;
            portraitSteps[i] = p.Step;
            Sfx(bank.portraitRotate, p.transform.position, 0.9f, 14f);   // 액자가 한 칸 돌아가는 순간
        }

        var puzzle = PortraitPuzzle.Instance;
        if (puzzle == null) return;
        if (puzzle.Solved && !portraitSolved)
        {
            portraitSolved = true;
            // 퍼즐이 풀려 정답 액자들이 모두 그 각도로 고정되는 소리 (조금씩 어긋나게)
            int locked = cfg.answer != null ? Mathf.Min(cfg.answer.Length, portraitList.Length) : 0;
            for (int i = 0; i < locked; i++)
            {
                if (portraitList[i] == null) continue;
                Vector3 pp = portraitList[i].transform.position;
                After(0.12f * i, () => LcwSfx.Play(bank.portraitLock, pp, 0.9f, 14f));
            }
            // 액자가 떨어지고(dropDelay) 열쇠 상자 뚜껑이 열리면서 열쇠가 드러나는 때에 맞춘다
            Vector3 at = cfg.keyBox != null ? cfg.keyBox.transform.position : Vector3.zero;
            if (cfg.keyBox != null) After(Mathf.Max(0f, cfg.dropDelay) + 0.3f, () => LcwSfx.Play(bank.keyBehindFrame, at, 1f, 18f));
        }
        else if (!puzzle.Solved) portraitSolved = false;
    }

    // ---------------------------------------------------------------- 인벤토리에 물건이 들어올 때 (편지류는 종이 소리, 나머지는 작은 금속 소리)
    readonly Dictionary<LGH.PlayerInventory, Dictionary<int, int>> invCounts = new Dictionary<LGH.PlayerInventory, Dictionary<int, int>>();
    PaperNote lastNote;

    void UpdatePickups()
    {
        var db = ItemDatabase.Instance;
        for (int i = 0; i < LGH.PlayerInventory.All.Count; i++)
        {
            var inv = LGH.PlayerInventory.All[i];
            if (inv == null) continue;
            var now = new Dictionary<int, int>();
            foreach (var s in inv.ItemSnapshot()) now[s.id] = (now.ContainsKey(s.id) ? now[s.id] : 0) + s.count;
            if (!invCounts.TryGetValue(inv, out var prev)) { invCounts[inv] = now; continue; }
            invCounts[inv] = now;
            if (db == null) continue;
            foreach (var kv in now)
            {
                int before = prev.ContainsKey(kv.Key) ? prev[kv.Key] : 0;
                if (kv.Value <= before) continue;
                var data = db.Get(kv.Key);
                bool paper = data != null && data.Readable;
                Sfx(paper ? bank.paperPickup : bank.smallMetalPickup, inv.transform.position + Vector3.up, 0.9f, 14f);
            }
        }

        // 쪽지를 집어 들어 읽기 시작할 때
        var note = PaperNote.Current;
        if (note != null && note != lastNote) Sfx2D(bank.paperPickup, 0.9f);
        lastNote = note;
    }

    // ---------------------------------------------------------------- GIM-03 약품 제조 — 장치 상태가 바뀌는 순간을 본다
    readonly Dictionary<PotionCraft.PotionStation, int[]> potionStates = new Dictionary<PotionCraft.PotionStation, int[]>();
    PotionCraft.PotionStation[] potionStations = new PotionCraft.PotionStation[0];
    float nextPotionScan;
    readonly Dictionary<ulong, int> heldCodes = new Dictionary<ulong, int>();
    readonly Dictionary<Dumbwaiter, bool> dumbMoving = new Dictionary<Dumbwaiter, bool>();
    Dumbwaiter[] dumbwaiters = new Dumbwaiter[0];

    void UpdateGim03()
    {
        if (Time.time >= nextPotionScan)
        {
            nextPotionScan = Time.time + 3f;
            potionStations = FindObjectsByType<PotionCraft.PotionStation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            dumbwaiters = FindObjectsByType<Dumbwaiter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        foreach (var st in potionStations)
        {
            if (st == null) continue;
            int[] now = st.GetState();
            if (!potionStates.TryGetValue(st, out var prev) || prev.Length != now.Length) { potionStates[st] = now; continue; }
            potionStates[st] = now;
            if (quiet) continue;
            Vector3 pos = st.transform.position;

            if (st is PotionCraft.BrewerStation)
            {
                // 상태: 재료, 물, 단계, 결과 — 재료 칸이 차면 약초 놓는 소리, 물 칸이 차면 병 내려놓는 소리
                if (prev[0] < 0 && now[0] >= 0) LcwSfx.Play(bank.herbPlace, pos, 0.9f, 14f);
                if (prev[1] < 0 && now[1] >= 0) LcwSfx.Play(bank.bottlePutDown, pos, 0.9f, 14f);
                // 재료와 물이 다 차서 제조(조합)가 시작되는 순간 (단계: 0 대기, 1 제조 중, 2 완료)
                if (prev[2] != 1 && now[2] == 1) After(0.35f, () => LcwSfx.Play(bank.combine, pos, 0.9f, 14f));
            }
            else if (st is PotionCraft.GrinderStation)
            {
                // 상태: 단계, 색, 진행 횟수 — 빻을 때마다 한 번
                if (now[2] > prev[2]) LcwSfx.Play(bank.grind, pos, 0.9f, 14f);
            }
            else if (st is PotionCraft.MixerStation)
            {
                // 상태: 쌓인 개수 ... — 가루가 들어갈 때마다 한 번
                if (now[0] > prev[0]) LcwSfx.Play(bank.powderPour, pos, 0.9f, 14f);
            }
        }

        // 손에 든 것이 빈손 → 물병이 되는 순간 (물병 들기). 모든 플레이어 대상.
        var sync = PotionCraft.PotionCraftSync.Instance;
        if (sync != null)
        {
            for (int i = 0; i < NetworkPlayer.All.Count; i++)
            {
                var p = NetworkPlayer.All[i];
                if (p == null || !p.IsSpawned) continue;
                ulong id = p.OwnerClientId;
                int code = sync.GetHeld(id);
                if (heldCodes.TryGetValue(id, out int before) && before == 0 && code != 0 && PotionCraft.Item.KindOf(code) == PotionCraft.ItemKind.Water)
                    Sfx(bank.bottlePickup, p.transform.position + Vector3.up, 0.9f, 14f);
                heldCodes[id] = code;
            }
        }

        // 도르래(덤웨이터)가 움직이기 시작할 때 (올라갈 때와 내려갈 때 모두)
        foreach (var d in dumbwaiters)
        {
            if (d == null) continue;
            bool moving = d.IsMoving;
            if (!dumbMoving.TryGetValue(d, out bool was)) { dumbMoving[d] = moving; continue; }
            dumbMoving[d] = moving;
            if (moving && !was) Sfx(bank.dumbwaiterUp, d.transform.position, 1f, 22f);
        }
    }

    // ---------------------------------------------------------------- GIM-04 온실 화분 — 선반 누름판이 눌리고 풀리는 순간
    readonly Dictionary<Gim004.Gim004ShelfSlot, bool> plateStates = new Dictionary<Gim004.Gim004ShelfSlot, bool>();
    Gim004.Gim004ShelfSlot[] shelfSlots = new Gim004.Gim004ShelfSlot[0];
    float nextShelfScan;

    void UpdateGim04()
    {
        if (Time.time >= nextShelfScan)
        {
            nextShelfScan = Time.time + 3f;
            shelfSlots = FindObjectsByType<Gim004.Gim004ShelfSlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }
        foreach (var s in shelfSlots)
        {
            if (s == null) continue;
            bool pressed = s.Pressed;
            if (!plateStates.TryGetValue(s, out bool prev)) { plateStates[s] = pressed; continue; }
            plateStates[s] = pressed;
            if (quiet || prev == pressed) continue;
            LcwSfx.Play(pressed ? bank.platePressDown : bank.platePressUp, s.Anchor.position, 0.9f, 14f);
        }
    }

    // ---------------------------------------------------------------- GIM-05 정원 미로
    Gim005.Gim005Controller boundMaze;
    Gim005.Gim005MazeArea mazeArea;
    AudioSource mazeSource;
    float mazeBlend;

    void UpdateGim05()
    {
        var c = Gim005.Gim005Controller.Instance;
        if (c != boundMaze)
        {
            if (boundMaze != null) boundMaze.EventReceived -= OnMazeEvent;
            boundMaze = c;
            if (boundMaze != null) boundMaze.EventReceived += OnMazeEvent;
        }
        if (mazeArea == null) mazeArea = FindFirstObjectByType<Gim005.Gim005MazeArea>();

        // 미로 안에 있는 동안은 엠비언스를 낮추고 미로 배경음을 깐다
        bool inside = !quiet && mazeArea != null && mazeArea.LocalPlayerInside();
        mazeBlend = Mathf.MoveTowards(mazeBlend, inside ? 1f : 0f, Time.unscaledDeltaTime * 0.7f);
        if (mazeSource == null)
        {
            mazeSource = gameObject.AddComponent<AudioSource>();
            mazeSource.playOnAwake = false; mazeSource.loop = true; mazeSource.spatialBlend = 0f; mazeSource.volume = 0f;
        }
        if (mazeSource.clip == null) mazeSource.clip = bank.mazeBgm;
        mazeSource.volume = 0.5f * mazeBlend;
        if (mazeBlend > 0.001f && !mazeSource.isPlaying) mazeSource.Play();
        else if (mazeBlend <= 0.001f && mazeSource.isPlaying) mazeSource.Pause();
    }

    void OnMazeEvent(Gim005.Gim005NetEvent e, int value)
    {
        if (quiet || bank == null) return;
        switch (e)
        {
            case Gim005.Gim005NetEvent.TimeOut:
                // 시간 초과 → 정신을 잃고 시작점으로 (미로 안에 있던 사람에게만 들린다), 덤불은 그 뒤에 다시 자란다
                if (mazeArea != null && mazeArea.LocalPlayerInside()) LcwSfx.Play2D(bank.faint, 1f);
                if (mazeArea != null) { var at = mazeArea.transform.position; After(1.2f, () => LcwSfx.Play(bank.hedgeRegrow, at, 1f, 30f)); }
                break;
            case Gim005.Gim005NetEvent.AttemptReset:
                if (mazeArea != null) LcwSfx.Play(bank.hedgeRegrow, mazeArea.transform.position, 1f, 30f);
                break;
            case Gim005.Gim005NetEvent.Complete:
                // 중앙 버튼을 누른 뒤 계단 앞 철창이 올라간다
                var button = FindFirstObjectByType<Gim005.Gim005CenterButton>();
                if (button != null) LcwSfx.Play(bank.mazeEndButton, button.transform.position, 1f, 22f);
                Vector3 rail = RailPosition();
                After(0.7f, () => LcwSfx.Play(bank.barsRise, rail, 1f, 26f));
                break;
        }
    }

    Vector3 railCached; bool railFound;
    Vector3 RailPosition()
    {
        if (!railFound)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "Rail_StairFront" || !t.root.name.Contains("Ravenwood")) continue;
                // 이 오브젝트 자체의 원점은 건물 원점이라서, 실제 난간 모양들의 중심을 소리 위치로 쓴다 (꺼진 뒤에도 계산된다)
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    railCached = b.center;
                }
                else railCached = t.position;
                railFound = true;
                break;
            }
        }
        return railFound ? railCached : Vector3.zero;
    }

    // ---------------------------------------------------------------- GIM-08 다락 상자와 망원경
    readonly Dictionary<StarLockBox, bool> boxStates = new Dictionary<StarLockBox, bool>();
    StarLockBox[] boxes = new StarLockBox[0];
    float nextBoxScan;
    bool wasViewingTelescope;

    void UpdateGim08()
    {
        if (Time.time >= nextBoxScan)
        {
            nextBoxScan = Time.time + 3f;
            boxes = FindObjectsByType<StarLockBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }
        foreach (var b in boxes)
        {
            if (b == null) continue;
            bool open = b.IsOpen;
            if (!boxStates.TryGetValue(b, out bool prev)) { boxStates[b] = open; continue; }
            boxStates[b] = open;
            if (!quiet && open && !prev) LcwSfx.Play(bank.treasureOpen, b.transform.position, 1f, 22f);
        }

        // 망원경을 들여다보는 순간, 그리고 눈을 떼는 순간 한 번씩
        bool viewing = Telescope.Viewing;
        if (viewing != wasViewingTelescope) Sfx2D(bank.telescope, 0.9f);
        wasViewingTelescope = viewing;
    }
}
