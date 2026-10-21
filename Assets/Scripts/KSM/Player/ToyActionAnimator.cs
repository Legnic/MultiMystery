using UnityEngine;

namespace KSM
{
    // Drives the Toy Figure's Animator for the two cosmetic action poses (candle-holding, examining)
    // and hands off bone ownership to/from ProceduralToyAnimator so they never fight over the same
    // bones. Purely local/visual - ToyActionNetwork decides *when* these should be on for this player
    // and calls these setters on every machine (owner and remote) once the network state changes.
    public class ToyActionAnimator : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] ProceduralToyAnimator proceduralAnimator;

        static readonly int HoldingCandleParam = Animator.StringToHash("HoldingCandle");
        static readonly int ExaminingParam = Animator.StringToHash("Examining");

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (proceduralAnimator == null) proceduralAnimator = GetComponent<ProceduralToyAnimator>();
        }

        public void SetHoldingCandle(bool on)
        {
            if (animator != null) animator.SetBool(HoldingCandleParam, on);
            if (proceduralAnimator != null) proceduralAnimator.suppressRightArm = on;
        }

        public void SetExamining(bool on)
        {
            if (animator != null) animator.SetBool(ExaminingParam, on);
            if (proceduralAnimator != null) proceduralAnimator.suppressAll = on;
        }
    }
}
