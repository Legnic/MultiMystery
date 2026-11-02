using System;
using System.Collections.Generic;
using System.Linq;
using Gim007;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CoopDemo.EditorTools
{
    // 자동 실행하지 않는다. Develop을 열고 방 경계를 확인한 뒤 Prepare/Bake를 실행한다.
    public class MasterRoomLightingWindow : EditorWindow
    {
        const string AssetRoot = "Assets/Settings/KSM/MasterRoom";
        const string SetupName = "MasterRoomLighting";
        // 기존 Gim007Builder가 기록한 Develop Master 방의 월드 경계.
        Vector3 roomMin = new Vector3(20f, 3.9f, 3f);
        Vector3 roomMax = new Vector3(36f, 7.5f, 16f);
        float exposure = 0.75f;
        bool baking;
        Scene bakeScene;

        [MenuItem("Tools/KSM/Lighting/Master Room")]
        static void Open() { GetWindow<MasterRoomLightingWindow>("Master Room Lighting"); }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Master 방의 월드 경계를 확인하세요. Prepare는 씬 인스턴스와 전용 에셋을 설정합니다. " +
                "Unity 베이크는 현재 씬 전체에 적용되며 기존 다른 방의 베이크 대상은 유지합니다. " +
                "태양/촛불은 변경하지 않고 고정 Mixed 보조광을 추가합니다.", MessageType.Info);
            roomMin = EditorGUILayout.Vector3Field("Room min (world)", roomMin);
            roomMax = EditorGUILayout.Vector3Field("Room max (world)", roomMax);
            exposure = EditorGUILayout.Slider("Room exposure (EV)", exposure, -1f, 2f);
            EditorGUILayout.LabelField("Tonemapping", "ACES / local Volume, priority 10");
            EditorGUILayout.LabelField("Bake", "Unity Compute GPU / 20 texels/m / 512 indirect samples / 4 bounces");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning))
            {
                if (GUILayout.Button("1. Prepare Master Room")) Run(Prepare);
                if (GUILayout.Button("2. Bake Current Scene")) Run(Bake);
            }
            if (Lightmapping.isRunning && GUILayout.Button("Cancel Bake")) Lightmapping.Cancel();
            EditorGUILayout.HelpBox("완료 후 가구 이동·문 열기·낮/밤·밝기 옵션을 Play 모드에서 확인하고 씬을 저장하세요. " +
                "노출 값은 시작점이며 실제 화면에서 조정해야 합니다.", MessageType.None);
        }

        void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static T[] InScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        }

        Bounds RoomBounds()
        {
            if (roomMax.x <= roomMin.x || roomMax.y <= roomMin.y || roomMax.z <= roomMin.z)
                throw new InvalidOperationException("방의 min/max 범위를 확인하세요.");
            return new Bounds((roomMin + roomMax) * 0.5f, roomMax - roomMin);
        }

        static Scene DevelopScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Develop.unity")
                throw new InvalidOperationException("Assets/Scenes/Develop.unity를 먼저 여세요. 다른 씬은 수정하지 않습니다.");
            return scene;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static void Changed(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        static GameObject Child(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null) return found.gameObject;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Master room lighting");
            go.transform.SetParent(parent, false);
            return go;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var value = go.GetComponent<T>();
            return value != null ? value : Undo.AddComponent<T>(go);
        }

        void Prepare()
        {
            var scene = DevelopScene();
            var bounds = RoomBounds();
            var gimmick = InScene<Gim007Furniture>(scene).SingleOrDefault(g => g.transform.root.name == "GIM007_MasterBedroom");
            if (gimmick == null || !bounds.Contains(gimmick.Body.position))
                throw new InvalidOperationException("GIM007_MasterBedroom/Furniture 또는 방 경계가 맞지 않습니다. 배치를 먼저 확인하세요.");
            var decor = InScene<Transform>(scene).SingleOrDefault(t => t.name == "F2_Master");
            if (decor == null) throw new InvalidOperationException("F2_Master 장식 루트를 찾을 수 없습니다.");
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null || new SerializedObject(pipeline).FindProperty("m_LightProbeSystem")?.intValue != 0)
                throw new InvalidOperationException("이 도구는 현재 프로젝트의 URP 일반 Light Probe 모드용입니다. APV 모드에서는 실행하지 마세요.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare Master room lighting");
            EnsureFolder(AssetRoot);
            var setup = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SetupName);
            if (setup == null)
            {
                setup = new GameObject(SetupName);
                Undo.RegisterCreatedObjectUndo(setup, "Master room lighting");
            }
            // 스케일/회전에 의존하지 않는 월드 경계. 재실행해도 배치가 누적되지 않는다.
            Undo.RecordObject(setup.transform, "Master room lighting");
            setup.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
            setup.transform.localScale = Vector3.one;

            int statics = 0;
            foreach (var r in InScene<MeshRenderer>(scene))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !bounds.Intersects(r.bounds)) continue;
                if (r.transform.IsChildOf(gimmick.transform.root) || r.GetComponentInParent<Rigidbody>() != null ||
                    r.GetComponentInParent<LGH.InteractableDoor>() != null || r.GetComponentInParent<LGH.InteractableDrawer>() != null ||
                    r.GetComponentInParent<LGH.WorldItem>() != null) continue;
                // F2_Master 장식과 방 경계에 걸친 건물 표면만 대상으로 한다.
                if (!r.transform.IsChildOf(decor) && r.transform.root.name != "Ravenwood_LGH") continue;
                Undo.RecordObject(r.gameObject, "Contribute GI");
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.ContributeGI);
                Undo.RecordObject(r, "Receive lightmaps");
                r.receiveGI = ReceiveGI.Lightmaps;
                Changed(r); Changed(r.gameObject);
                statics++;
            }

            var moving = gimmick.Body.GetComponentsInChildren<Renderer>(true);
            if (moving.Length == 0) throw new InvalidOperationException("움직이는 가구 렌더러를 찾을 수 없습니다.");
            var movingBounds = moving[0].bounds;
            foreach (var r in moving) movingBounds.Encapsulate(r.bounds);
            var anchor = Child(gimmick.Body, "LightingProbeAnchor").transform;
            Undo.RecordObject(anchor, "Probe anchor");
            anchor.position = movingBounds.center;
            foreach (var t in gimmick.Body.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(t.gameObject, "Dynamic furniture");
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(t.gameObject)
                    & ~(StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic));
                Changed(t.gameObject);
            }
            foreach (var r in moving)
            {
                Undo.RecordObject(r, "Furniture probes");
                if (r is MeshRenderer mesh) mesh.receiveGI = ReceiveGI.LightProbes;
                r.lightProbeUsage = LightProbeUsage.BlendProbes;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                r.probeAnchor = anchor;
                Changed(r);
            }

            var probes = EnsureComponent<LightProbeGroup>(Child(setup.transform, "LightProbes"));
            Undo.RecordObject(probes, "Room probes");
            Physics.SyncTransforms();
            var positions = new List<Vector3>();
            for (float x = roomMin.x + 0.5f; x < roomMax.x; x += 2f)
            for (float z = roomMin.z + 0.5f; z < roomMax.z; z += 2f)
            foreach (float height in new[] { 0.3f, 1.2f, 2.3f, 3.1f })
            {
                var p = new Vector3(x, roomMin.y + height, z);
                if (p.y >= roomMax.y - 0.1f || Physics.CheckSphere(p, 0.12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                positions.Add(probes.transform.InverseTransformPoint(p));
            }
            if (positions.Count < 4) throw new InvalidOperationException("프로브를 배치할 빈 공간이 부족합니다. 방 경계를 확인하세요.");
            probes.probePositions = positions.ToArray();
            Changed(probes);

            var light = EnsureComponent<Light>(Child(setup.transform, "WarmIndirectKey"));
            Undo.RecordObject(light, "Mixed room light");
            Undo.RecordObject(light.transform, "Mixed room light position");
            // 2026-10-30: 천장 바로 밑(roomMax.y-0.5)에 두면 빛이 천장 한 점에만 몰려 방 안으로 번지는 간접광이 거의 안 보였다.
            // 바닥에서 2.4m(방 가운데 허공)로 내려서 벽·바닥으로 퍼지게 한다.
            light.transform.position = new Vector3(bounds.center.x, roomMin.y + 2.4f, bounds.center.z);
            light.type = LightType.Point;
            // 2026-10-30: Mixed(직접광은 실시간으로 남음) → Baked. 이 광원은 실시간 직접광을 더하지 않고 라이트맵/프로브에만 구워진다.
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.color = new Color(1f, 0.82f, 0.65f);
            // 2026-10-30 시험: 1.2(천장 밑)=변화 거의 없음, 4(간접 1)=방 전체가 3~16배 밝아지고 천장이 하얗게 탐,
            // 1.5(간접 배율 4)=촛불 분위기를 유지하며 바닥·가구가 은은하게 보임 → 1.5 / 4를 기본값으로 한다.
            light.intensity = 1.5f;
            light.range = Mathf.Max(bounds.size.x, bounds.size.z) * 0.75f;
            light.bounceIntensity = 4f;   // 간접광(번짐) 배율. 직접광(천장 빛 무리)은 줄이고 번짐을 키운다.
            light.shadows = LightShadows.Soft;
            Changed(light);

            ConfigureReflectionProbe(setup, bounds);
            ConfigureBaking();
            ConfigureVolume(setup, bounds);
            EnablePlayerPostProcessing(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = setup;
            Debug.Log($"[MasterLighting] 설정 준비: 고정 표면 {statics}, 이동 가구 렌더러 {moving.Length}, 프로브 {positions.Count}. 아직 베이크 전입니다. 경계/보조광 위치를 확인한 뒤 Bake를 실행하세요.");
        }

        // 방 하나에 반사 프로브 하나(2026-10-30). 박스 투영으로 방 모양에 맞는 반사를 만들고, Baked라 라이트맵과 함께 구워진다.
        // 프로브 큐브맵은 베이크(Bake Current Scene)할 때 같이 만들어진다. 기본 반사(RenderSettings, 강도 0.05)는 거의 꺼져 있어서
        // 이 프로브가 없으면 방 안 표면에는 사실상 반사가 없다.
        void ConfigureReflectionProbe(GameObject setup, Bounds bounds)
        {
            var go = Child(setup.transform, "RoomReflectionProbe");
            var probe = EnsureComponent<ReflectionProbe>(go);
            Undo.RecordObject(probe, "Room reflection probe");
            Undo.RecordObject(go.transform, "Room reflection probe position");
            go.transform.position = new Vector3(bounds.center.x, roomMin.y + 1.6f, bounds.center.z);   // 서 있는 눈높이 근처
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = new Vector3(bounds.size.x, bounds.size.y, bounds.size.z);
            probe.center = new Vector3(0f, bounds.center.y - go.transform.position.y, 0f);   // 박스를 방 경계에 맞춘다
            probe.resolution = 128;
            probe.hdr = true;
            probe.intensity = 1f;
            probe.blendDistance = 1f;
            probe.importance = 1;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            Changed(probe);
        }

        void ConfigureBaking()
        {
            string path = AssetRoot + "/MasterRoom.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, path);
            }
            Undo.RecordObject(settings, "Master lighting settings");
            settings.bakedGI = true;
            settings.realtimeGI = false;
            // 2026-10-30: ProgressiveCPU는 이 Unity 버전(6000.6)에서 폐지되어(컴파일 경고) GPU 베이커로 바꿨다.
            settings.lightmapper = LightingSettings.Lightmapper.UnityComputeGPU;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapResolution = 20f;
            settings.lightmapMaxSize = 2048;
            settings.directSampleCount = 64;
            settings.indirectSampleCount = 512;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 4;
            Lightmapping.lightingSettings = settings;
            Changed(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        void ConfigureVolume(GameObject setup, Bounds bounds)
        {
            string path = AssetRoot + "/MasterRoomVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet<ColorAdjustments>(out var color))
            { color = profile.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(color, profile); }
            if (!profile.TryGet<Tonemapping>(out var tone))
            { tone = profile.Add<Tonemapping>(); AssetDatabase.AddObjectToAsset(tone, profile); }
            Undo.RecordObject(color, "Room exposure");
            Undo.RecordObject(tone, "Room tonemapping");
            color.postExposure.Override(exposure);
            tone.mode.Override(TonemappingMode.ACES);
            Changed(color); Changed(tone); Changed(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            var go = Child(setup.transform, "LocalVolume");
            var box = EnsureComponent<BoxCollider>(go);
            Undo.RecordObject(box, "Room volume bounds");
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = bounds.size;
            var volume = EnsureComponent<Volume>(go);
            Undo.RecordObject(volume, "Local room volume");
            volume.isGlobal = false;
            volume.priority = 10f;
            volume.blendDistance = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            var options = EnsureComponent<RoomExposureOptions>(go);
            Undo.RecordObject(options, "Room exposure options");
            options.baseExposureEV = exposure;
            Changed(box); Changed(volume); Changed(options);
        }

        static void EnableCamera(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObject(data, "Enable room post processing");
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1; // local Volume uses Default layer
            Changed(data);
        }

        static void EnablePlayerPostProcessing(Scene scene)
        {
            foreach (var camera in InScene<Camera>(scene)) EnableCamera(camera);
            // 실제 네트워크 플레이어는 런타임 생성되므로 등록된 스폰 프리팹의 카메라도 설정한다.
            foreach (var nm in InScene<NetworkManager>(scene))
            {
                if (nm.NetworkConfig.PlayerPrefab == null) continue;
                string path = AssetDatabase.GetAssetPath(nm.NetworkConfig.PlayerPrefab);
                if (string.IsNullOrEmpty(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true)) EnableCamera(camera);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        void Bake()
        {
            var scene = DevelopScene();
            if (!scene.GetRootGameObjects().Any(g => g.name == SetupName))
                throw new InvalidOperationException("먼저 Prepare Master Room을 실행하세요.");
            bakeScene = scene;
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
            baking = true;
            if (!Lightmapping.BakeAsync())
            {
                baking = false;
                Lightmapping.bakeCompleted -= OnBakeCompleted;
                throw new InvalidOperationException("Unity가 베이크를 시작하지 못했습니다. Lighting/Console을 확인하세요.");
            }
        }

        void OnBakeCompleted()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            baking = false;
            if (bakeScene.IsValid() && bakeScene.isLoaded) EditorSceneManager.MarkSceneDirty(bakeScene);
            Debug.Log("[MasterLighting] 베이크 종료. Console의 오류, 라이트맵/프로브 생성 및 실제 플레이 화면을 확인한 뒤 씬을 저장하세요.");
            Repaint();
        }

        void OnDisable()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            if (baking) Debug.LogWarning("[MasterLighting] 창이 닫혔습니다. 베이크 상태는 Lighting 창에서 확인하고 완료 후 씬을 저장하세요.");
        }
    }
}
