using System;
using System.Collections;
using UnityEngine;

// 리깅된 캐릭터가 생기기 전까지 쓰는 "임시 손". 카메라 아래(화면 밖)에 숨어 있다가,
// ReachTo가 호출되면 위로 살짝 떠오르는 곡선(2차 베지어 곡선)을 그리며 HandTarget으로 천천히 이동해 얹히고,
// Retract가 호출되면 같은 방식으로 카메라 아래로 돌아가 숨는다.
// 실제 손 모양은 자식 오브젝트(단순 도형 몇 개)로 만들고, 이 컴포넌트는 그 묶음 전체를 움직이기만 한다.
//
// 모델 축 약속 (HandTarget도 같은 약속으로 배치한다):
//   - 파란 Z축(forward) = 손가락 끝 방향
//   - 초록 Y축(up)      = 손등 방향 (손바닥 반대쪽)
//   - 원점(피벗)         = 손바닥 아랫면 중앙 → HandTarget 위치에 손바닥이 딱 닿는다
//
// 사용법: Main Camera의 자식으로 빈 오브젝트 "PlaceholderHand"를 만들어 이 컴포넌트를 붙이고,
//         화면 밖 아래쪽(예: 로컬 위치 0.22, -0.42, 0.35)에 놓는다. 그 자리가 "대기 자리"로 기억된다.
//         Player의 ObjectEchoController > Hand Reach 칸에 이 오브젝트를 연결한다.
public class PlaceholderHandReach : MonoBehaviour, IHandReach
{
    [Header("대기 자리")]
    [Tooltip("손이 숨어 있을 기준 카메라. 비워두면 부모(보통 Main Camera)를 쓴다. 대기 자리는 이 카메라 기준 로컬 좌표로 기억된다.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("대기 자리에 있는 동안 손 모양(렌더러)을 숨길지. 화면 밖에 있어도 그림자 등이 남을 수 있어서 기본으로 숨긴다.")]
    [SerializeField] private bool hideWhenResting = true;

    [Header("곡선 움직임")]
    [Tooltip("이동 경로가 위로 얼마나 휘어 오를지 (m). 0이면 직선. 손을 들어 올렸다 내려놓는 느낌을 준다.")]
    [SerializeField] private float arcHeight = 0.12f;

    [Tooltip("시간에 따른 진행도 곡선 (가로 0~1 시간, 세로 0~1 진행도). 기본은 천천히 시작해 천천히 멈추는 EaseInOut.")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("손이 HandTarget의 회전에 맞춰질 때 추가로 더할 회전 (도). 모델 축이 약속과 다를 때 보정용.")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Tooltip("도착 직전 마지막 몇 %에서 손이 살짝 내려앉는 깊이 (m). '얹는' 느낌을 주는 아주 작은 값.")]
    [SerializeField] private float settleDepth = 0.008f;

    // 대기 자리 (카메라 기준 로컬 좌표). Awake 때 현재 배치 위치를 기억한다.
    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;

    private Renderer[] renderers;
    private Coroutine moveRoutine;

    public bool IsMoving => moveRoutine != null;
    public bool IsExtended { get; private set; }

    private void Awake()
    {
        if (cameraTransform == null) cameraTransform = transform.parent;

        // 대기 자리는 "카메라 기준"으로 저장해야 플레이어가 돌아다녀도 항상 카메라 아래에 숨어 있게 된다.
        restLocalPosition = cameraTransform != null ? cameraTransform.InverseTransformPoint(transform.position) : transform.localPosition;
        restLocalRotation = cameraTransform != null ? Quaternion.Inverse(cameraTransform.rotation) * transform.rotation : transform.localRotation;

        renderers = GetComponentsInChildren<Renderer>(true);

        // 임시 도형에 기본으로 붙어 오는 콜라이더는 조준 레이캐스트/이동을 방해하므로 모두 끈다.
        foreach (Collider col in GetComponentsInChildren<Collider>(true)) col.enabled = false;

        SetVisible(!hideWhenResting);
    }

    // ── IHandReach ─────────────────────────────────────────

    public void ReachTo(Transform target, float duration, Action onArrived)
    {
        if (target == null)
        {
            Debug.LogWarning("[PlaceholderHandReach] 손을 뻗을 목표(HandTarget)가 비어 있습니다.", this);
            onArrived?.Invoke(); // 연출이 멈춰 버리지 않도록 바로 완료 처리한다
            return;
        }

        IsExtended = true;
        SetVisible(true);
        // 목표는 매 프레임 다시 읽는다 (움직이는 사물에 얹을 수도 있으므로 위치를 미리 고정하지 않는다).
        StartMove(() => target.position, () => target.rotation * Quaternion.Euler(rotationOffset), duration, true, onArrived);
    }

    public void Retract(float duration, Action onRetracted)
    {
        // 돌아갈 자리는 "지금 카메라 기준 대기 자리". 카메라가 돌고 있어도 따라가도록 매 프레임 계산한다.
        StartMove(GetRestPosition, GetRestRotation, duration, false, () =>
        {
            IsExtended = false;
            if (hideWhenResting) SetVisible(false);
            onRetracted?.Invoke();
        });
    }

    public void SnapToRest()
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = null;
        transform.SetPositionAndRotation(GetRestPosition(), GetRestRotation());
        IsExtended = false;
        if (hideWhenResting) SetVisible(false);
    }

    // ── 내부 동작 ──────────────────────────────────────────

    private void StartMove(Func<Vector3> getEndPos, Func<Quaternion> getEndRot, float duration, bool settle, Action onDone)
    {
        // 이미 움직이는 중이면 멈추고 "지금 위치"에서 새로 출발한다 (순간이동 방지).
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRoutine(getEndPos, getEndRot, duration, settle, onDone));
    }

