using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace KSM
{
    // KJH's gimmick components (Photo/Clue/ObjectEcho) need references to scene-only objects
    // (Develop's GameUICanvas panels, the ViewfinderVolume) that a prefab can't hold, because a
    // prefab asset can't point at something that only exists in one particular scene. This finds
    // those by name once the player spawns and hands them over through the ConfigureForOwner/Set*
    // methods each script already exposes for exactly this purpose (none of them are new).
    // Remote players get every gimmick component disabled so they don't run their own detection.
    public class GimmickInjectionBridge : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            bool mine = IsOwner;

            var interactionController = GetComponent<InteractionController>();
            var photoCapture = GetComponent<PhotoCaptureSystem>();
            var photoInventory = GetComponent<PhotoInventory>();
            var clueJournal = GetComponent<ClueJournal>();
            var objectEcho = GetComponent<ObjectEchoController>();
            var cameraModeController = GetComponent<CameraModeController>();
            var mediator = GetComponent<InteractionMediator>();
            var mapInteractor = GetComponent<LGH.PlayerInteractor>();

            if (interactionController != null) interactionController.enabled = mine;
            if (photoCapture != null) photoCapture.enabled = mine;
            if (photoInventory != null) photoInventory.enabled = mine;
            if (clueJournal != null) clueJournal.enabled = mine;
            if (objectEcho != null) objectEcho.enabled = mine;
            if (cameraModeController != null) cameraModeController.enabled = mine;
            if (mediator != null) mediator.enabled = mine;

            if (!mine) return;

            Camera cam = GetComponentInChildren<Camera>(true);
            GameObject canvas = GameObject.Find("GameUICanvas");
            if (canvas == null)
            {
                Debug.LogWarning("[GimmickInjectionBridge] GameUICanvas not found in scene — gimmick UI will not work.");
                return;
            }
            Transform ct = canvas.transform;

            Transform promptT = ct.Find("Common/InteractionPrompt");
            var promptUI = promptT != null ? promptT.GetComponent<InteractionPromptUI>() : null;
            if (interactionController != null && promptUI != null) interactionController.ConfigureForOwner(cam, promptUI);

            if (objectEcho != null)
            {
                objectEcho.SetViewCamera(cam);
                Transform blackoutT = ct.Find("ObjectEcho/EchoBlackout");
                if (blackoutT != null) objectEcho.SetBlackoutOverlay(blackoutT.GetComponent<CanvasGroup>());
            }

            if (photoCapture != null)
            {
                Transform flashT = ct.Find("Photo/FlashImage");
                Transform toastT = ct.Find("Common/AcquiredToast");
                photoCapture.ConfigureForOwner(
                    flashT != null ? flashT.GetComponent<FlashEffect>() : null,
                    toastT != null ? toastT.GetComponent<PhotoAcquiredToast>() : null);
            }

            if (photoInventory != null)
            {
                Transform panelT = ct.Find("Photo/PhotoInventoryPanel");
                Transform thumbT = ct.Find("Photo/PhotoInventoryPanel/ThumbnailParent");
                Transform enlargedT = ct.Find("Photo/EnlargedViewPanel");
                Transform countT = ct.Find("Photo/ViewfinderFrame/RemainingCountText");
                photoInventory.ConfigureForOwner(
                    panelT != null ? panelT.gameObject : null,
                    thumbT,
                    enlargedT != null ? enlargedT.gameObject : null,
                    enlargedT != null ? enlargedT.Find("EnlargedImage")?.GetComponent<Image>() : null,
                    enlargedT != null ? enlargedT.Find("TitleText")?.GetComponent<Text>() : null,
                    enlargedT != null ? enlargedT.Find("DescriptionText")?.GetComponent<Text>() : null,
                    countT != null ? countT.GetComponent<Text>() : null);
            }

            if (cameraModeController != null)
            {
                GameObject viewfinderVolumeObj = GameObject.Find("ViewfinderVolume");
                Transform frameT = ct.Find("Photo/ViewfinderFrame");
                cameraModeController.ConfigureForOwner(
                    viewfinderVolumeObj != null ? viewfinderVolumeObj.GetComponent<Volume>() : null,
                    frameT != null ? frameT.GetComponent<CanvasGroup>() : null);
            }

            if (mediator != null)
            {
                mediator.mapInteractor = mapInteractor;
                mediator.gimmickPromptGroup = promptT != null ? promptT.GetComponent<CanvasGroup>() : null;
            }
        }
    }
}
