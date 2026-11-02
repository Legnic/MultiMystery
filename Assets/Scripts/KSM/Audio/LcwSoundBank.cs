using UnityEngine;

// LCW님이 만든 사운드(Assets/Sounds/LCW)를 "상황 이름"으로 묶어 둔 목록.
// 코드는 파일 이름이 아니라 이 목록의 칸 이름(doorOpen, footstep ...)으로 소리를 찾는다 — 소리를 바꾸고 싶으면 이 에셋의 칸만 바꾸면 된다.
// 에셋은 Resources에 두어서(Assets/Resources/KSM/LcwSoundBank) 씬/프리팹에 따로 연결하지 않아도 어디서든 LcwSfx.Bank로 쓸 수 있다.
// 채우는 방법: 메뉴 Tools/KSM/Audio/Build LCW Sound Bank (재실행 가능, 이미 연결한 칸은 파일 이름이 바뀌지 않는 한 그대로).
[CreateAssetMenu(fileName = "LcwSoundBank", menuName = "KSM/LCW Sound Bank")]
public class LcwSoundBank : ScriptableObject
{
    [Header("기본 사운드")]
    public AudioClip doorOpen;          // 문열리는소리1
    public AudioClip doorClose;         // 문닫는소리1
    public AudioClip doorLocked;        // 잠긴문1 — 잠긴 문을 열어 보려 할 때
    public AudioClip doorUnlock;        // 잠금풀림1 — 열쇠 없이(기믹 등으로) 문 잠금이 풀릴 때
    public AudioClip keyTurn;           // 열쇠돌아가는소리2 — 열쇠로 잠금을 풀 때
    public AudioClip drawerOpen;        // 서랍여는소리1
    public AudioClip drawerClose;       // 서랍닫는소리1
    public AudioClip footstep;          // 발소리수정3 — 한 걸음
    public AudioClip jumpLanding;       // 점프후착지음 — 점프 후 지면에 닿을 때
    public AudioClip paperPickup;       // 종이집어들기1
    public AudioClip smallMetalPickup;  // 작은금속물건집기1
    public AudioClip numpadPress1;      // 번호키누르는소리1   (검토 필요: 어디에 쓰는지)
    public AudioClip numpadPress2;      // 번호키누르는소리2   (검토 필요)
    public AudioClip numberLockTurn;    // 숫자자물쇠돌리기1   (검토 필요)
    public AudioClip wrongInput;        // 오답입력효과음      (검토 필요)
    public AudioClip candle;            // 촛불소리1           (검토 필요)

    [Header("게임 브금 / 엠비언스")]
    public AudioClip ambience;          // 인게임엠비언스찐최종 — 플레이 중 계속 깔리는 소리
    public AudioClip lobbyBgm;          // 대기실브금후보1     (검토 필요: 후보)

    [Header("GIM-01 초상화")]
    public AudioClip portraitRotate;    // 01초상화회전
    public AudioClip portraitLock;      // 01초상화회전고정    (검토 필요)
    public AudioClip keyBehindFrame;    // 01액자뒤음악실열쇠등장

    [Header("GIM-02 인형극")]
    public AudioClip pianoDo;           // 01_Do_C4
    public AudioClip pianoRe;           // 02_Re_D4
    public AudioClip pianoMi;           // 03_Mi_E4
    public AudioClip pianoFa;           // 04_Fa_F4
    public AudioClip pianoSol;          // 05_Sol_G4
    public AudioClip musicBoxTurn;      // 02오르골돌아가는소리
    public AudioClip dollInsert;        // 원목인형꽃기
    public AudioClip dollRemove;        // 인형빼기
    public AudioClip paperRemove;       // 종이빼기
    public AudioClip puppetBulbOn;      // 인형극정답전구켜지기

    [Header("GIM-03 약품 제조")]
    public AudioClip herbPlace;         // 03약초 놓기
    public AudioClip bottlePickup;      // 03유리병드는소리
    public AudioClip bottlePutDown;     // 03유리병내리는소리
    public AudioClip combine;           // 03조합되는소리      (검토 필요: 제조 시작 vs 최종 혼합)
    public AudioClip powderPour;        // 가루쏟는소리
    public AudioClip dumbwaiterUp;      // 도르레올라가는소리
    public AudioClip grind;             // 약갈기

    [Header("GIM-04 온실 화분")]
    public AudioClip potPickup;         // 화분들기1
    public AudioClip potPutDown;        // 화분내리기1
    public AudioClip platePressDown;    // 화분누름판내려감
    public AudioClip platePressUp;      // 화분누름판올라감

    [Header("GIM-05 정원 미로")]
    public AudioClip hedgeRegrow;       // 05덤불다시자라는소리
    public AudioClip mazeEndButton;     // 05미로끝버튼 (2)
    public AudioClip buttonLightOn;     // 05버튼불빛켜짐      (검토 필요)
    public AudioClip brassButton;       // 05벽의놋쇠버튼
    public AudioClip faint;             // 05정신잃는소리
    public AudioClip mazeBgm;           // 미로배경음
    public AudioClip barsRise;          // 철창올라가는소리
    public AudioClip bushVanish;        // 풀숲사라지는소리

    [Header("GIM-07 안방 가구")]
    public AudioClip plateDown;         // 07누름판내려감
    public AudioClip moveL;             // 07물건옮기기L       (검토 필요)
    public AudioClip moveR;             // 07물건옮기기R       (검토 필요)
    public AudioClip minigamePress;     // 07미니게임누름소리1 (검토 필요)
    public AudioClip minigameWrong;     // 07미니게임틀림소리1

    [Header("GIM-08 다락 / 현관")]
    public AudioClip finalDoor;         // 08마지막현관문효과음
    public AudioClip treasureOpen;      // 08보물상자열리는소리2
    public AudioClip telescope;         // 천체망원경소리1
}
