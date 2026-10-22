using UnityEngine;

namespace Gim004
{
    // 중앙 선반 판정 결과 소리. 오답이면 짧은 실패음, 정답이면 성공음 — 1층(온실)에 있는 플레이어에게만 들린다.
    // 2층은 오답일 때 아무 표시도 없다 (명세서 6장). 소리를 비워 두면 임시 합성음을 쓴다.
    // 사용법: 중앙 선반 루트에 붙인다.
    public class Gim004ShelfFeedback : MonoBehaviour
    {
        [SerializeField] AudioClip wrongClip;
        [SerializeField] AudioClip completeClip;
        [Range(0f, 1f)] [SerializeField] float volume = 0.8f;

        Gim004Controller bound;

        void OnEnable() { TryBind(); }

        void Start() { TryBind(); }

        void OnDisable()
        {
            if (bound != null) bound.EventReceived -= OnEvent;
            bound = null;
        }

        void TryBind()
        {
            var c = Gim004Controller.Instance;
            if (c == null || c == bound) return;
            bound = c;
            c.EventReceived += OnEvent;
        }

        void OnEvent(Gim004NetEvent e, int value)
        {
            if (bound == null || !bound.LocalOnFloor1) return;
            if (e == Gim004NetEvent.Wrong) Play(wrongClip != null ? wrongClip : Gim002.Gim002Tones.WrongChord());
            else if (e == Gim004NetEvent.Complete) Play(completeClip != null ? completeClip : Gim002.Gim002Tones.Complete());
        }

        void Play(AudioClip clip)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position + Vector3.up, volume);
        }
    }
}
