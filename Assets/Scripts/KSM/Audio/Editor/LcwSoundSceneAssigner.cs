using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 Tools/KSM/Audio/Assign LCW Sounds To Scene — 지금 열려 있는 씬(Develop)에서, KJH 기믹 컴포넌트가 이미 가지고 있는
// "소리 칸(AudioClip)"에 LCW 사운드를 채워 넣는다. 칸이 채워지면 그 컴포넌트가 내던 코드 임시 소리는 쓰이지 않는다.
// 소리 칸이 없는 상황(문, 서랍, 발소리, 엠비언스, 약품 장치, 미로 사건 등)은 LcwSoundDirector가 맡는다.
// 재실행 가능 — 머지로 씬 값이 날아갔을 때 다시 돌리면 된다.
public static class LcwSoundSceneAssigner
{
    const string SilencePath = "Assets/Sounds/KSM/Silence_0.1s.wav";

    [MenuItem("Tools/KSM/Audio/Assign LCW Sounds To Scene")]
    public static void Assign()
    {
        var bank = AssetDatabase.LoadAssetAtPath<LcwSoundBank>("Assets/Resources/KSM/LcwSoundBank.asset");
        if (bank == null) { Debug.LogError("[LcwSoundScene] 먼저 Tools/KSM/Audio/Build LCW Sound Bank를 실행하세요."); return; }
        var silence = EnsureSilence();

        int n = 0;

        // GIM-02 건반: 기호 순서(해·구름·달·꽃·별) = 도·레·미·파·솔. 켜져 있는 새 건반(PianoKey_01~05)과 옛 건반(Key_1~5) 둘 다.
        var notes = new[] { bank.pianoDo, bank.pianoRe, bank.pianoMi, bank.pianoFa, bank.pianoSol };
        foreach (var k in Object.FindObjectsByType<Gim002.Gim002PianoKey>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var m = Regex.Match(k.name, @"(\d+)$");
            int idx = m.Success ? int.Parse(m.Groups[1].Value) - 1 : -1;
            if (idx < 0 || idx >= notes.Length) { Debug.LogWarning("[LcwSoundScene] 건반 번호를 읽지 못함: " + k.name); continue; }
            n += Set(k, "note", notes[idx]);
        }

        // GIM-02: 태엽 열쇠/음악실 열쇠를 꽂을 때 = 열쇠 돌아가는 소리, 태엽 손잡이 = 오르골 돌아가는 소리, 인형 꽂는 자리 = 원목 인형 꽂기
        foreach (var t in Object.FindObjectsByType<Gim002.Gim002KeyTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(t, "useClip", bank.keyTurn);
        foreach (var c in Object.FindObjectsByType<Gim002.Gim002StageCrank>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(c, "turnClip", bank.musicBoxTurn);
        foreach (var s in Object.FindObjectsByType<Gim002.Gim002StageSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (s.Kind == Gim002.StageSlotKind.Doll) n += Set(s, "placeClip", bank.dollInsert);

        // GIM-02 층별 효과음: 오답(무대/반주)은 공통 오답 소리, 무대 정답·공연 시작·장면 성공은 전구 켜지는 소리 (두 층 스피커 모두)
        foreach (var sp in Object.FindObjectsByType<Gim002.Gim002FeedbackSpeaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            n += Set(sp, "stageWrongClip", bank.wrongInput);
            n += Set(sp, "pianoWrongClip", bank.wrongInput);
            n += Set(sp, "stageRightClip", bank.puppetBulbOn);
            n += Set(sp, "performStartClip", bank.puppetBulbOn);
            n += Set(sp, "sceneClearClip", bank.puppetBulbOn);
        }

        // GIM-04: 화분을 선반 자리에 내려놓는 소리, 오답
        foreach (var s in Object.FindObjectsByType<Gim004.Gim004ShelfSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(s, "placeClip", bank.potPutDown);
        foreach (var f in Object.FindObjectsByType<Gim004.Gim004ShelfFeedback>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(f, "wrongClip", bank.wrongInput);

        // GIM-05: 문양 패널의 놋쇠 버튼 누르는 소리, 오답, 덤불이 열릴 때 패널의 버튼 불빛, 덤불이 사라지는 소리, 미로 안 오답
        foreach (var p in Object.FindObjectsByType<Gim005.Gim005Panel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            n += Set(p, "clickSound", bank.brassButton);
            n += Set(p, "wrongSound", bank.wrongInput);
            n += Set(p, "openSound", bank.buttonLightOn);
        }
        foreach (var g in Object.FindObjectsByType<Gim005.Gim005Gate>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(g, "openSound", bank.bushVanish);
        foreach (var a in Object.FindObjectsByType<Gim005.Gim005MazeArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(a, "wrongClip", bank.wrongInput);

        // GIM-07: 발판이 내려앉는 소리, 라운드 실패(키를 틀리거나 시간 초과) = 미니게임 틀림 소리.
        // 완료 때 문 소리는 LcwSoundDirector가 "문이 실제로 열릴 때" 한 번만 내므로 여기서는 소리 없음(안 비우면 임시 소리가 난다).
        foreach (var w in Object.FindObjectsByType<Gim007.Gim007WeightPlate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            n += Set(w, "clankClip", bank.plateDown);
            n += Set(w, "doorOpenClip", silence);
        }
        foreach (var f in Object.FindObjectsByType<Gim007.Gim007Furniture>(FindObjectsInactive.Include, FindObjectsSortMode.None)) n += Set(f, "failClip", bank.minigameWrong);

        // GIM-03 약품 제조: 카탈로그 에셋(KJH 데이터)의 "펑 터짐(오답 조합)" = 공통 오답 소리, "성공(최종 혼합)" = 조합되는 소리
        foreach (var guid in AssetDatabase.FindAssets("t:PotionCatalog"))
        {
            var cat = AssetDatabase.LoadAssetAtPath<PotionCraft.PotionCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (cat == null) continue;
            n += Set(cat, "explodeClip", bank.wrongInput);
            n += Set(cat, "successClip", bank.combine);
            EditorUtility.SetDirty(cat);
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[LcwSoundScene] 소리 칸 " + n + "개를 채웠습니다. 씬을 저장하세요.");
    }

    static int Set(Object target, string field, AudioClip clip)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("[LcwSoundScene] 칸 없음: " + target.GetType().Name + "." + field); return 0; }
        if (p.objectReferenceValue == clip) return 0;
        p.objectReferenceValue = clip;
        so.ApplyModifiedProperties();
        return 1;
    }

    // 0.1초짜리 무음 wav (코드가 임시 소리를 내지 못하게 칸을 "비우지 않으면서" 소리는 없애는 용도)
    static AudioClip EnsureSilence()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SilencePath);
        if (clip != null) return clip;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SilencePath));
        const int rate = 22050, samples = 2205;
        using (var fs = new System.IO.FileStream(SilencePath, System.IO.FileMode.Create))
        using (var w = new System.IO.BinaryWriter(fs))
        {
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + samples * 2); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(samples * 2);
            for (int i = 0; i < samples; i++) w.Write((short)0);
        }
        AssetDatabase.ImportAsset(SilencePath);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(SilencePath);
    }
}
