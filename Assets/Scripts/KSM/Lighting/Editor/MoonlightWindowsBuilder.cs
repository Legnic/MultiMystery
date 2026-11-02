using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopDemo.EditorTools
{
    // 창가 달빛(2026-10-30). 건물 바깥 창(F1/F2/AT_Ext_*_Glass*)마다 실시간 스포트라이트 하나(그림자 없음)로 창틀 모양 달빛 무늬를 바닥에 만든다.
    //  - 달빛 방향은 기존 방향광 Moonlight_LGH와 같다(아래 32°, 방향 150°) → 실제 창 틈으로 들어오는 빛 무늬와 겹친다.
    //  - 광원을 창 바로 앞에 두면 원뿔이 넓어 무늬가 늘어지므로, 달빛 방향으로 8m 뒤(창 바깥)에 둔 "먼 광원"으로 거의 평행한 빛을 흉내 낸다.
    //  - 스포트 원뿔은 원형이라 쿠키의 창은 텍스처 가운데 68%만 쓴다(모서리가 잘리지 않게).
    //  - 그림자가 없어서 빛이 벽을 통과한다. 그래서 (1) 달빛이 실제로 방 안으로 들어오는 방향의 창인지(바깥쪽 법선), (2) 창에서 달빛 방향으로 쏜 첫 충돌이
    //    바닥 같은 위쪽 면인지(벽이면 옆방 바닥으로 새므로 제외)를 검사해서 통과한 창에만 놓는다.
    // 재실행해도 안전하다(씬 루트의 MoonlightWindows를 지우고 다시 만든다).
    public static class MoonlightWindowsBuilder
    {
        const string RootName = "MoonlightWindows";
        const string CookiePath = "Assets/Settings/KSM/MasterRoom/MoonWindowCookie.png";
        const float Pitch = 32f, Yaw = 150f, SourceDistance = 8f, CookieSpan = 0.68f;
        // 방향광 Moonlight_LGH(0.35)가 같은 방향의 실제 무늬를 따로 만들어서, 스포트를 100으로 두면 둘이 겹쳐 과하게 밝고 창살이 어긋나 보인다 → 60으로 낮춤.
        // 2026-10-31: 방향광이 실내를 비추지 않게 분리(NightExteriorBuilder의 렌더링 레이어)하면서 방향광이 만들던 창 무늬 밝기가 사라져 세기를 60 → 100으로 올렸었다.
        // 같은 날 광원 거리·범위를 창마다 따로 잡게 바꾸면서(아래 d, D) 세기는 "바닥 무늬 밝기" 기준으로 계산한다.
        // TargetIlluminance: 이전 세기 100(범위 18, 광원→바닥 약 10.8m)일 때의 바닥 무늬 밝기와 같아지게 맞춘 값.
        // 이론값 0.65(100 × 0.758 ÷ 10.8²)로 두면 실측이 이전의 약 80%라서(Guest1 창, 같은 시점 켬/끔 차이 0.0348 vs 0.0434) 0.8로 올렸다.
        const float TargetIlluminance = 0.8f;
        const float MinSourceDistance = 2.5f, RangeMargin = 3f;

        [MenuItem("Tools/KSM/Lighting/Moonlight - Place On Windows")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Develop.unity") { Debug.LogError("[Moonlight] Develop 씬에서만 실행합니다."); return; }
            var cookie = EnsureCookie();
            foreach (var g in scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray()) Object.DestroyImmediate(g);
            // 예전 위치(안방 라이팅 묶음 안)에 있던 것도 정리한다.
            var old = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == RootName).ToArray();
            foreach (var t in old) if (t != null) Object.DestroyImmediate(t.gameObject);

            var root = new GameObject(RootName);
            var travel = Quaternion.Euler(Pitch, Yaw, 0f);
            var dir = travel * Vector3.forward;
            Physics.SyncTransforms();

            int made = 0; var skipped = new List<string>();
            var panes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false))
                .Where(r => r.name.Contains("_Ext_") && r.name.Contains("_Glass") && (r.name.StartsWith("F1_") || r.name.StartsWith("F2_") || r.name.StartsWith("AT_")))
                .OrderBy(r => r.name).ToArray();
            foreach (var r in panes)
            {
                var b = r.bounds;
                bool facesX = b.size.x < b.size.z;            // 얇은 축 = 벽 법선 축
                var axis = facesX ? Vector3.right : Vector3.forward;
                // 안쪽 = 위쪽에 천장이 있는 쪽(바깥은 하늘이 트여 있다).
                bool ceilPlus = Physics.Raycast(b.center + axis * 1.0f, Vector3.up, 5f, ~0, QueryTriggerInteraction.Ignore);
                bool ceilMinus = Physics.Raycast(b.center - axis * 1.0f, Vector3.up, 5f, ~0, QueryTriggerInteraction.Ignore);
                if (ceilPlus == ceilMinus) { skipped.Add(r.name + " (안/밖 판정 불가)"); continue; }
                var inward = ceilPlus ? axis : -axis;
                if (Vector3.Dot(dir, inward) < 0.3f) { skipped.Add(r.name + " (달빛이 들어오지 않는 방향)"); continue; }
                var entry = b.center + inward * 0.12f;
                if (!Physics.Raycast(entry, dir, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                { skipped.Add(r.name + " (달빛이 8m 안에 아무것에도 닿지 않음)"); continue; }
                // 건물 벽(Walls 아래)에 먼저 막히면 옆방 바닥으로 새므로 제외한다. 가구에 막히는 건 괜찮다(가구는 방 안의 물건이다).
                if (hit.normal.y < 0.6f && PathOf(hit.collider.transform).Contains("/Walls/"))
                { skipped.Add(r.name + " (달빛이 바닥에 닿기 전에 벽에 막힘)"); continue; }

                // 광원 자리(2026-10-31): 그림자가 없어서 빔이 지나는 길의 건물 안 표면을 전부 비춘다. 창에서 달빛 반대 방향으로 최대 8m를 가되,
                // 건물(Ravenwood_LGH)에 처음 막히는 곳 앞에서 멈춘다(같은 벽 조각과 바깥 쉘은 무시). 너무 짧으면(<2.5m) 그 창은 건너뛴다.
                //  예) F2_CorridorW_E_3_Glass2는 8m 뒤가 남쪽 날개 지붕 위라서 빔이 지붕·복도 천장을 뚫고 복도 북쪽 벽 윗부분에 창 무늬를 만들었다.
                float d = SourceDistance;
                string wallPrefix = r.name.Substring(0, r.name.LastIndexOf('_'));
                var back = Physics.RaycastAll(b.center + inward * 0.15f, -dir, SourceDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(back, (x, y) => x.distance.CompareTo(y.distance));
                foreach (var bh in back)
                {
                    if (bh.distance < 0.35f || bh.collider.name.StartsWith(wallPrefix)) continue;
                    if (!PathOf(bh.collider.transform).Contains("/Ravenwood_LGH/")) continue;
                    d = Mathf.Min(d, bh.distance - 0.3f);
                    break;
                }
                if (d < MinSourceDistance) { skipped.Add(r.name + " (광원을 둘 바깥 공간이 " + d.ToString("F1") + "m뿐)"); continue; }
                // 창에서 바닥까지 거리를 더한 "광원 → 바닥 무늬" 거리. 무늬 크기·범위·세기를 이 거리로 맞춘다.
                float D = d + 0.15f + hit.distance;

                var go = new GameObject("Moon_" + r.name);
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(b.center + inward * 0.15f - dir * d, travel);
                float side = Mathf.Max(Mathf.Max(b.size.x, b.size.z), b.size.y);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                // 점광원이라 바닥에서는 무늬가 (D/창까지 거리)배로 커진다 → 창 평면이 아니라 "바닥 무늬"가 창 크기가 되도록 각도를 잡는다(평행광과 같은 크기).
                l.spotAngle = 2f * Mathf.Atan(side / CookieSpan * 0.5f / D) * Mathf.Rad2Deg;
                l.innerSpotAngle = l.spotAngle * 0.9f;
                // 범위는 바닥 무늬 바로 뒤까지만: 더 길면 빔이 바닥·천장을 뚫고 아래층 벽·가구(1층 벽, 식물 등)까지 비춘다.
                l.range = D + RangeMargin;
                // 바닥 무늬 밝기를 거리와 상관없이 같게(= TargetIlluminance): 역제곱 감쇠와 범위 가장자리 감쇠를 보정한다.
                float fall = 1f - Mathf.Pow(D / l.range, 4f);
                l.intensity = TargetIlluminance * D * D / Mathf.Max(0.05f, fall * fall);
                l.color = new Color(0.55f, 0.68f, 1f);
                l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.renderMode = LightRenderMode.ForcePixel;
                l.cookie = cookie;
                made++;
            }
            var culling = root.AddComponent<MoonlightWindowCulling>();
            culling.sourceDistance = SourceDistance;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Moonlight] 창 {panes.Length}개 중 {made}개에 달빛을 놓았습니다. 제외 {skipped.Count}개:\n" + string.Join("\n", skipped));
        }

        static string PathOf(Transform t)
        {
            string path = "/" + t.name + "/";
            for (var p = t.parent; p != null; p = p.parent) path = "/" + p.name + path;
            return path;
        }

        // 창틀(2x2 칸) 쿠키. 텍스처 가운데 68%에 창을 그리고 나머지는 검게 둔다. 이미 있으면 그대로 쓴다.
        // 주의: 쿠키 형식(Cookie, 알파 8비트)으로 가져오면 이 URP 설정에서 빛이 통째로 사라진다 → 일반 텍스처로 가져온다.
        static Texture2D EnsureCookie()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
            if (existing != null) return existing;
            const int n = 256; const float margin = (1f - CookieSpan) * 0.5f;
            float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = ((x + 0.5f) / n - margin) / CookieSpan, v = ((y + 0.5f) / n - margin) / CookieSpan, a = 0f;
                if (u > 0f && u < 1f && v > 0f && v < 1f)
                {
                    float dEdge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    float dBar = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                    a = Smooth(0.058f, 0.082f, dEdge) * Smooth(0.01f, 0.034f, dBar);
                }
                tex.SetPixel(x, y, new Color(a, a, a, 1f));
            }
            tex.Apply();
            System.IO.File.WriteAllBytes(CookiePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CookiePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
        }
    }
}
