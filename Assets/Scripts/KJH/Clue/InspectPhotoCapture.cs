using UnityEngine;

// InspectableClue(눈앞에 들고 살펴보는 단서)에 함께 붙여서, "들고 보는 상태에서 사진 찍기"를 추가하는 컴포넌트.
// 흐름: 살펴보기 중 → [E] 사진 찍기 → 뷰파인더(카메라는 그대로, 단서는 고정) → [E] 촬영 / [Esc] 취소 → 다시 살펴보기
// 조작키를 늘리지 않기 위해, 이 컴포넌트가 붙은 단서를 살펴보는 동안에는 E = 사진 찍기, Esc = 내려놓기로 바뀐다.
// 사진 촬영은 기존 규칙대로 PastPhoto 능력(플레이어 B)만 할 수 있다. 능력이 없는 플레이어에게는 안내도 뜨지 않고,
// E도 원래대로 "내려놓기"로 동작한다. 한 번 찍으면 다시 찍을 수 없다 (PhotoSpot과 같은 규칙).
// 사용법: InspectableClue가 붙은 단서 루트에 같이 붙이고 Photo Item만 연결한다.
[RequireComponent(typeof(InspectableClue))]
public class InspectPhotoCapture : MonoBehaviour, IPhotoSubject
{
    [Header("사진")]
    [Tooltip("이 단서를 들고 촬영했을 때 지급할 사진 아이템 데이터.")]
    [SerializeField] private PhotoItemData photoItem;

    [Header("UI")]
    [Tooltip("뷰파인더에 들어가 있는 동안 보여줄 안내 문구.")]
    [SerializeField] private string capturePrompt = "[E] 촬영  [Esc] 취소";

    // 이미 촬영을 완료했는지. 한 번 true가 되면 다시는 찍을 수 없다.
    [SerializeField] private bool captured = false;

    private InspectableClue clue;
    private InteractionController controller;

    // 뷰파인더에 들어가 있는 동안 true. InspectableClue가 이 값을 보고 마우스 회전을 멈춘다 (촬영 순간 단서 고정).
    public bool IsCapturing { get; private set; }

    private void Awake()
    {
        clue = GetComponent<InspectableClue>();
    }

    // 이 플레이어가 지금 이 단서를 찍을 수 있는지. 살펴보기 안내 문구를 고를 때도 쓴다.
    public bool CanCapture(GameObject player)
    {
        return !captured && !IsCapturing && photoItem != null
            && PlayerAbilities.Has(player, PlayerAbility.PastPhoto)
            && player.GetComponent<PhotoCaptureSystem>() != null;
    }

    // 살펴보기 중 E를 눌렀을 때 InspectableClue가 호출한다. 촬영 모드에 들어갔으면 true (false면 원래대로 내려놓기).
    public bool TryBeginCapture(GameObject player)
    {
        if (player == null || !CanCapture(player)) return false;

        PhotoCaptureSystem captureSystem = player.GetComponent<PhotoCaptureSystem>();
        if (!captureSystem.BeginCapture(this)) return false;

        // 촬영 모드 중의 E/Esc는 InteractionController가 PhotoCaptureSystem으로 바로 보낸다 (촬영 / 취소).
        IsCapturing = true;
        controller = player.GetComponent<InteractionController>();
        if (controller != null && controller.PromptUI != null) controller.PromptUI.Show(capturePrompt);
        return true;
    }

    // ── IPhotoSubject ──────────────────────────────────────
    public Transform CameraAnchor => null;               // 카메라를 옮기지 않고 들고 보는 시점 그대로 찍는다
    public PhotoItemData PhotoItem => photoItem;
    public bool RestorePlayerControlOnExit => false;     // 촬영 후에도 살펴보기가 계속되므로 조작은 살펴보기가 끝날 때 돌려준다

    public void OnPhotoCaptured()
    {
        captured = true;
        IsCapturing = false;
        clue.RefreshInspectPrompt(); // 이제 찍을 수 없으니 "[E] 내려놓기" 안내로 돌아간다
    }

    public void OnPhotoCancelled()
    {
        IsCapturing = false;
        clue.RefreshInspectPrompt();
    }
}
