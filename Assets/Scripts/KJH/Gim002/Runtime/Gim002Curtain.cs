using UnityEngine;

namespace Gim002
{
    // 무대 커튼. 동기화 상태의 curtainOpen을 보고 좌우 커튼을 옆으로 밀어 연다/닫는다.
    // 열리고 닫히는 시간은 Config의 curtainSeconds를 따른다. 판정 담당과 시간이 같아서 "다 열린 뒤 꽂기 가능"이 자연스럽게 맞는다.
    public class Gim002Curtain : MonoBehaviour
    {
        [Tooltip("왼쪽 커튼")]
        [SerializeField] Transform left;
        [Tooltip("오른쪽 커튼")]
        [SerializeField] Transform right;
        [Tooltip("열렸을 때 커튼 폭 비율 (접힌 느낌)")]
        [SerializeField] float openWidthScale = 0.25f;
        [Tooltip("커튼이 움직일 때 소리 (비우면 소리 없음)")]
        [SerializeField] AudioClip moveClip;

        Vector3 leftClosedPos, rightClosedPos, leftClosedScale, rightClosedScale;
        float leftMeshWidth = 1f, rightMeshWidth = 1f;   // 스케일 1일 때 커튼 메시의 폭 (기본 Cube = 1, 모델 커튼은 메시마다 다름)
        float amount;          // 0 = 닫힘, 1 = 열림
        bool lastTarget;

        void Awake()
        {
            if (left != null) { leftClosedPos = left.localPosition; leftClosedScale = left.localScale; leftMeshWidth = MeshWidth(left); }
            if (right != null) { rightClosedPos = right.localPosition; rightClosedScale = right.localScale; rightMeshWidth = MeshWidth(right); }
        }

        // 커튼 메시의 로컬 X 폭 (메시가 없으면 1)
        static float MeshWidth(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.x : 1f;
        }

        void Update()
        {
            var c = Gim002Controller.Instance;
            if (c == null) return;

            bool target = c.State.curtainOpen;
            if (target != lastTarget)
            {
                lastTarget = target;
                if (moveClip != null) AudioSource.PlayClipAtPoint(moveClip, transform.position, 0.7f);
            }

            float seconds = Mathf.Max(0.01f, c.Timing.curtainSeconds);
            amount = Mathf.MoveTowards(amount, target ? 1f : 0f, Time.deltaTime / seconds);
            float k = Mathf.SmoothStep(0f, 1f, amount);
            Apply(left, leftClosedPos, leftClosedScale, leftMeshWidth, -1f, k);
            Apply(right, rightClosedPos, rightClosedScale, rightMeshWidth, 1f, k);
        }

        // 커튼 한 쪽을 바깥으로 밀면서 폭을 줄인다 (side: -1 왼쪽, +1 오른쪽)
        void Apply(Transform t, Vector3 closedPos, Vector3 closedScale, float meshWidth, float side, float k)
        {
            if (t == null) return;
            float w = Mathf.Lerp(1f, openWidthScale, k);
            t.localScale = new Vector3(closedScale.x * w, closedScale.y, closedScale.z);
            // 바깥쪽 끝을 고정한 채 접히도록: 줄어든 폭의 절반만큼 바깥으로 민다 (커튼 메시가 가운데 기준이라고 가정)
            float shift = meshWidth * (closedScale.x - closedScale.x * w) * 0.5f;
            t.localPosition = closedPos + new Vector3(side * shift, 0f, 0f);
        }
    }
}
