using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 메뉴 Tools/KSM/Audio/Build LCW Sound Bank — Resources/KSM/LcwSoundBank.asset을 만들고 LCW 사운드 파일을 칸마다 연결한다.
// 재실행 가능(에셋이 있으면 그대로 두고 칸만 다시 채운다). 칸 이름과 파일 이름의 짝은 아래 표 한 곳에만 있다.
public static class LcwSoundBankBuilder
{
    const string Root = "Assets/Sounds/LCW/기믹사운드/";
    const string BankPath = "Assets/Resources/KSM/LcwSoundBank.asset";

    static readonly (string field, string file)[] Map =
    {
        ("doorOpen", "기본사운드/기본사운드/문열리는소리1.wav"),
        ("doorClose", "기본사운드/기본사운드/문닫는소리1.wav"),
        ("doorLocked", "기본사운드/기본사운드/잠긴문1.wav"),
        ("doorUnlock", "기본사운드/기본사운드/잠금풀림1.wav"),
        ("keyTurn", "기본사운드/기본사운드/열쇠돌아가는소리2.wav"),
        ("drawerOpen", "기본사운드/기본사운드/서랍여는소리1.wav"),
        ("drawerClose", "기본사운드/기본사운드/서랍닫는소리1.wav"),
        ("footstep", "기본사운드/기본사운드/발소리수정3.wav"),
        ("jumpLanding", "기본사운드/기본사운드/점프후착지음.wav"),
        ("paperPickup", "기본사운드/기본사운드/종이집어들기1.wav"),
        ("smallMetalPickup", "기본사운드/기본사운드/작은금속물건집기1.wav"),
        ("numpadPress1", "기본사운드/기본사운드/번호키누르는소리1.wav"),
        ("numpadPress2", "기본사운드/기본사운드/번호키누르는소리2.wav"),
        ("numberLockTurn", "기본사운드/기본사운드/숫자자물쇠돌리기1.wav"),
        ("wrongInput", "기본사운드/기본사운드/오답입력효과음.wav"),
        ("candle", "기본사운드/기본사운드/촛불소리1.wav"),

        ("ambience", "게임브금/인게임엠비언스찐최종.wav"),
        ("lobbyBgm", "게임브금/대기실브금후보1.mp3"),

        ("portraitRotate", "GIM-01/01초상화회전.wav"),
        ("portraitLock", "GIM-01/01초상화회전고정.wav"),
        ("keyBehindFrame", "GIM-01/01액자뒤음악실열쇠등장.wav"),

        ("pianoDo", "GIM-02/01_Do_C4.wav"),
        ("pianoRe", "GIM-02/02_Re_D4.wav"),
        ("pianoMi", "GIM-02/03_Mi_E4.wav"),
        ("pianoFa", "GIM-02/04_Fa_F4.wav"),
        ("pianoSol", "GIM-02/05_Sol_G4.wav"),
        ("musicBoxTurn", "GIM-02/02오르골돌아가는소리.wav"),
        ("dollInsert", "GIM-02/원목인형꽃기.wav"),
        ("dollRemove", "GIM-02/인형빼기.wav"),
        ("paperRemove", "GIM-02/종이빼기.wav"),
        ("puppetBulbOn", "GIM-02/인형극정답전구켜지기.wav"),

        ("herbPlace", "GIM-03/03약초 놓기.wav"),
        ("bottlePickup", "GIM-03/03유리병드는소리.wav"),
        ("bottlePutDown", "GIM-03/03유리병내리는소리.wav"),
        ("combine", "GIM-03/03조합되는소리.wav"),
        ("powderPour", "GIM-03/가루쏟는소리.wav"),
        ("dumbwaiterUp", "GIM-03/도르레올라가는소리.wav"),
        ("grind", "GIM-03/약갈기.wav"),

        ("potPickup", "GIM-04/화분들기1.wav"),
        ("potPutDown", "GIM-04/화분내리기1.wav"),
        ("platePressDown", "GIM-04/화분누름판내려감.wav"),
        ("platePressUp", "GIM-04/화분누름판올라감.wav"),

        ("hedgeRegrow", "GIM-05/05덤불다시자라는소리.wav"),
        ("mazeEndButton", "GIM-05/05미로끝버튼 (2).wav"),
        ("buttonLightOn", "GIM-05/05버튼불빛켜짐.wav"),
        ("brassButton", "GIM-05/05벽의놋쇠버튼.wav"),
        ("faint", "GIM-05/05정신잃는소리.wav"),
        ("mazeBgm", "GIM-05/미로배경음.wav"),
        ("barsRise", "GIM-05/철창올라가는소리.wav"),
        ("bushVanish", "GIM-05/풀숲사라지는소리.wav"),

        ("plateDown", "GIM-07/07누름판내려감.wav"),
        ("moveL", "GIM-07/07물건옮기기L.wav"),
        ("moveR", "GIM-07/07물건옮기기R.wav"),
        ("minigamePress", "GIM-07/07미니게임누름소리1.wav"),
        ("minigameWrong", "GIM-07/07미니게임틀림소리1.wav"),

        ("finalDoor", "GIM-08/08마지막현관문효과음.wav"),
        ("treasureOpen", "GIM-08/08보물상자열리는소리2.wav"),
        ("telescope", "GIM-08/천체망원경소리1.wav"),
    };

    // 길이가 긴 소리(배경음/엠비언스)는 통째로 메모리에 올리지 않고 스트리밍으로 읽는다.
    static readonly HashSet<string> Streamed = new HashSet<string> { "ambience", "lobbyBgm", "mazeBgm" };

    [MenuItem("Tools/KSM/Audio/Build LCW Sound Bank")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/KSM")) AssetDatabase.CreateFolder("Assets/Resources", "KSM");

        var bank = AssetDatabase.LoadAssetAtPath<LcwSoundBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<LcwSoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }

        var so = new SerializedObject(bank);
        int ok = 0, missing = 0;
        foreach (var (field, file) in Map)
        {
            string path = Root + file;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var prop = so.FindProperty(field);
            if (clip == null || prop == null) { Debug.LogWarning("[LcwSoundBank] 연결 실패: " + field + " <- " + path + (prop == null ? " (칸 없음)" : " (파일 없음)")); missing++; continue; }
            prop.objectReferenceValue = clip;
            ok++;

            if (Streamed.Contains(field))
            {
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp != null)
                {
                    var s = imp.defaultSampleSettings;
                    if (s.loadType != AudioClipLoadType.Streaming)
                    {
                        s.loadType = AudioClipLoadType.Streaming;
                        imp.defaultSampleSettings = s;
                        imp.SaveAndReimport();
                    }
                }
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log("[LcwSoundBank] 연결 " + ok + "개, 실패 " + missing + "개 → " + BankPath);
    }
}
