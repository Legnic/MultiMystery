using LGH;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gim007
{
    // "이 컴퓨터 플레이어가 가구를 잡고 있는 동안"을 담당하는 로컬 전용 스크립트 (프리팹에 하나, 플레이어 프리팹은 수정하지 않음).
    //  - 잡기가 확정되면(동기화 상태에 내 ID가 손잡이에 있으면) 이동·시점을 잠그고(IPlayerLock), 손잡이 뒤 자리에 세운 뒤 손잡이를 바라보게 한다.
    //    가구가 미끄러지면 같이 따라간다. 손을 놓으면 원래대로.
    //  - 라운드마다 자기 손잡이의 키(W/A/S/D)만 화면 가운데 크게 보여 주고, 제한 시간 게이지와 상대 성공 여부를 표시한다.
    //  - 입력 판정은 이 컴퓨터 기준으로 한다: 키가 화면에 뜬 순간부터 roundSeconds 안에 맞는 키 → 성공 보고,
    //    다른 키(설정 시) 또는 시간 초과 → 실패 보고. 판정 담당은 두 보고를 모아 전진/숨고르기를 정한다.
    //  - 혼자 잡고 힘을 주면 "꿈쩍도 하지 않는다" + 힘쓰는 소리, 여러 번이면 힌트 문구 (명세서 4장).
    //  - E = 손 떼기. 손잡이를 바라보지 않아도 된다.
    //
    // 실행 순서를 PlayerInteractor(기본 0)보다 앞에 둔다: E로 놓은 바로 그 프레임에 PlayerInteractor가 손잡이를 다시 잡지 않도록
    // (ReleasedFrame을 먼저 기록해 둔다).
    [DefaultExecutionOrder(-40)]
    public class Gim007PushPlayer : MonoBehaviour
    {
        [Tooltip("혼자 힘쓸 때 소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip strainClip;
        [Tooltip("혼자 힘쓰기 반복 간격(초)")]
        [SerializeField] float strainCooldown = 0.7f;

        // E로 손을 놓은 프레임 (Gim007Handle이 같은 E로 다시 잡지 않게 확인)
        public static int ReleasedFrame { get; private set; } = -1;

        static Gim007Controller C { get { return Gim007Controller.Instance; } }

        int localHandle = -1;
        Transform body;
        CharacterController cc;
        IPlayerLock playerLock;
        Transform cam;
        Vector3 camBase;

        int roundSeen = -1;
        float roundStartTime;
        bool reported;
        Gim007RoundResult myResult;
        Gim007State lastState;
        float stateEnteredAt;

        int soloStrains;
        float nextStrainAt;
        float shakeUntil, shakeSeconds, shakeAmp;

        GUIStyle bigStyle, midStyle, smallStyle;

        public bool IsGripping { get { return localHandle >= 0; } }

        void OnEnable() { if (C != null) C.EventReceived += OnEvent; }
        void Start() { if (C != null) { C.EventReceived -= OnEvent; C.EventReceived += OnEvent; } }
        void OnDisable()
        {
            if (C != null) C.EventReceived -= OnEvent;
            EndGrip();
        }

        void Update()
        {
            var c = C;
            if (c == null) return;
            var s = c.State;
            int handle = c.LocalHandle;

            if (handle >= 0 && localHandle < 0) BeginGrip(handle, s);
            else if (handle < 0 && localHandle >= 0) EndGrip();
            if (localHandle < 0) return;

            // 내 플레이어가 사라졌으면(나가기 등) 손을 놓는다
            if (body == null || !body.gameObject.activeInHierarchy)
            {
                c.Net.RequestRelease();
                EndGrip();
                return;
            }

            if (s.state != lastState) { lastState = s.state; stateEnteredAt = Time.time; }
            if (s.state == Gim007State.Round && s.round != roundSeen)
            {
                roundSeen = s.round;
                roundStartTime = Time.time;   // 이 컴퓨터 화면에 키가 뜬 순간부터 잰다
                reported = false;
                myResult = Gim007RoundResult.Pending;
            }

            var rules = c.Config.rules;
            bool canInput = Cursor.lockState == CursorLockMode.Locked;   // 일시정지 메뉴 등이 떠 있으면 입력 없음
            var kb = Keyboard.current;

            if (canInput && kb != null && kb.eKey.wasPressedThisFrame && Gim007Handle.LastGripFrame != Time.frameCount)
            {
                ReleasedFrame = Time.frameCount;
                c.Net.RequestRelease();
                return;
            }

            Gim007Key? pressed = null;
            if (canInput && kb != null)
            {
                if (kb.wKey.wasPressedThisFrame) pressed = Gim007Key.W;
                else if (kb.aKey.wasPressedThisFrame) pressed = Gim007Key.A;
                else if (kb.sKey.wasPressedThisFrame) pressed = Gim007Key.S;
                else if (kb.dKey.wasPressedThisFrame) pressed = Gim007Key.D;
            }

            if (s.state == Gim007State.Round && !reported)
            {
                if (pressed.HasValue)
                {
                    bool? ok = Gim007Logic.JudgeKey(rules, s.Key(localHandle), pressed.Value);
                    if (ok.HasValue) Report(c, s.round, ok.Value);
                }
                if (!reported && Time.time - roundStartTime > rules.roundSeconds) Report(c, s.round, false);   // 시간 초과
            }
            else if (!s.BothGripped && pressed.HasValue)
            {
                Strain(c);   // 혼자 힘을 줘 봄
            }
        }

        void Report(Gim007Controller c, int round, bool success)
        {
            reported = true;
            myResult = success ? Gim007RoundResult.Success : Gim007RoundResult.Fail;
            c.Net.RequestReport(round, success);
        }

        void LateUpdate()
        {
            if (localHandle < 0 || body == null) return;
            var h = Gim007Handle.Find(localHandle);
            if (h == null) return;

            // 손잡이 뒤 자리에 선다 (가구가 미끄러지면 같이 따라간다).
            // 이동 잠금은 이게 실제로 담당한다: LGH PistolLoader가 매 프레임 SpeedMultiplier를 1로 되돌리므로,
            // WASD로 조금 움직였더라도 화면에 그려지기 전(LateUpdate)에 다시 이 자리로 돌려놓는다.
            Vector3 delta = h.StandPosition - body.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.000001f)
            {
                if (cc != null && cc.enabled) cc.Move(delta);
                else body.position += delta;
            }

            // 손잡이를 바라본다
            if (cam != null && playerLock != null)
            {
                Vector3 dir = h.LookPoint.position - cam.position;
                float flat = new Vector2(dir.x, dir.z).magnitude;
                if (flat > 0.01f)
                {
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float pitch = Mathf.Atan2(-dir.y, flat) * Mathf.Rad2Deg;   // + = 아래를 봄
                    playerLock.SetLookAngles(yaw, pitch);
                }
            }

            // 흔들림 (카메라 로컬 위치만 살짝, 끝나면 원위치)
            if (cam != null)
            {
                if (Time.time < shakeUntil)
                {
                    float k = (shakeUntil - Time.time) / Mathf.Max(0.01f, shakeSeconds);
                    cam.localPosition = camBase + Random.insideUnitSphere * shakeAmp * k;
                }
                else cam.localPosition = camBase;
            }
        }

        void BeginGrip(int handle, Gim007SyncState s)
        {
            localHandle = handle;
            body = Gim007Controller.LocalPlayer();
            cc = body != null ? body.GetComponent<CharacterController>() : null;
            playerLock = body != null ? body.GetComponent<IPlayerLock>() : null;
            cam = PlayerHand.ViewCamera();
            if (cam != null) camBase = cam.localPosition;
            if (playerLock != null) { playerLock.SpeedMultiplier = 0f; playerLock.LookEnabled = false; }
            roundSeen = -1;
            reported = false;
            lastState = s.state;
            stateEnteredAt = Time.time;
            if (!s.BothGripped) { nextStrainAt = 0f; Strain(C); }   // 혼자 잡음: 꿈쩍도 하지 않는다
        }

        void EndGrip()
        {
            if (localHandle < 0) return;
            localHandle = -1;
            if (playerLock != null) { playerLock.SpeedMultiplier = 1f; playerLock.LookEnabled = true; }
            if (cam != null) cam.localPosition = camBase;
            playerLock = null;
            body = null;
            cc = null;
            cam = null;
            shakeUntil = 0f;
        }

        void Strain(Gim007Controller c)
        {
            if (c == null || Time.time < nextStrainAt) return;
            nextStrainAt = Time.time + strainCooldown;
            soloStrains++;
            PlayerHand.Say(soloStrains >= c.Config.soloHintAfter ? c.Config.msgSoloHint : c.Config.msgSoloStuck, 2.5f);
            var clip = strainClip != null ? strainClip : Gim007Sounds.Strain();
            if (body != null) AudioSource.PlayClipAtPoint(clip, body.position + Vector3.up * 1.4f, 0.7f);
            Shake(c.Config.strainShake, 0.3f);
        }

        void Shake(float amp, float seconds)
        {
            shakeAmp = amp;
            shakeSeconds = seconds;
            shakeUntil = Time.time + seconds;
        }

        void OnEvent(Gim007NetEvent e, int value)
        {
            var c = C;
            if (c == null || localHandle < 0) return;
            switch (e)
            {
                case Gim007NetEvent.Success: Shake(c.Config.successShake, 0.4f); break;
                case Gim007NetEvent.Cancelled: PlayerHand.Say(c.Config.msgPartnerLeft); break;
            }
        }

        // ---------------- 화면 (IMGUI: LGH 인게임 UI와 같은 방식) ----------------

        void OnGUI()
        {
            var c = C;
            if (c == null || localHandle < 0) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;   // 일시정지·옵션 화면 위에는 그리지 않는다
            EnsureStyles();
            var v = UiScale.Apply();
            float w = v.x, h = v.y;
            var s = c.State;
            var rules = c.Config.rules;

            float cx = w / 2f, top = h * 0.16f;
            const float boxW = 300f;
            Fill(new Rect(cx - boxW / 2f, top, boxW, 190f), new Color(0f, 0f, 0f, 0.45f));

            // 진행 칸 (발판까지 남은 거리)
            int n = Mathf.Max(1, rules.requiredSteps);
            float dot = Mathf.Min(18f, (boxW - 40f) / n - 4f);
            float rowW = n * (dot + 4f) - 4f;
            for (int i = 0; i < n; i++)
                Fill(new Rect(cx - rowW / 2f + i * (dot + 4f), top + 12f, dot, 6f), i < s.step ? new Color(0.85f, 0.75f, 0.5f, 0.95f) : new Color(1f, 1f, 1f, 0.18f));

            var center = new Rect(cx - boxW / 2f, top + 26f, boxW, 110f);
            switch (s.state)
            {
                case Gim007State.Idle:
                    Label(center, c.Config.msgWaitingPartner, midStyle, new Color(0.9f, 0.9f, 0.9f));
                    break;
                case Gim007State.Ready:
                    Label(center, c.Config.msgReady, midStyle, Color.white);
                    break;
                case Gim007State.Round:
                {
                    Color keyCol = myResult == Gim007RoundResult.Success ? new Color(0.55f, 0.95f, 0.55f)
                                 : myResult == Gim007RoundResult.Fail ? new Color(1f, 0.45f, 0.4f) : Color.white;
                    var keyRect = new Rect(cx - 50f, top + 30f, 100f, 100f);
                    Fill(keyRect, new Color(1f, 1f, 1f, 0.1f));
                    Label(keyRect, s.Key(localHandle).ToString(), bigStyle, keyCol);
                    float left = myResult == Gim007RoundResult.Pending ? 1f - (Time.time - roundStartTime) / rules.roundSeconds : 0f;
                    Gauge(new Rect(cx - 110f, top + 140f, 220f, 8f), Mathf.Clamp01(left), new Color(0.95f, 0.85f, 0.6f));
                    break;
                }
                case Gim007State.Moving:
                    Label(center, "밀었다!", midStyle, new Color(0.7f, 0.95f, 0.7f));
                    break;
                case Gim007State.Resting:
                    Label(center, c.Config.msgResting, midStyle, new Color(0.85f, 0.85f, 0.9f));
                    Gauge(new Rect(cx - 110f, top + 140f, 220f, 8f), Mathf.Clamp01((Time.time - stateEnteredAt) / Mathf.Max(0.01f, rules.restSeconds)), new Color(0.6f, 0.7f, 0.9f));
                    break;
            }

            // 상대 상태
            string partner;
            int other = 1 - localHandle;
            if (s.Grip(other) == Gim007SyncState.NoPlayer) partner = "상대: 아직 안 잡음";
            else if (s.state != Gim007State.Round) partner = "상대: 잡고 있음";
            else
            {
                var r = s.Result(other);
                partner = r == Gim007RoundResult.Success ? "상대: 성공" : r == Gim007RoundResult.Fail ? "상대: 실패" : "상대: 입력 중…";
            }
            Label(new Rect(cx - boxW / 2f, top + 152f, boxW, 18f), partner, smallStyle, new Color(0.85f, 0.85f, 0.85f));
            Label(new Rect(cx - boxW / 2f, top + 170f, boxW, 18f), c.Config.msgReleasePrompt, smallStyle, new Color(0.65f, 0.65f, 0.65f));
            GUI.color = Color.white;
        }

        void EnsureStyles()
        {
            if (bigStyle != null) return;
            bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 64, fontStyle = FontStyle.Bold };
            midStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
        }

        static void Fill(Rect r, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Gauge(Rect r, float t01, Color col)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.15f));
            Fill(new Rect(r.x, r.y, r.width * t01, r.height), col);
        }

        static void Label(Rect r, string text, GUIStyle style, Color col)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(r.x + 1f, r.y + 2f, r.width, r.height), text, style);
            GUI.color = col;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }
    }
}
