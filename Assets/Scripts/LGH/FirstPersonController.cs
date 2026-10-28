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

        [Header("달리기")]
        public float sprintMultiplier = 1.6f;
        [Header("웅크리기")]
        [Tooltip("선 키 대비 웅크렸을 때 키 비율")]
        public float crouchHeightMultiplier = 0.6f;
        public float crouchSpeedMultiplier = 0.55f;
        public float crouchTransitionSpeed = 8f;

        public bool IsCrouching { get { return crouching; } }

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

        bool crouching;
        float standingHeight, crouchHeight;
        Vector3 standingCenter, crouchCenter;
        float standingCamY, crouchCamY;
        float crouchT;                 // 0 = 서 있음 ... 1 = 완전히 웅크림
        CapsuleCollider bodyBlocker;   // 플레이어끼리 서로 통과하지 못하게 하는 몸통 콜라이더 — 웅크리면 같이 작아져야 한다

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }

            standingHeight = controller.height;
            standingCenter = controller.center;
            crouchHeight = Mathf.Max(0.3f, standingHeight * crouchHeightMultiplier);
            float drop = standingHeight - crouchHeight;
            crouchCenter = standingCenter - new Vector3(0f, drop * 0.5f, 0f);
            standingCamY = cameraTransform != null ? cameraTransform.localPosition.y : 0f;
            crouchCamY = standingCamY - drop;

            // CharacterController와 같은 크기로 맞춰 둔 몸통 콜라이더만 따라 줄인다 (다른 용도의 콜라이더는 건드리지 않는다)
            var cap = GetComponent<CapsuleCollider>();
            if (cap != null && !cap.isTrigger && Mathf.Abs(cap.height - standingHeight) < 0.05f) bodyBlocker = cap;
        }

        // 일어서려는 자리 바로 위가 막혀 있지 않은지(낮은 천장 등) 확인한다
        bool CanStandUp()
        {
            // 지금 캡슐 머리 위로 "서 있을 때 키까지 모자란 만큼" 쏴 본다 (전환 중에도 맞는다)
            float need = standingHeight - controller.height;
            if (need <= 0.001f) return true;
            Vector3 origin = transform.position + Vector3.up * (controller.height - controller.radius);
            return !Physics.SphereCast(origin, controller.radius * 0.95f, Vector3.up, out _, need + 0.05f,
                ~0, QueryTriggerInteraction.Ignore);
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
            // cursor unlocked = some menu is up for THIS player only (pause/options/inventory/reading/...) — WASD/jump must stop too,
            // not just mouse-look. The other player's game keeps running; this only reads this player's own Cursor state.
            bool paused = Cursor.lockState != CursorLockMode.Locked;
            if (typing || paused) { h = 0f; v = 0f; }

            Vector3 moveDir = (transform.right * h + transform.forward * v);
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            // 웅크리기: 왼쪽 Ctrl을 누르고 있는 동안 웅크린다. 떼도 머리 위가 막혀 있으면 일어나지 못한다.
            bool wantCrouch = !typing && !paused && kb.leftCtrlKey.isPressed;
            if (!wantCrouch && crouching && !CanStandUp()) wantCrouch = true;
            crouching = wantCrouch;

            // 높이·중심·카메라를 따로따로 움직이면 중심이 높이보다 두 배 빨리 움직여서 캡슐 바닥이 바닥 아래로 파고든다(웅크릴 때)/떠오른다(일어설 때).
            // 하나의 비율(crouchT)로 셋을 같이 보간해서 캡슐 바닥이 항상 발 높이(0)에 붙어 있게 한다.
            float prevT = crouchT;
            crouchT = Mathf.MoveTowards(crouchT, crouching ? 1f : 0f, crouchTransitionSpeed * Time.deltaTime);
            controller.height = Mathf.Lerp(standingHeight, crouchHeight, crouchT);
            controller.center = Vector3.Lerp(standingCenter, crouchCenter, crouchT);
            if (bodyBlocker != null)
            {
                bodyBlocker.height = controller.height;
                bodyBlocker.center = controller.center;
            }
            if (cameraTransform != null && crouchT != prevT)
            {
                // 다른 스크립트가 카메라 높이를 건드려도 덮어쓰지 않게, 이번 프레임에 변한 만큼만 더한다
                var camLocal = cameraTransform.localPosition;
                camLocal.y += (crouchCamY - standingCamY) * (crouchT - prevT);
                cameraTransform.localPosition = camLocal;
            }

            // 달리기: 왼쪽 Shift를 누르고 있는 동안, 웅크리지 않았고 실제로 움직이고 있을 때만
            bool sprinting = !typing && !paused && !crouching && kb.leftShiftKey.isPressed && moveDir.sqrMagnitude > 0.01f;
            float speedMul = crouching ? crouchSpeedMultiplier : (sprinting ? sprintMultiplier : 1f);

            // on a ladder (got on with E): W / S climb, no gravity
            if (Climbing != null)
            {
                bool letGo = !Climbing.isActiveAndEnabled || !Climbing.Holds(controller)
                             || (controller.isGrounded && (v < 0f || h != 0f))          // at the bottom: step back / sideways
                             || (kb.spaceKey.wasPressedThisFrame && !typing && !paused)
                             || (kb.eKey.wasPressedThisFrame && !typing && !paused && Time.frameCount != climbFrame);   // E lets go anywhere
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
                if (kb.spaceKey.wasPressedThisFrame && !typing && !paused)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * moveSpeed * speedMul * SpeedMultiplier;
            velocity.y = verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
