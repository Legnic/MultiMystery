using Unity.Netcode;
using UnityEngine;

namespace KSM
{
    // Networks the two cosmetic action poses (holding a candle, examining something) for the Toy Figure
    // model. Each flag is written by its own owner (no server round-trip needed, same as a local-only
    // cosmetic) and mirrored to everyone else; ToyActionAnimator reacts to the change on every machine,
    // including the owner's own. Works standalone (not spawned) too, same offline-fallback pattern as
    // LGH.HeldCandle, so the pose can be tested in a scene with no NetworkManager running.
    public class ToyActionNetwork : NetworkBehaviour
    {
        [SerializeField] ToyActionAnimator actionAnimator;

        readonly NetworkVariable<bool> holdingCandle = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> examining = new NetworkVariable<bool>(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        bool offlineHoldingCandle, offlineExamining;

        public bool HoldingCandle { get { return IsSpawned ? holdingCandle.Value : offlineHoldingCandle; } }
        public bool Examining { get { return IsSpawned ? examining.Value : offlineExamining; } }

        void Awake()
        {
            if (actionAnimator == null) actionAnimator = GetComponent<ToyActionAnimator>();
        }

        public override void OnNetworkSpawn()
        {
            holdingCandle.OnValueChanged += OnHoldingCandleChanged;
            examining.OnValueChanged += OnExaminingChanged;
            if (actionAnimator != null)
            {
                actionAnimator.SetHoldingCandle(holdingCandle.Value);
                actionAnimator.SetExamining(examining.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            holdingCandle.OnValueChanged -= OnHoldingCandleChanged;
            examining.OnValueChanged -= OnExaminingChanged;
        }

        void OnHoldingCandleChanged(bool previous, bool current) { if (actionAnimator != null) actionAnimator.SetHoldingCandle(current); }
        void OnExaminingChanged(bool previous, bool current) { if (actionAnimator != null) actionAnimator.SetExamining(current); }

        // ---- called by whatever triggers the pose (only has an effect for the owner) ----

        public void RequestHoldingCandle(bool on)
        {
            if (IsSpawned)
            {
                if (!IsOwner) return;
                holdingCandle.Value = on;
            }
            else
            {
                offlineHoldingCandle = on;
                if (actionAnimator != null) actionAnimator.SetHoldingCandle(on);
            }
        }

        public void RequestExamining(bool on)
        {
            if (IsSpawned)
            {
                if (!IsOwner) return;
                examining.Value = on;
            }
            else
            {
                offlineExamining = on;
                if (actionAnimator != null) actionAnimator.SetExamining(on);
            }
        }
    }
}
