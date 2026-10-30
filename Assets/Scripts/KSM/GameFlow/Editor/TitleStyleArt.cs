using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo.EditorTools
{
    // 오프닝/엔딩 스킵 버튼과 결과 화면을 "지금 타이틀 화면이 쓰는 이미지"로 꾸미는 도구 (2026-10-30).
    //
    // 타이틀은 Assets/Images/KJH/Title/Title_MainMenu.png 한 장에 로고와 버튼 틀(시작/옵션/나가기, 글자까지 박힘)이 같이 들어 있고,
    // 그 위에 투명한 버튼 + Title_ButtonHoverOutline(호버 때만 보이는 테두리)을 얹는 구조다. 그래서 여기서는
    //  - 결과 배경: Title_MainMenu에서 버튼 3개 자리만 배경으로 지운 그림 (로고는 그대로)
    //  - 빈 버튼 틀: 첫 버튼("시작")에서 글자만 지운 그림 (스킵/타이틀로 버튼 글자를 직접 얹기 위함)
    // 을 만들어 쓰고, 버튼 동작·호버는 타이틀의 OptionsButton을 그대로 복제해서 똑같이 만든다.
    // (KSM/Title_Logo.png, KSM/MeetYou_UI_Resources_Individual 은 쓰지 않는다.)
    public static class TitleStyleArt
    {
        const string SourcePath = "Assets/Images/KJH/Title/Title_MainMenu.png";
        const string OutDir = "Assets/Images/KSM/TitleDerived";
        const string BackgroundPath = OutDir + "/Result_Background.png";
        const string ButtonPath = OutDir + "/Title_ButtonBlank.png";

        // Title_MainMenu.png(1536x864)에서 측정한 영역 (위에서부터 센 행). 로고 149~388, 버튼 3개 467~547 / 573~652 / 679~757.
        const int ButtonRowTop = 467, ButtonRowBottom = 547;
        const int EraseFromRow = 456, EraseToRow = 776;      // 버튼 3개 + 여백을 지운다
        const int BandTop = 395, BandBottom = 455;           // 로고와 버튼 사이의 빈 띠 (지운 자리를 이걸로 채운다)

        static readonly Color Gold = new Color(0.96f, 0.84f, 0.58f, 1f);

        // ------------------------------------------------------------------
        // 이미지 만들기
        // ------------------------------------------------------------------

        public static void EnsureArt(out Sprite background, out Sprite blankButton)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath) == null || AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPath) == null)
                GenerateArt();
            background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            blankButton = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPath);
        }

        [MenuItem("Tools/KSM/GameFlow/Regenerate Title-Derived Art")]
        public static void GenerateArt()
        {
            var bytes = System.IO.File.ReadAllBytes(SourcePath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, bytes);
            int W = tex.width, H = tex.height;
            var src = tex.GetPixels32();
            Color32 At(int x, int rowTop) { return src[(H - 1 - rowTop) * W + x]; }   // rowTop: 위에서부터 센 행

            // ---- 결과 배경: 버튼 자리를 빈 띠로 덮는다(거울처럼 번갈아 이어 붙여 이음매를 감춘다) ----
            var bg = (Color32[])src.Clone();
            int bandLen = BandBottom - BandTop + 1;
            for (int row = EraseFromRow; row <= EraseToRow; row++)
            {
                int k = (row - EraseFromRow) % (bandLen * 2);
                int srcRow = k < bandLen ? BandBottom - k : BandTop + (k - bandLen);
                for (int x = 0; x < W; x++) bg[(H - 1 - row) * W + x] = At(x, srcRow);
            }
            WritePng(BackgroundPath, bg, W, H);

            // ---- 빈 버튼 틀: 첫 버튼을 잘라 내고 글자만 지운다 ----
            int midRow = (ButtonRowTop + ButtonRowBottom) / 2;
            int bx0 = W, bx1 = -1;
            for (int x = 0; x < W; x++) { var p = At(x, midRow); if ((p.r + p.g + p.b) / 3 > 70) { if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; } }
            int bw = bx1 - bx0 + 1, bh = ButtonRowBottom - ButtonRowTop + 1;
            var btn = new Color32[bw * bh];
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++) btn[(bh - 1 - y) * bw + x] = At(bx0 + x, ButtonRowTop + y);   // 아래가 0번 행
            // 글자 영역: 장식 모서리를 피해 안쪽(가로 12%~88%, 세로 20%~80%)에서 밝은 글자 픽셀의 범위를 찾는다
            int tx0 = bw, tx1 = -1, ty0 = bh, ty1 = -1;
            for (int y = (int)(bh * 0.2f); y < (int)(bh * 0.8f); y++)
                for (int x = (int)(bw * 0.12f); x < (int)(bw * 0.88f); x++)
                {
                    var p = btn[(bh - 1 - y) * bw + x];
                    if ((p.r + p.g + p.b) / 3 > 150) { if (x < tx0) tx0 = x; if (x > tx1) tx1 = x; if (y < ty0) ty0 = y; if (y > ty1) ty1 = y; }
                }
            if (tx1 >= 0)
            {
                int pad = 8;
                int ex0 = Mathf.Max(0, tx0 - pad), ex1 = Mathf.Min(bw - 1, tx1 + pad);
                int ey0 = Mathf.Max(0, ty0 - pad), ey1 = Mathf.Min(bh - 1, ty1 + pad);
                int width = ex1 - ex0 + 1;
                for (int y = ey0; y <= ey1; y++)
                    for (int x = ex0; x <= ex1; x++)
                    {
                        // 글자 왼쪽의 빈 안쪽 면을 같은 높이에서 그대로 복사한다 (글자와 겹치지 않게 폭만큼 왼쪽)
                        int sx = x - width - 6;
                        if (sx < (int)(bw * 0.1f)) sx = x + width + 6;   // 왼쪽이 모자라면 오른쪽에서
                        btn[(bh - 1 - y) * bw + x] = btn[(bh - 1 - y) * bw + sx];
                    }
            }
            WritePng(ButtonPath, btn, bw, bh);
            Debug.Log($"[TitleStyleArt] 만들었습니다: 배경 {W}x{H}, 빈 버튼 {bw}x{bh} (글자 영역 {tx0}..{tx1}, {ty0}..{ty1})");
        }

        static void WritePng(string path, Color32[] pixels, int w, int h)
        {
            System.IO.Directory.CreateDirectory(OutDir);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(pixels); t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var srcImp = (TextureImporter)AssetImporter.GetAtPath(SourcePath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            if (srcImp != null) { imp.maxTextureSize = srcImp.maxTextureSize; imp.textureCompression = srcImp.textureCompression; imp.sRGBTexture = srcImp.sRGBTexture; }
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------
        // 타이틀 버튼 복제 (호버 테두리 포함)
        // ------------------------------------------------------------------

        static Transform FindTitleButtonSource()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.scene.IsValid() && go.transform.parent == null && go.name == "TitleCanvas")
                {
                    var t = go.transform.Find("TitleRoot/TitleArt/OptionsButton");
                    if (t != null) return t;
                }
            return null;
        }

        // 타이틀 OptionsButton(투명 버튼 + HoverOutline)을 복제하고, 그 뒤에 빈 버튼 틀 그림과 글자를 얹는다.
        // 복제본의 클릭 연결(타이틀 옵션 열기)은 지운다. 호버/눌림은 타이틀과 똑같이 동작한다.
        public static GameObject CreateButton(Transform parent, string name, string label, int fontSize)
        {
            var source = FindTitleButtonSource();
            if (source == null) { Debug.LogError("[TitleStyleArt] 타이틀의 OptionsButton을 못 찾았습니다(TitleCanvas/TitleRoot/TitleArt/OptionsButton)."); return null; }
            EnsureArt(out _, out var blank);

            var go = Object.Instantiate(source.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();   // 복제된 "옵션 열기" 연결 제거
            btn.interactable = true;

            // 빈 버튼 틀: 맨 뒤(호버 테두리 뒤)에 깐다
            var artGo = new GameObject("ButtonArt", typeof(RectTransform), typeof(Image));
            var artRt = (RectTransform)artGo.transform;
            artRt.SetParent(go.transform, false);
            artRt.anchorMin = Vector2.zero; artRt.anchorMax = Vector2.one; artRt.offsetMin = Vector2.zero; artRt.offsetMax = Vector2.zero;
            var img = artGo.GetComponent<Image>();
            img.sprite = blank; img.raycastTarget = false; img.preserveAspect = false;
            artGo.transform.SetAsFirstSibling();

            // 글자 (타이틀 버튼의 꺼져 있던 Label을 켜서 쓴다)
            var labelT = go.transform.Find("Label");
            var text = labelT.GetComponent<Text>();
            labelT.gameObject.SetActive(true);
            text.text = label; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold; text.color = Gold;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            if (GameFont.Body != null) text.font = GameFont.Body;
            if (text.GetComponent<Shadow>() == null) text.gameObject.AddComponent<Shadow>();
            return go;
        }

        // ------------------------------------------------------------------
        // 결과 화면: 배경(타이틀 그림 속 로고 포함) + 클리어 시간 + 타이틀로 돌아가기 버튼
        // ------------------------------------------------------------------

        public static void BuildResultContents(RectTransform canvasRect, out Text clearTime, out Button titleButton)
        {
            EnsureArt(out var bgSprite, out _);

            // 바탕: 타이틀과 같은 남색 + 가운데 그림(타이틀과 같은 AspectRatioFitter 방식)
            var back = canvasRect.Find("Background");
            if (back != null) back.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);

            var artGo = new GameObject("ResultArt", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            var artRt = (RectTransform)artGo.transform;
            artRt.SetParent(canvasRect, false);
            artRt.anchorMin = Vector2.zero; artRt.anchorMax = Vector2.one; artRt.offsetMin = Vector2.zero; artRt.offsetMax = Vector2.zero;
            var artImg = artGo.GetComponent<Image>(); artImg.sprite = bgSprite; artImg.raycastTarget = false;
            var fitter = artGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1536f / 864f;
            artGo.transform.SetSiblingIndex(back != null ? back.GetSiblingIndex() + 1 : 0);

            // 클리어 시간: 타이틀의 "시작" 버튼이 있던 줄
            var timeGo = new GameObject("ClearTimeText", typeof(RectTransform), typeof(Text), typeof(Shadow));
            var timeRt = (RectTransform)timeGo.transform;
            timeRt.SetParent(artRt, false);
            timeRt.anchorMin = new Vector2(0.2f, 0.37f); timeRt.anchorMax = new Vector2(0.8f, 0.46f);
            timeRt.offsetMin = Vector2.zero; timeRt.offsetMax = Vector2.zero;
            clearTime = timeGo.GetComponent<Text>();
            clearTime.text = "클리어 시간 --:--:--"; clearTime.fontSize = 46; clearTime.fontStyle = FontStyle.Bold; clearTime.color = Gold;
            clearTime.alignment = TextAnchor.MiddleCenter; clearTime.raycastTarget = false;
            clearTime.horizontalOverflow = HorizontalWrapMode.Overflow; clearTime.verticalOverflow = VerticalWrapMode.Overflow;
            clearTime.font = GameFont.Body != null ? GameFont.Body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 타이틀로 돌아가기: 타이틀의 "옵션" 버튼이 있던 줄·크기
            var btnGo = CreateButton(artRt, "TitleButton", "타이틀로 돌아가기", 38);
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = new Vector2(0.33f, 0.24f); btnRt.anchorMax = new Vector2(0.67f, 0.34f);
            btnRt.offsetMin = Vector2.zero; btnRt.offsetMax = Vector2.zero;
            titleButton = btnGo.GetComponent<Button>();
        }

        // ------------------------------------------------------------------
        // 이미 만들어진 씬에 적용 (GameFlow Build All을 다시 돌리지 않고 지금 씬을 바로 바꾼다)
        // ------------------------------------------------------------------

        static GameObject FindRoot(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.scene.IsValid() && go.transform.parent == null && go.name == name) return go;
            return null;
        }

        [MenuItem("Tools/KSM/GameFlow/Apply Title Art To Skip And Result")]
        public static void ApplyToScene()
        {
            // 스킵 버튼
            foreach (var cn in new[] { "IntroCanvas", "EndingCanvas" })
            {
                var canvas = FindRoot(cn);
                if (canvas == null) { Debug.LogWarning("[TitleStyleArt] " + cn + " 없음"); continue; }
                var old = canvas.transform.Find("PlaybackRoot/SkipButton");
                var parent = canvas.transform.Find("PlaybackRoot");
                var go = CreateButton(parent, "SkipButton", "스킵", 32);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(1, 0);
                rt.sizeDelta = new Vector2(320, 51); rt.anchoredPosition = new Vector2(-32, 32);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var ctrl = canvas.GetComponent<IllustratedCutsceneController>();
                var so = new SerializedObject(ctrl); so.FindProperty("skipButton").objectReferenceValue = go.GetComponent<Button>(); so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 결과 화면: Background만 남기고 다시 만든다
            var result = FindRoot("ResultCanvas");
            if (result != null)
            {
                var rect = (RectTransform)result.transform;
                foreach (var child in rect.Cast<Transform>().Where(c => c.name != "Background").ToList()) Object.DestroyImmediate(child.gameObject);
                BuildResultContents(rect, out var clearTime, out var titleButton);
                var ctrl = result.GetComponent<ResultCanvasController>();
                var so = new SerializedObject(ctrl);
                so.FindProperty("clearTimeText").objectReferenceValue = clearTime;
                so.FindProperty("titleButton").objectReferenceValue = titleButton;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[TitleStyleArt] 스킵 버튼 2개와 결과 화면에 타이틀 이미지를 적용했습니다. 씬을 저장하세요.");
        }
    }
}
