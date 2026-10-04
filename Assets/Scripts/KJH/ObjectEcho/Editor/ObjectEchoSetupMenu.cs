using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.UI;

// ObjectEcho(사물에 손 얹기 능력)를 씬에 설정해 주는 에디터 도구. Editor 폴더 안이라 게임 빌드에는 포함되지 않는다.
//
// 메뉴 1) Tools/KJH/ObjectEcho/Setup Player And UI
//   - AudioMixer(Master > Ambient, Echo 그룹, AmbientVolume 파라미터 Expose)를 없으면 만든다.
//   - Player에 ObjectEchoController, 자식 SoundCue, 카메라 아래 임시 손(PlaceholderHand)을 만든다.
//   - 상호작용 안내 UI가 있는 Canvas에 하단 중앙 스펙트럼 표시(SpectrumOverlay)를 만든다.
//   - 이미 있는 것은 건드리지 않고 빠진 것만 채운다 (여러 번 눌러도 안전).
//
// 메뉴 2) Tools/KJH/ObjectEcho/Make Object Echo Target (Top Surface / Front Surface)
//   - 선택한 사물에 ObjectEchoTarget을 붙이고, 자식 HandTarget / FocusPoint를 자동 배치한다.
//     Top Surface   = 책상 위 물건처럼 "위에서 손을 얹는" 경우 (손바닥이 아래를 향함)
//     Front Surface = 벽/액자처럼 "앞에서 손을 대는" 경우 (손바닥이 씬 뷰 카메라 쪽 면에 닿음)
//   - 콜라이더가 없으면 BoxCollider를 붙인다 (조준 레이에 맞아야 안내가 뜨므로).
public static class ObjectEchoSetupMenu
{
    private const string Root = "Tools/KJH/ObjectEcho/";
    private const string MixerPath = "Assets/Sounds/KJH/KJH_AudioMixer.mixer";
    private const string HandMaterialPath = "Assets/Scripts/KJH/ObjectEcho/PlaceholderHand_Mat.mat";
    private const string AmbientParam = "AmbientVolume";

    // ── 메뉴 1: 플레이어 / UI / 믹서 ────────────────────────

    [MenuItem(Root + "Setup Player And UI", false, 200)]
    private static void SetupPlayerAndUI()
    {
        AudioMixer mixer = GetOrCreateMixer();

        // Player = InteractionController가 붙은 오브젝트 (공용 상호작용 규칙을 따르는 플레이어).
        InteractionController ic = UnityEngine.Object.FindAnyObjectByType<InteractionController>();
        if (ic == null)
        {
            EditorUtility.DisplayDialog("ObjectEcho", "씬에 InteractionController가 붙은 Player가 없습니다.", "확인");
            return;
        }
        GameObject player = ic.gameObject;
        Camera cam = ic.LookCamera != null ? ic.LookCamera : player.GetComponentInChildren<Camera>();

        // 1) 컨트롤러
        ObjectEchoController controller = player.GetComponent<ObjectEchoController>();
        if (controller == null) controller = Undo.AddComponent<ObjectEchoController>(player);

        // 2) SoundCue (Echo 그룹으로 출력 → 환경음 줄이기에 같이 줄지 않음)
        SoundCue cue = player.GetComponentInChildren<SoundCue>(true);
        if (cue == null)
        {
            GameObject go = new GameObject("SoundCue");
            Undo.RegisterCreatedObjectUndo(go, "Create SoundCue");
            go.transform.SetParent(player.transform, false);
            go.AddComponent<AudioSource>().playOnAwake = false;
            cue = go.AddComponent<SoundCue>();
        }
        AudioMixerGroup echoGroup = mixer != null ? mixer.FindMatchingGroups("Echo").FirstOrDefault() : null;
        SetRef(cue, "outputGroup", echoGroup);
        cue.GetComponent<AudioSource>().outputAudioMixerGroup = echoGroup;

        // 3) 임시 손
        PlaceholderHandReach hand = player.GetComponentInChildren<PlaceholderHandReach>(true);
        if (hand == null && cam != null) hand = CreatePlaceholderHand(cam.transform);

        // 4) 스펙트럼 UI
        SpectrumOverlay overlay = UnityEngine.Object.FindAnyObjectByType<SpectrumOverlay>(FindObjectsInactive.Include);
        if (overlay == null && ic.PromptUI != null) overlay = CreateSpectrumOverlay(ic.PromptUI.GetComponentInParent<Canvas>());
        SetRef(cue, "spectrumOverlay", overlay);

        // 5) 컨트롤러 참조 연결
        SetRef(controller, "interactionController", ic);
        SetRef(controller, "playerMovement", player.GetComponent<PlayerMovement>());
        SetRef(controller, "viewCamera", cam);
        SetRef(controller, "handReach", hand);
        SetRef(controller, "soundCue", cue);
        if (mixer != null) SetRef(controller, "ambientMixer", mixer);

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("[ObjectEcho] Player/UI 설정 완료. 환경음 AudioSource들의 Output을 KJH_AudioMixer > Ambient 그룹으로 지정해야 환경음 줄이기가 들립니다.");
    }

