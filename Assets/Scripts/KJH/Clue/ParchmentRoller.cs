using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

// 평평한 양피지 메시의 정점을 코드로 "아르키메데스 나선" 모양으로 감아서 돌돌 말린 모양을 만드는 컴포넌트.
// 말림 정도는 rollAmount(0 = 평평, 1 = 완전히 말림) 값 하나로 조절된다.
// 나중에 이 값을 1 → 0으로 천천히 바꾸기만 하면 "양피지가 펼쳐지는 연출"이 되도록 설계했다.
//
// [ExecuteAlways]를 붙인 이유: 플레이 버튼을 누르지 않아도 에디터 화면(Scene 뷰)에서
// 슬라이더를 움직이면 바로 말린 모양을 확인할 수 있게 하기 위해서다.
//
// 사용법: MeshFilter가 붙어 있는 오브젝트(양피지 메시 오브젝트)에 붙인다.
// 원본 메시(FBX 안의 메시)는 절대 수정하지 않는다. 항상 "복사본" 메시를 만들어 그걸 변형한다.
// IRevealAnimation을 구현하므로, 같은 단서 오브젝트(또는 자식)에 InspectableClue가 있으면
// 살펴보기 시작 시 자동으로 펼쳐지고 내려놓을 때 다시 말린다.
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class ParchmentRoller : MonoBehaviour, IRevealAnimation
{
    // 로컬 좌표의 X / Y / Z 축 중 어느 것인지 고르기 위한 선택지.
    public enum Axis { X = 0, Y = 1, Z = 2 }

    [Header("원본 메시")]
    [Tooltip("변형 전 원본 메시. 비어 있으면 처음 켜질 때 MeshFilter에 들어 있던 메시를 자동으로 저장한다. 이 메시는 절대 수정되지 않는다.")]
    [SerializeField] private Mesh sourceMesh;

    [Header("말림 정도")]
    [Tooltip("0 = 완전히 평평, 1 = 완전히 말림. 나중에 펼침 연출은 이 값을 1 → 0으로 움직이면 된다.")]
    [Range(0f, 1f)]
    [SerializeField] private float rollAmount = 1f;

    [Header("나선 모양 (0 = 메시 크기에서 자동 계산)")]
    [Tooltip("가장 안쪽 심의 반지름 (m, 로컬 단위). 0이면 '길이 × 0.03'으로 자동 계산한다.")]
    [SerializeField] private float coreRadius = 0f;

    [Tooltip("한 바퀴 돌 때마다 반지름이 커지는 양 (m, 로컬 단위). 겹이 서로 뚫고 지나가지 않게 (눌린) 두께보다 커야 한다. 0이면 '눌린 두께 × 1.2'(최소 0.0008)로 자동 계산한다.")]
    [SerializeField] private float layerGap = 0f;

    [Tooltip("말릴 때 양피지 두께(와 표면 굴곡)를 이 배율로 눌러준다. 이 모델은 실제 두께가 약 2cm로 두꺼워서 그대로 감으면 원통이 너무 굵어지기 때문이다. 1 = 원래 두께 그대로. rollAmount가 0에서 0.1로 갈 때 서서히 적용된다.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float rolledThicknessScale = 0.25f;

    [Header("축 설정 (0단계 분석 결과: 길이 Y, 폭 X, 두께 Z)")]
    [Tooltip("말리는 방향의 축 (가장 긴 축).")]
    [SerializeField] private Axis lengthAxis = Axis.Y;

    [Tooltip("말린 원통의 축 (가운데 길이의 축). 이 축 방향 좌표는 변형하지 않는다.")]
    [SerializeField] private Axis widthAxis = Axis.X;

    [Tooltip("두께 축 (가장 짧은 축).")]
    [SerializeField] private Axis thicknessAxis = Axis.Z;

    [Header("방향")]
    [Tooltip("false = 길이 축의 작은 쪽(−) 끝부터 말기, true = 큰 쪽(+) 끝부터 말기.")]
    [SerializeField] private bool rollFromMaxEdge = false;

    [Tooltip("true = 두께 축 + 쪽으로 말려 올라감 (+ 쪽 면이 안쪽으로 감김). 이 양피지는 +Z가 위(그림 면)라서 true면 책상 반대쪽으로 말리고 그림이 안쪽에 숨는다.")]
    [SerializeField] private bool rollTowardPositive = true;

    [Tooltip("완전히 말렸을 때 원통이 원래 양피지의 가운데에 오도록 길이 축 방향으로 위치를 보정한다.")]
    [SerializeField] private bool centerWhenRolled = true;

    // 실제로 변형을 적용하는 복사본 메시. 씬/에셋에 저장되지 않는다 (HideFlags.DontSave).
    private Mesh instanceMesh;
    private MeshFilter meshFilter;
    private BoxCollider boxCollider;

    // 원본 정점 데이터 캐시. 매번 원본 메시에서 읽어오면 느리고 메모리 할당도 생기므로 한 번만 읽어둔다.
    private Vector3[] srcVertices;
    private Vector3[] srcNormals;
    private Vector4[] srcTangents;
    // 변형 결과를 담을 작업용 배열. 계산할 때마다 새로 만들지 않고 재사용한다.
    private Vector3[] outVertices;
    private Vector3[] outNormals;
    private Vector4[] outTangents;

    // 원본 메시의 길이/두께 정보 (원본 bounds에서 계산).
    private float lengthMin, lengthMax, thicknessMid, thicknessHalf;

    // 펼침/말림 애니메이션 코루틴 (AnimateRoll). 중복 실행을 막기 위해 들고 있는다.
    private Coroutine rollRoutine;

    // 외부(나중의 펼침 연출 등)에서 말림 정도를 읽고 바꾸는 통로.
    // 값을 바꾸면 바로 메시를 다시 계산한다 (매 프레임 계산하지 않고, 바뀔 때만 계산).
    public float RollAmount
    {
        get => rollAmount;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(clamped, rollAmount)) return; // 같은 값이면 다시 계산할 필요가 없다
            rollAmount = clamped;
            ApplyRoll();
        }
    }

    // 원클릭 설정 메뉴(ClueSetupMenu) 등에서 축/방향 정보를 읽기 위한 읽기 전용 통로.
    public Mesh SourceMesh => sourceMesh;
    public Axis LengthAxis => lengthAxis;
    public Axis ThicknessAxis => thicknessAxis;
    public bool RollTowardPositive => rollTowardPositive;

    private void Awake()
    {
        // GetComponent는 비용이 있으니 Awake에서 한 번만 찾아서 저장해 둔다.
        meshFilter = GetComponent<MeshFilter>();
        boxCollider = GetComponent<BoxCollider>();
    }

    // 에디터에서 컴포넌트를 처음 붙이거나 Inspector의 ⋮ → Reset을 누르면 Unity가 자동으로 호출한다.
    // 새 모델에 붙이자마자 축이 맞춰지도록 여기서 축 자동 판별을 한다.
    private void Reset()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (sourceMesh == null && meshFilter.sharedMesh != null && !IsInstanceMesh(meshFilter.sharedMesh))
        {
            sourceMesh = meshFilter.sharedMesh;
        }
        AutoDetectAxes();
    }

    // 원본 메시 크기를 보고 축을 자동으로 정한다: 가장 짧은 축 = 두께, 가장 긴 축 = 길이(말리는 방향), 나머지 = 폭.
    // Inspector 오른쪽 위 ⋮ 메뉴(또는 컴포넌트 우클릭)에서도 실행할 수 있다.
    [ContextMenu("축 자동 판별 (짧은 축 = 두께, 긴 축 = 길이)")]
    public void AutoDetectAxes()
    {
        if (sourceMesh == null) return;

#if UNITY_EDITOR
        // Ctrl+Z로 되돌릴 수 있도록 바꾸기 전 상태를 기록한다.
        UnityEditor.Undo.RecordObject(this, "Parchment 축 자동 판별");
#endif
        Vector3 size = sourceMesh.bounds.size;
        int[] order = { 0, 1, 2 };
        System.Array.Sort(order, (x, y) => size[x].CompareTo(size[y])); // 짧은 축부터 긴 축 순서로 정렬
        thicknessAxis = (Axis)order[0];
        widthAxis = (Axis)order[1];
        lengthAxis = (Axis)order[2];

        Rebuild(); // 축이 바뀌면 길이/두께 정보가 달라지므로 복사본 메시부터 다시 만든다
    }

    // 복사본 메시를 새로 만들고 현재 값으로 다시 변형한다. 축을 바꿨거나 콜라이더를 새로 붙였을 때 쓴다.
    public void Rebuild()
    {
        if (!isActiveAndEnabled || sourceMesh == null) return; // 꺼져 있으면 다음 OnEnable에서 어차피 다시 만든다
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        boxCollider = GetComponent<BoxCollider>();
        if (!CheckReadable()) return;
        CreateInstanceMesh();
        ApplyRoll();
    }

    // 원본 메시의 정점을 읽을 수 있는지 확인한다. FBX 임포트 설정의 Read/Write가 꺼져 있으면 정점을 읽지 못한다.
    private bool CheckReadable()
    {
        if (sourceMesh.isReadable) return true;
        Debug.LogWarning($"[ParchmentRoller] '{sourceMesh.name}' 메시를 읽을 수 없습니다. FBX를 선택하고 Inspector의 Model 탭에서 Read/Write를 켜주세요. " +
                         "(Tools/KJH/Clue/Make Inspectable Parchment 메뉴를 쓰면 자동으로 켜집니다)", this);
        return false;
    }

    private void OnEnable()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();

        // sourceMesh가 비어 있으면 지금 MeshFilter에 있는 메시를 원본으로 기억한다.
        // 단, 이미 우리가 만든 복사본이 들어 있는 경우(비정상 종료 등)는 원본으로 착각하면 안 되므로 걸러낸다.
        if (sourceMesh == null && meshFilter.sharedMesh != null && !IsInstanceMesh(meshFilter.sharedMesh))
        {
            sourceMesh = meshFilter.sharedMesh;
        }
        if (sourceMesh == null) return;
        if (!CheckReadable()) return;

        CreateInstanceMesh();
        ApplyRoll();

