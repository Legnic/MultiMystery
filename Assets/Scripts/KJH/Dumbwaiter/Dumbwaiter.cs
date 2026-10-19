using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 1층·2층을 오가는 소형 승강기(도르래). 케이지(cabin)가 층 사이를 오르내리고,
// 2층에서 실은 양초가 1층에서 꺼내진다 (반대로 1층에서 불러올리는 것도 가능).
// 화물은 양초마다 "남은 연소 시간(초)" 하나로 표현한다 (LGH.PlayerInventory/CandleNetworkSync와 같은 표현).
// 이 컴포넌트는 로컬 상태·연출만 가진다. 2인 플레이에서는 호스트가 DumbwaiterNetworkSync로 모든 변경을 결정하고 각 클라이언트에 같은 호출을 전달한다.
// 이동 중에는 케이지와 카운터웨이트가 반대 방향으로 움직이고, 도르래 바퀴가 돌며 로프 길이가 따라 변한다.
// 이 컴포넌트는 "상태/이동"만 책임지고, 플레이어와의 접점은 DumbwaiterWindow(싣기/꺼내기)·DumbwaiterButton(호출/보내기)이 맡는다.
// 구조물(벽·케이지·도르래)은 Tools/KJH/Dumbwaiter/Build Rig 에디터 메뉴가 만들어 준다.
public class Dumbwaiter : MonoBehaviour
{
    [Header("층 정지 위치 (이 오브젝트 기준 로컬 Y, 케이지 바닥 높이)")]
    [Tooltip("0번=1층, 1번=2층. 층 개수만큼 값을 넣는다.")]
    [SerializeField] private float[] stopHeights = { 1.0f, 4.9f };

    [Header("움직이는 부품")]
    [SerializeField] private Transform cabin;
    [SerializeField] private Transform counterweight;
    [Tooltip("돌아가는 도르래 바퀴 (축은 로컬 Z).")]
    [SerializeField] private Transform pulleyWheel;
    [Tooltip("케이지에 이어지는 로프. 위쪽 끝은 도르래, 아래쪽 끝은 케이지 윗면. 기본 실린더(높이 2) 기준으로 늘어난다.")]
    [SerializeField] private Transform cabinRope;
    [SerializeField] private Transform counterweightRope;
    [Tooltip("도르래 로프가 걸리는 높이 (로컬 Y).")]
    [SerializeField] private float pulleyHeight = 7.2f;
    [SerializeField] private float cabinHeight = 0.7f;
    [SerializeField] private float counterweightHeight = 0.5f;
    [SerializeField] private float pulleyRadius = 0.15f;

    [Header("이동")]
    [Tooltip("1층~2층 전체 구간을 오가는 데 걸리는 시간(초).")]
    [SerializeField] private float travelTime = 4f;
    [Tooltip("시작할 때 케이지가 있는 층 (0=1층, 1=2층).")]
    [SerializeField] private int startFloor = 1;

    // 이 도르래가 받는 화물 종류. 코드에 하드코딩하지 않고 여기 설정으로 정한다.
    // Candle: 기존 양초(LGH.PlayerInventory). PotionLump: 약품 제조 퍼즐의 덩이(화물 값 = 덩이 아이템 코드).
    public enum CargoKind { Candle, PotionLump }

    [Header("화물")]
    [Tooltip("받는 화물 종류 (필터). 약품 제조 퍼즐에서는 PotionLump, 예전 양초 테스트로 되돌리려면 Candle.")]
    [SerializeField] private CargoKind acceptedCargo = CargoKind.Candle;
    [Tooltip("한 번에 실을 수 있는 최대 개수.")]
    [SerializeField] private int capacity = 3;
    [Tooltip("케이지 안에 실린 개수만큼 켜지는 자리표시용 오브젝트들.")]
    [SerializeField] private GameObject[] cargoVisuals;

    [Header("소리 (비어 있어도 동작)")]
    [SerializeField] private AudioSource moveLoopSource;
    [SerializeField] private AudioClip arriveClip;

    // 층 번호는 0부터 (0=1층, 1=2층).
    public int CurrentFloor { get; private set; }
    public bool IsMoving { get; private set; }
    // 이동 중이면 도착할 층, 아니면 CurrentFloor와 같다.
    public int TargetFloor { get; private set; }
    public int FloorCount => stopHeights.Length;
    public CargoKind AcceptedCargo => acceptedCargo;
    public int CargoCount => cargo.Count;
    public bool IsFull => cargo.Count >= capacity;
    public IReadOnlyList<float> Cargo => cargo;

    // 상태가 바뀔 때마다 알린다 (UI/사운드/나중의 네트워크 동기화 지점).
    public event System.Action OnStateChanged;

    private readonly List<float> cargo = new List<float>();
    private float cabinY;

    private void Awake()
    {
        CurrentFloor = Mathf.Clamp(startFloor, 0, stopHeights.Length - 1);
        TargetFloor = CurrentFloor;
        SetCabinY(stopHeights[CurrentFloor]);
        RefreshCargoVisuals();
    }

