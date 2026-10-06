using System;

// 플레이어가 가질 수 있는 "고유 능력" 목록.
// 2인 협동에서 플레이어마다 할 수 있는 일이 다르다 (정보 비대칭):
//   - 플레이어 A: SoundEcho  — 사물에 손을 얹어 소리를 듣는다 (ObjectEcho)
//   - 플레이어 B: PastPhoto  — 정해진 지점에서 과거 사진을 찍고, 찍은 사진을 본다 (PhotoSpot / 사진 인벤토리)
// 단서 살펴보기처럼 누구나 하는 행동은 여기에 넣지 않는다 (능력이 필요 없는 행동).
//
// [Flags]: 한 플레이어가 여러 능력을 함께 가질 수 있게 하는 표시. 값이 1, 2, 4, 8 ...(2의 거듭제곱)이어야
// "SoundEcho | PastPhoto"처럼 묶을 수 있다. 혼자 테스트할 때는 두 능력을 다 켜 둔다.
// 새 능력을 추가할 때: 다음 값(4, 8, 16 ...)으로 한 줄 추가하고, 그 능력이 필요한 대상에 IRequiresAbility를 붙인다.
[Flags]
public enum PlayerAbility
{
    None = 0,
    SoundEcho = 1 << 0, // 1
    PastPhoto = 1 << 1, // 2
}
