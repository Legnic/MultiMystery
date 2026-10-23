using System.Collections.Generic;
using UnityEngine;

namespace Gim007
{
    // 가구 뒷면의 손잡이 지점 하나 (0 = 미는 사람 기준 왼쪽, 1 = 오른쪽).
    // LGH 플레이어(PlayerInteractor)가 바라보고 E를 누르면 Interact가 불린다 → 판정 담당에게 "잡기" 요청.
    // 어느 쪽이든 먼저 잡은 사람이 차지한다 (명세서 2장: A/B 고정 아님).
    //
    // 손 떼기(E)는 여기서 처리하지 않고 Gim007PushPlayer가 처리한다: 잡는 동안 시점이 고정되어 있어도
    // 조준점이 손잡이에서 벗어날 수 있어서, 손잡이를 바라보고 있지 않아도 E로 놓을 수 있게 하기 위해서다.
    //
    // 사용법: 콜라이더가 있는 오브젝트(가구 뒷면 절반 크기)에 붙이고, standPoint(플레이어가 서는 곳, 바닥 높이)와
    //  lookPoint(잡는 동안 바라볼 곳)를 연결한다.
    public class Gim007Handle : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("0 = 미는 사람 기준 왼쪽, 1 = 오른쪽")]
        [SerializeField] int index;
        [Tooltip("잡는 동안 플레이어가 서 있을 곳 (바닥 높이). 비우면 손잡이 뒤 0.6m")]
        [SerializeField] Transform standPoint;
        [Tooltip("잡는 동안 시점이 향할 곳. 비우면 이 오브젝트 위치")]
        [SerializeField] Transform lookPoint;

        public int Index { get { return index; } }
        public Transform LookPoint { get { return lookPoint != null ? lookPoint : transform; } }
        public Vector3 StandPosition { get { return standPoint != null ? standPoint.position : transform.position - transform.forward * 0.6f; } }

        // 이번 프레임에 손잡이로 잡기 요청을 보냈는가 (같은 E 입력으로 바로 놓아 버리지 않게)
        public static int LastGripFrame { get; private set; } = -1;

        static readonly List<Gim007Handle> all = new List<Gim007Handle>();
        public static Gim007Handle Find(int i)
        {
            foreach (var h in all) if (h != null && h.index == i) return h;
            return null;
        }

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        static Gim007Controller C { get { return Gim007Controller.Instance; } }

        bool OccupiedByOther
        {
            get
            {
                var c = C;
                if (c == null) return false;
                ulong g = c.State.Grip(index);
                return g != Gim007SyncState.NoPlayer && g != c.LocalPlayerId;
            }
        }

        // ---------------- IWorldInteractable ----------------

        public bool CanInteract { get { return C != null; } }

        public string InteractPrompt
        {
            get
            {
                var c = C;
                if (c == null) return null;
                if (c.IsComplete) return c.Config.msgLocked;
                if (c.LocalHandle >= 0) return c.Config.msgReleasePrompt;   // 잡는 중이면 어느 쪽을 보든 "손 떼기"
                if (OccupiedByOther) return c.Config.msgHandleTaken;
                return string.Format(c.Config.msgGripPrompt, c.Config.furnitureName);
            }
        }

        public void Interact(GameObject interactor)
        {
            var c = C;
            if (c == null) return;
            if (c.IsComplete) { PlayerHand.Say(c.Config.msgLocked); return; }
            if (c.LocalHandle >= 0) return;                                 // 손 떼기는 Gim007PushPlayer가 처리
            if (Gim007PushPlayer.ReleasedFrame == Time.frameCount) return;  // 방금 같은 E로 놓았음
            if (OccupiedByOther) { PlayerHand.Say(c.Config.msgHandleTaken); return; }
            LastGripFrame = Time.frameCount;
            c.Net.RequestGrip(index);
        }
    }
}
