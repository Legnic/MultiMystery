using UnityEngine;

namespace Gim005
{
    // 중앙 광장 버튼 (OBJ_CENTER_BUTTON). 미로 진행 중(Running)에 A가 바라보고 E → 기믹 완료 요청.
    public class Gim005CenterButton : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("눌릴 때 내려가는 부분 (비우면 이 오브젝트)")]
        [SerializeField] Transform cap;
        [SerializeField] float pressDepth = 0.03f;
        [SerializeField] AudioSource source;

        Vector3 capRest;

        void Awake()
        {
            if (cap == null) cap = transform;
            capRest = cap.localPosition;
        }

        void Update()
        {
            var c = Gim005Controller.Instance;
            bool down = c != null && c.IsComplete;
            cap.localPosition = down ? capRest + Vector3.down * pressDepth : capRest;
        }

        public bool CanInteract
        {
            get { var c = Gim005Controller.Instance; return c != null && !c.IsComplete; }
        }

        public string InteractPrompt
        {
            get
            {
                var c = Gim005Controller.Instance;
                return c != null && c.State.state == Gim005State.Running ? "[E] 버튼 누르기" : "버튼이 꿈쩍도 하지 않는다";
            }
        }

        public void Interact(GameObject interactor)
        {
            var c = Gim005Controller.Instance;
            if (c == null || c.Net == null || c.State.state != Gim005State.Running) return;
            if (source != null) source.PlayOneShot(Gim002.Gim002Tones.Click());
            c.Net.RequestCenter();
        }
    }
}
