using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 타이틀 화면 배경을 복도 그림 + 떠다니는 먼지로 바꾼다.
//   TitleRoot/Background   (기존 단색 바탕, 그대로 둔다)
//   TitleRoot/HallwayArt   (Title_Hallway.png, 화면을 꽉 채우게 비율 유지)
//   TitleRoot/TitleDust    (UIDustParticles)
//   TitleRoot/TitleArt     (Title_MainMenu_Overlay.png = 로고·버튼만 남기고 바탕을 투명하게 뽑은 그림)
// 재실행해도 같은 이름을 찾아 다시 맞춘다. 원래대로 되돌리려면 Revert 메뉴.
public static class TitleHallwayBuilder
{
    const string Dir = "Assets/Images/KJH/Title/";
    const string HallwayPath = Dir + "Title_Hallway.png";
    const string OverlayPath = Dir + "Title_MainMenu_Overlay.png";
    const string OriginalPath = Dir + "Title_MainMenu.png";

    [MenuItem("Tools/KJH/Title/Apply Hallway Background")]
    public static void Apply()
    {
        var hall = ImportSprite(HallwayPath, false);
        var overlay = ImportSprite(OverlayPath, true);
        if (hall == null || overlay == null) { Debug.LogError("[TitleHallway] 그림을 못 찾았습니다: " + HallwayPath + ", " + OverlayPath); return; }

        var root = FindTitleRoot();
        if (root == null) return;
        var art = root.Find("TitleArt");
        var bg = root.Find("Background");

        var hallRt = GetOrCreate(root, "HallwayArt");
        hallRt.SetSiblingIndex(bg != null ? bg.GetSiblingIndex() + 1 : 0);
        Stretch(hallRt);
        var img = GetOrAdd<Image>(hallRt.gameObject);
        img.sprite = hall;
        img.color = Color.white;
        img.raycastTarget = false;
        var fit = GetOrAdd<AspectRatioFitter>(hallRt.gameObject);
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = hall.rect.width / hall.rect.height;

        var dustRt = GetOrCreate(root, "TitleDust");
        dustRt.SetSiblingIndex(hallRt.GetSiblingIndex() + 1);
        Stretch(dustRt);
        var dust = GetOrAdd<UIDustParticles>(dustRt.gameObject);
        dust.color = new Color(1f, 0.93f, 0.82f, 1f);   // 촛불 빛을 받은 듯 살짝 따뜻한 흰색
        dust.raycastTarget = false;

        var artImg = art.GetComponent<Image>();
        Undo.RecordObject(artImg, "Title overlay");
        artImg.sprite = overlay;
        EditorUtility.SetDirty(artImg);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[TitleHallway] 타이틀 배경을 복도 그림 + 먼지로 바꿨습니다. 씬을 저장하세요.");
    }

    [MenuItem("Tools/KJH/Title/Revert To Plain Background")]
    public static void Revert()
    {
        var root = FindTitleRoot();
        if (root == null) return;
        foreach (var n in new[] { "HallwayArt", "TitleDust" })
        {
            var t = root.Find(n);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
        var artImg = root.Find("TitleArt").GetComponent<Image>();
        Undo.RecordObject(artImg, "Title original");
        artImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OriginalPath);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    static Transform FindTitleRoot()
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (c.name != "TitleCanvas") continue;
            var root = c.transform.Find("TitleRoot");
            if (root != null && root.Find("TitleArt") != null) return root;
        }
        Debug.LogError("[TitleHallway] 열린 씬에서 TitleCanvas/TitleRoot/TitleArt를 못 찾았습니다.");
        return null;
    }

    static Sprite ImportSprite(string path, bool alpha)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return null;
        bool changed = ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || ti.maxTextureSize < 2048 || ti.alphaIsTransparency != alpha;
        if (changed)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = alpha;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform GetOrCreate(Transform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (t != null) return t;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
