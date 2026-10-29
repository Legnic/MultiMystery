using UnityEditor;
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
            public Gesture(string name, float turnY, float scale, bool mirror, string icon) { this.name = name; this.turnY = turnY; this.scale = scale; this.mirror = mirror; this.icon = icon; }
        }

        static readonly Gesture[] Gestures =
        {
            new Gesture("Hand_Wave",         0f,   1.0f, false, "Greeting"),   // 인사
            new Gesture("Hand_VSign",        0f,   1.0f, false, "V"),          // 브이
            new Gesture("Hand_MiddleFinger", 0f,   1.0f, false, "FuckYou"),    // 중지
            new Gesture("Hand_Point",        135f, 0.8f, false, "There"),      // 가리키기
            new Gesture("Hand_ThumbsUp",     185f, 0.8f, true,  "ThumbsUp"),   // 엄지척
        };

        // LHJ 씬의 손 자리(slot)도 Y 180도로 돌아 있다 - 위 turnY 값들이 이 회전을 전제로 맞춰져 있다.
        static readonly Quaternion HolderRotation = Quaternion.Euler(0f, 180f, 0f);

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
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HandDir + g.name + "_Anim.controller");
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
