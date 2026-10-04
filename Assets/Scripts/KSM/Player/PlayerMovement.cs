using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float gravity = -9.81f;
        [SerializeField] Camera playerCamera;

        CharacterController m_Controller;
        NetworkTransform m_NetworkTransform;
        Vector3 m_VerticalVelocity;

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_NetworkTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(IsOwner);
            }

            if (IsOwner)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null && mainCamera.gameObject != (playerCamera != null ? playerCamera.gameObject : null))
                {
                    mainCamera.gameObject.SetActive(false);
                }

                // transform.position을 직접 대입하면 NetworkTransform이 다음 틱에
                // 기존 값으로 되돌린다. Teleport()로 설정해야 네트워크 상태에도 반영된다.
                // CharacterController가 켜진 채로 텔레포트하면 다음 Move() 호출 때
                // 컨트롤러가 내부에 캐시된 이전 위치로 되돌리므로, 잠깐 꺼야 한다.
                m_Controller.enabled = false;
                m_NetworkTransform.Teleport(GetSpawnPosition(), transform.rotation, transform.localScale);
                m_Controller.enabled = true;
            }
        }

        Vector3 GetSpawnPosition()
        {
            float offset = OwnerClientId == 0 ? -1.5f : 1.5f;
            return new Vector3(offset, 1f, 0f);
        }

        void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;

            Vector3 move = new Vector3(input.x, 0f, input.y);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            if (m_Controller.isGrounded)
            {
                m_VerticalVelocity = Vector3.zero;
            }
            else
            {
                m_VerticalVelocity += Vector3.up * (gravity * Time.deltaTime);
            }

            m_Controller.Move((move * moveSpeed + m_VerticalVelocity) * Time.deltaTime);
            // 주의: 여기에 transform.forward = move; 같은 회전 코드를 추가하면
            // 카메라가 캡슐 자식이라 같이 돌아가며 시야가 흔들린다. 넣지 말 것.
        }
    }
}
