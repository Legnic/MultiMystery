using LGH;
using UnityEngine;

namespace AtticTreasure
{
    // 황금상·현관 열쇠를 "내려놓을" 때 쓰는 숨은 자리 (LGH WorldItem). 처음에는 모든 컴퓨터에서 숨겨 둔다.
    // LGH ItemNetworkSync는 아이템을 내려놓을 때 "같은 종류의 숨겨진 WorldItem"을 다시 꺼내 쓰므로,
    // 이게 있어야 인벤토리의 물건을 바닥에 내려놓거나, 나간 플레이어의 물건이 그 자리에 떨어질 수 있다.
    // 내려놓은 물건은 일반 아이템처럼 E로 다시 줍는다 (황금상 획득 연출은 처음 한 번만).
    // 주의: ItemNetworkSync가 없는 씬(현재 KJH·Develop)에서는 내려놓기 자체가 동작하지 않는다 (LGH 쪽 문제).
    [RequireComponent(typeof(WorldItem))]
    public class AtticTreasureHiddenAtStart : MonoBehaviour
    {
        void Start()
        {
            var w = GetComponent<WorldItem>();
            if (w.IsPresent) w.SetState(false, 1);
        }
    }
}
