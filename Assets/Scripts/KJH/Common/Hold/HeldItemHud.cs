using UnityEngine;

// 손에 든 물건 이름을 화면 오른쪽 아래에 보여 주는 공용 표시 (2026-10-28 통일).
// 공용 들기 시스템(PlayerHand)과 PotionCraft(재료·물·가루 운반)가 같은 모양으로 쓴다 — 이름만, LevelUp 폰트.
// OnGUI 안에서만 부를 것.
public static class HeldItemHud
{
    static GUIStyle style, shadow;
    static Font styleFont;

    public static void Draw(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return;
        var font = CoopDemo.GameFont.Body;
        if (style == null || styleFont != font)
        {
            styleFont = font;
            style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, alignment = TextAnchor.MiddleRight, wordWrap = false };
            style.normal.textColor = CoopDemo.MeetYouGuiSkin.GuiColor(new Color(0.96f, 0.90f, 0.76f));
            shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        var old = GUI.matrix;
        var v = LGH.UiScale.Apply();
        // 오른쪽 아래 (획득 문구 자리 h-84 아래)
        var r = new Rect(v.x - 16f - 360f, v.y - 48f, 360f, 32f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), itemName, shadow);
        GUI.Label(r, itemName, style);
        GUI.matrix = old;
    }
}
