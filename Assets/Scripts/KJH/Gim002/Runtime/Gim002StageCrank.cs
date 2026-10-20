using UnityEngine;
using UnityEngine.InputSystem;

namespace Gim002
{
    // 2층 무대 준비 손잡이(OBJ_CRANK). 상호작용 키(E)를 crankHoldSeconds(기본 2초) 동안 누르고 있으면 커튼이 열린다.
    // 중간에 키를 떼거나 시선을 돌리면 처음부터 다시.
    //
    // LGH PlayerInteractor는 E를 "누른 순간"만 알려 주므로(Interact), 누르고 있는 시간은 여기서 직접 센다.
    // 입력은 기존 LGH 플레이어와 같은 방식(Input System의 Keyboard.current)으로 읽는다.
    // 2초를 다 채우면 판정 담당에게 "다 돌렸다"(RequestCrankCompleted)만 보낸다. 실제로 열지 말지는 판정 담당이 정한다.
    public class Gim002StageCrank : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("돌아가는 모습을 보여 줄 손잡이 부분 (비우면 회전 연출 없음)")]
        [SerializeField] Transform handle;
        [Tooltip("손잡이 회전축 (handle 로컬 기준)")]
        [SerializeField] Vector3 handleAxis = Vector3.right;
        [Tooltip("돌리는 동안 초당 회전 각도")]
        [SerializeField] float turnDegreesPerSecond = 360f;
        [Tooltip("돌리는 소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip turnClip;
        [Tooltip("손잡이를 붙잡고 있는지 확인하는 시선 거리 (LGH PlayerInteractor의 reach와 같게)")]
        [SerializeField] float reach = 2.6f;

        bool holding;
        float progress;
        float nextTick;

        static Gim002Controller C { get { return Gim002Controller.Instance; } }
        float HoldSeconds { get { return C != null ? C.Timing.crankHoldSeconds : 2f; } }
        bool Ready { get { return C != null && C.State.state == Gim002State.CurtainClosed; } }

        public bool CanInteract { get { return C != null && C.State.state >= Gim002State.Preparing && C.State.state != Gim002State.Complete; } }

        public string InteractPrompt
        {
            get
            {
                if (!Ready) return C != null && C.State.state == Gim002State.Preparing ? C.Config.msgNotReady : "무대 준비 손잡이";
                if (holding) return "손잡이를 돌리는 중... " + Mathf.RoundToInt(progress / HoldSeconds * 100f) + "%";
                return "[E 길게] 손잡이 돌리기";
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!Ready)
            {
                if (C != null && C.State.state == Gim002State.Preparing) PlayerHand.Say(C.Config.msgNotReady);
                return;
            }
            holding = true;
            progress = 0f;
        }

        void Update()
        {
            if (!holding) return;

            var kb = Keyboard.current;
            // 키를 뗐거나, 다른 곳을 보거나, 상태가 바뀌면 처음부터
            if (kb == null || !kb.eKey.isPressed || !LookingAtMe() || !Ready)
            {
                holding = false;
                progress = 0f;
                return;
            }

            progress += Time.deltaTime;
            if (handle != null) handle.Rotate(handleAxis, turnDegreesPerSecond * Time.deltaTime, Space.Self);
            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.25f;
                AudioSource.PlayClipAtPoint(turnClip != null ? turnClip : Gim002Tones.Wind(), transform.position, 0.5f);
            }

            if (progress >= HoldSeconds)
            {
                holding = false;
                progress = 0f;
                C.Net.RequestCrankCompleted();
            }
        }

        // 이 컴퓨터 플레이어가 아직 손잡이를 바라보고 있는가
        bool LookingAtMe()
        {
            Transform cam = PlayerHand.ViewCamera();
            if (cam == null) return false;
            RaycastHit hit;
            if (!Physics.Raycast(cam.position, cam.forward, out hit, reach, ~0, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.GetComponentInParent<Gim002StageCrank>() == this;
        }
    }
}