    public bool IsIdleAt(int floor) => !IsMoving && CurrentFloor == floor;

    // 지정한 층으로 보낸다. 이미 움직이는 중이거나 이미 그 층이면 false.
    public bool GoTo(int floor)
    {
        if (IsMoving || floor < 0 || floor >= stopHeights.Length || floor == CurrentFloor) return false;
        TargetFloor = floor;
        StartCoroutine(MoveRoutine(floor));
        return true;
    }

    // 양초 하나를 싣는다 (remaining = 남은 연소 시간). 움직이는 중이거나 가득 찼으면 false.
    public bool TryLoad(float remaining)
    {
        if (IsMoving || IsFull) return false;
        cargo.Add(remaining);
        RefreshCargoVisuals();
        OnStateChanged?.Invoke();
        return true;
    }

    // 실린 양초를 전부 꺼내 반환한다 (호출한 쪽이 인벤토리에 넣는다).
    public List<float> TakeAll()
    {
        var taken = new List<float>(cargo);
        cargo.Clear();
        RefreshCargoVisuals();
        OnStateChanged?.Invoke();
        return taken;
    }

    // 네트워크 동기화용: 화물 목록을 통째로 덮어쓴다.
    public void SetCargo(IEnumerable<float> items)
    {
        cargo.Clear();
        if (items != null) cargo.AddRange(items);
        RefreshCargoVisuals();
        OnStateChanged?.Invoke();
    }

    // 네트워크 동기화용: 연출 없이 바로 그 층에 놓는다 (나중에 접속한 플레이어용).
    public void SnapTo(int floor)
    {
        StopAllCoroutines();
        if (moveLoopSource != null) moveLoopSource.Stop();
        IsMoving = false;
        CurrentFloor = Mathf.Clamp(floor, 0, stopHeights.Length - 1);
        TargetFloor = CurrentFloor;
        SetCabinY(stopHeights[CurrentFloor]);
        OnStateChanged?.Invoke();
    }

    private IEnumerator MoveRoutine(int target)
    {
        IsMoving = true;
        OnStateChanged?.Invoke();
        if (moveLoopSource != null) moveLoopSource.Play();

        float from = cabinY;
        float to = stopHeights[target];
        // 이동 거리에 비례해 시간을 정한다 (travelTime은 맨 아래~맨 위 전체 구간 기준).
        float span = Mathf.Max(0.01f, Mathf.Abs(stopHeights[stopHeights.Length - 1] - stopHeights[0]));
        float duration = Mathf.Max(0.5f, travelTime * Mathf.Abs(to - from) / span);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // 출발·도착 때 덜컹이지 않게 부드럽게 가감속한다.
            SetCabinY(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
            yield return null;
        }
        SetCabinY(to);

        CurrentFloor = target;
        IsMoving = false;
        if (moveLoopSource != null) moveLoopSource.Stop();
        if (arriveClip != null) AudioSource.PlayClipAtPoint(arriveClip, transform.TransformPoint(new Vector3(0f, to, 0f)));
        OnStateChanged?.Invoke();
    }

    // 케이지 높이를 정하고 카운터웨이트·도르래·로프를 거기에 맞춘다.
    private void SetCabinY(float y)
    {
        cabinY = y;
        float bottom = stopHeights[0];
        float top = stopHeights[stopHeights.Length - 1];
        float cwY = bottom + top - y; // 케이지가 올라가면 카운터웨이트는 내려간다

        if (cabin != null) SetLocalY(cabin, y);
        if (counterweight != null) SetLocalY(counterweight, cwY);
        if (cabinRope != null) StretchRope(cabinRope, y + cabinHeight);
        if (counterweightRope != null) StretchRope(counterweightRope, cwY + counterweightHeight);
        if (pulleyWheel != null && pulleyRadius > 0f)
        {
            // 로프가 미끄러지지 않고 감기는 만큼 바퀴가 돈다 (각도 = 이동거리 / 반지름).
            float angle = (y - bottom) / pulleyRadius * Mathf.Rad2Deg;
            pulleyWheel.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private static void SetLocalY(Transform t, float y)
    {
        Vector3 p = t.localPosition;
        p.y = y;
        t.localPosition = p;
    }

    // 로프(높이 2짜리 기본 실린더)를 도르래~물체 윗면 사이에 맞게 늘린다.
    private void StretchRope(Transform rope, float lowerEnd)
    {
        float length = Mathf.Max(0.01f, pulleyHeight - lowerEnd);
        Vector3 p = rope.localPosition;
        p.y = lowerEnd + length * 0.5f;
        rope.localPosition = p;
        Vector3 s = rope.localScale;
        s.y = length * 0.5f; // 기본 실린더 높이가 2이므로 절반
        rope.localScale = s;
    }

    private void RefreshCargoVisuals()
    {
        if (cargoVisuals == null) return;
        for (int i = 0; i < cargoVisuals.Length; i++)
        {
            if (cargoVisuals[i] != null) cargoVisuals[i].SetActive(i < cargo.Count);
        }
    }
}
