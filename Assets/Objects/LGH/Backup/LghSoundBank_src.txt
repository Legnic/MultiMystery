using UnityEngine;

namespace LGH
{
    // Every sound the game plays from LGH scripts and from LghSoundDirector.
    // Lives at Assets/Objects/LGH/Resources/LGH_SoundBank.asset (loaded with Resources.Load).
    // The clips come from Assets/Sounds/LCW. Empty slots are simply silent.
    [CreateAssetMenu(fileName = "LGH_SoundBank", menuName = "LGH/Sound Bank")]
    public class LghSoundBank : ScriptableObject
    {
        [Header("BGM")]
        public AudioClip bgmLobby;          // 대기실브금후보1
        public AudioClip bgmGame;           // 인게임앰비언트후보1바람추가
        [Range(0f, 1f)] public float bgmLobbyVolume = 0.45f;
        [Range(0f, 1f)] public float bgmGameVolume = 0.35f;

        [Header("기본: 문 / 서랍 / 자물쇠")]
        public AudioClip doorOpen;          // 문열리는소리1
        public AudioClip doorClose;         // 문닫는소리1
        public AudioClip doorLocked;        // 잠긴문1
        public AudioClip keyTurn;           // 열쇠돌아가는소리2
        public AudioClip unlock;            // 잠금풀림1
        public AudioClip drawerOpen;        // 서랍여는소리1
        public AudioClip drawerClose;       // 서랍닫는소리1
        public AudioClip dialTurn;          // 숫자자물쇠돌리기1
        public AudioClip wrong;             // 오답입력효과음

        [Header("기본: 줍기 / 몸")]
        public AudioClip metalPickup;       // 작은금속물건집기1
        public AudioClip paperPickup;       // 종이집어들기1
        public AudioClip footstep;          // 발소리수정3
        public AudioClip candleLoop;        // 촛불소리1

        [Header("GIM-01 초상화")]
        public AudioClip portraitTurn;      // 01초상화회전
        public AudioClip portraitLock;      // 01초상화회전고정
        public AudioClip portraitKeyReveal; // 01액자뒤음악실열쇠등장

        [Header("GIM-02 인형극")]
        public AudioClip dollPull;          // 인형빼기
        public AudioClip paperPull;         // 종이빼기

        [Header("GIM-03 약 제조")]
        public AudioClip herbPlace;         // 03약초 놓기
        public AudioClip bottleUp;          // 03유리병드는소리
        public AudioClip bottleDown;        // 03유리병내리는소리
        public AudioClip powderPour;        // 가루쏟는소리
        public AudioClip grind;             // 약갈기

        [Header("GIM-04 화분")]
        public AudioClip potLift;           // 화분들기1
        public AudioClip plateDown;         // 화분누름판내려감
        public AudioClip plateUp;           // 화분누름판올라감

        [Header("GIM-05 미로")]
        public AudioClip mazeAmbient;       // 미로배경음
        public AudioClip faint;             // 05정신잃는소리
        public AudioClip bushRegrow;        // 05덤불다시자라는소리
        public AudioClip mazeEndButton;     // 05미로끝버튼

        [Header("GIM-07 가구 밀기")]
        public AudioClip minigamePress;     // 07미니게임누름소리1

        [Header("GIM-08 마지막")]
        public AudioClip telescope;         // 천체망원경소리1
        public AudioClip treasureOpen;      // 08보물상자열리는소리2
        public AudioClip frontDoorFinal;    // 08마지막현관문효과음

        static LghSoundBank cached;

        public static LghSoundBank Get
        {
            get
            {
                if (cached == null) cached = Resources.Load<LghSoundBank>("LGH_SoundBank");
                return cached;
            }
        }
    }
}
