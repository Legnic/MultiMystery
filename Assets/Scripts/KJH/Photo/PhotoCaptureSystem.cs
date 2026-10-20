using UnityEngine;

// 사진 촬영 "상태"를 관리하는 컴포넌트. 촬영 대상(IPhotoSubject: PhotoSpot, 살펴보는 단서의 InspectPhotoCapture)이
// BeginCapture를 호출하면 시작되고,
// 촬영 모드 중 E를 다시 누르면(InteractionController가 TryCapture()를 호출) 촬영을 확정해서
// 인벤토리에 사진을 넣는다. 입력은 이 컴포넌트가 직접 받지 않는다 — E/Esc 입력은
// InteractionController 한 곳에서만 받아서 "지금 상태에 따라 무슨 뜻인지"를 판단하고,
// 이 컴포넌트는 그 판단 결과(TryCapture/CancelCapture 호출)만 받아서 실행한다.
// 예전 버전과 달리 RenderTexture로 실제 렌더링을 하지 않는다 — 정해진 지점에서 정해진
// PhotoItemData를 "획득"하는 것으로 기획이 바뀌었기 때문에 카메라 두 대/레이어 분리가 필요 없다.
// 사용법: Player 오브젝트에 CameraModeController, PhotoInventory, InteractionController와
// 함께 붙인다 (전부 같은 오브젝트에 있어야 서로 참조하기 쉽다).
[RequireComponent(typeof(AudioSource))]
public class PhotoCaptureSystem : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CameraModeController cameraModeController;
    [SerializeField] private PhotoInventory photoInventory;
    [SerializeField] private FlashEffect flashEffect;
    [SerializeField] private PhotoAcquiredToast acquiredToast;

    [Header("사운드")]
    [SerializeField] private AudioClip shutterClip;

    // 지금 촬영 중인(=뷰파인더에 들어가 있는) 대상. null이면 평소 상태.
    private IPhotoSubject currentSubject;
    private AudioSource audioSource;

    // InteractionController가 "지금 촬영 모드라 E를 상호작용이 아니라 촬영으로 써야 하는지" 확인할 때 쓴다.
    public bool IsBusy => currentSubject != null;

    // 네트워크로 스폰되는 플레이어(예: FPSPlayer)는 프리팹 단계에서 씬의 Canvas UI 참조를
    // 미리 연결해둘 수 없어서(프리팹은 씬 오브젝트를 참조할 수 없음), 스폰 직후(OnNetworkSpawn 등)에
    // 코드로 찾아서 주입할 방법이 필요하다.
    public void ConfigureForOwner(FlashEffect flashEffect, PhotoAcquiredToast acquiredToast)
    {
        this.flashEffect = flashEffect;
        this.acquiredToast = acquiredToast;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // PhotoSpot.Interact() / InspectPhotoCapture가 호출한다. 촬영 모드에 들어갔으면 true.
    public bool BeginCapture(IPhotoSubject subject)
    {
        if (subject == null || IsBusy) return false; // 이미 다른 대상을 촬영 중이면 무시 (이론상 거의 발생하지 않음)
        // 이중 안전장치: 상호작용 필터를 거치지 않고 호출되더라도 PastPhoto 능력이 없으면 촬영하지 않는다.
        if (!PlayerAbilities.Has(gameObject, PlayerAbility.PastPhoto)) return false;

        currentSubject = subject;
        cameraModeController.EnterCaptureMode(subject.CameraAnchor, subject.RestorePlayerControlOnExit);
        return true;
    }

    // InteractionController가 촬영 모드 중 Esc를 누르면 호출한다 (설계 조건: 촬영 취소).
    public void CancelCapture()
    {
        if (!IsBusy) return;

        IPhotoSubject cancelled = currentSubject;
        currentSubject = null;
        cameraModeController.ExitCaptureMode();
        cancelled.OnPhotoCancelled();
    }

    // InteractionController가 촬영 모드 중 E를 누르면 호출한다 (키 단순화: 촬영 모드에서 E = 촬영 확정).
    public void TryCapture()
    {
        // 뷰파인더 전환이 끝나 카메라가 완전히 고정된 뒤에만 촬영을 받아들인다.
        // (전환 도중 눌러도 무시 — 카메라가 덜 움직인 상태에서 찍히면 어색하므로)
        if (!IsBusy || cameraModeController == null || !cameraModeController.IsLockedOnAnchor) return;

        IPhotoSubject subjectBeingCaptured = currentSubject;
        PhotoItemData item = subjectBeingCaptured.PhotoItem;

        if (flashEffect != null) flashEffect.Play();
        if (shutterClip != null) audioSource.PlayOneShot(shutterClip);

        if (item != null)
        {
            photoInventory.AddPhoto(item);
            if (acquiredToast != null) acquiredToast.Show(item);
        }
        else
        {
            Debug.LogWarning("[PhotoCaptureSystem] 촬영 대상에 PhotoItemData가 연결되어 있지 않습니다.", subjectBeingCaptured as Object);
        }

        currentSubject = null;
        cameraModeController.ExitCaptureMode();
        subjectBeingCaptured.OnPhotoCaptured();
    }
}
