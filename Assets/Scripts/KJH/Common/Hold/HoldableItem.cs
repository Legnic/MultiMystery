using System.Collections;
using UnityEngine;

// 공용 들기 시스템: 손에 들 수 있는 물건 (명세서의 IHoldable 역할). GIM-002의 인형·배경카드·놀이책·열쇠가 이걸 상속한다.
// GIM-003 재료 운반 등 다른 기믹에서도 그대로 쓸 수 있게 GIM-002 코드에 의존하지 않는다.
//
// 동작:
//  - 집기: 원래 자리에 있을 때 바라보고 E → 손에 듦 (손이 비어 있을 때만, 한 번에 1개)
//  - 내려놓기: 들고 있는 동안 "원래 자리"를 바라보고 E → 원래 자리로 (바닥 등 아무 곳에나 두지 않음 → 분실 방지)
//  - 꽂기/빼기: 꽂는 자리(IHoldableSocket)가 PlaceAt()/TryTake로 처리한다. 꽂혀 있는 동안의 E 입력은 자리에게 넘긴다.
//  - ReturnHome(): returnSeconds초 동안 부드럽게 원래 자리로 돌아간다.
//
// LGH 플레이어(PlayerInteractor)는 IWorldInteractable을 구현한 물체에 E를 전달하므로, 이 클래스도 그것을 구현한다.
// 사용법: 콜라이더가 있는 물건의 최상위 오브젝트에 붙인다. (콜라이더가 없으면 레이캐스트에 안 잡힌다)
public class HoldableItem : MonoBehaviour, IWorldInteractable
{
    public enum Where { Home, Held, Placed, Moving }

    [Header("이름·들기")]
    [Tooltip("안내 문구에 쓸 이름 (예: 망원경 부엉이)")]
    [SerializeField] protected string displayName = "물건";

    [Tooltip("손에 들었을 때 카메라 기준 위치 (오른쪽, 위, 앞)")]
    [SerializeField] Vector3 holdOffset = new Vector3(0.28f, -0.25f, 0.6f);

    [Tooltip("손에 들었을 때 카메라 기준 회전(도)")]
    [SerializeField] Vector3 holdEuler = Vector3.zero;

    [Tooltip("원래 자리로 돌아가는 데 걸리는 시간(초)")]
    [SerializeField] protected float returnSeconds = 0.5f;

    [Tooltip("자리에 꽂힐 때 움직이는 시간(초)")]
    [SerializeField] protected float placeSeconds = 0.15f;

    public virtual string DisplayName { get { return displayName; } }
    public Vector3 HoldOffset { get { return holdOffset; } }
    public Vector3 HoldEuler { get { return holdEuler; } }
    public Where Location { get; private set; }
    public IHoldableSocket Socket { get; private set; }

    // 이 컴퓨터의 손에 들려 있는가
    public bool IsHeldLocally { get { return Location == Where.Held && PlayerHand.LocalHeld == this; } }
    public bool IsAtHome { get { return Location == Where.Home; } }

    Transform homeParent;
    Vector3 homeLocalPos, homeLocalScale;
    Quaternion homeLocalRot;
    Collider[] colliders;
    HoldableHomeSpot homeSpot;
    Coroutine moving;

    protected virtual void Awake()
    {
        // 시작 위치를 "원래 자리"로 기억한다
        homeParent = transform.parent;
        homeLocalPos = transform.localPosition;
        homeLocalRot = transform.localRotation;
        homeLocalScale = transform.localScale;
        colliders = GetComponentsInChildren<Collider>(true);
        Location = Where.Home;
        homeSpot = HoldableHomeSpot.Create(this, homeParent, homeLocalPos, homeLocalRot, LocalBounds());
    }

    protected virtual void OnDestroy()
    {
        if (homeSpot != null) Destroy(homeSpot.gameObject);
    }

