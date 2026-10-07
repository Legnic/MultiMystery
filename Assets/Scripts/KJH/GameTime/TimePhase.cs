// 게임 안의 시간대. 위에서부터 순서대로 흐른다 (정오 → 저녁 → 자정).
// 값의 크기로 "이 시간대 이후인가"를 비교하므로, 새 시간대를 추가할 때는 흐르는 순서에 맞는 자리에 끼워 넣는다.
// (예: 정오 전에 "아침"을 넣으려면 Noon 위에 Morning을 추가하면 된다. 씬에는 이름이 아니라 숫자로 저장되므로,
//  중간에 끼워 넣으면 이미 설정해 둔 값이 한 칸씩 밀린다 → 추가 후 TimeUnlock 설정을 한 번 확인할 것)
public enum TimePhase
{
    Noon,     // 정오
    Evening,  // 저녁
    Midnight, // 자정
}

public static class TimePhaseNames
{
    // Inspector 버튼·로그에 보여줄 한국어 이름.
    public static string ToKorean(this TimePhase phase)
    {
        switch (phase)
        {
            case TimePhase.Noon: return "정오";
            case TimePhase.Evening: return "저녁";
            case TimePhase.Midnight: return "자정";
            default: return phase.ToString();
        }
    }
}
