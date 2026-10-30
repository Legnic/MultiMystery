using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CoopDemo.EditorTools
{
    // 안방(MasterRoomLightingWindow)에서 시험한 방식을 건물의 나머지 방 전체에 적용한다 (2026-10-30).
    //  - 방 경계는 Ravenwood_LGH/FloorN/Walls 의 벽 이름(F1_Ext_Study_E_9_...)에서 방별로 모아 정수로 반올림해 얻는다.
    //  - 방마다: 고정 표면 Contribute GI, 라이트 프로브 격자, 베이크 점광원(긴 방은 여러 개), 반사 프로브, 방 전용 Volume(ACES, +0.75EV).
    //  - 안방(F2_Master)은 MasterRoomLightingWindow가 이미 처리했으므로 건너뛴다. 재실행하면 방별 조명 묶음을 지우고 다시 만든다.
    //  - 움직이거나 모양이 바뀌는 것(문, 서랍, 스크립트가 붙은 기믹 가구, Animator, Rigidbody)과 반투명(유리)은 굽지 않는다.
    // 라이트맵 UV가 없는 메시는 이어서 'Master Room - Add Lightmap UVs'가 사본 메시로 채운다.
    public static class BuildingLightingBuilder
    {
        const string RootName = "BuildingLighting";
        const string VolumePath = "Assets/Settings/KSM/MasterRoom/MasterRoomVolume.asset";
        static readonly string[] SkipRooms = { "F2_Master" };   // MasterRoomLighting 이 담당

        class Room
        {
            public string key;          // F1_Study
            public int floor;           // 1, 2, 0(다락)
            public Vector3 min, max;
            public Bounds Bounds { get { return new Bounds((min + max) * 0.5f, max - min); } }
        }

        [MenuItem("Tools/KSM/Lighting/Building - Prepare Other Rooms")]
        public static void PrepareAll()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Develop.unity") { Debug.LogError("[BuildingLighting] Develop 씬에서만 실행합니다."); return; }
            var rooms = FindRooms(scene);
            var rw = scene.GetRootGameObjects().First(g => g.name == "Ravenwood_LGH");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null) { Debug.LogError("[BuildingLighting] 안방 Volume 프로필이 없습니다. 먼저 Master Room Prepare를 실행하세요."); return; }

            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            Physics.SyncTransforms();
            var renderers = rw.GetComponentsInChildren<MeshRenderer>(true);
            var flagged = new HashSet<MeshRenderer>();
            var report = new System.Text.StringBuilder();
            int totalProbes = 0, totalLights = 0;

            foreach (var room in rooms)
            {
                var b = room.Bounds;
                var group = new GameObject("Lighting_" + room.key);
                group.transform.SetParent(root.transform, false);
                group.transform.position = b.center;

                int statics = 0;
                foreach (var r in renderers)
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || !b.Intersects(r.bounds)) continue;
                    if (!IsBakeable(r)) continue;
                    if (flagged.Add(r)) statics++;
                }

                int probes = AddProbes(group.transform, room);
                int lights = AddLights(group.transform, room);
                AddReflectionProbe(group.transform, room);
                AddVolume(group.transform, room, profile);
                totalProbes += probes; totalLights += lights;
                report.AppendLine($"  {room.key}: {room.min:F0}~{room.max:F0}  새 고정 표면 {statics}, 프로브 {probes}, 광원 {lights}");
            }

            foreach (var r in flagged)
            {
                var go = r.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.ContributeGI);
                r.receiveGI = ReceiveGI.Lightmaps;
                EditorUtility.SetDirty(r); EditorUtility.SetDirty(go);
                if (PrefabUtility.IsPartOfPrefabInstance(r)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[BuildingLighting] 방 {rooms.Count}곳 준비: 새 고정 표면 {flagged.Count}, 프로브 {totalProbes}, 광원 {totalLights}.\n{report}" +
                      "이어서 'Master Room - Add Lightmap UVs' 후 베이크하세요.");
        }

        [MenuItem("Tools/KSM/Lighting/Building - Bake Current Scene")]
        static void Bake()
        {
            if (Lightmapping.isRunning) { Debug.LogWarning("[BuildingLighting] 이미 베이크 중입니다."); return; }
            if (!Lightmapping.BakeAsync()) Debug.LogError("[BuildingLighting] 베이크를 시작하지 못했습니다.");
        }

        // 반투명(유리)과 움직이거나 스크립트가 붙은 것은 굽지 않는다.
        static bool IsBakeable(MeshRenderer r)
        {
            foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue >= 3000) return false;
            if (r.GetComponentInParent<MonoBehaviour>(true) != null) return false;   // 문/서랍/기믹 장치/들 수 있는 물건 등
            if (r.GetComponentInParent<Rigidbody>(true) != null) return false;
            if (r.GetComponentInParent<Animator>(true) != null) return false;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null;
        }

        static List<Room> FindRooms(Scene scene)
        {
            var rw = scene.GetRootGameObjects().First(g => g.name == "Ravenwood_LGH").transform;
            var rx = new Regex(@"^(F1|F2|AT)_(Ext|Int)_([A-Za-z0-9]+)_");
            var dict = new Dictionary<string, Bounds>();
            foreach (var path in new[] { "Floor1/Walls", "Floor2/Walls", "Attic/Walls" })
            {
                var walls = rw.Find(path);
                if (walls == null) continue;
                foreach (Transform c in walls)
                {
                    var m = rx.Match(c.name);
                    var r = c.GetComponent<Renderer>();
                    if (!m.Success || r == null) continue;
                    string key = m.Groups[1].Value + "_" + m.Groups[3].Value;
                    if (dict.TryGetValue(key, out var bb)) { bb.Encapsulate(r.bounds); dict[key] = bb; }
                    else dict[key] = r.bounds;
                }
            }
            var list = new List<Room>();
            foreach (var kv in dict.OrderBy(k => k.Key))
            {
                if (SkipRooms.Contains(kv.Key)) continue;
                int floor = kv.Key.StartsWith("F1") ? 1 : kv.Key.StartsWith("F2") ? 2 : 0;
                float y0 = floor == 1 ? 0f : floor == 2 ? 3.9f : 7.8f;
                float y1 = floor == 1 ? 3.6f : floor == 2 ? 7.5f : 10.6f;
                var bd = kv.Value;
                list.Add(new Room
                {
                    key = kv.Key, floor = floor,
                    min = new Vector3(Mathf.Round(bd.min.x), y0, Mathf.Round(bd.min.z)),
                    max = new Vector3(Mathf.Round(bd.max.x), y1, Mathf.Round(bd.max.z)),
                });
            }
            return list;
        }

        static int AddProbes(Transform parent, Room room)
        {
            var go = new GameObject("LightProbes");
            go.transform.SetParent(parent, false);
            var group = go.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            for (float x = room.min.x + 0.5f; x < room.max.x; x += 2f)
            for (float z = room.min.z + 0.5f; z < room.max.z; z += 2f)
            foreach (float h in new[] { 0.3f, 1.2f, 2.3f, 3.1f })
            {
                var p = new Vector3(x, room.min.y + h, z);
                if (p.y >= room.max.y - 0.1f || Physics.CheckSphere(p, 0.12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                positions.Add(go.transform.InverseTransformPoint(p));
            }
            if (positions.Count < 4)   // 좁은 공간은 가운데에 최소한만
            {
                positions.Clear();
                var c = room.Bounds.center;
                foreach (float h in new[] { 0.5f, 1.5f, 2.6f })
                    positions.Add(go.transform.InverseTransformPoint(new Vector3(c.x, room.min.y + h, c.z)));
                positions.Add(go.transform.InverseTransformPoint(new Vector3(c.x + 0.5f, room.min.y + 1.5f, c.z + 0.5f)));
            }
            group.probePositions = positions.ToArray();
            return positions.Count;
        }

        // 긴 방은 긴 축을 따라 14m마다 하나씩. 광원은 안방 시험값(세기 1.5, 간접 배율 4)을 그대로 쓴다.
        static int AddLights(Transform parent, Room room)
        {
            var size = room.max - room.min;
            bool alongX = size.x >= size.z;
            float longSide = alongX ? size.x : size.z;
            int n = Mathf.Max(1, Mathf.RoundToInt(longSide / 14f));
            float seg = longSide / n;
            var c = room.Bounds.center;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n - 0.5f;
                var p = new Vector3(alongX ? c.x + t * longSide : c.x, room.min.y + 2.4f, alongX ? c.z : c.z + t * longSide);
                p = FindFreePoint(p, room);
                var go = new GameObject(n == 1 ? "WarmIndirectKey" : "WarmIndirectKey_" + i);
                go.transform.SetParent(parent, false);
                go.transform.position = p;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.color = new Color(1f, 0.82f, 0.65f);
                light.intensity = 1.5f;
                light.range = Mathf.Max(seg, Mathf.Min(size.x, size.z)) * 0.75f;
                light.bounceIntensity = 4f;
                light.shadows = LightShadows.Soft;
            }
            return n;
        }

        // 가구/벽 속에 광원이 들어가지 않게 높이와 옆으로 조금씩 옮겨 본다.
        static Vector3 FindFreePoint(Vector3 p, Room room)
        {
            if (!Physics.CheckSphere(p, 0.25f, ~0, QueryTriggerInteraction.Ignore)) return p;
            foreach (float dy in new[] { 0f, 0.4f, -0.4f, 0.8f, -0.8f })
            foreach (float r in new[] { 0f, 0.8f, 1.6f })
            for (int k = 0; k < (r == 0f ? 1 : 8); k++)
            {
                float a = k * Mathf.PI / 4f;
                var q = p + new Vector3(Mathf.Cos(a) * r, dy, Mathf.Sin(a) * r);
                if (q.y > room.max.y - 0.3f || q.y < room.min.y + 1f) continue;
                if (!Physics.CheckSphere(q, 0.25f, ~0, QueryTriggerInteraction.Ignore)) return q;
            }
            return p;
        }

        static void AddReflectionProbe(Transform parent, Room room)
        {
            var b = room.Bounds;
            var go = new GameObject("RoomReflectionProbe");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(b.center.x, room.min.y + 1.6f, b.center.z);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = b.size;
            probe.center = new Vector3(0f, b.center.y - go.transform.position.y, 0f);
            probe.resolution = 128;
            probe.hdr = true;
            probe.intensity = 1f;
            probe.blendDistance = 1f;
            probe.importance = 1;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        }

        static void AddVolume(Transform parent, Room room, VolumeProfile profile)
        {
            var go = new GameObject("LocalVolume");
            go.transform.SetParent(parent, false);
            go.transform.position = room.Bounds.center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = room.Bounds.size;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 10f;
            volume.blendDistance = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            go.AddComponent<RoomExposureOptions>().baseExposureEV = 0.75f;
        }
    }
}
