using System;
using UnityEngine;

namespace AtticTreasure
{
    // 진짜 연출이 생기기 전까지 쓰는 임시 연출: 화면이 잠깐 어두워지고 문구가 뜬다.
    // AtticTreasureSync의 Cutscene 칸이 비어 있으면 자동으로 이것을 쓴다 (같은 오브젝트에 없으면 붙인다).
    // 입력·이동은 잠그지 않는다 (진짜 연출을 만들 때 필요하면 그쪽에서 잠근다).
    public class AtticTreasurePlaceholderCutscene : AtticTreasureCutscene
    {
        [SerializeField] float seconds = 2.5f;
        [SerializeField] string pickerText = "황금상을 손에 넣었다";
        [SerializeField] string otherText = "동료가 황금상을 손에 넣었다";

        float startedAt = -1f;
        bool picker;
        Action done;
        GUIStyle style;

        public bool IsPlaying { get { return startedAt >= 0f; } }

        public override void Play(bool isPicker, Action onFinished)
        {
            picker = isPicker;
            done = onFinished;
            startedAt = Time.unscaledTime;
        }

        void Update()
        {
            if (startedAt < 0f || Time.unscaledTime - startedAt < seconds) return;
            startedAt = -1f;
            var d = done;
            done = null;
            d?.Invoke();
        }

        void OnGUI()
        {
            if (startedAt < 0f) return;
            float t = (Time.unscaledTime - startedAt) / Mathf.Max(0.01f, seconds);
            float a = Mathf.Clamp01(Mathf.Min(t * 4f, (1f - t) * 4f)) * 0.85f;   // 빠르게 어두워졌다가 끝에 밝아짐
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
            style.fontSize = Mathf.RoundToInt(Screen.height / 640f * 24f);
            GUI.color = new Color(0.95f, 0.85f, 0.5f, a / 0.85f);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), (picker ? pickerText : otherText) + "\n(황금상 획득 연출 자리)", style);
            GUI.color = Color.white;
        }
    }
}
