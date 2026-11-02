using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopDemo
{
    // 게임 안의 글자를 전부 LevelUp 폰트(Resources/OptionsDesignTheme.bodyFont)로 맞춘다 (2026-10-28).
    //  1) 화면 글자(OnGUI): 상호작용 문구, 진행 문구, 책·쪽지 읽기 화면, 기믹 안내 등은 대부분 GUI.skin.label을 바탕으로 만든
    //     스타일이라 폰트를 따로 정하지 않았다 → GUI.skin.font를 LevelUp으로 바꾸면 한 번에 바뀐다.
    //     LGH.PlayerInteractor.OnGUI(플레이 중 항상 있음)가 매 프레임 ApplyToGuiSkin()을 부른다.
    //     이미 폰트를 직접 정한 디자인 화면(옵션·로비·대기방·ESC 메뉴)은 영향을 받지 않는다.
    //  2) 물체에 붙은 3D 글자(TextMesh / TextMeshPro, Canvas 밖): 씬을 불러온 뒤 한 번 훑어서 폰트를 바꾼다.
    //     벽 너머로 비치지 않게 하는 깊이 검사 셰이더(KJH/Gim005/WorldText)를 쓰던 글자는 같은 셰이더로 LevelUp 글꼴 텍스처를 쓴다.
    public static class GameFont
    {
        const string DepthShaderName = "KJH/Gim005/WorldText";

        public static Font Body
        {
            get
            {
                var t = OptionsDesignTheme.Load();
                return t != null ? t.bodyFont : null;
            }
        }

        // OnGUI 안에서만 부를 것 (GUI.skin은 OnGUI 밖에서 쓸 수 없다)
        public static void ApplyToGuiSkin()
        {
            var f = Body;
            if (f != null && GUI.skin.font != f) GUI.skin.font = f;
        }

        // ---------------- 3D 글자 ----------------

        static bool hooked;
        static Material depthMat;
        static TMP_FontAsset tmpFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (hooked) { SceneManager.sceneLoaded -= OnSceneLoaded; Font.textureRebuilt -= OnFontRebuilt; }
            hooked = false;
            depthMat = null;
            tmpFont = null;
        }

        // 씬의 Awake가 모두 끝난 뒤 (다른 스크립트가 글자 머티리얼을 정한 뒤에 덮어써야 한다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
                Font.textureRebuilt += OnFontRebuilt;
            }
            ApplyToWorldText();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) { ApplyToWorldText(); }

        static void OnFontRebuilt(Font f)
        {
            if (depthMat != null && f == Body) depthMat.mainTexture = f.material.mainTexture;
        }

        public static void ApplyToWorldText()
        {
            var f = Body;
            if (f == null) return;

            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tm == null || tm.font == f) continue;
                tm.font = f;
                var r = tm.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    var old = r.sharedMaterial;
                    bool depth = old != null && old.shader != null && old.shader.name == DepthShaderName;
                    r.sharedMaterial = depth ? DepthMaterial(old, f) : f.material;
                }
                if (!string.IsNullOrEmpty(tm.text)) f.RequestCharactersInTexture(tm.text, tm.fontSize, tm.fontStyle);
            }

            var asset = TmpFont(f);
            if (asset == null) return;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.font == asset || t.GetComponentInParent<Canvas>(true) != null) continue;   // 화면 UI는 건드리지 않는다
                t.font = asset;
            }
        }

        static Material DepthMaterial(Material template, Font f)
        {
            if (depthMat == null) depthMat = new Material(template) { name = "GameFont WorldText (Runtime)" };
            depthMat.mainTexture = f.material.mainTexture;
            return depthMat;
        }

        static TMP_FontAsset TmpFont(Font f)
        {
            if (tmpFont == null)
            {
                try { tmpFont = TMP_FontAsset.CreateFontAsset(f); }
                catch (System.Exception e) { Debug.LogWarning("[GameFont] TextMeshPro용 LevelUp 글꼴을 만들지 못했습니다: " + e.Message); }
            }
            return tmpFont;
        }
    }
}
