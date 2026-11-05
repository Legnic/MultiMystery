using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // One per game (created automatically). Plays the sounds that are not tied to a single LGH script:
    //  - BGM: lobby music before the game, ambience while playing
    //  - footsteps of every player, candle crackle near lit candles
    //  - teammate gimmicks (GIM-01..07): it only WATCHES their public state and plays the LCW sounds,
    //    their scripts are not changed.
    public class LghSoundDirector : MonoBehaviour
    {
        static LghSoundDirector instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null || LghSoundBank.Get == null) return;
            var go = new GameObject("LGH_SoundDirector");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LghSoundDirector>();
        }

        LghSoundBank bank;
        AudioSource bgmLobby, bgmGame, mazeLoop;
        float rescanAt;

        void Awake()
        {
            bank = LghSoundBank.Get;
            bgmLobby = MakeLoop("BGM_Lobby", bank.bgmLobby);
            bgmGame = MakeLoop("BGM_Game", bank.bgmGame);
            mazeLoop = MakeLoop("Maze_Ambient", bank.mazeAmbient);
        }

        void OnDestroy() { if (instance == this) instance = null; }

        AudioSource MakeLoop(string n, AudioClip clip)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        static void Fade(AudioSource s, float target, float speed)
        {
            if (s == null || s.clip == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, speed * Time.unscaledDeltaTime);
            if (s.volume > 0.001f && !s.isPlaying) s.Play();
            else if (s.volume <= 0.001f && s.isPlaying) s.Pause();
        }

        void Update()
        {
            if (bank == null) return;
            if (Time.unscaledTime >= rescanAt) { rescanAt = Time.unscaledTime + 3f; Rescan(); }
            UpdateBgm();
            UpdateFootsteps();
            UpdateCandles();
            UpdatePortraits();
            UpdateStage();
            UpdatePotion();
            UpdateConservatory();
            UpdateMaze();
            UpdatePush();
        }

        // ------------------------------------------------------------ BGM
        void UpdateBgm()
        {
            int mode = 1;   // 1 lobby, 2 game, 0 silent
            var flow = CoopDemo.GameFlowSync.Instance;
            if (flow != null && flow.IsSpawned)
            {
                switch (flow.Stage)
                {
                    case CoopDemo.GameStage.Lobby: mode = 1; break;
                    case CoopDemo.GameStage.Intro:
                    case CoopDemo.GameStage.Playing: mode = 2; break;
                    case CoopDemo.GameStage.Ending: mode = 0; break;
                    default: mode = 1; break;
                }
            }
            else if (PlayerInteractor.Active != null) mode = 2;   // playing without the lobby (test / single player)

            Fade(bgmLobby, mode == 1 ? bank.bgmLobbyVolume : 0f, 0.4f);
            Fade(bgmGame, mode == 2 ? bank.bgmGameVolume : 0f, 0.4f);
        }

        // ------------------------------------------------------------ footsteps
        class Walker { public Vector3 last; public float travelled; public bool init; }
        readonly Dictionary<FirstPersonController, Walker> walkers = new Dictionary<FirstPersonController, Walker>();
        FirstPersonController[] fpcs = new FirstPersonController[0];

        void UpdateFootsteps()
        {
            if (bank.footstep == null) return;
            // 발소리는 KSM의 LcwSoundDirector가 나와 상대 모두 맡는다(접지 검사 포함, 걸음 간격 1.3m).
            // 여기서도 내면 같은 소리가 겹쳐 두 번 울리고, 이쪽은 접지 검사가 없어서 점프 중에도 났다(2026-11-05).
            if (LcwSoundDirector.Instance != null) return;
            foreach (var f in fpcs)
            {
                if (f == null || !f.gameObject.activeInHierarchy) continue;
                Walker w;
                if (!walkers.TryGetValue(f, out w)) { w = new Walker(); walkers[f] = w; }
                Vector3 p = f.transform.position;
                if (!w.init) { w.last = p; w.init = true; continue; }
                Vector3 d = p - w.last;
                w.last = p;
                float flat = new Vector2(d.x, d.z).magnitude;
                if (flat > 1.5f || f.Climbing != null) { w.travelled = 0f; continue; }   // teleport / ladder
                if (flat < 0.0005f || Mathf.Abs(d.y) > flat + 0.02f) continue;
                w.travelled += flat;
                if (w.travelled < 0.62f) continue;
                w.travelled = 0f;
                bool mine = f.enabled;   // only the local player's controller is enabled
                float vol = mine ? (f.IsCrouching ? 0.12f : 0.28f) : 0.45f;
                LghSfx.At(bank.footstep, p + Vector3.up * 0.05f, vol, 12f, Random.Range(0.9f, 1.1f));
            }
        }

        // ------------------------------------------------------------ candles
        readonly Dictionary<Object, AudioSource> candleLoops = new Dictionary<Object, AudioSource>();
        CandleSconce[] sconces = new CandleSconce[0];
        HeldCandle[] heldCandles = new HeldCandle[0];

        AudioSource CandleSource(Object key, Transform at, float vol)
        {
            AudioSource s;
            if (candleLoops.TryGetValue(key, out s) && s != null) return s;
            var go = new GameObject("CandleLoop");
            go.transform.SetParent(at, false);
            s = go.AddComponent<AudioSource>();
            LghSfx.Setup3D(s, 3.5f);
            s.minDistance = 0.4f;
            s.clip = bank.candleLoop;
            s.loop = true;
            s.volume = vol;
            candleLoops[key] = s;
            return s;
        }

        void UpdateCandles()
        {
            if (bank.candleLoop == null) return;
            foreach (var c in sconces)
            {
                if (c == null) continue;
                var s = CandleSource(c, c.flameRoot != null ? c.flameRoot : c.transform, 0.18f);
                SetLoop(s, c.IsLit);
            }
            foreach (var h in heldCandles)
            {
                if (h == null) continue;
                var s = CandleSource(h, h.flameRoot != null ? h.flameRoot : h.transform, 0.12f);
                SetLoop(s, h.Holding && h.IsBurning);
            }
        }

        static void SetLoop(AudioSource s, bool on)
        {
            if (on && !s.isPlaying) { s.time = Random.Range(0f, Mathf.Max(0f, s.clip.length - 0.1f)); s.Play(); }
            else if (!on && s.isPlaying) s.Stop();
        }

        // ------------------------------------------------------------ GIM-01 portraits
        class Tilt { public int step; public Quaternion rot; public bool check, waitStop; public float since; }
        readonly Dictionary<PortraitTilt, Tilt> tilts = new Dictionary<PortraitTilt, Tilt>();
        PortraitTilt[] portraits = new PortraitTilt[0];
        KeyBoxLid[] lids = new KeyBoxLid[0];
        readonly Dictionary<KeyBoxLid, bool> lidOpen = new Dictionary<KeyBoxLid, bool>();

        void UpdatePortraits()
        {
            foreach (var p in portraits)
            {
                if (p == null) continue;
                Tilt t;
                if (!tilts.TryGetValue(p, out t)) { tilts[p] = new Tilt { step = p.Step, rot = p.transform.localRotation }; continue; }
                bool moving = Quaternion.Angle(t.rot, p.transform.localRotation) > 0.01f;
                t.rot = p.transform.localRotation;
                if (t.check)
                {
                    t.check = false;
                    if (moving) { LghSfx.At(bank.portraitTurn, p.transform.position, 0.8f, 12f); t.waitStop = true; t.since = Time.time; }
                }
                if (p.Step != t.step) { t.step = p.Step; t.check = true; }   // decide next frame: instant (late join) changes do not move
                if (t.waitStop && !moving && Time.time - t.since > 0.1f)
                {
                    t.waitStop = false;
                    LghSfx.At(bank.portraitLock, p.transform.position, 0.8f, 12f);
                }
            }
            foreach (var l in lids)
            {
                if (l == null) continue;
                bool was;
                if (!lidOpen.TryGetValue(l, out was)) { lidOpen[l] = l.IsOpen; continue; }
                if (l.IsOpen && !was) LghSfx.At(bank.portraitKeyReveal, l.transform.position, 0.9f, 14f);
                lidOpen[l] = l.IsOpen;
            }
        }

        // ------------------------------------------------------------ GIM-02 puppet stage (taking a doll / backdrop out)
        int lastDoll = int.MinValue, lastBg = int.MinValue;

        void UpdateStage()
        {
            var c = Gim002.Gim002Controller.Instance;
            if (c == null) return;
            int doll = c.State.slotDoll, bg = c.State.slotBg;
            if (lastDoll != int.MinValue)
            {
                if (lastDoll >= 0 && doll < 0) PlayAtSlot(Gim002.StageSlotKind.Doll, bank.dollPull);
                if (lastBg >= 0 && bg < 0) PlayAtSlot(Gim002.StageSlotKind.Background, bank.paperPull);
            }
            lastDoll = doll; lastBg = bg;
        }

        static void PlayAtSlot(Gim002.StageSlotKind k, AudioClip clip)
        {
            var s = Gim002.Gim002StageSlot.Find(k);
            if (s != null) LghSfx.At(clip, s.Anchor.position, 0.8f, 12f);
        }

        // ------------------------------------------------------------ GIM-03 potion craft
        PotionCraft.PotionStation[] stations = new PotionCraft.PotionStation[0];
        readonly Dictionary<PotionCraft.PotionStation, int[]> stationState = new Dictionary<PotionCraft.PotionStation, int[]>();
        readonly Dictionary<PotionCraft.PotionStation, AudioSource> grindSources = new Dictionary<PotionCraft.PotionStation, AudioSource>();
        readonly Dictionary<ulong, int> heldCode = new Dictionary<ulong, int>();

        void UpdatePotion()
        {
            foreach (var st in stations)
            {
                if (st == null) continue;
                int[] now = st.GetState();
                int[] before;
                stationState.TryGetValue(st, out before);
                stationState[st] = (int[])now.Clone();
                if (before == null || before.Length != now.Length) continue;
                Vector3 p = st.transform.position + Vector3.up * 0.3f;

                if (st is PotionCraft.BrewerStation && now.Length >= 4)
                {
                    if (before[0] < 0 && now[0] >= 0) LghSfx.At(bank.herbPlace, p, 0.8f, 10f);
                    if (before[1] < 0 && now[1] >= 0) LghSfx.At(bank.bottleDown, p, 0.8f, 10f);
                }
                else if (st is PotionCraft.GrinderStation && now.Length >= 3)
                {
                    if (before[0] == 0 && now[0] == 1) LghSfx.At(bank.herbPlace, p, 0.8f, 10f);
                    if (now[2] > before[2] && bank.grind != null)
                    {
                        AudioSource g;
                        if (!grindSources.TryGetValue(st, out g) || g == null)
                        {
                            var go = new GameObject("GrindSound");
                            go.transform.SetParent(st.transform, false);
                            go.transform.localPosition = Vector3.up * 0.3f;
                            g = go.AddComponent<AudioSource>();
                            LghSfx.Setup3D(g, 10f);
                            g.clip = bank.grind;
                            g.volume = 0.8f;
                            grindSources[st] = g;
                        }
                        if (!g.isPlaying) g.Play();
                    }
                }
                else if (st is PotionCraft.MixerStation && now.Length >= 1)
                {
                    if (now[0] > before[0]) LghSfx.At(bank.powderPour, p, 0.8f, 10f);
                }
            }

            // picking up a water bottle from the shelf
            var sync = PotionCraft.PotionCraftSync.Instance;
            if (sync == null) return;
            if (NetworkPlayer.All.Count == 0) WatchHeld(sync, 0UL, PlayerInteractor.Active != null ? PlayerInteractor.Active.transform : null);
            foreach (var np in NetworkPlayer.All) if (np != null && np.IsSpawned) WatchHeld(sync, np.OwnerClientId, np.transform);
        }

        void WatchHeld(PotionCraft.PotionCraftSync sync, ulong id, Transform who)
        {
            int now = sync.GetHeld(id), before;
            if (heldCode.TryGetValue(id, out before) && before == 0 && now != 0 && who != null
                && PotionCraft.Item.KindOf(now) == PotionCraft.ItemKind.Water)
                LghSfx.At(bank.bottleUp, who.position + Vector3.up * 1.2f, 0.7f, 10f);
            heldCode[id] = now;
        }

        // ------------------------------------------------------------ GIM-04 conservatory pots
        Gim004.Gim004ShelfSlot[] shelfSlots = new Gim004.Gim004ShelfSlot[0];
        readonly Dictionary<Gim004.Gim004ShelfSlot, bool> slotPressed = new Dictionary<Gim004.Gim004ShelfSlot, bool>();
        Gim004.Gim004Pot[] pots = new Gim004.Gim004Pot[0];
        readonly Dictionary<Gim004.Gim004Pot, HoldableItem.Where> potWhere = new Dictionary<Gim004.Gim004Pot, HoldableItem.Where>();

        void UpdateConservatory()
        {
            foreach (var s in shelfSlots)
            {
                if (s == null) continue;
                bool now = s.Pressed, was;
                if (slotPressed.TryGetValue(s, out was) && now != was)
                    LghSfx.At(now ? bank.plateDown : bank.plateUp, s.Anchor.position, 0.7f, 10f);
                slotPressed[s] = now;
            }
            foreach (var pot in pots)
            {
                if (pot == null) continue;
                var now = pot.Location;
                HoldableItem.Where was;
                if (potWhere.TryGetValue(pot, out was) && now == HoldableItem.Where.Held && was != HoldableItem.Where.Held)
                    LghSfx.At(bank.potLift, pot.transform.position, 0.8f, 10f);
                potWhere[pot] = now;
            }
        }

        // ------------------------------------------------------------ GIM-05 garden maze
        Gim005.Gim005Controller mazeHooked;
        Gim005.Gim005MazeArea mazeArea;
        Gim005.Gim005CenterButton centerButton;
        Gim005.Gim005Gate[] gates = new Gim005.Gim005Gate[0];
        readonly Dictionary<Gim005.Gim005Gate, bool> gateOpen = new Dictionary<Gim005.Gim005Gate, bool>();
        float regrowCooldown;

        void UpdateMaze()
        {
            var c = Gim005.Gim005Controller.Instance;
            if (c != mazeHooked)
            {
                if (mazeHooked != null) mazeHooked.EventReceived -= OnMazeEvent;
                mazeHooked = c;
                if (c != null) c.EventReceived += OnMazeEvent;
            }
            bool inside = c != null && mazeArea != null && mazeArea.LocalPlayerInside() && !c.IsComplete;
            Fade(mazeLoop, inside ? 0.5f : 0f, 0.5f);

            foreach (var g in gates)
            {
                if (g == null) continue;
                bool now = g.IsOpen, was;
                if (gateOpen.TryGetValue(g, out was) && was && !now && Time.time >= regrowCooldown)
                {
                    regrowCooldown = Time.time + 1f;   // all bushes grow back together: one sound
                    LghSfx.At(bank.bushRegrow, g.transform.position, 0.9f, 30f);
                }
                gateOpen[g] = now;
            }
        }

        void OnMazeEvent(Gim005.Gim005NetEvent e, int value)
        {
            bool inside = mazeArea != null && mazeArea.LocalPlayerInside();
            switch (e)
            {
                case Gim005.Gim005NetEvent.TimeOut:
                case Gim005.Gim005NetEvent.AttemptReset:
                    if (inside) LghSfx.Local(bank.faint, 0.8f);
                    break;
                case Gim005.Gim005NetEvent.Complete:
                    if (centerButton != null) LghSfx.At(bank.mazeEndButton, centerButton.transform.position, 0.9f, 25f);
                    break;
            }
        }

        // ------------------------------------------------------------ GIM-07 pushing furniture (key minigame)
        void UpdatePush()
        {
            var c = Gim007.Gim007Controller.Instance;
            var kb = Keyboard.current;
            if (c == null || kb == null || c.IsComplete || c.LocalHandle < 0) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame
                || kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                LghSfx.Local(bank.minigamePress, 0.6f);
        }

        // ------------------------------------------------------------ scene lookup (scenes and players change)
        void Rescan()
        {
            fpcs = FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude);
            sconces = FindObjectsByType<CandleSconce>(FindObjectsInactive.Exclude);
            heldCandles = FindObjectsByType<HeldCandle>(FindObjectsInactive.Exclude);
            portraits = FindObjectsByType<PortraitTilt>(FindObjectsInactive.Exclude);
            lids = FindObjectsByType<KeyBoxLid>(FindObjectsInactive.Exclude);
            stations = FindObjectsByType<PotionCraft.PotionStation>(FindObjectsInactive.Exclude);
            shelfSlots = FindObjectsByType<Gim004.Gim004ShelfSlot>(FindObjectsInactive.Exclude);
            pots = FindObjectsByType<Gim004.Gim004Pot>(FindObjectsInactive.Exclude);
            gates = FindObjectsByType<Gim005.Gim005Gate>(FindObjectsInactive.Exclude);
            mazeArea = FindAnyObjectByType<Gim005.Gim005MazeArea>();
            centerButton = FindAnyObjectByType<Gim005.Gim005CenterButton>();
        }
    }
}
