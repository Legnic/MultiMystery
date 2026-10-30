using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace LGH
{
    // On the network player prefab. The owner gets movement, camera, sound and door controls;
    // the other player's copy is just a body carrying a candle.
    // Each player also has a nickname (set in the room menu) that the other player sees above their head.
    public class NetworkPlayer : NetworkBehaviour
    {
        public static NetworkPlayer Local { get; private set; }
        public static readonly List<NetworkPlayer> All = new List<NetworkPlayer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); Local = null; }

        [Tooltip("Components only the owner should run (FirstPersonController, PlayerInteractor, Camera, AudioListener).")]
        public Behaviour[] ownerOnly;
        [Tooltip("Own body renderers: hidden for the owner (shadow only), visible for the other player.")]
        public Renderer[] body;
        public Vector3[] spawnPoints = { new Vector3(43f, 0.05f, 14.6f), new Vector3(45f, 0.05f, 14.6f) };
        public float spawnYaw = 180f;
        [Tooltip("How far away the other player's name is still shown (meters).")]
        public float nameDistance = 14f;

        // the owner writes its own name, everyone reads it
        readonly NetworkVariable<FixedString32Bytes> nickname = new NetworkVariable<FixedString32Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        CharacterController controller;
        GUIStyle nameStyle, shadowStyle;

        public string Nickname
        {
            get
            {
                string n = IsSpawned ? nickname.Value.ToString() : "";
                return string.IsNullOrWhiteSpace(n) ? "플레이어 " + (OwnerClientId + 1) : n;
            }
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            SetOwnerParts(false);
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public override void OnNetworkSpawn()
        {
            bool mine = IsOwner;
            SetOwnerParts(mine);
            foreach (var r in body)
                if (r != null) r.shadowCastingMode = mine ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            if (controller != null) controller.enabled = mine;
            // performance: only your own candle casts shadows; other players' candles just light the room
            if (!mine) foreach (var cl in GetComponentsInChildren<Light>(true)) cl.shadows = LightShadows.None;
            nickname.OnValueChanged += OnNicknameChanged;
            RefreshName();
            var avatar = GetComponentInChildren<PlayerAvatar>(true);
            if (avatar != null) avatar.SetSlot(CoopDemo.PlayerSlot.Of(OwnerClientId));

            if (mine)
            {
                Local = this;
                SetNickname(MultiplayerMenu.Nickname);
                // 호스트 = 0번 자리, 그 외 = 1번 자리. (clientId % N 으로 정하면 게스트가 다시 들어올 때 호스트 자리가 된다)
                int slot = Mathf.Min(CoopDemo.PlayerSlot.Of(OwnerClientId), Mathf.Max(0, spawnPoints.Length - 1));
                if (controller != null) controller.enabled = false;
                // 씬에 PlayerSpawnPoint가 있으면 그 위치(방별 시작점)를 우선 사용
                Vector3 pos = spawnPoints[slot];
                float yaw = spawnYaw;
                if (PlayerSpawnPoint.TryGet(slot, out var spawnPos, out var spawnRot)) { pos = spawnPos; yaw = spawnRot; }
                transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                if (controller != null) controller.enabled = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            nickname.OnValueChanged -= OnNicknameChanged;
            if (Local == this) Local = null;
        }

        // 소유자의 실제 착지 접촉을 사용한다. 로컬은 즉시 재생하고 나머지 클라이언트에만 알린다.
        public void ReportJumpLanding()
        {
            if (IsSpawned && !IsOwner) return;
            LcwSoundDirector.PlayJumpLanding(transform.position);
            if (IsSpawned) JumpLandingRpc(transform.position);
        }

        [Rpc(SendTo.NotOwner, RequireOwnership = true)]
        void JumpLandingRpc(Vector3 position)
        {
            LcwSoundDirector.PlayJumpLanding(position);
        }

        // owner only: change the name everyone sees
        public void SetNickname(string value)
        {
            if (!IsSpawned || !IsOwner) return;
            nickname.Value = ToFixed(value);
        }

        void OnNicknameChanged(FixedString32Bytes before, FixedString32Bytes now) { RefreshName(); }

        void RefreshName() { gameObject.name = (IsOwner ? "Player (나) " : "Player (상대) ") + Nickname; }

        // FixedString32Bytes holds 29 bytes of UTF-8 (about 9 Korean letters); cut longer names cleanly
        static FixedString32Bytes ToFixed(string s)
        {
            s = string.IsNullOrEmpty(s) ? "" : s.Trim();
            while (s.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(s) > 29) s = s.Substring(0, s.Length - 1);
            return new FixedString32Bytes(s);
        }

        void SetOwnerParts(bool on)
        {
            if (ownerOnly == null) return;
            foreach (var b in ownerOnly) if (b != null) b.enabled = on;
        }

        // the other player's name floats above their head (hidden behind walls)
        void OnGUI()
        {
            if (!IsSpawned || IsOwner || Local == null) return;
            if (Cursor.lockState != CursorLockMode.Locked && !PlayerInventory.UiOpen) return; // pause menu is up
            var cam = Local.GetComponentInChildren<Camera>();
            if (cam == null || !cam.isActiveAndEnabled) return;
            float height = controller != null ? controller.height : 1.8f;
            Vector3 head = transform.position + Vector3.up * (height + 0.3f);
            Vector3 from = cam.transform.position;
            float dist = Vector3.Distance(from, head);
            if (dist > nameDistance) return;
            Vector3 sp = cam.WorldToScreenPoint(head);
            if (sp.z <= 0f) return;
            RaycastHit hit;
            if (Physics.Linecast(from, head, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform)) return;

            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                nameStyle.normal.textColor = new Color(1f, 0.93f, 0.78f);
                shadowStyle = new GUIStyle(nameStyle);
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            nameStyle.fontSize = shadowStyle.fontSize = Mathf.RoundToInt(18f * scale);
            var r = new Rect(sp.x - 150f, Screen.height - sp.y - 16f * scale, 300f, 32f * scale);
            string n = Nickname;
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), n, shadowStyle);
            GUI.Label(r, n, nameStyle);
            GUI.matrix = old;
        }
    }
}
