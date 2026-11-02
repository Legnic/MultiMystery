using System.Collections.Generic;
using LGH;
using UnityEngine;

// 게임 속 "상황"을 지켜보다가 그 상황에 맞는 LCW 사운드를 내는 지휘자. 이 컴퓨터에서만 돌고, 네트워크 오브젝트가 아니다(씬/프리팹에 둘 필요 없음 —
// 게임이 시작될 때 스스로 하나 만든다).
//
// 소리를 내는 원칙
//  - 상대 컴퓨터의 소리는 상대 컴퓨터가 낸다. 문이 열렸다/잠금이 풀렸다 같은 "모두에게 똑같이 동기화되는 상태"가 바뀌는 순간을 각자 보고 각자 소리를 낸다
//    (그래서 한 명이 문을 열면 두 명 다 그 문 위치에서 소리를 듣는다).
//  - 게임이 "플레이 중"이 아닐 때(타이틀/대기실/도입부/엔딩)나, 플레이 시작·접속 직후 1.5초 동안은 소리를 내지 않고 지금 상태만 기억한다
//    (방 초기화나 늦게 접속하면서 상태가 한꺼번에 맞춰질 때 문 소리가 와르르 나지 않게).
//  - 기믹마다 이미 있는 연출 신호(EventReceived 등)는 LGH/KJH 코드를 고치지 않고 여기서 구독한다. 소리를 받는 칸(AudioClip)이 있는 곳만 그 칸에 채워 넣었다.
public partial class LcwSoundDirector : MonoBehaviour
{
    public static LcwSoundDirector Instance { get; private set; }

    [Header("음량 (0~1)")]
    [Range(0f, 1f)] public float ambienceVolume = 0.35f;
    [Range(0f, 1f)] public float doorVolume = 1f;
    [Range(0f, 1f)] public float drawerVolume = 0.9f;
    [Range(0f, 1f)] public float footstepVolume = 0.8f;
    [Tooltip("내 발소리는 거리 감쇠 없이 재생한다. 상대 발소리는 footstepVolume을 사용한다.")]
    [Range(0f, 1f)] public float localFootstepVolume = 1f;

    [Range(0f, 1f)] public float jumpLandingVolume = 1f;

    [Header("발소리")]
    [Tooltip("걸을 때 한 걸음 사이 거리(m)")] public float walkStride = 1.3f;
    [Tooltip("웅크릴 때 한 걸음 사이 거리(m)")] public float crouchStride = 1.0f;

