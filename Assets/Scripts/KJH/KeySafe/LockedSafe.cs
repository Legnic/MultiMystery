using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 열쇠로 여는 금고. 상태는 Locked(잠김) → Opening(열리는 중) → Opened(열림) 세 가지이고, 되돌아가지 않는다.
// Locked에서 E를 누르면 상호작용한 플레이어의 인벤토리를 검사해서
//   - 요구 열쇠가 있으면: 자동으로 사용(별도 선택 UI 없음) → 열림 연출(ISafeOpenSequence) → Opened
//   - 없으면(다른 열쇠만 있어도): "알맞은 열쇠가 없는 것 같다" 문구 + 둔한 금속음 1회. 경고음·화면 흔들림은 쓰지 않는다(톤 유지).
// Opening 중에는 상호작용을 무시해서 연타해도 연출이 겹치지 않고, Opened 후에는 프롬프트를 띄우지 않는다(안의 단서가 잡히도록).
//
// 2인 협동 대비: 상태 변경을 TryOpen(검사·열쇠 소모) → ApplyOpened(실제 열림 적용) 두 단계로 나눠 두었다.
// 나중에 네트워크를 붙이면 TryOpen은 서버에서 검증하고, ApplyOpened(+연출)는 두 클라이언트 모두에서 재생하도록 나누면 된다.
// 금고가 열린 상태는 "월드 상태"(두 사람 모두에게 열림)이고, 실패 문구는 상호작용한 사람에게만 뜨는 로컬 연출이다.
//
// 사용법: 금고 루트에 붙이고 requiredKey를 연결한다. 같은 오브젝트에 ISafeOpenSequence(예: SimpleDoorOpenSequence)를 붙이면
//        그 연출을 재생하고, 없으면 연출 없이 즉시 열린다.
public class LockedSafe : MonoBehaviour, IInteractable
{
    public enum SafeState { Locked, Opening, Opened }

    [Header("열쇠")]
    [Tooltip("이 금고를 여는 열쇠. 플레이어 인벤토리에 같은 keyId의 열쇠가 있어야 열린다.")]
    [SerializeField] private KeyData requiredKey;

    [Header("문구")]
    [Tooltip("안내 문구에 표시할 동작 이름. 앞에 [E]가 자동으로 붙는다.")]
    [SerializeField] private string promptText = "열어보기";

    [Tooltip("맞는 열쇠가 없을 때 띄울 혼잣말. 금고마다 다르게 쓸 수 있도록 코드가 아니라 여기서 정한다.")]
    [SerializeField] private string lockedMessage = "알맞은 열쇠가 없는 것 같다";

    [Tooltip("잠김 문구가 완전히 보인 채로 유지되는 시간 (초). 이후 페이드아웃된다.")]
    [SerializeField] private float lockedMessageSeconds = 2f;

    [Header("사운드 (비어 있어도 동작)")]
    [Tooltip("잠긴 손잡이를 당기는 작고 둔한 금속음. 열쇠가 없을 때 한 번 재생한다.")]
    [SerializeField] private AudioClip lockedRattleSound;

    [Header("연출")]
    [Tooltip("열림 연출이 재생되는 동안 플레이어 이동·시점을 잠글지. 지금 임시 연출(0.8초)은 짧아서 꺼둠 — 열쇠 꽂기 모션처럼 긴 연출을 붙이면 켠다.")]
    [SerializeField] private bool lockPlayerDuringSequence = false;

    [Tooltip("상호작용 감지용 루트 콜라이더. 비워두면 같은 오브젝트의 콜라이더를 쓴다.")]
    [SerializeField] private Collider interactionCollider;

    [Tooltip("열린 뒤 루트 콜라이더를 끌지. 루트 콜라이더가 금고 전체(안쪽 단서 포함)를 감싸고 있어서, 켜두면 조준 레이가 단서에 닿지 못한다.")]
    [SerializeField] private bool disableColliderWhenOpened = true;

