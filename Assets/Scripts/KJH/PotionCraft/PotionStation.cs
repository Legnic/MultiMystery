using System.Collections;
using UnityEngine;

namespace PotionCraft
{
    // 약품 제조 퍼즐의 모든 "상호작용 가능한 장치" 공통 부모. IWorldInteractable이라서 LGH 플레이어(PlayerInteractor)의 E키로 바로 동작한다.
    // 규칙: 상태를 바꾸는 판정(ServerInteract)은 호스트(또는 혼자 플레이)에서만 실행되고,
    // 바뀐 상태(GetState)를 PotionCraftSync가 모든 클라이언트의 ApplyState로 전달한다. 연출은 Refresh/PlayFx에서 상태·신호로부터 만든다.
    public abstract class PotionStation : MonoBehaviour, IWorldSecondaryInteractable
    {
        public virtual bool CanInteract { get { return true; } }
        public abstract string InteractPrompt { get; }

        // 호스트 전용. 판정을 하고 손에 든 아이템(held)을 바꾼다. 안내/거부 문구를 돌려준다(없으면 null).
        public abstract string ServerInteract(ref int held);

        // R키 동작이 있는 장치만 CanSecondary를 true로 바꾸고 ServerSecondary를 구현한다 (없으면 R은 원래대로 양초 바꾸기).
        protected virtual bool CanSecondary { get { return false; } }
        public virtual string ServerSecondary(ref int held) { return null; }
        public bool CanSecondaryInteract { get { return CanInteract && CanSecondary; } }
        public virtual int[] GetState() { return System.Array.Empty<int>(); }
        public virtual void ApplyState(int[] s) { }
        // 상태가 바뀐 뒤 화면(모양)을 상태에 맞춘다. 호스트/클라이언트 모두 호출된다.
        public virtual void Refresh() { }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            var sync = PotionCraftSync.Instance;
            if (sync == null)
            {
                Debug.LogWarning("[PotionStation] 씬에 PotionCraftSync가 없습니다 (DoorSync_LGH 오브젝트에 붙여주세요).", this);
                return;
            }
            sync.RequestInteract(this);
        }

        public void SecondaryInteract(GameObject interactor)
        {
            if (!CanSecondaryInteract) return;
            var sync = PotionCraftSync.Instance;
            if (sync != null) sync.RequestInteract(this, true);
        }

        protected PotionCraftSync Sync { get { return PotionCraftSync.Instance; } }
        protected PotionCatalog Catalog { get { return PotionCraftSync.Instance != null ? PotionCraftSync.Instance.Catalog : null; } }
        protected int LocalHeld { get { return PotionCraftSync.Instance != null ? PotionCraftSync.Instance.LocalHeld : 0; } }

        protected string NameOf(int code) { var c = Catalog; return c != null ? c.NameOf(code) : "아이템"; }

        // 거부: 모두에게 짧은 흔들림을 보여 주고 문구를 돌려준다. 손의 아이템은 그대로 남는다.
        protected string Reject(string message)
        {
            if (Sync != null) Sync.Fx(this, PotionFx.Reject);
            return message;
        }

        protected void Publish() { if (Sync != null) Sync.Publish(this); }

        // ---------- 연출 (모든 클라이언트) ----------
        Vector3 shakeHome;
        Coroutine shaking;

        public virtual void PlayFx(PotionFx fx)
        {
            var cat = Catalog;
            if (fx == PotionFx.Reject)
            {
                if (cat != null && cat.rejectClip != null) AudioSource.PlayClipAtPoint(cat.rejectClip, transform.position);
                if (shaking == null) shaking = StartCoroutine(Shake());
            }
        }

        IEnumerator Shake()
        {
            shakeHome = transform.localPosition;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float k = 1f - t / 0.25f;
                transform.localPosition = shakeHome + Vector3.right * Mathf.Sin(t * 70f) * 0.03f * k;
                yield return null;
            }
            transform.localPosition = shakeHome;
            shaking = null;
        }
    }
}
