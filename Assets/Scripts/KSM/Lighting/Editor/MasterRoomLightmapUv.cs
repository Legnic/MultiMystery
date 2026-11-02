using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopDemo.EditorTools
{
    // 베이크 전 준비: Contribute GI가 켜진 메시 중 라이트맵용 UV(uv2)가 없는 것을 찾아, "사본 메시 + 자동 uv2"로 씬 참조만 바꾼다.
    //  - 원본 에셋(LGH_UVBoxes, SecretDress_*, VARCO FBX 등)은 수정하지 않는다 → 다른 팀원 폴더와 충돌하지 않는다.
    //  - 사본은 Assets/Settings/KSM/MasterRoom/Meshes 에 저장된다(원본 이름 + 원본 에셋 GUID 일부로 중복을 피함).
    //  - 같은 원본 메시는 사본 하나를 공유한다. 이미 uv2가 있는 메시(FBX의 generateSecondaryUV 포함)는 건드리지 않는다.
    // 재실행해도 안전하다(이미 사본이면 uv2가 있어 건너뜀).
    public static class MasterRoomLightmapUv
    {
        const string MeshDir = "Assets/Settings/KSM/MasterRoom/Meshes";

        [MenuItem("Tools/KSM/Lighting/Master Room - Add Lightmap UVs")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Develop.unity") { Debug.LogError("[MasterLighting] Develop 씬에서만 실행합니다."); return; }
            Directory(MeshDir);

            var made = new Dictionary<Mesh, Mesh>();
            int replaced = 0, skipped = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if ((GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) == 0) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                if (HasLightmapUv(mesh)) { skipped++; continue; }

                if (!made.TryGetValue(mesh, out var copy))
                {
                    copy = Object.Instantiate(mesh);
                    copy.name = mesh.name + "_lm";
                    var param = new UnwrapParam();
                    UnwrapParam.SetDefaults(out param);
                    Unwrapping.GenerateSecondaryUVSet(copy, param);
                    string src = AssetDatabase.GetAssetPath(mesh);
                    string tag = string.IsNullOrEmpty(src) ? "scene" : AssetDatabase.AssetPathToGUID(src).Substring(0, 6);
                    string path = AssetDatabase.GenerateUniqueAssetPath($"{MeshDir}/{Sanitize(mesh.name)}_{tag}_lm.asset");
                    AssetDatabase.CreateAsset(copy, path);
                    made[mesh] = copy;
                }
                Undo.RecordObject(mf, "Lightmap UV mesh");
                mf.sharedMesh = copy;
                EditorUtility.SetDirty(mf);
                if (PrefabUtility.IsPartOfPrefabInstance(mf)) PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                replaced++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MasterLighting] 라이트맵 UV 사본 메시 {made.Count}개 생성, 씬 렌더러 {replaced}개 교체, 이미 uv2 있어 건너뜀 {skipped}개. 원본 에셋은 수정하지 않았습니다.");
        }

        static bool HasLightmapUv(Mesh mesh)
        {
            if (mesh.uv2 != null && mesh.uv2.Length > 0) return true;
            var path = AssetDatabase.GetAssetPath(mesh);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            return importer != null && importer.generateSecondaryUV;   // FBX가 임포트 때 만들어 주는 경우
        }

        static string Sanitize(string s)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        static void Directory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            Directory(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
