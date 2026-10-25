using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PotionCraft.EditorTools
{
    // Tools/KJH/PotionCraft/Build All
    // 약품 제조 퍼즐에 필요한 데이터 에셋·임시 도형 프리팹을 만들고, KJH 씬에 배치/연결한다. 여러 번 실행해도 되도록 다시 만들어 덮어쓴다.
    //  - 데이터: PotionCatalog / PotionRecipeTable / RecipeBookData / FinalRecipeData
    //  - 프리팹: Item_*, Supply_*, Station_*, RecipeBook_F2, RecipeNote_F1
    //  - 씬: F1_Kitchen / F2_Guest1 아래 "PotionCraft_F1/F2" 묶음에 배치, DoorSync_LGH에 PotionCraftSync 추가, 도르래(cabin)를 덩이용으로 설정
    public static class PotionCraftBuilder
    {
        const string Root = "Assets/Prefabs/KJH/PotionCraft";
        const string MatDir = Root + "/Materials";
        const string DataDir = Root + "/Data";
        const string ItemDir = Root + "/Items";
        const string StationDir = Root + "/Stations";
        const string MixerModelPath = "Assets/VARCO 3D/KWJ/FBX/Props/Prop_IngredientMixer.fbx";
        const string NoteImagePath = "Assets/Images/KJH/PotionCraft/RecipeNote_Final.png";
        const string BookImagePath = "Assets/Images/KJH/PotionCraft/조합법{0}쪽.png";

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Tools/KJH/PotionCraft/Build All")]
        public static void BuildAll()
        {
            EnsureFolders();
            mats.Clear();
            var items = BuildItemPrefabs();
            var data = BuildData(items);
            BuildStationPrefabs(items);
            PlaceInScene(data);
            Debug.Log("[PotionCraft] 빌드 완료 — 데이터/프리팹 생성, KJH 씬에 배치");
        }

        // ---------------------------------------------------------------- 폴더/머티리얼/도형

        static void EnsureFolders()
        {
            foreach (var p in new[] { "Assets/Prefabs/KJH", Root, MatDir, DataDir, ItemDir, StationDir })
            {
                if (AssetDatabase.IsValidFolder(p)) continue;
                int slash = p.LastIndexOf('/');
                AssetDatabase.CreateFolder(p.Substring(0, slash), p.Substring(slash + 1));
            }
        }

        static Material Mat(string name, Color c, bool unlitParticle = false)
        {
            if (mats.TryGetValue(name, out var cached) && cached != null) return cached;
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var sh = Shader.Find(unlitParticle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return asset;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[PotionCraft] 필드를 찾을 수 없음: " + target.GetType().Name + "." + field); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[PotionCraft] 필드를 찾을 수 없음: " + target.GetType().Name + "." + field); return; }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[PotionCraft] 필드를 찾을 수 없음: " + target.GetType().Name + "." + field); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 색

        static readonly Color RedRootC = new Color(0.55f, 0.22f, 0.15f);
        static readonly Color SilverC = new Color(0.78f, 0.80f, 0.85f);
        static readonly Color GreenLeafC = new Color(0.25f, 0.62f, 0.25f);
        static readonly Color BlueFlowerC = new Color(0.30f, 0.45f, 0.95f);
        static readonly Color WaterRedC = new Color(0.85f, 0.15f, 0.15f);
        static readonly Color WaterBlueC = new Color(0.18f, 0.35f, 0.9f);
        static readonly Color WaterGreenC = new Color(0.2f, 0.75f, 0.3f);
        static readonly Color LumpC = new Color(0.45f, 0.45f, 0.45f);

        // ---------------------------------------------------------------- 아이템 프리팹 (손에 들거나 보관대 위에 놓는 모양)

        class ItemSet
        {
            public GameObject[] materials = new GameObject[4];
            public GameObject[] waters = new GameObject[3];
            public GameObject lump, powder;
        }

        static ItemSet BuildItemPrefabs()
        {
            var set = new ItemSet();

            // 붉은 뿌리: 굵고 긴 갈색 뿌리
            var r = new GameObject("Item_Material_RedRoot");
            Prim(PrimitiveType.Capsule, "Body", r.transform, Vector3.zero, new Vector3(0.07f, 0.12f, 0.07f), Mat("Item_RedRoot", RedRootC)).transform.localRotation = Quaternion.Euler(0, 0, 70f);
            set.materials[0] = SavePrefab(r, ItemDir + "/Item_Material_RedRoot.prefab");

            // 은빛 잎: 납작하고 넓은 은색 잎
            var s = new GameObject("Item_Material_SilverLeaf");
            Prim(PrimitiveType.Cube, "Body", s.transform, Vector3.zero, new Vector3(0.18f, 0.012f, 0.11f), Mat("Item_SilverLeaf", SilverC)).transform.localRotation = Quaternion.Euler(10f, 30f, 0);
            set.materials[1] = SavePrefab(s, ItemDir + "/Item_Material_SilverLeaf.prefab");

            // 초록 잎사귀: 납작한 타원 + 줄기
            var g = new GameObject("Item_Material_GreenLeaf");
            Prim(PrimitiveType.Sphere, "Body", g.transform, Vector3.zero, new Vector3(0.16f, 0.025f, 0.1f), Mat("Item_GreenLeaf", GreenLeafC));
            Prim(PrimitiveType.Cylinder, "Stem", g.transform, new Vector3(-0.09f, 0, 0), new Vector3(0.012f, 0.03f, 0.012f), Mat("Item_GreenLeaf", GreenLeafC)).transform.localRotation = Quaternion.Euler(0, 0, 90f);
            set.materials[2] = SavePrefab(g, ItemDir + "/Item_Material_GreenLeaf.prefab");

            // 파란 꽃: 둥근 파란 꽃송이 + 줄기
            var b = new GameObject("Item_Material_BlueFlower");
            Prim(PrimitiveType.Sphere, "Bloom", b.transform, new Vector3(0, 0.06f, 0), Vector3.one * 0.1f, Mat("Item_BlueFlower", BlueFlowerC));
            Prim(PrimitiveType.Cylinder, "Stem", b.transform, Vector3.zero, new Vector3(0.012f, 0.06f, 0.012f), Mat("Item_GreenLeaf", GreenLeafC));
            set.materials[3] = SavePrefab(b, ItemDir + "/Item_Material_BlueFlower.prefab");

            Color[] wc = { WaterRedC, WaterBlueC, WaterGreenC };
            string[] wn = { "Red", "Blue", "Green" };
            for (int i = 0; i < 3; i++)
            {
                var w = new GameObject("Item_Water_" + wn[i]);
                Prim(PrimitiveType.Cylinder, "Bottle", w.transform, Vector3.zero, new Vector3(0.09f, 0.07f, 0.09f), Mat("Item_Water_" + wn[i], wc[i]));
                Prim(PrimitiveType.Cylinder, "Neck", w.transform, new Vector3(0, 0.09f, 0), new Vector3(0.04f, 0.03f, 0.04f), Mat("Item_Water_" + wn[i], wc[i]));
                Prim(PrimitiveType.Cylinder, "Cap", w.transform, new Vector3(0, 0.125f, 0), new Vector3(0.05f, 0.012f, 0.05f), Mat("Item_Cap", new Color(0.9f, 0.88f, 0.8f)));
                set.waters[i] = SavePrefab(w, ItemDir + "/Item_Water_" + wn[i] + ".prefab");
            }

            // 덩이: 결과 색과 상관없이 항상 같은 회색
            var l = new GameObject("Item_Lump");
            Prim(PrimitiveType.Sphere, "Body", l.transform, Vector3.zero, new Vector3(0.17f, 0.13f, 0.15f), Mat("Item_Lump", LumpC));
            Prim(PrimitiveType.Sphere, "Bump", l.transform, new Vector3(0.05f, 0.05f, 0.02f), Vector3.one * 0.08f, Mat("Item_Lump", LumpC));
            set.lump = SavePrefab(l, ItemDir + "/Item_Lump.prefab");

            // 가루: 종이 위의 가루. "Tint"로 시작하는 부분이 가루 색으로 칠해진다.
            var p = new GameObject("Item_Powder");
            Prim(PrimitiveType.Cube, "Paper", p.transform, Vector3.zero, new Vector3(0.2f, 0.006f, 0.2f), Mat("Item_Paper", new Color(0.94f, 0.92f, 0.85f)));
            Prim(PrimitiveType.Sphere, "Tint_Pile", p.transform, new Vector3(0, 0.012f, 0), new Vector3(0.12f, 0.05f, 0.12f), Mat("Item_Powder", Color.white));
            set.powder = SavePrefab(p, ItemDir + "/Item_Powder.prefab");

            return set;
        }

        // ---------------------------------------------------------------- 데이터 에셋

        class DataSet
        {
            public PotionCatalog catalog;
            public PotionRecipeTable table;
            public RecipeBookData book;
            public FinalRecipeData final;
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        static DataSet BuildData(ItemSet items)
        {
            var d = new DataSet();

            d.catalog = CreateOrLoad<PotionCatalog>(DataDir + "/PotionCatalog.asset");
            d.catalog.materials = new[]
            {
                new PotionCatalog.MaterialInfo { id = MaterialId.RedRoot, displayName = "붉은 뿌리", color = RedRootC, prefab = items.materials[0] },
                new PotionCatalog.MaterialInfo { id = MaterialId.SilverLeaf, displayName = "은빛 잎", color = SilverC, prefab = items.materials[1] },
                new PotionCatalog.MaterialInfo { id = MaterialId.GreenLeaf, displayName = "초록 잎사귀", color = GreenLeafC, prefab = items.materials[2] },
                new PotionCatalog.MaterialInfo { id = MaterialId.BlueFlower, displayName = "파란 꽃", color = BlueFlowerC, prefab = items.materials[3] },
            };
            d.catalog.waters = new[]
            {
                new PotionCatalog.WaterInfo { id = WaterId.Red, displayName = "빨간색 물", color = WaterRedC, prefab = items.waters[0] },
                new PotionCatalog.WaterInfo { id = WaterId.Blue, displayName = "파란색 물", color = WaterBlueC, prefab = items.waters[1] },
                new PotionCatalog.WaterInfo { id = WaterId.Green, displayName = "초록색 물", color = WaterGreenC, prefab = items.waters[2] },
            };
            d.catalog.powders = new[]
            {
                new PotionCatalog.PowderInfo { id = PowderColor.Green, displayName = "초록색 가루", color = new Color(0.2f, 0.8f, 0.3f) },
                new PotionCatalog.PowderInfo { id = PowderColor.Red, displayName = "빨간색 가루", color = new Color(0.9f, 0.12f, 0.12f) },
                new PotionCatalog.PowderInfo { id = PowderColor.Blue, displayName = "파란색 가루", color = new Color(0.15f, 0.35f, 0.95f) },
                new PotionCatalog.PowderInfo { id = PowderColor.Pink, displayName = "분홍색 가루", color = new Color(1f, 0.5f, 0.75f) },
                new PotionCatalog.PowderInfo { id = PowderColor.Black, displayName = "검정색 가루", color = new Color(0.06f, 0.06f, 0.06f) },
                new PotionCatalog.PowderInfo { id = PowderColor.Gold, displayName = "황금빛 가루", color = new Color(1f, 0.82f, 0.2f) },
            };
            d.catalog.lumpName = "재료 덩이";
            d.catalog.lumpColor = LumpC;
            d.catalog.lumpPrefab = items.lump;
            d.catalog.powderPrefab = items.powder;
            d.catalog.fxMaterial = Mat("Fx_Particle", Color.white, true);
            EditorUtility.SetDirty(d.catalog);

            // 3-2 제조 결과표: 유효 조합 4개만 등록, 나머지는 전부 Black
            d.table = CreateOrLoad<PotionRecipeTable>(DataDir + "/PotionRecipeTable.asset");
            d.table.defaultResult = PowderColor.Black;
            d.table.entries = new[]
            {
                new PotionRecipeTable.Entry { material = MaterialId.RedRoot, water = WaterId.Green, result = PowderColor.Green },
                new PotionRecipeTable.Entry { material = MaterialId.SilverLeaf, water = WaterId.Red, result = PowderColor.Red },
                new PotionRecipeTable.Entry { material = MaterialId.GreenLeaf, water = WaterId.Blue, result = PowderColor.Blue },
                new PotionRecipeTable.Entry { material = MaterialId.BlueFlower, water = WaterId.Red, result = PowderColor.Pink },
            };
            EditorUtility.SetDirty(d.table);

            // 3-3 제조법 책 (함정 항목 포함, 결과 색은 적지 않는다)
            d.book = CreateOrLoad<RecipeBookData>(DataDir + "/RecipeBookData.asset");
            d.book.title = "제조법";
            d.book.pages = new[]
            {
                new RecipeBookData.Page { entryA = "은빛 잎 + 빨간색 물", entryB = "파란 뿌리 + 파란색 물" },
                new RecipeBookData.Page { entryA = "만드레이크 + 빨간색 물", entryB = "붉은 뿌리 + 초록색 물" },
                new RecipeBookData.Page { entryA = "초록 잎사귀 + 파란색 물", entryB = "붉은 뿌리 + 검정색 물" },
                new RecipeBookData.Page { entryA = "은빛 잎 + 보라색 물", entryB = "파란 꽃 + 빨간색 물" },
            };
            // 쪽마다 그림(조합법N쪽.png)이 있으면 연결한다 → 책이 그림으로 한 쪽씩 넘어간다
            for (int i = 0; i < d.book.pages.Length; i++)
                d.book.pages[i].image = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(BookImagePath, i + 1));
            EditorUtility.SetDirty(d.book);

            // 3-4 최종 레시피 (1층 쪽지)
            d.final = CreateOrLoad<FinalRecipeData>(DataDir + "/FinalRecipeData.asset");
            d.final.title = "쪽지";
            d.final.noteText = "빨간색 가루 + 파란색 가루 + 분홍색 가루";
            d.final.noteImage = AssetDatabase.LoadAssetAtPath<Texture2D>(NoteImagePath); // 그림이 있으면 글 대신 그림을 띄운다
            d.final.sequence = new[] { PowderColor.Red, PowderColor.Blue, PowderColor.Pink };
            EditorUtility.SetDirty(d.final);

            AssetDatabase.SaveAssets();
            return d;
        }

        // ---------------------------------------------------------------- 스테이션 프리팹

        static GameObject Inst(GameObject prefab, Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            return go;
        }

        static void BuildStationPrefabs(ItemSet items)
        {
            var wood = Mat("Station_Wood", new Color(0.42f, 0.28f, 0.16f));
            var stone = Mat("Station_Stone", new Color(0.35f, 0.35f, 0.38f));
            var dark = Mat("Station_Dark", new Color(0.12f, 0.12f, 0.13f));

            // ---- 보관대 (기본 프리팹 2개 + 변형 7개)
            var matBaseRoot = new GameObject("Supply_Material");
            Prim(PrimitiveType.Cube, "Crate", matBaseRoot.transform, new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f), wood, true);
            matBaseRoot.AddComponent<SupplyStation>();
            var matBase = SavePrefab(matBaseRoot, StationDir + "/Supply_Material.prefab");

            var waterBaseRoot = new GameObject("Supply_Water");
            Prim(PrimitiveType.Cube, "Shelf", waterBaseRoot.transform, new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.6f), stone, true);
            waterBaseRoot.AddComponent<SupplyStation>();
            var waterBase = SavePrefab(waterBaseRoot, StationDir + "/Supply_Water.prefab");

            string[] mn = { "RedRoot", "SilverLeaf", "GreenLeaf", "BlueFlower" };
            for (int i = 0; i < 4; i++)
            {
                var v = (GameObject)PrefabUtility.InstantiatePrefab(matBase);
                var sup = v.GetComponent<SupplyStation>();
                SetInt(sup, "kind", (int)ItemKind.Material);
                SetInt(sup, "material", i);
                // 상자 위에 같은 재료 3개를 쌓아 둔다 (색·모양으로 구분)
                Inst(items.materials[i], v.transform, new Vector3(-0.2f, 0.66f, 0f), Vector3.one);
                Inst(items.materials[i], v.transform, new Vector3(0.2f, 0.66f, 0.05f), Vector3.one);
                Inst(items.materials[i], v.transform, new Vector3(0f, 0.72f, -0.05f), Vector3.one);
                SavePrefab(v, StationDir + "/Supply_Material_" + mn[i] + ".prefab");
            }
            string[] wn = { "Red", "Blue", "Green" };
            for (int i = 0; i < 3; i++)
            {
                var v = (GameObject)PrefabUtility.InstantiatePrefab(waterBase);
                var sup = v.GetComponent<SupplyStation>();
                SetInt(sup, "kind", (int)ItemKind.Water);
                SetInt(sup, "water", i);
                Inst(items.waters[i], v.transform, new Vector3(-0.22f, 0.66f, 0f), Vector3.one);
                Inst(items.waters[i], v.transform, new Vector3(0f, 0.66f, 0f), Vector3.one);
                Inst(items.waters[i], v.transform, new Vector3(0.22f, 0.66f, 0f), Vector3.one);
                SavePrefab(v, StationDir + "/Supply_Water_" + wn[i] + ".prefab");
            }

            // ---- 1층 제조 통
            // 겉모습은 VARCO 혼합기 모델. Pot은 상호작용용 콜라이더만 남긴다.
            var brewer = new GameObject("Station_Brewer_F1");
            var pot = Prim(PrimitiveType.Cylinder, "Pot", brewer.transform, new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.4f, 0.9f), dark, true);
            pot.GetComponent<Renderer>().enabled = false;
            var shake = new GameObject("MixerShake").transform; // 피벗 = 바닥 가운데 (제조 중 흔들림 기준점)
            shake.SetParent(brewer.transform, false);
            var mixerModel = AssetDatabase.LoadAssetAtPath<GameObject>(MixerModelPath);
            if (mixerModel != null) Inst(mixerModel, shake, new Vector3(0, 0.4f, 0), Vector3.one * 0.9f).name = "Model";
            else Debug.LogWarning("[PotionCraft] 혼합기 모델이 없다: " + MixerModelPath);
            var lump = Inst(items.lump, brewer.transform, new Vector3(0, 0.85f, 0), Vector3.one * 1.6f); // 혼합기 깔때기 안에 얹힘
            lump.name = "Lump";
            var bs = brewer.AddComponent<BrewerStation>();
            SetRef(bs, "shakeVisual", shake);
            SetRef(bs, "lumpVisual", lump);
            lump.SetActive(false);
            SavePrefab(brewer, StationDir + "/Station_Brewer_F1.prefab");

            // ---- 2층 빻는 도구
            var grinder = new GameObject("Station_Grinder_F2");
            Prim(PrimitiveType.Cylinder, "Pedestal", grinder.transform, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.3f, 0.6f), wood, true);
            Prim(PrimitiveType.Sphere, "Bowl", grinder.transform, new Vector3(0, 0.68f, 0), new Vector3(0.5f, 0.3f, 0.5f), stone);
            var gLump = Inst(items.lump, grinder.transform, new Vector3(0, 0.76f, 0), Vector3.one * 1.3f);
            gLump.name = "Lump";
            var gPowder = Inst(items.powder, grinder.transform, new Vector3(0, 0.84f, 0), Vector3.one * 1.4f);
            gPowder.name = "PowderOnPaper";
            var pestle = Prim(PrimitiveType.Capsule, "Pestle", grinder.transform, new Vector3(0.05f, 1.2f, 0), new Vector3(0.1f, 0.22f, 0.1f), stone);
            pestle.transform.localRotation = Quaternion.Euler(0, 0, 8f);
            var bar = Prim(PrimitiveType.Cube, "ProgressBar", grinder.transform, new Vector3(0, 0.62f, 0.34f), new Vector3(0.4f, 0.04f, 0.03f), Mat("Station_Bar", new Color(0.9f, 0.8f, 0.3f)));
            var gs = grinder.AddComponent<GrinderStation>();
            SetRef(gs, "pestle", pestle.transform);
            SetRef(gs, "lumpVisual", gLump);
            SetRef(gs, "powderVisual", gPowder);
            SetRef(gs, "progressBar", bar.GetComponent<Renderer>());
            gLump.SetActive(false); gPowder.SetActive(false); bar.SetActive(false);
            SavePrefab(grinder, StationDir + "/Station_Grinder_F2.prefab");

            // ---- 2층 최종 혼합 통 + 비우기 버튼
            var mixer = new GameObject("Station_Mixer_F2");
            Prim(PrimitiveType.Cylinder, "Vat", mixer.transform, new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.25f, 0.7f), dark, true);
            var layers = new Renderer[MixerStation.SlotCount];
            var layerMat = Mat("Station_Layer", Color.white);
            for (int i = 0; i < layers.Length; i++)
            {
                var ly = Prim(PrimitiveType.Cylinder, "Layer" + i, mixer.transform, new Vector3(0, 0.55f + i * 0.1f, 0), new Vector3(0.55f, 0.05f, 0.55f), layerMat);
                layers[i] = ly.GetComponent<Renderer>();
                ly.SetActive(false);
            }
            var fxPoint = new GameObject("EffectPoint");
            fxPoint.transform.SetParent(mixer.transform, false);
            fxPoint.transform.localPosition = new Vector3(0, 0.95f, 0);
            var ms = mixer.AddComponent<MixerStation>();
            var mso = new SerializedObject(ms);
            var layersProp = mso.FindProperty("layers");
            layersProp.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++) layersProp.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            SetRef(ms, "effectPoint", fxPoint.transform);

            var btn = Prim(PrimitiveType.Cube, "ClearButton", mixer.transform, new Vector3(0.7f, 0.5f, 0), new Vector3(0.2f, 0.2f, 0.2f), Mat("Station_Button", new Color(0.8f, 0.15f, 0.12f)), true);
            Prim(PrimitiveType.Cube, "ButtonPost", mixer.transform, new Vector3(0.7f, 0.2f, 0), new Vector3(0.1f, 0.4f, 0.1f), wood);
            var cb = btn.AddComponent<MixerClearButton>();
            SetRef(cb, "mixer", ms);
            SavePrefab(mixer, StationDir + "/Station_Mixer_F2.prefab");

            // ---- 책 / 쪽지
            var book = new GameObject("RecipeBook_F2");
            Prim(PrimitiveType.Cube, "Stand", book.transform, new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.8f, 0.5f), wood, true);
            var cover = Prim(PrimitiveType.Cube, "Book", book.transform, new Vector3(0, 0.84f, 0), new Vector3(0.45f, 0.08f, 0.32f), Mat("Station_BookCover", new Color(0.35f, 0.1f, 0.12f)), true);
            cover.transform.localRotation = Quaternion.Euler(0, 12f, 0);
            Prim(PrimitiveType.Cube, "Pages", book.transform, new Vector3(0, 0.845f, 0), new Vector3(0.4f, 0.07f, 0.28f), Mat("Station_Paper", new Color(0.93f, 0.9f, 0.8f))).transform.localRotation = Quaternion.Euler(0, 12f, 0);
            var rb = book.AddComponent<ReadableStation>();
            SetInt(rb, "kind", (int)ReadableStation.Kind.Book);
            SavePrefab(book, StationDir + "/RecipeBook_F2.prefab");

            var note = new GameObject("RecipeNote_F1");
            Prim(PrimitiveType.Cube, "Stand", note.transform, new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.8f, 0.5f), wood, true);
            var paper = Prim(PrimitiveType.Cube, "Note", note.transform, new Vector3(0, 0.805f, 0), new Vector3(0.25f, 0.01f, 0.32f), Mat("Station_Paper", new Color(0.93f, 0.9f, 0.8f)), true);
            paper.transform.localRotation = Quaternion.Euler(0, -10f, 0);
            var rn = note.AddComponent<ReadableStation>();
            SetInt(rn, "kind", (int)ReadableStation.Kind.Note);
            SavePrefab(note, StationDir + "/RecipeNote_F1.prefab");

            // ---- 쓰레기통 (1층·2층 공용)
            var trash = new GameObject("Station_Trash");
            Prim(PrimitiveType.Cylinder, "Bin", trash.transform, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.3f, 0.5f), Mat("Station_Trash", new Color(0.2f, 0.28f, 0.22f)), true);
            Prim(PrimitiveType.Cylinder, "Inside", trash.transform, new Vector3(0, 0.595f, 0), new Vector3(0.42f, 0.005f, 0.42f), dark);
            trash.AddComponent<TrashStation>();
            SavePrefab(trash, StationDir + "/Station_Trash.prefab");

            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- 씬 배치

        static Transform FindByName(string n)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == n) return t;
            return null;
        }

        static GameObject Place(string prefabFile, Transform container, float x, float y, float z, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StationDir + "/" + prefabFile + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static Transform MakeContainer(string name, Transform parent)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void PlaceInScene(DataSet d)
        {
            var kitchen = FindByName("F1_Kitchen");
            var guest = FindByName("F2_Guest1");
            if (kitchen == null || guest == null) { Debug.LogError("[PotionCraft] F1_Kitchen 또는 F2_Guest1 을 찾을 수 없습니다 (KJH 씬이 열려 있나요?)"); return; }

            // 1층 (바닥 y = 0) — 도르래 창(31.6, z=0.15) 앞에서 가까운 빈 공간
            var f1 = MakeContainer("PotionCraft_F1", kitchen);
            float[] mx = { 27.0f, 28.1f, 29.2f, 30.3f };
            string[] mn = { "RedRoot", "SilverLeaf", "GreenLeaf", "BlueFlower" };
            for (int i = 0; i < 4; i++) Place("Supply_Material_" + mn[i], f1, mx[i], 0f, 5.2f);
            float[] wx = { 33.0f, 34.1f, 35.2f };
            string[] wn = { "Red", "Blue", "Green" };
            for (int i = 0; i < 3; i++) Place("Supply_Water_" + wn[i], f1, wx[i], 0f, 5.2f);
            Place("Station_Brewer_F1", f1, 31.6f, 0f, 3.6f);
            Place("Station_Trash", f1, 29.0f, 0f, 2.0f);
            Place("RecipeNote_F1", f1, 35.5f, 0f, 2.0f);

            // 2층 (바닥 y = 3.9)
            var f2 = MakeContainer("PotionCraft_F2", guest);
            Place("Station_Grinder_F2", f2, 30.0f, 3.9f, 3.2f);
            Place("Station_Mixer_F2", f2, 33.0f, 3.9f, 4.2f);
            Place("RecipeBook_F2", f2, 28.0f, 3.9f, 4.8f);
            Place("Station_Trash", f2, 29.0f, 3.9f, 1.2f);

            SetupSyncPrefab(d.catalog, d.table, d.book, d.final);

            // 도르래(cabin): 양초 대신 덩이만 1개 싣게 필터를 바꾸고, 방향을 1층→2층으로 (싣기 창=1층, 꺼내기 창=2층)
            var box = Object.FindAnyObjectByType<Dumbwaiter>(FindObjectsInactive.Include);
            if (box != null)
            {
                SetInt(box, "acceptedCargo", (int)Dumbwaiter.CargoKind.PotionLump);
                SetInt(box, "capacity", 1);
            }
            foreach (var w in Object.FindObjectsByType<DumbwaiterWindow>(FindObjectsInactive.Include))
            {
                if (w.name == "Window_F1") SetBool(w, "canLoad", true);
                else if (w.name == "Window_F2") SetBool(w, "canLoad", false);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // 동기화 스크립트는 DoorSync_LGH가 아니라 자기 전용 NetworkObject 프리팹에 둔다.
        // 이유: 클라이언트(게스트)는 씬의 DoorSync_LGH를 버리고 "네트워크 프리팹으로 등록된 DoorSync_LGH 프리팹"으로 바꿔 끼우기 때문에,
        // 씬에서만 붙인 컴포넌트는 게스트에게 존재하지 않는다. 자기 프리팹을 네트워크 프리팹에 등록하면 게스트에도 같은 컴포넌트·데이터가 생긴다.
        [MenuItem("Tools/KJH/PotionCraft/Setup Sync Prefab")]
        public static void SetupSyncPrefabMenu()
        {
            EnsureFolders();
            SetupSyncPrefab(
                AssetDatabase.LoadAssetAtPath<PotionCatalog>(DataDir + "/PotionCatalog.asset"),
                AssetDatabase.LoadAssetAtPath<PotionRecipeTable>(DataDir + "/PotionRecipeTable.asset"),
                AssetDatabase.LoadAssetAtPath<RecipeBookData>(DataDir + "/RecipeBookData.asset"),
                AssetDatabase.LoadAssetAtPath<FinalRecipeData>(DataDir + "/FinalRecipeData.asset"));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void SetupSyncPrefab(PotionCatalog catalog, PotionRecipeTable table, RecipeBookData book, FinalRecipeData final)
        {
            // 예전에 DoorSync_LGH 씬 오브젝트에 직접 붙였던 컴포넌트는 제거 (둘이 있으면 Instance가 꼬인다)
            var candle = Object.FindAnyObjectByType<LGH.CandleNetworkSync>(FindObjectsInactive.Include);
            if (candle != null)
            {
                var old = candle.GetComponent<PotionCraftSync>();
                if (old != null) Object.DestroyImmediate(old);
            }

            string path = Root + "/PotionCraftSync.prefab";
            var temp = new GameObject("PotionCraftSync");
            temp.AddComponent<Unity.Netcode.NetworkObject>();
            var sync = temp.AddComponent<PotionCraftSync>();
            SetRef(sync, "catalog", catalog);
            SetRef(sync, "recipeTable", table);
            SetRef(sync, "bookData", book);
            SetRef(sync, "finalRecipe", final);
            var prefab = SavePrefab(temp, path);

            // 네트워크 프리팹 목록에 등록 (게스트가 이 프리팹으로 같은 객체를 만든다)
            var list = AssetDatabase.LoadAssetAtPath<Unity.Netcode.NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            if (list == null) { Debug.LogError("[PotionCraft] Assets/DefaultNetworkPrefabs.asset 을 찾을 수 없습니다."); }
            else
            {
                bool has = false;
                foreach (var np in list.PrefabList) if (np != null && np.Prefab == prefab) has = true;
                if (!has) { list.Add(new Unity.Netcode.NetworkPrefab { Prefab = prefab }); EditorUtility.SetDirty(list); AssetDatabase.SaveAssets(); }
            }

            // 씬에는 프리팹 인스턴스로 배치 (이미 있으면 교체)
            foreach (var t in Object.FindObjectsByType<PotionCraftSync>(FindObjectsInactive.Include))
                Object.DestroyImmediate(t.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "PotionCraftSync";
        }
    }
}
