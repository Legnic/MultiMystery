using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoopDemo.EditorTools
{
    // Builds the placeholder gesture clip(s) + Animator Controller for the "face emote hand"
    // (LHJ's Hand_Wave_Rigged) and wires an instance under NetworkPlayer_LGH's Main Camera.
    // Re-runnable (same pattern as AvatarBuilder): clips/controller are rebuilt from code every
    // time, prefab wiring is only added if missing.
    //
    // 2026-10-27: team decided finger-curl gestures (peace/thumbsUp/point/middleFinger) looked
    // too distorted when posed by rotating joints in code - a teammate will animate those properly
    // in Blender instead. Only "Wave" (no finger curling, just a wrist rock) stays as a code-driven
    // placeholder for now. When the Blender clips arrive, drop them into OutDir with the same
    // naming scheme and extend BuildClips/BuildController the way this file did before this round
    // (see git history) rather than hand-posing joints again.
    public static class EmoteHandBuilder
    {
        const string HandFbx = "Assets/VARCO 3D/LHJ/Hands/Hand_Wave_Rigged.fbx";
        const string OutDir = "Assets/Animations/KSM/EmoteHand";
        const string PlayerPrefab = "Assets/Objects/LGH/NetworkPlayer_LGH.prefab";

        // 실제 게임에서 EmoteHand 오브젝트에 거는 회전과 반드시 같아야 한다 - 손목 흔들기(WaveAxis)는 세계축이라
        // 이 값이 다르면 전혀 다른 방향으로 돈다(실제로 한 번 빠뜨렸다가 "인사"가 거의 안 움직이는 버그가 났었음).
        static readonly Quaternion HolderRotation = Quaternion.Euler(0f, 180f, 0f);

        [MenuItem("Tools/KSM/EmoteHand/Build All")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Animations/KSM", "EmoteHand");

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HandFbx);
            if (fbx == null) { Debug.LogError("[EmoteHand] Hand_Wave_Rigged.fbx를 찾지 못했어요: " + HandFbx); return; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            inst.transform.localRotation = HolderRotation;
            try
            {
                var clips = BuildClips(inst);
                var controller = BuildController(clips);
                WirePlayerPrefab(controller);
            }
            finally { Object.DestroyImmediate(inst); }

            // 이전 라운드에서 만들었던 손가락 몸짓 클립은 더 이상 쓰지 않음 - 팀원이 Blender로 다시 만들 예정.
            foreach (var name in new[] { "Emote_Peace", "Emote_ThumbsUp", "Emote_Point", "Emote_MiddleFinger" })
                AssetDatabase.DeleteAsset(OutDir + "/" + name + ".anim");

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerPrefab, ImportAssetOptions.ForceUpdate);
            Debug.Log("[EmoteHand] Build All 완료: 인사 클립만 유지 + Animator + 플레이어 프리팹 연결.");
        }

        // ---------- 몸짓 클립 ----------

        static (AnimationClip idle, AnimationClip wave) BuildClips(GameObject inst)
        {
            Transform Find(string n) => inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            var hand = Find("Hand");
            if (hand == null)
            {
                Debug.LogError("[EmoteHand] 'Hand' 뼈를 찾지 못했어요 (이름이 바뀌었을 수 있어요).");
                return (null, null);
            }

            var restHand = hand.localRotation;

            AnimationClip MakeClip(string name, float[] times, System.Func<float, Quaternion> poseAt)
            {
                var clip = new AnimationClip { name = name, frameRate = 30 };
                var quats = times.Select(poseAt).ToArray();
                hand.localRotation = restHand;
                string p = AnimationUtility.CalculateTransformPath(hand, inst.transform);
                SetQuatCurves(clip, p, times, quats);
                var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, s);
                return clip;
            }

            var idle = MakeClip("Emote_Idle", new[] { 0f, 1f }, _ => restHand);

            // 인사: 손가락은 그대로(기본 자세가 이미 펼친 손) 손목만 좌우로 흔든다.
            Vector3 waveAxis = hand.forward;
            var waveTimes = new[] { 0f, 0.25f, 0.55f, 0.85f, 1.15f, 1.45f, 1.6f };
            var waveAngles = new[] { 0f, 22f, -22f, 22f, -22f, 10f, 0f };
            var wave = MakeClip("Emote_Wave", waveTimes, t =>
            {
                hand.localRotation = restHand;
                int i = System.Array.IndexOf(waveTimes, t);
                Pose(hand, waveAxis, waveAngles[i]);
                return hand.localRotation;
            });

            void Save(AnimationClip c)
            {
                string path = OutDir + "/" + c.name + ".anim";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(c, path);
            }
            Save(idle); Save(wave);
            return (idle, wave);
        }

        static void Pose(Transform bone, Vector3 axisWorld, float angle)
        {
            if (bone == null || Mathf.Abs(angle) < 0.001f) return;
            Quaternion pw = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
            bone.localRotation = Quaternion.Inverse(pw) * Quaternion.AngleAxis(angle, axisWorld) * pw * bone.localRotation;
        }

        static void SetQuatCurves(AnimationClip clip, string path, float[] times, Quaternion[] q)
        {
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
            Quaternion prev = q[0];
            for (int i = 0; i < times.Length; i++)
            {
                var cur = q[i];
                if (Quaternion.Dot(prev, cur) < 0f) cur = new Quaternion(-cur.x, -cur.y, -cur.z, -cur.w);
                prev = cur;
                cx.AddKey(times[i], cur.x); cy.AddKey(times[i], cur.y); cz.AddKey(times[i], cur.z); cw.AddKey(times[i], cur.w);
            }
            foreach (var cv in new[] { cx, cy, cz, cw }) for (int i = 0; i < cv.length; i++) cv.SmoothTangents(i, 0f);
            clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
        }

        // ---------- Animator Controller ----------

        static AnimatorController BuildController((AnimationClip idle, AnimationClip wave) clips)
        {
            string path = OutDir + "/EmoteHand.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Wave", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = clips.idle;
            idle.writeDefaultValues = true;
            sm.defaultState = idle;

            var wave = sm.AddState("Wave");
            wave.motion = clips.wave;
            wave.writeDefaultValues = true;

            var toWave = idle.AddTransition(wave);
            toWave.hasExitTime = false;
            toWave.duration = 0.08f;
            toWave.AddCondition(AnimatorConditionMode.If, 0f, "Wave");

            var back = wave.AddTransition(idle);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.12f;

            // 연타하면 처음부터 다시 재생
            var again = wave.AddTransition(wave);
            again.hasExitTime = false;
            again.duration = 0.05f;
            again.AddCondition(AnimatorConditionMode.If, 0f, "Wave");

            return ctrl;
        }

        // ---------- 플레이어 프리팹 연결 ----------

        static void WirePlayerPrefab(AnimatorController controller)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HandFbx);
            if (fbx == null) return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var cam = root.transform.Find("Main Camera");
                if (cam == null) { Debug.LogError("[EmoteHand] 플레이어 프리팹에 Main Camera가 없어요."); return; }

                var holder = cam.Find("EmoteHand");
                if (holder == null)
                {
                    // 클립의 뼈 경로("Hand_Armature/Hand/...")가 Animator가 붙은 오브젝트 기준이므로,
                    // 별도 빈 껍데기를 만들지 않고 FBX 인스턴스 자체를 "EmoteHand"로 쓴다(AvatarBuilder와 같은 방식).
                    var rig = (GameObject)PrefabUtility.InstantiatePrefab(fbx, cam);
                    rig.name = "EmoteHand";
                    holder = rig.transform;

                    var animator = rig.AddComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    var view = rig.AddComponent<EmoteHandView>();
                    var so = new SerializedObject(view);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // 2026-10-27 피드백: 얼굴 앞이 시야를 가려서 몸쪽(아래·옆)으로 내림 - 재실행해도 항상 이 값으로
                // 맞춰지도록 if(holder==null) 밖에 둔다. HeldCandle(0.22,-0.15,0.67)과 겹치지 않는 반대쪽.
                // scale=1(원본 FBX 크기)이면 폭이 1.2m짜리 거대한 손이 돼서 화면을 거의 다 가렸던 게 "시야를
                // 가리는 문제"의 진짜 원인이었음(스크린샷으로 직접 확인) - 실제 사람 손 크기(폭 약 18cm)에
                // 맞춰 0.22로 줄임.
                holder.localPosition = new Vector3(-0.20f, -0.05f, 0.42f);
                holder.localRotation = HolderRotation;
                holder.localScale = Vector3.one * 0.22f;

                var anim = holder.GetComponent<Animator>();
                if (anim != null) anim.runtimeAnimatorController = controller;

                // 마우스 가운데 버튼 도넛 메뉴 - 루트(FirstPersonController와 같은 자리)에 붙이고, 다른 소유자
                // 전용 스크립트들(FirstPersonController/PlayerInteractor)과 같은 방식으로 NetworkPlayer.ownerOnly에
                // 등록해서 상대 화면에서는 아예 꺼져 있게 한다.
                var wheel = root.GetComponent<EmoteWheelUI>();
                if (wheel == null) wheel = root.AddComponent<EmoteWheelUI>();
                var netPlayer = root.GetComponent<LGH.NetworkPlayer>();
                if (netPlayer != null)
                {
                    var npSo = new SerializedObject(netPlayer);
                    var arr = npSo.FindProperty("ownerOnly");
                    bool already = false;
                    for (int i = 0; i < arr.arraySize; i++)
                        if (arr.GetArrayElementAtIndex(i).objectReferenceValue == (Behaviour)wheel) already = true;
                    if (!already)
                    {
                        arr.InsertArrayElementAtIndex(arr.arraySize);
                        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = wheel;
                        npSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                else Debug.LogWarning("[EmoteHand] LGH.NetworkPlayer를 못 찾아서 EmoteWheelUI를 ownerOnly에 등록하지 못했어요.");

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
