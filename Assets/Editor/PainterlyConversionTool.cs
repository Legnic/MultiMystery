using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Batch-converts the project's "Universal Render Pipeline/Lit" materials to the new
// Painterly shader pack (Assets/AssetPacks/PainterlyAsset). This project never used a
// metallic/AO texture workflow (every material is flat Metallic/Smoothness + an optional
// BaseMap), so the Painterly shader's packed textures degenerate to "reuse BaseMap as-is" -
// no new texture baking needed. Normal maps are dropped: PainterlyLitSimple perturbs the
// mesh normal itself (brush-stroke look) instead of sampling a normal map.
// Transparent (_Surface=1) and alpha-clip (_AlphaClip=1) materials are skipped - the pack's
// transparent variant needs a different packing (_BC_rgb_transparency_a / hand-painted mask)
// that isn't safe to auto-generate for only a couple of materials.
public static class PainterlyConversionTool
{
    const string SourceShaderName = "Universal Render Pipeline/Lit";
    const string TargetShaderName = "Shader Graphs/PainterlyLitSimple";

    [MenuItem("Tools/Painterly/Convert ALL Lit Materials")]
    public static void ConvertAll()
    {
        var guids = AssetDatabase.FindAssets("t:Material");
        Convert(guids, "Convert all");
    }

    // Lets us sanity-check a handful of materials before committing to the full 400+ pass.
    public static void ConvertPaths(IEnumerable<string> paths, string undoLabel)
    {
        var list = new List<string>();
        foreach (var p in paths)
        {
            var guid = AssetDatabase.AssetPathToGUID(p);
            if (!string.IsNullOrEmpty(guid)) list.Add(guid);
        }
        Convert(list, undoLabel);
    }

    static void Convert(IEnumerable<string> guids, string undoLabel)
    {
        var targetShader = Shader.Find(TargetShaderName);
        if (targetShader == null)
        {
            Debug.LogError("[Painterly] Shader not found: " + TargetShaderName);
            return;
        }

        int converted = 0, skippedTransparent = 0, skippedAlphaClip = 0, skippedNotLit = 0;
        var report = new StringBuilder();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (mat.shader.name != SourceShaderName) { skippedNotLit++; continue; }

            bool transparent = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
            bool alphaClip = mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f;
            if (transparent) { skippedTransparent++; report.AppendLine("SKIP(transparent): " + path); continue; }
            if (alphaClip) { skippedAlphaClip++; report.AppendLine("SKIP(alphaClip): " + path); continue; }

            var baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            var baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
            bool hadNormalMap = mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
            float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;

            Undo.RecordObject(mat, undoLabel);
            mat.shader = targetShader;

            mat.SetColor("_BaseColor", baseColor);
            mat.SetTexture("_BC_rgb_smoothness_a", baseMap); // RGB reused as-is, alpha (usually opaque=1) x _Smoothness below
            mat.SetTexture("_AO_r_metalic_g", null);          // null samples white -> AO stays 1, metallic = _Metalic below
            mat.SetFloat("_Metalic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            // Painterly-only properties: demo-pack defaults, no material in this project has
            // ever set these, so they'd otherwise be 0 (which breaks the stroke effect).
            mat.SetColor("_VariationColorA", new Color(0f, 0f, 0f, 0f));
            mat.SetColor("_VariationColorB", new Color(1f, 1f, 1f, 0f));
            mat.SetFloat("_StrokeScale", 4f);
            mat.SetFloat("_StrokeRandomness", 1f);
            mat.SetFloat("_StrokeBlendingNoiseScale", 500f);
            mat.SetFloat("_StrokeBlendingNoiseStrength", 0.242f);
            mat.SetFloat("_UseBlendingNoise", 1f);
            mat.SetFloat("_Variation", 0.1f);

            EditorUtility.SetDirty(mat);
            converted++;
            if (hadNormalMap) report.AppendLine("converted (normal map dropped): " + path);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Painterly] converted={converted} skippedTransparent={skippedTransparent} skippedAlphaClip={skippedAlphaClip} skippedNotLit={skippedNotLit}");
        if (report.Length > 0) Debug.Log("[Painterly]\n" + report);
    }
}
