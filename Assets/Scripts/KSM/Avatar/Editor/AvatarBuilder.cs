using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoopDemo.EditorTools
{
    // 파랑이/주황이를 플레이어 아바타로 쓸 수 있게 만든다. 재실행 가능(같은 경로의 에셋을 덮어씀).
    //
    //  1) Assets/Prefabs/KSM/Avatar/Avatar_Orange|Blue.prefab : 리깅 FBX + 입 모양(MouthMesh) + 재질 + Animator + CharacterAvatarModel
    //  2) Avatar_Orange|Blue.controller + *_Idle.anim + *_Interact.anim : 상호작용(손 뻗기) Animator
    //  3) NetworkPlayer_LGH.prefab 의 Avatar 오브젝트에 CharacterAvatar를 붙이고 두 프리팹을 연결
    //
    // 걷기/점프는 클립이 아니라 CharacterAvatarModel 코드가 만든다(절차적). Animator는 상호작용만 담당한다.
    public static class AvatarBuilder
    {
        const string OutDir = "Assets/Prefabs/KSM/Avatar";
        const string PlayerPrefab = "Assets/Objects/LGH/NetworkPlayer_LGH.prefab";

        struct CharInfo { public string name; public string fbx; public string mouthMesh; public string material; }
        static readonly CharInfo[] Chars =
        {
            new CharInfo { name = "Orange", fbx = "Assets/VARCO 3D/LHJ/Char_Orange/Char_Orange_Rigged.fbx", mouthMesh = "Assets/VARCO 3D/LHJ/Char_Orange/Char_Orange_MouthMesh.asset", material = "Assets/VARCO 3D/LHJ/Char_Orange/Char_Orange_Mat.mat" },
            new CharInfo { name = "Blue",   fbx = "Assets/VARCO 3D/LHJ/Char_Blue/Char_Blue_Rigged.fbx",     mouthMesh = "Assets/VARCO 3D/LHJ/Char_Blue/Char_Blue_MouthMesh.asset",     material = "Assets/VARCO 3D/LHJ/Char_Blue/Char_Blue_Mat.mat" },
        };

        [MenuItem("Tools/KSM/Avatar/Build All")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Prefabs/KSM", "Avatar");
            foreach (var c in Chars) BuildCharacter(c);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RelinkControllers();
            WirePlayerPrefab();
            AssetDatabase.SaveAssets();
            // 컨트롤러를 다시 만든 직후에는 프리팹이 컨트롤러를 "없는 것"으로 읽은 채 임포트될 수 있어서 강제로 한 번 더 임포트한다.
            foreach (var c in Chars) AssetDatabase.ImportAsset(OutDir + "/Avatar_" + c.name + ".prefab", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PlayerPrefab, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Avatar] Build All 완료: 주황이/파랑이 프리팹, Animator, 플레이어 프리팹 연결.");
        }

        static void BuildCharacter(CharInfo c)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(c.fbx);
            var mouth = AssetDatabase.LoadAssetAtPath<Mesh>(c.mouthMesh);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(c.material);
            if (fbx == null || mouth == null || mat == null) { Debug.LogError("[Avatar] " + c.name + ": FBX/입 메시/재질을 찾지 못했어요."); return; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Avatar_" + c.name;

            // 이름을 두 캐릭터가 똑같게 맞춘다 (애니메이션 경로 공유, 이름으로 뼈 찾기)
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Char_" + c.name + "_Armature") t.name = "Armature";
                else if (t.name == "Char_" + c.name && t.GetComponent<SkinnedMeshRenderer>() != null) t.name = "Body";
            }

            var body = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
            body.sharedMesh = mouth;           // 원본 FBX 메시 + "MouthClose" 블렌드 셰이프
            body.sharedMaterial = mat;
            body.updateWhenOffscreen = true;   // 뼈를 코드로 움직이므로 화면 밖 판정이 틀어져 몸이 사라지는 것을 막는다
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var model = inst.AddComponent<CharacterAvatarModel>();
            model.AutoBind();

            // Animator (상호작용)
            var clips = BuildClips(c, inst, model);
            var controller = BuildController(c, clips.idle, clips.interact);
            var animator = inst.GetComponent<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var so = new SerializedObject(model);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = OutDir + "/Avatar_" + c.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, path);
            Object.DestroyImmediate(inst);
        }

        // ---------- Animator 클립 ----------

        static (AnimationClip idle, AnimationClip interact) BuildClips(CharInfo c, GameObject inst, CharacterAvatarModel m)
        {
            var root = inst.transform;
            var right = root.right;   // 캐릭터 오른쪽 (월드)
            // 클립이 움직이는 뼈 (CharacterAvatarModel이 "Animator가 쓴 값에서 시작"하는 뼈와 같아야 한다)
            // 상호작용은 왼팔로 한다 (오른팔은 촛불을 든 자세 그대로). 왼팔에는 든 자세가 없으므로 원하는 각도가 곧 클립 각도다.
            var targets = new List<Transform> { m.spine1, m.leftArm, m.leftForeArm };
            float arm = -88f;
            float fore = -34f;
            float lean = 9f;

            var times = new[] { 0f, 0.16f, 0.34f, 0.55f };
            var weights = new[] { 0f, 1f, 1f, 0f };   // 0 = 기준 자세, 1 = 손 뻗은 자세
            var quats = targets.ToDictionary(t => t, t => new Quaternion[times.Length]);

            for (int k = 0; k < times.Length; k++)
            {
                // 기준 자세에서 위 뼈부터 차례로 돌려 본다 (아래 뼈는 부모의 최신 회전을 써야 한다)
                var rest = targets.ToDictionary(t => t, t => t.localRotation);
                Pose(m.spine1, right, lean * weights[k]);
                Pose(m.leftArm, right, arm * weights[k]);
                Pose(m.leftForeArm, right, fore * weights[k]);
                foreach (var t in targets) quats[t][k] = t.localRotation;
                foreach (var t in targets) t.localRotation = rest[t];
            }

            var interact = new AnimationClip { name = "Avatar_" + c.name + "_Interact", frameRate = 60 };
            var idle = new AnimationClip { name = "Avatar_" + c.name + "_Idle", frameRate = 60 };
            foreach (var t in targets)
            {
                string p = AnimationUtility.CalculateTransformPath(t, root);
                SetQuatCurves(interact, p, times, quats[t], smooth: true);
                var restQ = t.localRotation;
                SetQuatCurves(idle, p, new[] { 0f, 1f }, new[] { restQ, restQ }, smooth: false);
            }
            var s = AnimationUtility.GetAnimationClipSettings(interact); s.loopTime = false; AnimationUtility.SetAnimationClipSettings(interact, s);
            var si = AnimationUtility.GetAnimationClipSettings(idle); si.loopTime = true; AnimationUtility.SetAnimationClipSettings(idle, si);

            string ip = OutDir + "/Avatar_" + c.name + "_Interact.anim";
            string dp = OutDir + "/Avatar_" + c.name + "_Idle.anim";
            AssetDatabase.DeleteAsset(ip); AssetDatabase.DeleteAsset(dp);
            AssetDatabase.CreateAsset(interact, ip);
            AssetDatabase.CreateAsset(idle, dp);
            return (idle, interact);
        }

        static void Pose(Transform bone, Vector3 axisWorld, float angle)
        {
            if (bone == null || Mathf.Abs(angle) < 0.001f) return;
            Quaternion pw = bone.parent.rotation;
            bone.localRotation = Quaternion.Inverse(pw) * Quaternion.AngleAxis(angle, axisWorld) * pw * bone.localRotation;
        }

        static void SetQuatCurves(AnimationClip clip, string path, float[] times, Quaternion[] q, bool smooth)
        {
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
            Quaternion prev = q[0];
            for (int i = 0; i < times.Length; i++)
            {
                var cur = q[i];
                if (Quaternion.Dot(prev, cur) < 0f) cur = new Quaternion(-cur.x, -cur.y, -cur.z, -cur.w);   // 가까운 쪽으로 돌도록 부호 맞춤
                prev = cur;
                cx.AddKey(times[i], cur.x); cy.AddKey(times[i], cur.y); cz.AddKey(times[i], cur.z); cw.AddKey(times[i], cur.w);
            }
            if (smooth) foreach (var cv in new[] { cx, cy, cz, cw }) for (int i = 0; i < cv.length; i++) cv.SmoothTangents(i, 0f);
            clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
        }

        static AnimatorController BuildController(CharInfo c, AnimationClip idle, AnimationClip interact)
        {
            string path = OutDir + "/Avatar_" + c.name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Interact", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            idleState.writeDefaultValues = true;
            var interactState = sm.AddState("Interact");
            interactState.motion = interact;
            interactState.writeDefaultValues = true;
            sm.defaultState = idleState;

            var toInteract = idleState.AddTransition(interactState);
            toInteract.hasExitTime = false;
            toInteract.duration = 0.08f;
            toInteract.AddCondition(AnimatorConditionMode.If, 0f, "Interact");

            var back = interactState.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.12f;

            // 연타하면 처음부터 다시 뻗는다
            var again = interactState.AddTransition(interactState);
            again.hasExitTime = false;
            again.duration = 0.05f;
            again.AddCondition(AnimatorConditionMode.If, 0f, "Interact");
            return ctrl;
        }

        // 컨트롤러를 지우고 같은 경로에 다시 만들면 방금 저장한 프리팹의 참조가 끊기는 경우가 있어서,
        // 모든 에셋이 저장된 뒤에 프리팹마다 컨트롤러를 다시 연결한다.
        static void RelinkControllers()
        {
            foreach (var c in Chars)
            {
                string pp = OutDir + "/Avatar_" + c.name + ".prefab";
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OutDir + "/Avatar_" + c.name + ".controller");
                if (ctrl == null) { Debug.LogError("[Avatar] 컨트롤러를 찾지 못함: " + c.name); continue; }
                var root = PrefabUtility.LoadPrefabContents(pp);
                try
                {
                    var anim = root.GetComponent<Animator>();
                    if (anim != null && anim.runtimeAnimatorController != ctrl)
                    {
                        anim.runtimeAnimatorController = ctrl;
                        PrefabUtility.SaveAsPrefabAsset(root, pp);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        // ---------- 플레이어 프리팹 연결 ----------

        static void WirePlayerPrefab()
        {
            var orange = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/Avatar_Orange.prefab");
            var blue = AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/Avatar_Blue.prefab");
            if (orange == null || blue == null) { Debug.LogError("[Avatar] 모델 프리팹이 없어서 플레이어 프리팹 연결을 건너뜁니다."); return; }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var avatarObj = root.transform.Find("Avatar");
                if (avatarObj == null) { Debug.LogError("[Avatar] 플레이어 프리팹에 Avatar 오브젝트가 없어요."); return; }
                var comp = avatarObj.GetComponent<CharacterAvatar>();
                if (comp == null) comp = avatarObj.gameObject.AddComponent<CharacterAvatar>();
                var so = new SerializedObject(comp);
                so.FindProperty("orangeModel").objectReferenceValue = orange.GetComponent<CharacterAvatarModel>();
                so.FindProperty("blueModel").objectReferenceValue = blue.GetComponent<CharacterAvatarModel>();
                so.ApplyModifiedPropertiesWithoutUndo();
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
