using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CoopDemo.EditorTools
{
    // 야외 밤 조명 (2026-10-30, A안). 낮/밤 주기는 쓰지 않고 밤으로 고정한다는 전제.
    //  - 하늘: KSM/NightSky(별+달) 스카이박스, 플레이어 카메라는 Skybox로 그린다.
    //  - 환경광: Trilight(위쪽만 살짝 푸르게), 안개: 색을 수평선에 맞추고 조금 옅게.
    //  - 달빛 방향광 세기, 가로등(빠진 Lantern_5 조명 보충, 따뜻한 색, 깜빡임), 분수 불빛.
    //  - DayNightCycle.manageEnvironment를 꺼서 환경광/안개를 덮어쓰지 않게 한다.
    // 재실행해도 같은 결과가 나온다(새 오브젝트는 이름으로 찾아 재사용).
    public static class NightExteriorBuilder
    {
        const string MatPath = "Assets/Materials/KSM/Exterior/NightSky.mat";
        const string PlayerPrefabPath = "Assets/Objects/LGH/NetworkPlayer_LGH.prefab";

        const uint MoonLayer = 2u;     // 렌더링 레이어 1 = 달빛 방향광 전용
        const uint AllLayers = 3u;     // 레이어 0 + 1: 기본 광원과 달빛 둘 다 받음
        const uint HeldCandleBodyLayers = 130u;   // 레이어 1 + 7: 달빛과 양초 전용 빛(CandleSelfLight)만 받음, 방 조명은 안 받음

        // 수평선 색 = 안개 색 (경계가 이어지게)
        static readonly Color Horizon = new Color(0.035f, 0.048f, 0.095f);

        [MenuItem("Tools/KSM/Lighting/Exterior - Apply Night Look")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Develop.unity") { Debug.LogError("[NightExterior] Develop 씬에서만 실행합니다."); return; }

            // 하늘
            var shader = Shader.Find("KSM/NightSky");
            if (shader == null) { Debug.LogError("[NightExterior] KSM/NightSky 셰이더를 찾을 수 없습니다."); return; }
            EnsureFolder("Assets/Materials/KSM/Exterior");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
            mat.shader = shader;
            mat.SetColor("_HorizonColor", Horizon);
            mat.SetFloat("_StarDensity", 0.03f);     // 2026-10-30 시험: 0.012는 너무 듬성듬성했다
            mat.SetFloat("_StarBrightness", 2.6f);
            mat.SetFloat("_StarScale", 90f);
            EditorUtility.SetDirty(mat);
            RenderSettings.skybox = mat;
            RenderSettings.sun = null;   // 프로시저럴 스카이박스용 연결이었다

            // 환경광(야외 오브젝트용): 위는 살짝 푸르게, 옆은 기존 밤 값과 비슷하게, 아래는 어둡게
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.06f, 0.075f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.032f, 0.036f, 0.062f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.012f, 0.016f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogDensity = 0.018f;

            // 달빛
            var ext = scene.GetRootGameObjects().First(g => g.name == "Exterior_LGH").transform;
            var moon = ext.Find("Moonlight_LGH").GetComponent<Light>();
            moon.intensity = 0.5f;
            EditorUtility.SetDirty(moon);
            var skyRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "NightSky_KSM");
            if (skyRoot == null) { skyRoot = new GameObject("NightSky_KSM"); SceneManager.MoveGameObjectToScene(skyRoot, scene); }
            var sm = skyRoot.GetComponent<NightSkyMoon>() ?? skyRoot.AddComponent<NightSkyMoon>();
            sm.moonLight = moon;

            // 주기 스크립트가 환경광/안개를 덮어쓰지 않게
            foreach (var d in Object.FindObjectsByType<LGH.DayNightCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { d.manageEnvironment = false; EditorUtility.SetDirty(d); }

            // 가로등
            var lanterns = ext.Find("Lanterns");
            Light template = null;
            foreach (Transform l in lanterns) { var li = l.Find("Light"); if (li != null) { template = li.GetComponent<Light>(); break; } }
            int added = 0;
            foreach (Transform l in lanterns)
            {
                var lightT = l.Find("Light");
                if (lightT == null && template != null)
                {
                    var go = new GameObject("Light");
                    go.transform.SetParent(l, false);
                    go.transform.position = template.transform.position + (l.position - template.transform.parent.position);
                    go.AddComponent<Light>();
                    lightT = go.transform; added++;
                }
                var light = lightT.GetComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.66f, 0.36f);
                light.intensity = 1.6f;
                light.range = 10f;
                light.shadows = LightShadows.None;
                EditorUtility.SetDirty(light);
                var head = l.Find("Head");
                var fl = l.GetComponent<LanternFlicker>() ?? l.gameObject.AddComponent<LanternFlicker>();
                fl.lanternLight = light;
                fl.head = head != null ? head.GetComponent<Renderer>() : null;
            }

            // 분수: 이름에 light가 붙어 있지만 조명이 없었다. 차가운 물빛을 아주 약하게.
            var fountain = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "fountain_4_light");
            if (fountain != null)
            {
                var glow = fountain.transform.Find("FountainGlow");
                if (glow == null)
                {
                    var go = new GameObject("FountainGlow");
                    go.transform.SetParent(fountain.transform, false);
                    go.transform.position = fountain.transform.position + new Vector3(0f, 1.0f, 0f);
                    go.AddComponent<Light>();
                    glow = go.transform;
                }
                var gl = glow.GetComponent<Light>();
                gl.type = LightType.Point;
                gl.color = new Color(0.45f, 0.7f, 1f);
                gl.intensity = 1.0f;
                gl.range = 7f;
                gl.shadows = LightShadows.None;
                EditorUtility.SetDirty(gl);
            }

            // 야외 오브젝트가 실내 프로브(따뜻한 베이크 조명)를 받지 않게 한다.
            // 건물 안 프로브를 만든 뒤 벽돌 외관(Vic_Brick 등, 건물 한가운데가 중심인 거대 메시)과 건물 가까운 나무가
            // 실내의 주황빛을 받아 밤인데 저녁처럼 밝게 보였다(2026-10-30 실측: 프로브 끄면 어두운 밤 톤).
            // 프로브를 끄면 환경광(Trilight)과 달빛·가로등만 받는다.
            int probesOff = 0;
            var outdoorRoots = new System.Collections.Generic.List<Transform>();
            foreach (var n in new[] { "Exterior_LGH", "VictorianExterior_LGH", "GIM005_Maze", "fountain_4_light" })
            { var g = scene.GetRootGameObjects().FirstOrDefault(x => x.name == n); if (g != null) outdoorRoots.Add(g.transform); }
            var rw = scene.GetRootGameObjects().FirstOrDefault(x => x.name == "Ravenwood_LGH");
            if (rw != null)
            {
                var c1 = rw.transform.Find("Courtyard"); if (c1 != null) outdoorRoots.Add(c1);
                var c2 = rw.transform.Find("TempFurniture/Courtyard"); if (c2 != null) outdoorRoots.Add(c2);
            }
            foreach (var t in outdoorRoots)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.lightProbeUsage == LightProbeUsage.Off) continue;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    EditorUtility.SetDirty(r);
                    if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    probesOff++;
                }

            // 달빛 방향광은 야외 전용으로 분리한다 (렌더링 레이어, 2026-10-31).
            // 창가 달빛은 스포트라이트(MoonlightWindows)가 만들고, 방향광도 같은 방향으로 자기 창 무늬를 만들어서 둘이 겹쳐 이중 무늬가 보였다.
            // → 방향광을 레이어 2(비트 1) 전용으로 두고, 그 빛을 받아야 하는 것(야외·지붕·플레이어)만 레이어 0·1을 갖게 한다.
            //   실내와 모든 기존 광원은 기본 레이어(0) 그대로라 새 조명을 추가해도 아무것도 안 해도 된다.
            // 주의: 라이트의 레이어는 Light.renderingLayerMask가 아니라 UniversalAdditionalLightData.renderingLayers를 URP가 읽는다.
            //       그림자는 모든 렌더러가 드리워야 하므로(천장·벽이 방향광을 막아야 함) 그림자 레이어를 따로 3으로 둔다.
            var moonData = moon.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>() ?? moon.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            moon.renderingLayerMask = (int)MoonLayer;
            moonData.renderingLayers = MoonLayer;
            moonData.customShadowLayers = true;
            moonData.shadowRenderingLayers = AllLayers;
            EditorUtility.SetDirty(moon); EditorUtility.SetDirty(moonData);
            int layered = 0;
            var layerRoots = new System.Collections.Generic.List<Transform>(outdoorRoots);
            if (rw != null)
            {
                var roof = rw.transform.Find("Floor2/Roof"); if (roof != null) layerRoots.Add(roof);
                var atticT = rw.transform.Find("Attic");
                if (atticT != null) foreach (Transform c in atticT) if (c.name.StartsWith("AtticRoof")) layerRoots.Add(c);
            }
            foreach (var t in layerRoots)
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.renderingLayerMask == AllLayers) continue;
                    r.renderingLayerMask = AllLayers;
                    EditorUtility.SetDirty(r);
                    if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                    layered++;
                }
            // 플레이어 아바타(야외에서 달빛을 받아야 함)
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/KSM/Avatar" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileName(p).StartsWith("Avatar_")) continue;
                var ar = PrefabUtility.LoadPrefabContents(p);
                try { foreach (var r in ar.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = AllLayers; PrefabUtility.SaveAsPrefabAsset(ar, p); }
                finally { PrefabUtility.UnloadPrefabContents(ar); }
            }

            // 플레이어 카메라가 하늘을 그리게 (지금은 단색 배경)
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true)) cam.clearFlags = CameraClearFlags.Skybox;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    // 플레이어 몸/손/양초도 야외 달빛을 받는다. 단 손 양초 몸체(밀랍·접시)는 방 조명(레이어 0)에서 뺀다:
                    // 불꽃과 5~8cm 거리라 방 조명을 받으면 밀랍 전체가 새하얗게 날아가고 불꽃보다 밀랍이 더 밝아 보인다.
                    // 전용 빛 CandleSelfLight(레이어 7 = 128)만 받게 해 불꽃 쪽이 가장 밝고 아래로 어두워지게 한다(2026-10-31).
                    bool candleBody = r.name == "Model" && r.GetComponentInParent<LGH.HeldCandle>() != null;
                    r.renderingLayerMask = candleBody ? HeldCandleBodyLayers : AllLayers;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[NightExterior] 적용 완료: 밤하늘 스카이박스, Trilight 환경광, 안개 0.018, 달빛 0.5, 가로등 조명 보충 {added}개 + 깜빡임, 분수 불빛, 야외 렌더러 {probesOff}개 프로브 끔, 플레이어 카메라 Skybox.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
