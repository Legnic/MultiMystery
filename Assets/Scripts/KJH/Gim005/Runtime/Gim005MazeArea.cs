using System.Collections;
using Unity.Netcode.Components;
using UnityEngine;

namespace Gim005
{
    // 1층 덤불 정원 쪽 담당 (GIM005_Maze 프리팹 루트에 붙음).
    //  - 입구(OBJ_MAZE_ENTRANCE): Ready 상태에서 이 컴퓨터 플레이어가 입구 상자 안에 들어오면 타이머 시작 요청.
    //    물리 트리거 대신 위치를 매 프레임 확인한다 (플레이어 쪽 Rigidbody/태그 설정에 의존하지 않도록).
    //  - 시간 초과(OBJ_MAZE_START): 미로 안에 있던 플레이어 화면이 흐려지고(약 2초) 입구 밖 시작점으로 옮겨진다.
    //  - 종소리(30초 전)·오답 경고음: 미로 안에 있는 플레이어에게만 들린다 (A 쪽 알림).
    // 상자 범위는 이 오브젝트 기준 로컬 좌표. 씬 뷰에서 노란 상자(미로)·초록 상자(입구)로 보인다.
    public class Gim005MazeArea : MonoBehaviour
    {
        [Header("범위 (로컬 좌표)")]
        [SerializeField] Vector3 areaCenter = new Vector3(0f, 1.5f, 0f);
        [SerializeField] Vector3 areaSize = new Vector3(16f, 4f, 16f);
        [SerializeField] Vector3 entranceCenter = new Vector3(0f, 1.2f, 7.2f);
        [SerializeField] Vector3 entranceSize = new Vector3(2.2f, 2.6f, 1.2f);

        [Header("시작(복귀) 지점")]
        [Tooltip("시간 초과 시 이 위치·방향으로 옮겨진다 (입구 밖)")]
        [SerializeField] Transform startPoint;

        [Header("소리 (비우면 임시 소리)")]
        [Tooltip("2D 소리용 (미로 안 플레이어에게만 재생)")]
        [SerializeField] AudioSource source2D;
        [SerializeField] AudioClip bellClip;
        [SerializeField] AudioClip wrongClip;

        [Header("흐려짐 연출")]
        [SerializeField] Color fogColor = new Color(0.04f, 0.05f, 0.06f, 0.92f);

        float nextEnterRequest;
        float fogAlpha;
        string fogText;
        bool busy;
        static Texture2D white;
        GUIStyle style;

        public Transform StartPoint { get { return startPoint; } }

        void Start()
        {
            var c = Gim005Controller.Instance;
            if (c != null) c.EventReceived += OnEvent;
        }

        void OnDestroy()
        {
            var c = Gim005Controller.Instance;
            if (c != null) c.EventReceived -= OnEvent;
        }

        bool InBox(Vector3 worldPos, Vector3 center, Vector3 size)
        {
            Vector3 p = transform.InverseTransformPoint(worldPos) - center;
            return Mathf.Abs(p.x) <= size.x * 0.5f && Mathf.Abs(p.y) <= size.y * 0.5f && Mathf.Abs(p.z) <= size.z * 0.5f;
        }

        // 이 컴퓨터 플레이어가 미로(입구 칸 포함) 안에 있는가
        public bool LocalPlayerInside()
        {
            var p = Gim005Controller.LocalPlayer();
            return p != null && (InBox(p.position, areaCenter, areaSize) || InBox(p.position, entranceCenter, entranceSize));
        }

        void Update()
        {
            var c = Gim005Controller.Instance;
            if (c == null || c.Net == null || busy) return;
            if (c.State.state != Gim005State.Ready || Time.time < nextEnterRequest) return;
            var p = Gim005Controller.LocalPlayer();
            if (p == null || !InBox(p.position, entranceCenter, entranceSize)) return;
            nextEnterRequest = Time.time + 0.5f;
            c.Net.RequestEnter();
        }

        void OnEvent(Gim005NetEvent e, int value)
        {
            var c = Gim005Controller.Instance;
            bool inside = LocalPlayerInside();
            switch (e)
            {
                case Gim005NetEvent.Started:
                    if (inside) PlayerHand.Say(c.Config.msgStarted, 3f);
                    break;
                case Gim005NetEvent.Bell:
                    if (inside) Play(bellClip != null ? bellClip : Gim002.Gim002Tones.Chord("gim005_bell", 2.5f, 523.3f, 1046.5f, 1568f));
                    break;
                case Gim005NetEvent.Wrong:
                    if (inside) Play(wrongClip != null ? wrongClip : Gim002.Gim002Tones.Warning());
                    break;
                case Gim005NetEvent.TimeOut:
                    if (inside && !busy) StartCoroutine(SendBack(c.Rules.timeoutFxSeconds, c.Config.msgTimeOut));
                    break;
                case Gim005NetEvent.AttemptReset:
                    if (inside && !busy) StartCoroutine(SendBack(0.6f, null));
                    break;
            }
        }

        void Play(AudioClip clip)
        {
            if (source2D != null && clip != null) source2D.PlayOneShot(clip);
        }

        // 화면이 흐려지고 → 시작점으로 이동 → 다시 밝아진다. 그동안 이동·시점을 잠근다.
        IEnumerator SendBack(float seconds, string message)
        {
            busy = true;
            fogText = message;
            var player = Gim005Controller.LocalPlayer();
            var lockable = player != null ? player.GetComponent<IPlayerLock>() : null;
            if (lockable != null) { lockable.SpeedMultiplier = 0f; lockable.LookEnabled = false; }

            // 덤불이 다시 자라기(= 다음 시도 Ready) 전에 옮겨야 하므로 연출 시간의 75% 지점에서 이동
            float fadeIn = Mathf.Max(0.1f, seconds * 0.75f);
            for (float t = 0f; t < fadeIn; t += Time.deltaTime)
            {
                fogAlpha = t / fadeIn;
                yield return null;
            }
            fogAlpha = 1f;
            if (player != null && startPoint != null) Teleport(player, startPoint.position, startPoint.eulerAngles.y);
            yield return new WaitForSeconds(0.25f);

            if (lockable != null) { lockable.SpeedMultiplier = 1f; lockable.LookEnabled = true; }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                fogAlpha = 1f - t / 0.6f;
                yield return null;
            }
            fogAlpha = 0f;
            fogText = null;
            busy = false;
        }

        // 플레이어 순간 이동. CharacterController는 꺼 두지 않으면 다음 Move()에서 원래 위치로 되돌리고,
        // 소유자 권한 NetworkTransform은 Teleport로 옮겨야 상대 화면에서 미끄러지듯 보간되지 않는다.
        public static void Teleport(Transform player, Vector3 pos, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var cc = player.GetComponent<CharacterController>();
            bool ccWas = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            var nt = player.GetComponent<NetworkTransform>();
            if (nt != null && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(pos, rot, player.localScale);
            else player.SetPositionAndRotation(pos, rot);

            if (cc != null) cc.enabled = ccWas;
            var lockable = player.GetComponent<IPlayerLock>();
            if (lockable != null) lockable.SetLookAngles(yaw, 0f);
        }

        void OnGUI()
        {
            if (fogAlpha <= 0f) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            var col = fogColor;
            col.a *= fogAlpha;
            GUI.color = col;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            if (!string.IsNullOrEmpty(fogText))
            {
                if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
                GUI.color = new Color(0.85f, 0.85f, 0.85f, fogAlpha);
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40f), fogText, style);
            }
            GUI.color = Color.white;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(areaCenter, areaSize);
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(entranceCenter, entranceSize);
        }
    }
}
