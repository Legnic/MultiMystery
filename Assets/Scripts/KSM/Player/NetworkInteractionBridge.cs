using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CoopDemo
{
    // FPSPlayer는 네트워크로 스폰되는 프리팹이라, 씬에 있는 InteractionPromptUI나
    // 자기 자신의 카메라 같은 참조를 프리팹 단계에서 미리 연결해둘 수 없다.
    // 그래서 스폰 직후(OnNetworkSpawn)에 코드로 찾아서 주입하고, 소유자(Owner)가 아니면
    // 상호작용 감지 자체를 꺼서 상대방 화면에서 중복으로 레이캐스트/안내 문구가 뜨지 않게 한다.
    [RequireComponent(typeof(InteractionController))]
    public class NetworkInteractionBridge : NetworkBehaviour
    {
        InteractionController interactionController;
        ObjectEchoController echoController;
        CameraModeController cameraModeController;
        PhotoInventory photoInventory;
        PhotoCaptureSystem photoCaptureSystem;

        void Awake()
        {
            interactionController = GetComponent<InteractionController>();
            echoController = GetComponent<ObjectEchoController>();
            cameraModeController = GetComponent<CameraModeController>();
            photoInventory = GetComponent<PhotoInventory>();
            photoCaptureSystem = GetComponent<PhotoCaptureSystem>();
        }

        public override void OnNetworkSpawn()
        {
            interactionController.enabled = IsOwner;
            if (echoController != null) echoController.enabled = IsOwner;
            // 사진 촬영 관련 컴포넌트도 소유자가 아니면 꺼서, 상대방 화면에서 Tab/E 입력이 중복으로
            // 처리되거나(인벤토리 토글 등) 카메라 전환 연출이 겹치지 않게 한다.
            if (cameraModeController != null) cameraModeController.enabled = IsOwner;
            if (photoInventory != null) photoInventory.enabled = IsOwner;
            if (photoCaptureSystem != null) photoCaptureSystem.enabled = IsOwner;
            if (!IsOwner) return;

            Camera ownCamera = GetComponentInChildren<Camera>(true);
            InteractionPromptUI promptUI = FindAnyObjectByType<InteractionPromptUI>(FindObjectsInactive.Include);
            interactionController.ConfigureForOwner(ownCamera, promptUI);
            if (echoController != null)
            {
                echoController.SetViewCamera(ownCamera);
                CanvasGroup blackout = GameObject.Find("Canvas")?.transform.Find("EchoBlackout")?.GetComponent<CanvasGroup>();
                echoController.SetBlackoutOverlay(blackout);
            }

            ConfigurePhotoSystemForOwner();
        }

        // 사진 촬영 시스템(CameraModeController/PhotoInventory/PhotoCaptureSystem)이 쓰는 Canvas UI/Volume은
        // 전부 씬(Develop)에 있는 오브젝트라 프리팹에 미리 연결해둘 수 없다 — 이름으로 찾아서 주입한다.
        // 씬에 해당 UI가 없는 경우(아직 안 만들어졌거나 이름이 바뀐 경우)에도 null 그대로 넘어가 각 컴포넌트가
        // 알아서 "참조 없음"으로 동작하도록 둔다(동작은 하되 그 연출만 생략됨).
        void ConfigurePhotoSystemForOwner()
        {
            Transform canvas = GameObject.Find("Canvas")?.transform;
            if (canvas == null) return;

            if (cameraModeController != null)
            {
                Volume viewfinderVolume = GameObject.Find("ViewfinderVolume")?.GetComponent<Volume>();
                CanvasGroup viewfinderFrame = canvas.Find("ViewfinderFrame")?.GetComponent<CanvasGroup>();
                cameraModeController.ConfigureForOwner(viewfinderVolume, viewfinderFrame);
            }

            if (photoInventory != null)
            {
                Transform invPanel = canvas.Find("PhotoInventoryPanel");
                Transform enlargedPanel = canvas.Find("EnlargedViewPanel");
                Transform viewfinderFrame = canvas.Find("ViewfinderFrame");
                photoInventory.ConfigureForOwner(
                    invPanel?.gameObject,
                    invPanel != null ? invPanel.Find("ThumbnailParent") : null,
                    enlargedPanel?.gameObject,
                    enlargedPanel != null ? enlargedPanel.Find("EnlargedImage")?.GetComponent<Image>() : null,
                    enlargedPanel != null ? enlargedPanel.Find("TitleText")?.GetComponent<Text>() : null,
                    enlargedPanel != null ? enlargedPanel.Find("DescriptionText")?.GetComponent<Text>() : null,
                    viewfinderFrame != null ? viewfinderFrame.Find("RemainingCountText")?.GetComponent<Text>() : null);
            }

            if (photoCaptureSystem != null)
            {
                FlashEffect flashEffect = canvas.Find("FlashImage")?.GetComponent<FlashEffect>();
                PhotoAcquiredToast acquiredToast = canvas.Find("AcquiredToast")?.GetComponent<PhotoAcquiredToast>();
                photoCaptureSystem.ConfigureForOwner(flashEffect, acquiredToast);
            }
        }
    }
}