#if UNITY_EDITOR
        // 씬 저장 직전에 MeshFilter를 원본으로 돌려놓고, 저장이 끝나면 다시 복사본을 끼운다.
        // 이유: 저장되지 않는 복사본(DontSave)을 가리킨 채 저장하면 씬 파일에 "메시 없음"으로 기록되기 때문.
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaved -= OnSceneSaved;
#endif
        // 꺼질 때는 MeshFilter를 원본 메시로 되돌리고 복사본을 지운다.
        // 그래야 컴포넌트를 지우거나 씬을 닫아도 양피지 메시가 사라지지 않는다.
        if (meshFilter != null && sourceMesh != null) meshFilter.sharedMesh = sourceMesh;
        DestroyInstanceMesh();
    }

    // Inspector에서 값을 바꿀 때(그리고 Undo 할 때) Unity가 자동으로 호출한다.
    private void OnValidate()
    {
        // OnValidate는 OnEnable보다 먼저 불릴 수도 있어서, 준비가 안 됐으면 아무것도 하지 않는다.
        if (instanceMesh == null || srcVertices == null) return;
        ApplyRoll();
    }

    // 나중의 펼침 연출용: 말림 값을 target까지 duration초 동안 부드럽게 바꾼다.
    // 이번 작업에서는 호출하지 않는다 (연결 지점만 만들어 둠).
    public Coroutine AnimateRoll(float target, float duration)
    {
        if (rollRoutine != null) StopCoroutine(rollRoutine);
        rollRoutine = StartCoroutine(AnimateRollRoutine(Mathf.Clamp01(target), duration));
        return rollRoutine;
    }

    // Inspector의 UnityEvent(예: InspectableClue의 On Inspect Started)에 연결하기 위한 버전.
    // UnityEvent 목록에는 "반환값이 없고(void) 인자가 0~1개"인 함수만 보이기 때문에,
    // 인자가 2개이고 Coroutine을 돌려주는 AnimateRoll은 목록에 나타나지 않는다. 그래서 감싸는 함수를 따로 둔다.
    // 인자 = 걸리는 시간 (초).
    public void UnrollOver(float duration)
    {
        AnimateRoll(0f, duration); // 0 = 평평하게 펼치기
    }

    // 다시 돌돌 마는 버전 (예: On Inspect Ended에 연결해서 내려놓을 때 말아 넣기). 인자 = 걸리는 시간 (초).
    public void RollUpOver(float duration)
    {
        AnimateRoll(1f, duration); // 1 = 완전히 말기
    }

    // IRevealAnimation 구현: InspectableClue가 자동으로 호출한다. 양피지에서 "드러내기" = 펼치기.
    public void PlayReveal(float duration) => UnrollOver(duration);

    // IRevealAnimation 구현: "감추기" = 다시 말기.
    public void PlayHide(float duration) => RollUpOver(duration);

    private IEnumerator AnimateRollRoutine(float target, float duration)
    {
        float start = rollAmount;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 연출은 일시정지(timeScale = 0)와 무관하게 진행되도록 unscaledDeltaTime을 쓴다.
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)); // 천천히 시작해서 천천히 멈춤
            RollAmount = Mathf.Lerp(start, target, t);
            yield return null;
        }
        RollAmount = target;
        rollRoutine = null;
    }

    // ── 메시 관리 ─────────────────────────────────────────────

    private void CreateInstanceMesh()
    {
        DestroyInstanceMesh();

        // 원본을 복사한다. 원본(FBX 안의 메시)은 읽기만 하고 절대 수정하지 않는다.
        instanceMesh = Instantiate(sourceMesh);
        instanceMesh.name = sourceMesh.name + " (Rolled)";
        // DontSave: 씬이나 에셋 파일에 저장되지 않는 임시 메시로 만든다 (씬 파일이 불필요하게 커지는 것도 방지).
        instanceMesh.hideFlags = HideFlags.DontSave;
        // 값이 자주 바뀔 수 있는 메시라고 GPU 쪽에 알려준다 (펼침 애니메이션 대비).
        instanceMesh.MarkDynamic();

        // 원본 정점 데이터를 한 번만 읽어서 캐시한다.
        srcVertices = sourceMesh.vertices;
        srcNormals = sourceMesh.normals;
        srcTangents = sourceMesh.tangents;
        outVertices = new Vector3[srcVertices.Length];
        outNormals = new Vector3[srcNormals.Length];
        outTangents = new Vector4[srcTangents.Length];

        // 원본 bounds에서 길이 축의 시작/끝, 두께 축의 가운데/절반 두께를 구한다.
        Bounds b = sourceMesh.bounds;
        int li = (int)lengthAxis, ti = (int)thicknessAxis;
        lengthMin = b.min[li];
        lengthMax = b.max[li];
        thicknessMid = b.center[ti];
        thicknessHalf = b.extents[ti];

        meshFilter.sharedMesh = instanceMesh;
    }

    private void DestroyInstanceMesh()
    {
        if (instanceMesh == null) return;
        // 에디터 모드에서는 Destroy가 동작하지 않으므로 DestroyImmediate를 쓴다.
        if (Application.isPlaying) Destroy(instanceMesh);
        else DestroyImmediate(instanceMesh);
        instanceMesh = null;
    }

    private static bool IsInstanceMesh(Mesh mesh)
    {
        return (mesh.hideFlags & HideFlags.DontSave) != 0 && mesh.name.EndsWith(" (Rolled)");
    }

