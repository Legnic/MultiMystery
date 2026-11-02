using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AtticTreasure.EditorTools
{
    // 황금상 획득 연출(AtticTreasureGlowCutscene)을 AtticTreasureSync.prefab에 연결한다. 몇 번을 다시 실행해도 된다.
    //  - Materials/M_TreasureGlowAdditive.mat (URP Particles/Unlit, 더하기 섞기) — 없을 때만 만든다.
    //    빌드에 셰이더가 포함되도록 재질을 에셋으로 두고 연출 컴포넌트가 참조한다.
    //  - 프리팹에 AtticTreasureGlowCutscene를 붙이고 AtticTreasureSync.cutscene 칸에 연결, 임시 연출 컴포넌트는 뗀다.
    //    (연출 수치는 이미 붙어 있으면 그대로 둔다 → 튜닝 값 보존)
    // Build All도 끝에 이것을 부른다.
    public static class AtticTreasureCutsceneBuilder
    {
        const string SyncPath = "Assets/Prefabs/KJH/AtticTreasure/AtticTreasureSync.prefab";
        const string MatPath = "Assets/Prefabs/KJH/AtticTreasure/Materials/M_TreasureGlowAdditive.mat";

        [MenuItem("Tools/KJH/AtticTreasure/Build Glow Cutscene")]
        public static void Build()
        {
            var mat = AdditiveMaterial();
            var root = PrefabUtility.LoadPrefabContents(SyncPath);
            try
            {
                var glow = root.GetComponent<AtticTreasureGlowCutscene>();
                if (glow == null) glow = root.AddComponent<AtticTreasureGlowCutscene>();
                var gso = new SerializedObject(glow);
                gso.FindProperty("additiveMaterial").objectReferenceValue = mat;
                gso.ApplyModifiedPropertiesWithoutUndo();

                foreach (var p in root.GetComponents<AtticTreasurePlaceholderCutscene>()) Object.DestroyImmediate(p, true);

                var sync = root.GetComponent<AtticTreasureSync>();
                var so = new SerializedObject(sync);
                so.FindProperty("cutscene").objectReferenceValue = glow;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, SyncPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("[AtticTreasure] 황금상 획득 연출(AtticTreasureGlowCutscene)을 " + SyncPath + "에 연결했습니다.");
        }

        static Material AdditiveMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            mat = new Material(shader) { name = "M_TreasureGlowAdditive" };
            mat.SetFloat("_Surface", 1f);          // 반투명
            mat.SetFloat("_Blend", 2f);            // 더하기
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            mat.SetColor("_BaseColor", Color.white);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
