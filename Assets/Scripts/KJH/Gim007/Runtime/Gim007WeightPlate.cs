using UnityEngine;

namespace Gim007
{
    // 비밀문(드레스룸 양문) 옆 무게 발판.
    //  - 가구가 발판에 도달(완료)하면 발판 윗판이 내려앉고 "철컥" + 문이 천천히 열리는 소리, 모두에게 완료 문구.
    //  - 사람이 올라가면 덜컥거리다 만다 (2026-10-23 결정): 문 쪽에서 작은 덜컥 소리 + "무게가 부족하다" 문구, 문은 열리지 않는다.
    //    → "사람 대신 가구로 눌러야 한다"는 동기를 주고, 한 명이 발판을 밟고 다른 한 명만 지나가는 우회를 막는다.
    //    사람 감지는 각 컴퓨터가 "자기 플레이어"만 확인한다 (동기화 불필요, 연출 전용).
    //
    // 모델 교체: 자식 "Visual"(Gim007_PlateVisual 프리팹)만 바꾸면 된다. 그 안에서 내려앉는 부분의 이름을 plateTopName으로 맞추면
    //  자동으로 찾는다 (없으면 내려앉는 연출만 빠진다).
    public class Gim007WeightPlate : MonoBehaviour
    {
        [Tooltip("내려앉는 윗판. 비우면 자식 중 plateTopName 이름을 찾는다")]
        [SerializeField] Transform plateTop;
        [SerializeField] string plateTopName = "PlateTop";
        [Tooltip("사람 감지 영역 (이 오브젝트 기준 로컬 크기, 바닥면 가운데 기준)")]
        [SerializeField] Vector3 areaSize = new Vector3(2.1f, 1f, 1.1f);
        [Tooltip("완료 시 내려앉는 깊이(m)")]
        [SerializeField] float pressDepth = 0.035f;

        [Header("소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip rattleClip;
        [SerializeField] AudioClip clankClip;
        [SerializeField] AudioClip doorOpenClip;

        static Gim007Controller C { get { return Gim007Controller.Instance; } }

        Vector3 topRest;
        bool hasTop;
        bool localInside;
        float nextRattleAt;
        float rattleUntil;

        void Awake()
        {
            if (plateTop == null && !string.IsNullOrEmpty(plateTopName))
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == plateTopName) { plateTop = t; break; }
            hasTop = plateTop != null;
            if (hasTop) topRest = plateTop.localPosition;
        }

        void OnEnable() { if (C != null) C.EventReceived += OnEvent; }
        void Start() { if (C != null) { C.EventReceived -= OnEvent; C.EventReceived += OnEvent; } }
        void OnDisable() { if (C != null) C.EventReceived -= OnEvent; }

        void Update()
        {
            var c = C;
            if (c == null) return;

            bool inside = LocalPlayerInside();
            if (inside && !localInside && !c.IsComplete && Time.time >= nextRattleAt) Rattle(c);
            localInside = inside;

            if (!hasTop) return;
            float depth = 0f;
            if (c.IsComplete) depth = pressDepth;
            else if (Time.time < rattleUntil) depth = pressDepth * 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 70f));   // 덜컥덜컥
            Vector3 target = topRest + Vector3.down * depth;
            plateTop.localPosition = Vector3.MoveTowards(plateTop.localPosition, target, 0.2f * Time.deltaTime + (c.IsComplete ? 0f : 0.05f));
        }

        bool LocalPlayerInside()
        {
            var p = Gim007Controller.LocalPlayer();
            if (p == null) return false;
            Vector3 l = transform.InverseTransformPoint(p.position);
            return Mathf.Abs(l.x) <= areaSize.x * 0.5f && Mathf.Abs(l.z) <= areaSize.z * 0.5f && l.y > -0.3f && l.y < areaSize.y;
        }

        void Rattle(Gim007Controller c)
        {
            nextRattleAt = Time.time + c.Config.plateRattleCooldown;
            rattleUntil = Time.time + 0.45f;
            var clip = rattleClip != null ? rattleClip : Gim007Sounds.Rattle();
            AudioSource.PlayClipAtPoint(clip, transform.position, 0.6f);
            var door = c.DoorPosition;
            if (door.HasValue) AudioSource.PlayClipAtPoint(clip, door.Value, 0.5f);   // 문 쪽에서도 덜컥 → 발판과 문이 이어져 있다는 단서
            PlayerHand.Say(c.Config.msgPlateRattle, 2.5f);
        }

        void OnEvent(Gim007NetEvent e, int value)
        {
            if (e != Gim007NetEvent.Complete) return;
            var c = C;
            AudioSource.PlayClipAtPoint(clankClip != null ? clankClip : Gim007Sounds.Clank(), transform.position + Vector3.up * 0.2f, 0.8f);
            var door = c != null ? c.DoorPosition : null;
            if (door.HasValue) AudioSource.PlayClipAtPoint(doorOpenClip != null ? doorOpenClip : Gim007Sounds.DoorOpen(), door.Value, 0.7f);
            if (c != null) PlayerHand.Say(c.Config.msgComplete, 4f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, areaSize.y * 0.5f, 0f), areaSize);
        }
    }
}
