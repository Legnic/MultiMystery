using System.Text;
using UnityEngine;

namespace Gim002
{
    // 반주판(OBJ_SCOREBOARD). 평소엔 덮개가 닫혀 있고, 무대 정답(scoreboardOpen)이면 덮개가 열리며 현재 장면의 기호 순서가 보인다.
    // 2층 플레이어(B)만 볼 수 있는 정보라서 1층에 불러 줘야 한다 (정보 비대칭).
    // 기호 표시는 아트가 오면 symbolRenderers(기호 그림 칸)에 머티리얼을 끼우면 되고, 지금은 TextMesh 글자로 대신한다.
    public class Gim002Scoreboard : MonoBehaviour
    {
        [Tooltip("열리고 닫히는 덮개 (경첩 위치가 피벗이 되도록 부모 오브젝트를 두는 것을 권장)")]
        [SerializeField] Transform lid;
        [Tooltip("덮개가 열린 각도 (lid 로컬 X축 기준)")]
        [SerializeField] float openAngle = -100f;
        [Tooltip("덮개 여닫는 시간(초)")]
        [SerializeField] float lidSeconds = 0.4f;
        [Tooltip("기호 순서를 보여 줄 글자 (임시). 아트가 오면 아래 symbolSlots를 쓰고 이건 비워도 된다")]
        [SerializeField] TextMesh symbolText;
        [Tooltip("(선택) 기호 그림을 보여 줄 칸들. 칸 수보다 기호가 적으면 남는 칸은 숨긴다")]
        [SerializeField] Renderer[] symbolSlots = new Renderer[0];
        [Tooltip("(선택) SymbolId 순서(해, 구름, 달, 꽃, 별)대로의 기호 머티리얼")]
        [SerializeField] Material[] symbolMaterials = new Material[0];

        Quaternion lidClosed;
        float amount;
        int shownScene = -1;
        bool shownOpen;

        void Awake()
        {
            if (lid != null) lidClosed = lid.localRotation;
            Refresh(false, 0);
        }

        void Update()
        {
            var c = Gim002Controller.Instance;
            if (c == null) return;

            bool open = c.State.scoreboardOpen;
            amount = Mathf.MoveTowards(amount, open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, lidSeconds));
            if (lid != null) lid.localRotation = lidClosed * Quaternion.Euler(openAngle * Mathf.SmoothStep(0f, 1f, amount), 0f, 0f);

            if (open != shownOpen || c.State.sceneIndex != shownScene) Refresh(open, c.State.sceneIndex);
        }

        // 기호 표시를 현재 장면에 맞게 다시 그린다 (닫혀 있으면 숨김)
        void Refresh(bool open, int sceneIndex)
        {
            shownOpen = open;
            shownScene = sceneIndex;
            var c = Gim002Controller.Instance;
            var scene = c != null && c.Config != null ? c.Config.GetScene(sceneIndex) : null;
            var symbols = open && scene != null ? scene.symbols : new SymbolId[0];

            if (symbolText != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < symbols.Length; i++)
                {
                    if (i > 0) sb.Append("  →  ");
                    sb.Append(c.Config.SymbolName(symbols[i]));
                }
                symbolText.text = sb.ToString();
                symbolText.gameObject.SetActive(open);
            }

            for (int i = 0; i < symbolSlots.Length; i++)
            {
                var r = symbolSlots[i];
                if (r == null) continue;
                bool used = i < symbols.Length;
                r.gameObject.SetActive(used);
                int m = used ? (int)symbols[i] : -1;
                if (used && m < symbolMaterials.Length && symbolMaterials[m] != null) r.sharedMaterial = symbolMaterials[m];
            }
        }
    }
}
