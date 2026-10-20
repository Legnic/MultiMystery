using System.Collections;
using UnityEngine;

// 임시 금고 열림 연출: 문 회전축(DoorPivot)을 Y축으로 약 100도, 0.8초에 걸쳐 돌린다.
// 실제 금고 모델/열쇠 꽂기 모션이 준비되면 이 컴포넌트를 빼고 다른 ISafeOpenSequence 구현으로 교체한다.
// 사용법: 금고 루트(LockedSafe와 같은 오브젝트)에 붙인다. doorPivot을 비워두면 자식 중 "DoorPivot"을 찾아 쓴다.
public class SimpleDoorOpenSequence : MonoBehaviour, ISafeOpenSequence
{
    [Tooltip("문이 돌아가는 축 (경첩 위치의 빈 오브젝트). 비워두면 자식에서 이름이 'DoorPivot'인 오브젝트를 찾는다.")]
    [SerializeField] private Transform doorPivot;

    [Tooltip("문이 열리는 각도 (도). 양수면 위에서 봤을 때 시계 방향. 90도보다 살짝 더 열어 '활짝 열렸다'는 느낌을 준다.")]
    [SerializeField] private float openAngle = 100f;

    [Tooltip("문이 다 열리는 데 걸리는 시간 (초). 0이면 즉시 회전. 너무 빠르면 갑툭튀처럼 느껴져서 0.8초 정도로 천천히.")]
    [SerializeField] private float duration = 0.8f;

    [Tooltip("회전 속도 곡선. 기본값은 천천히 시작해서 천천히 멈추는 형태(무거운 금속 문 느낌).")]
    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("문이 열리기 시작할 때 한 번 재생할 소리 (경첩 끼익 등). 비워둬도 된다.")]
    [SerializeField] private AudioClip openSound;

    private void Awake()
    {
        // Inspector에서 연결을 깜빡해도 프리팹 계층 규칙(DoorPivot 이름 고정)대로 찾아서 동작하게 한다.
        if (doorPivot == null) doorPivot = transform.Find("DoorPivot");
    }

    public IEnumerator Play(KeyData key)
    {
        if (doorPivot == null)
        {
            Debug.LogWarning("[SimpleDoorOpenSequence] DoorPivot을 찾지 못해 문 회전 없이 엽니다.", this);
            yield break;
        }

        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, doorPivot.position);

        // 시작 회전에서 Y축으로 openAngle만큼 더 돌린 회전을 목표로 한다 (프리팹마다 문의 초기 각도가 달라도 동작).
        Quaternion start = doorPivot.localRotation;
        Quaternion end = start * Quaternion.Euler(0f, openAngle, 0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = easing.Evaluate(Mathf.Clamp01(elapsed / duration));
            doorPivot.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        // 마지막 프레임 오차 없이 정확히 목표 각도로 맞춘다 (duration이 0일 때는 여기서 즉시 회전).
        doorPivot.localRotation = end;
    }
}
