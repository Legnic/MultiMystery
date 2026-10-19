using UnityEngine;

namespace KSM
{
    // Lets LGH's PlayerInteractor (doors/drawers/candles/portraits) and KJH's InteractionController
    // (clues/photos/object echo/safes) share one player without both reacting to E or showing a
    // prompt at the same time. Neither teammate's script is touched — this only toggles
    // PlayerInteractor on/off based on whether KJH's prompt UI is currently visible, which is read
    // straight off its CanvasGroup (no new public API needed on InteractionController).
    //
    // KJH's system wins when it has something focused: its targets (a clue, a photo spot) are
    // usually a deliberate, narrow look-target, while LGH's door/drawer detection is a wider
    // "anything in front of me" raycast that's more likely to be incidentally in view.
    //
    // Known trade-off: disabling PlayerInteractor fires its OnEnable/OnDisable, which clears the
    // static PlayerInteractor.Active reference LGH's DoorNetworkSync uses to deliver "locked door"
    // messages. A message that arrives in the same instant KJH has a target focused would be
    // dropped silently. Narrow edge case (only matters while aiming at a KJH target), not fixed
    // here because fixing it would mean touching DoorNetworkSync.cs.
    public class InteractionMediator : MonoBehaviour
    {
        public LGH.PlayerInteractor mapInteractor;
        public CanvasGroup gimmickPromptGroup;
        public float visibleThreshold = 0.01f;

        void LateUpdate()
        {
            if (mapInteractor == null) return;
            bool gimmickFocused = gimmickPromptGroup != null && gimmickPromptGroup.alpha > visibleThreshold;
            if (mapInteractor.enabled == gimmickFocused) mapInteractor.enabled = !gimmickFocused;
        }
    }
}
