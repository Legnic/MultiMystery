using UnityEngine;

// 승강기 케이지가 보이는 창구. 케이지가 이 층에 멈춰 있을 때만 상호작용된다.
//  - 싣기 창(canLoad, 보통 2층): E를 누를 때마다 가진 양초를 하나씩 케이지에 싣는다.
//  - 꺼내기 창(보통 1층): 케이지에 실려 온 양초를 전부 꺼내 인벤토리에 넣는다.
// 실제 처리는 DumbwaiterNetworkSync가 한다 (2인 플레이에서는 호스트가 결정하고 모두에게 반영).
// 콜라이더(트리거 아님)가 창 구멍을 덮고 있어서 바라보기 레이가 맞는다.
public class DumbwaiterWindow : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private Dumbwaiter dumbwaiter;
    [Tooltip("이 창이 있는 층 (0=1층, 1=2층).")]
    [SerializeField] private int floor;
    [Tooltip("켜면 싣기 창, 끄면 꺼내기 창.")]
    [SerializeField] private bool canLoad;

    public bool CanInteract
    {
        get
        {
            if (dumbwaiter == null || !dumbwaiter.IsIdleAt(floor)) return false;
            return canLoad ? !dumbwaiter.IsFull : dumbwaiter.CargoCount > 0;
        }
    }

    public string InteractPrompt
    {
        get
        {
            bool lump = dumbwaiter != null && dumbwaiter.AcceptedCargo == Dumbwaiter.CargoKind.PotionLump;
            string what = lump ? "덩이" : "양초";
            return canLoad ? "[E] 도르래에 " + what + (lump ? " 싣기" : " 담기") : "[E] 도르래에서 " + what + " 꺼내기";
        }
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        var sync = KJH.DumbwaiterNetworkSync.Instance;
        if (sync == null)
        {
            Debug.LogWarning("[DumbwaiterWindow] 씬에 DumbwaiterNetworkSync가 없습니다 (DumbwaiterSync 프리팹을 씬에 배치해주세요).", this);
            return;
        }

        var inventory = interactor != null ? interactor.GetComponent<LGH.PlayerInventory>() : null;
        string message = canLoad ? sync.RequestLoad(dumbwaiter, floor, inventory) : sync.RequestTake(dumbwaiter, floor, inventory);
        if (!string.IsNullOrEmpty(message) && LGH.PlayerInteractor.Active != null) LGH.PlayerInteractor.Active.ShowMessage(message);
    }
}