#if UNITY_EDITOR
    private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        if (scene != gameObject.scene || meshFilter == null || sourceMesh == null) return;
        meshFilter.sharedMesh = sourceMesh; // 씬 파일에는 원본 메시 참조가 저장되게 한다
    }

    private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene != gameObject.scene || meshFilter == null || instanceMesh == null) return;
        meshFilter.sharedMesh = instanceMesh; // 저장이 끝났으니 다시 말린 메시를 보여준다
    }
#endif

    // ── 변형 계산 ─────────────────────────────────────────────

    // 현재 rollAmount에 맞게 모든 정점을 다시 계산해서 복사본 메시에 넣는다.
    private void ApplyRoll()
    {
        if (instanceMesh == null || srcVertices == null) return;

        int li = (int)lengthAxis, wi = (int)widthAxis, ti = (int)thicknessAxis;
        if (li == wi || li == ti || wi == ti)
        {
            Debug.LogWarning("[ParchmentRoller] 길이/폭/두께 축은 서로 달라야 합니다.", this);
            return;
        }

        float length = lengthMax - lengthMin;
        if (length <= 0f) return;

        // 길이 축 방향 부호: 어느 끝에서부터 말지에 따라 "자유단에서 잰 거리 u"의 방향이 바뀐다.
        float dirL = rollFromMaxEdge ? -1f : 1f;
        float edgeL = rollFromMaxEdge ? lengthMax : lengthMin;
        // 두께 축 부호: 어느 쪽으로 말려 올라갈지.
        float dirT = rollTowardPositive ? 1f : -1f;

        // 두께 누르기 배율. rollAmount가 0일 때는 원래 모양(1배), 0.1 이상이면 rolledThicknessScale을 그대로 쓴다.
        // 이렇게 하면 0일 때는 원본과 완전히 똑같고, 말리기 시작하면 바로 얇아진다.
        float thickScale = Mathf.Lerp(1f, rolledThicknessScale, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rollAmount / 0.1f)));

        // 나선 모양 값 (0이면 자동 계산).
        float a = coreRadius > 0f ? coreRadius : length * 0.03f;
        float pressedThickness = thicknessHalf * 2f * rolledThicknessScale;
        float bGap = layerGap > 0f ? layerGap : Mathf.Max(pressedThickness * 1.2f, 0.0008f);

        // S = 말린 길이. Φ = 말린 부분 전체의 감긴 각도, R = 가장 바깥 반지름.
        float S = rollAmount * length;
        float Phi = SolvePhi(S, a, bGap);
        float R = SpiralRadius(Phi, a, bGap);

        // 완전히 말렸을 때 원통이 양피지 가운데에 오도록 길이 방향으로 밀어주는 양.
        float shift = centerWhenRolled ? -0.5f * length * rollAmount : 0f;

        for (int i = 0; i < srcVertices.Length; i++)
        {
            Vector3 p = srcVertices[i];

            // u: 자유단(말기 시작하는 끝)에서 잰 길이 방향 거리.
            float u = (p[li] - edgeL) * dirL;
            // h: 두께 방향 좌표. 가운데 면을 0으로, "말려 올라가는 쪽"을 +로 맞춘다.
            float h = (p[ti] - thicknessMid) * dirT;
            // 두께를 "바깥쪽 면(-thicknessHalf)" 기준으로 눌러준다.
            // 바깥 면 기준으로 누르는 이유: 그래야 책상에 닿는 면 높이가 그대로라 원통이 떠 보이지 않는다.
            h = -thicknessHalf + (h + thicknessHalf) * thickScale;

            float x, y;      // 2D 평면(길이 방향 x, 두께 방향 y)에서의 새 위치
            float sinT = 0f, cosT = 1f; // 노멀 회전용 (평평한 부분은 회전 없음)

            if (u < S)
            {
                // 감기는 정점: 심에서부터 잰 각도 φ를 구하고, 접합부 기준 안쪽으로 돈 각도 θ = Φ − φ.
                float phi = SolvePhi(u, a, bGap);
                float theta = Phi - phi;
                float r = SpiralRadius(phi, a, bGap);
                sinT = Mathf.Sin(theta);
                cosT = Mathf.Cos(theta);

                // 원통 중심 C = (S, R). 위치 = C + (r − h) × (−sinθ, −cosθ).
                // θ = 0(접합부)에서 (S, h)가 되어 평평한 부분과 끊김 없이 이어진다.
                float rad = r - h;
                x = S - rad * sinT;
                y = R - rad * cosT;
            }
            else
            {
                // 아직 안 감긴 정점: 그대로 평평하게 둔다.
                x = u;
                y = h;
            }

            x += shift;

            // 2D 결과를 다시 로컬 3D 좌표로 되돌린다. 폭 축 좌표(p[wi])는 그대로 둔다.
            p[li] = edgeL + x * dirL;
            p[ti] = thicknessMid + y * dirT;
            outVertices[i] = p;

            // 노멀/탄젠트는 RecalculateNormals를 쓰지 않고 원래 값을 θ만큼 회전한다.
            // 이유: 다시 계산하면 앞면/뒷면/옆면이 만나는 모서리가 뭉개져서 경계가 깨지기 때문.
            if (i < srcNormals.Length) outNormals[i] = RotateInPlane(srcNormals[i], li, ti, dirL, dirT, sinT, cosT);
            if (i < srcTangents.Length)
            {
                Vector4 t4 = srcTangents[i];
                Vector3 t3 = RotateInPlane(new Vector3(t4.x, t4.y, t4.z), li, ti, dirL, dirT, sinT, cosT);
                outTangents[i] = new Vector4(t3.x, t3.y, t3.z, t4.w); // w(좌우 손잡이 방향)는 그대로 유지
            }
        }

        instanceMesh.vertices = outVertices;
        if (srcNormals.Length > 0) instanceMesh.normals = outNormals;
        if (srcTangents.Length > 0) instanceMesh.tangents = outTangents;
        instanceMesh.RecalculateBounds(); // 화면 밖 판정(컬링)이 바뀐 모양을 따라가도록 bounds를 갱신

        UpdateCollider();
    }

    // 같은 오브젝트에 BoxCollider가 있으면 말린 모양 크기에 맞춰 준다 (레이캐스트가 실제 모양에 맞게 맞도록).
    private void UpdateCollider()
    {
        if (boxCollider == null) return;
        Bounds b = instanceMesh.bounds;
        boxCollider.center = b.center;
        boxCollider.size = b.size;
    }

    // 2D 평면(길이 축, 두께 축)에서 벡터를 θ만큼 돌린다. 평평할 때의 "위"(0,1)가 원통 안쪽 (sinθ, cosθ)을 향하게 하는 회전.
    private static Vector3 RotateInPlane(Vector3 v, int li, int ti, float dirL, float dirT, float sinT, float cosT)
    {
        float vx = v[li] * dirL;
        float vy = v[ti] * dirT;
        float rx = vx * cosT + vy * sinT;
        float ry = -vx * sinT + vy * cosT;
        v[li] = rx * dirL;
        v[ti] = ry * dirT;
        return v;
    }

    // 나선 반지름 r(φ) = a + (b / 2π)·φ
    private static float SpiralRadius(float phi, float a, float b)
    {
        return a + b / (2f * Mathf.PI) * phi;
    }

    // 감긴 길이 s(φ) = aφ + (b / 4π)φ² 를 φ에 대해 푼 값 (근의 공식). 감긴 길이 s가 주어지면 각도 φ를 돌려준다.
    private static float SolvePhi(float s, float a, float b)
    {
        if (s <= 0f) return 0f;
        float k = b / (4f * Mathf.PI);
        if (k < 1e-8f) return s / a; // 간격이 0이면 원 둘레를 따라 감는 것과 같다
        return (-a + Mathf.Sqrt(a * a + 4f * k * s)) / (2f * k);
    }
}
