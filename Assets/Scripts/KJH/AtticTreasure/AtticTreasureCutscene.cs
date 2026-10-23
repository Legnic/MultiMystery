using System;
using UnityEngine;

namespace AtticTreasure
{
    // 황금상 획득 연출의 "끼우는 자리". 실제 연출(타임라인, 별도 씬 등)은 이 클래스를 상속해서 만들고,
    // AtticTreasureSync의 Cutscene 칸에 연결하면 된다. 비워 두면 아래 임시 연출(AtticTreasurePlaceholderCutscene)이 쓰인다.
    //
    // 규칙:
    //  - Play는 두 사람 모두의 컴퓨터에서 불린다 (2026-10-23 결정: 연출은 두 사람 모두에게).
    //  - isPicker = 이 컴퓨터 플레이어가 황금상을 집은 사람인가. 인벤토리에는 집은 사람에게만 들어간다.
    //  - 연출이 끝나면 반드시 onFinished를 한 번 부를 것. 집은 사람 쪽 onFinished가 불려야 황금상이 인벤토리에 들어간다.
    //    (안 부르면 판정 담당이 cutsceneTimeoutSeconds 뒤에 강제로 넣는다)
    //  - 별도 씬으로 만들 경우: Play에서 SceneManager.LoadSceneAsync(이름, Additive)로 연출 씬을 열고,
    //    끝나면 UnloadSceneAsync 후 onFinished를 부르는 식으로 상속 클래스를 만들면 된다.
    public abstract class AtticTreasureCutscene : MonoBehaviour
    {
        public abstract void Play(bool isPicker, Action onFinished);
    }
}
