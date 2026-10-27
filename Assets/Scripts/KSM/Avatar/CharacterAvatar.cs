using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoopDemo
{
    // 네트워크 플레이어 프리팹(NetworkPlayer_LGH)의 "Avatar" 오브젝트에 붙는다.
    //
    //  - 호스트 플레이어(1층)는 항상 주황이, 게스트 플레이어(2층)는 항상 파랑이를 쓴다.
    //    (OwnerClientId == ServerClientId 인 쪽이 호스트. 양쪽 컴퓨터가 같은 규칙으로 정하므로 항상 같은 모습이다.)
    //  - 예전 큐브 임시 아바타(LGH.PlayerAvatar)는 숨기고 그 자리에 모델을 만든다.
    //  - 말하는 중(Vivox)이면 입을 움직인다. 내 것은 SelfSpeaking, 상대 것은 RemoteSpeaking.
    //  - E로 상호작용하면 왼손을 뻗는 Animator 동작이 재생되고, 상대 컴퓨터에도 같은 동작이 재생된다
    //    (소유자가 NetworkVariable 카운터를 올리면 다른 쪽이 변화를 보고 재생).
    public class CharacterAvatar : NetworkBehaviour
    {
        [SerializeField] CharacterAvatarModel orangeModel;
        [SerializeField] CharacterAvatarModel blueModel;
        [Tooltip("모델 전체 크기 배율 (원본 키 약 1.04m → 캐릭터 컨트롤러 높이 1.53m에 맞춘다)")]
        [SerializeField] float modelScale = 1.5f;

        readonly NetworkVariable<byte> interactCount = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // 웅크림 여부 — 소유자(FirstPersonController.IsCrouching)가 쓰고, 다 같이 읽어서 모델 자세에 반영한다.
        readonly NetworkVariable<bool> crouching = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // 감정표현(1~4키: 인사/브이/따봉/가리키기) — interactCount와 같은 패턴.
        // 아래 4비트 = 몸짓 번호(0~3), 위 4비트 = 같은 몸짓을 또 눌러도 값이 바뀌도록 하는 카운터.
        readonly NetworkVariable<byte> emoteSignal = new NetworkVariable<byte>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        byte emoteCounter;

        public CharacterAvatarModel Model { get; private set; }
        public bool IsHostCharacter { get; private set; }

        LGH.PlayerInteractor interactor;
        LGH.FirstPersonController firstPerson;
        Transform cameraTransform;
        bool lastCrouchSent;
        EmoteHandView emoteHand;   // "EmoteHand" 오브젝트(카메라 자식, Hand_Wave_Rigged) - 내 것은 그대로 1인칭, 상대 것은 얼굴 앞 소켓으로 옮겨 붙인다

        // ---- 재접속 위치 기억 (서버 컴퓨터에만 쌓인다) ----
        // 자리(PlayerSlot)별로 "마지막으로 있던 곳"을 기억해 두었다가, 같은 자리 사람이 다시 들어오면 거기로 돌려보낸다.
        // 호스트 컴퓨터가 방을 새로 시작하면(호스트 본인 플레이어가 스폰되면) 비운다.
        struct SavedSpot { public Vector3 position; public float yaw, pitch; }
        static readonly System.Collections.Generic.Dictionary<int, SavedSpot> savedSpots = new System.Collections.Generic.Dictionary<int, SavedSpot>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { savedSpots.Clear(); }

        public override void OnNetworkSpawn()
        {
            IsHostCharacter = OwnerClientId == NetworkManager.ServerClientId;
            SpawnModel(IsHostCharacter ? orangeModel : blueModel);

            interactCount.OnValueChanged += OnInteractCountChanged;
            crouching.OnValueChanged += OnCrouchChanged;
            emoteSignal.OnValueChanged += OnEmoteSignalChanged;
            if (IsOwner)
            {
                interactor = GetComponentInParent<LGH.PlayerInteractor>();
                if (interactor != null) interactor.Interacted += OnLocalInteract;
                firstPerson = GetComponentInParent<LGH.FirstPersonController>();
            }
            var cam = GetComponentInParent<NetworkObject>().GetComponentInChildren<Camera>(true);
            cameraTransform = cam != null ? cam.transform : null;
            emoteHand = GetComponentInParent<NetworkObject>().GetComponentInChildren<EmoteHandView>(true);

            // 상대 화면에서는 촛불이 얼굴 앞(카메라 기준 위치)이 아니라 모델의 손에 들려 보이게 옮긴다.
            // 내 촛불은 그대로 1인칭 카메라 앞에 둔다 → "1인칭 촛불"과 "모델이 든 촛불"이 서로 다른 모습으로 따로 쓰인다.
            // 감정표현 손(EmoteHand)도 같은 방식: 내 화면은 1인칭 그대로, 상대 화면에서는 모델 얼굴 앞으로 옮겨 붙인다.
            if (!IsOwner && Model != null)
            {
                var candle = GetComponentInParent<NetworkObject>().GetComponentInChildren<LGH.HeldCandle>(true);
                if (candle != null) Model.AttachHandCandle(candle.transform);
                if (emoteHand != null) Model.AttachFaceItem(emoteHand.transform);
            }

            if (IsServer)
            {
                if (IsOwner) savedSpots.Clear();                    // 호스트가 방을 새로 시작했다
                else StartCoroutine(SendSavedSpotWhenReady());      // 다시 들어온 사람이면 나갔던 곳으로
            }
        }

        public override void OnNetworkDespawn()
        {
            interactCount.OnValueChanged -= OnInteractCountChanged;
            crouching.OnValueChanged -= OnCrouchChanged;
            emoteSignal.OnValueChanged -= OnEmoteSignalChanged;
            if (interactor != null) interactor.Interacted -= OnLocalInteract;
            interactor = null;
            // 방에 남은 사람(호스트)이 있는 동안 나간 사람의 마지막 위치를 기억한다
            if (IsServer && !IsOwner && NetworkManager != null && NetworkManager.IsListening) RememberSpot();
            if (Model != null) { Destroy(Model.gameObject); Model = null; }
        }

        int Slot { get { return PlayerSlot.Of(OwnerClientId); } }

        void RememberSpot()
        {
            var root = NetworkObject.transform;
            float pitch = 0f;
            if (cameraTransform != null) { pitch = cameraTransform.localEulerAngles.x; if (pitch > 180f) pitch -= 360f; }
            savedSpots[Slot] = new SavedSpot { position = root.position, yaw = root.eulerAngles.y, pitch = pitch };
        }

        // 새 플레이어 오브젝트가 상대 컴퓨터에 만들어지기 전에 RPC가 도착하지 않도록 잠깐 기다렸다가 보낸다
        System.Collections.IEnumerator SendSavedSpotWhenReady()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            if (!IsSpawned) yield break;
            if (savedSpots.TryGetValue(Slot, out var spot)) RestoreSpotRpc(spot.position, spot.yaw, spot.pitch);
        }

        // 서버 → 이 플레이어(소유자)에게만: 나갔던 곳으로 돌아가라
        [Rpc(SendTo.Owner)]
        void RestoreSpotRpc(Vector3 position, float yaw, float pitch)
        {
            ApplySpot(position, yaw, pitch);
        }

        public void ApplySpot(Vector3 position, float yaw, float pitch)
        {
            var root = NetworkObject.transform;
            var cc = root.GetComponent<CharacterController>();
            var nt = root.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            var fpc = root.GetComponent<LGH.FirstPersonController>();
            bool ccWas = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;   // CharacterController가 켜져 있으면 다음 Move()에서 옛 위치로 되돌린다
            var rot = Quaternion.Euler(0f, yaw, 0f);
            if (nt != null && IsOwner) nt.Teleport(position, rot, root.localScale);
            else root.SetPositionAndRotation(position, rot);
            if (fpc != null) fpc.SetLookAngles(yaw, pitch);
            if (cc != null) cc.enabled = ccWas;
        }

        // 테스트/디버그용: 지금 기억된 자리 (없으면 false)
        public static bool TryGetSavedSpot(int slot, out Vector3 position)
        {
            if (savedSpots.TryGetValue(slot, out var s)) { position = s.position; return true; }
            position = default;
            return false;
        }

        void SpawnModel(CharacterAvatarModel prefab)
        {
            if (prefab == null) { Debug.LogWarning("[CharacterAvatar] 모델 프리팹이 연결되지 않았어요: " + name); return; }
            if (Model != null) Destroy(Model.gameObject);

            // 예전 임시 큐브 아바타 숨기기 (같은 오브젝트 아래의 렌더러 전부 + 흔들기 스크립트)
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var legacy = GetComponent<LGH.PlayerAvatar>();
            if (legacy != null) legacy.enabled = false;

            Model = Instantiate(prefab, transform);
            Model.name = prefab.name;
            Model.transform.localPosition = Vector3.zero;
            Model.transform.localRotation = Quaternion.identity;
            Model.transform.localScale = Vector3.one * modelScale;

            // 내 몸은 1인칭 카메라를 가리지 않게 그림자만, 상대 몸은 그대로 보이게
            foreach (var r in Model.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = IsOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        void OnLocalInteract()
        {
            if (Model != null) Model.TriggerInteract();
            interactCount.Value = (byte)(interactCount.Value + 1);
        }

        void OnInteractCountChanged(byte before, byte now)
        {
            if (IsOwner || Model == null) return;
            Model.TriggerInteract();
        }

        void OnCrouchChanged(bool before, bool now)
        {
            if (Model != null) Model.SetCrouching(now);
        }

        // 0=인사 1=브이 2=따봉 3=가리키기. EmoteWheelUI(마우스 가운데 버튼 도넛 메뉴)가 고른 번호를 넘겨준다.
        // 누른 사람 화면엔 바로(1인칭), 상대 화면엔 emoteSignal을 보고 재생한다.
        public void RequestEmote(int gesture)
        {
            if (emoteHand == null || gesture < 0) return;
            emoteHand.Play(gesture);                                      // 내 화면: 바로 재생
            emoteCounter++;
            emoteSignal.Value = (byte)(gesture | (emoteCounter << 4));    // 상대 화면: 값이 바뀌는 걸 보고 재생
        }

        void OnEmoteSignalChanged(byte before, byte now)
        {
            if (IsOwner || emoteHand == null) return;   // 소유자는 위에서 이미 직접 재생했다
            emoteHand.Play(now & 0x0F);
        }

        void Update()
        {
            if (Model == null) return;

            var voice = VoiceChatManager.Instance;
            bool speaking = voice != null && (IsOwner ? voice.SelfSpeaking : voice.RemoteSpeaking);
            Model.SetSpeaking(speaking);

            // 내가 웅크리고 있으면(로컬 FirstPersonController) 바뀐 순간에만 서버에 알려서 상대 화면에도 반영한다
            if (IsOwner && firstPerson != null)
            {
                bool c = firstPerson.IsCrouching;
                if (c != lastCrouchSent) { crouching.Value = c; lastCrouchSent = c; }
            }

            if (cameraTransform != null)
            {
                float pitch = cameraTransform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;   // 위쪽은 음수, 아래쪽은 양수
                Model.SetHeadPitch(pitch);
            }
        }
    }
}
