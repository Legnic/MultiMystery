using UnityEngine;

// 씬에 놓인 열쇠 오브젝트. 바라보고 E를 누르면 상호작용한 플레이어의 인벤토리(PlayerInventory)에 열쇠가 들어가고,
// "[열쇠 이름]을(를) 얻었다" 문구가 잠깐 뜬 뒤 오브젝트는 사라진다(비활성화).
// 사용법: 열쇠 오브젝트(콜라이더가 있는 것)의 최상위에 붙이고 keyData를 연결한다. (KeyPickup.prefab 사용 권장)
public class KeyPickup : MonoBehaviour, IInteractable
{
    [Header("열쇠")]
    [Tooltip("주웠을 때 인벤토리에 들어갈 열쇠 데이터.")]
    [SerializeField] private KeyData keyData;

    [Tooltip("안내 문구에 표시할 동작 이름. 앞에 [E]가 자동으로 붙는다.")]
    [SerializeField] private string promptText = "줍기";

    [Header("연출 (비어 있어도 동작)")]
    [Tooltip("주울 때 재생할 작은 소리 (열쇠 짤랑 등).")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("획득 문구를 띄울 UI. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private InteractionMessageUI messageUI;

    public KeyData KeyData => keyData;

    private void Awake()
    {
        // 모든 열쇠·금고가 같은 문구 UI 하나를 같이 쓰므로, 비워두면 씬에서 한 번만 찾아 캐싱한다.
        if (messageUI == null) messageUI = FindAnyObjectByType<InteractionMessageUI>(FindObjectsInactive.Include);
    }

    // ── IInteractable ──────────────────────────────────────
    // 데이터가 연결된 열쇠만 주울 수 있다 (실수로 비워둔 열쇠에 프롬프트가 뜨지 않게).
    public bool CanInteract => keyData != null && isActiveAndEnabled;
    public string InteractPrompt => $"[E] {promptText}";

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        // 상호작용한 "그 플레이어"의 인벤토리에 넣는다 (2인 협동에서 전역 인벤토리를 쓰지 않기 위함).
        // 상호작용할 때만 한 번 호출되고 플레이어가 바뀔 수 있어서 Awake 캐싱 대신 여기서 찾는다.
        PlayerInventory inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
        if (inventory == null)
        {
            Debug.LogWarning("[KeyPickup] 상호작용한 플레이어에게 PlayerInventory가 없어 열쇠를 넣을 수 없습니다. Player 오브젝트에 PlayerInventory를 붙여주세요.", this);
            return;
        }

        inventory.AddKey(keyData);

        string keyName = keyData.DisplayNameOrFallback;
        if (messageUI != null) messageUI.Show($"{keyName}{ObjectParticle(keyName)} 얻었다");

        // 오브젝트를 끄기 전에 소리를 재생한다 (PlayClipAtPoint는 별도 임시 오브젝트에서 재생되어 꺼져도 끊기지 않음).
        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        // 파괴하지 않고 비활성화만 한다: 나중에 저장/불러오기나 네트워크 동기화에서 "주운 상태"를 되돌리기 쉽게 하기 위함.
        gameObject.SetActive(false);
    }

    // 이름의 마지막 글자에 받침이 있으면 "을", 없으면 "를"을 붙인다 (예: 서재 열쇠를 / 금고 손잡이를 / 낡은 반지를).
    // 한글이 아닌 글자로 끝나면 판단할 수 없으므로 "을(를)"로 둔다.
    private static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "을(를)";
        char last = word[word.Length - 1];
        // 한글 완성형 음절 범위: '가'(0xAC00) ~ '힣'(0xD7A3). 한 음절마다 받침 경우가 28가지라 28로 나눈 나머지가 0이면 받침 없음.
        if (last < 0xAC00 || last > 0xD7A3) return "을(를)";
        return (last - 0xAC00) % 28 == 0 ? "를" : "을";
    }
}
