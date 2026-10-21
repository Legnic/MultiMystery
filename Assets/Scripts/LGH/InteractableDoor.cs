using UnityEngine;

namespace LGH
{
    // Hinged door that swings away from whoever opens it.
    // Single player: PlayerInteractor calls Interact().
    // Multiplayer: PlayerInteractor asks DoorNetworkSync, the host decides, and every player calls ApplyState().
    public class InteractableDoor : MonoBehaviour, IRoomResettable
    {
        [Header("Parts")]
        public Transform hinge;                 // pivot at the jamb; its first child is the door leaf
        public InteractableDoor partner;        // other leaf of a double door (opens together)

        [Header("Motion")]
        public float openAngle = 95f;
        public float swingSpeed = 220f;         // degrees per second
        [Tooltip("On: always swings by openAngle (trapdoors / hatches). Off: swings away from the player.")]
        public bool fixedSwing = false;

        [Header("Lock")]
        public bool locked = false;
        public string lockedMessage = "잠겨 있다";
        [Tooltip("이 아이템(열쇠)을 가진 플레이어는 잠긴 문을 열 수 있다. 비워두면 열쇠로는 열리지 않는다.")]
        public ItemData keyItem;
        [Tooltip("문을 열 때 열쇠를 인벤토리에서 없앤다")]
        public bool consumeKey = false;
        public bool oneWay = false;
        public Vector3 openSideLocal = Vector3.forward;   // side (in this object's local space) the door can be opened from
        public string oneWayMessage = "이쪽에서는 열리지 않는다";

        public bool IsOpen { get; private set; }
        public float TargetAngle { get { return targetAngle; } }

        [Header("Closed pose")]
        [Tooltip("Hinge rotation when the door is shut. Saved with the scene so a door that was saved half open still knows where 'closed' is.")]
        [SerializeField] Quaternion closedRotation = Quaternion.identity;
        [SerializeField] bool useSavedClosedRotation = true;

        Quaternion closedLocal;
        Vector3 leafDirParent;
        float currentAngle, targetAngle;
        bool initialized;
        bool initialLocked;

        void Awake() { Init(); }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            initialLocked = locked;   // remember the authored lock state, in case a puzzle unlocks it later
            if (hinge == null) hinge = transform;
            closedLocal = useSavedClosedRotation ? closedRotation : hinge.localRotation;
            hinge.localRotation = closedLocal;   // start shut even if the scene was saved with the door open
            Vector3 d = hinge.childCount > 0 ? hinge.GetChild(0).position - hinge.position : hinge.right;
            d = Vector3.ProjectOnPlane(d, Vector3.up).normalized;
            leafDirParent = hinge.parent != null ? hinge.parent.InverseTransformDirection(d) : d;
        }

        // Why the door can't be used right now (null = it can).
        public string CheckBlocked(Vector3 userPosition, PlayerInventory inventory = null)
        {
            if (locked && !CanUnlockWith(inventory)) return keyItem != null ? keyItem.NameOrFallback + "이(가) 필요하다" : lockedMessage;
            if (!IsOpen && oneWay)
            {
                Vector3 side = transform.TransformDirection(openSideLocal);
                if (Vector3.Dot(userPosition - transform.position, side) < 0f) return oneWayMessage;
            }
            return null;
        }

        // Angle that swings the door away from the user.
        public float ComputeAngle(bool open, Vector3 userPosition)
        {
            Init();
            if (!open) return 0f;
            if (fixedSwing) return openAngle;
            Vector3 leafDir = hinge.parent != null ? hinge.parent.TransformDirection(leafDirParent) : leafDirParent;
            Vector3 normal = Vector3.Cross(Vector3.up, leafDir);
            float userSide = Mathf.Sign(Vector3.Dot(userPosition - hinge.position, normal));
            Vector3 swung = Quaternion.AngleAxis(openAngle, Vector3.up) * leafDir;
            return Vector3.Dot(swung, normal) * userSide > 0f ? -openAngle : openAngle;
        }

        // Local (single player) use. Returns a message to show, or null when the door moved.
        public string Interact(Vector3 userPosition, PlayerInventory inventory = null)
        {
            string blocked = CheckBlocked(userPosition, inventory);
            if (blocked != null) return blocked;
            string keyMsg = locked ? UseKey(inventory) : null;
            SetOpen(!IsOpen, userPosition);
            if (partner != null) partner.SetOpen(IsOpen, userPosition);
            return keyMsg;
        }

        public void SetOpen(bool open, Vector3 userPosition)
        {
            ApplyState(open, ComputeAngle(open, userPosition));
        }

        public void ApplyState(bool open, float angle, bool instant = false)
        {
            Init();
            IsOpen = open;
            targetAngle = open ? angle : 0f;
            if (instant)
            {
                currentAngle = targetAngle;
                hinge.localRotation = closedLocal * Quaternion.AngleAxis(currentAngle, Vector3.up);
            }
        }

        // Puzzle scripts: in multiplayer call DoorNetworkSync.Instance.SetLocked(door, false) on the host instead.
        [ContextMenu("Use current rotation as closed")]
        void CaptureClosed() { closedRotation = (hinge != null ? hinge : transform).localRotation; useSavedClosedRotation = true; }

        public bool CanUnlockWith(PlayerInventory inventory) { return locked && keyItem != null && inventory != null && inventory.HasItem(keyItem); }

        // Single player: open the lock with the key in the bag. Returns the message to show.
        public string UseKey(PlayerInventory inventory)
        {
            if (!CanUnlockWith(inventory)) return null;
            Unlock();
            if (consumeKey) inventory.RemoveItem(keyItem, 1);
            return keyItem.NameOrFallback + "(으)로 문을 열었다";
        }

        public void Unlock() { locked = false; if (partner != null) partner.locked = false; }
        public void Lock() { locked = true; if (partner != null) partner.locked = true; }

        public void ResetForNewRoom()
        {
            locked = initialLocked;
            ApplyState(false, 0f, true);
        }

        void Update()
        {
            if (Mathf.Approximately(currentAngle, targetAngle)) return;
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
            hinge.localRotation = closedLocal * Quaternion.AngleAxis(currentAngle, Vector3.up);
        }
    }
}
