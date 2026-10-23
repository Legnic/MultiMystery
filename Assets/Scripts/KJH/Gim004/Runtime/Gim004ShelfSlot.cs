using System.Collections.Generic;
using UnityEngine;

namespace Gim004
{
    // 중앙 선반의 화분 자리 하나 (선반 정면에서 바라본 왼쪽부터 0~3).
    // 놓기·바꾸기·빼기 규칙(명세서 6장)을 여기서 처리하고, 판정 담당에게 요청한다.
    //  - 놓기: 화분을 들고 빈 자리를 바라보고 E → 놓임, 자리 버튼이 눌림 (정답 여부와 관계없음)
    //  - 바꾸기: 찬 자리에 놓으면 든 화분이 놓이고, 있던 화분은 처음 자리로
    //  - 빼기: 빈손으로 놓인 화분을 바라보고 E → 다시 손에, 버튼이 올라옴
    //  - 판정 중·완료 후에는 바꿀 수 없다 (완료 후 화분 잠금)
    // 사용법: 콜라이더가 있는 오브젝트(빈 자리를 바라볼 때 맞을 곳)에 붙이고, anchor에 화분이 놓일 위치, button에 눌리는 버튼을 연결한다.
    public class Gim004ShelfSlot : MonoBehaviour, IWorldInteractable, IHoldableSocket
    {
        [Tooltip("선반 정면에서 바라본 왼쪽부터 0, 1, 2, 3")]
        [SerializeField] int index;
        [Tooltip("화분이 놓일 위치·방향. 비우면 이 오브젝트 위치")]
        [SerializeField] Transform anchor;
        [Tooltip("화분이 놓이면 눌리는 버튼 (비우면 버튼 연출 없음)")]
        [SerializeField] Transform button;
        [Tooltip("버튼이 눌리는 깊이(m)")]
        [SerializeField] float buttonDepth = 0.025f;
        [Tooltip("버튼이 움직이는 속도(m/초)")]
        [SerializeField] float buttonSpeed = 0.25f;
        [Tooltip("놓을 때 소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip placeClip;

        public int Index { get { return index; } }
        public Transform Anchor { get { return anchor != null ? anchor : transform; } }

        static readonly List<Gim004ShelfSlot> all = new List<Gim004ShelfSlot>();
        public static Gim004ShelfSlot Find(int i)
        {
            foreach (var s in all) if (s != null && s.index == i) return s;
            return null;
        }

        Vector3 buttonUp;

        void Awake() { if (button != null) buttonUp = button.localPosition; }
        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        static Gim004Controller C { get { return Gim004Controller.Instance; } }
        static Gim004Pot HeldPot { get { return PlayerHand.LocalHeld as Gim004Pot; } }

        // 지금 이 자리에 화분이 (보이게) 놓여 있는가: 동기화 상태 또는 내가 방금 놓아 답을 기다리는 중
        Gim004Pot PlacedPot()
        {
            var a = Anchor;
            for (int i = 0; i < a.childCount; i++)
            {
                var p = a.GetChild(i).GetComponent<Gim004Pot>();
                if (p != null && p.IsPlacedOn(a)) return p;
            }
            return null;
        }

        public bool Pressed { get { return (C != null && C.State.Slot(index) >= 0) || PlacedPot() != null; } }

        void Update()
        {
            if (button == null) return;
            Vector3 target = Pressed ? buttonUp + Vector3.down * buttonDepth : buttonUp;
            button.localPosition = Vector3.MoveTowards(button.localPosition, target, buttonSpeed * Time.deltaTime);
        }

        // 지금 선반을 바꿀 수 없는 이유. 바꿀 수 있으면 null (빈 문자열 = 잠깐 기다림)
        string BlockReason()
        {
            var c = C;
            if (c == null) return "";
            if (c.IsComplete) return c.Config.msgShelfLocked;
            if (!c.ShelfEditable) return c.Config.msgJudging;
            return null;
        }

        // ---------------- 빈 자리를 바라볼 때 (IWorldInteractable) ----------------

        public bool CanInteract { get { return C != null && HeldPot != null; } }

        public string InteractPrompt
        {
            get
            {
                var held = HeldPot;
                if (held == null) return null;
                string why = BlockReason();
                if (why != null) return why.Length > 0 ? why : "...";
                return "[E] " + held.DisplayName + (PlacedPot() != null ? " 바꿔 놓기" : " 놓기");
            }
        }

        public void Interact(GameObject interactor)
        {
            var held = HeldPot;
            if (held == null || C == null) return;
            string why = BlockReason();
            if (why != null) { PlayerHand.Say(why); return; }
            Place(held);
        }

        void Place(Gim004Pot pot)
        {
            // 바꾸기: 있던 화분은 바로 처음 자리로 보낸다 (판정 담당의 답도 같은 결과)
            var old = PlacedPot();
            if (old != null && old != pot) { old.PredictLeave(); old.ReturnHome(); }

            PlayerHand.Local.Release();
            pot.PredictPlace(this);
            PlayClip(placeClip != null ? placeClip : Gim002.Gim002Tones.Click());
            C.Net.RequestPlace(index, pot.Id);
        }

        // ---------------- 놓인 화분을 바라볼 때 (IHoldableSocket) ----------------

        public bool CanUsePlaced(HoldableItem placed) { return C != null; }

        public string PromptForPlaced(HoldableItem placed)
        {
            if (HeldPot != null) return InteractPrompt;                    // 다른 화분을 들고 있으면 바꾸기 안내
            if (PlayerHand.LocalHeld != null) return placed.DisplayName;    // 기믹과 상관없는 물건을 든 경우
            string why = BlockReason();
            if (why != null) return why.Length > 0 ? why : placed.DisplayName;
            return "[E] " + placed.DisplayName + " 빼기";
        }

        public void UsePlaced(HoldableItem placed)
        {
            var c = C;
            if (HeldPot != null) { Interact(null); return; }
            if (PlayerHand.LocalHeld != null || c == null) return;
            string why = BlockReason();
            if (why != null) { if (why.Length > 0) PlayerHand.Say(why); return; }

            var pot = placed as Gim004Pot;
            if (pot == null || !PlayerHand.Local.TryTake(pot)) return;
            pot.PredictLeave();
            c.Net.RequestTake(index);
        }

        void PlayClip(AudioClip clip)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, Anchor.position, 0.7f);
        }
    }
}