    LcwSoundBank bank;
    bool wasActive;
    float quietUntil;
    bool quiet;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Instance != null) return;
        var go = new GameObject("LcwSoundDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<LcwSoundDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitAmbience();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // 지금 효과음을 내도 되는 단계인가 (게임 흐름 시스템이 없는 테스트 씬이면 항상 true)
    static bool GameActive
    {
        get
        {
            var gf = CoopDemo.GameFlowSync.Instance;
            if (gf == null || !gf.IsSpawned) return NetworkPlayerPresent();
            return gf.Stage == CoopDemo.GameStage.Playing;
        }
    }

    static bool NetworkPlayerPresent() { return NetworkPlayer.Local != null; }

    void Update()
    {
        bank = LcwSfx.Bank;
        if (bank == null) return;

        bool active = GameActive;
        if (active && !wasActive) quietUntil = Time.time + 1.5f;
        wasActive = active;
        quiet = !active || Time.time < quietUntil;

        UpdateAmbience(active);
        UpdateLobbyBgm();
        UpdateDoors();
        UpdateDrawers();
        UpdateFootsteps();
        UpdateGimmicks();
    }

    // ---------------------------------------------------------------- 엠비언스 (플레이 중 계속 깔리는 소리)
    AudioSource ambienceSource;
    float ambienceTarget;

    void InitAmbience()
    {
        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.spatialBlend = 0f;
        ambienceSource.volume = 0f;
    }

    void UpdateAmbience(bool active)
    {
        if (ambienceSource == null) return;
        if (ambienceSource.clip == null && bank.ambience != null) ambienceSource.clip = bank.ambience;
        ambienceTarget = active ? ambienceVolume * AmbienceDuck : 0f;
        ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambienceTarget, Time.unscaledDeltaTime * 0.6f);   // 약 0.6초에 1.0 → 천천히 페이드
        if (ambienceSource.volume > 0.001f && !ambienceSource.isPlaying) ambienceSource.Play();
        else if (ambienceSource.volume <= 0.001f && ambienceTarget <= 0.001f && ambienceSource.isPlaying) ambienceSource.Pause();
    }

    // ---------------------------------------------------------------- 대기실 브금 (타이틀 화면 + 대기실에서 이어서 계속, 도입부가 시작되면 사라진다)
    [Range(0f, 1f)] public float lobbyBgmVolume = 0.5f;
    AudioSource lobbyBgmSource;

    void UpdateLobbyBgm()
    {
        if (lobbyBgmSource == null)
        {
            lobbyBgmSource = gameObject.AddComponent<AudioSource>();
            lobbyBgmSource.playOnAwake = false; lobbyBgmSource.loop = true; lobbyBgmSource.spatialBlend = 0f; lobbyBgmSource.volume = 0f;
        }
        if (lobbyBgmSource.clip == null) lobbyBgmSource.clip = bank.lobbyBgm;

        // 타이틀/옵션/방 목록(아직 방에 들어가기 전)과 대기실은 같은 한 곡이 끊기지 않고 이어진다. 도입부·플레이·엔딩·결과에서는 페이드 아웃.
        // 소리 원본(AudioSource)을 하나만 쓰고 끌 때는 정지가 아니라 일시정지라서, 다시 타이틀로 돌아오면 그 자리에서 이어진다.
        var gf = CoopDemo.GameFlowSync.Instance;
        bool wanted = gf != null && (!gf.IsSpawned || gf.Stage == CoopDemo.GameStage.Lobby);
        float target = wanted ? lobbyBgmVolume : 0f;
        lobbyBgmSource.volume = Mathf.MoveTowards(lobbyBgmSource.volume, target, Time.unscaledDeltaTime * 0.8f);
        if (lobbyBgmSource.volume > 0.001f && !lobbyBgmSource.isPlaying) lobbyBgmSource.UnPause();
        if (lobbyBgmSource.volume > 0.001f && !lobbyBgmSource.isPlaying) lobbyBgmSource.Play();
        else if (lobbyBgmSource.volume <= 0.001f && target <= 0.001f && lobbyBgmSource.isPlaying) lobbyBgmSource.Pause();
    }

    // 별 상자의 숫자를 돌리거나 버튼을 누를 때 (StarLockBox가 부른다 — 이 컴퓨터에서 소리를 내므로 두 플레이어 모두 듣는다)
    public static void PlayStarLockPress(Vector3 position)
    {
        var b = LcwSfx.Bank;
        if (b != null && Instance != null && !Instance.quiet) LcwSfx.Play(b.numpadPress1, position, 0.9f, 14f);
    }

    // 안방 가구 미니게임에서 내가 키를 맞게 눌렀을 때 (Gim007PushPlayer가 부른다)
    public static void PlayMinigamePress()
    {
        var b = LcwSfx.Bank;
        if (b != null && Instance != null && !Instance.quiet) LcwSfx.Play2D(b.minigamePress, 0.9f);
    }

    // ---------------------------------------------------------------- 문
    struct DoorState { public bool open, locked; }
    readonly Dictionary<InteractableDoor, DoorState> doorStates = new Dictionary<InteractableDoor, DoorState>();
    readonly HashSet<InteractableDoor> doorsHandled = new HashSet<InteractableDoor>();
    InteractableDoor[] doors = new InteractableDoor[0];
    float nextDoorScan;

    void UpdateDoors()
    {
        if (Time.time >= nextDoorScan)
        {
            nextDoorScan = Time.time + 3f;
            doors = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        doorsHandled.Clear();
        foreach (var d in doors)
        {
            if (d == null) continue;
            bool open = d.IsOpen, locked = d.locked;
            if (!doorStates.TryGetValue(d, out var prev)) { doorStates[d] = new DoorState { open = open, locked = locked }; continue; }
            doorStates[d] = new DoorState { open = open, locked = locked };
            if (quiet || doorsHandled.Contains(d)) continue;

            Vector3 pos = d.transform.position;
            if (prev.locked && !locked)
            {
                // 열쇠로 푸는 문은 열쇠 소리, 기믹 등으로 풀리는 문은 잠금 풀리는 소리
                Mark(d);
                LcwSfx.Play(d.keyItem != null ? bank.keyTurn : bank.doorUnlock, pos, doorVolume, 22f);
            }
            if (prev.open != open)
            {
                Mark(d);
                // 현관문(탈출구)이 열릴 때만 "마지막 현관문" 효과음, 나머지 문은 보통 문 소리
                bool front = d.name.StartsWith("Door_Front");
                LcwSfx.Play(open ? (front ? bank.finalDoor : bank.doorOpen) : bank.doorClose, pos, doorVolume, 28f);
            }
        }
    }

    // 양문이 같이 열릴 때 소리가 두 번 겹치지 않게 짝문도 이번 프레임에는 처리한 것으로 친다
    void Mark(InteractableDoor d)
    {
        doorsHandled.Add(d);
        if (d.partner != null) doorsHandled.Add(d.partner);
    }

    // 잠긴 문을 열어 보려 할 때 (PlayerInteractor가 부른다 — 이 컴퓨터 플레이어의 시도이므로 이 컴퓨터에서만 들린다)
    public static void PlayDoorLocked(Vector3 position)
    {
        var b = LcwSfx.Bank;
        if (b != null) LcwSfx.Play(b.doorLocked, position, 1f, 18f);
    }

    // ---------------------------------------------------------------- 서랍
    readonly Dictionary<InteractableDrawer, bool> drawerStates = new Dictionary<InteractableDrawer, bool>();
    InteractableDrawer[] drawers = new InteractableDrawer[0];
    float nextDrawerScan;

    void UpdateDrawers()
    {
        if (Time.time >= nextDrawerScan)
        {
            nextDrawerScan = Time.time + 3f;
            drawers = FindObjectsByType<InteractableDrawer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }
        foreach (var d in drawers)
        {
            if (d == null) continue;
            bool open = d.IsOpen;
            if (!drawerStates.TryGetValue(d, out bool prev)) { drawerStates[d] = open; continue; }
            drawerStates[d] = open;
            if (quiet || prev == open) continue;
            LcwSfx.Play(open ? bank.drawerOpen : bank.drawerClose, d.transform.position, drawerVolume, 16f);
        }
    }

    // 접지 판정은 실제로 CharacterController.Move를 실행하는 소유자가 담당한다.
    // 원격 플레이어의 착지도 같은 위치에서 한 번 재생하여 거리 감쇠를 적용한다.
    public static void PlayJumpLanding(Vector3 position)
    {
        var b = LcwSfx.Bank;
        if (b == null || Instance == null || Instance.quiet || !GameActive) return;
        LcwSfx.Play(b.jumpLanding, position, Instance.jumpLandingVolume, 16f, 1.5f, 0f);
    }

    // ---------------------------------------------------------------- 발소리 (나와 상대 둘 다, 움직인 거리로 한 걸음씩)
    class Walker { public Vector3 last; public float distance; public bool init; }
    readonly Dictionary<NetworkPlayer, Walker> walkers = new Dictionary<NetworkPlayer, Walker>();

    void UpdateFootsteps()
    {
        for (int i = 0; i < NetworkPlayer.All.Count; i++)
        {
            var p = NetworkPlayer.All[i];
            if (p == null || !p.isActiveAndEnabled) continue;
            if (!walkers.TryGetValue(p, out var w)) { w = new Walker(); walkers[p] = w; }

            Vector3 pos = p.transform.position;
            if (!w.init) { w.last = pos; w.init = true; continue; }
            Vector3 delta = pos - w.last;
            w.last = pos;
            delta.y = 0f;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = delta.magnitude / dt;
            if (quiet || speed < 0.3f || speed > 12f) { if (speed > 12f) w.distance = 0f; continue; }   // 순간이동/스폰은 걸음이 아니다

            // 땅에 닿아 있을 때만 (점프 중에는 소리 없음)
            if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, 0.7f, ~0, QueryTriggerInteraction.Ignore)) continue;

            bool local = p == NetworkPlayer.Local;
            var movement = local ? p.GetComponent<FirstPersonController>() : null;
            // 내 상태는 실제 입력 결과를 사용한다. 상대는 웅크림 값이 동기화되지 않아 속도로 추정한다.
            bool crouch = movement != null ? movement.IsCrouching : speed < 3.0f;
            float stride = crouch ? crouchStride : walkStride;
            w.distance += delta.magnitude;
            if (w.distance < stride) continue;
            w.distance = 0f;

            float vol = (local ? localFootstepVolume : footstepVolume)
                * (crouch ? 0.45f : (speed > 5f ? 1.15f : 1f));
            if (local)
                LcwSfx.Play2D(bank.footstep, Mathf.Clamp01(vol), 0.06f);
            else
                LcwSfx.Play(bank.footstep, pos, Mathf.Clamp01(vol * 0.9f), 16f, 1.5f, 0.06f);
        }
    }
}
