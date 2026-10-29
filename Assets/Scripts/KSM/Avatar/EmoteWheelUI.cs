using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    // 마우스 가운데 버튼을 누르고 있으면 화면 가운데에 도넛 모양 몸짓 선택 메뉴가 뜬다.
    // 마우스를 원하는 조각 쪽으로 움직였다가 버튼을 떼면 그 몸짓이 재생된다(CharacterAvatar.RequestEmote 호출).
    // 누르는 동안은 시점 회전만 멈추고(FirstPersonController.LookEnabled) 이동은 그대로 된다 - 커서 잠금
    // 자체는 계속 유지하므로(마우스 델타만 읽음) PlayerInteractor 등 다른 "커서 풀림=일시정지" 판정과 안 겹친다.
    // NetworkPlayer.ownerOnly 배열에 들어가서 소유자 화면에서만 켜진다(FirstPersonController/PlayerInteractor와 같은 방식).
    public class EmoteWheelUI : MonoBehaviour
    {
        // 2026-10-29: LHJ님이 Blender로 만든 손 5종을 연결. 순서는 EmoteHandView.hands(EmoteHandBuilder.Gestures)와 같아야 한다.
        // 칸 이름(화면에는 그리지 않고 칸 개수·순서 기준으로만 쓴다 - 2026-10-29 텍스트 표시 제거, 아이콘만 표시)
        static readonly string[] Labels = { "인사", "브이", "중지", "가리키기", "엄지척" };
        [Tooltip("Labels와 같은 순서의 아이콘 (Assets/Images/KSM/Emote). EmoteHandBuilder가 연결한다. 비어 있으면 점으로 그린다")]
        public Texture2D[] icons;
        public float radius = 110f;
        public float iconSize = 64f;   // 고른 칸은 1.3배
        public float deadZone = 30f;   // 이 반경 안에서 떼면 선택 취소

        LGH.FirstPersonController move;
        CharacterAvatar avatar;
        bool open;
        Vector2 cursor;   // 화면 가운데(0,0) 기준 가상 커서 - OS 커서는 계속 잠긴 채다

        Texture2D ringTex, dotTex, dotHighlightTex, iconBackTex;

        void Awake()
        {
            move = GetComponent<LGH.FirstPersonController>();
            avatar = GetComponentInChildren<CharacterAvatar>(true);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) { if (open) Close(false); return; }   // 일시정지 등이 뜨면 취소

            if (!open && mouse.middleButton.wasPressedThisFrame) Open();
            else if (open && mouse.middleButton.wasReleasedThisFrame) Close(true);

            if (open)
            {
                cursor += mouse.delta.ReadValue();
                cursor = Vector2.ClampMagnitude(cursor, radius * 1.6f);
            }
        }

        void Open()
        {
            open = true;
            cursor = Vector2.zero;
            if (move != null) move.LookEnabled = false;
        }

        void Close(bool confirm)
        {
            open = false;
            if (move != null) move.LookEnabled = true;
            if (!confirm || cursor.magnitude < deadZone) return;   // 가운데서 떼면 취소
            if (avatar != null) avatar.RequestEmote(IndexOf(cursor));
        }

        // 바로 위(0시 방향)를 0번으로 시계방향 순서 - OnGUI에서 그리는 순서와 맞춰야 한다.
        static int IndexOf(Vector2 v)
        {
            float step = 360f / Labels.Length;
            float ang = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            return Mathf.RoundToInt(ang / step) % Labels.Length;
        }

        void OnDisable() { if (open) Close(false); }

        void OnGUI()
        {
            if (!open) return;
            EnsureAssets();
            var v = LGH.UiScale.Apply();
            Vector2 center = new Vector2(v.x * 0.5f, v.y * 0.5f);

            float ringSize = radius * 2.4f;
            GUI.DrawTexture(new Rect(center.x - ringSize * 0.5f, center.y - ringSize * 0.5f, ringSize, ringSize), ringTex);

            int hovered = cursor.magnitude < deadZone ? -1 : IndexOf(cursor);
            for (int i = 0; i < Labels.Length; i++)
            {
                float ang = i * (360f / Labels.Length) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
                Vector2 pos = center + dir * radius;
                bool hi = i == hovered;

                var icon = icons != null && i < icons.Length ? icons[i] : null;
                if (icon != null)
                {
                    // 어두운 원 배경 위에 아이콘. 고른 칸은 노란 테두리 + 1.3배
                    float iconPx = hi ? iconSize * 1.3f : iconSize;
                    float back = iconPx * 1.25f;
                    if (hi) GUI.DrawTexture(new Rect(pos.x - back * 0.5f - 4f, pos.y - back * 0.5f - 4f, back + 8f, back + 8f), dotHighlightTex);
                    GUI.DrawTexture(new Rect(pos.x - back * 0.5f, pos.y - back * 0.5f, back, back), iconBackTex);
                    GUI.DrawTexture(new Rect(pos.x - iconPx * 0.5f, pos.y - iconPx * 0.5f, iconPx, iconPx), icon, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    float dotSize = hi ? 46f : 34f;
                    var dotTexture = hi ? dotHighlightTex : dotTex;
                    GUI.DrawTexture(new Rect(pos.x - dotSize * 0.5f, pos.y - dotSize * 0.5f, dotSize, dotSize), dotTexture);
                }
            }
        }

        void EnsureAssets()
        {
            if (ringTex != null) return;

            ringTex = MakeRing(256, 0.62f, 0.78f, new Color(0f, 0f, 0f, 0.55f));
            dotTex = MakeDot(64, new Color(1f, 1f, 1f, 0.5f));
            dotHighlightTex = MakeDot(64, new Color(1f, 0.85f, 0.4f, 0.95f));
            iconBackTex = MakeDot(128, new Color(0.08f, 0.08f, 0.1f, 0.78f));
        }

        // 가운데가 뚫린 도넛 모양 알파 텍스처 (innerFrac~outerFrac 사이만 칠한다, 0~1 = 텍스처 반지름 비율)
        static Texture2D MakeRing(int size, float innerFrac, float outerFrac, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            float inner = c * innerFrac, outer = c * outerFrac;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = 0f;
                    if (d >= inner && d <= outer)
                    {
                        float edge = Mathf.Min(d - inner, outer - d);
                        a = Mathf.Clamp01(edge / 3f);   // 안팎 가장자리만 살짝 부드럽게
                    }
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D MakeDot(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = Mathf.Clamp01((c - d) / 2.5f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            if (ringTex != null) Destroy(ringTex);
            if (dotTex != null) Destroy(dotTex);
            if (dotHighlightTex != null) Destroy(dotHighlightTex);
            if (iconBackTex != null) Destroy(iconBackTex);
        }
    }
}
