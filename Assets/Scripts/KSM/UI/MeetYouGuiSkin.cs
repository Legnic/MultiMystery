using UnityEngine;

namespace CoopDemo
{
    // MeetYouUiTheme 이미지로 만든 IMGUI 스킨. OnGUI 안에서 Begin()으로 켜고 End()로 원래대로 돌려놓는다.
    //
    //   var prev = MeetYouGuiSkin.Begin();
    //   try { ...GUI 그리기... } finally { MeetYouGuiSkin.End(prev); }
    //
    // 스킨을 켠 뒤에 `new GUIStyle(GUI.skin.label)` 처럼 만든 스타일은 이 스킨의 모양을 물려받는다.
    // 이미지는 "1:1 픽셀"로 그려지는 9-slice라서, 쓰는 크기에 맞게 한 번 줄여서 만든다(UiScale이 화면에 맞게 키워 준다).
    public static class MeetYouGuiSkin
    {
        public static Color Parchment => GuiColor(new Color(0.93f, 0.86f, 0.68f));
        public static Color Gold => GuiColor(new Color(0.98f, 0.84f, 0.5f));

        // IMGUI(OnGUI) 글자색·GUI.color 틴트 보정.
        // 이 프로젝트는 Linear 색 공간인데, 빌드(플레이어)에서는 IMGUI 색에 sRGB→Linear 변환이 한 번 더 걸려
        // 지정한 색보다 어둡고 진하게(노랗게) 나온다 — 에디터 게임 뷰는 지정한 색 그대로 나온다.
        // (2026-10-28 실측: 크림색 245,230,194 → 빌드 232,201,137 = 정확히 리니어 변환값)
        // 그래서 빌드에서만 반대로 감마 쪽으로 한 번 올려서 넘긴다. 흰색·검은색과 알파는 그대로다.
        public static Color GuiColor(Color c)
        {
            if (Application.isEditor || QualitySettings.activeColorSpace != ColorSpace.Linear) return c;
            return c.gamma;
        }

        static GUISkin skin;
        static GUIStyle window, inner;
        static bool builtOnce;

        // 스킨이 새로 만들어질 때마다 올라간다. 이 스킨에서 파생한 GUIStyle을 캐시해 두는 쪽은
        // 값이 바뀌면 스타일을 다시 만들어야 한다 (Play 시 씬을 다시 로드하지 않는 설정이라, 컴포넌트가 이전 실행에서 만든
        // 스타일 — 이미 파괴된 텍스처를 가리키는 — 을 그대로 들고 있을 수 있다).
        public static int Version { get; private set; }

        // 테마 에셋 (Resources/MeetYouUiTheme). 없으면 null — 쓰는 쪽은 기본 모양으로 대신 그린다.
        static MeetYouUiTheme theme;
        public static MeetYouUiTheme Theme
        {
            get
            {
                if (theme == null) theme = Resources.Load<MeetYouUiTheme>("MeetYouUiTheme");
                return theme;
            }
        }

        // 큰 창 배경 (GUI.Box(rect, GUIContent.none, MeetYouGuiSkin.Window))
        public static GUIStyle Window { get { return window ?? GUI.skin.box; } }
        // 창 안쪽 영역
        public static GUIStyle Inner { get { return inner ?? GUI.skin.box; } }

        public static GUISkin Begin()
        {
            var prev = GUI.skin;
            var s = Build();
            if (s != null) GUI.skin = s;
            return prev;
        }

        public static void End(GUISkin prev)
        {
            if (prev != null) GUI.skin = prev;
        }

