using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // On the player. The flintlock pistol from the attic:
    //  - Loading: while you carry it, hold R. It takes loadSeconds in total (1:30) and can be done bit by bit
    //    (let go of R and the progress stays). The loading noise is heard by everyone near you - do it in secret.
    //  - Shooting is only possible once the front door opens (just before the ending). From then on, with a loaded
    //    pistol: hold right mouse to aim, left click to fire. There is one bullet.
    //  - Hitting another player ends the game for everyone: the betrayal ending.
    public class PistolLoader : NetworkBehaviour
    {
        public static PistolLoader Local { get; private set; }
        // true when R belongs to loading (so the candle swap stays out of the way)
        public static bool WantsR { get { return Local != null && Local.HasGun && !Local.Loaded && !Local.spent; } }
        // the front door has opened: guns may be fired
        public static bool GunPhase { get; private set; }
        public static bool EndingShown { get { return endingTitle != null; } }

        public ItemData pistol;
        public GameObject viewModel;            // the pistol model shown in your hand while aiming
        public Material viewModelMaterial;
        public float loadSeconds = 90f;
        [Tooltip("How far away other players can hear the loading noise (metres).")]
        public float hearDistance = 14f;
        [Range(0f, 1f)] public float volume = 0.9f;
        [Tooltip("Walking speed while loading (1 = normal).")]
        public float moveWhileLoading = 0.35f;
        public float range = 60f;

        readonly NetworkVariable<bool> loadingNet = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> progressNet = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        float progress;
        bool loadingLocal, spent, aiming;
        PlayerInventory inventory;
        FirstPersonController move;
        Camera cam;
        float baseFov = -1f;
        GameObject heldGun;
        AudioSource noise, bangSource;
        Light flash;
        float flashUntil;
        static AudioClip loadClip, bangClip;
        GUIStyle hintStyle, barStyle, endTitleStyle, endBodyStyle;
        float doneMessageUntil;

        static InteractableDoor[] frontDoors;
        static bool phaseAnnounced;
        static string endingTitle, endingBody;

        public float Progress { get { return Mine ? progress : progressNet.Value; } }
        public bool Loaded { get { return !spent && Progress >= 1f; } }
        public bool HasGun { get { return inventory != null && pistol != null && inventory.HasItem(pistol); } }
        public bool IsLoading { get { return Mine ? loadingLocal : loadingNet.Value; } }
        bool Mine { get { return IsSpawned ? IsOwner : true; } }

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            move = GetComponent<FirstPersonController>();
            cam = GetComponentInChildren<Camera>(true);
            // body hitbox (the CharacterController is switched off on other players' copies)
            if (transform.Find("Hitbox") == null)
            {
                var hb = new GameObject("Hitbox");
                hb.transform.SetParent(transform, false);
                var cap = hb.AddComponent<CapsuleCollider>();
                cap.isTrigger = true; cap.radius = 0.3f; cap.height = 1.6f; cap.center = new Vector3(0f, 0.8f, 0f);
            }
            noise = MakeSource("PistolLoadNoise", new Vector3(0f, 1.1f, 0.3f), hearDistance, volume);
            noise.clip = LoadClip();
            noise.loop = true;
            bangSource = MakeSource("PistolBang", new Vector3(0f, 1.4f, 0.4f), 80f, 1f);
            bangSource.clip = BangClip();
            var fl = new GameObject("MuzzleFlash");
            fl.transform.SetParent(cam != null ? cam.transform : transform, false);
            fl.transform.localPosition = new Vector3(0.15f, -0.1f, 0.9f);
            flash = fl.AddComponent<Light>();
            flash.type = LightType.Point; flash.range = 7f; flash.intensity = 6f; flash.color = new Color(1f, 0.75f, 0.4f); flash.enabled = false;
        }

        AudioSource MakeSource(string n, Vector3 local, float maxDist, float vol)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1.5f; a.maxDistance = maxDist; a.volume = vol; a.dopplerLevel = 0f;
            return a;
        }

        public override void OnNetworkSpawn() { if (IsOwner) Local = this; }
        public override void OnNetworkDespawn() { if (Local == this) Local = null; SetAim(false); }
        void OnEnable() { if (!IsSpawned && Local == null) Local = this; }
        void OnDisable() { if (Local == this && !IsSpawned) Local = null; }

        void Update()
        {
            UpdatePhase();
            if (Mine) OwnerUpdate();
            bool play = IsLoading;
            if (play && !noise.isPlaying) noise.Play();
            else if (!play && noise.isPlaying) noise.Stop();
            if (flash.enabled && Time.time > flashUntil) flash.enabled = false;
        }

        // ---------------- front door = guns allowed ----------------
        static void UpdatePhase()
        {
            if (frontDoors == null || frontDoors.Length == 0)
            {
                var all = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include);
                var list = new System.Collections.Generic.List<InteractableDoor>();
                foreach (var d in all) if (d.name.StartsWith("Door_Front")) list.Add(d);
                frontDoors = list.ToArray();
            }
            bool open = false, relocked = frontDoors.Length > 0;
            foreach (var d in frontDoors)
            {
                if (d == null) continue;
                if (d.IsOpen) open = true;
                if (!d.locked || d.IsOpen) relocked = false;
            }
            if (open && !GunPhase) { GunPhase = true; phaseAnnounced = false; }
            if (GunPhase && relocked) { GunPhase = false; phaseAnnounced = false; endingTitle = null; }   // new room
        }

        // ---------------- owner: load / aim / fire ----------------
        void OwnerUpdate()
        {
            if (pistol == null && ItemDatabase.Instance != null) pistol = ItemDatabase.Instance.Find("pistol_flintlock");
            if (!GunPhase && spent) { spent = false; progress = 0f; }   // room was reset
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool busy = EndingShown || PlayerInventory.UiOpen || MultiplayerMenu.Typing || Cursor.lockState != CursorLockMode.Locked || (move != null && !move.enabled);

            if (GunPhase && !phaseAnnounced)
            {
                phaseAnnounced = true;
                var pi = PlayerInteractor.Active;
                if (pi != null && HasGun)
                    pi.ShowMessage(Loaded ? "정문이 열렸다. 지금이라면 총을 쓸 수 있다.  [우클릭] 조준  [좌클릭] 발사" : "정문이 열렸다. 총이 장전되어 있었다면...", 5f);
            }

            // loading
            bool want = HasGun && !spent && progress < 1f && !aiming && kb != null && kb.rKey.isPressed && !busy;
            if (want)
            {
                progress = Mathf.Min(1f, progress + Time.deltaTime / Mathf.Max(1f, loadSeconds));
                if (progress >= 1f)
                {
                    want = false;
                    doneMessageUntil = Time.time + 3f;
                    var pi = PlayerInteractor.Active;
                    if (pi != null) pi.ShowMessage(GunPhase ? "장전 완료. [우클릭] 조준  [좌클릭] 발사" : "장전 완료. 총알 한 발이 들어갔다. 아직은 쏠 때가 아니다.", 3f);
                }
            }
            if (want != loadingLocal)
            {
                loadingLocal = want;
                if (IsSpawned) loadingNet.Value = want;
            }
            if (IsSpawned && Mathf.Abs(progressNet.Value - progress) > 0.01f) progressNet.Value = progress;

            // aiming / firing
            bool canShoot = GunPhase && HasGun && Loaded && !busy;
            SetAim(canShoot && mouse != null && mouse.rightButton.isPressed);
            if (aiming && mouse.leftButton.wasPressedThisFrame) Fire();

            if (move != null && !EndingShown) move.SpeedMultiplier = loadingLocal ? moveWhileLoading : aiming ? 0.5f : 1f;
            if (cam != null)
            {
                if (baseFov < 0f) baseFov = cam.fieldOfView;
                float target = aiming ? baseFov * 0.72f : baseFov;
                if (StarLockBox.Inspecting == null) cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, target, Time.deltaTime * 120f);
            }
        }

        void SetAim(bool on)
        {
            if (aiming == on) return;
            aiming = on;
            if (on && heldGun == null && viewModel != null && cam != null)
            {
                heldGun = Instantiate(viewModel, cam.transform);
                heldGun.name = "HeldPistol";
                heldGun.transform.localPosition = new Vector3(0.17f, -0.15f, 0.42f);
                heldGun.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * viewModel.transform.localRotation;
                heldGun.transform.localScale = Vector3.one * 0.38f;
                foreach (var r in heldGun.GetComponentsInChildren<Renderer>())
                {
                    if (viewModelMaterial != null) r.sharedMaterial = viewModelMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach (var c in heldGun.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            if (heldGun != null) heldGun.SetActive(on);
        }

        void Fire()
        {
            spent = true;
            SetAim(false);
            ulong target = ulong.MaxValue;
            if (cam != null)
            {
                var hits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, range, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue;   // own body
                    var other = h.collider.GetComponentInParent<PistolLoader>();
                    if (other != null && other != this) { target = other.IsSpawned ? other.OwnerClientId : 0; break; }
                    if (h.collider.isTrigger) continue;                          // ladder spaces etc. don't stop bullets
                    break;                                                        // a wall / furniture stops the bullet
                }
            }
            if (IsSpawned) FireServerRpc(target);
            else ShotFired(target, 0, true);
        }

        [Rpc(SendTo.Server)]
        void FireServerRpc(ulong target) { FireClientsRpc(target, OwnerClientId); }

        [Rpc(SendTo.ClientsAndHost)]
        void FireClientsRpc(ulong target, ulong shooter) { ShotFired(target, shooter, false); }

        void ShotFired(ulong target, ulong shooter, bool offline)
        {
            bangSource.Play();
            flash.enabled = true; flashUntil = Time.time + 0.07f;
            if (Mine) { spent = true; progress = 0f; }
            bool hit = target != ulong.MaxValue;
            ulong me = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.LocalClientId : 0;
            if (!hit)
            {
                if (Mine && PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage("빗나갔다... 총알은 이제 없다.", 4f);
                else if (PlayerInteractor.Active != null) PlayerInteractor.Active.ShowMessage("어디선가 총성이 울렸다!", 3f);
                return;
            }
            string shooterName = NameOf(shooter), victimName = NameOf(target);
            if (!offline && me == shooter)
                ShowEnding("배신 엔딩", "당신은 " + victimName + "을(를) 쏘았다.\n\n정문 너머의 보물은 이제 당신 혼자의 것이다.\n하지만 그 무게를 평생 혼자 짊어지게 될 것이다.");
            else if (!offline && me == target)
                ShowEnding("배신 엔딩", "등 뒤에서 총성이 울렸다.\n\n믿었던 " + shooterName + "의 총알이었다.\n보물은 끝내 당신의 것이 되지 못했다.");
            else
                ShowEnding("배신 엔딩", shooterName + "이(가) " + victimName + "을(를) 쏘았다.\n\n정문 앞에서, 보물은 한 사람의 몫이 되었다.");
        }

        static string NameOf(ulong client)
        {
            foreach (var p in NetworkPlayer.All)
                if (p != null && p.IsSpawned && p.OwnerClientId == client) return string.IsNullOrEmpty(p.Nickname) ? "동료" : p.Nickname;
            return "동료";
        }

        static void ShowEnding(string title, string body)
        {
            endingTitle = title; endingBody = body;
            var l = Local;
            if (l != null)
            {
                l.SetAim(false);
                if (l.move != null) { l.move.SpeedMultiplier = 0f; l.move.LookEnabled = false; }
            }
        }

        // ---------------- HUD ----------------
        void OnGUI()
        {
            if (!Mine) return;
            var v = UiScale.Apply();
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                hintStyle.normal.textColor = new Color(0.9f, 0.85f, 0.75f, 0.9f);
                barStyle = new GUIStyle(hintStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
                barStyle.normal.textColor = new Color(1f, 0.9f, 0.7f);
                endTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                endTitleStyle.normal.textColor = new Color(0.85f, 0.15f, 0.12f);
                endBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperCenter, wordWrap = true };
                endBodyStyle.normal.textColor = new Color(0.9f, 0.86f, 0.8f);
            }
            if (EndingShown)
            {
                GUI.depth = -200;
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.88f); GUI.DrawTexture(new Rect(0, 0, v.x, v.y), Texture2D.whiteTexture); GUI.color = old;
                GUI.Label(new Rect(0, v.y * 0.28f, v.x, 60), endingTitle, endTitleStyle);
                GUI.Label(new Rect(v.x * 0.2f, v.y * 0.28f + 80, v.x * 0.6f, 200), endingBody, endBodyStyle);
                GUI.Label(new Rect(0, v.y - 50, v.x, 24), "[Esc] 메뉴", hintStyle);
                return;
            }
            if (!HasGun || PlayerInventory.UiOpen) return;
            if (aiming)
            {
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.DrawTexture(new Rect(v.x * 0.5f - 3, v.y * 0.5f - 3, 6, 6), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.3f, 0.2f, 1f); GUI.DrawTexture(new Rect(v.x * 0.5f - 2, v.y * 0.5f - 2, 4, 4), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(0, v.y - 124, v.x, 22), "[좌클릭] 발사 (총알 1발)", barStyle);
                return;
            }
            int pct = Mathf.FloorToInt(progress * 100f);
            float w = 260f, x = (v.x - w) * 0.5f, y = v.y - 120f;
            if (loadingLocal)
            {
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.65f); GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, 18), Texture2D.whiteTexture);
                GUI.color = new Color(0.82f, 0.62f, 0.3f, 1f); GUI.DrawTexture(new Rect(x, y, w * progress, 14), Texture2D.whiteTexture);
                GUI.color = old;
                float left = (1f - progress) * loadSeconds;
                GUI.Label(new Rect(0, y - 26, v.x, 22), "장전 중... " + pct + "%  (남은 시간 " + Mathf.FloorToInt(left / 60f) + ":" + Mathf.FloorToInt(left % 60f).ToString("00") + ")", barStyle);
                GUI.Label(new Rect(0, y + 18, v.x, 20), "철컥거리는 소리가 주변에 들린다!", hintStyle);
            }
            else if (spent)
                GUI.Label(new Rect(0, y - 4, v.x, 22), "총알이 없다", hintStyle);
            else if (progress < 1f)
                GUI.Label(new Rect(0, y - 4, v.x, 22), "[R] 길게 눌러 권총 장전  " + pct + "%", hintStyle);
            else if (GunPhase)
                GUI.Label(new Rect(0, y - 4, v.x, 22), "[우클릭] 조준  [좌클릭] 발사", barStyle);
            else if (Time.time < doneMessageUntil)
                GUI.Label(new Rect(0, y - 4, v.x, 22), "권총 장전 완료 (1발) - 정문이 열리면 쏠 수 있다", barStyle);
        }

        // ---------------- sounds made in code ----------------
        // ramrod scraping down the barrel + metal clicks (2.4 s loop)
        static AudioClip LoadClip()
        {
            if (loadClip != null) return loadClip;
            const int rate = 22050;
            int n = Mathf.RoundToInt(rate * 2.4f);
            var data = new float[n];
            var rnd = new System.Random(7);
            float lp = 0f, bp = 0f;
            float[] clicks = { 0.05f, 0.62f, 1.25f, 1.31f, 1.9f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.35f;
                bp += ((white - lp) - bp) * 0.25f;
                float stroke = Mathf.Sin(t * Mathf.PI * 1.6f);
                float s = bp * Mathf.Pow(Mathf.Abs(stroke), 1.5f) * 0.55f + Mathf.Sin(2f * Mathf.PI * 1830f * t) * 0.04f * Mathf.Abs(stroke);
                foreach (float c in clicks)
                {
                    float d = t - c;
                    if (d < 0f || d > 0.12f) continue;
                    float env = Mathf.Exp(-d * 55f);
                    s += env * (Mathf.Sin(2f * Mathf.PI * 2900f * d) * 0.5f + Mathf.Sin(2f * Mathf.PI * 4300f * d) * 0.3f + Mathf.Sin(2f * Mathf.PI * 920f * d) * 0.35f);
                }
                data[i] = Mathf.Clamp(s, -1f, 1f) * 0.8f;
            }
            int fade = rate / 50;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] *= k; data[n - 1 - i] *= k; }
            loadClip = AudioClip.Create("PistolLoading", n, 1, rate, false);
            loadClip.SetData(data, 0);
            return loadClip;
        }

        // black-powder bang: sharp crack, low boom, rumbling tail (1.6 s)
        static AudioClip BangClip()
        {
            if (bangClip != null) return bangClip;
            const int rate = 22050;
            int n = Mathf.RoundToInt(rate * 1.6f);
            var data = new float[n];
            var rnd = new System.Random(3);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.12f;
                lp2 += (lp - lp2) * 0.05f;
                float crack = white * Mathf.Exp(-t * 60f);
                float boom = Mathf.Sin(2f * Mathf.PI * (55f + 40f * Mathf.Exp(-t * 8f)) * t) * Mathf.Exp(-t * 6f) * 0.9f;
                float tail = lp2 * 4f * Mathf.Exp(-t * 2.2f);
                data[i] = Mathf.Clamp(crack * 0.9f + boom + tail, -1f, 1f);
            }
            bangClip = AudioClip.Create("PistolBang", n, 1, rate, false);
            bangClip.SetData(data, 0);
            return bangClip;
        }
    }
}
