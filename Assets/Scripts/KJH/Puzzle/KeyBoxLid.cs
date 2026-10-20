using System.Collections;
using UnityEngine;

// 열쇠 상자 뚜껑. PortraitPuzzle이 정답 판정 시 Open()을 호출하면 경첩 축을 기준으로 뚜껑이 열린다.
// 뚜껑 모델이 나중에 진짜 박스로 교체되면 lid만 새 뚜껑으로 다시 연결하고, 경첩 위치/축/각도는 Inspector에서 맞추면 된다.
public class KeyBoxLid : MonoBehaviour
{
    [Tooltip("열릴 뚜껑 (Boxupper)")]
    [SerializeField] Transform lid;
    [Tooltip("경첩 위치. 뚜껑의 로컬 좌표 기준(큐브면 한쪽 모서리는 ±0.5). 교체 모델은 이 값을 모델에 맞게 조정")]
    [SerializeField] Vector3 hingeLocalOffset = new Vector3(0f, -0.5f, 0.5f);
    [Tooltip("경첩 회전축. 뚜껑의 로컬 좌표 기준")]
    [SerializeField] Vector3 hingeLocalAxis = Vector3.right;
    [Tooltip("열린 각도(도). 열리는 방향이 반대면 부호를 바꾼다")]
    [SerializeField] float openAngle = -110f;
    [SerializeField] float duration = 0.9f;

    public bool IsOpen { get; private set; }

    Vector3 closedPos;
    Quaternion closedRot;
    bool cached;

    void Awake()
    {
        IsOpen = false;
        cached = false;
    }

    void Cache()
    {
        if (cached || lid == null) return;
        closedPos = lid.position;
        closedRot = lid.rotation;
        cached = true;
    }

    // delay초 뒤에 연다(액자 낙하 타이밍과 맞추기 위함). instant면 연출 없이 바로 열린 상태로 만든다(늦게 접속한 플레이어용).
    public void Open(float delay = 0f, bool instant = false)
    {
        if (lid == null) { Debug.LogWarning("[KeyBoxLid] lid가 연결되어 있지 않아 뚜껑을 열 수 없다", this); return; }
        if (IsOpen) return;
        IsOpen = true;
        Debug.Log($"[KeyBoxLid] 뚜껑 열기 시작 delay={delay} instant={instant} activeInHierarchy={isActiveAndEnabled}", this);
        Cache();
        if (instant) SetAngle(openAngle);
        else StartCoroutine(Animate(delay));
    }

    IEnumerator Animate(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            SetAngle(openAngle * k);
            yield return null;
        }
        SetAngle(openAngle);
    }

    void SetAngle(float angle)
    {
        Vector3 pivot = closedPos + closedRot * Vector3.Scale(hingeLocalOffset, lid.lossyScale);
        Vector3 axis = closedRot * hingeLocalAxis.normalized;
        Quaternion q = Quaternion.AngleAxis(angle, axis);
        lid.SetPositionAndRotation(pivot + q * (closedPos - pivot), q * closedRot);
    }
}