        static GUISkin Build()
        {
            if (skin != null) return skin;
            if (builtOnce) return null;   // 테마가 없거나 실패했으면 매번 다시 시도하지 않는다
            builtOnce = true;

            var theme = Resources.Load<MeetYouUiTheme>("MeetYouUiTheme");
            if (theme == null) return null;

            // Object.Instantiate(GUI.skin)로 복제하면 스타일이 초기화되지 않은 채로 남는 경우가 있어서,
            // 새 스킨을 만들고 기본 스킨의 스타일을 하나씩 복사해 쓴다.
            skin = CopyDefaultSkin(GUI.skin);
            skin.name = "MeetYouGuiSkin";
            Version++;

            // 글자색
            skin.label.normal.textColor = Parchment;
            skin.toggle.normal.textColor = Parchment; skin.toggle.onNormal.textColor = Parchment;
            skin.toggle.hover.textColor = Color.white; skin.toggle.onHover.textColor = Color.white;
            skin.toggle.active.textColor = Color.white; skin.toggle.onActive.textColor = Color.white;

            // 창 / 안쪽 영역
            window = new GUIStyle(skin.box) { border = new RectOffset(34, 34, 34, 34), padding = new RectOffset(16, 16, 16, 16) };
            SetBg(window, Scaled(theme.windowPanel, 400), null);
            inner = new GUIStyle(skin.box) { border = new RectOffset(16, 16, 16, 16), padding = new RectOffset(12, 12, 10, 10) };
            SetBg(inner, Scaled(theme.innerPanel, 200), null);
            skin.box = inner;

            // 버튼
            var n = Scaled(theme.buttonNormal, 250);
            var h = Scaled(theme.buttonHover, 250);
            var p = Scaled(theme.buttonPressed, 250);
            if (n != null)
            {
                var b = skin.button;
                b.border = new RectOffset(18, 18, 14, 14);
                b.padding = new RectOffset(14, 14, 6, 6);
                b.alignment = TextAnchor.MiddleCenter;
                b.normal.background = n; b.hover.background = h != null ? h : n; b.active.background = p != null ? p : n;
                b.focused.background = n;
                var sel = Scaled(theme.buttonSelected, 250);
                if (sel == null) sel = p != null ? p : n;
                b.onNormal.background = sel; b.onHover.background = sel; b.onActive.background = sel;
                b.normal.textColor = Gold; b.hover.textColor = Color.white; b.active.textColor = Color.white;
                b.onNormal.textColor = Color.white; b.onHover.textColor = Color.white; b.onActive.textColor = Color.white;
                b.focused.textColor = Gold;
            }

            // 글자 입력칸
            var f = Scaled(theme.fieldPanel, 220);
            if (f != null)
            {
                var t = skin.textField;
                t.border = new RectOffset(14, 14, 12, 12);
                t.padding = new RectOffset(12, 12, 6, 6);
                SetBg(t, f, f);
                t.normal.textColor = Parchment; t.focused.textColor = Color.white; t.hover.textColor = Parchment;
            }

            // 슬라이더 (가는 막대 + 금색 마름모)
            var bar = Scaled(theme.sliderBar, 160);
            var thumb = Brighten(Resize(theme.sliderThumb, 30, 30), 1.35f);
            if (bar != null && thumb != null)
            {
                var sl = skin.horizontalSlider;
                sl.border = new RectOffset(10, 10, 0, 0);
                sl.fixedHeight = 16;
                sl.margin = new RectOffset(4, 4, 8, 8);
                SetBg(sl, SliderTrack(64, 16), null);
                var th = skin.horizontalSliderThumb;
                th.fixedWidth = 30; th.fixedHeight = 16; th.overflow = new RectOffset(0, 0, 7, 7);
                th.border = new RectOffset(0, 0, 0, 0);
                th.normal.background = th.hover.background = th.active.background = th.focused.background = thumb;
            }

            // 체크박스: 왼쪽에 칸(+체크), 오른쪽은 글자 자리(투명)
            var box = Resize(theme.toggleBox, 26, 26);
            var checkedBox = Composite(theme.toggleBox, theme.checkIcon, 26);
            if (box != null)
            {
                var tg = skin.toggle;
                tg.border = new RectOffset(26, 0, 0, 0);
                tg.padding = new RectOffset(34, 0, 3, 0);
                tg.overflow = new RectOffset(0, 0, -1, 0);
                var off = Pad(box, 64, 28);
                var on = Pad(checkedBox != null ? checkedBox : box, 64, 28);
                tg.normal.background = tg.hover.background = tg.active.background = tg.focused.background = off;
                tg.onNormal.background = tg.onHover.background = tg.onActive.background = tg.onFocused.background = on;
            }

            return skin;
        }

