using System.Collections;
using UnityEngine;

namespace Gim002
{
    // 인형 공연 연출. 인형(Gim002StageItem)과 같은 오브젝트에 붙인다.
    //  - 반주 정답 → 상태가 Perform이 되면, 무대에 꽂힌 인형이 장면 데이터의 공연 트리거(예: ANIM_OWL_LOOK)를 실행
    //  - 반주 오답(EV_PIANO_WRONG) → 꽂힌 인형이 고개를 젓는다
    // Animator가 연결돼 있으면 트리거를 보내고, 없으면(아트 전) 간단한 흔들기/깡충 동작으로 대신한다.
    public class Gim002Puppet : MonoBehaviour
    {
        [Tooltip("공연 애니메이션용 Animator (없으면 임시 동작)")]
        [SerializeField] Animator animator;
        [Tooltip("고개 젓기 Animator 트리거 이름")]
        [SerializeField] string shakeTrigger = "ANIM_SHAKE_HEAD";
        [Tooltip("임시 동작에서 움직일 부분 (비우면 이 오브젝트의 첫 자식)")]
        [SerializeField] Transform body;

        Gim002StageItem item;
        Coroutine acting;
        Vector3 bodyPos;
        Quaternion bodyRot;

        void Awake()
        {
            item = GetComponent<Gim002StageItem>();
            if (body == null && transform.childCount > 0) body = transform.GetChild(0);
            if (body != null) { bodyPos = body.localPosition; bodyRot = body.localRotation; }
        }

        void Start()
        {
            var c = Gim002Controller.Instance;
            if (c == null) return;
            c.StateChanged += OnState;
            c.EventReceived += OnEvent;
        }

        void OnDestroy()
        {
            var c = Gim002Controller.Instance;
            if (c == null) return;
            c.StateChanged -= OnState;
            c.EventReceived -= OnEvent;
        }

        bool OnStage { get { return item != null && item.Kind == StageSlotKind.Doll && item.InSyncedSlot(); } }

        void OnState(Gim002SyncState prev, Gim002SyncState now)
        {
            if (now.state != Gim002State.Perform || prev.state == Gim002State.Perform || !OnStage) return;
            var c = Gim002Controller.Instance;
            var scene = c.Config.GetScene(now.sceneIndex);
            string trigger = scene != null ? scene.performTrigger : "";
            if (animator != null && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
            else Play(Perform(c.Timing.performSeconds));
            Debug.Log("[GIM-002] 공연: " + item.DisplayName + " (" + trigger + ")", this);
        }

        void OnEvent(Gim002NetEvent e, int value)
        {
            if (e != Gim002NetEvent.PianoWrong || !OnStage) return;
            if (animator != null && !string.IsNullOrEmpty(shakeTrigger)) animator.SetTrigger(shakeTrigger);
            else Play(Shake(Gim002Controller.Instance.Timing.pianoWrongSeconds));
        }

        void Play(IEnumerator routine)
        {
            if (body == null || !isActiveAndEnabled) return;
            if (acting != null) StopCoroutine(acting);
            acting = StartCoroutine(routine);
        }

        // 임시 공연: 깡충깡충 뛰며 한 바퀴 돈다
        IEnumerator Perform(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                body.localPosition = bodyPos + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 6f)) * 0.06f;
                body.localRotation = bodyRot * Quaternion.Euler(0f, k * 360f, 0f);
                yield return null;
            }
            ResetPose();
        }

        // 임시 고개 젓기: 좌우로 빠르게 흔든다
        IEnumerator Shake(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                body.localRotation = bodyRot * Quaternion.Euler(0f, Mathf.Sin(t * 30f) * 20f * (1f - t / seconds), 0f);
                yield return null;
            }
            ResetPose();
        }

        void ResetPose()
        {
            if (body == null) return;
            body.localPosition = bodyPos;
            body.localRotation = bodyRot;
            acting = null;
        }
    }
}
