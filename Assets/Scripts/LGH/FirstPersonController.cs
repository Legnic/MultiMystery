using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour, IPlayerLock
    {
        public float moveSpeed = 4.0f;
        public float mouseSensitivity = 2.0f;
        public float gravity = -9.81f;
        public float jumpHeight = 1.2f;
        public Transform cameraTransform;

        // IPlayerLock: lets a cutscene-style interaction (object echo, clue viewing, ...) pause
        // movement/look and drive the camera itself without needing to know this is a
        // FirstPersonController specifically.
        public float SpeedMultiplier { get; set; } = 1f;
        public bool LookEnabled { get; set; } = true;
        public float Yaw => transform.eulerAngles.y;
        public float Pitch => pitch;
        public void SetLookAngles(float yaw, float pitchValue)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitch = Mathf.Clamp(pitchValue, -80f, 80f);
            if (cameraTransform != null) cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // ladder the player is holding on to (null = walking)
        public Ladder Climbing { get; private set; }

        public void StartClimb(Ladder ladder)
        {
            if (ladder == null || controller == null) return;
            Vector3 delta = ladder.GrabPoint(controller) - controller.bounds.center;
            controller.enabled = false;
            transform.position += delta;
            controller.enabled = true;
            verticalVelocity = 0f;
            Climbing = ladder;
            climbFrame = Time.frameCount;
        }

        public void StopClimb() { Climbing = null; verticalVelocity = 0f; climbFrame = Time.frameCount; }
        int climbFrame = -1;
        // true on the frame the player got on / off, so the same E press is not used twice
        public bool ClimbChangedThisFrame { get { return climbFrame == Time.frameCount; } }

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // Esc that just closed the bag must not also open the pause menu
            if (kb.escapeKey.wasPressedThisFrame && !PlayerInventory.EscapeConsumed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (LookEnabled && mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue() * (mouseSensitivity * 0.02f);
                transform.Rotate(Vector3.up, delta.x);
                pitch -= delta.y;
                pitch = Mathf.Clamp(pitch, -80f, 80f);
                if (cameraTransform != null)
                    cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            float h = 0f, v = 0f;
            if (kb.wKey.isPressed) v += 1f;
            if (kb.sKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed) h += 1f;
            if (kb.aKey.isPressed) h -= 1f;

            // typing in a menu text box (nickname) must not walk the player around
            bool typing = (MultiplayerMenu.Typing || GUIUtility.keyboardControl != 0) && Cursor.lockState != CursorLockMode.Locked;
            if (typing) { h = 0f; v = 0f; }

            Vector3 moveDir = (transform.right * h + transform.forward * v);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // on a ladder (got on with E): W / S climb, no gravity
            if (Climbing != null)
            {
                bool letGo = !Climbing.isActiveAndEnabled || !Climbing.Holds(controller)
                             || (controller.isGrounded && (v < 0f || h != 0f))          // at the bottom: step back / sideways
                             || (kb.spaceKey.wasPressedThisFrame && !typing)
                             || (kb.eKey.wasPressedThisFrame && !typing && Time.frameCount != climbFrame);   // E lets go anywhere
                if (letGo) StopClimb();
                else
                {
                    verticalVelocity = 0f;
                    controller.Move(Climbing.ClimbVelocity(controller, moveDir, v, moveSpeed, Time.deltaTime) * SpeedMultiplier * Time.deltaTime);
                    return;
                }
            }

            if (controller.isGrounded)
            {
                verticalVelocity = -0.5f;
                if (kb.spaceKey.wasPressedThisFrame && !typing)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * moveSpeed * SpeedMultiplier;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
