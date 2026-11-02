using System;
using System.Collections.Generic;
using LGH;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AtticTreasure
{
    // 황금상 획득 연출 (2026-10-29): 열린 별 상자 속 황금상으로 카메라가 천천히 다가가고,
    // 빛이 점점 강해지다가 마지막에 왕관 가운데 보석이 크게 반짝인다.
    //  - 보석 반짝임: 왕관 끝(위쪽 둘레)에 작은 별빛이 짧게 나타났다 사라지고, 끝에 보석에서 큰 별빛.
    //  - 황금빛 후광: 황금상 뒤의 부드러운 빛 + 상자 안을 비추는 금색 조명 + 연출 중에만 블룸을 세게.
    //  - 떠다니는 빛 입자: 작은 금색 점들이 황금상 둘레를 나선으로 천천히 올라간다.
    //  - 카메라 줌인: 빛이 강해지는 동안 다가가고, 다 다가간 순간 보석이 반짝이도록 시간을 맞췄다.
    //
    // 구조: 이 컴퓨터에서만 도는 연출이다(AtticTreasureSync가 두 사람 모두에게 Play를 부른다).
    //  플레이어 카메라는 건드리지 않고 연출용 카메라를 잠깐 만들어 덮어쓴다(손에 든 양초가 화면을 가리지 않게).
    //  연출 동안은 커서를 풀어 이동·상호작용을 멈춘다(이 프로젝트의 "커서 풀림 = 조작 멈춤" 관례, 일시정지 창은 OverlayOpen으로 막음).
    //  빛·입자는 런타임에 만든 사각형 몇 개로 그리고, 재질은 additiveMaterial(URP Particles/Unlit, 더하기 섞기) 하나를 복제해 쓴다.
    //  모델의 메쉬는 실행 중에 읽을 수 없어서(Read/Write 꺼짐) 보석·왕관 위치는 황금상 겉모습 크기(bounds) 기준 비율로 정한다.
    public class AtticTreasureGlowCutscene : AtticTreasureCutscene
    {
        [Header("재질 (URP Particles/Unlit, 더하기 섞기) — Tools/KJH/AtticTreasure/Build Glow Cutscene가 만든다")]
        [SerializeField] Material additiveMaterial;

        [Header("시간 (초)")]
        [SerializeField] float zoomSeconds = 4.2f;
        [SerializeField] float gemFlashAt = 4.25f;
        [SerializeField] float gemFlashSeconds = 0.8f;
        [SerializeField] float totalSeconds = 6.2f;
        [SerializeField] float fadeSeconds = 0.5f;

        [Header("카메라 (황금상 정면 기준, m)")]
        [Tooltip("시작 거리/높이 (동료 화면 — 집은 사람은 자기 시점에서 출발)")]
        [SerializeField] float startDistance = 1.15f;
        [SerializeField] float startHeight = 0.65f;
        [Tooltip("끝 거리/높이 — 상자 앞벽 너머로 황금상 전체가 보이는 위치")]
        [SerializeField] float endDistance = 0.66f;
        [SerializeField] float endHeight = 0.72f;
        [Tooltip("줌인하는 동안 옆으로 살짝 도는 각도 (시작 → 끝)")]
        [SerializeField] float orbitStartDeg = -14f;
        [SerializeField] float orbitEndDeg = 4f;
        [SerializeField] float startFov = 50f;
        [SerializeField] float endFov = 36f;

        [Header("보석·왕관 위치 (황금상 겉모습 높이 비율)")]
        [SerializeField] float gemHeight01 = 0.87f;
        [Tooltip("보석이 몸 중심에서 정면으로 나와 있는 거리 (m)")]
        [SerializeField] float gemForward = 0.085f;
        [SerializeField] float crownHeight01 = 0.96f;
        [Tooltip("왕관 끝 둘레 반지름 = 겉모습 가로 반폭 × 이 값")]
        [SerializeField] float crownRadius01 = 0.8f;
        [SerializeField] int crownTips = 5;

        [Header("빛")]
        [SerializeField] Color goldColor = new Color(1f, 0.72f, 0.28f);
        [SerializeField] Color gemColor = new Color(1f, 0.85f, 1f);
        [SerializeField] float glowLightIntensity = 1.1f;
        [SerializeField] float keyLightIntensity = 0.45f;
        [SerializeField] int moteCount = 40;
        [Tooltip("연출 동안 덮어쓸 블룸 세기/기준 밝기")]
        [SerializeField] float bloomIntensity = 0.9f;
        [SerializeField] float bloomThreshold = 1.0f;

        [Header("소리 (비우면 LCW '인형극정답전구켜지기')")]
        [SerializeField] AudioClip gemFlashClip;

        [Header("문구")]
        [SerializeField] string pickerText = "황금상을 손에 넣었다";
        [SerializeField] string otherText = "동료가 황금상을 손에 넣었다";

        // ---- 실행 상태 ----
        bool playing, picker, flashSoundPlayed;
        Action done;
        float startedAt, endFadeUntil = -1f;

        Transform statue;   // 연출용 임시 황금상 (진짜 황금상의 겉모습을 복제한 것)
        bool homeCached; Vector3 homePos; Quaternion homeRot;
        Vector3 center, bottom, up, front;
        float height, halfWidth;

        Camera playerCam, cam;
        bool playerCamWasEnabled;
        Vector3 startPos; Quaternion startRot; float startFovValue;
        bool fromPlayerView;

        CursorLockMode prevLock; bool prevVisible;
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        FirstPersonController fpc; bool prevLook = true;

        GameObject fxRoot;
        Light glowLight, keyLight;
        Volume volume; VolumeProfile profile;
        Mesh quad;
        Texture2D softTex, starTex;
        readonly List<Material> mats = new List<Material>();

        class Sprite3D { public Transform t; public Material m; }
        Sprite3D halo, gemStar, gemCore;
        readonly List<Sprite3D> tipStars = new List<Sprite3D>();
        float[] tipPhase;
        readonly List<Sprite3D> motes = new List<Sprite3D>();
        float[] moteAngle, moteRadius, moteSpeed, moteSpin, moteLife, moteSize;

        GUIStyle style;

        public bool IsPlaying { get { return playing; } }

        public override void Play(bool isPicker, Action onFinished)
        {
            if (playing) Finish(false);
            picker = isPicker;
            done = onFinished;
            startedAt = Time.unscaledTime;
            flashSoundPlayed = false;
            playing = true;
            endFadeUntil = -1f;

            var s = FindAnyObjectByType<AtticTreasureStatue>(FindObjectsInactive.Include);
            statue = s != null ? MakeStatueCopy(s) : null;
            if (statue == null) { Debug.LogWarning("[AtticTreasure] 황금상을 찾지 못해 연출을 건너뜁니다", this); return; }

            MeasureStatue();
            LockPlayer();
            HidePlayers();
            BuildCamera();
            BuildFx();
        }

        // ---------------- 준비 ----------------

        // 상자 속 원래 자리를 미리 기억해 둔다 (연출 시점에 진짜 황금상이 이미 손으로 옮겨졌어도 같은 자리에 보이게)
        void Start()
        {
            var s = FindAnyObjectByType<AtticTreasureStatue>(FindObjectsInactive.Include);
            if (s != null && s.IsAtHome) { homeCached = true; homePos = s.transform.position; homeRot = s.transform.rotation; }
        }

        // 진짜 황금상은 연출 중 숨겨져 있거나(상태 = 연출 중) 이미 손에 옮겨졌을 수 있다(2026-10-29 실제 플레이에서 빈 상자가 보임).
        // 그래서 겉모습만 복제한 임시 황금상을 상자 속 원래 자리에 세워 두고 연출이 끝나면 지운다. 진짜 황금상의 상태·표시는 건드리지 않는다.
        Transform MakeStatueCopy(AtticTreasureStatue s)
        {
            var src = s.transform;
            Vector3 pos = src.position; Quaternion rot = src.rotation;
            if (!s.IsAtHome && homeCached) { pos = homePos; rot = homeRot; }

            var root = new GameObject("AtticTreasure_StatueCopy").transform;
            root.SetPositionAndRotation(pos, rot);
            root.localScale = src.lossyScale;
            foreach (Transform child in src)
            {
                if (child.GetComponentInChildren<Renderer>(true) == null) continue;
                var copy = Instantiate(child.gameObject, root, false);
                copy.SetActive(true);
                foreach (var c in copy.GetComponentsInChildren<Component>(true))
                {
                    if (c is Transform || c is MeshFilter || c is Renderer) continue;
                    Destroy(c);   // 콜라이더·스크립트는 빼고 겉모습만 남긴다
                }
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            }
            if (root.GetComponentInChildren<Renderer>(true) == null) { Destroy(root.gameObject); return null; }
            return root;
        }

        void MeasureStatue()
        {
            var rs = statue.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(statue.position, Vector3.zero);
            bool any = false;
            foreach (var r in rs)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            up = Vector3.up;
            front = Vector3.ProjectOnPlane(statue.forward, up).normalized;
            if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
            height = Mathf.Max(0.05f, b.size.y);
            halfWidth = Mathf.Max(0.02f, Mathf.Max(b.extents.x, b.extents.z));
            bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            center = b.center;
        }

        Vector3 GemPoint { get { return bottom + up * (height * gemHeight01) + front * gemForward; } }
        Vector3 LookTarget { get { return bottom + up * (height * 0.55f); } }

        void LockPlayer()
        {
            prevLock = Cursor.lockState;
            prevVisible = Cursor.visible;
            MultiplayerMenu.OverlayOpen = true;   // 커서를 풀어도 일시정지 창이 뜨지 않게
            Cursor.lockState = CursorLockMode.None;   // 커서가 풀려 있으면 이동·상호작용이 멈춘다
            Cursor.visible = false;

            var pi = PlayerInteractor.Active;
            fpc = pi != null ? pi.GetComponentInParent<FirstPersonController>() : null;
            if (fpc != null) { prevLook = fpc.LookEnabled; fpc.LookEnabled = false; }
        }

        // 플레이어들(내 손의 양초, 상자 앞에 서 있는 동료 몸 등)이 연출 카메라를 가리지 않게 잠깐 숨긴다.
        // 황금상은 연출이 끝난 뒤에 손에 쥐어지므로 여기서 숨겨지지 않는다.
        void HidePlayers()
        {
            hiddenRenderers.Clear();
            foreach (var np in NetworkPlayer.All)
                if (np != null) HideUnder(np.transform);
            // 내 왼손에 든 물건(예: 동료 쪽에서 들고 있는 현관 열쇠)은 플레이어 밖의 손 오브젝트에 붙어 있을 수 있어 따로 숨긴다
            var held = PlayerHand.LocalHeld;
            if (held != null) HideUnder(held.transform);
        }

        void HideUnder(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled || hiddenRenderers.Contains(r)) continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }

        void ShowPlayers()
        {
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            hiddenRenderers.Clear();
        }

        void BuildCamera()
        {
            var view = PlayerHand.ViewCamera();
            playerCam = view != null ? view.GetComponent<Camera>() : null;
            if (playerCam == null) playerCam = Camera.main;

            var go = new GameObject("AtticTreasure_CutsceneCamera");
            cam = go.AddComponent<Camera>();
            if (playerCam != null)
            {
                cam.CopyFrom(playerCam);
                var src = playerCam.GetUniversalAdditionalCameraData();
                var dst = cam.GetUniversalAdditionalCameraData();
                dst.renderPostProcessing = true;
                dst.antialiasing = src.antialiasing;
                dst.volumeLayerMask = src.volumeLayerMask;
                cam.depth = playerCam.depth + 10f;
                playerCamWasEnabled = playerCam.enabled;
                playerCam.enabled = false;
            }
            else cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cam.nearClipPlane = 0.02f;

            // 집은 사람은 자기 눈에서 그대로 출발, 동료는 어둠에서 시작 지점으로 바로 들어온다
            fromPlayerView = picker && playerCam != null && Vector3.Distance(playerCam.transform.position, center) < 4f;
            if (fromPlayerView)
            {
                startPos = playerCam.transform.position;
                startRot = playerCam.transform.rotation;
                startFovValue = playerCam.fieldOfView;
            }
            else
            {
                startPos = ShotPosition(startDistance, startHeight, orbitStartDeg);
                startRot = Quaternion.LookRotation(LookTarget - startPos, up);
                startFovValue = startFov;
            }
            cam.transform.SetPositionAndRotation(startPos, startRot);
            cam.fieldOfView = startFovValue;
        }

        Vector3 ShotPosition(float dist, float h, float orbitDeg)
        {
            Vector3 dir = Quaternion.AngleAxis(orbitDeg, up) * front;
            return LookTarget + dir * dist + up * h;
        }

        void BuildFx()
        {
            fxRoot = new GameObject("AtticTreasure_CutsceneFX");
            quad = MakeQuad();
            if (softTex == null) softTex = MakeSoftTexture(64);
            if (starTex == null) starTex = MakeStarTexture(128);

            // 상자 안을 금빛으로 물들이는 조명 + 카메라 쪽에서 황금상을 살짝 비추는 조명
            glowLight = MakeLight("GlowLight", center + up * (height * 0.2f) - front * (halfWidth * 1.6f), 1.3f);   // 뒤에서 비춰 윤곽이 빛나게
            keyLight = MakeLight("KeyLight", ShotPosition(endDistance + 0.2f, endHeight + 0.3f, orbitEndDeg), 2.5f);
            keyLight.color = Color.Lerp(goldColor, Color.white, 0.6f);

            // 후광: 황금상 뒤에 크고 부드러운 빛, 앞쪽엔 작고 진한 빛
            halo = MakeSprite("Halo", softTex, false);

            // 왕관 끝 별빛
            tipStars.Clear();
            int n = Mathf.Max(1, crownTips);
            tipPhase = new float[n];
            for (int i = 0; i < n; i++)
            {
                tipStars.Add(MakeSprite("TipStar" + i, starTex, true));
                tipPhase[i] = UnityEngine.Random.value;
            }

            // 보석 별빛
            gemCore = MakeSprite("GemCore", softTex, true);
            gemStar = MakeSprite("GemStar", starTex, true);

            // 떠다니는 빛 입자
            motes.Clear();
            int m = Mathf.Max(0, moteCount);
            moteAngle = new float[m]; moteRadius = new float[m]; moteSpeed = new float[m];
            moteSpin = new float[m]; moteLife = new float[m]; moteSize = new float[m];
            for (int i = 0; i < m; i++)
            {
                motes.Add(MakeSprite("Mote" + i, softTex, true));
                moteAngle[i] = UnityEngine.Random.value * Mathf.PI * 2f;
                moteRadius[i] = halfWidth * UnityEngine.Random.Range(0.9f, 2.6f);
                moteSpeed[i] = UnityEngine.Random.Range(0.04f, 0.1f);
                moteSpin[i] = UnityEngine.Random.Range(0.4f, 0.9f) * (UnityEngine.Random.value < 0.8f ? 1f : -1f);
                moteLife[i] = UnityEngine.Random.value;   // 0~1 위치(높이 비율)에서 시작
                moteSize[i] = UnityEngine.Random.Range(0.008f, 0.02f);
            }

            // 연출 동안만 블룸을 세게 (기존 전역 볼륨 위에 우선순위 높은 볼륨을 잠깐 얹는다)
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.tint.Override(Color.Lerp(goldColor, Color.white, 0.4f));
            var vgo = new GameObject("Bloom");
            vgo.transform.SetParent(fxRoot.transform, false);
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = profile;
            volume.weight = 0f;
        }

        Light MakeLight(string name, Vector3 pos, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(fxRoot.transform, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.color = goldColor;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            return l;
        }

        Sprite3D MakeSprite(string name, Texture tex, bool overlay)
        {
            var go = new GameObject(name);
            go.transform.SetParent(fxRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Material mat;
            if (additiveMaterial != null) mat = new Material(additiveMaterial);
            else mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            // 별빛·입자는 황금상/상자에 가려지지 않게 맨 위에 그린다 (후광은 황금상 뒤에 있어야 하므로 깊이 검사)
            if (overlay) { mat.SetFloat("_ZTest", (float)CompareFunction.Always); mat.renderQueue = 3100; }
            mats.Add(mat);
            mr.sharedMaterial = mat;
            go.SetActive(false);
            return new Sprite3D { t = go.transform, m = mat };
        }

        // ---------------- 진행 ----------------

        void LateUpdate()
        {
            if (!playing) return;
            if (statue == null || cam == null) { Finish(true); return; }

            float t = Time.unscaledTime - startedAt;
            Cursor.visible = false;
            if (Cursor.lockState == CursorLockMode.Locked) Cursor.lockState = CursorLockMode.None;

            // 카메라: 처음엔 빠르게, 끝에 아주 천천히 멈추듯 다가간다
            float z = Smoother(Mathf.Clamp01(t / zoomSeconds));
            Vector3 endPos = ShotPosition(endDistance, endHeight, orbitEndDeg);
            Quaternion endRot = Quaternion.LookRotation(LookTarget - endPos, up);
            Vector3 pos;
            Quaternion rot;
            if (fromPlayerView)
            {
                pos = Vector3.Lerp(startPos, endPos, z);
                rot = Quaternion.Slerp(startRot, endRot, Smoother(Mathf.Clamp01(t / (zoomSeconds * 0.45f))));
            }
            else
            {
                float orbit = Mathf.Lerp(orbitStartDeg, orbitEndDeg, z);
                pos = ShotPosition(Mathf.Lerp(startDistance, endDistance, z), Mathf.Lerp(startHeight, endHeight, z), orbit);
                rot = Quaternion.LookRotation(LookTarget - pos, up);
            }
            // 보석이 반짝일 때 아주 살짝 앞으로 밀리는 느낌
            float flash01 = FlashCurve(t);
            pos += rot * Vector3.forward * (0.015f * flash01);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = Mathf.Lerp(startFovValue, endFov, z);

            // 빛 세기: 줌인을 따라 점점 강해진다
            float glow = Smoother(Mathf.Clamp01((t - 0.2f) / (zoomSeconds - 0.2f)));
            float outFade = 1f - Mathf.Clamp01((t - (totalSeconds - fadeSeconds)) / fadeSeconds);
            float g = glow * outFade;
            float pulse = 1f + 0.08f * Mathf.Sin(t * 5.3f);

            glowLight.intensity = glowLightIntensity * g * pulse + glowLightIntensity * 1.5f * flash01;
            keyLight.intensity = keyLightIntensity * Mathf.Clamp01(t / 0.8f) * outFade;
            volume.weight = Mathf.Clamp01(0.35f + 0.65f * g + flash01) * outFade;

            Vector3 toCam = (cam.transform.position - center).normalized;
            Vector3 camRight = cam.transform.right, camUp = cam.transform.up;

            // 후광
            Place(halo, center - toCam * (halfWidth * 1.4f), height * Mathf.Lerp(1.6f, 2.4f, g) * pulse, goldColor, 0.32f * g + 0.3f * flash01, 0f);

            // 왕관 끝 별빛: 1초쯤부터 하나씩 짧게 깜빡 (정면 쪽 반원에 고르게)
            int n = tipStars.Count;
            float tipsOn = Mathf.Clamp01((t - 1.0f) / 0.8f) * outFade;
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? 0f : Mathf.Lerp(-80f, 80f, i / (float)(n - 1));
                Vector3 dir = Quaternion.AngleAxis(a, up) * front;
                Vector3 p = bottom + up * (height * crownHeight01) + dir * (halfWidth * crownRadius01);
                // 1.1초마다 한 번, 0.35초 동안: 크기가 0→최대→0
                float cyc = Mathf.Repeat(t / 1.1f + tipPhase[i], 1f) * 1.1f;
                float k = cyc < 0.35f ? Mathf.Sin(cyc / 0.35f * Mathf.PI) : 0f;
                Place(tipStars[i], p + toCam * 0.01f, 0.07f * k, Color.Lerp(goldColor, Color.white, 0.6f) * 2.2f, k * tipsOn, cyc * 60f);
            }

            // 보석: 줌인이 끝날 무렵 큰 별빛
            Vector3 gem = GemPoint + toCam * 0.012f;
            float gemIdle = 0.25f * g * (0.6f + 0.4f * Mathf.Sin(t * 7f));
            Place(gemCore, gem, Mathf.Lerp(0.025f, 0.05f, flash01) + 0.01f * g, gemColor * 1.6f, gemIdle + 0.5f * flash01, 0f);
            Place(gemStar, gem, 0.24f * flash01, gemColor * 3f, flash01, 15f + 50f * Mathf.Clamp01((t - gemFlashAt + gemFlashSeconds * 0.5f) / gemFlashSeconds));
            if (!flashSoundPlayed && t >= gemFlashAt - 0.05f)
            {
                flashSoundPlayed = true;
                var clip = gemFlashClip != null ? gemFlashClip : (LcwSfx.Bank != null ? LcwSfx.Bank.puppetBulbOn : null);
                if (clip != null) LcwSfx.Play2D(clip, 0.9f);
            }

            // 떠다니는 입자: 황금상 둘레를 나선으로 천천히 올라간다
            float rise = height * 1.8f;
            float dt = Time.unscaledDeltaTime;
            float motesOn = Mathf.Clamp01(0.25f + g) * outFade;
            for (int i = 0; i < motes.Count; i++)
            {
                moteLife[i] += moteSpeed[i] * dt / rise * (1f + g);
                if (moteLife[i] > 1f) { moteLife[i] -= 1f; moteAngle[i] = UnityEngine.Random.value * Mathf.PI * 2f; }
                moteAngle[i] += moteSpin[i] * dt;
                float life = moteLife[i];
                float r = moteRadius[i] * (1f + 0.25f * life);
                Vector3 p = bottom + up * (life * rise - height * 0.1f)
                    + new Vector3(Mathf.Cos(moteAngle[i]), 0f, Mathf.Sin(moteAngle[i])) * r;
                float fade = Mathf.Sin(life * Mathf.PI);   // 아래에서 생겨 위에서 사라짐
                float twinkle = 0.65f + 0.35f * Mathf.Sin(t * 9f + i * 1.7f);
                Place(motes[i], p, moteSize[i] * (1f + 0.5f * g), goldColor * 2.4f, fade * twinkle * motesOn, 0f);
            }

            if (t >= totalSeconds) Finish(true);
        }

        // 보석 반짝임 곡선: gemFlashAt에 최대, 빠르게 커졌다가 조금 천천히 줄어든다
        float FlashCurve(float t)
        {
            float half = gemFlashSeconds * 0.5f;
            float d = t - gemFlashAt;
            if (d < -half * 0.6f || d > half * 1.4f) return 0f;
            if (d < 0f) return Smoother(1f + d / (half * 0.6f));
            return 1f - Smoother(d / (half * 1.4f));
        }

        void Place(Sprite3D s, Vector3 pos, float size, Color color, float alpha, float rollDeg)
        {
            bool on = alpha > 0.003f && size > 0.0005f;
            if (s.t.gameObject.activeSelf != on) s.t.gameObject.SetActive(on);
            if (!on) return;
            s.t.position = pos;
            s.t.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, rollDeg);
            s.t.localScale = new Vector3(size, size, size);
            color.a = Mathf.Clamp01(alpha);
            s.m.SetColor("_BaseColor", color);
        }

        static float Smoother(float x) { x = Mathf.Clamp01(x); return x * x * x * (x * (x * 6f - 15f) + 10f); }

        // ---------------- 끝내기 ----------------

        void Finish(bool callDone)
        {
            if (!playing) return;
            playing = false;

            if (fxRoot != null) Destroy(fxRoot);
            fxRoot = null;
            foreach (var m in mats) if (m != null) Destroy(m);
            mats.Clear();
            if (profile != null) Destroy(profile);
            profile = null;
            if (quad != null) Destroy(quad);
            quad = null;
            tipStars.Clear(); motes.Clear();

            if (cam != null) Destroy(cam.gameObject);
            cam = null;

            if (playerCam != null) playerCam.enabled = playerCamWasEnabled || playerCam.enabled;
            if (statue != null) endFadeUntil = Time.unscaledTime + fadeSeconds;   // 원래 시점은 어둠에서 밝아지며 돌아온다
            if (statue != null) Destroy(statue.gameObject);   // 임시 황금상 정리 (진짜는 이제 손에 쥐어진다)
            statue = null;

            ShowPlayers();
            if (fpc != null) fpc.LookEnabled = prevLook;
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
            MultiplayerMenu.OverlayOpen = false;

            if (callDone)
            {
                var d = done;
                done = null;
                d?.Invoke();
            }
        }

        void Update()
        {
            // 황금상을 못 찾았을 때는 바로 끝 (다음 프레임에 — Play를 부른 쪽 처리가 끝난 뒤)
            if (playing && statue == null) { playing = false; var d = done; done = null; d?.Invoke(); }
        }

        void OnDisable() { Finish(false); }

        void OnDestroy()
        {
            if (softTex != null) Destroy(softTex);
            if (starTex != null) Destroy(starTex);
        }

        // ---------------- 화면 위 (어둠 + 문구) ----------------

        void OnGUI()
        {
            float a = 0f;
            if (playing && statue != null)
            {
                float t = Time.unscaledTime - startedAt;
                if (!fromPlayerView) a = Mathf.Max(a, 1f - t / fadeSeconds);   // 동료: 어둠에서 시작
                a = Mathf.Max(a, Mathf.Clamp01((t - (totalSeconds - fadeSeconds)) / fadeSeconds));   // 끝에 어두워짐

                float ta = Mathf.Clamp01((t - (gemFlashAt + 0.2f)) / 0.4f) * (1f - Mathf.Clamp01((t - (totalSeconds - fadeSeconds * 0.8f)) / (fadeSeconds * 0.8f)));
                if (ta > 0f) DrawCaption(ta);
            }
            else if (endFadeUntil > 0f)
            {
                a = Mathf.Clamp01((endFadeUntil - Time.unscaledTime) / fadeSeconds);
                if (a <= 0f) endFadeUntil = -1f;
            }
            if (a <= 0f) return;
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawCaption(float alpha)
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(Screen.height / 720f * 30f);
            var r = new Rect(0, Screen.height * 0.74f, Screen.width, Screen.height * 0.1f);
            string text = picker ? pickerText : otherText;
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.7f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            GUI.color = new Color(1f, 0.86f, 0.5f, alpha);
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        // ---------------- 만드는 것들 ----------------

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "CutsceneQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };   // 앞뒤 둘 다
            m.RecalculateBounds();
            return m;
        }

        // 가운데가 밝고 바깥으로 부드럽게 사라지는 원
        static Texture2D MakeSoftTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CutsceneSoft" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float v = Mathf.Clamp01(1f - d);
                    v = v * v * (3f - 2f * v);
                    px[y * size + x] = new Color(1f, 1f, 1f, v * v);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // 네 갈래 별빛 (가로·세로 긴 빛줄기 + 대각선 짧은 빛줄기 + 밝은 중심)
        static Texture2D MakeStarTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CutsceneStar" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
                    float ray1 = Mathf.Max(Ray(ax, ay, 0.035f), Ray(ay, ax, 0.035f));
                    float ux = Mathf.Abs(dx + dy) * 0.7071f, uy = Mathf.Abs(dx - dy) * 0.7071f;
                    float ray2 = 0.45f * Mathf.Max(Ray(ux * 1.8f, uy, 0.03f), Ray(uy * 1.8f, ux, 0.03f));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = Mathf.Clamp01(1f - d / 0.1f);
                    float v = Mathf.Clamp01(Mathf.Max(Mathf.Max(ray1, ray2), core * core));
                    px[y * size + x] = new Color(1f, 1f, 1f, v);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // along: 빛줄기 방향 거리(0=중심, 1=끝), across: 옆 방향 거리
        static float Ray(float along, float across, float width)
        {
            if (along >= 1f) return 0f;
            float w = width * (1f - along);
            float side = Mathf.Clamp01(1f - across / Mathf.Max(0.0001f, w));
            float len = 1f - along;
            return side * side * len * len;
        }
    }
}
