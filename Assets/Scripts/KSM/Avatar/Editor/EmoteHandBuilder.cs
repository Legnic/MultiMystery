using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoopDemo.EditorTools
{
    // Wires the "face emote hand" under NetworkPlayer_LGH's Main Camera. Re-runnable: the EmoteHand
    // holder is rebuilt from scratch every time.
    //
    // 2026-10-29: 코드로 관절을 돌려 만들던 임시 클립(Emote_Idle/Wave)을 버리고, LHJ님이 Blender로 만든
    // 손 5종(Assets/VARCO 3D/LHJ/Hands)과 각 손의 Animator Controller를 그대로 쓴다. 손별 방향(turnY)·크기·
    // 좌우반전 값은 LHJ 씬의 HandEmote에 저장된 값을 그대로 옮겨 온 것 - LHJ님이 값을 바꾸면 Gestures 표만
    // 고치고 Build All을 다시 돌리면 된다. 표 순서 = EmoteHandView.hands = EmoteWheelUI.Labels 순서.
    public static class EmoteHandBuilder
    {
        const string HandDir = "Assets/VARCO 3D/LHJ/Hands/";
        const string HandMaterial = HandDir + "Hand_White_Mat.mat";
        const string OldClipDir = "Assets/Animations/KSM/EmoteHand";
        const string PlayerPrefab = "Assets/Objects/LGH/NetworkPlayer_LGH.prefab";
        const string IconDir = "Assets/Images/KSM/Emote/";   // 도넛 메뉴 아이콘 (Gesture.icon + ".png")

        struct Gesture
        {
            public string name; public float turnY; public float scale; public bool mirror; public string icon;
            public float otherYaw;   // 상대 화면에서만 더 돌리는 Y 각도 (인사·브이=180: 마주 선 상대에게 손바닥이 보이게)
            public Gesture(string name, float turnY, float scale, bool mirror, string icon, float otherYaw = 0f) { this.name = name; this.turnY = turnY; this.scale = scale; this.mirror = mirror; this.icon = icon; this.otherYaw = otherYaw; }
        }

        static readonly Gesture[] Gestures =
        {
            // 인사·브이: turnY 180 = 1인칭에서 손등이 보이고, 마주 선 상대에게는 손바닥이 보인다(2026-10-30).
            new Gesture("Hand_Wave",         180f, 1.0f, false, "Greeting"),   // 인사
            new Gesture("Hand_VSign",        180f, 1.0f, false, "V"),          // 브이
            new Gesture("Hand_MiddleFinger", 0f,   1.0f, false, "FuckYou"),    // 중지
            // 가리키기: LHJ 원본(135도)은 손끝이 정면에서 왼쪽으로 41도 틀어져 있다. 177도 = 정면 가운데를 가리킨다. (2026-10-30)
            new Gesture("Hand_Point",        177f, 0.8f, false, "There"),      // 가리키기
            new Gesture("Hand_ThumbsUp",     185f, 0.8f, true,  "ThumbsUp"),   // 엄지척
        };

        // LHJ 씬의 손 자리(slot)도 Y 180도로 돌아 있다 - 위 turnY 값들이 이 회전을 전제로 맞춰져 있다.
        static readonly Quaternion HolderRotation = Quaternion.Euler(0f, 180f, 0f);

        // ---- 가리키기: 가운데에서 위아래로 미세하게 까딱 ----
        const string PointAnimDir = "Assets/Animations/KSM/EmoteHandPoint/";   // OldClipDir(EmoteHand)는 Build All이 지우므로 다른 폴더를 쓴다
        const string PointBoneName = "Hand_Point_Armature/Hand";   // LHJ 원본 클립이 움직이던 뼈 경로와 같다
        const float PointBobDegrees = 6f;                          // 위아래로 ±6도
        const float PointBobSeconds = 1.2f;                        // 한 번 오르내리는 시간

        // 재실행해도 같은 에셋을 덮어쓴다. 원본(LHJ) 클립의 기준 자세(Hand 뼈 localRotation = X축 90도)에서 로컬 X축으로만 돌린다.
        static RuntimeAnimatorController EnsurePointBobController()
        {
            System.IO.Directory.CreateDirectory(PointAnimDir);
            string clipPath = PointAnimDir + "Hand_Point_Bob.anim";
            string ctrlPath = PointAnimDir + "Hand_Point_Bob.controller";

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.ClearCurves();
            clip.frameRate = 30f;
            var baseRot = Quaternion.Euler(90f, 0f, 0f);
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
            const int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float t = PointBobSeconds * i / steps;
                float a = Mathf.Sin(2f * Mathf.PI * i / steps) * PointBobDegrees;
                var q = baseRot * Quaternion.AngleAxis(a, Vector3.right);
                cx.AddKey(t, q.x); cy.AddKey(t, q.y); cz.AddKey(t, q.z); cw.AddKey(t, q.w);
            }
            clip.SetCurve(PointBoneName, typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(PointBoneName, typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(PointBoneName, typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(PointBoneName, typeof(Transform), "localRotation.w", cw);
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState state = null;
            foreach (var s in sm.states) if (s.state.name == "Bob") state = s.state;
            if (state == null) state = sm.AddState("Bob");
            state.motion = clip;
            sm.defaultState = state;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        [MenuItem("Tools/KSM/EmoteHand/Build All")]
        public static void BuildAll()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(HandMaterial);
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var cam = root.transform.Find("Main Camera");
                if (cam == null) { Debug.LogError("[EmoteHand] 플레이어 프리팹에 Main Camera가 없어요."); return; }

                var old = cam.Find("EmoteHand");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var holder = new GameObject("EmoteHand").transform;
                holder.SetParent(cam, false);
                // 얼굴 앞이 시야를 가리지 않도록 왼쪽 아래(HeldCandle의 반대쪽). scale 0.22 = 실제 사람 손 크기(폭 약 18cm).
                holder.localPosition = new Vector3(-0.20f, -0.05f, 0.42f);
                holder.localRotation = HolderRotation;
                holder.localScale = Vector3.one * 0.22f;

                var hands = new GameObject[Gestures.Length];
                for (int i = 0; i < Gestures.Length; i++)
                {
                    var g = Gestures[i];
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HandDir + g.name + "_Rigged.fbx");
                    // 가리키기는 LHJ 원본(좌우로 휘두름) 대신 가운데에서 위아래로 미세하게 까딱이는 우리 쪽 애니메이션을 쓴다.
                    var ctrl = g.name == "Hand_Point"
                        ? EnsurePointBobController()
                        : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HandDir + g.name + "_Anim.controller");
                    if (fbx == null || ctrl == null) { Debug.LogError("[EmoteHand] 손 모델/컨트롤러를 못 찾았어요: " + g.name); continue; }

                    // 좌우 뒤집기는 손을 돌린 뒤 화면 기준으로 뒤집히도록 감싸는 오브젝트에서 한다(LHJ HandEmote와 같은 순서).
                    var wrap = new GameObject(g.name).transform;
                    wrap.SetParent(holder, false);
                    wrap.localScale = new Vector3(g.mirror ? -1f : 1f, 1f, 1f);

                    var hand = (GameObject)PrefabUtility.InstantiatePrefab(fbx, wrap);
                    hand.transform.localPosition = Vector3.zero;
                    hand.transform.localRotation = Quaternion.Euler(0f, g.turnY, 0f);
                    hand.transform.localScale = Vector3.one * g.scale;
                    if (mat != null)
                        foreach (var r in hand.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;

                    var animator = hand.GetComponent<Animator>();
                    if (animator == null) animator = hand.AddComponent<Animator>();
                    animator.runtimeAnimatorController = ctrl;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // 상대 화면에서도 항상 움직이게

                    wrap.gameObject.SetActive(false);
                    hands[i] = wrap.gameObject;
                }

                var view = holder.gameObject.AddComponent<EmoteHandView>();
                view.hands = hands;
                view.otherViewYaw = new float[Gestures.Length];
                for (int i = 0; i < Gestures.Length; i++) view.otherViewYaw[i] = Gestures[i].otherYaw;

                // 마우스 가운데 버튼 도넛 메뉴 - 루트에 붙이고 NetworkPlayer.ownerOnly에 등록해 상대 화면에서는 꺼져 있게 한다.
                var wheel = root.GetComponent<EmoteWheelUI>();
                if (wheel == null) wheel = root.AddComponent<EmoteWheelUI>();
                var icons = new Texture2D[Gestures.Length];
                for (int i = 0; i < Gestures.Length; i++)
                {
                    icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(IconDir + Gestures[i].icon + ".png");
                    if (icons[i] == null) Debug.LogWarning("[EmoteHand] 도넛 메뉴 아이콘을 못 찾았어요: " + Gestures[i].icon);
                }
                wheel.icons = icons;
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

            // 예전 임시 클립(코드로 만든 인사)은 더 이상 쓰지 않는다.
            if (AssetDatabase.IsValidFolder(OldClipDir)) AssetDatabase.DeleteAsset(OldClipDir);

            AssetDatabase.SaveAssets();
            Debug.Log("[EmoteHand] Build All 완료: LHJ 손 5종 + 플레이어 프리팹 연결.");
        }
    }
}
