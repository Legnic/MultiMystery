// GIM-002 「반주로 완성하는 인형극」에서 쓰는 ID와 상태 정의 모음.
// 이 폴더(Core)는 별도 어셈블리(Gim002.Core)라서 네트워크·플레이어 코드 없이도 컴파일되고,
// 그래서 EditMode 테스트가 판정 로직만 따로 검사할 수 있다.
namespace Gim002
{
    // 인형 4종. OwlBook(책 든 부엉이)은 어느 장면의 정답도 아닌 함정이다.
    public enum DollId { OwlScope, OwlBook, FrogCan, CatViolin }

    // 배경카드 5종. Sea(바다)·Castle(성)은 함정이다.
    public enum BackgroundId { NightForest, Greenhouse, MusicRoom, Sea, Castle }

    // 피아노 건반에 붙은 기호 5종 (해=도, 구름=레, 달=미, 꽃=파, 별=솔 — 가안).
    public enum SymbolId { Sun, Cloud, Moon, Flower, Star }

    // 기믹 전체 상태. 기획 6-3 상태 전이표의 LOCKED ~ COMPLETE와 1:1로 대응한다.
    public enum Gim002State
    {
        Locked,         // GIM-001이 아직 안 풀림
        Preparing,      // 음악실 문 열기 + 태엽 열쇠 사용을 기다리는 중
        CurtainClosed,  // 장면 N 시작 전, 손잡이를 돌려야 커튼이 열림
        Staging,        // 커튼 열림. 인형·배경카드를 꽂는 중
        Accompany,      // 무대 정답. 반주판이 열려 있고 피아노 입력을 받는 중
        Perform,        // 반주 정답. 인형 공연 중 (모든 입력 무시)
        Complete        // 3장면 모두 성공
    }

    // 판정 담당(호스트)이 모두에게 알리는 일회성 사건. 상태(Gim002SyncState)와 달리 "연출을 한 번 재생"하는 용도다.
    public enum Gim002NetEvent
    {
        StageWrong,   // EV_STAGE_WRONG  — 무대 오답
        StageRight,   // EV_STAGE_RIGHT  — 무대 정답 (반주판 열림)
        KeyPressed,   // EV_KEY_PRESSED  — 기호 건반이 기록됨 (값 = SymbolId)
        PianoWrong,   // EV_PIANO_WRONG  — 반주 오답
        SceneClear,   // EV_SCENE_CLEAR  — 장면 성공 (값 = 장면 번호)
        Complete      // EV_COMPLETE     — 기믹 완료
    }

    // 네트워크로 동기화할 상태 묶음 (기획 8장 "네트워크 동기화가 필요한 상태").
    // 클라이언트는 이 값만 보고 화면(커튼, 슬롯, 표시등 등)을 맞춘다.
    // 슬롯은 "비어 있음"을 표현해야 해서 enum 대신 int로 두고 -1을 빈 칸으로 쓴다(네트워크로 보내기도 쉽다).
    [System.Serializable]
    public struct Gim002SyncState
    {
        public const int Empty = -1;

        public Gim002State state;
        public int sceneIndex;      // 1~3 (진행 중이거나 다음에 할 장면)
        public int progressCount;   // 0~3 (성공한 장면 수 = 켜진 진행 표시등 수)
        public bool musicDoorOpened;
        public bool stageWound;
        public bool curtainOpen;
        public int slotDoll;        // DollId 값 또는 Empty
        public int slotBg;          // BackgroundId 값 또는 Empty
        public bool scoreboardOpen;

        // 슬롯 값을 enum?로 꺼내 쓰기 쉽게 해 주는 도우미
        public DollId? SlotDoll { get { return slotDoll < 0 ? (DollId?)null : (DollId)slotDoll; } }
        public BackgroundId? SlotBg { get { return slotBg < 0 ? (BackgroundId?)null : (BackgroundId)slotBg; } }

        public static int Encode(DollId? d) { return d.HasValue ? (int)d.Value : Empty; }
        public static int Encode(BackgroundId? b) { return b.HasValue ? (int)b.Value : Empty; }

        // 상태가 바뀌었는지 비교할 때 사용 (바뀐 경우에만 네트워크로 보내기 위해)
        public bool SameAs(Gim002SyncState o)
        {
            return state == o.state && sceneIndex == o.sceneIndex && progressCount == o.progressCount
                && musicDoorOpened == o.musicDoorOpened && stageWound == o.stageWound && curtainOpen == o.curtainOpen
                && slotDoll == o.slotDoll && slotBg == o.slotBg && scoreboardOpen == o.scoreboardOpen;
        }

        public override string ToString()
        {
            return $"{state} 장면{sceneIndex} 진행{progressCount}/3 문={musicDoorOpened} 태엽={stageWound} 커튼={curtainOpen} 인형={SlotDoll} 배경={SlotBg} 반주판={scoreboardOpen}";
        }
    }
}
