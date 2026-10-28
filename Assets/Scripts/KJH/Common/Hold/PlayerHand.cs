using UnityEngine;

// 공용 들기 시스템의 "손". 이 컴퓨터의 플레이어가 지금 들고 있는 물건 1개를 기억한다.
//
// 설계 이유:
//  - 손에 든 물건은 네트워크로 동기화하지 않는다(기획: 상대가 볼 수 없음, 재접속 시 원래 자리로).
//    그래서 플레이어 프리팹(NetworkPlayer_LGH)에 붙이지 않고, 처음 필요할 때 씬에 하나 자동으로 만들어지는 로컬 전용 오브젝트로 둔다.
//    → LGH 플레이어 프리팹을 고치지 않아도 되고, 상대 플레이어 복사본에는 손이 생기지 않는다.
//  - 한 번에 1개만 든다. 이미 들고 있으면 TryTake가 거절하고 "손이 가득 찼다"를 띄운다.
//  - 들고 있는 물건은 카메라 자식으로 붙이지 않고 매 프레임 카메라 앞 위치로 옮긴다.
//    (카메라 자식으로 붙이면 플레이어가 사라질 때 물건까지 같이 파괴되기 때문)
public class PlayerHand : MonoBehaviour
{
    static PlayerHand local;

    // 이 컴퓨터의 손. 없으면 자동으로 만든다.
    public static PlayerHand Local
    {
        get
        {
            if (local == null)
            {
                var go = new GameObject("PlayerHand (Local)");
                local = go.AddComponent<PlayerHand>();
            }
            return local;
        }
    }

    // 손을 만들지 않고 "지금 이 물건을 들고 있나"만 확인하고 싶을 때 사용
    public static HoldableItem LocalHeld { get { return local != null ? local.Held : null; } }

    // 이 컴퓨터에서 방금 집었다/놓았다 — HoldableNetworkSync가 구독해서 서버에 알리는 용도.
    // (HoldableItem/PlayerHand는 네트워크를 몰라도 되게, 이벤트만 던지고 누가 듣는지는 신경 쓰지 않는다)
    public static event System.Action<HoldableItem> LocalTaken;
    public static event System.Action<HoldableItem> LocalReleased;

    [Tooltip("이미 무언가를 들고 있는데 또 집으려 할 때 띄울 문구")]
    public string handFullMessage = "손이 가득 찼다";

    public HoldableItem Held { get; private set; }
    public bool IsEmpty { get { return Held == null; } }

    void Awake()
    {
        if (local != null && local != this) { Destroy(gameObject); return; }
        local = this;
    }

    void OnDestroy()
    {
        if (local == this) local = null;
    }

    // 물건 집기. 손이 비어 있을 때만 성공한다.
    public bool TryTake(HoldableItem item)
    {
        if (item == null) return false;
        if (Held != null)
        {
            Say(handFullMessage);
            return false;
        }
        Held = item;
        item.NotifyTaken();
        LocalTaken?.Invoke(item);
        return true;
    }

    // 손에서 놓기(어디에 둘지는 호출한 쪽이 정한다: 꽂기, 원래 자리, 사용해서 사라짐 등). 들고 있던 물건을 돌려준다.
    public HoldableItem Release()
    {
        var item = Held;
        Held = null;
        if (item != null) { item.NotifyReleased(); LocalReleased?.Invoke(item); }
        return item;
    }

    // 들고 있던 물건을 원래 자리로 돌려보낸다 (내려놓기, 접속 끊김 등)
    public void ReturnHeldHome()
    {
        var item = Release();
        if (item != null) item.ReturnHome();
    }

    void LateUpdate()
    {
        if (Held == null) return;

        // 들고 있던 물건이 꺼지거나 파괴되면(예: 열쇠를 써서 사라짐) 손을 비운다
        // (Release로 비워야 HoldableNetworkSync가 "놓았다"를 알 수 있다 — 그냥 null로 두면 서버 목록에 "들고 있음"이 계속 남는다)
        if (!Held.isActiveAndEnabled) { Release(); return; }

        // 내 플레이어(카메라)가 사라졌으면(접속 끊김 등) 들고 있던 물건은 원래 자리로
        Transform cam = ViewCamera();
        if (cam == null) { ReturnHeldHome(); return; }

        // 카메라 기준 오른쪽 아래 앞에 물건을 띄운다
        Held.transform.SetPositionAndRotation(cam.TransformPoint(Held.HoldOffset), cam.rotation * Quaternion.Euler(Held.HoldEuler));
    }

    // 손에 든 물건 이름을 오른쪽 아래에 (메뉴가 떠 있을 때는 숨김)
    void OnGUI()
    {
        if (Held == null || Cursor.lockState != CursorLockMode.Locked) return;
        HeldItemHud.Draw(Held.HudName);
    }

    // 이 컴퓨터 플레이어의 시점 카메라 (LGH PlayerInteractor가 쓰는 것과 같은 카메라)
    public static Transform ViewCamera()
    {
        var pi = LGH.PlayerInteractor.Active;
        return pi != null ? pi.viewCamera : null;
    }

    // 화면 가운데 아래에 짧은 문구 띄우기 (LGH 안내 문구 UI 재사용)
    public static void Say(string message, float seconds = 2f)
    {
        if (string.IsNullOrEmpty(message)) return;
        var pi = LGH.PlayerInteractor.Active;
        if (pi != null) pi.ShowMessage(message, seconds);
        else Debug.Log("[PlayerHand] " + message);
    }
}