    [Header("참조 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private InteractionMessageUI messageUI;

    [Header("추가 이벤트 (조명·소리 등 그 밖의 연출용)")]
    [Tooltip("금고가 완전히 열렸을 때 호출된다.")]
    [SerializeField] private UnityEvent onOpenedEvent;

    // 금고가 완전히 열렸을 때 코드에서 구독하는 이벤트 (SafeClue가 이걸 듣고 단서 콜라이더를 켠다).
    public event Action<LockedSafe> OnOpened;

    public SafeState State { get; private set; } = SafeState.Locked;
    public bool IsOpened => State == SafeState.Opened;
    public KeyData RequiredKey => requiredKey;

    // 같은 오브젝트에 붙은 열림 연출. 없으면 null → 즉시 열림.
    private ISafeOpenSequence openSequence;

    private void Awake()
    {
        // GetComponent는 Awake에서 한 번만 해 둔다 (상호작용할 때마다 찾지 않도록).
        openSequence = GetComponent<ISafeOpenSequence>();
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
        if (messageUI == null) messageUI = FindAnyObjectByType<InteractionMessageUI>(FindObjectsInactive.Include);
    }

    // ── IInteractable ──────────────────────────────────────
    // 잠겨 있을 때만 상호작용 대상이 된다. Opening 중엔 연타 무시, Opened 후엔 프롬프트를 안 띄워 단서가 우선 잡히게 한다.
    public bool CanInteract => State == SafeState.Locked;
    public string InteractPrompt => $"[E] {promptText}";

    public void Interact(GameObject interactor)
    {
        // 상호작용한 "그 플레이어"의 인벤토리만 검사한다 (전역 인벤토리 없음).
        PlayerInventory inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
        TryOpen(inventory, interactor);
    }

    // ── 1단계: 검사 + 열쇠 사용 ────────────────────────────
    // 열쇠가 맞으면 열쇠를 (설정에 따라) 소모하고 열림 연출을 시작한 뒤 true. 아니면 실패 피드백을 주고 false.
    // 네트워크가 붙으면 이 함수가 "서버 검증" 자리가 된다.
    public bool TryOpen(PlayerInventory inventory, GameObject interactor = null)
    {
        if (State != SafeState.Locked) return false; // 열리는 중/이미 열림: 연타나 중복 요청은 조용히 무시

        if (requiredKey == null)
        {
            Debug.LogWarning("[LockedSafe] requiredKey가 비어 있어 이 금고는 열 수 없습니다.", this);
            return false;
        }

        if (inventory == null || !inventory.HasKey(requiredKey))
        {
            // 열쇠가 아예 없든, 다른 열쇠만 있든 같은 문구를 쓴다 (어떤 열쇠가 맞는지 힌트를 주지 않기 위함).
            ShowLockedFeedback();
            return false;
        }

        // 열쇠를 쓰는 순간 인벤토리에서 뺀다 (연출 도중에 같은 열쇠로 다른 걸 여는 일이 없게).
        if (requiredKey.consumeOnUse) inventory.RemoveKey(requiredKey);

        StartCoroutine(OpenRoutine(requiredKey, interactor));
        return true;
    }

    // 연출 재생 → 끝나면 ApplyOpened.
    private IEnumerator OpenRoutine(KeyData key, GameObject interactor)
    {
        // 상태를 가장 먼저 Opening으로 바꿔서, 이 프레임 이후의 상호작용(연타)은 CanInteract에서 바로 걸러진다.
        State = SafeState.Opening;

        PlayerMovement movement = lockPlayerDuringSequence && interactor != null ? interactor.GetComponent<PlayerMovement>() : null;
        SetPlayerLocked(movement, true);

        // 연출 컴포넌트를 실행 중에 지웠다면 C# 참조는 남아 있어도 Unity 오브젝트는 파괴된 상태다.
        // 그런 경우도 "연출 없음"으로 보고 즉시 연다 (훅 분리 확인 시나리오 대비).
        if (openSequence is UnityEngine.Object sequenceObject && sequenceObject == null) openSequence = null;
        if (openSequence != null) yield return openSequence.Play(key);

        SetPlayerLocked(movement, false);
        ApplyOpened();
    }

    // ── 2단계: 열림 상태 적용 ──────────────────────────────
    // 실제로 "열린 상태"를 적용하는 유일한 함수. 네트워크가 붙으면 양쪽 클라이언트가 이걸 호출해서 같은 월드 상태를 맞춘다.
    public void ApplyOpened()
    {
        if (State == SafeState.Opened) return; // 두 번 적용돼도 이벤트가 중복 발생하지 않게

        State = SafeState.Opened;

        // 루트 콜라이더가 안쪽 단서를 가리지 않게 끈다 (본체·문짝의 물리 충돌은 각자 콜라이더가 담당).
        if (disableColliderWhenOpened && interactionCollider != null) interactionCollider.enabled = false;

        OnOpened?.Invoke(this);
        onOpenedEvent?.Invoke();
    }

    // 맞는 열쇠가 없을 때: 문구 + 작은 금속음. 문구가 떠 있는 동안 다시 와도 InteractionMessageUI가 타이머만 리셋한다.
    private void ShowLockedFeedback()
    {
        if (messageUI != null) messageUI.Show(lockedMessage, lockedMessageSeconds);
        else Debug.LogWarning("[LockedSafe] 씬에 InteractionMessageUI가 없어 잠김 문구를 띄울 수 없습니다.", this);

        if (lockedRattleSound != null) AudioSource.PlayClipAtPoint(lockedRattleSound, transform.position);
    }

    // 연출 중 플레이어 이동·시점 잠금 (ViewableClue, CameraModeController와 같은 방식).
    private static void SetPlayerLocked(PlayerMovement movement, bool locked)
    {
        if (movement == null) return;
        movement.SpeedMultiplier = locked ? 0f : 1f;
        movement.LookEnabled = !locked;
    }
}
