using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    /// <summary>
    /// LGH의 FirstPersonController(자유 시점 1인칭 이동)를 네트워크 플레이어용으로 옮긴 버전.
    /// 입력/카메라/커서는 소유자(Owner)에게만 적용하고, 위치·좌우 회전은 NetworkTransform이 동기화한다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetworkFirstPersonController : NetworkBehaviour, IPlayerLock
    {
        [SerializeField] float moveSpeed = 4f;
        [SerializeField] float mouseSensitivity = 2f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] Camera playerCamera;

        [Header("스폰 위치 (호스트=0, 클라이언트=1 순으로 spacing 만큼 벌려 배치)")]
        [SerializeField] Vector3 spawnOrigin = new Vector3(-0.2f, 0.05f, 6.6f);
        [SerializeField] float spawnSpacing = 1f;

        CharacterController m_Controller;
        NetworkTransform m_NetworkTransform;
        float m_VerticalVelocity;
        float m_Pitch;

        // ── IPlayerLock (ObjectEchoController 등이 이동/시점을 잠그는 데 씀) ──
        public float SpeedMultiplier { get; set; } = 1f;
        public bool LookEnabled { get; set; } = true;
        public float Yaw => transform.eulerAngles.y;
        public float Pitch => m_Pitch;

        public void SetLookAngles(float yaw, float pitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            m_Pitch = Mathf.Clamp(pitch, -80f, 80f);
            if (playerCamera != null) playerCamera.transform.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
        }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_NetworkTransform = GetComponent<NetworkTransform>();
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }
        }

        public override void OnNetworkSpawn()
        {
            // 카메라/오디오 리스너는 내 플레이어 것만 켠다 (상대 플레이어의 카메라가 화면을 덮지 않도록).
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(IsOwner);
            }

            if (!IsOwner)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera != null && playerCamera != null && mainCamera.gameObject != playerCamera.gameObject)
            {
                mainCamera.gameObject.SetActive(false);
            }

            // transform.position 직접 대입은 NetworkTransform이 되돌리고,
            // CharacterController가 켜진 채 텔레포트하면 다음 Move()에서 캐시된 위치로 돌아간다.
            // → 컨트롤러 끄기 → Teleport → 켜기 순서 필수 (KSM PlayerMovement와 동일한 이유).
            m_Controller.enabled = false;
            m_NetworkTransform.Teleport(GetSpawnPosition(), transform.rotation, transform.localScale);
            m_Controller.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        Vector3 GetSpawnPosition()
        {
            float side = OwnerClientId == 0 ? -0.5f : 0.5f;
            return spawnOrigin + Vector3.right * (side * spawnSpacing * 2f);
        }

        void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null)
            {
                return;
            }

            // ESC로 커서 해제(UI 조작), 화면 클릭하면 다시 잠금.
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked
                     && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (LookEnabled && mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue() * (mouseSensitivity * 0.02f);
                transform.Rotate(Vector3.up, delta.x);
                m_Pitch = Mathf.Clamp(m_Pitch - delta.y, -80f, 80f);
                if (playerCamera != null)
                {
                    playerCamera.transform.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
                }
            }

            float h = 0f, v = 0f;
            if (kb.wKey.isPressed) v += 1f;
            if (kb.sKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed) h += 1f;
            if (kb.aKey.isPressed) h -= 1f;

            Vector3 moveDir = transform.right * h + transform.forward * v;
            if (moveDir.sqrMagnitude > 1f)
            {
                moveDir.Normalize();
            }

            if (m_Controller.isGrounded)
            {
                m_VerticalVelocity = -0.5f;
                // LookEnabled를 "연출 중 입력 잠금" 신호로 같이 써서, 사물 소리를 듣는 동안은 점프도 막는다.
                if (LookEnabled && kb.spaceKey.wasPressedThisFrame)
                {
                    m_VerticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                m_VerticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 velocity = moveDir * (moveSpeed * SpeedMultiplier);
            velocity.y = m_VerticalVelocity;
            m_Controller.Move(velocity * Time.deltaTime);
        }
    }
}
