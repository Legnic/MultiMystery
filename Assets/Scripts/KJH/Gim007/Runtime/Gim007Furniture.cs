using UnityEngine;

namespace Gim007
{
    // 함께 미는 가구(서랍장). 물리 대신 "정해진 경로(시작점 → 발판)를 진행 칸만큼 보간"해서 움직인다
    // (명세서 8장 권장: 끼임 방지, 잘못된 방향으로 밀 수 없음).
    //
    // 구조 (GIM007_MasterBedroom 프리팹):
    //  Furniture (이 컴포넌트, 움직이지 않음)
    //   ├ PathStart / PathEnd  경로 양 끝 (가구 바닥 가운데 기준). 위치를 옮기면 경로가 바뀐다
    //   └ Body                 실제로 움직이는 부분: BoxCollider + 운동학 Rigidbody
    //      ├ Visual            겉모습 프리팹(Gim007_FurnitureVisual) — 모델 교체는 이것만 바꾸면 된다
    //      ├ Handle_0 / Handle_1  손잡이(Gim007Handle). 각자 StandPoint(서는 곳)·LookPoint(바라보는 곳)를 가진다
    //
    // 소리(모든 컴퓨터, 가구 위치에서): 성공 = 긁히는 소리, 실패 = 덜컹 + 거친 숨소리(+가끔 집 안 삐걱임), 완료는 발판 쪽이 담당.
    public class Gim007Furniture : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] Transform pathStart;
        [SerializeField] Transform pathEnd;
        [Tooltip("한 칸 미끄러지는 데 걸리는 시간(초). Config의 moveSeconds보다 짧게")]
        [SerializeField] float slideSeconds = 0.55f;

        [Header("소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip scrapeClip;
        [SerializeField] AudioClip failClip;
        [SerializeField] AudioClip breathClip;
        [SerializeField] AudioClip creakClip;
        [SerializeField] float volume = 0.8f;

        public Transform Body { get { return body != null ? body : transform; } }
        // 이번 프레임에 가구가 움직인 거리 (미는 플레이어를 같이 끌고 가는 데 쓴다)
        public Vector3 FrameDelta { get; private set; }
        public bool IsSliding { get; private set; }

        bool placed;

        static Gim007Controller C { get { return Gim007Controller.Instance; } }

        void OnEnable() { if (C != null) C.EventReceived += OnEvent; }
        void Start() { if (C != null) { C.EventReceived -= OnEvent; C.EventReceived += OnEvent; } }
        void OnDisable() { if (C != null) C.EventReceived -= OnEvent; }

        public Vector3 PositionAt(float t01)
        {
            if (pathStart == null || pathEnd == null) return Body.position;
            return Vector3.Lerp(pathStart.position, pathEnd.position, Mathf.Clamp01(t01));
        }

        public float StepDistance
        {
            get
            {
                if (C == null || pathStart == null || pathEnd == null) return 0.5f;
                return Vector3.Distance(pathStart.position, pathEnd.position) / Mathf.Max(1, C.Config.rules.requiredSteps);
            }
        }

        void Update()
        {
            FrameDelta = Vector3.zero;
            var c = C;
            if (c == null || body == null) return;
            Vector3 target = PositionAt(c.Progress01);
            if (pathStart != null) body.rotation = pathStart.rotation;

            // 처음(늦게 접속한 경우 포함)에는 바로 그 자리에 둔다. 그 뒤로는 한 칸씩 미끄러진다
            if (!placed) { body.position = target; placed = true; return; }
            Vector3 before = body.position;
            float speed = StepDistance / Mathf.Max(0.05f, slideSeconds);
            // 여러 칸이 한 번에 바뀌면(디버그 등) 너무 오래 걸리지 않게 빨리 따라간다
            if (Vector3.Distance(before, target) > StepDistance * 1.5f) speed *= 4f;
            body.position = Vector3.MoveTowards(before, target, speed * Time.deltaTime);
            FrameDelta = body.position - before;
            IsSliding = FrameDelta.sqrMagnitude > 0f;
        }

        void OnEvent(Gim007NetEvent e, int value)
        {
            switch (e)
            {
                case Gim007NetEvent.Success:
                    Play(scrapeClip != null ? scrapeClip : Gim007Sounds.Scrape(), volume);
                    break;
                case Gim007NetEvent.Fail:
                    Play(failClip != null ? failClip : Gim007Sounds.Thud(), volume * 0.6f);
                    Play(breathClip != null ? breathClip : Gim007Sounds.Breath(), volume * 0.7f);
                    if (C != null && Random.value < C.Config.restCreakChance)
                    {
                        // 집 안 어딘가(가구에서 6~9m 떨어진 임의의 방향)에서 작게 삐걱인다. 큰 소리 금지
                        Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                        AudioSource.PlayClipAtPoint(creakClip != null ? creakClip : Gim007Sounds.Creak(), Body.position + dir * Random.Range(6f, 9f) + Vector3.up * 2f, 0.35f);
                    }
                    break;
            }
        }

        void Play(AudioClip clip, float vol)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, Body.position + Vector3.up * 0.5f, vol);
        }
    }
}