    // 손등(Y+) 위, 손가락(Z+) 앞 방향 약속에 맞춘 단순 도형 손. 피벗 = 손바닥 아랫면 중앙.
    private static PlaceholderHandReach CreatePlaceholderHand(Transform cam)
    {
        GameObject root = new GameObject("PlaceholderHand");
        Undo.RegisterCreatedObjectUndo(root, "Create Placeholder Hand");
        root.transform.SetParent(cam, false);
        // 화면 밖 오른쪽 아래가 대기 자리 (실행 시 이 위치를 기억함).
        root.transform.localPosition = new Vector3(0.22f, -0.42f, 0.35f);
        root.transform.localRotation = Quaternion.Euler(20f, -10f, 0f);

        Material mat = GetOrCreateHandMaterial();
        // 손바닥
        AddPart(root.transform, PrimitiveType.Cube, "Palm", new Vector3(0f, 0.012f, 0.02f), Vector3.zero, new Vector3(0.085f, 0.024f, 0.1f), mat);
        // 손가락 4개 (캡슐은 기본이 세로라 X축으로 90도 눕힌다)
        float[] xs = { -0.03f, -0.01f, 0.01f, 0.03f };
        float[] lengths = { 0.032f, 0.038f, 0.036f, 0.028f };
        for (int i = 0; i < 4; i++)
        {
            AddPart(root.transform, PrimitiveType.Capsule, "Finger" + (i + 1),
                new Vector3(xs[i], 0.01f, 0.07f + lengths[i] * 0.8f), new Vector3(90f, 0f, 0f),
                new Vector3(0.019f, lengths[i], 0.019f), mat);
        }
        // 엄지 (오른손 손등이 위일 때 엄지는 왼쪽)
        AddPart(root.transform, PrimitiveType.Capsule, "Thumb", new Vector3(-0.055f, 0.01f, 0.035f), new Vector3(90f, -40f, 0f),
            new Vector3(0.022f, 0.03f, 0.022f), mat);
        // 소매 끝(1890년대 셔츠 커프스 느낌의 짧은 원기둥)
        AddPart(root.transform, PrimitiveType.Cylinder, "Cuff", new Vector3(0f, 0.014f, -0.06f), new Vector3(90f, 0f, 0f),
            new Vector3(0.075f, 0.03f, 0.05f), mat);

        return root.AddComponent<PlaceholderHandReach>();
    }

