using UnityEngine;

namespace CoopDemo
{
    // Lives on the "EmoteHand" child under the player's camera (Hand_Wave_Rigged + Animator).
    // CharacterAvatar finds this to (a) play a gesture on the owner's own first-person view and
    // (b) hand its Transform to CharacterAvatarModel.AttachFaceItem so the SAME object shows up
    // near the 3D avatar's face on everyone else's screen - the same trick HeldCandle already uses.
    public class EmoteHandView : MonoBehaviour
    {
        public Animator animator;
        public float holdSeconds = 2.0f;

        // 2026-10-27: 손가락 구부리기 몸짓은 팀원이 Blender로 다시 만들 때까지 인사만 남김.
        static readonly int[] GestureHash =
        {
            Animator.StringToHash("Wave"),
        };

        float hideAt;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            gameObject.SetActive(false);   // hidden until Play() is called; cache the Animator ref first
        }

        public void Play(int gesture)
        {
            if (gesture < 0 || gesture >= GestureHash.Length) return;
            gameObject.SetActive(true);
            hideAt = Time.time + holdSeconds;
            if (animator != null) animator.SetTrigger(GestureHash[gesture]);
        }

        void Update()
        {
            if (gameObject.activeSelf && Time.time >= hideAt) gameObject.SetActive(false);
        }
    }
}
