using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메인화면 버튼에 마우스를 올리면 살짝 커지고 밝아지는 효과
// 버튼 이미지 오브젝트에 붙이면 된다. 값은 모두 Inspector에서 조절할 수 있다.
public class MainMenuButtonHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler   // 마우스가 들어오고 나가는 순간을 알려 주는 기능
{
    [Header("크기")]
    [SerializeField] private float hoverScale = 1.08f;    // 마우스를 올렸을 때 크기 배율 (1.08 = 8% 커짐)
    [SerializeField] private float animationSpeed = 12f;  // 커지고 작아지는 속도 (클수록 빠름)

    [Header("밝기")]
    [SerializeField] private Graphic targetGraphic;                                  // 밝기를 바꿀 이미지 (비워 두면 자기 자신의 Image를 자동으로 사용)
    [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f, 1f); // 평소: 살짝 어둡게
    [SerializeField] private Color hoverColor = Color.white;                         // 올렸을 때: 원래 밝기

    private Vector3 baseScale;   // 처음 크기 (되돌아갈 기준)
    private bool isHovered;      // 지금 마우스가 올라와 있는지

    // 컴포넌트를 처음 붙일 때 자동으로 실행: 같은 오브젝트의 Image를 찾아 연결해 준다
    private void Reset()
    {
        targetGraphic = GetComponent<Graphic>();
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
        if (targetGraphic != null) targetGraphic.color = normalColor;
    }

    private void OnDisable()
    {
        // 화면이 꺼졌다 켜질 때 커진 상태로 남지 않게 원래대로
        isHovered = false;
        transform.localScale = baseScale;
        if (targetGraphic != null) targetGraphic.color = normalColor;
    }

    private void Update()
    {
        // 프레임 속도와 상관없이 부드럽게 목표값을 따라가게 하는 계산
        // unscaledDeltaTime: 게임이 일시정지(timeScale = 0)여도 동작
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);

        Vector3 targetScale = isHovered ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);

        if (targetGraphic != null)
            targetGraphic.color = Color.Lerp(targetGraphic.color, isHovered ? hoverColor : normalColor, t);
    }

    public void OnPointerEnter(PointerEventData eventData) { isHovered = true; }   // 마우스가 들어옴
    public void OnPointerExit(PointerEventData eventData)  { isHovered = false; }  // 마우스가 나감
}
