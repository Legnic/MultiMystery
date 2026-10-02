using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public float moveSpeed = 4.0f;
        public float mouseSensitivity = 2.0f;
        public float gravity = -9.81f;
        public float jumpHeight = 1.2f;
        public Transform cameraTransform;

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

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
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

            Vector3 moveDir = (transform.right * h + transform.forward * v);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            if (controller.isGrounded)
            {
                verticalVelocity = -0.5f;
                if (kb.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * moveSpeed;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
