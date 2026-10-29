using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FloorMap
{
    // 층별 지도 액자 프리팹 2개(1층·2층)를 만들고, 열린 씬에 정해진 자리로 배치한다.
    //  Tools/KJH/FloorMap/Build Prefabs     : 그림 가져오기 설정 + 머티리얼 + 프리팹 (재실행 가능, 덮어씀)
    //  Tools/KJH/FloorMap/Place In Open Scene : 활성 씬에 두 액자를 배치 (같은 이름이 있으면 지우고 다시 놓음)
    //
    // 액자는 VARCO Prop_PortraitFrame(세로 0.73×1.0m)을 원래 방향(위쪽 걸이 문양이 위)으로 세우고,
    // 가로로 더 늘려(FrameScaleWidth) 가로형 지도가 들어가게 한다. 앞면 회색 그림 자리(원본 0.54×0.70m)에 지도 Quad를 붙인다.
    // 그림 자리 위치는 모델 메시를 직접 레이캐스트해 잰 값이다(앞면 = 메시 -Y, 깊이 -0.027, 메시 Z = 위쪽).
    public static class FloorMapBuilder
    {
        const string FramePrefabPath = "Assets/VARCO 3D/KWJ/Prefabs/Gimmick/Prop_PortraitFrame.prefab";
        const string ImageDir = "Assets/Images/KJH/FloorMap";
        const string PrefabDir = "Assets/Prefabs/KJH/FloorMap";
        const string MaterialDir = PrefabDir + "/Materials";

        // 액자 크기 배율 (원본 폭 0.732 × 높이 1.002m). 그림 자리 가로:세로 = 0.544×폭배율 : 0.702×높이배율
        // 지도 그림 비율(0.700:0.540 = 1.296)에 맞추려면 폭배율 = 높이배율 × 1.672
        const float FrameScaleHeight = 1.1f;
        const float FrameScaleWidth = FrameScaleHeight * 1.672f;
        const float FrameScaleDepth = 1.1f;
        // 메시 기준 치수
        const float MeshBackY = 0.051f;        // 뒷면 (벽에 닿는 면)
        const float CanvasFrontY = -0.0285f;   // 앞면 그림 자리 바로 앞 (1.5mm 띄움)
        const float CanvasCenterZ = -0.043f;   // 그림 자리 가운데 높이 (메시 Z = 위쪽)
        const float CanvasHeight = 0.700f;     // 그림 자리 세로 (메시 Z, 테두리 안쪽 여유 포함)
        const float CanvasWidth = 0.540f;      // 그림 자리 가로 (메시 X)
        const float FrameHeight = 1.002f, FrameWidth = 0.732f, FrameDepth = 0.102f;

        // 지도 그림이 조금 보이도록 하는 발광 세기 (어두운 저택에서도 읽히게)
        const float Emission = 0.1f;

        struct Spot { public string floor; public Vector3 wallPoint; public Vector3 outward; }

        // 배치 자리: 벽 앞면 위의 점 + 방 쪽 방향. 높이는 그 층 바닥 + 1.65m (액자 가운데)
        static readonly Spot[] Spots =
        {
            // 1층 현관 홀, 갤러리와 맞닿은 벽(앞면 x=47.875). 갤러리 쪽 문(z 13.2~)과 겹치지 않는 자리
            new Spot { floor = "1F", wallPoint = new Vector3(47.875f, 0f + 1.65f, 11.8f), outward = Vector3.left },
            // 2층 계단 홀, 드레스룸과 맞닿은 벽(앞면 x=40.125). 남쪽 복도 벽(z 3.13)에서 떨어진 자리
            new Spot { floor = "2F", wallPoint = new Vector3(40.125f, 3.9f + 1.65f, 4.4f), outward = Vector3.right },
        };
        const float WallGap = 0.03f;   // 벽지 장식 메시가 벽 앞으로 살짝 나와 있어서 띄우는 간격

        static string PrefabPath(string floor) { return PrefabDir + "/FloorMapBoard_" + floor + ".prefab"; }
        static string TexturePath(string floor) { return ImageDir + "/FloorMap_" + floor + "_Board.png"; }

        [MenuItem("Tools/KJH/FloorMap/Build Prefabs")]
        public static void BuildPrefabs()
        {
            var frame = AssetDatabase.LoadAssetAtPath<GameObject>(FramePrefabPath);
            if (frame == null) { Debug.LogError("[FloorMap] 액자 프리팹이 없다: " + FramePrefabPath); return; }
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);

            foreach (var spot in Spots)
            {
                var tex = ImportTexture(TexturePath(spot.floor));
                if (tex == null) { Debug.LogError("[FloorMap] 지도 그림이 없다: " + TexturePath(spot.floor)); continue; }
                var mat = MakeMaterial(spot.floor, tex);
                BuildPrefab(spot.floor, frame, tex, mat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[FloorMap] 프리팹 생성 완료: " + PrefabDir);
        }

        [MenuItem("Tools/KJH/FloorMap/Place In Open Scene")]
        public static void PlaceInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var spot in Spots)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(spot.floor));
                if (prefab == null) { Debug.LogError("[FloorMap] 먼저 Build Prefabs를 실행할 것: " + PrefabPath(spot.floor)); continue; }
                string name = "FloorMapBoard_" + spot.floor;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == name) Undo.DestroyObjectImmediate(root);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.name = name;
                go.transform.SetPositionAndRotation(spot.wallPoint + spot.outward * WallGap,
                    Quaternion.LookRotation(spot.outward, Vector3.up));
                Undo.RegisterCreatedObjectUndo(go, "Place FloorMapBoard");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[FloorMap] " + scene.name + " 씬에 지도 액자 2개 배치 (저장은 직접)");
        }

        static Texture2D ImportTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            bool changed = false;
            if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; changed = true; }
            if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; changed = true; }
            // 기본값(가까운 2의 거듭제곱으로 늘림)이면 2048×2048 정사각형이 되어 확대 화면 비율이 깨진다
            if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (imp.anisoLevel != 4) { imp.anisoLevel = 4; changed = true; }
            if (imp.textureCompression != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
            if (changed) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material MakeMaterial(string floor, Texture2D tex)
        {
            string path = MaterialDir + "/M_FloorMap_" + floor + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", Color.white * Emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildPrefab(string floor, GameObject framePrefab, Texture2D tex, Material mat)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                float sw = FrameScaleWidth, sh = FrameScaleHeight, sd = FrameScaleDepth;
                var root = new GameObject("FloorMapBoard_" + floor);
                EditorSceneManager.MoveGameObjectToScene(root, preview);

                // 루트: +Z = 방 쪽(벽에서 나오는 방향), 원점 = 액자 뒷면 가운데
                var board = root.AddComponent<FloorMapBoard>();
                var so = new SerializedObject(board);
                so.FindProperty("mapTexture").objectReferenceValue = tex;
                so.ApplyModifiedPropertiesWithoutUndo();

                // 액자: 원래 방향 그대로 세움 — 메시 위쪽(Z, 걸이 문양)이 위(+Y), 앞면(-Y)이 방 쪽(+Z), 메시 X가 가로
                var frame = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, preview);
                frame.name = "Frame";
                frame.transform.SetParent(root.transform, false);
                frame.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
                frame.transform.localPosition = new Vector3(0f, 0f, MeshBackY * sd);
                frame.transform.localScale = new Vector3(sw, sd, sh);   // 메시 축 순서: X=가로, Y=두께, Z=세로

                // 지도 그림: 기본 Quad는 -Z 쪽에서 보이므로 Y로 180도 돌려 방 쪽에서 보이게
                var map = GameObject.CreatePrimitive(PrimitiveType.Quad);
                map.name = "Map";
                Object.DestroyImmediate(map.GetComponent<Collider>());
                map.transform.SetParent(root.transform, false);
                map.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                map.transform.localPosition = new Vector3(0f, CanvasCenterZ * sh, (MeshBackY - CanvasFrontY) * sd);
                map.transform.localScale = new Vector3(CanvasWidth * sw, CanvasHeight * sh, 1f);
                var mr = map.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // 조준용 콜라이더 (액자 전체)
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0f, MeshBackY * sd);
                box.size = new Vector3(FrameWidth * sw, FrameHeight * sh, FrameDepth * sd);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(floor));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
