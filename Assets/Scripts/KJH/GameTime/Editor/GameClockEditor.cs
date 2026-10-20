using System;
using UnityEditor;
using UnityEngine;

// GameClock Inspector에 시간대 전환 버튼(정오/저녁/자정/다음)을 보여주는 에디터 도구.
// 시간이 흐르는 방식은 다른 곳에서 만들 예정이라, 그 전까지 테스트용으로 손으로 넘기기 위함이다.
// 버튼은 플레이 중에만 누를 수 있다 (시간대는 실행 중에만 존재하는 값이기 때문. 시작 시간대는 Start Phase로 정한다).
// Editor 폴더 안이라 게임 빌드에는 포함되지 않는다.
[CustomEditor(typeof(GameClock))]
public class GameClockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var clock = (GameClock)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("시간대 전환 (테스트용)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 버튼으로 시간대를 넘길 수 있습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("현재 시간대", clock.CurrentPhase.ToKorean());

        EditorGUILayout.BeginHorizontal();
        foreach (TimePhase phase in Enum.GetValues(typeof(TimePhase)))
        {
            // 지금 시간대 버튼은 눌린 것처럼 보이게 비활성화한다.
            using (new EditorGUI.DisabledScope(phase == clock.CurrentPhase))
            {
                if (GUILayout.Button(phase.ToKorean(), GUILayout.Height(28))) clock.SetPhase(phase);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("다음 시간대 ▶", GUILayout.Height(24))) clock.NextPhase();

        // 플레이 중 현재 시간대 표시가 바로 갱신되도록 계속 다시 그린다.
        Repaint();
    }
}