        static GUISkin CopyDefaultSkin(GUISkin def)
        {
            var s = ScriptableObject.CreateInstance<GUISkin>();
            s.hideFlags = HideFlags.HideAndDontSave;
            s.font = def.font;
            foreach (var p in typeof(GUISkin).GetProperties())
            {
                if (p.PropertyType != typeof(GUIStyle) || !p.CanRead || !p.CanWrite) continue;
                var src = (GUIStyle)p.GetValue(def);
                if (src != null) p.SetValue(s, new GUIStyle(src));
            }
            s.customStyles = new GUIStyle[def.customStyles.Length];
            for (int i = 0; i < def.customStyles.Length; i++) s.customStyles[i] = new GUIStyle(def.customStyles[i]);
            return s;
        }

        static void SetBg(GUIStyle s, Texture2D normal, Texture2D hover)
        {
            if (normal == null) return;
            s.normal.background = normal;
            s.hover.background = hover != null ? hover : normal;
            s.active.background = hover != null ? hover : normal;
            s.focused.background = hover != null ? hover : normal;
        }

        // 가로 폭 width에 맞춰 비율대로 줄인 복사본
        static Texture2D Scaled(Texture2D src, int width)
        {
            if (src == null) return null;
            int h = Mathf.Max(1, Mathf.RoundToInt(src.height * (width / (float)src.width)));
            return Resize(src, width, h);
        }

        static Texture2D Resize(Texture2D src, int w, int h)
        {
            if (src == null) return null;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // 금색 테두리의 둥근 가는 막대 (작은 크기로 줄인 이미지는 형태가 뭉개져서 직접 그린다)
        static Texture2D SliderTrack(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            var edge = new Color(0.85f, 0.62f, 0.28f, 1f);
            var fill = new Color(0.05f, 0.09f, 0.12f, 1f);
            float r = h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 가운데 직사각형 + 양끝 반원까지의 거리로 안/밖/테두리를 정한다
                    float cx = Mathf.Clamp(x + 0.5f, r, w - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - r) * (y + 0.5f - r));
                    float inside = r - d;   // 0 이하면 밖
                    Color c = inside <= 0f ? Color.clear : (inside < 2f ? edge : fill);
                    if (inside > 0f && inside < 1f) c.a = inside;   // 가장자리 부드럽게
                    px[y * w + x] = c;
                }
            t.SetPixels(px);
            t.Apply(false, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // 줄이는 과정에서 어두워진 가는 선(금색 테두리 등)을 보이게 밝힌다 (알파는 그대로)
        static Texture2D Brighten(Texture2D t, float k)
        {
            if (t == null) return null;
            var px = t.GetPixels();
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(Mathf.Clamp01(px[i].r * k), Mathf.Clamp01(px[i].g * k), Mathf.Clamp01(px[i].b * k), Mathf.Clamp01(px[i].a * 1.2f));
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        // 칸 위에 체크 아이콘을 얹은 이미지
        static Texture2D Composite(Texture2D boxSrc, Texture2D iconSrc, int size)
        {
            if (boxSrc == null || iconSrc == null) return null;
            var baseTex = Resize(boxSrc, size, size);
            int iconSize = Mathf.RoundToInt(size * 0.78f);
            var icon = Resize(iconSrc, iconSize, iconSize);
            int off = (size - iconSize) / 2;
            var dst = baseTex.GetPixels();
            var ip = icon.GetPixels();
            for (int y = 0; y < iconSize; y++)
                for (int x = 0; x < iconSize; x++)
                {
                    var c = ip[y * iconSize + x];
                    int di = (y + off) * size + (x + off);
                    dst[di] = Color.Lerp(dst[di], new Color(c.r, c.g, c.b, 1f), c.a);
                }
            baseTex.SetPixels(dst);
            baseTex.Apply(false, false);
            return baseTex;
        }

        // 왼쪽 위에 이미지를 두고 나머지는 투명한 w×h 이미지
        static Texture2D Pad(Texture2D src, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color[w * h];
            t.SetPixels(clear);
            int oy = (h - src.height) / 2;
            t.SetPixels(0, oy, src.width, src.height, src.GetPixels());
            t.Apply(false, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

#if UNITY_EDITOR
        // 도메인 리로드를 끄고 Play할 때 이전 실행의 스킨이 남지 않게 한다 (파괴된 텍스처 참조 방지)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            skin = null; window = null; inner = null; builtOnce = false; theme = null;
        }
#endif
    }
}
