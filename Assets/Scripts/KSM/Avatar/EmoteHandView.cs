using UnityEngine;

namespace CoopDemo
{
    // Lives on the "EmoteHand" holder under the player's camera. CharacterAvatar finds this to
    // (a) play a gesture on the owner's own first-person view and (b) hand its Transform to
    // CharacterAvatarModel.AttachFaceItem so the SAME object shows up near the 3D avatar's face
    // on everyone else's screen - the same trick HeldCandle already uses.
    //
    // 2026-10-29: LHJ님이 Blender로 만든 손 5종(인사·브이·중지·가리키기·엄지척)을 자식으로 미리 넣어 두고,
    // 몸짓을 고르면 그 손 하나만 켠다. 각 손의 Animator는 켜지는 순간 기본 동작(Swing)을 처음부터 재생한다.
    // 자식 구성·손별 방향/크기/좌우반전은 EmoteHandBuilder(Tools/KSM/EmoteHand/Build All)가 만든다.
    public class EmoteHandView : MonoBehaviour
    {
        public GameObject[] hands;      // 0=인사 1=브이 2=중지 3=가리키기 4=엄지척 (EmoteWheelUI.Labels와 같은 순서)
        public float holdSeconds = 3.0f;

        float hideAt;

        void Awake()
        {
            gameObject.SetActive(false);   // hidden until Play() is called
        }

        public void Play(int gesture)
        {
            if (hands == null || gesture < 0 || gesture >= hands.Length || hands[gesture] == null) return;
            gameObject.SetActive(true);
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
