using UnityEngine;

namespace LGH
{
    // One touchable spot on the star chest: a quadrant number (index 0-3 = I-IV) or the open button (index 4).
    public class StarLockPart : MonoBehaviour, IWorldInteractable
    {
        public StarLockBox box;
        [Tooltip("0-3 = quadrant I, II, III, IV   4 = the button")]
        public int index;

        // E on any part of the chest opens the close-up view; numbers are then turned with the mouse
        public bool CanInteract { get { return box != null && box.CanInteract; } }
        public string InteractPrompt { get { return box != null ? box.InteractPrompt : null; } }
        public void Interact(GameObject interactor) { if (box != null) box.ToggleInspect(interactor); }
    }
}
