using UnityEngine;

namespace CoopDemo
{
    // Lives on the "EmoteHand" holder under the player's camera. CharacterAvatar finds this to
    // (a) play a gesture on the owner's own first-person view and (b) hand its Transform to
    // CharacterAvatarModel.AttachEmoteHand so the SAME object shows up above the 3D avatar's left hand
    // on everyone else's screen - the same trick HeldCandle already uses.
    //
    // 2026-10-29: LHJ님이 Blender로 만든 손 5종(인사·브이·중지·가리키기·엄지척)을 자식으로 미리 넣어 두고,
    // 몸짓을 고르면 그 손 하나만 켠다. 각 손의 Animator는 켜지는 순간 기본 동작(Swing)을 처음부터 재생한다.
    // 자식 구성·손별 방향/크기/좌우반전은 EmoteHandBuilder(Tools/KSM/EmoteHand/Build All)가 만든다.
    public class EmoteHandView : MonoBehaviour
    {
        public GameObject[] hands;      // 0=인사 1=브이 2=중지 3=가리키기 4=엄지척 (EmoteWheelUI.Labels와 같은 순서)
        public float holdSeconds = 3.0f;
        [Tooltip("상대 화면에서만 손을 Y로 더 돌리는 각도(몸짓별). 1인칭(내 화면) 방향은 그대로 두고, 마주 선 상대에게 보이는 면만 바꾼다. 인사·브이=180 → 상대에게 손바닥이 보인다.")]
        public float[] otherViewYaw;

        float hideAt;
        int current = -1;

        // 지금 보이는 손에 맞는 "상대 화면 전용" 추가 회전 (CharacterAvatarModel이 매 프레임 읽는다)
        public float CurrentOtherViewYaw
        {
            get { return otherViewYaw != null && current >= 0 && current < otherViewYaw.Length ? otherViewYaw[current] : 0f; }
        }

        void Awake()
        {
            gameObject.SetActive(false);   // hidden until Play() is called
        }

        public void Play(int gesture)
        {
            if (hands == null || gesture < 0 || gesture >= hands.Length || hands[gesture] == null) return;
            gameObject.SetActive(true);
            current = gesture;
            for (int i = 0; i < hands.Length; i++)
                if (hands[i] != null) hands[i].SetActive(false);
            hands[gesture].SetActive(true);   // 꺼졌다 켜지면 Animator가 처음부터 다시 재생한다(연타해도 처음부터)
            hideAt = Time.time + holdSeconds;
        }

        void Update()
        {
            if (gameObject.activeSelf && Time.time >= hideAt) gameObject.SetActive(false);
        }
    }
}
