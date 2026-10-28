using UnityEngine;
using UnityEngine.InputSystem;

// 숫자 키를 누르면 손모양(인사·브이·중지·가리키기·엄지척)이 잠깐 떴다가 사라진다. (LHJ 시점 테스트용)
// 손이 뜨는 자리(slots)는 여러 개 둘 수 있다. 예: 내 화면에 보이는 손 + 상대 화면에 보이는 손.
// 각 자리의 레이어를 그대로 손에도 입혀서, 정해진 카메라에만 보이게 한다.
public class HandEmote : MonoBehaviour
{
    [System.Serializable]
    public class Gesture
    {
        public string name;
        public GameObject model;
        public RuntimeAnimatorController controller;
        public Key key;
        public float turnY;        // 손마다 원래 만들어진 방향이 달라서 Y축으로 돌려 맞춘다 (도)
        public float scale = 1f;   // 손마다 크기가 달라서 맞춘다
        public Vector3 offset;     // 손마다 뜨는 자리를 조금 옮긴다 (예: 엄지척은 얼굴 옆으로 들어 올림)
    }

    [SerializeField] Gesture[] gestures;
    [SerializeField] Transform[] slots;
    [SerializeField] Transform myViewSlot;   // 내 화면에 보이는 손 자리. 여기는 offset을 쓰지 않는다
    [SerializeField] Material handMaterial;
    [SerializeField] float showSeconds = 3f;

    [System.NonSerialized] GameObject[] shown;
    float hideAt;

    void Update()
    {
        // 플레이 중에 스크립트가 다시 컴파일돼도 깨지지 않게 여기서 준비한다
        if (shown == null || shown.Length != slots.Length) shown = new GameObject[slots.Length];

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            foreach (var gesture in gestures)
            {
                if (keyboard[gesture.key].wasPressedThisFrame) Show(gesture);
            }
        }

        if (shown.Length > 0 && shown[0] != null && Time.time >= hideAt) Hide();
    }

    void Show(Gesture gesture)
    {
        Hide();
        for (int i = 0; i < slots.Length; i++)
        {
            var hand = Instantiate(gesture.model, slots[i], false);
            hand.name = gesture.name;
            if (slots[i] != myViewSlot) hand.transform.localPosition = gesture.offset;
            hand.transform.localRotation = Quaternion.Euler(0f, gesture.turnY, 0f);
            hand.transform.localScale = Vector3.one * gesture.scale;
            SetLayer(hand.transform, slots[i].gameObject.layer);
            if (handMaterial != null)
            {
                foreach (var r in hand.GetComponentsInChildren<Renderer>()) r.sharedMaterial = handMaterial;
            }

            var animator = hand.GetComponent<Animator>();
            if (animator == null) animator = hand.AddComponent<Animator>();
            animator.runtimeAnimatorController = gesture.controller;
            shown[i] = hand;
        }
        hideAt = Time.time + showSeconds;
    }

    void Hide()
    {
        if (shown == null) return;
        for (int i = 0; i < shown.Length; i++)
        {
            if (shown[i] != null) Destroy(shown[i]);
            shown[i] = null;
        }
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayer(child, layer);
    }
}