    // 물건의 크기(자기 좌표 기준). 원래 자리 표시용 콜라이더 크기로 쓴다.
    Bounds LocalBounds()
    {
        var b = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        bool first = true;
        foreach (var c in colliders)
        {
            if (c == null) continue;
            Bounds wb = c.bounds;
            Vector3 lc = transform.InverseTransformPoint(wb.center);
            Vector3 ls = transform.InverseTransformVector(wb.size);
            var lb = new Bounds(lc, new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
            if (first) { b = lb; first = false; } else b.Encapsulate(lb);
        }
        return b;
    }

    // ---------------- 상속해서 바꿀 수 있는 규칙 ----------------

    // 지금 집을 수 있는가 (예: 열쇠는 GIM-001이 풀리기 전엔 못 집음)
    protected virtual bool CanPickUp { get { return true; } }

    // ---------------- IWorldInteractable ----------------

    public virtual bool CanInteract
    {
        get
        {
            if (!isActiveAndEnabled) return false;
            if (Location == Where.Placed && Socket != null) return Socket.CanUsePlaced(this);
            if (Location == Where.Held) return false;
            return CanPickUp;
        }
    }

    public virtual string InteractPrompt
    {
        get
        {
            if (Location == Where.Placed && Socket != null) return Socket.PromptForPlaced(this);
            if (PlayerHand.LocalHeld != null) return DisplayName + "  (손에 다른 물건을 들고 있다)";
            return "[E] " + DisplayName + " 집기";
        }
    }

    public virtual void Interact(GameObject interactor)
    {
        if (Location == Where.Placed && Socket != null) { Socket.UsePlaced(this); return; }
        if (Location == Where.Held || !CanPickUp) return;
        PlayerHand.Local.TryTake(this);
    }

    // ---------------- 위치 바꾸기 ----------------

    // PlayerHand가 호출: 손에 들렸다
    public void NotifyTaken()
    {
        StopMoving();
        Socket = null;
        transform.SetParent(null, true);
        Location = Where.Held;
        SetCollidersEnabled(false);            // 손에 든 물건이 시선을 가로막지 않게
        if (homeSpot != null) homeSpot.SetActive(true);   // 원래 자리를 바라보면 "제자리에 두기"가 뜨도록
        OnTaken();
    }

    // PlayerHand가 호출: 손에서 놓였다 (이어서 ReturnHome/PlaceAt/Consume 중 하나가 불린다)
    public void NotifyReleased()
    {
        if (homeSpot != null) homeSpot.SetActive(false);
        SetCollidersEnabled(true);
        if (Location == Where.Held) Location = Where.Moving;
        OnReleased();
    }

    // 원래 자리로 부드럽게 돌아간다. seconds를 생략하면 returnSeconds 사용.
    public void ReturnHome(float seconds = -1f)
    {
        if (Location == Where.Held) { PlayerHand.Local.Release(); }
        Socket = null;
        if (Location == Where.Home && moving == null) return;
        transform.SetParent(homeParent, true);
        MoveTo(homeLocalPos, homeLocalRot, homeLocalScale, seconds < 0f ? returnSeconds : seconds, Where.Home);
    }

    // anchor 위치에 꽂는다. socket은 꽂힌 동안 E 입력을 대신 처리할 자리.
    public void PlaceAt(Transform anchor, IHoldableSocket socket, float seconds = -1f)
    {
        if (Location == Where.Held) PlayerHand.Local.Release();
        Socket = socket;
        transform.SetParent(anchor, true);
        Vector3 scaleInAnchor = transform.localScale;   // 월드 크기 유지
        MoveTo(Vector3.zero, Quaternion.identity, scaleInAnchor, seconds < 0f ? placeSeconds : seconds, Where.Placed);
    }

    // 지금 anchor에 꽂혀 있는가 (이동 중 포함)
    public bool IsPlacedOn(Transform anchor) { return transform.parent == anchor && (Location == Where.Placed || moving != null); }

    // 사용해서 사라짐 (예: 열쇠를 꽂아 사용)
    public void Consume()
    {
        if (PlayerHand.LocalHeld == this) PlayerHand.Local.Release();
        StopMoving();
        gameObject.SetActive(false);
    }

    void MoveTo(Vector3 localPos, Quaternion localRot, Vector3 localScale, float seconds, Where arrive)
    {
        StopMoving();
        if (seconds <= 0f || !isActiveAndEnabled)
        {
            transform.localPosition = localPos;
            transform.localRotation = localRot;
            transform.localScale = localScale;
            Location = arrive;
            return;
        }
        Location = arrive == Where.Placed ? Where.Placed : Where.Moving;
        moving = StartCoroutine(Move(localPos, localRot, localScale, seconds, arrive));
    }

    IEnumerator Move(Vector3 localPos, Quaternion localRot, Vector3 localScale, float seconds, Where arrive)
    {
        Vector3 p0 = transform.localPosition, s0 = transform.localScale;
        Quaternion r0 = transform.localRotation;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            transform.localPosition = Vector3.Lerp(p0, localPos, k);
            transform.localRotation = Quaternion.Slerp(r0, localRot, k);
            transform.localScale = Vector3.Lerp(s0, localScale, k);
            yield return null;
        }
        transform.localPosition = localPos;
        transform.localRotation = localRot;
        transform.localScale = localScale;
        Location = arrive;
        moving = null;
    }

    void StopMoving()
    {
        if (moving != null) { StopCoroutine(moving); moving = null; }
    }

    void SetCollidersEnabled(bool on)
    {
        foreach (var c in colliders) if (c != null) c.enabled = on;
    }

    // 상속한 클래스가 필요하면 덮어쓴다 (예: 놀이책을 놓으면 책 UI 닫기)
    protected virtual void OnTaken() { }
    protected virtual void OnReleased() { }
}