    private static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        // 기본 도형에 붙어 오는 콜라이더는 필요 없다 (조준 레이/플레이어 이동 방해 방지).
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localEulerAngles = euler;
        part.transform.localScale = scale;
        if (mat != null) part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static Material GetOrCreateHandMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(HandMaterialPath);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        // 짙은 갈색 가죽 장갑 톤 (임시 손이 너무 눈에 띄지 않게).
        mat = new Material(shader) { name = "PlaceholderHand_Mat" };
        mat.SetColor("_BaseColor", new Color(0.24f, 0.17f, 0.12f));
        mat.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(mat, HandMaterialPath);
        return mat;
    }

    // 하단 중앙의 스펙트럼 표시 UI.
    private static SpectrumOverlay CreateSpectrumOverlay(Canvas canvas)
    {
        if (canvas == null) return null;
        GameObject root = new GameObject("SpectrumOverlay", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(root, "Create Spectrum Overlay");
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        // 화면 하단 중앙. 상호작용 안내 문구(y=160)보다 아래에 둔다.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 90f);
        rt.sizeDelta = new Vector2(620f, 110f);

        GameObject line = new GameObject("InkLine", typeof(RectTransform));
        RectTransform lrt = line.GetComponent<RectTransform>();
        lrt.SetParent(rt, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        InkWaveformGraphic graphic = line.AddComponent<InkWaveformGraphic>();
        // 세피아 계열 잉크 색. 어두운 실내에서도 보이도록 너무 어둡지 않은 갈색으로.
        graphic.color = new Color(0.72f, 0.58f, 0.40f, 0.9f);

        SpectrumOverlay overlay = root.AddComponent<SpectrumOverlay>();
        SetRef(overlay, "canvasGroup", root.GetComponent<CanvasGroup>());
        SetRef(overlay, "waveform", graphic);
        root.GetComponent<CanvasGroup>().alpha = 0f;
        return overlay;
    }

    // AudioMixer 에셋을 만들고 Ambient/Echo 그룹 + AmbientVolume 파라미터를 Expose한다.
    // Unity는 믹서를 코드로 만드는 공개 API가 없어서 에디터 내부 API를 리플렉션으로 호출한다.
    // (실패하면 README의 수동 설정 방법대로 직접 만들면 된다)
    private static AudioMixer GetOrCreateMixer()
    {
        AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null) return existing;

        try
        {
            const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            Type groupType = Type.GetType("UnityEditor.Audio.AudioMixerGroupController, UnityEditor");
            Type pathType = Type.GetType("UnityEditor.Audio.AudioGroupParameterPath, UnityEditor");
            Type exposedType = Type.GetType("UnityEditor.Audio.ExposedAudioParameter, UnityEditor");

            object mixer = controllerType.GetMethod("CreateMixerControllerAtPath", F).Invoke(null, new object[] { MixerPath });
            object master = controllerType.GetProperty("masterGroup", F).GetValue(mixer);
            MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", F);
            MethodInfo addChild = controllerType.GetMethod("AddChildToParent", F);
            MethodInfo addToView = controllerType.GetMethod("AddGroupToCurrentView", F);

            object ambient = createGroup.Invoke(mixer, new object[] { "Ambient", false });
            addChild.Invoke(mixer, new[] { ambient, master });
            object echo = createGroup.Invoke(mixer, new object[] { "Echo", false });
            addChild.Invoke(mixer, new[] { echo, master });
            // 믹서 창의 "보기(View)" 목록에 그룹을 표시한다. 새 믹서는 보기 목록이 비어 있어 실패할 수 있는데,
            // 그룹 자체는 이미 만들어졌으므로 실패해도 무시한다 (믹서 창을 열면 자동으로 정리됨).
            try { addToView.Invoke(mixer, new[] { ambient }); addToView.Invoke(mixer, new[] { echo }); }
            catch (TargetInvocationException) { }

            // Ambient 그룹의 Volume을 Expose하고 이름을 AmbientVolume으로 바꾼다.
            GUID volumeGuid = (GUID)groupType.GetMethod("GetGUIDForVolume", F).Invoke(ambient, null);
            object path = Activator.CreateInstance(pathType, ambient, volumeGuid);
            controllerType.GetMethod("AddExposedParameter", F).Invoke(mixer, new[] { path });

            PropertyInfo exposedProp = controllerType.GetProperty("exposedParameters", F);
            Array exposed = (Array)exposedProp.GetValue(mixer);
            for (int i = 0; i < exposed.Length; i++)
            {
                object p = exposed.GetValue(i);
                if ((GUID)exposedType.GetField("guid", F).GetValue(p) != volumeGuid) continue;
                exposedType.GetField("name", F).SetValue(p, AmbientParam);
                exposed.SetValue(p, i);
            }
            exposedProp.SetValue(mixer, exposed);

            EditorUtility.SetDirty((UnityEngine.Object)mixer);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ObjectEcho] AudioMixer 자동 생성 실패 (README의 수동 설정을 따라주세요): {(e.InnerException ?? e).Message}");
            return null;
        }
    }

    // ── 메뉴 2: 대상 오브젝트 만들기 ────────────────────────

    [MenuItem(Root + "Make Object Echo Target (Top Surface)", true)]
    [MenuItem(Root + "Make Object Echo Target (Front Surface)", true)]
    private static bool ValidateMakeTarget() => !EditorApplication.isPlaying && Selection.gameObjects.Any(g => g.scene.IsValid());

    [MenuItem(Root + "Make Object Echo Target (Top Surface)", false, 220)]
    private static void MakeTargetTop()
    {
        foreach (GameObject go in Selection.gameObjects.Where(g => g.scene.IsValid())) MakeTarget(go, false);
    }

    [MenuItem(Root + "Make Object Echo Target (Front Surface)", false, 221)]
    private static void MakeTargetFront()
    {
        foreach (GameObject go in Selection.gameObjects.Where(g => g.scene.IsValid())) MakeTarget(go, true);
    }

    // 사물에 ObjectEchoTarget을 붙이고 HandTarget/FocusPoint를 표면에 자동 배치한다. 이미 있으면 위치만 다시 맞추지 않는다.
    public static ObjectEchoTarget MakeTarget(GameObject go, bool frontSurface)
    {
        ObjectEchoTarget target = go.GetComponent<ObjectEchoTarget>();
        if (target == null) target = Undo.AddComponent<ObjectEchoTarget>(go);
        if (go.GetComponentInChildren<Collider>() == null) Undo.AddComponent<BoxCollider>(go);

        Bounds b = GetBounds(go);
        SerializedObject so = new SerializedObject(target);

        if (so.FindProperty("handTarget").objectReferenceValue == null)
        {
            Transform hand = CreateChild(go.transform, "HandTarget");
            if (!frontSurface)
            {
                // 윗면 중앙. 손등(Y)은 위, 손가락(Z)은 씬 뷰 카메라에서 멀어지는 수평 방향.
                hand.position = new Vector3(b.center.x, b.max.y, b.center.z);
                Vector3 fwd = Vector3.ProjectOnPlane(b.center - ViewPosition(b), Vector3.up);
                if (fwd.sqrMagnitude < 0.0001f) fwd = go.transform.forward;
                hand.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }
            else
            {
                // 씬 뷰 카메라 쪽을 향한 옆면 중앙. 손등(Y)은 면 바깥쪽, 손가락(Z)은 위쪽.
                Vector3 toView = ViewPosition(b) - b.center;
                Vector3 normal = Mathf.Abs(toView.x) * b.extents.z > Mathf.Abs(toView.z) * b.extents.x
                    ? new Vector3(Mathf.Sign(toView.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(toView.z));
                hand.position = b.center + Vector3.Scale(normal, b.extents);
                hand.rotation = Quaternion.LookRotation(Vector3.up, normal);
            }
            so.FindProperty("handTarget").objectReferenceValue = hand;
        }

        if (so.FindProperty("focusPoint").objectReferenceValue == null)
        {
            Transform focus = CreateChild(go.transform, "FocusPoint");
            // 손 자리와 사물 중심 사이 → 손과 사물이 함께 화면 가운데쯤 들어온다.
            Transform hand = (Transform)so.FindProperty("handTarget").objectReferenceValue;
            focus.position = Vector3.Lerp(b.center, hand.position, 0.6f);
            so.FindProperty("focusPoint").objectReferenceValue = focus;
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(go.scene);
        return target;
    }

    // ── 보조 ───────────────────────────────────────────────

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // 씬 뷰 카메라 위치 (없으면 사물 앞쪽 -Z 방향 2m).
    private static Vector3 ViewPosition(Bounds b)
    {
        SceneView view = SceneView.lastActiveSceneView;
        return view != null && view.camera != null ? view.camera.transform.position : b.center - Vector3.forward * 2f;
    }

    private static void SetRef(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        if (owner == null) return;
        SerializedObject so = new SerializedObject(owner);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        // 이미 누군가 연결해 둔 값은 덮어쓰지 않는다 (빈 칸만 채움).
        if (p.objectReferenceValue != null || value == null) return;
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