    private IEnumerator MoveRoutine(Func<Vector3> getEndPos, Func<Quaternion> getEndRot, float duration, bool settle, Action onDone)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 일시정지(timeScale = 0) 중에도 연출이 멈추지 않도록 unscaledDeltaTime을 쓴다 (단서 연출과 같은 규칙).
            elapsed += Time.unscaledDeltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / duration));

            Vector3 endPos = getEndPos();
            Vector3 up = cameraTransform != null ? cameraTransform.up : Vector3.up;

            // 2차 베지어 곡선: 시작점 → (가운데 위로 띄운 조절점) → 끝점.
            // 조절점을 위로 올려 두면 손이 직선으로 미끄러지지 않고 "들어 올렸다 내려놓는" 호를 그린다.
            Vector3 control = (startPos + endPos) * 0.5f + up * arcHeight;
            Vector3 pos = Bezier(startPos, control, endPos, t);

            // 뻗을 때만: 마지막 15% 구간에서 아주 살짝 눌렀다 제자리로 오는 "얹힘" 느낌을 더한다.
            if (settle && t > 0.85f)
            {
                float k = Mathf.InverseLerp(0.85f, 1f, t);
                pos -= getEndRot() * Vector3.up * (Mathf.Sin(k * Mathf.PI) * settleDepth);
            }

            transform.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, getEndRot(), t));
            yield return null;
        }

        // 마지막엔 오차 없이 정확한 목표 자세로 맞춘다.
        transform.SetPositionAndRotation(getEndPos(), getEndRot());
        moveRoutine = null;
        onDone?.Invoke();
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private Vector3 GetRestPosition() =>
        cameraTransform != null ? cameraTransform.TransformPoint(restLocalPosition) : transform.position;

    private Quaternion GetRestRotation() =>
        cameraTransform != null ? cameraTransform.rotation * restLocalRotation : transform.rotation;

    private void SetVisible(bool visible)
    {
        if (renderers == null) return;
        foreach (Renderer r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }
}
