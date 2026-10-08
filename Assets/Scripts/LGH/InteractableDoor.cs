using UnityEngine;

namespace LGH
{
    // Hinged door that swings away from whoever opens it.
    // Single player: PlayerInteractor calls Interact().
    // Multiplayer: PlayerInteractor asks DoorNetworkSync, the host decides, and every player calls ApplyState().
    public class InteractableDoor : MonoBehaviour
    {
        [Header("Parts")]
        public Transform hinge;                 // pivot at the jamb; its first child is the door leaf
        public InteractableDoor partner;        // other leaf of a double door (opens together)

        [Header("Motion")]
        public float openAngle = 95f;
        public float swingSpeed = 220f;         // degrees per second

        [Header("Lock")]
        public bool locked = false;
        public string lockedMessage = "잠겨 있다";
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

        void Awake() { Init(); }

        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (hinge == null) hinge = transform;
            closedLocal = useSavedClosedRotation ? closedRotation : hinge.localRotation;
            hinge.localRotation = closedLocal;   // start shut even if the scene was saved with the door open
            Vector3 d = hinge.childCount > 0 ? hinge.GetChild(0).position - hinge.position : hinge.right;
            d = Vector3.ProjectOnPlane(d, Vector3.up).normalized;
            leafDirParent = hinge.parent != null ? hinge.parent.InverseTransformDirection(d) : d;
        }

        // Why the door can't be used right now (null = it can).
        public string CheckBlocked(Vector3 userPosition)
        {
            if (locked) return lockedMessage;
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
            Vector3 leafDir = hinge.parent != null ? hinge.parent.TransformDirection(leafDirParent) : leafDirParent;
            Vector3 normal = Vector3.Cross(Vector3.up, leafDir);
            float userSide = Mathf.Sign(Vector3.Dot(userPosition - hinge.position, normal));
            Vector3 swung = Quaternion.AngleAxis(openAngle, Vector3.up) * leafDir;
            return Vector3.Dot(swung, normal) * userSide > 0f ? -openAngle : openAngle;
        }

        // Local (single player) use. Returns a message to show, or null when the door moved.
        public string Interact(Vector3 userPosition)
        {
            string blocked = CheckBlocked(userPosition);
            if (blocked != null) return blocked;
            SetOpen(!IsOpen, userPosition);
            if (partner != null) partner.SetOpen(IsOpen, userPosition);
            return null;
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

        public void Unlock() { locked = false; }
        public void Lock() { locked = true; }

        void Update()
        {
            if (Mathf.Approximately(currentAngle, targetAngle)) return;
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
            hinge.localRotation = closedLocal * Quaternion.AngleAxis(currentAngle, Vector3.up);
        }
    }
}
