using System.Collections.Generic;
using UnityEngine;

namespace LGH
{
    // A ladder. Look at it and press E to get on: then W climbs up, S climbs down (no gravity).
    // E again (or Space) lets go; stepping off at the top or reaching the floor with S also gets you off.
    // The BoxCollider on this object (trigger) is the climbing space; a solid child collider is what you aim at.
    [RequireComponent(typeof(BoxCollider))]
    public class Ladder : MonoBehaviour, IWorldInteractable
    {
        public static readonly List<Ladder> All = new List<Ladder>();

        public float climbSpeed = 2.2f;
        [Tooltip("World height of the floor at the top. Below it the player is kept inside the ladder space.")]
        public float exitHeight = 7.78f;
        public string prompt = "[E] 사다리 타기";

        BoxCollider zone;

        public Bounds Area { get { if (zone == null) zone = GetComponent<BoxCollider>(); return zone.bounds; } }

        void OnEnable() { zone = GetComponent<BoxCollider>(); zone.isTrigger = true; if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ---- IWorldInteractable
        public bool CanInteract { get { return true; } }
        public string InteractPrompt
        {
            get
            {
                var me = PlayerInteractor.Active != null ? PlayerInteractor.Active.GetComponent<FirstPersonController>() : null;
                return me != null && me.Climbing == this ? "[E] 사다리에서 내리기" : prompt;
            }
        }
        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            var fpc = interactor.GetComponentInParent<FirstPersonController>();
            if (fpc == null) fpc = interactor.GetComponentInChildren<FirstPersonController>();
            if (fpc == null || fpc.ClimbChangedThisFrame) return;
            if (fpc.Climbing == this) fpc.StopClimb();
            else fpc.StartClimb(this);
        }

        public bool Holds(CharacterController cc) { return cc != null && Area.Contains(cc.bounds.center); }

        // where to put the player when they grab the ladder (centre of the body, inside the climbing space)
        public Vector3 GrabPoint(CharacterController cc)
        {
            var b = Area;
            Vector3 c = cc.bounds.center;
            float r = cc.radius + 0.03f, half = cc.height * 0.5f;
            return new Vector3(
                Mathf.Clamp(c.x, b.min.x + r, b.max.x - r),
                Mathf.Clamp(c.y, b.min.y + half + 0.02f, b.max.y - 0.15f),
                Mathf.Clamp(c.z, b.min.z + r, b.max.z - r - 0.16f));   // stay clear of the rungs
        }

        // movement for one frame while on the ladder
        public Vector3 ClimbVelocity(CharacterController cc, Vector3 moveDir, float v, float walkSpeed, float dt)
        {
            Vector3 vel = moveDir * walkSpeed * 0.45f;
            vel.y = v * climbSpeed;
            if (cc.bounds.min.y < exitHeight && dt > 0f)
            {
                var b = Area;
                float r = Mathf.Min(cc.radius, Mathf.Min(b.extents.x, b.extents.z) - 0.01f);
                Vector3 c = cc.bounds.center;
                Vector3 next = c + vel * dt;
                if ((next.x < b.min.x + r && vel.x < 0f) || (next.x > b.max.x - r && vel.x > 0f)) vel.x = 0f;
                if ((next.z < b.min.z + r && vel.z < 0f) || (next.z > b.max.z - r && vel.z > 0f)) vel.z = 0f;
            }
            return vel;
        }
    }
}
