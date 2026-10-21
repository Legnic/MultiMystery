using UnityEngine;

namespace Gim002
{
    // 무대 오답 때 2층 플레이어 화면을 빨갛게 한 번 깜빡인다 (stageWrongFlashSeconds, 기본 0.5초).
    // "2층에서"만 보여야 하므로, 이 오브젝트(무대) 근처 radius 안에 있는 이 컴퓨터 플레이어에게만 표시한다.
    // 1층 쪽 오답 알림은 피아노 위 "무대" 표시등 깜빡임 + 경고음이 맡는다.
    public class Gim002ScreenFlash : MonoBehaviour
    {
        [Tooltip("이 거리 안에 있는 플레이어 화면만 깜빡인다 (m)")]
        [SerializeField] float radius = 9f;
        [SerializeField] Color flashColor = new Color(0.8f, 0f, 0f, 0.45f);

        float start, until;
        static Texture2D white;

        void Start()
        {
            var c = Gim002Controller.Instance;
            if (c != null) c.EventReceived += OnEvent;
        }

        void OnDestroy()
        {
            var c = Gim002Controller.Instance;
            if (c != null) c.EventReceived -= OnEvent;
        }

        void OnEvent(Gim002NetEvent e, int value)
        {
            if (e != Gim002NetEvent.StageWrong) return;
            var pi = LGH.PlayerInteractor.Active;
            if (pi == null || Vector3.Distance(pi.transform.position, transform.position) > radius) return;
            start = Time.time;
            until = Time.time + Gim002Controller.Instance.Timing.stageWrongFlashSeconds;
        }

        void OnGUI()
        {
            if (Time.time >= until) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            // 한 번 확 밝아졌다가 사라지는 모양
            float k = Mathf.InverseLerp(start, until, Time.time);
            var c = flashColor;
            c.a *= Mathf.Sin(k * Mathf.PI);
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = Color.white;
        }
    }
}
