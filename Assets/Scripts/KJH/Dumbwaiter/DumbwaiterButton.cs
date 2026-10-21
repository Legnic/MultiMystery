using UnityEngine;

// 승강기 호출/전송 버튼. 케이지가 이 층에 있으면 반대 층으로 보내고, 다른 층에 있으면 이 층으로 부른다.
// 케이지가 움직이는 동안에는 눌러도 반응하지 않는다. 실제 처리는 DumbwaiterNetworkSync가 한다.
public class DumbwaiterButton : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private Dumbwaiter dumbwaiter;
    [Tooltip("이 버튼이 있는 층 (0=1층, 1=2층).")]
    [SerializeField] private int floor;

    public bool CanInteract => dumbwaiter != null && !dumbwaiter.IsMoving;

    public string InteractPrompt
    {
        get
        {
            if (dumbwaiter == null) return string.Empty;
            if (dumbwaiter.CurrentFloor != floor) return "[E] 도르래 부르기";
            return floor > 0 ? "[E] 도르래 내려보내기" : "[E] 도르래 올려보내기";
        }
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        var sync = KJH.DumbwaiterNetworkSync.Instance;
        if (sync == null)
        {
            Debug.LogWarning("[DumbwaiterButton] 씬에 DumbwaiterNetworkSync가 없습니다 (DumbwaiterSync 프리팹을 씬에 배치해주세요).", this);
            return;
        }

        string message = sync.RequestMove(dumbwaiter, floor);
        if (!string.IsNullOrEmpty(message) && LGH.PlayerInteractor.Active != null) LGH.PlayerInteractor.Active.ShowMessage(message);
    }
}
