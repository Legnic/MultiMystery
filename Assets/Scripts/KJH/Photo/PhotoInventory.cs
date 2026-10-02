using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 찍은 사진들을 보관하고, Tab 키로 목록 UI를 열고 닫으며, 썸네일 클릭 시 제목/설명과 함께
// 확대 보기를 담당한다.
// 보관 방식: 실제로 저장하는 건 PhotoItemData의 photoId 목록 뿐이다 (설계 조건 참고 —
// 나중에 네트워크로 동기화할 때 무거운 텍스처 대신 ID 문자열만 주고받기 위함).
// 화면에 보여줄 스프라이트/제목/설명은 촬영 시점에 넘겨받은 PhotoItemData를 런타임에만
// 들고 있는 조회용 사전(itemLookup)에서 꺼내 쓴다.
// 사용법: Canvas 아래의 빈 오브젝트(예: PhotoInventory)에 붙인다.
public class PhotoInventory : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Transform thumbnailParent;
    [SerializeField] private GameObject thumbnailButtonPrefab;
    [SerializeField] private GameObject enlargedViewPanel;
    [SerializeField] private Image enlargedImage;
    [SerializeField] private Text enlargedTitleText;
    [SerializeField] private Text enlargedDescriptionText;
    [Tooltip("남은/촬영한 장수를 표시할 텍스트. 비워둬도 동작에는 문제 없다.")]
    [SerializeField] private Text remainingCountText;

    [Header("입력 액션")]
    [SerializeField] private InputActionReference toggleInventoryAction;
    [SerializeField] private InputActionReference closePhotoAction;

    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("촬영 중(뷰파인더 들어가 있는 동안)에는 인벤토리를 못 열게 막기 위한 참조. 비워둬도 동작한다.")]
    [SerializeField] private PhotoCaptureSystem photoCaptureSystem;

    [Header("설정")]
    [Tooltip("최대로 보관할 수 있는 사진 장수. 0 이하로 두면 '제한 없음'으로 동작한다 (기본값).")]
    [SerializeField] private int maxPhotos = 0;

    public bool IsOpen { get; private set; }

    private bool IsUnlimited => maxPhotos <= 0;
    public int RemainingSlots => IsUnlimited ? int.MaxValue : Mathf.Max(0, maxPhotos - capturedPhotoIds.Count);
    public int MaxPhotos => maxPhotos;

    // 실제로 영속적으로 들고 있어야 하는 건 ID 목록뿐이다.
    private readonly List<string> capturedPhotoIds = new List<string>();
    // ID로 실제 데이터(이미지/제목/설명)를 찾기 위한 런타임 조회용 사전. 사진을 찍을 때마다 채워진다.
    private readonly Dictionary<string, PhotoItemData> itemLookup = new Dictionary<string, PhotoItemData>();
    private readonly List<GameObject> spawnedThumbnails = new List<GameObject>();
    private bool isEnlargedViewOpen;

    // 같은 Player 오브젝트의 InteractionController. 살펴보기 중인지 확인하는 용도.
    private InteractionController interactionController;

    private void Awake()
    {
        interactionController = GetComponent<InteractionController>();
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (enlargedViewPanel != null) enlargedViewPanel.SetActive(false);
        UpdateRemainingCountText();
    }

    private void OnEnable()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.action.performed += OnToggleInventory;
            toggleInventoryAction.action.Enable();
        }
        if (closePhotoAction != null)
        {
            closePhotoAction.action.performed += OnClosePhoto;
            closePhotoAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleInventoryAction != null) toggleInventoryAction.action.performed -= OnToggleInventory;
        if (closePhotoAction != null) closePhotoAction.action.performed -= OnClosePhoto;
    }

    private void OnToggleInventory(InputAction.CallbackContext context)
    {
        // 촬영 모드 중엔 인벤토리를 열지 못하게 막는다 (뷰파인더 화면 위에 인벤토리가 겹치면 혼란스러움).
        if (photoCaptureSystem != null && photoCaptureSystem.IsBusy) return;
        // 단서 살펴보기 중에도 열지 못하게 막는다 (커서 잠금/시점 회전 상태가 서로 꼬이는 것을 방지).
        if (interactionController != null && interactionController.IsModalActive) return;

        if (IsOpen) CloseInventory();
        else OpenInventory();
    }

    private void OnClosePhoto(InputAction.CallbackContext context)
    {
        if (isEnlargedViewOpen) CloseEnlargedView();
        else if (IsOpen) CloseInventory();
    }

    private void OpenInventory()
    {
        IsOpen = true;
        if (inventoryPanel != null) inventoryPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (playerMovement != null) playerMovement.LookEnabled = false;
    }

    private void CloseInventory()
    {
        IsOpen = false;
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        CloseEnlargedView();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (playerMovement != null) playerMovement.LookEnabled = true;
    }

    private void OpenEnlargedView(PhotoItemData item)
    {
        isEnlargedViewOpen = true;
        if (enlargedViewPanel != null) enlargedViewPanel.SetActive(true);
        if (enlargedImage != null) enlargedImage.sprite = item.image;
        if (enlargedTitleText != null) enlargedTitleText.text = item.title;
        if (enlargedDescriptionText != null) enlargedDescriptionText.text = item.description;
    }

    private void CloseEnlargedView()
    {
        isEnlargedViewOpen = false;
        if (enlargedViewPanel != null) enlargedViewPanel.SetActive(false);
    }

    // PhotoCaptureSystem이 촬영에 성공했을 때 호출한다.
    public void AddPhoto(PhotoItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.photoId)) return;
        if (!IsUnlimited && capturedPhotoIds.Count >= maxPhotos) return;
        if (capturedPhotoIds.Contains(item.photoId)) return; // 같은 ID가 중복으로 들어오는 것을 막는 안전장치

        capturedPhotoIds.Add(item.photoId);
        itemLookup[item.photoId] = item;

        SpawnThumbnail(item);
        UpdateRemainingCountText();
    }

    private void SpawnThumbnail(PhotoItemData item)
    {
        if (thumbnailButtonPrefab == null || thumbnailParent == null) return;

        GameObject thumbnailObject = Instantiate(thumbnailButtonPrefab, thumbnailParent);
        spawnedThumbnails.Add(thumbnailObject);

        Image thumbnailImage = thumbnailObject.GetComponent<Image>();
        if (thumbnailImage != null) thumbnailImage.sprite = item.image;

        Button button = thumbnailObject.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OpenEnlargedView(item));
        }
    }

    private void UpdateRemainingCountText()
    {
        if (remainingCountText == null) return;

        remainingCountText.text = IsUnlimited
            ? $"{capturedPhotoIds.Count}장 촬영함"
            : $"{RemainingSlots} / {maxPhotos}";
    }
}
