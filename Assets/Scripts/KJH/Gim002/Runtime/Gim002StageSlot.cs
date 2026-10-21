using System.Collections.Generic;
using UnityEngine;

namespace Gim002
{
    // 2층 무대의 인형 꽂이(OBJ_SLOT_DOLL) / 배경카드 슬롯(OBJ_SLOT_BG).
    // 꽂기·바꾸기·빼기 규칙(기획 6-1)을 여기서 처리하고, 결과는 판정 담당에게 "바뀐 뒤 두 슬롯 값"으로 요청한다.
    //  - 꽂기: 커튼 열림(Staging) + 맞는 종류를 들고 있을 때. 인형은 꽂이에만, 카드는 슬롯에만.
    //  - 바꾸기: 이미 꽂혀 있으면 든 것이 꽂히고, 원래 것은 제자리로 (판정 전에만)
    //  - 빼기: 빈손으로 꽂힌 물건을 바라보고 E → 다시 손에 (판정 전에만)
    //  - 반주 단계 이후(슬롯 잠금)에는 뺄 수 없다.
    // 사용법: 콜라이더가 있는 오브젝트(빈 슬롯을 바라볼 때 맞을 곳)에 붙이고, anchor에 물건이 놓일 위치를 연결한다.
    public class Gim002StageSlot : MonoBehaviour, IWorldInteractable, IHoldableSocket
    {
        [SerializeField] StageSlotKind kind;
        [Tooltip("꽂힌 물건이 놓일 위치·방향. 비우면 이 오브젝트 위치")]
        [SerializeField] Transform anchor;
        [Tooltip("꽂힐 때 소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip placeClip;

        public StageSlotKind Kind { get { return kind; } }
        public Transform Anchor { get { return anchor != null ? anchor : transform; } }

        static readonly List<Gim002StageSlot> all = new List<Gim002StageSlot>();
        public static Gim002StageSlot Find(StageSlotKind k)
        {
            foreach (var s in all) if (s != null && s.kind == k) return s;
            return null;
        }

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        static Gim002Controller C { get { return Gim002Controller.Instance; } }
        static Gim002StageItem HeldItem { get { return PlayerHand.LocalHeld as Gim002StageItem; } }

        int Occupant { get { var c = C; return c == null ? -1 : (kind == StageSlotKind.Doll ? c.State.slotDoll : c.State.slotBg); } }

        // 들고 있는 물건(held)을 이 슬롯에 꽂을 수 없는 이유. 꽂을 수 있으면 null.
        string BlockReason(Gim002StageItem held)
        {
            var c = C;
            if (c == null) return "";
            var cfg = c.Config;
            if (held.Kind != kind) return held.Kind == StageSlotKind.Doll ? cfg.msgWrongSlotDoll : cfg.msgWrongSlotBg;
            var st = c.State.state;
            if (st == Gim002State.Accompany || st == Gim002State.Perform || st == Gim002State.Complete) return cfg.msgSlotLocked;
            if (!c.State.curtainOpen) return cfg.msgCurtainClosed;
            if (!c.StageEditable) return "";   // 커튼이 열리는 중 / 판정 중 → 잠깐 기다림
            return null;
        }

        // ---------------- 빈 슬롯을 바라볼 때 (IWorldInteractable) ----------------

        public bool CanInteract { get { return C != null && HeldItem != null; } }

        public string InteractPrompt
        {
            get
            {
                var held = HeldItem;
                if (held == null) return null;
                string why = BlockReason(held);
                if (why != null) return why.Length > 0 ? why : "...";
                return "[E] " + held.DisplayName + (Occupant >= 0 ? " 바꿔 꽂기" : " 꽂기");
            }
        }

        public void Interact(GameObject interactor)
        {
            var held = HeldItem;
            if (held == null || C == null) return;
            string why = BlockReason(held);
            if (why != null) { PlayerHand.Say(why); return; }
            Place(held);
        }

        // 꽂기 (이미 꽂혀 있으면 바꾸기: 밀려난 물건은 동기화 상태에서 빠지므로 스스로 제자리로 돌아간다)
        void Place(Gim002StageItem item)
        {
            var s = C.State;
            DollId? doll = s.SlotDoll;
            BackgroundId? bg = s.SlotBg;
            if (kind == StageSlotKind.Doll) doll = item.Doll; else bg = item.Background;

            PlayerHand.Local.Release();
            item.PredictPlace(this);
            PlayClip(placeClip != null ? placeClip : Gim002Tones.Click());
            C.Net.RequestSlotChanged(doll, bg);
        }

        // ---------------- 꽂힌 물건을 바라볼 때 (IHoldableSocket) ----------------

        public bool CanUsePlaced(HoldableItem placed) { return C != null; }

        public string PromptForPlaced(HoldableItem placed)
        {
            var c = C;
            if (HeldItem != null) return InteractPrompt;                 // 다른 걸 들고 있으면 바꾸기 안내
            if (PlayerHand.LocalHeld != null) return placed.DisplayName; // 열쇠 등 무대와 상관없는 물건을 든 경우
            var st = c.State.state;
            if (st == Gim002State.Accompany || st == Gim002State.Perform || st == Gim002State.Complete) return c.Config.msgSlotLocked;
            if (!c.StageEditable) return placed.DisplayName;
            return "[E] " + placed.DisplayName + " 빼기";
        }

        public void UsePlaced(HoldableItem placed)
        {
            var c = C;
            if (HeldItem != null) { Interact(null); return; }
            if (PlayerHand.LocalHeld != null) return;
            if (!c.StageEditable)
            {
                var st = c.State.state;
                if (st == Gim002State.Accompany || st == Gim002State.Perform || st == Gim002State.Complete) PlayerHand.Say(c.Config.msgSlotLocked);
                return;
            }

            // 빼기: 손에 들고, 이 슬롯을 비운 값으로 요청
            var item = placed as Gim002StageItem;
            if (item == null || !PlayerHand.Local.TryTake(item)) return;
            var s = c.State;
            DollId? doll = s.SlotDoll;
            BackgroundId? bg = s.SlotBg;
            if (kind == StageSlotKind.Doll) doll = null; else bg = null;
            c.Net.RequestSlotChanged(doll, bg);
        }

        void PlayClip(AudioClip clip)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, Anchor.position, 0.7f);
        }
    }
}
